using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace ClickyKeys
{
    /// <summary>
    /// Read-only statistics view. Mirrors the lifecycle/ownership pattern of
    /// <see cref="Info"/>: created by <see cref="MainWindow"/> with itself as
    /// the <see cref="IOverlay"/>, shown non-modally, signals back via
    /// <see cref="IOverlay.OnStatsClose"/> on close.
    ///
    /// Data source: the same JSON snapshot that <see cref="KeyStatsService"/>
    /// writes every 30 s. To avoid showing values that lag by up to half a
    /// minute, we call <see cref="KeyStatsService.Flush"/> right before
    /// reading — so the file on disk matches in-memory state at the instant
    /// the window opens.
    /// </summary>
    public partial class Stats : Window
    {
        private readonly IOverlay _mainOverlay;

        // Path is duplicated rather than reached via a singleton accessor
        // because KeyStatsService keeps its file path private — this view is
        // intentionally a passive reader of the on-disk format. If the file
        // location ever moves, both spots need to update; the schema version
        // in the JSON guards against silent format drift.
        private static readonly string StatsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickyKeys",
            "keystats.json");

        private static readonly JsonSerializerOptions JsonReadOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        public Stats(IOverlay mainOverlay)
        {
            _mainOverlay = mainOverlay;
            InitializeComponent();

            // Respect the "Collect key-press statistics" setting. Prefer the
            // live collector's state (the source of truth while running); fall
            // back to the persisted flag if the service isn't available. When
            // collection is off we show a notice instead of the counters and
            // skip the flush/read entirely.
            bool collecting = App.KeyStats?.IsCollecting ?? ConfigStore.Load().CollectKeyStats;
            if (!collecting)
            {
                ShowDisabledState();
                return;
            }

            // Force the live in-memory counters to disk so the snapshot we
            // read below isn't stale. Failure here is non-fatal — we'll just
            // show whatever the last successful save contained.
            try { App.KeyStats?.Flush(); }
            catch (Exception ex) { Debug.WriteLine($"Stats: pre-read flush failed: {ex}"); }

            LoadAndRender();
        }

        /// <summary>
        /// Swaps the window into its "collection turned off" presentation:
        /// hides every counter card and reveals the notice that points the
        /// user at Settings. No file is read in this state.
        /// </summary>
        private void ShowDisabledState()
        {
            DisabledPanel.Visibility = Visibility.Visible;
            StatsContentPanel.Visibility = Visibility.Collapsed;
            LastUpdatedText.Text = "Statistics collection is disabled.";
        }

        private void LoadAndRender()
        {
            var snapshot = TryLoadSnapshot();

            // Totals card: mouse / keys / wheel separately, plus a combined
            // line. Numbers are thousand-grouped using the OS locale so big
            // counts (gamers will hit 6-digit territory fast) stay readable.
            TotalMouseClicksText.Text = FormatCount(snapshot.TotalMouseClicks);
            TotalKeyPressesText.Text = FormatCount(snapshot.TotalKeyPresses);
            TotalWheelTicksText.Text = FormatCount(snapshot.TotalWheelTicks);
            TotalCombinedText.Text = FormatCount(
                snapshot.TotalMouseClicks
                + snapshot.TotalKeyPresses
                + snapshot.TotalWheelTicks);

            // Per-bucket lists, sorted descending by count so the most-used
            // entries surface at the top — that's the usual reason someone
            // opens this view.
            //
            // The raw snapshot keys are System.Windows.Input enum names
            // ("OemComma", "D5", "LeftCtrl", "MouseWheelDown", ...) — fine
            // for the on-disk schema but unfriendly in the UI. We translate
            // them through FriendlyKeyName before building each StatRow so
            // the user sees the actual symbol or a spaced-out compound name
            // (",", "5", "Left Ctrl", "Wheel Down", ...).
            var keyRows = snapshot.Keys
                .Select(kv => new StatRow(FriendlyKeyName.ForKey(kv.Key), kv.Value))
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ToList();

            var mouseRows = snapshot.Mouse
                .Select(kv => new StatRow(FriendlyKeyName.ForMouseButton(kv.Key), kv.Value))
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ToList();

            var wheelRows = snapshot.Wheel
                .Select(kv => new StatRow(FriendlyKeyName.ForWheel(kv.Key), kv.Value))
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ToList();

            KeysList.ItemsSource = keyRows;
            MouseList.ItemsSource = mouseRows;
            WheelList.ItemsSource = wheelRows;

            // Empty-state placeholders — friendlier than blank cards on a
            // first run before any input has been recorded.
            KeysEmptyText.Visibility = keyRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            MouseEmptyText.Visibility = mouseRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            WheelEmptyText.Visibility = wheelRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            LastUpdatedText.Text = snapshot.LastUpdatedUtc == default
                ? "No data recorded yet."
                : $"Last updated: {snapshot.LastUpdatedUtc.ToLocalTime():g}";
        }

        private static KeyStatsSnapshot TryLoadSnapshot()
        {
            // Returning an empty snapshot on any failure (missing file, bad
            // JSON, IO error) keeps the UI rendering predictable — the user
            // sees zeros and the "no data" placeholders rather than a crash
            // dialog or a half-rendered window.
            if (!File.Exists(StatsFilePath))
                return new KeyStatsSnapshot();

            try
            {
                var json = File.ReadAllText(StatsFilePath);
                return JsonSerializer.Deserialize<KeyStatsSnapshot>(json, JsonReadOpts)
                       ?? new KeyStatsSnapshot();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Stats: failed to load {StatsFilePath}: {ex}");
                return new KeyStatsSnapshot();
            }
        }

        private static string FormatCount(long value) =>
            value.ToString("N0", CultureInfo.CurrentCulture);

        private void Click_Close(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object? sender, EventArgs e) =>
            _mainOverlay.OnStatsClose();
    }

    /// <summary>
    /// Row DTO bound by <c>StatRowTemplate</c> in Stats.xaml. Pre-formats
    /// the count in the constructor so the XAML template doesn't need a
    /// value converter just to insert thousands separators.
    /// </summary>
    public sealed class StatRow
    {
        public string Name { get; }
        public long Count { get; }
        public string CountFormatted { get; }

        public StatRow(string name, long count)
        {
            Name = name;
            Count = count;
            CountFormatted = count.ToString("N0", CultureInfo.CurrentCulture);
        }
    }
}
