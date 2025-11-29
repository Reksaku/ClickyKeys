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
using System.Text.RegularExpressions;
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
    public interface SettingsOverlay
    {
        void LoadSettingsFile(string file);
        void SelectSettingsFile(string file);
        void RevertSettingsFile();
    }
    public partial class Settings : Window, SettingsOverlay
    {
        private DependencyPropertyDescriptor _colorDp;

        private SettingsConfiguration _settings;

        private string _selectedSettingsProfile;
        private string _temporarySettingsProfile = string.Empty;

        private readonly IOverlay _mainOverlay;
        public FontSettings KeysFontSettings { get; set; } = new FontSettings();
        public FontSettings ValuesFontSettings { get; set; } = new FontSettings();

        private Color backgroundColor;
        private Color panelsColor;
        private Color keysColor;
        private Color valuesColor;

        public Settings(SettingsConfiguration settingsConfiguration, IOverlay mainOverlay, string selectedPrifile)
        {
            _settings = settingsConfiguration;
            _mainOverlay = mainOverlay;
        

            InitializeComponent();

            _selectedSettingsProfile = selectedPrifile;

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

            BackgroundRainbowCheckBox.IsChecked = _settings.IsBackgroundRainbow;
            if (_settings.IsBackgroundRainbow == true)
                _mainOverlay.SetBackgroundRainbow(true);
            else
                _mainOverlay.SetBackgroundRainbow(false);

            BackgroundColorPicker.Color = (Color)ColorConverter.ConvertFromString(_settings.BackgroundColor);
            PanelsColorPicker.Color = (Color)ColorConverter.ConvertFromString(_settings.PanelsColor);
            KeysColorPicker.Color = (Color)ColorConverter.ConvertFromString(_settings.KeysTextColor);
            ValuesColorPicker.Color = (Color)ColorConverter.ConvertFromString(_settings.ValuesTextColor);


            KeysFontPicker.SettingsParameter = _settings.KeysFontSettings;
            ValuesFontPicker.SettingsParameter = _settings.ValuesFontSettings;


            string name = string.Empty;
            if (_temporarySettingsProfile != string.Empty)
                name = _temporarySettingsProfile;
            else 
                name = _selectedSettingsProfile;

            try
            {
                string[] names = Regex.Split(name, @"ClickyKeys\\settings\\");
                name = Regex.Replace(names[1], @".json", " ");
            }
            catch (Exception)
            {
                name = Regex.Replace(name, @".json", "");
            }

            SettingsLabel.Content = $"Settings profile: {name}";
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
            _mainOverlay.OnSettingsClose(_selectedSettingsProfile);
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
            if (NewFileNameTextBox.Visibility == Visibility.Visible)
                _selectedSettingsProfile = NewFileNameTextBox.Text+".json";
            else if(_temporarySettingsProfile != string.Empty)
                _selectedSettingsProfile = _temporarySettingsProfile;

            SettingsService _settingsService = new(_selectedSettingsProfile);
            SettingsConfiguration _settingsConfiguration = _settingsService.Load();

            _settingsConfiguration.GridColumns = (int)ColumnsCount.Value;
            _settingsConfiguration.GridRows = (int)RowsCount.Value;
            var converter = new ColorConverter();
            _settingsConfiguration.BackgroundColor = converter.ConvertToString(backgroundColor);
            _settingsConfiguration.PanelsColor = converter.ConvertToString(panelsColor);
            _settingsConfiguration.KeysTextColor = converter.ConvertToString(keysColor);
            _settingsConfiguration.ValuesTextColor = converter.ConvertToString(valuesColor);
            _settingsConfiguration.KeysFontSettings = KeysFontPicker.SettingsParameter;
            _settingsConfiguration.ValuesFontSettings = ValuesFontPicker.SettingsParameter;
            _settingsConfiguration.IsBackgroundRainbow = BackgroundRainbowCheckBox.IsChecked ?? false;

            _settingsService.Save(_settingsConfiguration);
            _mainOverlay.OnSettingsClose(_selectedSettingsProfile);
            this.Close();
        }
        private void Click_Load(object? sender, EventArgs e)
        {
            SetLoader loading = new(this);
            loading.Show();
        }
        private void Click_Close(object? sender, EventArgs e)
        {
            _mainOverlay.OnSettingsClose(_selectedSettingsProfile);
            this.Close();
        }

        private void Click_SaveAs(object? sender, EventArgs e)
        {
            NewFileNameTextBox.Visibility = Visibility.Visible;
            SaveAsButton.Visibility = Visibility.Hidden;
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

        public void LoadSettingsFile(string file)
        {
            SettingsService settingsService = new SettingsService(file);
            _settings = settingsService.Load();
            SetOnStart();
            _mainOverlay.OnGridChange(_settings);
        }
        public void SelectSettingsFile(string file)
        {
            _temporarySettingsProfile = file;
            SetOnStart();
        }
        public void RevertSettingsFile()
        {
            SettingsService settingsService = new SettingsService(_selectedSettingsProfile);
            _settings = settingsService.Load();
            SetOnStart();
            _mainOverlay.OnGridChange(_settings);
        }
    }

    //public class FontPickerViewModel
    //{
    //    public FontSettings _fontSettings { get; } = new FontSettings();

    //    public System.Collections.Generic.List<FontFamily> FontFamilies { get; } =
    //        Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();
    //}
}
