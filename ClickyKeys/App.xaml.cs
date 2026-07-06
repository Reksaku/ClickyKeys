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

        // Cumulative application-uptime tracker. Same lifecycle as _keyStats:
        // started after the main services come up, disposed (with a final
        // flush) on exit.
        private UptimeStatsService? _uptime;

        // DRAFT — opt-in product telemetry (see TelemetryService header).
        // Unlike _keyStats/_uptime this never writes local files and holds
        // no unmanaged resources, so it needs no Dispose/teardown step —
        // only a seed at startup and a live toggle from Settings.
        private TelemetryService? _telemetry;



        // Mutex that guarantees only one instance of ClickyKeys runs at a time.
        private static Mutex? _singleInstanceMutex;

        // Unique name for the mutex.
        private const string MutexName = "Global\\ClickyKeys_SingleInstance";

        /// <summary>
        /// How this process was launched, expressed as the exact token expected
        /// by the releases endpoint:
        ///   "auto_start" — Windows started the app at login (the autostart Run
        ///                  entry passes <see cref="AutostartService.AutostartArg"/>);
        ///   "user_start" — any other launch (user double-clicked, etc.).
        /// Resolved once in <see cref="OnStartup"/> and read by
        /// <c>MainWindow.VerifyVersion</c> when it queries releases.php.
        /// </summary>
        public static string LaunchTrigger { get; private set; } = "user_start";

        /// <summary>
        /// Process-wide accessor for the live <see cref="KeyStatsService"/>
        /// instance. Used by windows that need to flush in-memory counters
        /// to disk before reading the snapshot file (e.g. the Stats view).
        /// Null before <see cref="OnStartup"/> and after <see cref="OnExit"/>.
        /// </summary>
        public static KeyStatsService? KeyStats => (Current as App)?._keyStats;

        /// <summary>
        /// Process-wide accessor for the live <see cref="UptimeStatsService"/>.
        /// Used by the Stats view to read/flush current uptime and by Settings
        /// to toggle collection. Null before <see cref="OnStartup"/> and after
        /// <see cref="OnExit"/>.
        /// </summary>
        public static UptimeStatsService? Uptime => (Current as App)?._uptime;

        /// <summary>
        /// Process-wide accessor for the live <see cref="TelemetryService"/>.
        /// Used by ConsentDialog's Answered handler and Settings to flip the
        /// live switch without needing a restart. Null before
        /// <see cref="OnStartup"/> and after <see cref="OnExit"/>.
        /// </summary>
        public static TelemetryService? Telemetry => (Current as App)?._telemetry;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Determine how we were launched BEFORE any window is created, so
            // the first releases.php query (fired from MainWindow's ctor) can
            // report it. The autostart Run entry appends AutostartArg; its
            // presence on the command line means Windows launched us at login.
            if (e.Args != null && Array.IndexOf(e.Args, AutostartService.AutostartArg) >= 0)
                LaunchTrigger = "auto_start";

            // Upgrade any legacy autostart entry (exe path without the flag) so
            // future login launches carry AutostartArg and are reported as
            // auto_start. No effect on the CURRENT launch — its command line is
            // already set by Windows — but fixes every subsequent one.
            AutostartService.EnsureCurrent();

            // Apply the UI language before any window is created so every window
            // resolves its {DynamicResource ...} strings in the right language
            // from the very first render. If the user has saved an explicit
            // choice we honour it; otherwise (first launch) we auto-detect the
            // system display language and use the matching translation, falling
            // back to English when the OS language isn't one we ship.
            LocalizationManager.Apply(
                LocalizationManager.ResolveStartupLanguage(ConfigStore.Load().Language));

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
            splash.SetStatus(LocalizationManager.T("Splash_StartingKeyStats"));
            _keyStats = new KeyStatsService();
            // Seed the collection switch from config BEFORE Start() so the very
            // first input event is handled per the user's saved preference.
            _keyStats.ConfigureCollecting(ConfigStore.Load().CollectKeyStats);
            _keyStats.Start();

            // Application-uptime tracker — independent of the input hook, same
            // seed-then-start pattern as key stats.
            _uptime = new UptimeStatsService();
            _uptime.ConfigureCollecting(ConfigStore.Load().CollectUptime);
            _uptime.Start();

            // Gamepad input. The singleton polls connected controllers;
            // InputCounter consumes presses for panel counting and
            // KeyStatsService for statistics.
            GamepadInputService.Instance.SetTriggerThresholdPercent(
                ConfigStore.Load().GamepadTriggerThreshold);
            GamepadInputService.Instance.Start();

            // DRAFT — opt-in telemetry (None/Basic/Full). Seeded from
            // whatever is already on disk: null (never asked) resolves to
            // None here; MainWindow's first-run ConsentDialog is what turns
            // a fresh null into a real choice (see MainWindow ctor +
            // ConsentDialog). SendAppStartAsync() is itself a no-op at
            // None, so it's safe to call unconditionally every launch.
            _telemetry = new TelemetryService();
            _telemetry.ConfigureCollecting(ConfigStore.Load().TelemetryLevel ?? TelemetryLevel.None);
            _telemetry.SendAppStartAsync();

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

            // Fold + persist the final uptime segment. Independent of the input
            // hook, so ordering relative to it doesn't matter.
            _uptime?.Dispose();
            _uptime = null;

            // No teardown needed (no local file, no unmanaged handle) — just
            // drop the reference so App.Telemetry reads null after exit,
            // matching _keyStats/_uptime's post-OnExit contract.
            _telemetry = null;

            // Stop the gamepad polling thread.
            GamepadInputService.Instance.Stop();

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
