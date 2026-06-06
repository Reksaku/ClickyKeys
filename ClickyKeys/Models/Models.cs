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
}
