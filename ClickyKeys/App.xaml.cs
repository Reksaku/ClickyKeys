using System.Configuration;
using System.Data;
using System;
using System.Threading.Tasks;
using System.Windows;
using ClickyKeys.Windows;


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


        // Mutex that guarantees only one instance of ClickyKeys runs at a time.
        private static Mutex? _singleInstanceMutex;

        // Unique name for the mutex.
        private const string MutexName = "Global\\ClickyKeys_SingleInstance";

        /// <summary>
        /// Process-wide accessor for the live <see cref="KeyStatsService"/>
        /// instance. Used by windows that need to flush in-memory counters
        /// to disk before reading the snapshot file (e.g. the Stats view).
        /// Null before <see cref="OnStartup"/> and after <see cref="OnExit"/>.
        /// </summary>
        public static KeyStatsService? KeyStats => (Current as App)?._keyStats;

        protected override void OnStartup(StartupEventArgs e)
        {
            /// <summary>
            /// --- Single-instance guard ---
            /// Try to create the named mutex. If another instance already owns it,
            /// createdNew is false and we abort immediately without showing any UI.
            /// </summary>
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name: MutexName,
                createdNew: out bool createdNew);

            if (!createdNew)
            {
                // A running instance already owns the mutex.
                // Show a quiet, self-dismissing toast instead of a modal dialog.
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;

                base.OnStartup(e);
                var toast = new AlreadyRunningToast();
                toast.Closed += (_, _) => Shutdown();
                toast.Show();
                return;
            }

            base.OnStartup(e);

            // Show a splash screen immediately so the user knows the app is starting.
            var splash = new SplashWindow();
            splash.Show();

            // Low-level hooks (WH_KEYBOARD_LL / WH_MOUSE_LL) MUST be installed
            // on a thread that runs a Windows message loop — the UI thread.
            // Installing them on a Task.Run thread pool thread causes system-wide
            // input lag because Windows times out waiting for the hookless thread
            // to drain its message queue on every single keystroke/mouse event.
            GlobalInputHook.Instance.Start();

            // KeyStatsService only does JSON I/O — safe to start on a thread pool
            // thread. Fire-and-forget; the service is self-contained.
            splash.SetStatus("Starting key stats service…");
            _keyStats = new KeyStatsService();
            _keyStats.Start();

            var main = new MainWindow();
            MainWindow = main;
            main.Show();

            // "Start minimized" setting. MainWindow's constructor has already
            // created (or migrated) config.json by this point, so the value we
            // read here is current. We minimize only after Show() so the window
            // is fully realized first; it lands as an ordinary minimized button
            // on the Windows taskbar (see MainWindow.MinimizeToTaskbarAtStartup).
            if (ConfigStore.Load().StartMinimized)
                main.MinimizeToTaskbarAtStartup();

            splash.Close();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Flush + unsubscribe before unhooking the global hooks so the
            // final write captures everything up to the moment of exit.
            _keyStats?.Dispose();
            _keyStats = null;

            GlobalInputHook.Instance.Stop();

            // Release and dispose the mutex so the OS reclaims the handle cleanly.
            if (_singleInstanceMutex is not null)
            {
                _singleInstanceMutex.ReleaseMutex();
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }
    }


}
