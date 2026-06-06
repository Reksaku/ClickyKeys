using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace ClickyKeys
{
    /// <summary>
    /// Tracks how long the application has been running, cumulatively across
    /// sessions. Deliberately mirrors <see cref="KeyStatsService"/> in shape
    /// and lifecycle so the two are easy to reason about together:
    ///
    /// <list type="bullet">
    /// <item>Self-contained service started from <see cref="App.OnStartup"/>
    /// and disposed in <see cref="App.OnExit"/>.</item>
    /// <item>A master <c>_collecting</c> switch (the "Collect application uptime
    /// data" setting) gates accumulation. Off ⇒ time stops advancing and the
    /// file is no longer written.</item>
    /// <item>Periodic, decoupled saves on a fixed timer; loads prior total on
    /// construction so the counter survives restarts.</item>
    /// <item>Atomic writes via <see cref="AtomicFile"/>.</item>
    /// </list>
    ///
    /// Unlike key stats there is nothing privacy-sensitive about wall-clock
    /// uptime, so — while collecting — we DO write on every tick (the value is
    /// always advancing). When collection is off, nothing changes and the saver
    /// skips the write, so the file mtime stays put.
    ///
    /// Timing model: a <see cref="Stopwatch"/> measures the current run segment;
    /// folding adds the elapsed segment into <c>_baseSeconds</c> (the persisted
    /// cumulative total) and restarts the segment. We use a monotonic Stopwatch
    /// rather than DateTime subtraction so system clock changes / DST don't
    /// corrupt the count.
    ///
    /// File location: <c>%APPDATA%\ClickyKeys\uptime.json</c> — the same data
    /// folder as keystats.json, alongside the rest of the app's files.
    /// </summary>
    public sealed class UptimeStatsService : IDisposable
    {
        // ---- Configuration ------------------------------------------------

        private const string FileName = "uptime.json";
        private const string AppName = "ClickyKeys";
        private const string SchemaVersion = "1.0";
        private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

        private static readonly JsonSerializerOptions JsonWriteOpts = new()
        {
            WriteIndented = true,
        };

        private static readonly JsonSerializerOptions JsonReadOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        // ---- State --------------------------------------------------------

        private readonly string _filePath;
        private readonly Timer _saveTimer;
        private readonly object _saveLock = new();

        // Guards the timing state (_baseSeconds + _sw). Short critical sections
        // only — never held across disk I/O.
        private readonly object _timeLock = new();

        // Persisted cumulative seconds (everything folded so far). The current
        // unfolded run segment lives in _sw.
        private double _baseSeconds;

        // Monotonic timer for the current run segment. Running only while
        // collection is active.
        private readonly Stopwatch _sw = new();

        // Whole-seconds value last written to disk, for dirty-skip.
        private long _lastSavedWholeSeconds = -1;

        private DateTime _firstRecordedUtc;

        private bool _disposed;
        private bool _started;

        // Master collection switch (the "Collect application uptime data"
        // setting). volatile for cheap cross-thread reads; all mutations that
        // touch the stopwatch go through _timeLock.
        private volatile bool _collecting = true;

        // ---- Construction -------------------------------------------------

        public UptimeStatsService()
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName);
            Directory.CreateDirectory(appDataDir);
            _filePath = Path.Combine(appDataDir, FileName);

            TryLoad();

            if (_firstRecordedUtc == default)
                _firstRecordedUtc = DateTime.UtcNow;

            // Created stopped; Start() arms it.
            _saveTimer = new Timer(
                OnSaveTimerTick,
                state: null,
                dueTime: Timeout.InfiniteTimeSpan,
                period: Timeout.InfiniteTimeSpan);
        }

        // ---- Public API ---------------------------------------------------

        /// <summary>
        /// Current cumulative uptime, including the in-progress run segment.
        /// Safe to read at any time.
        /// </summary>
        public TimeSpan TotalUptime
        {
            get
            {
                lock (_timeLock)
                {
                    var live = _sw.IsRunning ? _sw.Elapsed.TotalSeconds : 0;
                    return TimeSpan.FromSeconds(_baseSeconds + live);
                }
            }
        }

        /// <summary>Whether uptime is currently being accumulated.</summary>
        public bool IsCollecting => _collecting;

        /// <summary>
        /// Seeds the collection state from persisted config. Call once before
        /// <see cref="Start"/>; no side effects.
        /// </summary>
        public void ConfigureCollecting(bool enabled) => _collecting = enabled;

        /// <summary>
        /// Begin (or resume) accumulating uptime and arm the save timer.
        /// Idempotent.
        /// </summary>
        public void Start()
        {
            if (_started || _disposed) return;
            _started = true;

            lock (_timeLock)
            {
                if (_collecting)
                    _sw.Restart();
            }

            _saveTimer.Change(SaveInterval, SaveInterval);
        }

        /// <summary>
        /// Runtime toggle for the "Collect application uptime data" setting.
        ///
        /// Turning OFF: folds the current run segment into the cumulative total,
        /// stops the stopwatch, and flushes once so the file reflects time up to
        /// this moment. No further writes happen while off.
        ///
        /// Turning ON: starts a fresh run segment; the cumulative total carries
        /// over, so the counter continues rather than restarts.
        /// </summary>
        public void SetCollecting(bool enabled)
        {
            bool changedToOff = false;

            lock (_timeLock)
            {
                if (_collecting == enabled) return;
                _collecting = enabled;

                if (enabled)
                {
                    // Only actually run the stopwatch once the service has been
                    // started; before Start() we just record intent.
                    if (_started)
                        _sw.Restart();
                }
                else
                {
                    FoldNoLock();
                    _sw.Reset();
                    changedToOff = true;
                }
            }

            if (changedToOff)
            {
                try { SaveIfDirty(); }
                catch (Exception ex) { Debug.WriteLine($"UptimeStatsService flush on disable failed: {ex}"); }
            }
        }

        /// <summary>Force a flush to disk (e.g. on shutdown).</summary>
        public void Flush() => SaveIfDirty();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _saveTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _saveTimer.Dispose();

            // Fold the final segment and write it out.
            try { SaveIfDirty(); }
            catch (Exception ex) { Debug.WriteLine($"UptimeStatsService final flush failed: {ex}"); }
        }

        // ---- Timing -------------------------------------------------------

        /// <summary>
        /// Adds the elapsed run segment into the cumulative total and restarts
        /// the segment. Caller MUST hold <see cref="_timeLock"/>. No-op when the
        /// stopwatch isn't running (i.e. collection is off).
        /// </summary>
        private void FoldNoLock()
        {
            if (_sw.IsRunning)
            {
                _baseSeconds += _sw.Elapsed.TotalSeconds;
                _sw.Restart();
            }
        }

        // ---- Persistence --------------------------------------------------

        private void OnSaveTimerTick(object? state) => SaveIfDirty();

        private void SaveIfDirty()
        {
            // Overlapping ticks simply skip rather than queueing I/O.
            if (!Monitor.TryEnter(_saveLock)) return;
            try
            {
                long whole;
                DateTime firstUtc;
                lock (_timeLock)
                {
                    FoldNoLock();
                    whole = (long)Math.Round(_baseSeconds);
                    firstUtc = _firstRecordedUtc;
                }

                // Nothing changed since the last write (collection off, or less
                // than a whole second elapsed) — skip to avoid pointless I/O.
                if (whole == Interlocked.Read(ref _lastSavedWholeSeconds))
                    return;

                var snapshot = new UptimeSnapshot
                {
                    Version = SchemaVersion,
                    FirstRecordedUtc = firstUtc,
                    LastUpdatedUtc = DateTime.UtcNow,
                    TotalSeconds = whole,
                };

                var json = JsonSerializer.Serialize(snapshot, JsonWriteOpts);
                AtomicFile.WriteAllText(_filePath, json);

                Interlocked.Exchange(ref _lastSavedWholeSeconds, whole);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UptimeStatsService save failed: {ex}");
            }
            finally
            {
                Monitor.Exit(_saveLock);
            }
        }

        private void TryLoad()
        {
            if (!File.Exists(_filePath)) return;

            try
            {
                var json = File.ReadAllText(_filePath);
                var snap = JsonSerializer.Deserialize<UptimeSnapshot>(json, JsonReadOpts);
                if (snap == null) return;

                _baseSeconds = snap.TotalSeconds;
                _firstRecordedUtc = snap.FirstRecordedUtc == default
                    ? DateTime.UtcNow
                    : snap.FirstRecordedUtc;

                // Loaded state matches disk — don't trigger an immediate
                // re-save until at least one whole second has accumulated.
                Interlocked.Exchange(ref _lastSavedWholeSeconds, (long)Math.Round(_baseSeconds));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UptimeStatsService load failed, starting fresh: {ex}");
            }
        }
    }

    /// <summary>
    /// On-disk schema for <see cref="UptimeStatsService"/>. Plain, human-
    /// readable JSON: a single cumulative seconds counter plus coarse
    /// first/last timestamps.
    /// </summary>
    public sealed class UptimeSnapshot
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("first_recorded_utc")]
        public DateTime FirstRecordedUtc { get; set; }

        [JsonPropertyName("last_updated_utc")]
        public DateTime LastUpdatedUtc { get; set; }

        [JsonPropertyName("total_seconds")]
        public long TotalSeconds { get; set; }
    }
}
