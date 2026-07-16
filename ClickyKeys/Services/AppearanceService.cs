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

namespace ClickyKeys
{
    public class AppearanceService
    {

        private readonly string _filePath;
        private readonly object _lock = new();
        string appName = "ClickyKeys";

        // Cached once per process — constructing JsonSerializerOptions on every
        // save is expensive and (before .NET 7) warmed a new serialization
        // cache per instance.
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // Debounce plumbing. Callers fire SaveDebounced as often as they like;
        // only the latest payload actually hits disk, after a short quiet
        // period. FlushAsync lets shutdown wait for the trailing write.
        private readonly object _debounceLock = new();
        private CancellationTokenSource? _debounceCts;
        private Task _pendingSave = Task.CompletedTask;

        public AppearanceService(string file)
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName, "settings");

            Directory.CreateDirectory(appDataDir);

            _filePath = Path.Combine(appDataDir, file);



            if (!File.Exists(_filePath))
            {
                Save(new AppearanceConfiguration());
            }
        }

        public AppearanceConfiguration Load() // Load data from settings.json
        {
            lock (_lock)
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<AppearanceConfiguration>(json, ReadOptions)
                           ?? new AppearanceConfiguration();
                }
                catch
                {
                    // Return to defaults
                    var defaults = new AppearanceConfiguration();
                    defaults.Version = new Configuration().Version;
                    Save(defaults);
                    return defaults;
                }
            }
        }


        /// <summary>
        /// Synchronous atomic save. Writes to a sibling ".tmp" file and moves
        /// it over the target, so a crash mid-write can't leave an invalid
        /// JSON blob behind.
        /// </summary>
        public void Save(AppearanceConfiguration appearance)
        {
            var json = JsonSerializer.Serialize(appearance, WriteOptions);
            lock (_lock)
            {
                AtomicFile.WriteAllText(_filePath, json);
            }
        }

        /// <summary>
        /// Async atomic save. Serialization runs on the caller's context, then
        /// the actual disk write happens off the UI thread. Safe to call from
        /// UI event handlers with <c>await</c>.
        /// </summary>
        public async Task SaveAsync(AppearanceConfiguration appearance, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(appearance, WriteOptions);
            await AtomicFile.WriteAllTextAsync(_filePath, json, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Coalesces rapid-fire saves (e.g. dragging a color picker) into a
        /// single trailing write. Each call cancels the previous pending
        /// write and schedules a new one after <paramref name="delay"/>.
        /// </summary>
        public void SaveDebounced(AppearanceConfiguration appearance, TimeSpan? delay = null)
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
                        await SaveAsync(appearance, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Superseded by a newer SaveDebounced — fine.
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"AppearanceService.SaveDebounced failed: {ex}");
                    }
                }, token);
            }
        }

        /// <summary>
        /// Awaits any in-flight debounced save. Call on window close so the
        /// user's latest change is durable before the process exits.
        /// </summary>
        public Task FlushAsync()
        {
            Task pending;
            lock (_debounceLock) { pending = _pendingSave; }
            return pending;
        }

    }



    public class AppearanceConfiguration // Default configuration
    {
        [JsonPropertyName("localization")]
        public string Localization { get; set; } = "English";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "0.0.0";

        // Grid

        [JsonPropertyName("grid_rows")]
        public int GridRows { get; set; } = 2;

        [JsonPropertyName("grid_columns")]
        public int GridColumns { get; set; } = 4;

        // Per-panel size in device-independent pixels. Older config.json files
        // without these fields deserialize to the historical defaults
        // (200 x 100), so existing layouts are unchanged. Adjustable in the
        // Appearance window; the grid uses Auto rows/columns so it reflows to
        // whatever size the panels report.
        [JsonPropertyName("panel_width")]
        public int PanelWidth { get; set; } = 200;

        [JsonPropertyName("panel_height")]
        public int PanelHeight { get; set; } = 100;

        // Opacity of the WHOLE overlay window (toolbar + panels), as a percent
        // 20–100. Applied via Window.Opacity to both the master window and the
        // transparent sub-window. Clamped to a 20% floor so the app can never
        // be made completely invisible/unclickable from this slider. Stored per
        // appearance profile. Older config files without it default to 100
        // (fully opaque), so existing setups are unchanged.
        [JsonPropertyName("window_opacity")]
        public int WindowOpacity { get; set; } = 100;

        //[JsonPropertyName("panels_spacing")]
        //public int PanelsSpacing { get; set; } = 0;

        //[JsonPropertyName("line_height")]
        //public int LineHeight { get; set; } = 0;

        // Colors

        [JsonPropertyName("background_color")]
        public string BackgroundColor { get; set; } = "#FFC6D3E1";

        [JsonPropertyName("panels_color")]
        public string PanelsColor { get; set; } = "#FFFBFBFB";

        [JsonPropertyName("keys_text_color")]
        public string KeysTextColor { get; set; } = "#FFEFA3A3";

        [JsonPropertyName("values_text_color")]
        public string ValuesTextColor { get; set; } = "#FF98BBB8";

        [JsonPropertyName("background_rainbow")]
        public bool IsBackgroundRainbow { get; set; } = false;

        // Duration of one full rainbow colour cycle, in seconds (1–10).
        [JsonPropertyName("rainbow_speed_seconds")]
        public int RainbowSpeedSeconds { get; set; } = 6;

        //[JsonPropertyName("line_color")]
        //public string LineColor { get; set; } = "#000000";

        // Opacity

        //[JsonPropertyName("panels_opacity")]
        //public int PanelsOpacity { get; set; } = 25;

        //[JsonPropertyName("text_opacity")]
        //public double TextOpacity { get; set; } = 1.0;

        //[JsonPropertyName("line_opacity")]
        //public double LineOpacity { get; set; } = 1.0;

        // Font

        [JsonPropertyName("keys_font")]
        public FontAppearance KeysFontAppearance { get; set; } = new();

        [JsonPropertyName("values_font")]
        public FontAppearance ValuesFontAppearance { get; set; } = new();

    }
}
