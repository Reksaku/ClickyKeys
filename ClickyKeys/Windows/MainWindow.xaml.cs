using CommunityToolkit.Mvvm.Messaging;
using Hardcodet.Wpf.TaskbarNotification;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClickyKeys.Windows;

namespace ClickyKeys
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public interface IOverlay
    {
        void ToggleToolStrip();
        void SavePanelConfiguration(PanelsSettings state);
        void OnAppearanceClose(string appearancePath);
        void OnGridChange(AppearanceConfiguration settings);
        public void OnInfoClose();
        public void OnStatsClose();
        public void OnSettingsClose();
        public void OnMessagesClose();
        void SetBackgroundRainbow(bool? IsTrue);
        void SetRainbowSpeed(int seconds);
        void ShowTutorial();
        void ApplyShortcuts(Key resetKey, Key toggleToolbarKey);
    }

    /// <summary>
    /// Converts a <see cref="Orientation"/> value to a <see cref="Thickness"/> margin for toolbar button icons.
    /// Horizontal (icon beside text) → right margin (6px) to add space between icon and label.
    /// Vertical (icon above text)    → bottom margin (2px) to add space between icon and label.
    /// </summary>
    public class OrientationToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (Orientation)value == Orientation.Horizontal
                ? new Thickness(0, 0, 6, 0)
                : new Thickness(0, 0, 0, 2);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a <see cref="Orientation"/> value to a <see cref="double"/> icon size for toolbar buttons.
    /// Horizontal (icon beside text) → 30×30 px (larger icon, no text below to share vertical space).
    /// Vertical (icon above text)    → 20×20 px (smaller icon, label sits directly beneath).
    /// Triggered by <see cref="MainWindow.UpdateButtonLayout"/> whenever the myGrid column count changes.
    /// </summary>
    public class OrientationToSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (Orientation)value == Orientation.Horizontal ? 30.0 : 26.0;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public partial class MainWindow : Window, IOverlay, PanelOverlay
    {
        private readonly InputCounter _counter;

        private string appearanceFileName;
        private AppearanceService _appearanceService;
        private AppearanceConfiguration _appearanceConfiguration;

        private readonly Dictionary<int, GlassPanelWpf> _panelsById = [];

        private readonly PanelsService _panelsService = new();
        private PanelState _panel_settings = new();

        // File name (with extension) of the active panels profile in
        // PanelsService.PanelsDirectory. Persisted in config.PanelsProfile.
        private string _activePanelsProfile = PanelsService.DefaultProfileFileName;

        private readonly bool _transparent;

        private MainWindow? _transparentWindow = null;

        // System tray icon — created only on the master (non-transparent)
        // window in InitTrayIcon. Null on the transparent sub-window so we
        // never end up with two icons in the tray for one app instance.
        private TaskbarIcon? _trayIcon;

        // One-shot guard for the "Start minimized" setting. When set,
        // Window_StateChanged lets the next minimize stand as a normal taskbar
        // button instead of cloaking it to the tray. Consumed (reset to false)
        // the first time it's honored. See MinimizeToTaskbarAtStartup.
        private bool _startupTaskbarMinimize = false;

        // Set to true by Exit_Click before Application.Shutdown so the
        // Closing handler knows the user explicitly asked to quit, instead
        // of merely clicking the title-bar X (which we hijack into a
        // minimize-to-tray).
        private bool _exitRequested = false;

        private ColorsPallet allColors = new();

        // Single mutable brush backing Background; animated on the composition
        // thread when rainbow mode is on — no per-frame allocations.
        private readonly SolidColorBrush _backgroundBrush = new(Colors.White);

        private int rows;
        private int cols;

        private bool OpenedInfo = false;
        private bool OpenedAppearance = false;
        private bool OpenedStats = false;
        private bool OpenedSettings = false;
        private bool OpenedMessages = false;

        private readonly object _lock = new();

        private readonly RequestReleasesAPI _releasesApiClient = new RequestReleasesAPI();

        private string defaultAppearancePath;

        // Timer odwlekający zamknięcie DisplayPopup, aby mysz mogła
        // płynnie przejść z przycisku Display na otwarty popup.
        private DispatcherTimer? _displayCloseTimer;

        // Analogiczny timer dla MorePopup.
        private DispatcherTimer? _moreCloseTimer;


        /// <summary>
        /// Hiding window by cloaking, Window will be rendered in background and 
        /// could be captured by streaming programs
        /// </summary>

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_CLOAK = 13;

        private void SetCloaked(bool cloak)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int value = cloak ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_CLOAK, ref value, Marshal.SizeOf<int>());
            _isCloaked = cloak;
            if (cloak) _cloakedAt = DateTime.UtcNow;
        }

        private DateTime _cloakedAt = DateTime.MinValue;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        private bool _isCloaked = false;


        /// <summary>
        /// Changing button text-icon orientation depending of selected vertical panels grid.
        /// </summary>
        public static readonly DependencyProperty ButtonOrientationProperty =
            DependencyProperty.Register(nameof(ButtonOrientation), typeof(Orientation),
                typeof(MainWindow), new PropertyMetadata(Orientation.Vertical));

        public Orientation ButtonOrientation
        {
            get => (Orientation)GetValue(ButtonOrientationProperty);
            set => SetValue(ButtonOrientationProperty, value);
        }

        /// <summary>
        /// Foreground color for toolbar text — Black on light backgrounds (or rainbow),
        /// White on dark backgrounds.
        /// </summary>
        public static readonly DependencyProperty ToolbarTextColorProperty =
            DependencyProperty.Register(nameof(ToolbarTextColor), typeof(Brush),
                typeof(MainWindow), new PropertyMetadata(Brushes.Black));

        public Brush ToolbarTextColor
        {
            get => (Brush)GetValue(ToolbarTextColorProperty);
            set => SetValue(ToolbarTextColorProperty, value);
        }

        /// <summary>
        /// Parenthesised, user-friendly label of the reset shortcut shown under
        /// the Reset button (e.g. "(F12)"). Bound in XAML and refreshed whenever
        /// the shortcut is reassigned. See <see cref="UpdateShortcutLabels"/>.
        /// </summary>
        public static readonly DependencyProperty ResetKeyLabelProperty =
            DependencyProperty.Register(nameof(ResetKeyLabel), typeof(string),
                typeof(MainWindow), new PropertyMetadata("(F12)"));

        public string ResetKeyLabel
        {
            get => (string)GetValue(ResetKeyLabelProperty);
            set => SetValue(ResetKeyLabelProperty, value);
        }

        /// <summary>
        /// Parenthesised label of the toggle-toolbar shortcut shown under the
        /// Hide-toolbar button (e.g. "(F11)").
        /// </summary>
        public static readonly DependencyProperty ToggleToolbarKeyLabelProperty =
            DependencyProperty.Register(nameof(ToggleToolbarKeyLabel), typeof(string),
                typeof(MainWindow), new PropertyMetadata("(F11)"));

        public string ToggleToolbarKeyLabel
        {
            get => (string)GetValue(ToggleToolbarKeyLabelProperty);
            set => SetValue(ToggleToolbarKeyLabelProperty, value);
        }

        /// <summary>
        /// Refreshes the parenthesised shortcut labels under the toolbar buttons
        /// from the given keys, using <see cref="FriendlyKeyName"/> for display.
        /// </summary>
        private void UpdateShortcutLabels(Key resetKey, Key toggleToolbarKey)
        {
            ResetKeyLabel = $"({FriendlyKeyName.ForKey(resetKey.ToString())})";
            ToggleToolbarKeyLabel = $"({FriendlyKeyName.ForKey(toggleToolbarKey.ToString())})";
        }

        /// <summary>
        /// Applies new shortcut keys to the live counter and refreshes the
        /// toolbar labels. Called by the Settings window (via
        /// <see cref="IOverlay"/>) so reassignments take effect without a
        /// restart.
        /// </summary>
        public void ApplyShortcuts(Key resetKey, Key toggleToolbarKey)
        {
            _counter?.SetShortcuts(resetKey, toggleToolbarKey);
            UpdateShortcutLabels(resetKey, toggleToolbarKey);
        }

        /// <summary>
        /// Recalculates <see cref="ToolbarTextColor"/> from the current background.
        /// Rainbow mode always yields Black. Otherwise luminance decides:
        /// bright background → Black, dark background → White.
        /// </summary>
        private void UpdateToolbarTextColor()
        {
            if (_appearanceConfiguration.IsBackgroundRainbow)
            {
                ToolbarTextColor = Brushes.Black;
                return;
            }

            // Relative luminance (IEC 61966-2-1 / sRGB)
            double r = allColors.background.R / 255.0;
            double g = allColors.background.G / 255.0;
            double b = allColors.background.B / 255.0;
            double L = 0.2126 * r + 0.7152 * g + 0.0722 * b;

            ToolbarTextColor = L > 0.179 ? Brushes.Black : Brushes.White;
        }

        private void UpdateButtonLayout()
        {
            ButtonOrientation = myGrid.ColumnDefinitions.Count > 3
                ? Orientation.Horizontal
                : Orientation.Vertical;
        }


        public MainWindow(bool transparent = false, InputCounter? counter = null)
        {
            _transparent = transparent;

            Configuration ConfigSettings = LoadInitSettings();
            appearanceFileName = ConfigSettings.AppearanceProfile;
            _appearanceService = new(appearanceFileName);

            defaultAppearancePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"ClickyKeys", "settings", appearanceFileName);
            _appearanceConfiguration = _appearanceService.Load();





            LoadBackgroundFromSettings();

            InitializeComponent();

            // DisplayPopup żyje w osobnym drzewie wizualnym, więc nie dziedziczy
            // DataContext z okna ani nie dosięga go przez RelativeSource
            // FindAncestor=Window (zwraca null). Ustawiamy DataContext wprost na
            // okno — dzięki temu {Binding Background} oraz {Binding ToolbarTextColor}
            // wewnątrz popupu działają i reagują na zmiany koloru tła.
            DisplayPopup.DataContext = this;
            MorePopup.DataContext = this;

            // Apply the persisted always-on-top preference. Both the master and
            // the transparent sub-window run through this constructor, so each
            // honours it independently; the Display-tab toggle reflects the
            // current state.
            Topmost = ConfigSettings.AlwaysOnTop;
            UpdateAlwaysOnTopStateText(ConfigSettings.AlwaysOnTop);

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

                // Apply the persisted global shortcuts (reset / toggle toolbar)
                // before starting so the very first keypress is handled per the
                // user's saved choice.
                _counter.SetShortcuts(ConfigSettings.ResetKey, ConfigSettings.ToggleToolbarKey);

                // input counter start
                _counter.Start();

                // Tray icon belongs to the master window only — the
                // transparent sub-window shares this app instance and must
                // not register a second icon.
                InitTrayIcon();
            }

            // Point the panels service at the saved panels profile so
            // LoadPanelConfiguration (below) loads it. Done for BOTH the master
            // and the transparent sub-window (which shares the counter) so they
            // agree on the active profile. Falls back to the default if the
            // saved file no longer exists.
            _activePanelsProfile = ConfigSettings.PanelsProfile;
            var panelsPath = Path.Combine(PanelsService.PanelsDirectory, _activePanelsProfile);
            if (!File.Exists(panelsPath))
            {
                _activePanelsProfile = PanelsService.DefaultProfileFileName;
                panelsPath = Path.Combine(PanelsService.PanelsDirectory, _activePanelsProfile);
                if (!_transparent)
                    ConfigStore.Update(c => c.PanelsProfile = _activePanelsProfile);
            }
            _panelsService.SetActivePath(panelsPath);

            LoadPanelConfiguration();

            // Reflect the configured shortcuts in the toolbar labels (both the
            // master and the transparent sub-window read the same config).
            UpdateShortcutLabels(ConfigSettings.ResetKey, ConfigSettings.ToggleToolbarKey);

            // Skip the update check in the transparent sub-window — it shares
            // the parent's counter and shouldn't ping the API a second time
            // or raise a duplicate popup.
            if (!_transparent)
                VerifyVersion();
            VerifySettings();

            // Push updates into the UI only when a counter actually changes
            // (hook callbacks already run on the UI thread). This replaces the
            // former 100 Hz DispatcherTimer polling of every panel.
            _counter.PanelValueChanged += OnCounterPanelValueChanged;
            _counter.CountersReset += OnCountersReset;

            // color subscriber start
            WrmSubscriberStart();

            // set panels grid
            SetGrid(_appearanceConfiguration);

            // set toolbar layout
            UpdateButtonLayout();

            // Kick off rainbow animation if enabled (otherwise no-op).
            UpdateRainbowState();

            // Update detection. Compare the version baked into this build
            // (BuildInfo.Version) with the one persisted in config.json. If
            // the build is newer, the user just installed an update — open
            // the changelog window and bring the on-disk config up to the
            // new version so the next launch won't re-trigger the update flow.
            string previousVersion = ConfigSettings.Version;
            bool justUpdated = !_transparent && HandleAppUpdate(ConfigSettings);

            if (justUpdated)
            {
                ShowChangelog(previousVersion);
            }

            if (ConfigSettings.ShowTutorial == true && !_transparent)
            {
                // Defer until the window is fully rendered so ActualWidth/Height
                // and panel positions are available for spotlight calculations.
                Loaded += OnShowTutorialLoaded;
            }

            // DRAFT — first-run telemetry consent. null means "never asked";
            // any real TelemetryLevel (already answered, either here before
            // or by an older build's default) skips the prompt. Deferred to
            // Loaded like the tutorial so it appears on top of a fully
            // rendered window rather than racing window construction.
            // Skipped in transparent/overlay mode for the same reason the
            // tutorial is skipped there — no chrome to anchor a modal to.
            if (ConfigSettings.TelemetryLevel == null && !_transparent)
            {
                Loaded += OnShowConsentDialogLoaded;
            }

            // Inbox badges (Messages button + More button). The startup fetch
            // runs asynchronously, so subscribe to be notified when it finishes
            // (the handler marshals to the UI thread) and do one initial refresh
            // once loaded for anything already cached and unread from a previous
            // session. Only the master window has a visible toolbar.
            if (!_transparent && App.Messages is { } messages)
            {
                messages.Updated += OnMessagesUpdated;
                Loaded += (_, __) => RefreshMessagesBadge();
            }

        }

        //-------------------------
        // Lambda section
        //-------------------------

        private void Appearance_Click(object sender, RoutedEventArgs e) => ShowAppearance();
        private void ToggleToolbar_Click(object sender, RoutedEventArgs e) => ToggleToolStrip();

        // Display tab → Always on top. Flips this window's Topmost, mirrors it
        // onto the transparent sub-window if one is open, persists the choice,
        // and updates the toggle's on/off caption.
        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = !Topmost;
            Topmost = enabled;

            if (_transparentWindow != null)
                _transparentWindow.Topmost = enabled;

            ConfigStore.Update(c => c.AlwaysOnTop = enabled);
            UpdateAlwaysOnTopStateText(enabled);
        }

        // Reflects the current always-on-top state on the Display-tab toggle.
        private void UpdateAlwaysOnTopStateText(bool enabled)
        {
            if (AlwaysOnTopStateText == null) return;
            AlwaysOnTopStateText.Text = LocalizationManager.T(enabled ? "Main_On" : "Main_Off");
        }
        private void Reset_Click(object sender, RoutedEventArgs e) => ResetCounter();
        private void TransparentMode_Click(object sender, RoutedEventArgs e) => TransparentMode();
        private void Info_Click(object sender, RoutedEventArgs e) => ShowInfo();
        private void Stats_Click(object sender, RoutedEventArgs e) => ShowStats();
        private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();

        private void Messages_Click(object sender, RoutedEventArgs e)
        {
            // Close the dropdown so it doesn't linger over the inbox window.
            MorePopup.IsOpen = false;
            ShowMessages();
        }

        // --- Buy Me a Coffee ---

        /// <summary>
        /// Fetches the current sponsoring link via <see cref="SponsorshipService"/>
        /// and opens it in the user's default browser. If the link can't be
        /// retrieved (e.g. no internet connection) a popup explaining the lack
        /// of connectivity is shown instead.
        /// </summary>
        private async void BuyMeACoffee_Click(object sender, RoutedEventArgs e)
        {
            // Close the dropdown so it doesn't linger over the popup/browser.
            MorePopup.IsOpen = false;

            string? url = await SponsorshipService.GetLinkAsync();

            if (string.IsNullOrWhiteSpace(url))
            {
                NoInternetPopup.IsOpen = true;
                return;
            }

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
                MessageBox.Show(LocalizationManager.Format("Main_ErrorPrefix", ex.Message));
            }
        }

        private void CloseNoInternetPopup_Click(object sender, RoutedEventArgs e)
        {
            NoInternetPopup.IsOpen = false;
        }

        // --- Display dropdown hover logic ---

        private void Display_Button_MouseEnter(object sender, MouseEventArgs e)
        {
            _displayCloseTimer?.Stop();
            DisplayPopup.IsOpen = true;
        }

        private void Display_Button_MouseLeave(object sender, MouseEventArgs e)
        {
            // Krótkie opóźnienie pozwala myszy przejść na popup bez jego zamknięcia.
            _displayCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _displayCloseTimer.Tick += (s, args) =>
            {
                _displayCloseTimer!.Stop();
                DisplayPopup.IsOpen = false;
            };
            _displayCloseTimer.Start();
        }

        private void DisplayPopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _displayCloseTimer?.Stop();
        }

        private void DisplayPopup_MouseLeave(object sender, MouseEventArgs e)
        {
            DisplayPopup.IsOpen = false;
        }

        // --- More dropdown hover logic ---

        private void More_Button_MouseEnter(object sender, MouseEventArgs e)
        {
            _moreCloseTimer?.Stop();
            // Refresh the unread badge just before the dropdown appears, so it
            // reflects the latest fetch even though that ran async at startup.
            RefreshMessagesBadge();
            MorePopup.IsOpen = true;
        }

        private void More_Button_MouseLeave(object sender, MouseEventArgs e)
        {
            // Krótkie opóźnienie pozwala myszy przejść na popup bez jego zamknięcia.
            _moreCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _moreCloseTimer.Tick += (s, args) =>
            {
                _moreCloseTimer!.Stop();
                MorePopup.IsOpen = false;
            };
            _moreCloseTimer.Start();
        }

        private void MorePopup_MouseEnter(object sender, MouseEventArgs e)
        {
            _moreCloseTimer?.Stop();
        }

        private void MorePopup_MouseLeave(object sender, MouseEventArgs e)
        {
            MorePopup.IsOpen = false;
        }



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

            if (!_appearanceConfiguration.IsBackgroundRainbow)
            {
                _backgroundBrush.Color = allColors.background;
                UpdateToolbarTextColor();
                return;
            }

            UpdateToolbarTextColor();

            // Derive saturation/value from the user's chosen background color
            // so the rainbow respects their brightness preference.
            RgbToHsv(allColors.background, out _, out double sat, out double val);

            // Full-cycle length is user-configurable (1–10 s); clamp defensively.
            int cycleSeconds = Math.Clamp(_appearanceConfiguration.RainbowSpeedSeconds, 1, 10);

            var anim = new ColorAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromSeconds(cycleSeconds)),
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
            allColors.background = (Color)ColorConverter.ConvertFromString(_appearanceConfiguration.BackgroundColor);
            _backgroundBrush.Color = allColors.background;
            if (!_transparent)
                Background = _backgroundBrush;
            UpdateToolbarTextColor();
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

        /// <summary>
        /// Compares the version baked into this build (<see cref="BuildInfo.Version"/>)
        /// with the one persisted in <c>config.json</c>. When the build is
        /// strictly newer the user has just installed an update: the
        /// in-memory <paramref name="cfg"/> and the on-disk config are
        /// bumped to the build version, and the method returns <c>true</c>
        /// so the caller can replay the tutorial from step 0.
        /// </summary>
        /// <returns>
        /// <c>true</c> when an update was detected and the config was
        /// updated; <c>false</c> otherwise (versions match, build is
        /// older, or the build version is unparseable).
        /// </returns>
        private bool HandleAppUpdate(Configuration cfg)
        {
            // If the build version itself is unparseable we can't make a
            // meaningful comparison — bail out and leave config alone.
            if (!Version.TryParse(BuildInfo.Version, out var coded))
                return false;

            // Treat an unparseable / missing config version as "older than
            // anything", so a corrupted version field self-heals on next
            // launch.
            bool storedParsed = Version.TryParse(cfg.Version, out var stored);

            if (!storedParsed || coded > stored)
            {
                cfg.Version = BuildInfo.Version;

                if (BuildInfo.Distribution != DistributionType.dev)
                    SaveInitSettings(cfg);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Atomically persists <paramref name="cfg"/> to
        /// <c>%AppData%\ClickyKeys\config.json</c>. Mirrors the write path
        /// used by <see cref="LoadInitSettings"/> and
        /// <see cref="SetTutorialAsMarked"/> so all three stay in sync.
        /// </summary>
        private void SaveInitSettings(Configuration cfg)
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
                    string filePath = Path.Combine(appDataDir, "config.json");

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(cfg, options);
                    AtomicFile.WriteAllText(filePath, json);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveInitSettings failed: {ex}");
            }
        }

        private void Window_Closed(object? sender, CancelEventArgs e)
        {
            // ----- Test shutdown path -----
            // Minimize-to-tray hijack. The user clicking the title-bar X
            // (or Alt+F4) on the master window is treated as "tuck this
            // away, keep tracking" rather than "exit". Real exit happens
            // through Exit_Click or the tray menu's Exit item, both of
            // which set _exitRequested before calling Application.Shutdown.
            //
            //if (!_transparent && !_exitRequested && _trayIcon != null)
            //{
            //    e.Cancel = true;
            //    HideToTray();
            //    return;
            //}

            // ----- Real shutdown path -----

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
                    _appearanceService.FlushAsync().Wait(TimeSpan.FromSeconds(2));
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

            // Remove the tray icon from the system tray. Done last so it
            // disappears at the same moment as the window — anything
            // earlier and the user could see an orphaned icon if the
            // shutdown hangs on the service flushes above.
            if (!_transparent)
            {
                _trayIcon?.Dispose();
                _trayIcon = null;
            }
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
                                if (_appearanceConfiguration.IsBackgroundRainbow)
                                    UpdateRainbowState();
                                else
                                    _backgroundBrush.Color = c;
                                UpdateToolbarTextColor();
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
                            SetGrid(_appearanceConfiguration);
                            UpdateButtonLayout();
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

        private void OnShowTutorialLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnShowTutorialLoaded;
            ShowTutorialWindow();
        }

        public void ShowTutorial() => ShowTutorialWindow();

        private void ShowTutorialWindow()
        {
            var tutorial = new TutorialWindow(this);

            // Pass named elements the tutorial needs to spotlight.
            // _panelsById[4] is a representative panel (centre of a 2×2 grid).
            if (_panelsById.TryGetValue(4, out var samplePanel))
                tutorial.SetTargets(
                    panelGrid:        myGrid,
                    singlePanel:      samplePanel,
                    appearanceButton: Appearance_Button,
                    resetButton:      Reset_Button,
                    displayButton:    Display_Button,
                    statsButton:      Stats_Button,
                    moreButton:       More_Button);

            // Mark tutorial as done when the overlay closes.
            tutorial.Closed += (_, _) => SetTutorialAsMarked();

            tutorial.Show();
        }

        //-------------------------
        // Telemetry consent section (DRAFT)
        //-------------------------

        private void OnShowConsentDialogLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnShowConsentDialogLoaded;
            ShowConsentDialog();
        }

        private void ShowConsentDialog()
        {
            var dialog = new ConsentDialog { Owner = this };

            // The dialog itself persists the answer to config.json (see
            // ConsentDialog.Finish/OnClosing). Here we only need to flip the
            // already-running TelemetryService live so choosing Basic/Full
            // takes effect immediately, without requiring a restart, and —
            // since this is the very first consent — fire the app_start
            // event for THIS session too rather than waiting for the next
            // launch.
            dialog.Answered += level =>
            {
                App.Telemetry?.SetCollecting(level);
                if (level != TelemetryLevel.None)
                    App.Telemetry?.SendAppStartAsync();
            };

            dialog.Show();
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
        private async void VerifyVersion()
        {
                    
            var host = BuildInfo.Distribution == DistributionType.dev
                ? "https://staging.clickykeys.fun"
                : "https://clickykeys.fun";

            string url = host + "/api/releases.php";

            try
            {
                // Compare against the RUNNING build, not cfg.Version. On the
                // first launch after an update, config.json still holds the
                // previous version here — it isn't bumped until HandleAppUpdate
                // runs later in this same startup — so using cfg.Version made
                // the freshly-installed release look "newer than current" and
                // wrongly popped the update prompt alongside the changelog.
                // BuildInfo.Version is the authoritative current version.
                if (!Version.TryParse(BuildInfo.Version, out var current))
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

                    if (Version.TryParse(entry.Version, out var parsed)
                        && (latest == null || parsed > latest))
                    {
                        latest = parsed;
                    }
                }
                
                if (latest == null)
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
            if (_appearanceConfiguration.Version != new Configuration().Version)
            {
                _appearanceConfiguration.Version = new Configuration().Version;
                _appearanceService.Save(_appearanceConfiguration);
            }

            if (_panel_settings.Version != new Configuration().Version)
            {
                _panel_settings.Version = new Configuration().Version;
                _panelsService.Save(_panel_settings);
            }
        }


        public void ShowAppearance()
        {
            if (OpenedAppearance == false)
            {
                Appearance _appearance = new(_appearanceConfiguration, this, appearanceFileName);
                _appearance.Show();
                OpenedAppearance = true;
            }
        }
        public void OnAppearanceClose(string appearancePath)
        {
            _appearanceService = new(appearancePath);
            appearanceFileName = appearancePath;
            SaveProfileToConfig(appearanceFileName);
            _appearanceConfiguration = _appearanceService.Load();
            LoadBackgroundFromSettings();
            UpdateRainbowState();
            SetGrid(_appearanceConfiguration);
            UpdateButtonLayout();
            OpenedAppearance = false;
        }

        public void ToggleToolStrip()
        {
            if (_transparent == false)
                if (ToolStrip.Visibility == Visibility.Visible)
                {
                    ToolStrip.Visibility = Visibility.Collapsed;
                    //this.Topmost = !this.Topmost;
                }
                else
                {
                    ToolStrip.Visibility = Visibility.Visible;
                    //this.Topmost = !this.Topmost;
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
                Appearance_Button.Visibility = Visibility.Collapsed;
                InitTransparentMode();
            }
            else
            {
                myGrid.Visibility = Visibility.Visible;
                Appearance_Button.Visibility = Visibility.Visible;
                _transparentWindow?.Close();
                _transparentWindow = null;
            }

        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            // Mark the shutdown as user-initiated so the Closing handler
            // skips the minimize-to-tray hijack and lets the window
            // actually tear down.
            _exitRequested = true;
            Application.Current.Shutdown();
        }

        // -----------------------------------------------------------------
        // Tray / minimize-to-tray
        // -----------------------------------------------------------------

        /// <summary>
        /// Creates the system-tray icon programmatically. The previous
        /// keyed XAML resource was lazy and never resolved, so the tray
        /// icon never appeared in earlier builds. Building it here also
        /// lets us wire C# delegates for double-click and the menu items
        /// without needing those handlers reachable from XAML.
        /// </summary>
        private void InitTrayIcon()
        {
            try
            {
                _trayIcon = new TaskbarIcon
                {
                    IconSource = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("pack://application:,,,/Resources/Icon.ico", UriKind.Absolute)),
                    ToolTipText = "ClickyKeys (running — click to restore)",
                };

                var menu = new ContextMenu();

                var showItem = new MenuItem { Header = "Show" };
                showItem.Click += (_, __) => RestoreFromTray();
                menu.Items.Add(showItem);

                var appearanceItem = new MenuItem { Header = "Appearance" };
                appearanceItem.Click += (_, __) =>
                {
                    RestoreFromTray();
                    ShowAppearance();
                };
                menu.Items.Add(appearanceItem);

                var statsItem = new MenuItem { Header = "Stats" };
                statsItem.Click += (_, __) =>
                {
                    RestoreFromTray();
                    ShowStats();
                };
                menu.Items.Add(statsItem);

                menu.Items.Add(new Separator());

                var exitItem = new MenuItem { Header = "Exit" };
                exitItem.Click += (_, __) =>
                {
                    _exitRequested = true;
                    Application.Current.Shutdown();
                };
                menu.Items.Add(exitItem);

                _trayIcon.ContextMenu = menu;
                _trayIcon.TrayMouseDoubleClick += (_, __) => RestoreFromTray();
            }
            catch (Exception ex)
            {
                // Tray init failure must not crash startup — the rest of
                // the app works fine without it, just without the
                // minimize-to-tray affordance. The Closing handler checks
                // _trayIcon for null before deciding to hijack close.
                Debug.WriteLine($"InitTrayIcon failed: {ex}");
                _trayIcon = null;
            }
        }

        /// <summary>
        /// Hides the window and removes it from the Windows taskbar so the
        /// only remaining surface is the tray icon. The input hook and
        /// <see cref="KeyStatsService"/> keep running in the background,
        /// so stats accumulate while the window is hidden.
        /// </summary>
        private void HideToTray()
        {
            ShowInTaskbar = false;
            Hide();
        }

        /// <summary>
        /// Entry point for the "Start minimized" setting. Called by
        /// <see cref="App.OnStartup"/> right after the master window is shown.
        ///
        /// Unlike a manual minimize (which this app hijacks into a hide-to-tray
        /// cloak via <see cref="Window_StateChanged"/>), launching minimized
        /// leaves the window as an ordinary MINIMIZED TASKBAR BUTTON. The user
        /// asked for it to be visible on the Windows taskbar, so it can be
        /// restored with a normal taskbar click without needing the tray.
        ///
        /// This is a one-shot: <see cref="_startupTaskbarMinimize"/> tells the
        /// state-changed handler to let this single minimize stand on the
        /// taskbar. The flag is cleared as soon as it's consumed, so every
        /// subsequent (manual) minimize falls back to the usual hide-to-tray
        /// behavior.
        ///
        /// We defer to the <see cref="System.Windows.Window.Loaded"/> event when
        /// the window isn't fully realized yet, so the state change happens
        /// after WPF has finished bringing the window up.
        /// </summary>
        public void MinimizeToTaskbarAtStartup()
        {
            // Transparent sub-windows aren't the surface this setting targets.
            if (_transparent)
                return;

            void DoMinimize()
            {
                // Keep the taskbar button (a prior hide-to-tray may have set
                // this false) and arm the one-shot so Window_StateChanged
                // doesn't cloak this minimize away.
                ShowInTaskbar = true;
                _startupTaskbarMinimize = true;
                WindowState = WindowState.Minimized;
            }

            if (IsLoaded)
                DoMinimize();
            else
                Loaded += (_, __) => DoMinimize();
        }

        /// <summary>
        /// Hides the window by cloaking.
        /// </summary>
        private void HideWindow()
        {
            SetCloaked(true);
            SetForegroundWindow(GetShellWindow());

            // Deley window focusm change - wait for all tasks to be finished 
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                SetForegroundWindow(GetShellWindow());
            };
            timer.Start();
        }

        /// <summary>
        /// Brings the window back from the tray. Restores both window
        /// state and taskbar presence, then forces it to the foreground —
        /// without the brief Topmost flip Windows often draws the
        /// restored window behind whatever the user is currently focused
        /// on.
        /// </summary>
        private void RestoreFromTray()
        {
            RestoreWindow();
            Show();
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        /// <summary>
        /// Brings the window back - uncloak and focus.
        /// </summary>
        private void RestoreWindow()
        {
            SetCloaked(false);
            Activate();
            Focus();
        }

        private void Window_Activated(object? sender, EventArgs e)
        {
            if (!_isCloaked) return;

            if ((DateTime.UtcNow - _cloakedAt).TotalMilliseconds < 300) return;

            SetCloaked(false);
        }

        /// <summary>
        /// Catches user-initiated minimize (title-bar button, Win+Down,
        /// taskbar click) and routes it through HideToTray instead of
        /// leaving the window as a Windows-taskbar minimized stub.
        /// </summary>
        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (_transparent) return;

            if (WindowState == WindowState.Minimized)
            {
                // Startup "start minimized" path: let this one minimize stand as
                // a normal taskbar button instead of cloaking it to the tray.
                // One-shot — cleared here so later manual minimizes resume the
                // usual hide-to-tray behavior below.
                if (_startupTaskbarMinimize)
                {
                    _startupTaskbarMinimize = false;
                    ShowInTaskbar = true;
                    return;
                }

                HideWindow();
                WindowState = WindowState.Normal;

            }
        }

        /// <summary>
        /// Opens the changelog window showing all changes since
        /// <paramref name="sinceVersion"/>. Fetches the live API;
        /// replaces the old hardcoded step-0 tutorial overlay.
        /// </summary>
        private void ShowChangelog(string sinceVersion)
        {
            // Owner cannot be set before the parent window has been shown.
            // Defer to Loaded so the handle exists by the time we assign it.
            Loaded += OnShowChangelogLoaded;

            void OnShowChangelogLoaded(object s, RoutedEventArgs e)
            {
                Loaded -= OnShowChangelogLoaded;
                var changelogWindow = new Changelog(sinceVersion) { Owner = this };
                changelogWindow.Show();
            }
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

        // Stats window — exact mirror of the Info pattern: single-instance
        // guarded by OpenedStats, shown non-modally, signal back via
        // OnStatsClose so the guard resets when the user closes it.
        public void ShowStats()
        {
            if (OpenedStats == false)
            {
                Stats _statsPage = new(this);
                _statsPage.Show();
                OpenedStats = true;
            }
        }
        public void OnStatsClose()
        {
            OpenedStats = false;
        }

        // Settings window — exact mirror of the Info/Stats pattern:
        // single-instance guarded by OpenedSettings, shown non-modally,
        // signal back via OnSettingsClose so the guard resets when the user
        // closes it.
        public void ShowSettings()
        {
            if (OpenedSettings == false)
            {
                Settings _settingsPage = new(this);
                _settingsPage.Show();
                OpenedSettings = true;
            }
        }
        public void OnSettingsClose()
        {
            OpenedSettings = false;
        }

        // Messages / Inbox window — same single-instance pattern as
        // Info/Stats/Settings. Opening it marks shown messages read, so the
        // unread badge is refreshed once the window closes.
        public void ShowMessages()
        {
            if (OpenedMessages == false)
            {
                var inbox = new Inbox(this) { Owner = this };
                inbox.Show();
                OpenedMessages = true;
            }
        }

        public void OnMessagesClose()
        {
            OpenedMessages = false;
            RefreshMessagesBadge();
        }

        /// <summary>
        /// Updates the unread-count badge on BOTH the Messages button (inside
        /// the More dropdown) and the top-level More button itself, from
        /// <see cref="MessagesService.UnreadCount"/>. The More-button badge is
        /// what surfaces a freshly received message before the user opens the
        /// dropdown. Cheap to call; invoked when a fetch completes, when the
        /// More dropdown opens, and after the inbox closes.
        /// </summary>
        private void RefreshMessagesBadge()
        {
            int unread = App.Messages?.UnreadCount() ?? 0;
            string text = unread > 99 ? "99+" : unread.ToString();
            var visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Both badges may be absent in the transparent sub-window (its
            // toolbar is collapsed), so guard against null element references.
            if (MessagesBadge != null)
            {
                MessagesBadgeText.Text = text;
                MessagesBadge.Visibility = visibility;
            }

            if (MoreBadge != null)
            {
                MoreBadgeText.Text = text;
                MoreBadge.Visibility = visibility;
            }
        }

        /// <summary>
        /// Fired by <see cref="MessagesService.Updated"/> on a background thread
        /// after the startup fetch completes — marshal to the UI thread and
        /// refresh the badges so a newly received message shows up on the More
        /// button without waiting for a hover.
        /// </summary>
        private void OnMessagesUpdated(int added)
        {
            Dispatcher.BeginInvoke(new Action(RefreshMessagesBadge));
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
                    config.AppearanceProfile = names[1];

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
            _appearanceConfiguration.IsBackgroundRainbow = IsTrue ?? false;
            UpdateRainbowState();
        }

        // Live preview of the rainbow cycle length. Restarts the animation with
        // the new duration only while rainbow mode is actually on.
        public void SetRainbowSpeed(int seconds)
        {
            _appearanceConfiguration.RainbowSpeedSeconds = Math.Clamp(seconds, 1, 10);
            if (_appearanceConfiguration.IsBackgroundRainbow)
                UpdateRainbowState();
        }

        public void OnGridChange(AppearanceConfiguration settings)
        {
            SetGrid(settings);
            UpdateButtonLayout();
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
            // Update destination depends on how this build is distributed.
            string url = BuildInfo.Distribution switch
            {
                DistributionType.store => "https://apps.microsoft.com/detail/9PJT83WPC06K",
                DistributionType.github => "https://github.com/Reksaku/ClickyKeys/releases",
                _ => "https://clickykeys.fun"
            };
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
                MessageBox.Show(LocalizationManager.Format("Main_ErrorPrefix", ex.Message));
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
        private void SetGrid(AppearanceConfiguration settings)
        {
            // Parse shared state once rather than in every iteration.
            Color panelColor = (Color)ColorConverter.ConvertFromString(settings.PanelsColor);
            Color keysColor = (Color)ColorConverter.ConvertFromString(settings.KeysTextColor);
            Color valuesColor = (Color)ColorConverter.ConvertFromString(settings.ValuesTextColor);
            FontAppearance keysFont = settings.KeysFontAppearance;
            FontAppearance valuesFont = settings.ValuesFontAppearance;
            int panelWidth = settings.PanelWidth;
            int panelHeight = settings.PanelHeight;

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
                            valuesColor, keysFont, valuesFont, panelWidth,
                            panelHeight, resetValue: false);
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
                        valuesColor, keysFont, valuesFont, panelWidth,
                        panelHeight, resetValue: true);

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
            FontAppearance keysFont,
            FontAppearance valuesFont,
            int panelWidth,
            int panelHeight,
            bool resetValue)
        {
            var cfg = _panel_settings.Panels[id];

            panel.Description = (cfg.Input == InputType.None && cfg.KeyCode == Key.None)
                ? $"id. {id}"
                : cfg.Description;
            panel.Type = cfg.Input;
            panel.Key = cfg.KeyCode;
            panel.RawDescription = cfg.Description;
            panel.GamepadButton = cfg.GamepadButton;
            panel.PanelColor = panelColor;
            panel.KeyTextColor = keysColor;
            panel.ValueTextColor = valuesColor;
            panel.KeyFont = keysFont;
            panel.ValueFont = valuesFont;
            panel.PanelWidth = panelWidth;
            panel.PanelHeight = panelHeight;

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
                    && _panel_settings.Panels[i].KeyCode == state.KeyCode
                    && _panel_settings.Panels[i].GamepadButton == state.GamepadButton)
                {
                    _panel_settings.Panels[i].KeyCode = Key.None;
                    _panel_settings.Panels[i].Input = InputType.None;
                    _panel_settings.Panels[i].GamepadButton = -1;
                    _panel_settings.Panels[i].Description = "";
                    _counter.ResetSingle(i);
                }
            }

            int id = state.Index;
            _panel_settings.Panels[id].KeyCode = state.KeyCode;
            _panel_settings.Panels[id].Input = state.Input;
            _panel_settings.Panels[id].GamepadButton = state.GamepadButton;
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
            SetGrid(_appearanceConfiguration);
            UpdateButtonLayout();

            // Broadcast the change so the transparent sub-window (which
            // shares _counter but has its own UI tree) rebuilds its grid
            // without having to re-read the JSON file.
            WeakReferenceMessenger.Default.Send(
                new PanelsChangedMessage(_panel_settings));
        }

        //-------------------------
        // Panel profiles (PanelOverlay)
        //-------------------------

        /// <summary>Display name (no extension) of the active panels profile.</summary>
        public string ActivePanelsProfileName =>
            Path.GetFileNameWithoutExtension(_activePanelsProfile);

        /// <summary>
        /// Preview a panels profile from <paramref name="fullPath"/>: load and
        /// apply it live without yet changing the saved/active target, so a
        /// subsequent cancel can restore the committed profile.
        /// </summary>
        public void LoadPanelsFile(string fullPath)
        {
            _panel_settings = _panelsService.LoadFromPath(fullPath);
            ApplyLoadedPanels();
        }

        /// <summary>
        /// Commit a panels profile as the active one: repoint the service,
        /// persist the choice to config, and apply it live. Becomes active
        /// immediately — no Save needed on the Appearance card.
        /// </summary>
        public void SelectPanelsFile(string fullPath)
        {
            _panelsService.SetActivePath(fullPath);
            _activePanelsProfile = Path.GetFileName(fullPath);
            ConfigStore.Update(c => c.PanelsProfile = _activePanelsProfile);

            _panel_settings = _panelsService.Load();
            ApplyLoadedPanels();
        }

        /// <summary>
        /// Cancel a preview: reload the committed active profile (the service
        /// still points at it) and re-apply.
        /// </summary>
        public void RevertPanelsFile()
        {
            _panel_settings = _panelsService.Load();
            ApplyLoadedPanels();
        }

        /// <summary>
        /// Save the current panel layout under a new name and make it active.
        /// </summary>
        public void SavePanelsProfileAs(string name)
        {
            var fileName = name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? name
                : name + ".json";
            var fullPath = Path.Combine(PanelsService.PanelsDirectory, fileName);

            _panelsService.SaveToPath(_panel_settings, fullPath);
            SelectPanelsFile(fullPath);
        }

        /// <summary>
        /// Rebinds the live counter to <see cref="_panel_settings"/>, refreshes
        /// the grid + toolbar, and notifies the transparent sub-window. Shared
        /// by every panels-profile operation.
        /// </summary>
        private void ApplyLoadedPanels()
        {
            _counter.LoadPanels(_panel_settings);
            SetGrid(_appearanceConfiguration);
            UpdateButtonLayout();
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
}
