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

            // Restore persisted/current state into the controls. The
            // _initialising guard keeps the Checked/Unchecked handlers from
            // treating this programmatic restore as a user action (which would
            // pointlessly re-write the registry / config).
            _initialising = true;
            try
            {
                // Launch-on-startup: the registry is the source of truth, so
                // read it live rather than trusting a cached flag.
                AutostartToggle.IsChecked = AutostartService.IsEnabled();

                // Start-minimized and key-stats collection live in config.json.
                var cfg = ConfigStore.Load();
                StartMinimizedToggle.IsChecked = cfg.StartMinimized;
                CollectKeyStatsToggle.IsChecked = cfg.CollectKeyStats;
            }
            finally
            {
                _initialising = false;
            }

            // TODO (wiring): the uptime toggle and the language selector are
            // still UI-only — restore their saved values here once those
            // mechanisms exist.
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

            bool wanted = CollectKeyStatsToggle.IsChecked == true;

            // Persist the preference so it survives restarts...
            ConfigStore.Update(cfg => cfg.CollectKeyStats = wanted);

            // ...and apply it to the live collector immediately so the change
            // takes effect without needing a restart. The service may be null
            // if accessed before startup finished or after shutdown — the
            // persisted value above covers that case on next launch.
            App.KeyStats?.SetCollecting(wanted);
        }

        // ----------------------------------------------------------------
        // Usage
        // ----------------------------------------------------------------

        private void AutostartToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;

            // OS integration via the per-user "Run" registry key — see
            // AutostartService for the full rationale (HKCU vs HKLM, why the
            // registry is the source of truth, exe-path handling).
            bool wanted = AutostartToggle.IsChecked == true;
            bool ok = AutostartService.Set(wanted);

            // If the registry write failed (e.g. locked-down machine), the OS
            // state didn't change — snap the toggle back so the UI never claims
            // a state that isn't real. Re-guard so this correction doesn't
            // recurse back into this handler.
            if (!ok)
            {
                _initialising = true;
                try { AutostartToggle.IsChecked = AutostartService.IsEnabled(); }
                finally { _initialising = false; }

                MessageBox.Show(
                    this,
                    "Couldn't update the Windows startup setting. " +
                    "Your security software or account policy may be blocking it.",
                    "ClickyKeys",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void StartMinimizedToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;

            // Persist the flag; App.OnStartup reads it on the next launch and
            // routes the window through MainWindow.MinimizeToTaskbarAtStartup.
            // Update() does a load-mutate-save so we don't clobber other config
            // fields a different writer may have changed.
            bool wanted = StartMinimizedToggle.IsChecked == true;
            ConfigStore.Update(cfg => cfg.StartMinimized = wanted);
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
