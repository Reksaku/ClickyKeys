using System.Collections.Generic;
using System.Text;

namespace ClickyKeys
{
    /// <summary>
    /// Translates raw <c>System.Windows.Input.Key</c> / <c>MouseButton</c>
    /// enum names (as persisted in <c>keystats.json</c>) into labels suited
    /// for the everyday user that opens the Stats window.
    ///
    /// Why a separate layer: the on-disk schema used by
    /// <see cref="KeyStatsService"/> is intentionally the verbatim
    /// <c>Enum.ToString()</c> output, because it round-trips losslessly via
    /// <c>Enum.TryParse</c> on load and keeps the JSON snapshot stable
    /// across schema versions. Those raw names ("OemComma", "D5",
    /// "LeftCtrl", "MouseWheelDown") are unfriendly to anyone who is not
    /// familiar with the WPF input enums, so we translate at the UI seam
    /// rather than at the persistence seam.
    ///
    /// Rules applied:
    /// <list type="bullet">
    /// <item><c>D0..D9</c> → the digit itself (top-row number keys).</item>
    /// <item><c>Oem*</c> → the actual punctuation symbol the key produces
    /// on a US-layout keyboard (e.g. <c>OemComma</c> → ",", <c>Oem3</c> →
    /// "`").</item>
    /// <item>Compound PascalCase names get spaces inserted at case
    /// boundaries (<c>LeftCtrl</c> → "Left Ctrl", <c>PageUp</c> → "Page
    /// Up", <c>MouseWheelDown</c> → "Mouse Wheel Down").</item>
    /// <item>A handful of common keys with non-obvious enum names get
    /// dedicated overrides (<c>Back</c> → "Backspace", <c>Return</c> →
    /// "Enter", <c>Snapshot</c> → "Print Screen", etc.).</item>
    /// </list>
    /// </summary>
    public static class FriendlyKeyName
    {
        // Note: lookups are case-insensitive so this is robust to any
        // casing drift in the JSON file (e.g. hand edits, older versions).
        private static readonly Dictionary<string, string> KeyMap =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            // -- Top-row digits (D0..D9 in the WPF Key enum) --
            ["D0"] = "0", ["D1"] = "1", ["D2"] = "2", ["D3"] = "3", ["D4"] = "4",
            ["D5"] = "5", ["D6"] = "6", ["D7"] = "7", ["D8"] = "8", ["D9"] = "9",

            // -- OEM punctuation: replace with the symbol the key prints --
            // Both the numbered (Oem1..Oem8) and named aliases are mapped so
            // either form in the JSON file resolves to the same label.
            ["Oem1"] = ";",   ["OemSemicolon"]     = ";",
            ["Oem2"] = "/",   ["OemQuestion"]      = "/",
            ["Oem3"] = "`",   ["OemTilde"]         = "`",
            ["Oem4"] = "[",   ["OemOpenBrackets"]  = "[",
            ["Oem5"] = "\\",  ["OemPipe"]          = "\\",  ["OemBackslash"] = "\\",
            ["Oem6"] = "]",   ["OemCloseBrackets"] = "]",
            ["Oem7"] = "'",   ["OemQuotes"]        = "'",
            ["Oem8"] = "Oem 8",
            ["OemComma"]  = ",",
            ["OemPeriod"] = ".",
            ["OemMinus"]  = "-",
            ["OemPlus"]   = "=",

            // -- Common keys whose enum name is not what the keycap says --
            ["Back"]        = "Backspace",
            ["Capital"]     = "Caps Lock",
            ["CapsLock"]    = "Caps Lock",
            ["Escape"]      = "Esc",
            ["Return"]      = "Enter",
            ["Snapshot"]    = "Print Screen",
            ["PrintScreen"] = "Print Screen",
            ["Apps"]        = "Menu",
            ["Next"]        = "Page Down",
            ["PageDown"]    = "Page Down",
            ["Prior"]       = "Page Up",
            ["PageUp"]      = "Page Up",
            ["Scroll"]      = "Scroll Lock",
            ["NumLock"]     = "Num Lock",

            // Windows keys + arrows (Up/Down/Left/Right are also valid mouse
            // names — the mouse map below has its own entries).
            ["LWin"]  = "Left Win",
            ["RWin"]  = "Right Win",
            ["Up"]    = "Up Arrow",
            ["Down"]  = "Down Arrow",
            ["Left"]  = "Left Arrow",
            ["Right"] = "Right Arrow",

