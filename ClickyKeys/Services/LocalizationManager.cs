using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;

namespace ClickyKeys
{
    /// <summary>
    /// Runtime UI localization for ClickyKeys.
    ///
    /// HOW IT WORKS
    /// ------------
    /// Every user-facing string lives in a per-language string
    /// <see cref="ResourceDictionary"/> under <c>/Resources</c>
    /// (<c>Strings.en.xaml</c>, <c>Strings.pl.xaml</c>, …). Exactly one of
    /// those dictionaries is merged into <see cref="Application.Resources"/> at
    /// any time. XAML references those strings with
    /// <c>{DynamicResource Key}</c>; because the reference is *dynamic*, every
    /// already-open window re-resolves its text the instant we swap the merged
    /// dictionary — so switching language takes effect live, with no restart
    /// and no per-window rebuild.
    ///
    /// Code-behind that needs a string (e.g. MessageBox text, values formatted
    /// at runtime) should call <see cref="T(string)"/> / <see cref="Format"/>
    /// rather than hardcoding English.
    ///
    /// PERSISTENCE
    /// -----------
    /// The chosen culture code is stored in <c>config.json</c>
    /// (<see cref="Configuration.Language"/>) and re-applied on the next launch
    /// by <see cref="App.OnStartup"/>.
    /// </summary>
    internal static class LocalizationManager
    {
        /// <summary>Culture codes we ship translations for, in display order.</summary>
        public static readonly IReadOnlyList<string> SupportedLanguages = new[]
        {
            "en", "pl", "de", "es", "fr", "it", "nb", "nl", "sv", "es-MX", "pt-BR", "pt", "el"
        };

        public const string DefaultLanguage = "en";

        /// <summary>The culture code currently applied (e.g. "en", "pl").</summary>
        public static string CurrentLanguage { get; private set; } = DefaultLanguage;

        /// <summary>
        /// Raised after the language has been switched. Lets code-behind that
        /// rendered text imperatively (not via DynamicResource) refresh itself.
        /// </summary>
        public static event EventHandler? LanguageChanged;

        // The string dictionary we last merged in, so we can pull it back out
        // before merging the replacement (otherwise stale keys would linger and
        // mask the new ones depending on merge order).
        private static ResourceDictionary? _current;

        /// <summary>
        /// Normalizes an arbitrary/blank/unknown culture code to the canonical
        /// form of one we ship, falling back to <see cref="DefaultLanguage"/>.
        ///
        /// Region-aware: an exact (case-insensitive) match to a shipped code is
        /// preferred so regional variants like "es-MX" or "pt-BR" resolve to
        /// their own dictionary rather than collapsing to the base language.
        /// Only when there's no exact entry do we fall back to the primary
        /// subtag (so "pl-PL" still maps to "pl", and an unshipped "es-AR"
        /// degrades gracefully to "es"). The return value is always the exact
        /// string from <see cref="SupportedLanguages"/>, so callers can compare
        /// it directly against ComboBox tags / file names.
        /// </summary>
        public static string Normalize(string? code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return DefaultLanguage;

            var c = code.Trim();

            // 1) Exact, region-aware match (e.g. "es-MX", "pt-BR", "en").
            foreach (var s in SupportedLanguages)
                if (string.Equals(s, c, StringComparison.OrdinalIgnoreCase))
                    return s;

            // 2) Fall back to the primary subtag (e.g. "pl-PL" -> "pl",
            //    "es-AR" -> "es") when we don't ship that exact region.
            var primary = c.Split('-')[0];
            foreach (var s in SupportedLanguages)
                if (string.Equals(s, primary, StringComparison.OrdinalIgnoreCase))
                    return s;

            return DefaultLanguage;
        }

        /// <summary>
        /// Swaps the active string dictionary and updates the thread cultures.
        /// Safe to call repeatedly; a no-op if the requested language is already
        /// active. Must run on the UI thread (touches Application.Resources).
        /// </summary>
        public static void Apply(string? code)
        {
            var lang = Normalize(code);

            var dict = LoadDictionary(lang);
            if (dict is null)
            {
                // Couldn't find the requested pack resource — fall back to
                // English so the UI is never left with unresolved keys.
                if (lang != DefaultLanguage)
                {
                    Apply(DefaultLanguage);
                    return;
                }
                Debug.WriteLine("LocalizationManager: default dictionary missing.");
                return;
            }

            var app = Application.Current;
            if (app is null) return;

            // Remove the previously merged language dictionary (if any) before
            // adding the new one.
            if (_current is not null)
                app.Resources.MergedDictionaries.Remove(_current);

            app.Resources.MergedDictionaries.Add(dict);
            _current = dict;
            CurrentLanguage = lang;

            // Keep .NET's culture in sync so date/number formatting and any
            // CultureInfo-driven logic match the chosen UI language.
            try
            {
                var ci = CultureInfo.GetCultureInfo(lang);
                Thread.CurrentThread.CurrentCulture = ci;
                Thread.CurrentThread.CurrentUICulture = ci;
                CultureInfo.DefaultThreadCurrentCulture = ci;
                CultureInfo.DefaultThreadCurrentUICulture = ci;
            }
            catch (CultureNotFoundException) { /* keep process default */ }

            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        private static ResourceDictionary? LoadDictionary(string lang)
        {
            try
            {
                var uri = new Uri(
                    $"pack://application:,,,/Resources/Strings.{lang}.xaml",
                    UriKind.Absolute);
                return new ResourceDictionary { Source = uri };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalizationManager: failed to load '{lang}': {ex}");
                return null;
            }
        }

        /// <summary>
        /// Looks up a localized string by key from the active dictionaries.
        /// Returns the key itself if the resource is missing, which makes
        /// untranslated keys obvious in the UI rather than throwing.
        /// </summary>
        public static string T(string key)
        {
            var res = Application.Current?.TryFindResource(key);
            return res as string ?? key;
        }

        /// <summary>
        /// <see cref="T(string)"/> followed by <see cref="string.Format(string, object[])"/>
        /// using the current culture.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            var fmt = T(key);
            try { return string.Format(CultureInfo.CurrentCulture, fmt, args); }
            catch (FormatException) { return fmt; }
        }
    }
}
