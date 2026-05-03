using System.Configuration;
using System.Data;
using System;
using System.Windows;


namespace ClickyKeys
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Privacy-safe global stats collector. Lives for the whole process —
        // started after the input hook is armed, disposed before the hook is
        // torn down so the final flush still has live event state to capture.
        private KeyStatsService? _keyStats;

        /// <summary>
        /// Process-wide accessor for the live <see cref="KeyStatsService"/>
        /// instance. Used by windows that need to flush in-memory counters
        /// to disk before reading the snapshot file (e.g. the Stats view).
        /// Null before <see cref="OnStartup"/> and after <see cref="OnExit"/>.
        /// </summary>
        public static KeyStatsService? KeyStats => (Current as App)?._keyStats;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            GlobalInputHook.Instance.Start();

            // Subscribes as a second consumer of the same hook events that
            // InputCounter uses. Persists aggregate counters to
            // %APPDATA%\ClickyKeys\keystats.json every 30 s.
            _keyStats = new KeyStatsService();
            _keyStats.Start();

            var main = new MainWindow();
            MainWindow = main;
            main.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Flush + unsubscribe before unhooking the global hooks so the
            // final write captures everything up to the moment of exit.
            _keyStats?.Dispose();
            _keyStats = null;

            GlobalInputHook.Instance.Stop();

            base.OnExit(e);
        }
    }


}
