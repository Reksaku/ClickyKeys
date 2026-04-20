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
using System.Windows.Media.Animation;
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

        // Single mutable brush backing Background; animated on the composition
        // thread when rainbow mode is on — no per-frame allocations.
        private readonly SolidColorBrush _backgroundBrush = new(Colors.White);

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

            // Skip the update check in the transparent sub-window — it shares
            // the parent's counter and shouldn't ping the API a second time
            // or raise a duplicate popup.
            if (!_transparent)
                VerifyVersion(ConfigSettings);
            VerifySettings();

            // Push updates into the UI only when a counter actually changes
            // (hook callbacks already run on the UI thread). This replaces the
            // former 100 Hz DispatcherTimer polling of every panel.
            _counter.PanelValueChanged += OnCounterPanelValueChanged;
            _counter.CountersReset += OnCountersReset;

            // color subscriber start
            WrmSubscriberStart();

            // set panels grid
            SetGrid(_settingsConfiguration);

            // Kick off rainbow animation if enabled (otherwise no-op).
            UpdateRainbowState();

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


        private void InitTransparentMode()
        {
            _transparentWindow = new MainWindow(true, _counter);
            _transparentWindow.Show();

        }

        /// <summary>
        /// Drives the background color. When rainbow is OFF the brush holds a
        /// static color. When ON a <see cref="ColorAnimationUsingKeyFrames"/>
        /// cycles the single <see cref="_backgroundBrush"/> on the WPF
        /// composition thread — no per-frame allocations, no UI-thread ticks.
        /// </summary>
        private void UpdateRainbowState()
        {
            // Always clear any running animation first.
            _backgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);

            // Make sure Background is bound to our mutable brush (may be
            // Brushes.Transparent in transparent mode — leave that alone).
            if (!_transparent && !ReferenceEquals(Background, _backgroundBrush))
                Background = _backgroundBrush;

            if (!_settingsConfiguration.IsBackgroundRainbow)
            {
                _backgroundBrush.Color = allColors.background;
                return;
            }

            // Derive saturation/value from the user's chosen background color
            // so the rainbow respects their brightness preference.
            RgbToHsv(allColors.background, out _, out double sat, out double val);

            var anim = new ColorAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromSeconds(6)),
                RepeatBehavior = RepeatBehavior.Forever
            };

            const int steps = 24;
            for (int i = 0; i <= steps; i++)
            {
                double hue = i * 360.0 / steps;
                var color = HsvToRgb(hue, sat, val);
                anim.KeyFrames.Add(new LinearColorKeyFrame(
                    color,
                    KeyTime.FromPercent((double)i / steps)));
            }

            _backgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        public void LoadBackgroundFromSettings()
        {
            allColors.background = (Color)ColorConverter.ConvertFromString(_settingsConfiguration.BackgroundColor);
            _backgroundBrush.Color = allColors.background;
            if (!_transparent)
                Background = _backgroundBrush;
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
                    AtomicFile.WriteAllText(_filePath, json);
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
            // Unhook from counter events on both main and transparent windows
            // to prevent leaks and updates into disposed controls.
            _counter.PanelValueChanged -= OnCounterPanelValueChanged;
            _counter.CountersReset -= OnCountersReset;

            // Stop any background animation we own.
            _backgroundBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);

            // Block (briefly) on any pending debounced writes so the user's
            // latest edit is durable before the process exits. Only the main
            // window owns the services; the transparent sub-window shares
            // them and would double-flush otherwise.
            if (!_transparent)
            {
                try
                {
                    _panelsService.FlushAsync().Wait(TimeSpan.FromSeconds(2));
                    _settingsService.FlushAsync().Wait(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Window_Closed flush failed: {ex}");
                }
            }

            if (_transparent == false)
                _counter.Dispose();
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
                                // Mutate the existing brush rather than
                                // replacing it, so any running rainbow
                                // animation stays attached; in rainbow mode
                                // rebuild the animation with new sat/val.
                                allColors.background = c;
                                if (_settingsConfiguration.IsBackgroundRainbow)
                                    UpdateRainbowState();
                                else
                                    _backgroundBrush.Color = c;
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

                // Keep the transparent sub-window in sync with panel edits
                // done on the main window. Only the transparent instance
                // needs to react here — the main window already applies the
                // change inline in SavePanelConfiguration.
                if (_transparent)
                {
                    WeakReferenceMessenger.Default.Register<PanelsChangedMessage>(
                        recipient: this,
                        handler: (r, m) =>
                        {
                            if (m.Value == null) return;

                            _panel_settings = m.Value;
                            // The transparent window shares the parent's
                            // counter, so we do NOT reload it here (the
                            // parent already did).
                            SetGrid(_settingsConfiguration);
                        });
                }
            };


            Closed += (_, __) =>
            {
                // UnregisterAll covers both ColorChangedMessage and the
                // transparent-only PanelsChangedMessage subscription without
                // needing the caller to remember which channels were live.
                WeakReferenceMessenger.Default.UnregisterAll(this);
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
                    AtomicFile.WriteAllText(_filePath, json);

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetTutorialAsMarked failed: {ex}");
            }
        }

        //-------------------------
        // Controls section
        //-------------------------

        /// <summary>
        /// Checks the releases endpoint according to the active distribution.
        /// <para>
        /// Distribution now comes from <see cref="BuildInfo.Distribution"/>
        /// — a compile-time constant — instead of the JSON-deserialised
        /// <see cref="Configuration"/>. That way a user can't edit
        /// <c>config.json</c> to flip to <c>dev</c> and suppress the update
        /// prompt.
        /// </para>
        /// <para>
        /// Both the <c>store</c> and <c>github</c> channels now follow the
        /// same verify-and-notify flow: fetch the releases feed, filter by
        /// this build's channel, pick the highest valid version, and raise
        /// <c>MyPopup</c> if it is newer than the running version.
        /// </para>
        /// </summary>
        private async void VerifyVersion(Configuration cfg)
        {
            const string url = "https://clickykeys.fun/api/releases.php";

            try
            {
                // Dev builds intentionally skip the check — no release feed
                // to compare against.
                if (BuildInfo.Distribution == DistributionType.dev)
                    return;

                // store + github share the same logic. If more channels are
                // added later, add them to the guard below.
                if (BuildInfo.Distribution != DistributionType.store
                    && BuildInfo.Distribution != DistributionType.github)
                    return;

                var data = await _releasesApiClient
                    .GetJsonAsync<MyReleasesResponse[]>(url);

                if (data == null || data.Length == 0)
                    return;

                // Only consider releases tagged for this build's channel so
                // a store build doesn't get prompted about a github-only
                // release and vice versa.
                Version? latest = null;
                foreach (var entry in data)
                {
                    if (entry.distribution != BuildInfo.Distribution)
                        continue;

                    if (Version.TryParse(entry.Version, out var parsed)
                        && (latest == null || parsed > latest))
                    {
                        latest = parsed;
                    }
                }

                if (latest == null)
                    return;

                if (!Version.TryParse(cfg.Version, out var current))
                    return;

                if (latest > current)
                    MyPopup.IsOpen = true;
            }
            catch (Exception ex)
            {
                // Surface the failure to attached debuggers / log collectors
                // rather than silently eating it.
                Debug.WriteLine($"VerifyVersion failed: {ex}");
            }
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
            LoadBackgroundFromSettings();
            UpdateRainbowState();
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
                    // Atomic write — if the process dies mid-write the user's
                    // config.json stays the previous valid copy rather than a
                    // truncated file that crashes next startup.
                    AtomicFile.WriteAllText(_filePath, json);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveProfileToConfig failed: {ex}");
            }
        }

        /// <summary>
        /// Event-driven replacement for the old 100 Hz UpdateValues() polling.
        /// Called directly from <see cref="InputCounter"/> on the UI thread
        /// whenever a tracked counter changes — O(1) lookup, one panel update,
        /// one flash trigger.
        /// </summary>
        private void OnCounterPanelValueChanged(int panelIndex, int newValue)
        {
            if (!_panelsById.TryGetValue(panelIndex, out var panel))
                return;

            if (panel.Value == newValue)
                return;

            panel.Value = newValue;
            panel.TriggerFlash();
        }

        private void OnCountersReset()
        {
            foreach (var panel in _panelsById.Values)
                panel.Value = 0;
        }

        public void SetBackgroundRainbow(bool? IsTrue)
        {
            _settingsConfiguration.IsBackgroundRainbow = IsTrue ?? false;
            UpdateRainbowState();
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

        /// <summary>
        /// Syncs the visible grid to <paramref name="settings"/>. A full rebuild
        /// (Children.Clear + new <see cref="GlassPanelWpf"/> instances) only
        /// runs when the grid dimensions changed. Otherwise existing panels
        /// have their state updated in place, which preserves their
        /// <c>Value</c>/flash state and avoids WPF control construction costs.
        /// </summary>
        private void SetGrid(SettingsConfiguration settings)
        {
            // Parse shared state once rather than in every iteration.
            Color panelColor = (Color)ColorConverter.ConvertFromString(settings.PanelsColor);
            Color keysColor = (Color)ColorConverter.ConvertFromString(settings.KeysTextColor);
            Color valuesColor = (Color)ColorConverter.ConvertFromString(settings.ValuesTextColor);
            FontSettings keysFont = settings.KeysFontSettings;
            FontSettings valuesFont = settings.ValuesFontSettings;

            bool dimensionsChanged =
                settings.GridRows != rows ||
                settings.GridColumns != cols ||
                _panelsById.Count != settings.GridRows * settings.GridColumns;

            if (!dimensionsChanged)
            {
                // Fast path: reuse existing controls.
                int total = rows * cols;
                for (int id = 0; id < total; id++)
                {
                    if (_panelsById.TryGetValue(id, out var existing))
                        ApplyPanelState(existing, id, panelColor, keysColor,
                            valuesColor, keysFont, valuesFont, resetValue: false);
                }
                return;
            }

            // Full rebuild: dimensions changed.
            myGrid.Children.Clear();
            myGrid.RowDefinitions.Clear();
            myGrid.ColumnDefinitions.Clear();
            _panelsById.Clear();

            rows = settings.GridRows;
            cols = settings.GridColumns;

            for (int r = 0; r < rows; r++)
                myGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < cols; c++)
                myGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            int nextId = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var panel = new GlassPanelWpf(this) { ID = nextId };
                    ApplyPanelState(panel, nextId, panelColor, keysColor,
                        valuesColor, keysFont, valuesFont, resetValue: true);

                    _panelsById[nextId] = panel;
                    Grid.SetRow(panel, r);
                    Grid.SetColumn(panel, c);
                    myGrid.Children.Add(panel);
                    nextId++;
                }
            }
        }

        private void ApplyPanelState(
            GlassPanelWpf panel,
            int id,
            Color panelColor,
            Color keysColor,
            Color valuesColor,
            FontSettings keysFont,
            FontSettings valuesFont,
            bool resetValue)
        {
            var cfg = _panel_settings.Panels[id];

            panel.Description = (cfg.Input == InputType.None && cfg.KeyCode == Key.None)
                ? $"id. {id}"
                : cfg.Description;
            panel.Type = cfg.Input;
            panel.Key = cfg.KeyCode;
            panel.PanelColor = panelColor;
            panel.KeyTextColor = keysColor;
            panel.ValueTextColor = valuesColor;
            panel.KeyFont = keysFont;
            panel.ValueFont = valuesFont;

            // For newly-constructed panels seed the value from the live
            // counter (0 if unknown). In-place updates keep the current
            // display value so the user doesn't see counters flash to 0.
            if (resetValue)
                panel.Value = _counter?.GetPanelCount(id) ?? 0;
        }

        public void SavePanelConfiguration(PanelsSettings state)
        {
            // If the chosen input is already bound to another panel, clear
            // that panel and zero its counter. Pass the index directly so the
            // reset hits the correct counter (old code iterated a Dictionary
            // and used the iteration index — undefined ordering, wrong panel).
            for (int i = 0; i < rows * cols; i++)
            {
                if (i == state.Index) continue;

                if (_panel_settings.Panels[i].Input == state.Input
                    && _panel_settings.Panels[i].KeyCode == state.KeyCode)
                {
                    _panel_settings.Panels[i].KeyCode = Key.None;
                    _panel_settings.Panels[i].Input = InputType.None;
                    _panel_settings.Panels[i].Description = "";
                    _counter.ResetSingle(i);
                }
            }

            int id = state.Index;
            _panel_settings.Panels[id].KeyCode = state.KeyCode;
            _panel_settings.Panels[id].Input = state.Input;
            _panel_settings.Panels[id].Description = state.Description;

            // Reassigning a panel invalidates its running count.
            _counter.ResetSingle(id);

            // Debounced atomic save — coalesces rapid-fire panel edits into a
            // single trailing write and doesn't block the UI thread on disk.
            // The in-memory state is already authoritative for this session,
            // so the UI updates (LoadPanels + SetGrid) don't need to wait.
            _panelsService.SaveDebounced(_panel_settings);

            // Rebind the live counter to the new layout and refresh the grid
            // before the disk write finishes. No need to reload from disk —
            // _panel_settings is already the source of truth here.
            _counter.LoadPanels(_panel_settings);
            SetGrid(_settingsConfiguration);

            // Broadcast the change so the transparent sub-window (which
            // shares _counter but has its own UI tree) rebuilds its grid
            // without having to re-read the JSON file.
            WeakReferenceMessenger.Default.Send(
                new PanelsChangedMessage(_panel_settings));
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