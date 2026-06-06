using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    public interface AppearanceOverlay
    {
        void LoadAppearanceFile(string file);
        void SelectAppearanceFile(string file);
        void RevertAppearanceFile();
    }
    public partial class Appearance : Window, AppearanceOverlay
    {
        private DependencyPropertyDescriptor _colorDp;

        private AppearanceConfiguration _appearance;

        private string _selectedAppearanceProfile;
        private string _temporaryAppearanceProfile = string.Empty;

        private readonly IOverlay _mainOverlay;
        public FontAppearance KeysFontAppearance { get; set; } = new FontAppearance();
        public FontAppearance ValuesFontAppearance { get; set; } = new FontAppearance();

        private Color backgroundColor;
        private Color panelsColor;
        private Color keysColor;
        private Color valuesColor;

        public Appearance(AppearanceConfiguration appearanceConfiguration, IOverlay mainOverlay, string selectedPrifile)
        {
            _appearance = appearanceConfiguration;
            _mainOverlay = mainOverlay;
        

            InitializeComponent();

            _selectedAppearanceProfile = selectedPrifile;

            DataContext = this;

            _colorDp = DependencyPropertyDescriptor.FromProperty(
            MD.ColorPicker.ColorProperty, typeof(MD.ColorPicker));


            _colorDp.AddValueChanged(BackgroundColorPicker, OnBackgroundColorChanged);

            _colorDp.AddValueChanged(PanelsColorPicker, OnPanelsColorChanged);

            _colorDp.AddValueChanged(KeysColorPicker, OnKeysColorChanged);

            _colorDp.AddValueChanged(ValuesColorPicker, OnValuesColorChanged);

            this.Closed += OnClosedDetachHandlers;

            SetOnStart();
            this.Tag = "idle";

        }

        private void SetOnStart()
        {
            RowsCount.Value = _appearance.GridRows;
            ColumnsCount.Value = _appearance.GridColumns;

            BackgroundRainbowCheckBox.IsChecked = _appearance.IsBackgroundRainbow;
            if (_appearance.IsBackgroundRainbow == true)
                _mainOverlay.SetBackgroundRainbow(true);
            else
                _mainOverlay.SetBackgroundRainbow(false);

            BackgroundColorPicker.Color = (Color)ColorConverter.ConvertFromString(_appearance.BackgroundColor);
            PanelsColorPicker.Color = (Color)ColorConverter.ConvertFromString(_appearance.PanelsColor);
            KeysColorPicker.Color = (Color)ColorConverter.ConvertFromString(_appearance.KeysTextColor);
            ValuesColorPicker.Color = (Color)ColorConverter.ConvertFromString(_appearance.ValuesTextColor);


            KeysFontPicker.AppearanceParameter = _appearance.KeysFontAppearance;
            ValuesFontPicker.AppearanceParameter = _appearance.ValuesFontAppearance;


            string name = string.Empty;
            if (_temporaryAppearanceProfile != string.Empty)
                name = _temporaryAppearanceProfile;
            else 
                name = _selectedAppearanceProfile;

            try
            {
                string[] names = Regex.Split(name, @"ClickyKeys\\settings\\");
                name = Regex.Replace(names[1], @".json", " ");
            }
            catch (Exception)
            {
                name = Regex.Replace(name, @".json", "");
            }

            ProfilesLabel.Content = $"Profile: {name}";
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

        void ForceColorChange()
        {
            OnBackgroundColorChanged(BackgroundColorPicker, EventArgs.Empty);
            OnPanelsColorChanged(PanelsColorPicker, EventArgs.Empty);
            OnKeysColorChanged(KeysColorPicker, EventArgs.Empty);
            OnValuesColorChanged(ValuesColorPicker, EventArgs.Empty);
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
            _mainOverlay.OnAppearanceClose(_selectedAppearanceProfile);
        }

        private void Click_BackgroundRainbowCheckBox(object? sender, EventArgs e)
        {
            _mainOverlay.SetBackgroundRainbow(BackgroundRainbowCheckBox.IsChecked);
        }
        private void Click_GridRows(object? sender, EventArgs e)
        {
            _appearance.GridRows = (int)RowsCount.Value;
            _mainOverlay.OnGridChange(_appearance);
        }
        private void Click_GridColumns(object? sender, EventArgs e)
        {
            _appearance.GridColumns = (int)ColumnsCount.Value;
            _mainOverlay.OnGridChange(_appearance);
        }
        // Plain "Save": persists the current edits to the active profile
        // (the temporary one if a different profile was selected this session,
        // otherwise the one we opened with) and closes. "Save As" no longer
        // routes through here — it has its own popup (see Click_SaveAsConfirm).
        private async void Click_SaveAndClose(object? sender, EventArgs e)
        {
            _appearance.GridColumns = (int)ColumnsCount.Value;
            _mainOverlay.OnGridChange(_appearance);

            if (_temporaryAppearanceProfile != string.Empty)
                _selectedAppearanceProfile = _temporaryAppearanceProfile;

            await SaveProfileAsync(_selectedAppearanceProfile);
        }

        // Writes the current appearance state to <paramref name="profileFile"/>
        // (a "*.json" file name), notifies the main window, and closes. Shared
        // by the plain "Save" and the "Save As" popup so both paths persist an
        // identical snapshot of the UI.
        private async Task SaveProfileAsync(string profileFile)
        {
            _selectedAppearanceProfile = profileFile;

            AppearanceService _appearanceService = new(_selectedAppearanceProfile);
            AppearanceConfiguration _appearanceConfiguration = _appearanceService.Load();

            _appearanceConfiguration.GridColumns = (int)ColumnsCount.Value;
            _appearanceConfiguration.GridRows = (int)RowsCount.Value;
            var converter = new ColorConverter();
            _appearanceConfiguration.BackgroundColor = converter.ConvertToString(backgroundColor);
            _appearanceConfiguration.PanelsColor = converter.ConvertToString(panelsColor);
            _appearanceConfiguration.KeysTextColor = converter.ConvertToString(keysColor);
            _appearanceConfiguration.ValuesTextColor = converter.ConvertToString(valuesColor);
            _appearanceConfiguration.KeysFontAppearance = KeysFontPicker.AppearanceParameter;
            _appearanceConfiguration.ValuesFontAppearance = ValuesFontPicker.AppearanceParameter;
            _appearanceConfiguration.IsBackgroundRainbow = BackgroundRainbowCheckBox.IsChecked ?? false;

            // Async atomic save: doesn't block the UI thread while the file
            // hits disk. If the async path fails for any reason we fall back
            // to the synchronous save so the user's changes still land.
            try
            {
                await _appearanceService.SaveAsync(_appearanceConfiguration);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveProfileAsync: SaveAsync failed, falling back: {ex}");
                _appearanceService.Save(_appearanceConfiguration);
            }

            _mainOverlay.OnAppearanceClose(_selectedAppearanceProfile);
            this.Close();
        }
        private void Click_Load(object? sender, EventArgs e)
        {
            if(this.Tag.ToString() != "loading")
            {
                SetLoader loading = new(this);
                this.Tag = "loading";
                loading.Show();
            }

        }
        private void Click_Close(object? sender, EventArgs e)
        {
            _mainOverlay.OnAppearanceClose(_selectedAppearanceProfile);
            this.Close();
        }

        // "Save As" now opens a small modal popup (SaveAsOverlay) instead of
        // revealing an inline textbox in the footer. Clear any previous input
        // and validation state, show the popup, and focus the name field.
        private void Click_SaveAs(object? sender, EventArgs e)
        {
            SaveAsNameTextBox.Text = string.Empty;
            SaveAsErrorText.Visibility = Visibility.Collapsed;
            SaveAsOverlay.Visibility = Visibility.Visible;
            SaveAsNameTextBox.Focus();
        }

        // Confirm: validate the typed name, then save the current appearance to
        // a brand-new "<name>.json" profile and close. An empty/whitespace name
        // is rejected inline (no save, no close) so the user can correct it.
        private async void Click_SaveAsConfirm(object? sender, RoutedEventArgs e)
        {
            string name = (SaveAsNameTextBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                SaveAsErrorText.Visibility = Visibility.Visible;
                SaveAsNameTextBox.Focus();
                return;
            }

            SaveAsOverlay.Visibility = Visibility.Collapsed;
            await SaveProfileAsync(name + ".json");
        }

        // Cancel: dismiss the popup and discard the typed name; nothing is
        // saved and the Appearance window stays open.
        private void Click_SaveAsCancel(object? sender, RoutedEventArgs e)
        {
            SaveAsOverlay.Visibility = Visibility.Collapsed;
        }

        // Keyboard shortcuts inside the name field: Enter confirms, Esc cancels.
        private void SaveAsNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Click_SaveAsConfirm(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Click_SaveAsCancel(sender, e);
            }
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

        public void LoadAppearanceFile(string file)
        {
            AppearanceService appearanceService = new AppearanceService(file);
            var loaded = appearanceService.Load();

             CopyAppearance(_appearance, loaded);

            _mainOverlay.OnGridChange(_appearance);

            SetOnStart();
            ForceColorChange();

        }
        public void SelectAppearanceFile(string file)
        {
            _temporaryAppearanceProfile = file;

            SetOnStart();

            this.Tag = "idle";

        }
        public void RevertAppearanceFile()
        {
            AppearanceService appearanceService = new AppearanceService(_selectedAppearanceProfile);
            _appearance = appearanceService.Load();
            SetOnStart();
            _mainOverlay.OnGridChange(_appearance);
            this.Tag = "idle";
        }

        private static void CopyAppearance(AppearanceConfiguration target, AppearanceConfiguration source)
        {
            target.GridRows = source.GridRows;
            target.GridColumns = source.GridColumns;
            target.BackgroundColor = source.BackgroundColor;
            target.PanelsColor = source.PanelsColor;
            target.KeysTextColor = source.KeysTextColor;
            target.ValuesTextColor = source.ValuesTextColor;
            target.IsBackgroundRainbow = source.IsBackgroundRainbow;

            CopyFontAppearance(target.KeysFontAppearance, source.KeysFontAppearance);
            CopyFontAppearance(target.ValuesFontAppearance, source.ValuesFontAppearance);
        }

        private static void CopyFontAppearance(FontAppearance target, FontAppearance source)
        {
            target.FontFamily = source.FontFamily;
            target.FontSize = source.FontSize;
            target.IsBold = source.IsBold;
            target.IsItalic = source.IsItalic;
            target.IsUnderline = source.IsUnderline;
        }
    }
}
