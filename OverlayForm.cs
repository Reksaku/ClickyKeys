using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace ClickyKeys
{
    public partial class OverlayForm : Form
    {
        public InputCounter _counter;
        private SettingsService _settingsService;
        private PanelsService _panelsService;
        private Settings _settings;
        private PanelState _panel_settings;

        private Dictionary<Keys, GlassPanel> _panelsByKeyCode = new();
        private Dictionary<int, GlassPanel> _panelsById = new();

        private int grid_collumns = 4;
        private int grid_rows = 4;

        private readonly WinFormsTimer _timer = new() { Interval = 200 }; // IU refreshing interval


        public OverlayForm(SettingsService settings, PanelsService panels_settings)
        {

            _settingsService = settings;
            _panelsService = panels_settings;
            _settings = _settingsService.Load();
            _panel_settings = _panelsService.Load();
            

            InitializeComponent();

            _counter = new InputCounter(this);


            loadFromSettings();
            _counter.LoadPanels(_panel_settings);
            _counter.Start();

            _timer.Tick += (_, __) => UpdateValues();
            _timer.Start();
        }

        public void loadFromSettings()
        {
            _settings = _settingsService.Load();
            BackColor = ColorTranslator.FromHtml(_settings.BackgroundColor);
            PrepareGrid(_settings.GridColumns, _settings.GridRows);
        }

        public void PrepareGrid(int columns, int rows)
        {
            grid_collumns = columns;
            grid_rows = rows;


            _grid.Controls.Clear();
            Size = new Size((int)(45 + columns * 200), (int)(100 + rows * 120));
            _grid.Size = new Size(columns * 200, rows * 120);
            _grid.ColumnCount = columns;
            _grid.RowCount = rows;

            _grid.ColumnStyles.Clear();
            for (int i = 0; i < columns; i++)
                _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 / columns));
            _grid.RowStyles.Clear();
            for (int i = 0; i < rows; i++)
                _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100 / rows));

            ConfigureGrid(columns, rows);



        }

        public void ConfigureGrid(int columns, int rows)
        {
            Keys n;
            string d;
            InputType input;
            for (int i = 0; i < columns * rows; i++)
            {
                if (_panel_settings.Panels[i].Input == InputType.Key && _panel_settings.Panels[i].KeyCode == Keys.None)
                    d = $"id. {i}";
                else d = $"{_panel_settings.Panels[i].Description}";

                n = _panel_settings.Panels[i].KeyCode;
                // d = _panel_settings.Panels[i].Description;
                input = _panel_settings.Panels[i].Input;

                var panel = new GlassPanel(this)
                {
                    Inset = 0,
                    Key = n,
                    Value = -1,
                    ID = i,
                    Description = d,
                    Type = input,
                    KeyTextColor = ColorTranslator.FromHtml(_settings.KeyTextColor),
                    ValueTextColor = ColorTranslator.FromHtml(_settings.ValueTextColor),
                    PanelColor = ColorTranslator.FromHtml(_settings.PanelsColor),
                    PanelOpacity = _settings.PanelsOpacity
                };
                _panelsByKeyCode[n] = panel;
                _panelsById[i] = panel;
                _grid.Controls.Add(panel);
            }
        }

        public void UpdateOpacity(int opacity)
        {
            for (int i = 0; i < grid_collumns * grid_rows; i++)
            {
                _panelsById[i].PanelOpacity = opacity;
                _panelsById[i].TriggerFlash();
            }
        }


        private void UpdateValues()
        {
            var stats = _counter.GetStats();
            for (int i = 0; i < grid_collumns * grid_rows; i++)
            {
                var panel = _panelsById[i];
                foreach (var (c, it, n, v) in stats.Take(grid_collumns * grid_rows)) 
                {
                    if (panel.Key == c && panel.Type == it)
                    {
                        try
                        {
                            if (panel.Value != v)
                            {
                                panel.Value = v;
                                panel.TriggerFlash();
                            }
                        }
                        catch { }
                    }
                }

            }
        }

        public void EditPanel(int id, string description, InputType input, Keys key_code)
        {
            _panelsById[id].TriggerFlash();
            for (int i = 0; i < 100; i++)
            {
                if (_panel_settings.Panels[i].KeyCode == key_code)
                {
                    _panel_settings.Panels[i].Input = InputType.Key;
                    _panel_settings.Panels[i].KeyCode = Keys.None;
                    _panel_settings.Panels[i].Description = "";
                }
            }
            _panel_settings.Panels[id].Input = input;
            _panel_settings.Panels[id].KeyCode = key_code;
            _panel_settings.Panels[id].Description = description;

            _panelsService.Save(_panel_settings);

            _counter.LoadPanels(_panel_settings);

            loadFromSettings();
        }


        public void ShowSettings()
        {
            SettingsForm _settings = new SettingsForm(this, _settingsService);
            _settings.Show();
        }

        private void Settings_Click(object sender, EventArgs e)
        {
            ShowSettings();
        }

        private void OverlayForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
                toolStrip.Visible = false;
            for (int i = 0; i < grid_collumns * grid_rows; i++)
                _panelsById[i].GlassPanelKeyDown(e);

        }
        public void ToggleToolStrip()
        {
            if (toolStrip.InvokeRequired)
            {
                toolStrip.BeginInvoke(new Action(ToggleToolStrip));
                return;
            }

            toolStrip.Visible = !toolStrip.Visible;
            if (toolStrip.Visible == true)
            {
                Size = new Size((int)(45 + grid_collumns * 200), (int)(100 + grid_rows * 120));
                _grid.Location = new Point(9, 33);
            }
            else
            {
                Size = new Size((int)(45 + grid_collumns * 200), (int)(72 + grid_rows * 120));
                _grid.Location = new Point(9, 9);
            }
        }

        private void toolStripReset_Click(object sender, EventArgs e)
        {
            _counter.Reset();
        }

        private void toolStripHideTb_Click(object sender, EventArgs e)
        {
            ToggleToolStrip();
        }
    }

}
