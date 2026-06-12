using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using System.Windows.Input;

namespace ClickyKeys
{

    internal class PanelsService
    {
        // The currently active profile file. No longer readonly: switching
        // panel profiles repoints this at another file in PanelsDirectory.
        private string _filePath;
        private readonly object _lock = new();

        /// <summary>
        /// Folder holding every panels profile (.json). Created on first
        /// access. Shared by <see cref="PanelLoader"/> and MainWindow so they
        /// resolve the same location.
        /// </summary>
        public static string PanelsDirectory
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ClickyKeys", "panels");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>The default profile file name, always present on disk.</summary>
        public const string DefaultProfileFileName = "default panels.json";

        /// <summary>File name (with extension) of the active profile.</summary>
        public string ActiveFileName => Path.GetFileName(_filePath);

        private static readonly JsonSerializerOptions JsonOptions = new()

        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // Debounce plumbing — same shape as AppearanceService.
        private readonly object _debounceLock = new();
        private CancellationTokenSource? _debounceCts;
        private Task _pendingSave = Task.CompletedTask;

        public PanelsService(string appName = "ClickyKeys")
        {
            _filePath = Path.Combine(PanelsDirectory, DefaultProfileFileName);

            if (!File.Exists(_filePath))
            {
                // create default 100 panels
                var def = new PanelState();
                def.Version = new Configuration().Version;
                DeaultWSAD(def);
                for (int i = 4; i < 100; i++)
                {
                    def.Panels.Add(new PanelsSettings
                    {
                        Index = i,
                        KeyCode = Key.None,
                        Input = InputType.None,
                        Description = ""
                    });
                }
                Save(def);
            }
        }
        private void DeaultWSAD(PanelState def)
        {
            def.Panels.Add(new PanelsSettings { Index = 0, KeyCode = Key.W, Description = "W" });
            def.Panels.Add(new PanelsSettings { Index = 1, KeyCode = Key.S, Description = "S" });
            def.Panels.Add(new PanelsSettings { Index = 2, KeyCode = Key.A, Description = "A" });
            def.Panels.Add(new PanelsSettings { Index = 3, KeyCode = Key.D, Description = "D" });
        }

        /// <summary>
        /// Synchronous atomic save. Uses <see cref="AtomicFile"/> so a crash
        /// during the write can't leave a truncated JSON file that the next
        /// Load() would fail on.
        /// </summary>
        public void Save(PanelState state)
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            lock (_lock)
            {
                AtomicFile.WriteAllText(_filePath, json);
            }
        }

        /// <summary>
        /// Async atomic save for UI callers that don't want to block.
        /// </summary>
        public async Task SaveAsync(PanelState state, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await AtomicFile.WriteAllTextAsync(_filePath, json, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Debounced save — coalesces bursts of edits into one trailing write.
        /// </summary>
        public void SaveDebounced(PanelState state, TimeSpan? delay = null)
        {
            var wait = delay ?? TimeSpan.FromMilliseconds(500);

            lock (_debounceLock)
            {
                _debounceCts?.Cancel();
                _debounceCts = new CancellationTokenSource();
                var token = _debounceCts.Token;

                _pendingSave = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(wait, token).ConfigureAwait(false);
                        await SaveAsync(state, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"PanelsService.SaveDebounced failed: {ex}");
                    }
                }, token);
            }
        }

        /// <summary>
        /// Awaits any in-flight debounced save. Call on shutdown.
        /// </summary>
        public Task FlushAsync()
        {
            Task pending;
            lock (_debounceLock) { pending = _pendingSave; }
            return pending;
        }

        public PanelState Load() => LoadFromPath(_filePath);

        /// <summary>
        /// Reads and validates a specific profile file WITHOUT changing the
        /// active target. Used to preview a profile before it is committed.
        /// </summary>
        public PanelState LoadFromPath(string fullPath)
        {
            lock (_lock)
            {
                var json = File.ReadAllText(fullPath);
                return ValidateFormat(JsonSerializer.Deserialize<PanelState>(json, JsonOptions)
                       ?? new PanelState());
            }
        }

        /// <summary>
        /// Writes <paramref name="state"/> to a specific file (used by
        /// "Save As" to create a new profile) without changing the active
        /// target.
        /// </summary>
        public void SaveToPath(PanelState state, string fullPath)
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            lock (_lock)
            {
                AtomicFile.WriteAllText(fullPath, json);
            }
        }

        /// <summary>
        /// Repoints the service at another profile file. Any pending debounced
        /// write to the previous file is flushed first so it lands on the OLD
        /// path (SaveAsync resolves <c>_filePath</c> at execution time).
        /// </summary>
        public void SetActivePath(string fullPath)
        {
            try { FlushAsync().Wait(TimeSpan.FromSeconds(2)); }
            catch (Exception ex) { Debug.WriteLine($"PanelsService.SetActivePath flush failed: {ex}"); }

            lock (_lock)
            {
                _filePath = fullPath;
            }
        }

        /// <summary>
        /// Full paths of every *.json profile in <see cref="PanelsDirectory"/>.
        /// </summary>
        public static IEnumerable<string> EnumerateProfilePaths() =>
            Directory.EnumerateFiles(PanelsDirectory, "*.json", SearchOption.TopDirectoryOnly);
        private PanelState ValidateFormat(PanelState fileData)
        {
            Dictionary<int, Key> _key = new();
            foreach (var (panel, idx) in fileData.Panels.Select((p, i) => (p, i)))
            {
                bool duplicate = false;
                if (panel.Input == InputType.Key && panel.KeyCode != Key.None)
                {

                    foreach (var (_, name) in _key)
                    {
                        if (name == panel.KeyCode)
                        {
                            duplicate = true;
                            fileData.Panels[idx].KeyCode = Key.None;
                            fileData.Panels[idx].Description = "";
                        }
                    }
                    if (duplicate == false)
                    {
                        _key[idx] = panel.KeyCode;
                    }
                }
            }

            return fileData;
        }
    }


}
