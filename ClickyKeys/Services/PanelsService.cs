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
        private readonly string _filePath;
        private readonly object _lock = new();

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
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName,"panels");

            Directory.CreateDirectory(appDataDir);

            _filePath = Path.Combine(appDataDir, "default panels.json");

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

        public PanelState Load()
        {
            lock (_lock)
            {
                var json = File.ReadAllText(_filePath);
                return ValidateFormat(JsonSerializer.Deserialize<PanelState>(json, JsonOptions)
                       ?? new PanelState());
            }
        }
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
