using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        // Which shortcut button (if any) is currently waiting to capture the
        // next key press. None means the buttons behave as ordinary buttons.
        private enum ShortcutTarget { None, Reset, Toggle }
        private ShortcutTarget _capturing = ShortcutTarget.None;

        public Settings(IOverlay mainOverlay)
        {
            _mainOverlay = mainOverlay;

            // Guard BEFORE InitializeComponent: the ComboBox's initial
            // selection raises SelectionChanged during XAML init, and without
            // this flag that spurious event would apply/persist the default
            // language and clobber a saved choice before we get to restore it.
            _initialising = true;
            InitializeComponent();

            // Restore persisted/current state into the controls. The
            // _initialising guard keeps the Checked/Unchecked handlers from
            // treating this programmatic restore as a user action (which would
            // pointlessly re-write the registry / config).
            try
            {
                // Launch-on-startup: the registry is the source of truth, so
                // read it live rather than trusting a cached flag.
                AutostartToggle.IsChecked = AutostartService.IsEnabled();

                // Start-minimized and the two collection switches live in
                // config.json.
                var cfg = ConfigStore.Load();
                StartMinimizedToggle.IsChecked = cfg.StartMinimized;
                CollectKeyStatsToggle.IsChecked = cfg.CollectKeyStats;
                CollectUptimeToggle.IsChecked = cfg.CollectUptime;

                // Language: select the ComboBoxItem whose Tag matches the active
                // language. When the user has saved an explicit choice we match
                // that; when they haven't (blank = auto-detect), we reflect the
                // language that's actually applied right now
                // (LocalizationManager.CurrentLanguage, set at startup from the
                // detected system language) so the combo never disagrees with
                // what's on screen.
                var savedLang = string.IsNullOrWhiteSpace(cfg.Language)
                    ? LocalizationManager.CurrentLanguage
                    : LocalizationManager.Normalize(cfg.Language);
                foreach (var obj in LanguageComboBox.Items)
                {
                    if (obj is ComboBoxItem item &&
                        (item.Tag as string) == savedLang)
                    {
                        LanguageComboBox.SelectedItem = item;
                        break;
                    }
                }

                // Shortcut buttons show the currently assigned keys.
                RefreshShortcutButtons(cfg);
            }
            finally
            {
                _initialising = false;
            }
        }

        // ----------------------------------------------------------------
        // Data Collection
        // ----------------------------------------------------------------

        private void CollectUptimeToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialising) return;

            bool wanted = CollectUptimeToggle.IsChecked == true;

            // Persist for next launch, then apply to the live tracker so the
            // change takes effect immediately (no restart needed).
            ConfigStore.Update(cfg => cfg.CollectUptime = wanted);
            App.Uptime?.SetCollecting(wanted);
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
                    LocalizationManager.T("Settings_AutostartError"),
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

            // Read the chosen culture from the selected item's Tag ("en"/"pl").
            string code = LocalizationManager.Normalize(
                (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string);

            // Apply live: LocalizationManager swaps the merged string
            // dictionary, so every open window re-resolves its
            // {DynamicResource ...} text immediately — no restart needed.
            LocalizationManager.Apply(code);

            // Persist so the choice is restored on the next launch.
            ConfigStore.Update(cfg => cfg.Language = code);
        }

        // ----------------------------------------------------------------
        // Shortcuts (reset / toggle toolbar)
        // ----------------------------------------------------------------

        /// <summary>
        /// Updates both shortcut buttons' captions to the keys stored in
        /// <paramref name="cfg"/>, using <see cref="FriendlyKeyName"/> for a
        /// readable label.
        /// </summary>
        private void RefreshShortcutButtons(Configuration cfg)
        {
            ResetKeyButton.Content = FriendlyKeyName.ForKey(cfg.ResetKey.ToString());
            ToggleToolbarKeyButton.Content = FriendlyKeyName.ForKey(cfg.ToggleToolbarKey.ToString());
        }

        private void ResetKeyButton_Click(object sender, RoutedEventArgs e)
            => BeginCapture(ShortcutTarget.Reset);

        private void ToggleToolbarKeyButton_Click(object sender, RoutedEventArgs e)
            => BeginCapture(ShortcutTarget.Toggle);

        /// <summary>
        /// Puts a shortcut button into "press a key" mode. The next
        /// <see cref="ShortcutButton_PreviewKeyDown"/> assigns the key.
        /// </summary>
        private void BeginCapture(ShortcutTarget target)
        {
            _capturing = target;
            var btn = target == ShortcutTarget.Reset ? ResetKeyButton : ToggleToolbarKeyButton;
            btn.Content = LocalizationManager.T("Settings_PressKey");
            btn.Focus();
        }

        private void CancelCapture()
        {
            _capturing = ShortcutTarget.None;
            RefreshShortcutButtons(ConfigStore.Load());
        }

        private void ShortcutButton_LostFocus(object sender, RoutedEventArgs e)
        {
            // Clicking elsewhere while still capturing reverts the prompt.
            if (_capturing != ShortcutTarget.None)
                CancelCapture();
        }

        private void ShortcutButton_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Not capturing → let the button behave normally (Space/Enter click).
            if (_capturing == ShortcutTarget.None)
                return;

            // We own every key while capturing so Space/Enter don't "click" the
            // button and Tab doesn't move focus.
            e.Handled = true;

            // Alt-combinations arrive as Key.System with the real key in SystemKey.
            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // Esc cancels without changing anything.
            if (key == Key.Escape)
            {
                CancelCapture();
                return;
            }

            if (key == Key.None)
                return; // ignore, keep waiting

            var cfg = ConfigStore.Load();

            // Reject assigning the same key to both shortcuts.
            Key other = _capturing == ShortcutTarget.Reset ? cfg.ToggleToolbarKey : cfg.ResetKey;
            if (key == other)
            {
                MessageBox.Show(
                    this,
                    LocalizationManager.T("Settings_ShortcutConflict"),
                    "ClickyKeys",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                CancelCapture();
                return;
            }

            // Persist the new assignment...
            if (_capturing == ShortcutTarget.Reset)
                ConfigStore.Update(c => c.ResetKey = key);
            else
                ConfigStore.Update(c => c.ToggleToolbarKey = key);

            _capturing = ShortcutTarget.None;

            // ...refresh the buttons and apply to the live counter + toolbar
            // labels so the change takes effect immediately (no restart).
            var updated = ConfigStore.Load();
            RefreshShortcutButtons(updated);
            _mainOverlay.ApplyShortcuts(updated.ResetKey, updated.ToggleToolbarKey);
        }

        // ----------------------------------------------------------------
        // Window lifecycle
        // ----------------------------------------------------------------

        private void Click_Close(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object? sender, EventArgs e) =>
            _mainOverlay.OnSettingsClose();
    }
}
