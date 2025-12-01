using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ControlzEx.Standard;
using MahApps.Metro.Controls;
using MaterialDesignColors;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using static System.Windows.Forms.Design.AxImporter;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace ClickyKeys
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public interface IOverlay
    {
        void ToggleToolStrip();
        void SavePanelConfiguration(PanelsSettings state);
        void OnSettingsClose(string settingsPath);
        void OnGridChange(SettingsConfiguration settings);
        public void OnInfoClose();
        void SetBackgroundRainbow(bool? IsTrue);
    }

    public partial class MainWindow : Window, IOverlay
    {
        private readonly InputCounter _counter;
        private readonly DispatcherTimer _uiTimer;
        private readonly DispatcherTimer _backgroundTimer;

        private string settingsFileName;
        private SettingsService _settingsService;
        private SettingsConfiguration _settingsConfiguration;

        public ObservableCollection<StatRow> Stats { get; } = [];

        private readonly Dictionary<int, GlassPanelWpf> _panelsById = [];

        private readonly PanelsService _panelsService = new();
        private PanelState _panel_settings = new();

        private readonly bool _transparent;

        private MainWindow? _transparentWindow = null;

        private ColorsPallet allColors = new();
        private hsvColor BackgroundHSV = new();

        private int rows;
        private int cols;

        private bool OpenedInfo = false;
        private bool OpenedSettings = false;

        private readonly object _lock = new();

        private readonly RequestReleasesAPI _releasesApiClient = new RequestReleasesAPI();

        private string defaultSettingsPath;

        private int _tutorialStep = 0;


        public MainWindow(bool transparent = false, InputCounter? counter = null)
        {
            _transparent = transparent;

            Configuration ConfigSettings = LoadInitSettings();
            settingsFileName = ConfigSettings.SettingsProfile;
            _settingsService = new(settingsFileName);

            defaultSettingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"ClickyKeys", "settings", settingsFileName);
            _settingsConfiguration = _settingsService.Load();


            


            LoadBackgroundFromSettings();

            InitializeComponent();

            // configuration for transparent mode
            if (counter != null && transparent == true)
            {
                _counter = counter;
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;
                this.Title = "ClickyKeys: Transparent Mode";
                ToolStrip.Visibility = Visibility.Collapsed;
            }
            else
            {
                _counter = new InputCounter(this);

                // input counter start
                _counter.Start();
            }

            LoadPanelConfiguration();

            VerifyVersion();
            VerifySettings();

            // interface refresh rate
            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _uiTimer.Tick += (_, __) => UpdateValues();

            // timer start
            _uiTimer.Start();

            _backgroundTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            SetRainbowTimers();


            // color subscriber start 
            WrmSubscriberStart();            
            
            // set panels grid
            SetGrid(_settingsConfiguration);

            if(ConfigSettings.ShowTutorial == true)
            {
                ShowTutorial();
            }

        }

        //-------------------------
        // Lambda section
        //-------------------------

        private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();
        private void ToggleToolbar_Click(object sender, RoutedEventArgs e) => ToggleToolStrip();
        private void Reset_Click(object sender, RoutedEventArgs e) => ResetCounter();
        private void TransparentMode_Click(object sender, RoutedEventArgs e) => TransparentMode();
        private void Info_Click(object sender, RoutedEventArgs e) => ShowInfo();



        //-------------------------
        // Setup section
        //-------------------------

        private void LoadPanelConfiguration()
        {
            // loading panels preset
            _panel_settings = _panelsService.Load();
            // loading panels configuration 
            _counter.LoadPanels(_panel_settings);
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _counter.Start();     // global hooks
            _uiTimer.Start();     // UI refreshing
        }

        private void InitTransparentMode()
        {
            _transparentWindow = new MainWindow(true, _counter);
            _transparentWindow.Show();

        }

        private void SetRainbowTimers()
        {
            _backgroundTimer.Tick += (s, e) =>
            {
                double _hue = BackgroundHSV.hue;
                double _sat = BackgroundHSV.sat;
                double _val = BackgroundHSV.val;

                // calculate saturation and value
                RgbToHsv(allColors.background, out _, out _sat, out _val);

                _hue = (_hue + 1) % 360; // hue rotation
                if (_settingsConfiguration.IsBackgroundRainbow == true)
                    Background = new SolidColorBrush(HsvToRgb(_hue, _sat, _val));

                BackgroundHSV.hue = _hue;
            };
            _backgroundTimer.Start();
        }

        public void LoadBackgroundFromSettings()
        {
            allColors.background = (Color)ColorConverter.ConvertFromString(_settingsConfiguration.BackgroundColor);
            Background = new BrushConverter().ConvertFromString(_settingsConfiguration.BackgroundColor) as Brush;
        }

        Configuration LoadInitSettings()
        {
            string appName = "ClickyKeys";
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName);
            Directory.CreateDirectory(appDataDir);
            string _filePath = Path.Combine(appDataDir, "config.json");

            if (!File.Exists(_filePath))
                lock (_lock)
                {
                    Configuration config = new();
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(config, options);
                    File.WriteAllText(_filePath, json);
                    return config;
                }
            else
                lock (_lock)
                {
                    try
                    {
                        var json = File.ReadAllText(_filePath);
                        return JsonSerializer.Deserialize<Configuration>(json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                               ?? new Configuration();
                    }
                    catch
                    {
                        return new Configuration();
                    }
                }
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            if (_transparent == false)
                _counter.Dispose();
            _uiTimer.Stop();
            _transparentWindow?.Close();
            _transparentWindow = null;
        }

        private void WrmSubscriberStart()
        {
            Loaded += (_, __) =>
            {
                WeakReferenceMessenger.Default.Register<ColorChangedMessage>(
                    recipient: this,
                    handler: (r, m) =>
                    {
                        if (m.Value is not Color c) return;

                        switch (m.Target)
                        {
                            case ColorTarget.Background:
                                // change on background color
                                allColors.background = c;
                                if (_settingsConfiguration.IsBackgroundRainbow == false)
                                    Background = new SolidColorBrush(c);
                                break;

                            case ColorTarget.Panels:
                                // function to change panels color
                                OnPanelsColorChanged(c);
                                break;
                            case ColorTarget.Keys:
                                // function to change panels color
                                OnKeysColorChanged(c);
                                break;
                            case ColorTarget.Values:
                                // function to change panels color
                                OnValuesColorChanged(c);
                                break;
                        }
                    });
            };


            Closed += (_, __) =>
            {
                WeakReferenceMessenger.Default.Unregister<ColorChangedMessage>(this);
            };
        }



        //-------------------------
        // Tutorial section
        //-------------------------


        private void ShowTutorial()
        {
            _tutorialStep = 0;
            TutorialOverlay.Visibility = Visibility.Visible;
            UpdateTutorialText();
        }

        private void UpdateTutorialText()
        {
            Color panelColor = (Color)ColorConverter.ConvertFromString(_settingsConfiguration.PanelsColor);
            Color keysColor = (Color)ColorConverter.ConvertFromString(_settingsConfiguration.KeysTextColor);
            Color valuesColor = (Color)ColorConverter.ConvertFromString(_settingsConfiguration.ValuesTextColor);
            FontSettings keysFont = _settingsConfiguration.KeysFontSettings;
            FontSettings valuesFont = _settingsConfiguration.ValuesFontSettings;
            GlassPanelWpf panel = new(this)
            {
                Value = 67,
                Description = "Example",
                PanelColor = panelColor,
                KeyTextColor = keysColor,
                ValueTextColor = valuesColor,
                KeyFont = keysFont,
                ValueFont = valuesFont,
            };

            switch (_tutorialStep)
            {
                case 0:
                    TutorialText.TextAlignment = TextAlignment.Center;
                    TutorialText.Text = "Welcome to ClickyKeys!\n" +
                        "\nLet me guide you through a quick tutorial." +
                        "\nClick Next to continue.";
                    break;
                case 1:
                    TutorialText.Text = "These are your display panels — the main feature of ClickyKeys.\n" +
                        "\nLet’s take a look at an example.";
                    PanelBoxGlow.Visibility = Visibility.Visible;
                    SetBorder(_panelsById[4], PanelBoxGlow, 10, 8, 180, 84);
                    break;
                case 2:
                    TutorialText.TextAlignment = TextAlignment.Left;
                    TutorialText.Text = "Left-click a panel to edit its settings.";
                    SetBorder(_panelsById[4], PanelBox, -4, -4, 200, 100);
                    PanelBox.Visibility = Visibility.Visible;
                    PanelBoxGrid.Children.Add(panel);
                    break;
                case 3:
                    TutorialText.Text = "In the Description field, enter the name of the tile." +
                        "\nThen press Input and choose the key you want to assign.";
                    SetBorder(_panelsById[4], PanelBoxGlow, 50, 14, 96, 36);
                    
                    PanelBoxGrid.Children.RemoveAt(0);
                    PanelBoxGrid.Children.Add(panel);
                    panel.OpenEditor();
                    break;
                case 4:
                    TutorialText.Text = "Confirm your changes using the green button," +
                        "\nor discard them using the red one.";
                    SetBorder(_panelsById[4], PanelBoxGlow, 98, 46, 80, 38);
                    PanelBoxGrid.Children.RemoveAt(0);
                    PanelBoxGrid.Children.Add(panel);
                    panel.OpenEditor();
                    panel.DescriptionBox.Text = "Sprint";
                    panel.InputBtn.Content = "Shift";
                    break;
                case 5:
                    TutorialText.Text = "To customize the program’s appearance, open the Settings tab in the top-left corner.";
                    PanelBoxGrid.Children.RemoveAt(0);
                    SetBorder(Settings_Button, PanelBoxGlow, 5, 5, 70, 40);
                    break;
                case 6:
                    TutorialText.Text = "To save your layout for later, click Save As at the bottom." +
                        "\nEnter a name and press Save." +
                        "\nSaved profiles can be loaded from the Load tab.";
                    break;
                case 7:
                    TutorialText.TextAlignment = TextAlignment.Center;
                    TutorialText.Text = "That's all!" +
                        "\nEnjoy using ClickyKeys!";
                    break;

                default:
                    TutorialOverlay.Visibility = Visibility.Collapsed;
                    SetTutorialAsMarked();
                    break;
            }
        }

        private void NextTutorial_Click(object sender, RoutedEventArgs e)
        {
            _tutorialStep++;
            UpdateTutorialText();
        }

        private void SkipTutorial_Click(object sender, RoutedEventArgs e)
        {
            TutorialOverlay.Visibility = Visibility.Collapsed;
            SetTutorialAsMarked();
        }

        private void SetBorder(FrameworkElement target, Border targetBox, double offsetX = 0, double offsetY = 0, double sizeX = 10, double sizeY = 10)
        {
            if (!TutorialOverlay.IsVisible)
                return;

            var point = target.TransformToAncestor(this).Transform(new Point(0, 0));

            targetBox.HorizontalAlignment = HorizontalAlignment.Left;
            targetBox.VerticalAlignment = VerticalAlignment.Top;
            targetBox.Width = sizeX;
            targetBox.Height = sizeY;

            targetBox.Margin = new Thickness(
                point.X+ offsetX,
                point.Y+ offsetY,
                0,
                0);
        }

        private void SetTutorialAsMarked()
        {
            try
            {
                lock (_lock)
                {
                    string appName = "ClickyKeys";
                    var appDataDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        appName);
                    Directory.CreateDirectory(appDataDir);
                    string _filePath = Path.Combine(appDataDir, "config.json");


                    var json = File.ReadAllText(_filePath);
                    Configuration config = JsonSerializer.Deserialize<Configuration>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                            ?? new Configuration();

                    config.ShowTutorial = false;

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    json = JsonSerializer.Serialize(config, options);
                    File.WriteAllText(_filePath, json);

                }
            }
            catch { }
        }

        //-------------------------
        // Controls section
        //-------------------------

        private async void VerifyVersion()
        {
            var url = "https://clickykeys.fun/api/releases.php";
            try
            {

                if (new Configuration().Distribution == DistributionType.dev)
                {

                }
                else if (new Configuration().Distribution == DistributionType.store)
                {
                    await _releasesApiClient.GetJsonAsync<MyReleasesResponse[]>(url);
                }
                else if (new Configuration().Distribution == DistributionType.github)
                {
                    var data = await _releasesApiClient.GetJsonAsync<MyReleasesResponse[]>(url);
                    if (data != null)
                    {
                        Version programVersion = new Version(new Configuration().Version);
                        Version officialReleaseVersion = new Version(data[data.Length - 1].Version);

                        if(officialReleaseVersion.CompareTo(programVersion) > 0)
                        {
                            MyPopup.IsOpen = true;
                        }
                    }
                }

            }
            catch (Exception ex) { }

        }

        private void VerifySettings()
        {
            if (_settingsConfiguration.Version != new Configuration().Version)
            {
                _settingsConfiguration.Version = new Configuration().Version;
                _settingsService.Save(_settingsConfiguration);
            }

            if (_panel_settings.Version != new Configuration().Version)
            { 
                _panel_settings.Version = new Configuration().Version;
                _panelsService.Save(_panel_settings);
            }
        }


        public void ShowSettings()
        {
            if(OpenedSettings == false)
            {
                Settings _settings = new(_settingsConfiguration, this, settingsFileName);
                _settings.Show();
                OpenedSettings = true;
            }
        }
        public void OnSettingsClose(string settingsPath)
        {
            _settingsService = new(settingsPath);
            settingsFileName = settingsPath;
            SaveProfileToConfig(settingsFileName);
            _settingsConfiguration = _settingsService.Load();
            Background = new BrushConverter().ConvertFromString(_settingsConfiguration.BackgroundColor) as Brush;
            SetGrid(_settingsConfiguration);
            OpenedSettings = false;
        }

        public void ToggleToolStrip()
        {
            if (_transparent == false)
                if (ToolStrip.Visibility == Visibility.Visible)
                {
                    ToolStrip.Visibility = Visibility.Collapsed;
                    this.Topmost = !this.Topmost;
                }
                else
                {
                    ToolStrip.Visibility = Visibility.Visible;
                    this.Topmost = !this.Topmost;
                }
        }

        public void ResetCounter()
        {
            _counter.Reset();
        }
        public void TransparentMode()
        {
            if (_transparentWindow == null)
            {
                myGrid.Visibility = Visibility.Collapsed;
                Settings_Button.Visibility = Visibility.Collapsed;
                InitTransparentMode();
            }
            else
            {
                myGrid.Visibility = Visibility.Visible;
                Settings_Button.Visibility = Visibility.Visible;
                _transparentWindow?.Close();
                _transparentWindow = null;
            }

        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        public void ShowInfo()
        {
            if (OpenedInfo == false)
            {
                Info _infoPage = new(this);
                _infoPage.Show();
                OpenedInfo = true;
            }
        }
        public void OnInfoClose()
        {
            OpenedInfo = false;
        }
        private void SaveProfileToConfig(string profile)
        {
            try
            {
                lock (_lock)
                {
                    string appName = "ClickyKeys";
                    var appDataDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        appName);
                    Directory.CreateDirectory(appDataDir);
                    string _filePath = Path.Combine(appDataDir, "config.json");


                    var json = File.ReadAllText(_filePath);
                    Configuration config = JsonSerializer.Deserialize<Configuration>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                            ?? new Configuration();

                    string[] names = Regex.Split(profile, @"ClickyKeys\\settings\\");
                    config.SettingsProfile = names[1];

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    json = JsonSerializer.Serialize(config, options);
                    File.WriteAllText(_filePath, json);
                }
            }
            catch { }
        }

        private void UpdateValues()
        {
            var stats = _counter.GetStats();
            for (int i = 0; i < cols * rows; i++)
            {
                var panel = _panelsById[i];
                foreach (var (c, it, n, v) in stats.Take(cols * rows))
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

        public void SetBackgroundRainbow(bool? IsTrue)
        {
            _settingsConfiguration.IsBackgroundRainbow = IsTrue ?? false;
            if (_settingsConfiguration.IsBackgroundRainbow == false)
                //LoadBackgroundFromSettings();
                SetGrid(_settingsConfiguration);
        }

        public void OnGridChange(SettingsConfiguration settings)
        {
            SetGrid(settings);
        }
        //-------------------------
        // OnColorChanges section
        //-------------------------

        private void OnPanelsColorChanged(Color c)
        {
            // on panels color change in settings -> change panels color
            for (int i = 0; i < cols * rows; i++)
            {
                var panel = _panelsById[i];
                panel.PanelColor = c;
            }
        }

        private void OnKeysColorChanged(Color c)
        {
            // on panels color change in settings -> change panels color
            for (int i = 0; i < cols * rows; i++)
            {
                var panel = _panelsById[i];
                panel.KeyTextColor = c;
            }
        }

        private void OnValuesColorChanged(Color c)
        {
            // on panels color change in settings -> change panels color
            for (int i = 0; i < cols * rows; i++)
            {
                var panel = _panelsById[i];
                panel.ValueTextColor = c;
            }
        }


        //-------------------------
        // New version popup
        //-------------------------

        private void ClosePopup_Click(object sender, RoutedEventArgs e)
        {
            MyPopup.IsOpen = false;
        }

        private void OpenLink_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://clickykeys.fun/update#downloads";

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }

            MyPopup.IsOpen = false;
        }




        //-------------------------
        // Grid section
        //-------------------------

        private void SetGrid(SettingsConfiguration settings)
        {

            int id = 0;
            Key n;
            string d;
            InputType input;
            myGrid.Children.Clear();
            myGrid.RowDefinitions.Clear();
            myGrid.ColumnDefinitions.Clear();

            // update grid size
            rows = settings.GridRows;
            cols = settings.GridColumns;

            for (int r = 0; r < settings.GridRows; r++)
            {
                myGrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            }
            for (int c = 0; c < settings.GridColumns; c++)
            {
                myGrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            }


            for (int r = 0; r < settings.GridRows; r++)
            {
                for (int c = 0; c < settings.GridColumns; c++)
                {
                    if (_panel_settings.Panels[id].Input == InputType.None && _panel_settings.Panels[id].KeyCode == Key.None)
                        d = $"id. {id}";
                    else d = $"{_panel_settings.Panels[id].Description}";

                    n = _panel_settings.Panels[id].KeyCode;
                    input = _panel_settings.Panels[id].Input;
                    Color panelColor = (Color)ColorConverter.ConvertFromString(settings.PanelsColor);
                    Color keysColor = (Color)ColorConverter.ConvertFromString(settings.KeysTextColor);
                    Color valuesColor = (Color)ColorConverter.ConvertFromString(settings.ValuesTextColor);
                    FontSettings keysFont = settings.KeysFontSettings;
                    FontSettings valuesFont = settings.ValuesFontSettings;

                    var panel = new GlassPanelWpf(this)
                    {
                        ID = id,
                        Value = 0,
                        Description = d,
                        Type = input,
                        Key = n,
                        PanelColor = panelColor,
                        KeyTextColor = keysColor,
                        ValueTextColor = valuesColor,
                        KeyFont = keysFont,
                        ValueFont = valuesFont,
                    };

                    _panelsById[id] = panel;
                    Grid.SetRow(panel, r);
                    Grid.SetColumn(panel, c);

                    myGrid.Children.Add(panel);
                    id++;
                }
            }

        }

        public void SavePanelConfiguration(PanelsSettings state)
        {
            //_counter.Dispose();
            for (int i = 0; i < rows * cols; i++)
            {
                if (_panel_settings.Panels[i].Input == state.Input
                    && _panel_settings.Panels[i].KeyCode == state.KeyCode)
                {
                    _panel_settings.Panels[i].KeyCode = Key.None;
                    _panel_settings.Panels[i].Input = InputType.None;
                    _panel_settings.Panels[i].Description = "";
                    _counter.ResetSingle(state.KeyCode);
                }
            }
            int id = state.Index;
            _panel_settings.Panels[id].KeyCode = state.KeyCode;
            _panel_settings.Panels[id].Input = state.Input;
            _panel_settings.Panels[id].Description = state.Description;
            _panelsService.Save(_panel_settings);
            LoadPanelConfiguration();
            SetGrid(_settingsConfiguration);
            //_counter.Start();
        }


        //-------------------------
        // Colors converter section
        //-------------------------

        // RGB (0..255) -> HSV (H 0..360, S 0..1, V 0..1)
        private static void RgbToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            // Hue
            if (delta == 0) h = 0;
            else if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * (((b - r) / delta) + 2);
            else /* max == b */ h = 60 * (((r - g) / delta) + 4);

            if (h < 0) h += 360;

            // Saturation
            s = (max == 0) ? 0 : delta / max;

            // Value
            v = max;
        }

        // HSV -> RGB (Color, 0..255)
        private static Color HsvToRgb(double h, double s, double v)
        {
            double c = v * s;                 // chroma
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = v - c;

            double r1, g1, b1;
            if (h < 60) (r1, g1, b1) = (c, x, 0);
            else if (h < 120) (r1, g1, b1) = (x, c, 0);
            else if (h < 180) (r1, g1, b1) = (0, c, x);
            else if (h < 240) (r1, g1, b1) = (0, x, c);
            else if (h < 300) (r1, g1, b1) = (x, 0, c);
            else (r1, g1, b1) = (c, 0, x);

            byte R = (byte)Math.Round((r1 + m) * 255);
            byte G = (byte)Math.Round((g1 + m) * 255);
            byte B = (byte)Math.Round((b1 + m) * 255);

            return Color.FromRgb(R, G, B);
        }
    }

    public sealed class StatRow
    {
        public Key Code { get; set; }
        public InputType Input { get; set; }
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }
}