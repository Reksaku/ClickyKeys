using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Input;

namespace ClickyKeys
{
    /// <summary>
    /// Privacy-safe global input statistics collector.
    ///
    /// Subscribes to <see cref="GlobalInputHook"/> events as a *second*
    /// consumer alongside <see cref="InputCounter"/>: while InputCounter
    /// only tracks keys the user explicitly mapped to a panel, this service
    /// tracks <em>every</em> key the hook reports — but only as aggregate
    /// counters.
    ///
    /// Privacy properties (intentional):
    /// <list type="bullet">
    /// <item>Only per-key COUNTERS are persisted. Never sequences, never
    /// per-event timestamps, never the foreground window or process name.
    /// A single coarse <c>last_updated_utc</c> is the only timing
    /// information in the file.</item>
    /// <item>The file is OVERWRITTEN in place every save (never appended),
    /// so an attacker reading historical snapshots cannot reconstruct
    /// ordering — only frequency.</item>
    /// <item>Saves happen on a fixed 30 s timer regardless of typing burst
    /// patterns (the timer is decoupled from input events, so file-mtime
    /// jitter does not leak typing rhythm).</item>
    /// <item>Auto-repeat is collapsed to a single press (mirrors
    /// <see cref="InputCounter"/>), so holding a key down does not inflate
    /// counts.</item>
    /// <item>Saves are skipped when nothing has changed since the last
    /// write — no spurious file-mtime updates that could hint at user
    /// activity.</item>
    /// <item>The hook callback path stays a pure in-memory increment;
    /// serialization and disk I/O happen on the timer thread, never inside
    /// the low-level hook callback (which has a strict OS timeout).</item>
    /// </list>
    ///
    /// File location: <c>%APPDATA%\ClickyKeys\keystats.json</c> — the
    /// app's root data folder, one level above the per-profile
    /// <c>settings\</c> subfolder so the stats are not tied to a particular
    /// settings profile. Atomic writes via <see cref="AtomicFile"/>, so a
    /// crash mid-write cannot corrupt the file.
    /// </summary>
    public sealed class KeyStatsService : IDisposable
    {
        // ---- Configuration ------------------------------------------------

        private const string FileName = "keystats.json";
        private const string AppName = "ClickyKeys";
        private const string SchemaVersion = "1.0";
        private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(30);

        private static readonly JsonSerializerOptions JsonWriteOpts = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };

        private static readonly JsonSerializerOptions JsonReadOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        // ---- State --------------------------------------------------------

        private readonly string _filePath;
        private readonly Timer _saveTimer;
        private readonly object _saveLock = new();

        // ConcurrentDictionary lets the hook callback (whichever thread the
        // hook pumps on) and the timer thread share state without explicit
        // locks on the hot path.
        private readonly ConcurrentDictionary<Key, long> _keyCounts = new();
        private readonly ConcurrentDictionary<MouseButton, long> _mouseCounts = new();
        private readonly ConcurrentDictionary<InputType, long> _wheelCounts = new();

        // Gamepad buttons are keyed by their friendly name (e.g. "A", "LB",
        // "Button 5") — already display-ready, so the Stats view uses the key
        // as-is. Different controllers that report the same button aggregate
        // together, which is what an aggregate stat wants.
        private readonly ConcurrentDictionary<string, long> _gamepadCounts = new();

        // Auto-repeat dedupe — mirrors InputCounter._pressed semantics so
        // both views of the world report consistent numbers.
        private readonly HashSet<Key> _pressed = new();
        private readonly object _pressedLock = new();

        // Single dirty counter bumped on every accepted event. Saver
        // compares current vs. last-saved to skip writes when nothing
        // changed since the previous tick.
        private long _dirtyTicks;
        private long _lastSavedDirtyTicks;

        // Aggregate totals — kept as separate longs so we don't have to sum
        // the dictionaries on every save.
        private long _totalKeyPresses;
        private long _totalMouseClicks;
        private long _totalWheelTicks;
        private long _totalGamepadPresses;

        private DateTime _firstRecordedUtc;

