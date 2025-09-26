using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClickyKeys
{
    public partial class SettingsForm : Form
    {
        private readonly OverlayForm _overlay;
        private readonly SettingsService _settingsService;
        private readonly Settings _settings;


        public SettingsForm(OverlayForm overlay, SettingsService settings)
        {
            InitializeComponent();
            _overlay = overlay;
            _settingsService = settings;
            _settings = _settingsService.Load();
            setSettingsValues();
        }
        private void setSettingsValues()
        {
            trBrColumns.Value = _settings.GridColumns;
            trBrRows.Value = _settings.GridRows;
            trBrSpacing.Value = _settings.PanelsSpacing;
            trBrOpacity.Value = _settings.PanelsOpacity;  

            labelRows.Text = _settings.GridRows.ToString();
            labelColumns.Text = _settings.GridColumns.ToString();
            labelSapcing.Text = _settings.PanelsSpacing.ToString() + " px";
            labelOpacity.Text = _settings.PanelsOpacity.ToString() + " %";

            btnKeyTextColor.BackColor = ColorTranslator.FromHtml(_settings.KeyTextColor);
            btnValueTextColor.BackColor = ColorTranslator.FromHtml(_settings.ValueTextColor);
            btnPanelsColor.BackColor = ColorTranslator.FromHtml(_settings.PanelsColor);
            btnBackgroundColor.BackColor = ColorTranslator.FromHtml(_settings.BackgroundColor);
        }

        private void trBrRows_ValueChanged(object sender, EventArgs e)
        {
            labelRows.Text = trBrRows.Value.ToString();
            _overlay.PrepareGrid(trBrColumns.Value, trBrRows.Value, trBrSpacing.Value);
        }

        private void trBrColumns_ValueChanged(object sender, EventArgs e)
        {
            labelColumns.Text = trBrColumns.Value.ToString();
            _overlay.PrepareGrid(trBrColumns.Value, trBrRows.Value, trBrSpacing.Value);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            _settings.GridColumns = trBrColumns.Value;
            _settings.GridRows = trBrRows.Value;
            _settings.PanelsSpacing = trBrSpacing.Value;
            _settings.PanelsOpacity = trBrOpacity.Value;

            _settingsService.Save(_settings);
            _overlay.LoadFromSettings();
            Close();
        }
        private void btnApply_Click(object sender, EventArgs e)
        {
            _settings.GridColumns = trBrColumns.Value;
            _settings.GridRows = trBrRows.Value;
            _settings.PanelsSpacing = trBrSpacing.Value;
            _settingsService.Save(_settings);
            _overlay.LoadFromSettings();
        }

        private void btnExit_MouseClick(object sender, MouseEventArgs e)
        {
            _overlay.LoadFromSettings();
            Close();
        }

        private void btnKeyTextColor_Click(object sender, EventArgs e)
        {
            if (colDial.ShowDialog() == DialogResult.OK)
            {
                btnKeyTextColor.BackColor = colDial.Color;
                var c = colDial.Color;
                _settings.KeyTextColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
        }

        private void btnValueTextColor_Click(object sender, EventArgs e)
        {
            if (colDial.ShowDialog() == DialogResult.OK)
            {
                btnValueTextColor.BackColor = colDial.Color;
                var c = colDial.Color;
                _settings.ValueTextColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
        }

        private void btnBackgroundColor_Click(object sender, EventArgs e)
        {
            if (colDial.ShowDialog() == DialogResult.OK)
            {
                btnBackgroundColor.BackColor = colDial.Color;
                var c = colDial.Color;
                _settings.BackgroundColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
        }

        private void btnPanelsColor_Click(object sender, EventArgs e)
        {
            if (colDial.ShowDialog() == DialogResult.OK)
            {
                btnPanelsColor.BackColor = colDial.Color;
                var c = colDial.Color;
                _settings.PanelsColor = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
        }

        private void trBrOpacity_ValueChanged(object sender, EventArgs e)
        {
            labelOpacity.Text = trBrOpacity.Value.ToString() + " %";
            _overlay.UpdateOpacity(trBrOpacity.Value);
        }

        private void trBrSpacing_ValueChanged(object sender, EventArgs e)
        {
            labelSapcing.Text = trBrSpacing.Value.ToString() + " px";
            _overlay.PrepareGrid(trBrColumns.Value, trBrRows.Value, trBrSpacing.Value);

        }
    }


}
