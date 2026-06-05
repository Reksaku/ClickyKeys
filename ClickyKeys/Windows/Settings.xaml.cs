using System;
using System.Windows;
using System.Windows.Controls;

namespace ClickyKeys
{
    /// <summary>
    /// Program settings view. Mirrors the lifecycle/ownership pattern of
    /// <see cref="Info"/> and <see cref="Stats"/>: created by
    /// <see cref="MainWindow"/> with itself as the <see cref="IOverlay"/>,
    /// shown non-modally, and signals back via
    /// <see cref="IOverlay.OnSettingsClose"/> on close.
    ///
    /// STATUS: the controls below are intentionally UI-only. Each handler is
    /// a stub that the wiring layer will fill in later. They are split into
    /// two categories that match the XAML cards:
    ///
    ///   Data Collection
    ///     - CollectUptimeToggle     -> app uptime tracking on/off
    ///     - CollectKeyStatsToggle   -> key-press statistics on/off
    ///
    ///   Usage
    ///     - AutostartToggle         -> launch on Windows startup
    ///     - StartMinimizedToggle    -> start in minimized state
    ///     - LanguageComboBox        -> UI language
    ///
    /// IMPORTANT for whoever wires these up: the constructor sets the initial
    /// control states from persisted config BEFORE subscribing intent should
    /// be handled carefully. Right now there is no persistence, so the
    /// Checked/Unchecked handlers can fire during InitializeComponent if a
    /// control starts checked. Guard real side effects behind the
    /// <see cref="_initialising"/> flag (see below) so loading saved values
    /// doesn't immediately re-trigger the very action being restored.
    /// </summary>
    public partial class Settings : Window
    {
        private readonly IOverlay _mainOverlay;

        // Set while we push persisted values into the controls. Event handlers
        // should early-return when this is true so that *restoring* a saved
        // state doesn't get mistaken for the *user* changing it (which would,
        // e.g., rewrite the registry or reload resources needlessly).
        private bool _initialising;

        public Settings(IOverlay mainOverlay)
        {
            _mainOverlay = mainOverlay;
            InitializeComponent();

            // TODO (wiring): load saved values here, e.g.
            //   _initialising = true;
            //   CollectUptimeToggle.IsChecked   = config.CollectUptime;
            //   CollectKeyStatsToggle.IsChecked = config.CollectKeyStats;
            //   AutostartToggle.IsChecked       = AutostartService.IsEnabled();
            //   StartMinimizedToggle.IsChecked  = config.StartMinimized;
            //   SelectLanguage(config.LanguageCode);
            //   _initialising = false;
        }

        // ----------------------------------------------------------------
        // Data Collection
        // ----------------------------------------------------------------

        private void CollectUptimeToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;

            // TODO (wiring): enable/disable the uptime tracker and persist the
            // choice. Straightforward boolean — likely just a flag the uptime
            // service checks before accumulating time.
            // bool enabled = CollectUptimeToggle.IsChecked == true;
        }

        private void CollectKeyStatsToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;

            // TODO (wiring): toggle KeyStatsService collection and persist.
            // NOTE: decide what "off" means for existing data — pause counting
            // only, or also stop flushing keystats.json? The Stats window reads
            // that file directly, so a paused-but-retained model keeps old
            // numbers visible while no new ones accrue. Recommend pause-only.
            // bool enabled = CollectKeyStatsToggle.IsChecked == true;
        }

        // ----------------------------------------------------------------
        // Usage
        // ----------------------------------------------------------------

        private void AutostartToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;

            // TODO (wiring): COMPLEX — OS integration.
            // Autostart on Windows is typically registered by writing the
            // executable path under:
            //   HKCU\Software\Microsoft\Windows\CurrentVersion\Run
            //     value name: "ClickyKeys", value data: "\"<full exe path>\""
            // Use the *current* process path (Environment.ProcessPath) rather
            // than a cached/installed path so it survives the app being moved.
            // Writing HKCU needs no elevation. Remember to DELETE the value on
            // untoggle, and treat a missing key as "disabled" when reading the
            // initial state. If start-minimized is on, you may want to append a
            // "--minimized" arg here so autostart launches quietly.
            // bool enabled = AutostartToggle.IsChecked == true;
        }

        private void StartMinimizedToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;

            // TODO (wiring): persist the flag and have App startup honour it
            // (WindowState = Minimized, or hide to the tray NotifyIcon instead
            // of the taskbar). Coordinate with the autostart arg note above.
            // bool enabled = StartMinimizedToggle.IsChecked == true;
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initialising) return;

            // TODO (wiring): COMPLEX — localization.
            // Read the chosen culture from the selected item's Tag ("en"/"pl"),
            // persist it, then apply it. Applying mid-session means swapping the
            // merged localized ResourceDictionary and setting
            // Thread.CurrentThread.CurrentUICulture; already-rendered windows
            // won't re-localize automatically, so either rebuild open windows
            // or tell the user a restart is needed. Simplest first pass: save
            // the choice and apply on next launch.
            // string? code = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string;
        }

        // ----------------------------------------------------------------
        // Window lifecycle
        // ----------------------------------------------------------------

        private void Click_Close(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object? sender, EventArgs e) =>
            _mainOverlay.OnSettingsClose();
    }
}
