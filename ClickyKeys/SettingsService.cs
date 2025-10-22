using System;
using System.Collections.Generic;
using System.IO;
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
                Save(new SettingsConfiguration());
            }
        }

        public SettingsConfiguration Load() // Load data from settings.json
        {
            lock (_lock)
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<SettingsConfiguration>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new SettingsConfiguration();
                }
                catch
                {
                    // Return to defaults
                    var defaults = new SettingsConfiguration();
                    Save(defaults);
                    return defaults;
                }
            }
        }


        public void Save(SettingsConfiguration settings) // Override settings.json with new data
        {
            lock (_lock)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_filePath, json); // file override
            }
        }

    }



    public class SettingsConfiguration // Default configuration
    {
        [JsonPropertyName("localization")]
        public string Localization { get; set; } = "English";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "2.0.0";

        // Grid

        [JsonPropertyName("grid_rows")]
        public int GridRows { get; set; } = 2;

        [JsonPropertyName("grid_columns")]
        public int GridColumns { get; set; } = 4;

        //[JsonPropertyName("panels_spacing")]
        //public int PanelsSpacing { get; set; } = 0;

        //[JsonPropertyName("line_height")]
        //public int LineHeight { get; set; } = 0;

        // Colors

        [JsonPropertyName("background_color")]
        public string BackgroundColor { get; set; } = "#FFFFF7E5";

        [JsonPropertyName("panels_color")]
        public string PanelsColor { get; set; } = "#FFFCF7E7";

        [JsonPropertyName("keys_text_color")]
        public string KeysTextColor { get; set; } = "#FF5882D4";

        [JsonPropertyName("values_text_color")]
        public string ValuesTextColor { get; set; } = "#FFFF0101";

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
        public FontSettings KeysFontSettings { get; set; } = new();

        [JsonPropertyName("values_font")]
        public FontSettings ValuesFontSettings { get; set; } = new();

    }
}
