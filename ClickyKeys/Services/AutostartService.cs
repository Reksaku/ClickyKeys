using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace ClickyKeys
{
    /// <summary>
    /// Manages "launch ClickyKeys when Windows starts" via the per-user
    /// autostart registry key:
    ///
    ///   HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
    ///       value name : "ClickyKeys"
    ///       value data : "&lt;full path to ClickyKeys.exe&gt;"
    ///
    /// Why HKCU and not HKLM: writing under HKCU needs no administrator
    /// elevation and only affects the current user — exactly the right scope
    /// for an opt-in convenience toggle. HKLM\...\Run would need elevation and
    /// would enable autostart for every account on the machine.
    ///
    /// The registry value is the SINGLE SOURCE OF TRUTH for this feature. We
    /// never cache "is autostart on?" in config.json, because the user can
    /// remove the entry through Task Manager > Startup or other tools, and a
    /// cached flag would then lie. <see cref="IsEnabled"/> always re-reads the
    /// key.
    ///
    /// NOTE on the executable path: we resolve it fresh from
    /// <see cref="Environment.ProcessPath"/> every time we enable, and we treat
    /// a stored value whose path no longer matches the current exe as "stale"
    /// (see <see cref="IsEnabled"/>). That way moving or reinstalling the app
    /// to a new location and re-toggling fixes the entry, rather than leaving
    /// Windows pointing the autostart launch at a path that no longer exists.
    /// </summary>
    internal static class AutostartService
    {
        private const string RunKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Registry value name. Must stay stable across versions, otherwise an
        // old entry would be orphaned and the app would appear twice in the
        // Startup list.
        private const string ValueName = "ClickyKeys";

        // Command-line flag appended to the autostart launch command. Its
        // ONLY purpose is to let the app tell, at startup, that it was launched
        // by Windows at login (auto_start) rather than by the user
        // (user_start). See <see cref="App.LaunchTrigger"/>. Must stay stable
        // across versions so an entry written by an older build still parses.
        public const string AutostartArg = "--autostart";

        /// <summary>
        /// Absolute path to the running executable, quoted so a path
        /// containing spaces (e.g. "C:\Program Files\...") is parsed by the
        /// shell as a single argument. Returns null if the path can't be
        /// determined (extremely unusual — would indicate a hosting model we
        /// don't support).
        /// </summary>
        private static string? QuotedExePath
        {
            get
            {
                var path = Environment.ProcessPath;
                return string.IsNullOrEmpty(path) ? null : $"\"{path}\"";
            }
        }

        /// <summary>
        /// Full command written to the Run value: the quoted executable path
        /// followed by <see cref="AutostartArg"/>. The trailing flag is what
        /// lets <see cref="App.LaunchTrigger"/> resolve to <c>auto_start</c>
        /// when Windows launches the app at login. Returns null when the exe
        /// path can't be determined.
        /// </summary>
        private static string? LaunchCommand
        {
            get
            {
                var exe = QuotedExePath;
                return exe is null ? null : $"{exe} {AutostartArg}";
            }
        }

        /// <summary>
        /// True only when the Run value exists AND points at the executable
        /// that is currently running. A value that points somewhere else is
        /// reported as not-enabled so the UI prompts a refresh on next toggle.
        /// </summary>
        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                var stored = key?.GetValue(ValueName) as string;
                if (string.IsNullOrEmpty(stored))
                    return false;

                var expected = LaunchCommand;
                if (expected is null)
                    return false;

                // Case-insensitive compare; Windows paths aren't case
                // sensitive and the shell may normalise casing.
                return string.Equals(stored, expected, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutostartService.IsEnabled failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Writes (or overwrites) the Run value so the app launches at login.
        /// Returns false on failure so the caller can revert the toggle UI.
        /// </summary>
        public static bool Enable()
        {
            try
            {
                var exe = LaunchCommand;
                if (exe is null)
                    return false;

                // writable:true creates the Run subkey if it somehow doesn't
                // exist (it always does on a normal Windows install).
                using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
                if (key is null)
                    return false;

                key.SetValue(ValueName, exe, RegistryValueKind.String);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutostartService.Enable failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Removes the Run value. A missing value is treated as success — the
        /// desired end state ("not in startup") is already true.
        /// </summary>
        public static bool Disable()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key is null)
                    return true; // No Run key at all → nothing to remove.

                if (key.GetValue(ValueName) != null)
                    key.DeleteValue(ValueName, throwOnMissingValue: false);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AutostartService.Disable failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Convenience wrapper: enable or disable to match
        /// <paramref name="enabled"/>. Returns whether the operation
        /// succeeded.
        /// </summary>
        public static bool Set(bool enabled) => enabled ? Enable() : Disable();
    }
}
