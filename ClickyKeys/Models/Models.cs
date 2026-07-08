using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace ClickyKeys
{
    public class MyReleasesResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("release_id")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("safety_signature")]
        public string Signature { get; set; } = string.Empty;

        [JsonPropertyName("release_date")]
        public DateTime Release_date { get; set; }

        [JsonPropertyName("distribution")]
        public DistributionType distribution { get; set; } = DistributionType.dev;
    }
    public enum DistributionType
    {
        dev = 0,
        store = 1,
        github = 2

    }

    public class Configuration
    {
        // Default to the version baked into the running binary so a fresh
        // config.json is never marked as "older" than the build that wrote
        // it. The hardcoded build version lives in BuildInfo so there's
        // one source of truth for update detection.
        [JsonPropertyName("version")]
        public string Version { get; set; } = BuildInfo.Version;

        // Distribution was previously a JSON-serializable property, which
        // meant any user could edit config.json and switch their "channel"
        // (e.g. silence update prompts by flipping to dev). It now lives in
        // BuildInfo.Distribution as a compile-time constant that cannot be
        // overridden from disk. Existing JSON entries named "distribution"
        // are ignored on load (System.Text.Json skips unknown properties).

        [JsonPropertyName("settings_profile")]
        public string AppearanceProfile { get; set; } = "default settings.json";

        [JsonPropertyName("panels_profile")]
        public string PanelsProfile { get; set; } = "default panels.json";

        [JsonPropertyName("show_tutorial")]
        public bool ShowTutorial { get; set; } = true;

        // When true the app realizes its window and then immediately tucks it
        // away to the system tray on launch (see App.OnStartup +
        // MainWindow.MinimizeToTaskbarAtStartup). Persisted here so the choice is
        // honoured on EVERY launch, not just autostart launches.
        //
        // NOTE: launch-on-startup is deliberately NOT stored here — the
        // Windows "Run" registry value is its single source of truth, so the
        // config can never drift out of sync with what the OS will actually
        // do. See AutostartService.
        [JsonPropertyName("start_minimized")]
        public bool StartMinimized { get; set; } = false;

        // Master switch for input-statistics collection (KeyStatsService).
        // When false, no new key/mouse/wheel events are counted or written to
        // keystats.json, and the Statistics window shows a "collection disabled"
        // notice instead of the counters. Defaults to true so existing users
        // keep the behaviour they had before this setting existed.
        [JsonPropertyName("collect_key_stats")]
        public bool CollectKeyStats { get; set; } = true;

        // Master switch for application-uptime collection (UptimeStatsService).
        // When false, running time stops accumulating and uptime.json is no
        // longer written; the Statistics window shows an "uptime disabled"
        // notice in place of the value. Defaults to true to match prior
        // behaviour for existing users.
        [JsonPropertyName("collect_uptime")]
        public bool CollectUptime { get; set; } = true;

        // UI language as a culture code ("en", "pl", "pt-BR", ...). Applied at
        // startup by App.OnStartup and switched live from the Settings window.
        //
        // EMPTY = "not chosen yet": on first launch (or for an older config.json
        // with no "language" field) this stays empty, which signals
        // App.OnStartup to AUTO-DETECT the system language
        // (LocalizationManager.ResolveStartupLanguage). The field is only
        // written with a concrete code once the user explicitly picks a
        // language in Settings, after which that choice always wins over the
        // system language. Unknown values fall back to English via
        // LocalizationManager.Normalize.
        [JsonPropertyName("language")]
        public string Language { get; set; } = "";

        // Global shortcut keys handled by InputCounter via the low-level hook.
        // Defaults preserve the historical behaviour: F12 resets all counters,
        // F11 toggles the toolbar. Serialized as the Key enum name (e.g. "F12")
        // so the value round-trips losslessly and is human-readable in
        // config.json. The user can reassign both from the Settings window.
        [JsonPropertyName("reset_key")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Key ResetKey { get; set; } = Key.F12;

        [JsonPropertyName("toggle_toolbar_key")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Key ToggleToolbarKey { get; set; } = Key.F11;

        // How far an analog gamepad trigger (LT/RT) must be pressed before it
        // counts as a single press, in percent (1–100). Adjustable in Settings.
        [JsonPropertyName("gamepad_trigger_threshold")]
        public int GamepadTriggerThreshold { get; set; } = 25;

        // ── Product telemetry consent (DRAFT) ───────────────────────────
        //
        // Master opt-in switch for TelemetryService, with THREE consent
        // states rather than a plain on/off:
        //   null  = user has never been asked (fresh install, or upgrading
        //           from a version that predates telemetry). MainWindow shows
        //           ConsentDialog once on the next startup and persists the
        //           answer here.
        //   None  = user opted out (either from the initial dialog or later
        //           from Settings → Data Collection). No payload is ever
        //           sent while this is None.
        //   Basic = anonymous id + app version + distribution + OS version +
        //           UI language, once per launch. Same payload as before
        //           this tier split existed.
        //   Full  = everything Basic sends, PLUS a feature-usage snapshot
        //           (panel/grid layout, display modes, color/font
        //           customization, shortcuts, gamepad threshold, aggregate
        //           key/mouse/uptime counters) so we can tell whether
        //           shipped features are actually being used and configured
        //           correctly — see TelemetryService.BuildFeatureSnapshot for
        //           the exact field list and what is deliberately excluded
        //           (no panel descriptions/labels, no raw color values tied
        //           to a person, nothing the user typed).
        //
        // This is the ONLY signal TelemetryService trusts to decide whether
        // — and how much — to send. See TelemetryService.ConfigureCollecting.
        [JsonPropertyName("telemetry_level")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TelemetryLevel? TelemetryLevel { get; set; } = null;

        // Random identifier generated locally the first time the user opts
        // in (see TelemetryService.EnsureUserId). Intentionally NOT derived
        // from any hardware/OS identifier (MAC address, volume serial,
        // machine GUID, etc.) — it exists only to let backend aggregation
        // distinguish "1 user across 30 sessions" from "30 users", and
        // resets to a new value if the user clears config.json. Empty until
        // consent is first given. Shared by both the Basic and Full tiers —
        // switching between them does not generate a new id.
        [JsonPropertyName("telemetry_user_id")]
        public string TelemetryUserId { get; set; } = "";
    }

    /// <summary>
    /// The three telemetry consent tiers a user can choose (see
    /// <see cref="Configuration.TelemetryLevel"/> for what each one means and
    /// sends). Persisted as its string name in config.json
    /// (<c>"None"</c>/<c>"Basic"</c>/<c>"Full"</c>) via
    /// <see cref="JsonStringEnumConverter"/> so the file stays human-readable.
    /// </summary>
    public enum TelemetryLevel
    {
        None = 0,
        Basic = 1,
        Full = 2,
    }

    public enum InputType
    {
        None = 0,

        // Keyboard
        Key = 1,

        // Mouse – buttons
        MouseLeft = 10,
        MouseRight = 11,
        MouseMiddle = 12,
        MouseXButton1 = 13,
        MouseXButton2 = 14,

        // Mouse – scroll
        MouseWheelUp = 20,
        MouseWheelDown = 21,
        MouseWheelLeft = 22,
        MouseWheelRight = 23,

        // Game controller button (specific button stored in
        // PanelsSettings.GamepadButton). Matched against any connected pad.
        Gamepad = 30,
    }

    public enum ColorTarget
    {
        Panels,
        Background,
        Keys,
        Values
    }


    public class PanelsSettings
    {
        public int Index { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Key KeyCode { get; set; } = Key.None;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public InputType Input { get; set; } = InputType.Key;

        // Raw button index when Input == Gamepad; -1 = unset. Older config
        // files without this field deserialize to -1, so key/mouse panels are
        // unaffected.
        public int GamepadButton { get; set; } = -1;

        public string Description { get; set; } = "";
    }

    public class PanelState
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "0.0.0";

        [JsonPropertyName("panels")]
        public List<PanelsSettings> Panels { get; set; } = new();
    }

    public class hsvColor
    {
        public double hue = 0.0;
        public double sat = 1.0;
        public double val = 1.0;
    }

    public class ColorsPallet
    {
        public Color background;
        public Color panels;
        public Color keys;
        public Color values;
    }

    // ── Changelog API ─────────────────────────────────────────────────────────

    public class ChangelogResponse
    {
        [JsonPropertyName("since")]
        public string Since { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("entries")]
        public List<ChangelogEntry> Entries { get; set; } = [];
    }

    public class ChangelogEntry
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("release_date")]
        public string ReleaseDate { get; set; } = string.Empty;

        [JsonPropertyName("changes")]
        public List<ChangelogItem> Changes { get; set; } = [];
    }

    public class ChangelogItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("detail")]
        public string Detail { get; set; } = string.Empty;
    }

    // ── Messages / Inbox API ───────────────────────────────────────────────────
    //
    // Announcements & news, fetched incrementally from
    // clickykeys.fun/api/messages.php. The client stores an opaque, server-
    // signed cursor and echoes it back so it only ever receives messages newer
    // than its last successful check (see MessagesService + MESSAGES_PLAN.md).

    /// <summary>
    /// One request's worth of the messages feed: the messages newer than the
    /// cursor the client sent, plus the new cursor to persist for next time.
    /// </summary>
    public class MessagesResponse
    {
        // Opaque, server-signed watermark. The client stores this verbatim and
        // sends it back next launch; it never parses or trusts its contents.
        // Empty/absent on error responses — in which case the client keeps the
        // cursor it already had.
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<MessageEntry> Messages { get; set; } = [];
    }

    /// <summary>
    /// A single announcement. <see cref="Id"/> is the stable key used for the
    /// read/unread set and for cache de-duplication.
    /// </summary>
    public class MessageEntry
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "announcement";

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("publish_at")]
        public DateTime PublishAt { get; set; }

        // null = never expires.
        [JsonPropertyName("expires_at")]
        public DateTime? ExpiresAt { get; set; }

        // Optional; omitted target means "everyone".
        [JsonPropertyName("target")]
        public MessageTarget? Target { get; set; }

        // Optional call-to-action. Only https links are ever opened.
        [JsonPropertyName("link")]
        public MessageLink? Link { get; set; }
    }

    /// <summary>
    /// Audience filter for a message. Both criteria are also enforced server-
    /// side (from the User-Agent tokens); the client re-checks as a defensive
    /// measure. A null/empty field means "no restriction on that axis".
    /// </summary>
    public class MessageTarget
    {
        // Distribution channels this message is for ("store", "github").
        // Empty/absent = all channels.
        [JsonPropertyName("distributions")]
        public List<string> Distributions { get; set; } = [];

        // Version rule. Supported forms:
        //   ""        → all versions
        //   "2.4.2"   → exactly that version
        //   "2.4.0+"  → that version or newer
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    public class MessageLink
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// The locally persisted state of the inbox, stored DPAPI-encrypted in
    /// <c>%AppData%\ClickyKeys\messages.dat</c> (never in config.json). Because
    /// fetching is incremental, <see cref="Cache"/> is the ONLY complete record
    /// of what the user has been shown — it can't be rebuilt from the server.
    /// A failure to decrypt/parse self-heals to a fresh empty instance.
    /// </summary>
    public class MessagesState
    {
        // Last server-issued cursor. Empty = "never fetched"; the client then
        // sends no cursor and the server opens the delivery window at "now".
        [JsonPropertyName("cursor")]
        public string Cursor { get; set; } = string.Empty;

        // Ids of messages the user has opened/read.
        [JsonPropertyName("read_ids")]
        public List<int> ReadIds { get; set; } = [];

        // Every message delivered so far (post-filter), de-duplicated by id.
        [JsonPropertyName("cache")]
        public List<MessageEntry> Cache { get; set; } = [];
    }
}
