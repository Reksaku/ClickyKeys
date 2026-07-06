using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace ClickyKeys.Windows
{
    /// <summary>
    /// DRAFT — first-run opt-in prompt for <see cref="TelemetryService"/>.
    ///
    /// Shown by MainWindow exactly once, the first time
    /// <c>Configuration.TelemetryLevel</c> is <c>null</c> (see MainWindow's
    /// constructor, mirroring the existing ShowTutorial gate). Presents three
    /// equally-weighted options — None / Basic / Full, see ConsentDialog.xaml
    /// for what each sends — and persists an explicit answer via
    /// <see cref="ConfigStore"/> the moment one is clicked, then raises
    /// <see cref="Answered"/> so the caller can seed/toggle the live
    /// <see cref="TelemetryService"/> instance without needing a restart.
    ///
    /// Deliberately has no "Remind me later": a silently-deferred consent
    /// prompt tends to just re-appear forever, which is worse UX than a clear
    /// choice now — and every option (including None) is always revisitable
    /// later from Settings → Data Collection, so answering costs the user
    /// nothing they can't undo.
    /// </summary>
    public partial class ConsentDialog : Window
    {
        /// <summary>
        /// Raised once, right after the answer is persisted to config.json,
        /// with the level the user chose.
        /// </summary>
        public event Action<TelemetryLevel>? Answered;

        private bool _answered;

        public ConsentDialog()
        {
            InitializeComponent();
        }

        private void Full_Click(object sender, RoutedEventArgs e) => Finish(TelemetryLevel.Full);

        private void Basic_Click(object sender, RoutedEventArgs e) => Finish(TelemetryLevel.Basic);

        private void None_Click(object sender, RoutedEventArgs e) => Finish(TelemetryLevel.None);

        private void Finish(TelemetryLevel level)
        {
            _answered = true;

            // Load-mutate-save so we never clobber unrelated config fields.
            // Note: we deliberately do NOT generate TelemetryUserId here —
            // TelemetryService.EnsureUserId does that lazily on the first
            // actual send, keeping "id issuance" and "consent capture" as
            // two separate, independently auditable steps.
            ConfigStore.Update(cfg => cfg.TelemetryLevel = level);

            try { Answered?.Invoke(level); }
            catch (Exception ex) { Debug.WriteLine($"ConsentDialog.Answered handler failed: {ex}"); }

            Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        /// <summary>
        /// Guards against the window being dismissed without clicking one of
        /// the three options (title-bar [X], Alt-F4, Esc). Treated as an
        /// implicit "not now" and recorded as None — the alternative
        /// (leaving TelemetryLevel as null) would just make the prompt
        /// resurface on every single launch, which is worse than a
        /// revisitable "none."
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_answered)
                ConfigStore.Update(cfg => cfg.TelemetryLevel = TelemetryLevel.None);

            base.OnClosing(e);
        }
    }
}
