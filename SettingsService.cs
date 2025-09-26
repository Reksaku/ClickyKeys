using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClickyKeys
{
    public class SettingsService
    {

        private readonly string _filePath;
        private readonly object _lock = new();

        public SettingsService(string appName = "ClickyKeys")
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName);

            Directory.CreateDirectory(appDataDir);
            _filePath = Path.Combine(appDataDir, "settings.json");

            if (!File.Exists(_filePath))
            {
                Save(new Settings());
            }
        }

        public Settings Load() // Load data from settings.json
        {
            lock (_lock)
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<Settings>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new Settings();
                }
                catch
                {
                    // Return to defaults
                    var defaults = new Settings();
                    Save(defaults);
                    return defaults;
                }
            }
        }


        public void Save(Settings settings) // Override settings.json with new data
        {
            lock (_lock)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_filePath, json); // file override
            }
        }

    }



    public class Settings // Default configuration
    {
        [JsonPropertyName("localization")]
        public string Localization { get; set; } = "English";

        // Grid

        [JsonPropertyName("grid_rows")]
        public int GridRows { get; set; } = 2;

        [JsonPropertyName("grid_columns")]
        public int GridColumns { get; set; } = 4;

        [JsonPropertyName("panels_spacing")]
        public int PanelsSpacing { get; set; } = 0;

        //[JsonPropertyName("line_height")]
        //public int LineHeight { get; set; } = 0;

        // Colors

        [JsonPropertyName("background_color")]
        public string BackgroundColor { get; set; } = "#c0c0c0";

        [JsonPropertyName("panels_color")]
        public string PanelsColor { get; set; } = "#ffffff";

        [JsonPropertyName("key_text_color")]
        public string KeyTextColor { get; set; } = "#000000";

        [JsonPropertyName("value_text_color")]
        public string ValueTextColor { get; set; } = "#ff0080";

        //[JsonPropertyName("line_color")]
        //public string LineColor { get; set; } = "#000000";

        // Opacity

        [JsonPropertyName("panels_opacity")]
        public int PanelsOpacity { get; set; } = 25;

        //[JsonPropertyName("text_opacity")]
        //public double TextOpacity { get; set; } = 1.0;

        //[JsonPropertyName("line_opacity")]
        //public double LineOpacity { get; set; } = 1.0;

        // Font

        //[JsonPropertyName("font_size")]
        //public double FontSize { get; set; } = 1.0;






        [JsonIgnore]
        public string BackgroundRgb => string.Join(", ", HexToRgb(BackgroundColor));

        public static int[] HexToRgb(string hex)
        {
            var h = (hex ?? "#000000").TrimStart('#');
            if (h.Length == 3) h = string.Concat(h.Select(c => $"{c}{c}"));
            var r = Convert.ToInt32(h[..2], 16);
            var g = Convert.ToInt32(h.Substring(2, 2), 16);
            var b = Convert.ToInt32(h.Substring(4, 2), 16);
            return new[] { r, g, b };
        }
    }
}
