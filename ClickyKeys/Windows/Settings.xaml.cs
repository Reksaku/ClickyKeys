using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MD = MaterialDesignThemes.Wpf;


namespace ClickyKeys
{
    
    public partial class Settings : Window
    {
        private DependencyPropertyDescriptor _colorDp;

        private readonly SettingsConfiguration _settings;

        private readonly IOverlay _mainOverlay;
        public FontSettings KeysFontSettings { get; set; } = new FontSettings();
        public FontSettings ValuesFontSettings { get; set; } = new FontSettings();

        private Color backgroundColor;
        private Color panelsColor;
        private Color keysColor;
        private Color valuesColor;

        public Settings(SettingsConfiguration settingsConfiguration, IOverlay mainOverlay)
        {
            _settings = settingsConfiguration;
            _mainOverlay = mainOverlay;
        

            InitializeComponent();



            DataContext = this;

            _colorDp = DependencyPropertyDescriptor.FromProperty(
            MD.ColorPicker.ColorProperty, typeof(MD.ColorPicker));


            _colorDp.AddValueChanged(BackgroundColorPicker, OnBackgroundColorChanged);

            _colorDp.AddValueChanged(PanelsColorPicker, OnPanelsColorChanged);

            _colorDp.AddValueChanged(KeysColorPicker, OnKeysColorChanged);

            _colorDp.AddValueChanged(ValuesColorPicker, OnValuesColorChanged);

            this.Closed += OnClosedDetachHandlers;

            SetOnStart();

        }

        private void SetOnStart()
        {
            RowsCount.Value = _settings.GridRows;
            ColumnsCount.Value = _settings.GridColumns;

            BackgroundColorPicker.Color = (Color)ColorConverter.ConvertFromString(_settings.BackgroundColor);
            PanelsColorPicker.Color = (Color)ColorConverter.ConvertFromString(_settings.PanelsColor);
            KeysColorPicker.Color = (Color)ColorConverter.ConvertFromString(_settings.KeysTextColor);
            ValuesColorPicker.Color = (Color)ColorConverter.ConvertFromString(_settings.ValuesTextColor);

            KeysFontSettings = _settings.KeysFontSettings;
            ValuesFontSettings = _settings.ValuesFontSettings;

            BackgroundRainbowCheckBox.IsChecked = _settings.IsBackgroundRainbow;
        }

        private void OnBackgroundColorChanged(object sender, EventArgs e)
        {
            var picker = (MD.ColorPicker)sender;
            backgroundColor = picker.Color;                  
            WeakReferenceMessenger.Default.Send(
                new ColorChangedMessage(backgroundColor, ColorTarget.Background));
        }

        private void OnPanelsColorChanged(object sender, EventArgs e)
        {
            var picker = (MD.ColorPicker)sender;
            panelsColor = picker.Color;
            WeakReferenceMessenger.Default.Send(
                new ColorChangedMessage(panelsColor, ColorTarget.Panels));
        }

        private void OnKeysColorChanged(object sender, EventArgs e)
        {
            var picker = (MD.ColorPicker)sender;
            keysColor = picker.Color;
            WeakReferenceMessenger.Default.Send(
                new ColorChangedMessage(keysColor, ColorTarget.Keys));
        }

        private void OnValuesColorChanged(object sender, EventArgs e)
        {
            var picker = (MD.ColorPicker)sender;
            valuesColor = picker.Color;
            WeakReferenceMessenger.Default.Send(
                new ColorChangedMessage(valuesColor, ColorTarget.Values));
        }

        private void OnClosedDetachHandlers(object? sender, EventArgs e)
        {
            if (_colorDp is null) return;
            _colorDp.RemoveValueChanged(BackgroundColorPicker, OnBackgroundColorChanged);
            _colorDp.RemoveValueChanged(PanelsColorPicker, OnPanelsColorChanged);
            _colorDp.RemoveValueChanged(KeysColorPicker, OnKeysColorChanged);
            _colorDp.RemoveValueChanged(ValuesColorPicker, OnValuesColorChanged);
            this.Closed -= OnClosedDetachHandlers;
            _colorDp = null!;
        }
        private void Window_Closed(object? sender, EventArgs e)
        {
            _mainOverlay.OnSettingsClose();
        }

        private void Click_BackgroundRainbowCheckBox(object? sender, EventArgs e)
        {
            _mainOverlay.SetBackgroundRainbow(BackgroundRainbowCheckBox.IsChecked);
        }
        private void Click_GridRows(object? sender, EventArgs e)
        {
            _settings.GridRows = (int)RowsCount.Value;
            _mainOverlay.OnGridChange(_settings);
        }
        private void Click_GridColumns(object? sender, EventArgs e)
        {
            _settings.GridColumns = (int)ColumnsCount.Value;
            _mainOverlay.OnGridChange(_settings);
        }
        private void Click_SaveAndClose(object? sender, EventArgs e)
        {
            _settings.GridColumns = (int)ColumnsCount.Value;
            _mainOverlay.OnGridChange(_settings);

            SettingsService _settingsService = new();
            SettingsConfiguration _settingsConfiguration = _settingsService.Load();

            _settingsConfiguration.GridColumns = (int)ColumnsCount.Value;
            _settingsConfiguration.GridRows = (int)RowsCount.Value;
            var converter = new ColorConverter();
            _settingsConfiguration.BackgroundColor = converter.ConvertToString(backgroundColor);
            _settingsConfiguration.PanelsColor = converter.ConvertToString(panelsColor);
            _settingsConfiguration.KeysTextColor = converter.ConvertToString(keysColor);
            _settingsConfiguration.ValuesTextColor = converter.ConvertToString(valuesColor);
            _settingsConfiguration.KeysFontSettings = KeysFontSettings;
            _settingsConfiguration.ValuesFontSettings = ValuesFontSettings;
            _settingsConfiguration.IsBackgroundRainbow = BackgroundRainbowCheckBox.IsChecked ?? false;

            _settingsService.Save(_settingsConfiguration);
            _mainOverlay.OnSettingsClose();
            this.Close();
        }

        private void Click_Close(object? sender, EventArgs e)
        {
            _mainOverlay.OnSettingsClose();
            this.Close();
        }

        private void Click_KeysSize(object? sender, EventArgs e)
        {

        }

        private void Click_ValuesSize(object? sender, EventArgs e)
        {

        }


        private void Window_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
        {

        }

        private void BackgroundRainbowCheckBox_Click(object sender, RoutedEventArgs e)
        {

        }
    }

    //public class FontPickerViewModel
    //{
    //    public FontSettings _fontSettings { get; } = new FontSettings();

    //    public System.Collections.Generic.List<FontFamily> FontFamilies { get; } =
    //        Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();
    //}
}
