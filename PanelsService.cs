using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.IO;

namespace ClickyKeys
{
    public class PanelsService
    {
        private readonly string _filePath;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };


        public PanelsService(string appName = "ClickyKeys")
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName);

            Directory.CreateDirectory(appDataDir);

            _filePath = Path.Combine(appDataDir, "panels.json");

            if (!File.Exists(_filePath))
            {
                // create default 100 panels
                var def = new PanelState();
                DeaultWSAD(def);
                for (int i = 4; i < 100; i++)
                {
                    def.Panels.Add(new PanelsSettings
                    {
                        Index = i,
                        KeyCode = Keys.None,
                        Description = ""
                    });
                }
                Save(def);
            }
        }

        private void DeaultWSAD(PanelState def)
        {
            def.Panels.Add(new PanelsSettings { Index = 0, KeyCode = Keys.W, Description = "W"});
            def.Panels.Add(new PanelsSettings { Index = 1, KeyCode = Keys.S, Description = "S" });
            def.Panels.Add(new PanelsSettings { Index = 2, KeyCode = Keys.A, Description = "A" });
            def.Panels.Add(new PanelsSettings { Index = 3, KeyCode = Keys.D, Description = "D" });
        }

        public PanelState Load()
        {
            var json = File.ReadAllText(_filePath);
            return ValidateFormat( JsonSerializer.Deserialize<PanelState>(json, JsonOptions)
                   ?? new PanelState() );
        }

        private PanelState ValidateFormat(PanelState fileData)
        {
            Dictionary<int, Keys> _key = new();
            foreach (var (panel, idx) in fileData.Panels.Select((p, i) => (p, i)))
            {
                bool duplicate = false;
                if (panel.Input == InputType.Key && panel.KeyCode != Keys.None)
                {

                    foreach (var (_, name) in _key)
                    {
                        if (name == panel.KeyCode)
                        {
                            duplicate = true;
                            fileData.Panels[idx].KeyCode = Keys.None;
                            fileData.Panels[idx].Description = "";
                        }
                    }
                    if (duplicate == false)
                    {
                        _key[idx] = panel.KeyCode;
                    }

                }
                //else if (panel.Input == InputType.MouseLeft)
                //{
                //    _mouseLeftPanelIndex = idx;
                //    _panelCounts.TryAdd(idx, 0);
                //}
                //else if (panel.Input == InputType.MouseRight)
                //{
                //    _mouseRightPanelIndex = idx;
                //    _panelCounts.TryAdd(idx, 0);
                //}
            }

            return fileData;
        }

        public void Save(PanelState state)
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
    }


    public enum InputType
    {
        Key,
        MouseLeft,
        MouseRight
    }


    public class PanelsSettings
    {
        public int Index { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Keys KeyCode { get; set; } = Keys.None;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public InputType Input { get; set; } = InputType.Key;

        public string Description { get; set; } = "";
    }

    public class PanelState
    {
        public List<PanelsSettings> Panels { get; set; } = new();
    }

}
