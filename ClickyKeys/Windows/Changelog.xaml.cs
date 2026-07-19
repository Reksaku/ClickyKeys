using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClickyKeys
{
    /// <summary>
    /// Displays the changelog fetched from the API in a dedicated window.
    /// Replaces the previous hardcoded step-0 tutorial overlay.
    /// </summary>
    public partial class Changelog : Window
    {
        private readonly string _sinceVersion;

        // Colours for the change-type badge
        private static readonly SolidColorBrush BrushNew    = new(Color.FromRgb(76, 175, 80));   // green
        private static readonly SolidColorBrush BrushFix    = new(Color.FromRgb(255, 178, 102));  // orange
        private static readonly SolidColorBrush BrushChange =  new(Color.FromRgb(33, 150, 243));  // blue
        private static readonly SolidColorBrush BrushBreaking = new(Color.FromRgb(244, 67, 54));   // red
        private static readonly SolidColorBrush BrushOther  = new(Color.FromRgb(158, 158, 158)); // grey

        public Changelog(string sinceVersion)
        {
            _sinceVersion = sinceVersion;
            InitializeComponent();
            Loaded += async (_, __) => await FetchAndDisplayAsync();
        }

        private static readonly string Endpoint = ResolveEndpoint();
        private static string ResolveEndpoint()
        {
            var host = BuildInfo.Distribution == DistributionType.dev
                ? "https://staging.clickykeys.fun"
                : "https://clickykeys.fun";
            return host + "/api/changelog.php";
        }

        private async System.Threading.Tasks.Task FetchAndDisplayAsync()
        {
            string url = Endpoint + $"?since={Uri.EscapeDataString(_sinceVersion)}";

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("ClickyKeysApp", BuildInfo.Version));
                http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("Distro", BuildInfo.Distribution.ToString()));
                http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("Type", "application"));

                var json = await http.GetStringAsync(url);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var response = JsonSerializer.Deserialize<ChangelogResponse>(json, options);

                if (response == null || response.Entries.Count == 0)
                {
                    ShowError(LocalizationManager.T("Changelog_NoEntries"));
                    return;
                }

                BuildEntries(response);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Changelog fetch failed: {ex}");
                ShowError(LocalizationManager.T("Changelog_LoadErrorConn"));
            }
        }

        private void BuildEntries(ChangelogResponse response)
        {
            SubtitleText.Text = LocalizationManager.Format(
                response.Count == 1 ? "Changelog_ChangesSinceOne" : "Changelog_ChangesSinceMany",
                response.Count, response.Since);

            // Show at most 2 newest entries; the window auto-sizes to fit them.
            int entryIndex = 0;
            foreach (var entry in response.Entries.Take(2))
            {
                // The newest version's caption opens expanded, so the release
                // summary is readable without a click.
                bool isNewestEntry = entryIndex++ == 0;

                // ── Version card ──────────────────────────────────────────

                var card = new MaterialDesignThemes.Wpf.Card
                {
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(12)
                };

                var cardContent = new StackPanel();

                // Version header row
                var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

                headerRow.Children.Add(new TextBlock
                {
                    Text = $"v{entry.Version}",
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center
                });

                headerRow.Children.Add(new TextBlock
                {
                    Text = FormatDate(entry.ReleaseDate),
                    FontSize = 11,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)FindResource("MaterialDesignBody")
                });

                cardContent.Children.Add(headerRow);

                // ── "info" changes act as the version's main caption ───────
                // They render directly under the version header (no badge),
                // styled as a description. Everything else renders as a normal
                // badged change row below.
                foreach (var item in entry.Changes)
                {
                    if (IsInfo(item.Type))
                        cardContent.Children.Add(BuildInfoCaption(item, startExpanded: isNewestEntry));
                }

                // Remaining changes are grouped by type so every version reads
                // in the same order (new → change → fix → …) instead of the
                // arbitrary order the feed happens to return.
                foreach (var item in entry.Changes
                                          .Where(i => !IsInfo(i.Type))
                                          .OrderBy(i => TypeOrder(i.Type)))
                {
                    cardContent.Children.Add(BuildChangeRow(item));
                }

                card.Content = cardContent;
                EntriesPanel.Children.Add(card);
            }

            LoadingPanel.Visibility = Visibility.Collapsed;
            ContentScroller.Visibility = Visibility.Visible;
        }

        private static bool IsInfo(string type) =>
            string.Equals(type, "info", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Builds an "info" change as the version's main caption: no badge,
        /// shown directly under the version header as a description. Expandable
        /// to its full <c>detail</c> when one is present.
        /// </summary>
        /// <param name="startExpanded">
        /// True for the newest version's caption, so its detail is already open
        /// when the window appears.
        /// </param>
        private UIElement BuildInfoCaption(ChangelogItem item, bool startExpanded = false)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };

            // caption (fills) | chevron — same reasoning as BuildChangeRow.
            var headerRow = new Grid { VerticalAlignment = VerticalAlignment.Top };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var caption = new TextBlock
            {
                Text = item.Summary,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("MaterialDesignBody")
            };
            Grid.SetColumn(caption, 0);
            headerRow.Children.Add(caption);

            MakeExpandable(headerRow, item.Detail, container, startExpanded);
            return container;
        }

        /// <summary>
        /// Builds a normal change row: type badge + summary, expandable to its
        /// full <c>detail</c> when one is present.
        /// </summary>
        private UIElement BuildChangeRow(ChangelogItem item)
        {
            var container = new StackPanel { Margin = new Thickness(0, 3, 0, 0) };

            // badge | summary (fills) | chevron. A Grid rather than a horizontal
            // StackPanel so the summary gets all the width left over and only
            // wraps when it genuinely runs out, instead of at a fixed size.
            var headerRow = new Grid { VerticalAlignment = VerticalAlignment.Top };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                Background = BadgeColor(item.Type),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(0, 2, 8, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            badge.Child = new TextBlock
            {
                Text = item.Type.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };

            Grid.SetColumn(badge, 0);
            headerRow.Children.Add(badge);

            var summary = new TextBlock
            {
                Text = item.Summary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(summary, 1);
            headerRow.Children.Add(summary);

            MakeExpandable(headerRow, item.Detail, container);
            return container;
        }

        /// <summary>
        /// Makes a change clickable to reveal its full detail. Appends a chevron
        /// to <paramref name="headerRow"/>, wraps it in a hit-testable border,
        /// and adds a collapsed detail block to <paramref name="container"/>.
        /// When <paramref name="detail"/> is blank, the row stays static.
        /// </summary>
        private void MakeExpandable(Panel headerRow, string detail, StackPanel container,
                                    bool startExpanded = false)
        {
            bool hasDetail = !string.IsNullOrWhiteSpace(detail);

            var headerBorder = new Border
            {
                // Transparent (not null) so the whole row is hit-testable.
                Background = Brushes.Transparent,
                Cursor = hasDetail ? Cursors.Hand : null
            };

            if (!hasDetail)
            {
                headerBorder.Child = headerRow;
                container.Children.Add(headerBorder);
                return;
            }

            var chevron = new TextBlock
            {
                Text = startExpanded ? "▾" : "▸", // ▾ expanded / ▸ collapsed
                FontSize = 11,
                Margin = new Thickness(6, 2, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("MaterialDesignBody")
            };
            // The header is a Grid whose last column is reserved for the chevron,
            // so the summary keeps the rest of the width.
            if (headerRow is Grid grid && grid.ColumnDefinitions.Count > 0)
                Grid.SetColumn(chevron, grid.ColumnDefinitions.Count - 1);
            headerRow.Children.Add(chevron);

            headerBorder.Child = headerRow;
            container.Children.Add(headerBorder);

            var detailBlock = new TextBlock
            {
                Text = detail,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 4, 0, 2),
                FontSize = 12,
                Visibility = startExpanded ? Visibility.Visible : Visibility.Collapsed,
                Foreground = (Brush)FindResource("MaterialDesignBody")
            };
            container.Children.Add(detailBlock);

            headerBorder.MouseLeftButtonUp += (_, __) =>
            {
                bool show = detailBlock.Visibility != Visibility.Visible;
                detailBlock.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                chevron.Text = show ? "▾" : "▸"; // ▾ expanded / ▸ collapsed
            };
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            SubtitleText.Text = LocalizationManager.T("Changelog_LoadErrorSubtitle");
        }

        private static SolidColorBrush BadgeColor(string type) => type.ToLowerInvariant() switch
        {
            "new"       => BrushNew,
            "fix"       => BrushFix,
            "change"    => BrushChange,
            "breaking"  => BrushBreaking,
            _           => BrushOther
        };

        /// <summary>
        /// Sort weight for change types, so each version lists its changes in a
        /// predictable order — new first, then change, fix, breaking, and finally
        /// anything unrecognised. Items sharing a type keep the order the feed
        /// returned them in, because <c>OrderBy</c> is a stable sort.
        /// ("info" never reaches this — those render as the version caption.)
        /// </summary>
        private static int TypeOrder(string type) => type.ToLowerInvariant() switch
        {
            "new"       => 0,
            "change"    => 1,
            "fix"       => 2,
            "breaking"  => 3,
            _           => 4
        };

        private static string FormatDate(string raw)
        {
            if (DateTime.TryParse(raw, out var dt))
                return dt.ToString("d MMM yyyy");
            return raw;
        }

        private void Window_Closed(object sender, System.ComponentModel.CancelEventArgs e) { }

        private void Click_Close(object sender, RoutedEventArgs e) => Close();
    }
}
