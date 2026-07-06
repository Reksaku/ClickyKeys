using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ClickyKeys
{
    /// <summary>
    /// DRAFT — opt-in, two-tier product-usage telemetry.
    ///
    /// Separate from <see cref="KeyStatsService"/> and
    /// <see cref="UptimeStatsService"/> on purpose: those two are LOCAL-ONLY
    /// (data never leaves the machine — see their headers and
    /// Info_PrivacyText1). This service is the one and only component that
    /// talks to a ClickyKeys server about *the user*, so it is kept small,
    /// isolated, and easy to audit.
    ///
    /// Consent has three states (<see cref="TelemetryLevel"/>), not a plain
    /// on/off:
    /// <list type="bullet">
    /// <item><c>null</c> (not <see cref="TelemetryLevel"/> — the config field
    /// itself is nullable) — not asked yet. This service stays silent;
    /// MainWindow shows ConsentDialog and writes the answer.</item>
    /// <item><see cref="TelemetryLevel.None"/> — opted out. Every send below
    /// is a no-op.</item>
    /// <item><see cref="TelemetryLevel.Basic"/> — sends ONLY the identity
    /// fields: anonymous id, app version, distribution, OS version, UI
    /// language. Exactly what shipped before this tier split.</item>
    /// <item><see cref="TelemetryLevel.Full"/> — sends everything Basic
    /// sends, PLUS a feature-usage snapshot built by
    /// <see cref="BuildFeatureSnapshot"/>. Every field in that snapshot is
    /// deliberately a count, a boolean, or a small enum-like value — never
    /// free text the user typed (panel descriptions/labels are excluded on
    /// purpose), never a raw color/font value tied back to a person, never a
    /// file path, never per-key identity. The goal is "are panels being
    /// configured, are display modes being used, are shortcuts being
    /// customized, are the numbers roughly sane" — not "what does this
    /// specific user do."</item>
    /// </list>
    ///
    /// What is deliberately NEVER sent at either tier: key/mouse input
    /// content, window titles, file paths, panel descriptions/labels, exact
    /// color values, or anything else the user typed or chose as a custom
    /// name. Aggregate counts (e.g. total key presses) are fine — they carry
    /// the same privacy shape as the already-local-only KeyStatsService
    /// totals; WHICH keys were pressed is not fine and is never sent.
    ///
    /// Failure handling: matches SponsorshipService — swallow every
    /// exception (no internet, DNS failure, 5xx, timeout) and log to Debug
    /// only. Telemetry must never surface an error to the user or affect app
    /// behaviour.
    /// </summary>
    public sealed class TelemetryService
    {
        // DRAFT endpoint — mirrors the existing releases.php / sponsorship.php
        // convention on the same host. See TELEMETRY_BACKEND.md for the
        // server-side plan (accepts an optional "feature" object for Full).
        private const string Endpoint = "https://clickykeys.fun/api/telemetry.php";

        private static readonly HttpClient _http = BuildHttpClient();

        // Seeded once from config at startup (App.OnStartup), then flipped
        // live by Settings whenever the user changes the level. volatile so
        // a change made on the UI thread is visible to the fire-and-forget
        // send task without an explicit lock.
        private volatile TelemetryLevel _level = TelemetryLevel.None;

        /// <summary>Current consent level this instance will honour.</summary>
        public TelemetryLevel Level => _level;

        /// <summary>
        /// Seeds the initial state from persisted config. Call once before
        /// any send is attempted (no side effects, no network call).
        /// </summary>
        public void ConfigureCollecting(TelemetryLevel level) => _level = level;

        /// <summary>
        /// Runtime change from Settings → Data Collection, or from the
        /// first-run ConsentDialog. Takes effect for the NEXT send attempt.
        /// </summary>
        public void SetCollecting(TelemetryLevel level) => _level = level;

        /// <summary>
        /// Fire-and-forget "app_start" event. Safe to call unconditionally
        /// from App.OnStartup — it checks the current level itself and
        /// returns immediately when it's None, so callers don't need to
        /// guard the call site.
        /// </summary>
        public void SendAppStartAsync()
        {
            if (_level == TelemetryLevel.None) return;

            // Deliberately not awaited by the caller: startup must never
            // wait on network I/O. Exceptions are caught inside the task so
            // an unobserved-task-exception can't crash the process.
            _ = Task.Run(async () =>
            {
                try
                {
                    var cfg = ConfigStore.Load();
                    var level = cfg.TelemetryLevel ?? TelemetryLevel.None;
                    if (level == TelemetryLevel.None) return; // re-check latest persisted value

                    var userId = EnsureUserId(cfg);

                    var payload = new TelemetryEvent
                    {
                        UserId = userId,
                        AppVersion = BuildInfo.Version,
                        Distribution = BuildInfo.Distribution.ToString(),
                        OsVersion = Environment.OSVersion.VersionString,
                        Language = LocalizationManager.CurrentLanguage,
                        EventType = "app_start",
                        TimestampUtc = DateTime.UtcNow,
                        Level = level.ToString().ToLowerInvariant(),
                    };

                    // Full tier additionally attaches the feature-usage
                    // snapshot. Built fresh from on-disk config/profile files
                    // every send — no dependency on MainWindow's live state,
                    // so this works the same whether or not a window exists
                    // yet.
                    if (level == TelemetryLevel.Full)
                        payload.Feature = BuildFeatureSnapshot(cfg);

                    var json = JsonSerializer.Serialize(payload,
                        new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
                    using var content = new StringContent(json, Encoding.UTF8, "application/json");
                    using var response = await _http.PostAsync(Endpoint, content).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        Debug.WriteLine($"TelemetryService: server returned {(int)response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"TelemetryService.SendAppStartAsync failed: {ex}");
                }
            });
        }

        /// <summary>
        /// Returns the persisted anonymous id, generating and persisting a
        /// fresh one on first use. Called only on the path where consent is
        /// already known to be Basic or Full, so an id is never generated
        /// for a user who hasn't opted into at least Basic.
        /// </summary>
        private static string EnsureUserId(Configuration cfg)
        {
            if (!string.IsNullOrWhiteSpace(cfg.TelemetryUserId))
                return cfg.TelemetryUserId;

            var id = Guid.NewGuid().ToString("N");
            ConfigStore.Update(c => c.TelemetryUserId = id);
            return id;
        }

        /// <summary>
        /// Builds the Full-tier feature-usage snapshot. Every field here was
        /// picked to answer "is this feature being used / configured / does
        /// it look correct" without ever carrying anything the user typed or
        /// anything that identifies them beyond the same anonymous id Basic
        /// already sends. Reads straight from the on-disk profile files
        /// (appearance + panels + config), independent of whether a window
        /// is open — see the services' own Load() methods, unchanged here.
        ///
        /// Deliberately EXCLUDED, and why:
        /// <list type="bullet">
        /// <item>Panel <c>Description</c> / assigned key identity — free
        /// text / potentially personal ("Girlfriend's stream key" is an
        /// extreme but real example of what a user could type there).</item>
        /// <item>Exact color hex values — not risky on their own, but add no
        /// signal over "customized: yes/no" for the stated goal, so there's
        /// no reason to widen the payload for them.</item>
        /// <item>Exact assigned shortcut keys — same reasoning; only whether
        /// they differ from the F11/F12 defaults is sent.</item>
        /// </list>
        /// </summary>
        private static TelemetryFeatureSnapshot BuildFeatureSnapshot(Configuration cfg)
        {
            var snapshot = new TelemetryFeatureSnapshot();

            // ---- Appearance (grid, colors, fonts, rainbow) ----------------
            try
            {
                var appearance = new AppearanceService(cfg.AppearanceProfile).Load();
                var defaults = new AppearanceConfiguration();

                snapshot.GridRows = appearance.GridRows;
                snapshot.GridColumns = appearance.GridColumns;

                snapshot.ColorsCustomized =
                    appearance.BackgroundColor != defaults.BackgroundColor ||
                    appearance.PanelsColor != defaults.PanelsColor ||
                    appearance.KeysTextColor != defaults.KeysTextColor ||
                    appearance.ValuesTextColor != defaults.ValuesTextColor;

                snapshot.FontsCustomized =
                    appearance.KeysFontAppearance.FontFamily.Source != defaults.KeysFontAppearance.FontFamily.Source ||
                    appearance.ValuesFontAppearance.FontFamily.Source != defaults.ValuesFontAppearance.FontFamily.Source ||
                    appearance.KeysFontAppearance.IsBold != defaults.KeysFontAppearance.IsBold ||
                    appearance.KeysFontAppearance.IsItalic != defaults.KeysFontAppearance.IsItalic ||
                    appearance.KeysFontAppearance.IsUnderline != defaults.KeysFontAppearance.IsUnderline;

                snapshot.RainbowBackgroundEnabled = appearance.IsBackgroundRainbow;
                snapshot.RainbowSpeedSeconds = appearance.RainbowSpeedSeconds;
            }
            catch (Exception ex) { Debug.WriteLine($"BuildFeatureSnapshot appearance failed: {ex}"); }

            // ---- Panels (layout shape only, never description/key identity) --
            try
            {
                // Point at the user's ACTIVE panels profile (cfg.PanelsProfile),
                // not the constructor's "default panels.json" — mirrors how
                // MainWindow resolves the active profile at startup.
                var panelsService = new PanelsService();
                var panelsPath = Path.Combine(PanelsService.PanelsDirectory, cfg.PanelsProfile);
                if (!File.Exists(panelsPath))
                    panelsPath = Path.Combine(PanelsService.PanelsDirectory, PanelsService.DefaultProfileFileName);
                panelsService.SetActivePath(panelsPath);

                var panels = panelsService.Load();
                var configured = panels.Panels.Where(p => p.Input != InputType.None).ToList();

                snapshot.PanelsConfiguredCount = configured.Count;
                snapshot.PanelsUsingKeyInput = configured.Count(p => p.Input == InputType.Key);
                snapshot.PanelsUsingMouseInput = configured.Count(p =>
                    p.Input is InputType.MouseLeft or InputType.MouseRight or InputType.MouseMiddle
                        or InputType.MouseXButton1 or InputType.MouseXButton2);
                snapshot.PanelsUsingWheelInput = configured.Count(p =>
                    p.Input is InputType.MouseWheelUp or InputType.MouseWheelDown
                        or InputType.MouseWheelLeft or InputType.MouseWheelRight);
                snapshot.PanelsUsingGamepadInput = configured.Count(p => p.Input == InputType.Gamepad);
            }
            catch (Exception ex) { Debug.WriteLine($"BuildFeatureSnapshot panels failed: {ex}"); }

            // ---- Usage settings --------------------------------------------
            snapshot.GamepadTriggerThreshold = cfg.GamepadTriggerThreshold;
            snapshot.AutostartEnabled = SafeIsAutostartEnabled();
            snapshot.StartMinimized = cfg.StartMinimized;
            snapshot.ShortcutsCustomized = cfg.ResetKey != Key.F12 || cfg.ToggleToolbarKey != Key.F11;
            snapshot.TutorialCompleted = cfg.ShowTutorial == false;

            // ---- Aggregate counters (same privacy shape as the already
            // local-only KeyStatsService/UptimeStatsService totals — see
            // their headers). Read directly off disk so this works
            // regardless of whether those services are running in-process. --
            var (keyPresses, mouseClicks, wheelTicks, gamepadPresses) = ReadKeyStatsTotals();
            snapshot.TotalKeyPresses = keyPresses;
            snapshot.TotalMouseClicks = mouseClicks;
            snapshot.TotalWheelTicks = wheelTicks;
            snapshot.TotalGamepadPresses = gamepadPresses;
            snapshot.TotalUptimeSeconds = ReadUptimeTotalSeconds();

            return snapshot;
        }

        private static bool SafeIsAutostartEnabled()
        {
            try { return AutostartService.IsEnabled(); }
            catch (Exception ex) { Debug.WriteLine($"BuildFeatureSnapshot autostart failed: {ex}"); return false; }
        }

        /// <summary>
        /// Best-effort read of keystats.json's four cumulative totals.
        /// Independent of the live KeyStatsService instance/lock — this is
        /// telemetry, not the source of truth, so a slightly-stale read (up
        /// to the service's own 30s save interval) is fine, and a missing or
        /// unreadable file just yields zeros rather than throwing.
        /// </summary>
        private static (long keys, long mouse, long wheel, long gamepad) ReadKeyStatsTotals()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ClickyKeys", "keystats.json");
                if (!File.Exists(path)) return (0, 0, 0, 0);

                var snap = JsonSerializer.Deserialize<KeyStatsSnapshot>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (snap is null) return (0, 0, 0, 0);

                return (snap.TotalKeyPresses, snap.TotalMouseClicks, snap.TotalWheelTicks, snap.TotalGamepadPresses);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TelemetryService.ReadKeyStatsTotals failed: {ex}");
                return (0, 0, 0, 0);
            }
        }

        /// <summary>Best-effort read of uptime.json's cumulative total, same reasoning as above.</summary>
        private static long ReadUptimeTotalSeconds()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ClickyKeys", "uptime.json");
                if (!File.Exists(path)) return 0;

                var snap = JsonSerializer.Deserialize<UptimeSnapshot>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return snap?.TotalSeconds ?? 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TelemetryService.ReadUptimeTotalSeconds failed: {ex}");
                return 0;
            }
        }

        private static HttpClient BuildHttpClient()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            // Same User-Agent identity convention as RequestReleasesAPI /
            // SponsorshipService, so the backend sees one consistent client
            // fingerprint across every endpoint.
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("ClickyKeysApp", BuildInfo.Version));
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Distro", BuildInfo.Distribution.ToString()));
            http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Type", "application"));

            return http;
        }
    }

    /// <summary>
    /// Wire format POSTed to telemetry.php. <see cref="Feature"/> is present
    /// only for the Full tier (omitted entirely for Basic — see the
    /// JsonIgnoreCondition on the serializer options in SendAppStartAsync).
    /// </summary>
    public sealed class TelemetryEvent
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("app_version")]
        public string AppVersion { get; set; } = string.Empty;

        [JsonPropertyName("distribution")]
        public string Distribution { get; set; } = string.Empty;

        [JsonPropertyName("os_version")]
        public string OsVersion { get; set; } = string.Empty;

        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("timestamp_utc")]
        public DateTime TimestampUtc { get; set; }

        /// <summary>"basic" or "full" — lets the backend validate/branch on
        /// whether a "feature" object should be expected.</summary>
        [JsonPropertyName("level")]
        public string Level { get; set; } = "basic";

        [JsonPropertyName("feature")]
        public TelemetryFeatureSnapshot? Feature { get; set; }
    }

    /// <summary>
    /// Full-tier-only feature-usage snapshot. See
    /// <see cref="TelemetryService.BuildFeatureSnapshot"/> for exactly what
    /// each field means and what was deliberately left out.
    /// </summary>
    public sealed class TelemetryFeatureSnapshot
    {
        [JsonPropertyName("grid_rows")]
        public int GridRows { get; set; }

        [JsonPropertyName("grid_columns")]
        public int GridColumns { get; set; }

        [JsonPropertyName("colors_customized")]
        public bool ColorsCustomized { get; set; }

        [JsonPropertyName("fonts_customized")]
        public bool FontsCustomized { get; set; }

        [JsonPropertyName("rainbow_background_enabled")]
        public bool RainbowBackgroundEnabled { get; set; }

        [JsonPropertyName("rainbow_speed_seconds")]
        public int RainbowSpeedSeconds { get; set; }

        [JsonPropertyName("panels_configured_count")]
        public int PanelsConfiguredCount { get; set; }

        [JsonPropertyName("panels_using_key_input")]
        public int PanelsUsingKeyInput { get; set; }

        [JsonPropertyName("panels_using_mouse_input")]
        public int PanelsUsingMouseInput { get; set; }

        [JsonPropertyName("panels_using_wheel_input")]
        public int PanelsUsingWheelInput { get; set; }

        [JsonPropertyName("panels_using_gamepad_input")]
        public int PanelsUsingGamepadInput { get; set; }

        [JsonPropertyName("gamepad_trigger_threshold")]
        public int GamepadTriggerThreshold { get; set; }

        [JsonPropertyName("autostart_enabled")]
        public bool AutostartEnabled { get; set; }

        [JsonPropertyName("start_minimized")]
        public bool StartMinimized { get; set; }

        [JsonPropertyName("shortcuts_customized")]
        public bool ShortcutsCustomized { get; set; }

        [JsonPropertyName("tutorial_completed")]
        public bool TutorialCompleted { get; set; }

        [JsonPropertyName("total_key_presses")]
        public long TotalKeyPresses { get; set; }

        [JsonPropertyName("total_mouse_clicks")]
        public long TotalMouseClicks { get; set; }

        [JsonPropertyName("total_wheel_ticks")]
        public long TotalWheelTicks { get; set; }

        [JsonPropertyName("total_gamepad_presses")]
        public long TotalGamepadPresses { get; set; }

        [JsonPropertyName("total_uptime_seconds")]
        public long TotalUptimeSeconds { get; set; }
    }
}