        private bool _disposed;
        private bool _started;

        // Master collection switch (the "Collect key-press statistics" setting).
        // volatile so the hook-callback thread sees toggles made on the UI
        // thread without a lock on the hot path. When false, the event handlers
        // early-return, so nothing is counted and — because _dirtyTicks never
        // advances — the save timer also stops writing the file. In-memory
        // (and on-disk) cumulative totals are preserved untouched, so flipping
        // the switch back on resumes from where it left off.
        private volatile bool _collecting = true;

        // ---- Construction -------------------------------------------------

        public KeyStatsService()
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppName);
            Directory.CreateDirectory(appDataDir);
            _filePath = Path.Combine(appDataDir, FileName);

            // Restore the previous session's counters before we start
            // accepting events, so cumulative stats survive restarts.
            TryLoad();

            if (_firstRecordedUtc == default)
                _firstRecordedUtc = DateTime.UtcNow;

            // Created in a stopped state; Start() arms it.
            _saveTimer = new Timer(
                OnSaveTimerTick,
                state: null,
                dueTime: Timeout.InfiniteTimeSpan,
                period: Timeout.InfiniteTimeSpan);
        }

        // ---- Public API ---------------------------------------------------

        /// <summary>
        /// Begin collecting input and arm the periodic save timer.
        /// Idempotent — calling twice is a no-op.
        /// </summary>
        public void Start()
        {
            if (_started || _disposed) return;
            _started = true;

            GlobalInputHook.Instance.KeyDown += OnKeyDown;
            GlobalInputHook.Instance.KeyUp += OnKeyUp;
            GlobalInputHook.Instance.MouseDown += OnMouseDown;
            GlobalInputHook.Instance.Wheel += OnWheel;

            GamepadInputService.Instance.ButtonPressed += OnGamepadButtonPressed;

            _saveTimer.Change(SaveInterval, SaveInterval);
        }

        /// <summary>
        /// Whether input statistics are currently being collected.
        /// </summary>
        public bool IsCollecting => _collecting;

        /// <summary>
        /// Sets the initial collection state from persisted config. Intended to
        /// be called once, before <see cref="Start"/>, and has no side effects
        /// (no flush, no cleanup) — it just seeds the switch so the first event
        /// after Start is handled correctly.
        /// </summary>
        public void ConfigureCollecting(bool enabled) => _collecting = enabled;

        /// <summary>
        /// Runtime toggle for the "Collect key-press statistics" setting. Safe
        /// to call from the UI thread while the hook is live.
        ///
        /// Turning collection OFF: drops any in-flight auto-repeat state (so a
        /// key still physically held when the user disables won't be mis-deduped
        /// later) and flushes once, so the file reflects everything counted up
        /// to this moment. After that, no further writes happen while off
        /// because no new events advance the dirty counter.
        ///
        /// Turning collection ON: simply resumes counting; cumulative totals
        /// loaded at startup (and anything counted earlier this session) are
        /// preserved, so the numbers continue rather than restart.
        /// </summary>
        public void SetCollecting(bool enabled)
        {
            if (_collecting == enabled) return;
            _collecting = enabled;

            if (!enabled)
            {
                lock (_pressedLock) { _pressed.Clear(); }
                try { SaveIfDirty(); }
                catch (Exception ex) { Debug.WriteLine($"KeyStatsService flush on disable failed: {ex}"); }
            }
        }

        /// <summary>
        /// Force a flush to disk. Call on app shutdown so the most recent
        /// up-to-30-seconds of activity is preserved.
        /// </summary>
        public void Flush() => SaveIfDirty();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // Stop scheduled saves first so the final flush below cannot
            // race with an in-flight tick.
            _saveTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _saveTimer.Dispose();

            if (_started)
            {
                GlobalInputHook.Instance.KeyDown -= OnKeyDown;
                GlobalInputHook.Instance.KeyUp -= OnKeyUp;
                GlobalInputHook.Instance.MouseDown -= OnMouseDown;
                GlobalInputHook.Instance.Wheel -= OnWheel;

                GamepadInputService.Instance.ButtonPressed -= OnGamepadButtonPressed;
            }

            // Final flush so even an ungraceful shutdown preserves the
            // latest counts. Swallow exceptions — we're tearing down.
            try { SaveIfDirty(); }
            catch (Exception ex) { Debug.WriteLine($"KeyStatsService final flush failed: {ex}"); }
        }

        // ---- Event handlers ----------------------------------------------

        private void OnKeyDown(Key key)
        {
            if (!_collecting) return;
            if (key == Key.None) return;

            // Auto-repeat dedupe. Hardware repeats fire WM_KEYDOWN at the
            // LL hook layer, which would otherwise inflate counts when a
            // key is held down (e.g. holding Backspace).
            lock (_pressedLock)
            {
                if (!_pressed.Add(key)) return;
            }

            _keyCounts.AddOrUpdate(key, 1, (_, v) => v + 1);
            Interlocked.Increment(ref _totalKeyPresses);
            Interlocked.Increment(ref _dirtyTicks);
        }

        private void OnKeyUp(Key key)
        {
            // Released-key bookkeeping only; harmless to run while paused, but
            // skip it too so a disabled collector touches no shared state.
            if (!_collecting) return;
            if (key == Key.None) return;
            lock (_pressedLock) { _pressed.Remove(key); }
        }

        private void OnMouseDown(MouseButton btn)
        {
            if (!_collecting) return;
            _mouseCounts.AddOrUpdate(btn, 1, (_, v) => v + 1);
            Interlocked.Increment(ref _totalMouseClicks);
            Interlocked.Increment(ref _dirtyTicks);
        }

        private void OnWheel(InputType direction)
        {
            if (!_collecting) return;
            _wheelCounts.AddOrUpdate(direction, 1, (_, v) => v + 1);
            Interlocked.Increment(ref _totalWheelTicks);
            Interlocked.Increment(ref _dirtyTicks);
        }

        // Raised on the gamepad poll thread. No auto-repeat dedupe is needed:
        // the service already only fires on rising edges. Shared state is
        // concurrent/interlocked, so no marshaling is required.
        private void OnGamepadButtonPressed(object? sender, GamepadButtonEventArgs e)
        {
            if (!_collecting) return;
            _gamepadCounts.AddOrUpdate(e.ButtonName, 1, (_, v) => v + 1);
            Interlocked.Increment(ref _totalGamepadPresses);
            Interlocked.Increment(ref _dirtyTicks);
        }

        // ---- Persistence --------------------------------------------------

        private void OnSaveTimerTick(object? state) => SaveIfDirty();

        private void SaveIfDirty()
        {
            // Single-threaded save section: timer ticks could overlap if
            // the disk is slow. TryEnter so an overlapping tick simply
            // skips instead of queueing more I/O.
            if (!Monitor.TryEnter(_saveLock)) return;
            try
            {
                long current = Interlocked.Read(ref _dirtyTicks);
                if (current == Interlocked.Read(ref _lastSavedDirtyTicks))
                    return; // No new input since last save — nothing to do.

                var snapshot = BuildSnapshot();
                var json = JsonSerializer.Serialize(snapshot, JsonWriteOpts);
                AtomicFile.WriteAllText(_filePath, json);

                Interlocked.Exchange(ref _lastSavedDirtyTicks, current);
            }
            catch (Exception ex)
            {
                // Persistence failures are non-fatal — counters keep
                // accumulating in memory and the next tick retries.
                Debug.WriteLine($"KeyStatsService save failed: {ex}");
            }
            finally
            {
                Monitor.Exit(_saveLock);
            }
        }

        private KeyStatsSnapshot BuildSnapshot()
        {
            var snap = new KeyStatsSnapshot
            {
                Version = SchemaVersion,
                FirstRecordedUtc = _firstRecordedUtc,
                LastUpdatedUtc = DateTime.UtcNow,
                TotalKeyPresses = Interlocked.Read(ref _totalKeyPresses),
                TotalMouseClicks = Interlocked.Read(ref _totalMouseClicks),
                TotalWheelTicks = Interlocked.Read(ref _totalWheelTicks),
                TotalGamepadPresses = Interlocked.Read(ref _totalGamepadPresses),
            };

            // Sort entries for stable file diffs — makes the JSON friendly
            // to human inspection and to any version-control style tooling
            // the user might point at the file.
            foreach (var kv in _keyCounts.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
                snap.Keys[kv.Key.ToString()] = kv.Value;

            foreach (var kv in _mouseCounts.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
                snap.Mouse[kv.Key.ToString()] = kv.Value;

            foreach (var kv in _wheelCounts.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal))
                snap.Wheel[kv.Key.ToString()] = kv.Value;

            foreach (var kv in _gamepadCounts.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                snap.Gamepad[kv.Key] = kv.Value;

            return snap;
        }

        private void TryLoad()
        {
            if (!File.Exists(_filePath)) return;

            try
            {
                var json = File.ReadAllText(_filePath);
                var snap = JsonSerializer.Deserialize<KeyStatsSnapshot>(json, JsonReadOpts);
                if (snap == null) return;

                _firstRecordedUtc = snap.FirstRecordedUtc == default
                    ? DateTime.UtcNow
                    : snap.FirstRecordedUtc;

                _totalKeyPresses = snap.TotalKeyPresses;
                _totalMouseClicks = snap.TotalMouseClicks;
                _totalWheelTicks = snap.TotalWheelTicks;
                _totalGamepadPresses = snap.TotalGamepadPresses;

                foreach (var kv in snap.Keys)
                    if (Enum.TryParse<Key>(kv.Key, out var k))
                        _keyCounts[k] = kv.Value;

                foreach (var kv in snap.Mouse)
                    if (Enum.TryParse<MouseButton>(kv.Key, out var b))
                        _mouseCounts[b] = kv.Value;

                foreach (var kv in snap.Wheel)
                    if (Enum.TryParse<InputType>(kv.Key, out var d))
                        _wheelCounts[d] = kv.Value;

                // Gamepad keys are already friendly-name strings — stored as-is.
                foreach (var kv in snap.Gamepad)
                    _gamepadCounts[kv.Key] = kv.Value;

                // Loaded state matches what is on disk — don't trigger an
                // immediate re-save until at least one new event arrives.
                Interlocked.Exchange(ref _lastSavedDirtyTicks, Interlocked.Read(ref _dirtyTicks));
            }
            catch (Exception ex)
            {
                // Corrupt or schema-mismatched file — start fresh rather
                // than crashing. Existing file will be overwritten on the
                // next successful save.
                Debug.WriteLine($"KeyStatsService load failed, starting fresh: {ex}");
            }
        }
    }

    /// <summary>
    /// On-disk schema for <see cref="KeyStatsService"/>. Plain JSON, kept
    /// human-readable so a curious user can open the file in Notepad and
    /// verify exactly what is being recorded — only counters, no sequences.
    /// </summary>
    public sealed class KeyStatsSnapshot
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("first_recorded_utc")]
        public DateTime FirstRecordedUtc { get; set; }

        [JsonPropertyName("last_updated_utc")]
        public DateTime LastUpdatedUtc { get; set; }

        [JsonPropertyName("total_key_presses")]
        public long TotalKeyPresses { get; set; }

        [JsonPropertyName("total_mouse_clicks")]
        public long TotalMouseClicks { get; set; }

        [JsonPropertyName("total_wheel_ticks")]
        public long TotalWheelTicks { get; set; }

        [JsonPropertyName("total_gamepad_presses")]
        public long TotalGamepadPresses { get; set; }

        [JsonPropertyName("keys")]
        public Dictionary<string, long> Keys { get; set; } = new();

        [JsonPropertyName("mouse")]
        public Dictionary<string, long> Mouse { get; set; } = new();

        [JsonPropertyName("wheel")]
        public Dictionary<string, long> Wheel { get; set; } = new();

        [JsonPropertyName("gamepad")]
        public Dictionary<string, long> Gamepad { get; set; } = new();
    }
}
