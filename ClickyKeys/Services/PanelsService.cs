using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using System.Windows.Input;

namespace ClickyKeys
{

    internal class PanelsService
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
                appName,"panels");

            Directory.CreateDirectory(appDataDir);

            _filePath = Path.Combine(appDataDir, "default_panels.json");

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

        public void Save(PanelState state)
        {
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(_filePath, json);
        }

        public PanelState Load()
        {
            var json = File.ReadAllText(_filePath);
            return ValidateFormat(JsonSerializer.Deserialize<PanelState>(json, JsonOptions)
                   ?? new PanelState());
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