            // Numpad operators — the bare enum names ("Add", "Subtract",
            // "Multiply", "Divide", "Decimal", "Separator") look nothing
            // like the keycap, so spell out which key they refer to.
            ["Add"]       = "Numpad +",
            ["Subtract"]  = "Numpad -",
            ["Multiply"]  = "Numpad *",
            ["Divide"]    = "Numpad /",
            ["Decimal"]   = "Numpad .",
            ["Separator"] = "Numpad ,",

            // Modifier pairs — the generic CamelCase splitter would also
            // produce "Left Ctrl", but listing them explicitly documents
            // the intended label and avoids any drift if the splitter is
            // tweaked later.
            ["LeftCtrl"]   = "Left Ctrl",
            ["RightCtrl"]  = "Right Ctrl",
            ["LeftShift"]  = "Left Shift",
            ["RightShift"] = "Right Shift",
            ["LeftAlt"]    = "Left Alt",
            ["RightAlt"]   = "Right Alt",

            // Numpad digits — "NumPad0" is two compound parts (Num+Pad)
            // followed by a digit, which the splitter would render as
            // "Num Pad 0". The conventional spelling is "Numpad", so
            // override.
            ["NumPad0"] = "Numpad 0", ["NumPad1"] = "Numpad 1",
            ["NumPad2"] = "Numpad 2", ["NumPad3"] = "Numpad 3",
            ["NumPad4"] = "Numpad 4", ["NumPad5"] = "Numpad 5",
            ["NumPad6"] = "Numpad 6", ["NumPad7"] = "Numpad 7",
            ["NumPad8"] = "Numpad 8", ["NumPad9"] = "Numpad 9",
        };

        private static readonly Dictionary<string, string> MouseMap =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            // Mouse buttons share the literal name "Left"/"Right" with the
            // arrow keys, so they need their own dictionary rather than
            // being merged with KeyMap.
            ["Left"]     = "Left Click",
            ["Right"]    = "Right Click",
            ["Middle"]   = "Middle Click",
            ["XButton1"] = "Mouse Button 4",
            ["XButton2"] = "Mouse Button 5",
        };

        private static readonly Dictionary<string, string> WheelMap =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            ["MouseWheelUp"]    = "Wheel Up",
            ["MouseWheelDown"]  = "Wheel Down",
            ["MouseWheelLeft"]  = "Wheel Left",
            ["MouseWheelRight"] = "Wheel Right",
        };

        public static string ForKey(string raw)         => Translate(raw, KeyMap);
        public static string ForMouseButton(string raw) => Translate(raw, MouseMap);
        public static string ForWheel(string raw)       => Translate(raw, WheelMap);

        private static string Translate(string raw, Dictionary<string, string> overrides)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            if (overrides.TryGetValue(raw, out var mapped)) return mapped;
            return SplitPascalCase(raw);
        }

        /// <summary>
        /// Inserts spaces at PascalCase boundaries so compound enum names
        /// read naturally. Letters and digits are kept fused so values like
        /// <c>"F12"</c> are preserved rather than rendered as <c>"F 12"</c>.
        /// Boundary rules:
        /// <list type="bullet">
        /// <item>lower → upper (e.g. <c>"PageUp"</c> → <c>"Page Up"</c>)</item>
        /// <item>upper → upper followed by lower (acronym → word, e.g.
        /// <c>"XMLHttp"</c> → <c>"XML Http"</c>)</item>
        /// </list>
        /// </summary>
        private static string SplitPascalCase(string raw)
        {
            if (raw.Length <= 1) return raw;

            var sb = new StringBuilder(raw.Length + 4);
            sb.Append(raw[0]);
            for (int i = 1; i < raw.Length; i++)
            {
                char prev = raw[i - 1];
                char curr = raw[i];

                bool lowerToUpper =
                    char.IsLower(prev) && char.IsUpper(curr);

                bool acronymBoundary =
                    char.IsUpper(prev) && char.IsUpper(curr)
                    && i + 1 < raw.Length && char.IsLower(raw[i + 1]);

                if (lowerToUpper || acronymBoundary)
                    sb.Append(' ');

                sb.Append(curr);
            }
            return sb.ToString();
        }
    }
}
