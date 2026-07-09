using System;
using System.Diagnostics;
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
        private static readonly SolidColorBrush BrushFix    = new(Color.FromRgb(244, 67, 54));   // red
        private static readonly SolidColorBrush BrushChange = new(Color.FromRgb(255, 178, 102));  // blue
        private static readonly SolidColorBrush BrushInfo   = new(Color.FromRgb(33, 150, 243));  // orange
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
            foreach (var entry in response.Entries.Take(2))
            {
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
                    Foreground = (Brush)FindResource("MaterialDesignBodyLight")
                });

                cardContent.Children.Add(headerRow);

                // ── "info" changes act as the version's main caption ───────
                // They render directly under the version header (no badge),
                // styled as a description. Everything else renders as a normal
                // badged change row below.
                foreach (var item in entry.Changes)
                {
                    if (IsInfo(item.Type))
                        cardContent.Children.Add(BuildInfoCaption(item));
                }

                foreach (var item in entry.Changes)
                {
                    if (!IsInfo(item.Type))
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
        private UIElement BuildInfoCaption(ChangelogItem item)
        {
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };

            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };

            headerRow.Children.Add(new TextBlock
            {
                Text = item.Summary,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 380,
                Foreground = (Brush)FindResource("MaterialDesignBodyLight")
            });

            MakeExpandable(headerRow, item.Detail, container);
            return container;
        }

        /// <summary>
        /// Builds a normal change row: type badge + summary, expandable to its
        /// full <c>detail</c> when one is present.
        /// </summary>
        private UIElement BuildChangeRow(ChangelogItem item)
        {
            var container = new StackPanel { Margin = new Thickness(0, 3, 0, 0) };

            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Top
            };

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

            headerRow.Children.Add(badge);
            headerRow.Children.Add(new TextBlock
            {
                Text = item.Summary,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 340
            });

            MakeExpandable(headerRow, item.Detail, container);
            return container;
        }

        /// <summary>
        /// Makes a change clickable to reveal its full detail. Appends a chevron
        /// to <paramref name="headerRow"/>, wraps it in a hit-testable border,
        /// and adds a collapsed detail block to <paramref name="container"/>.
        /// When <paramref name="detail"/> is blank, the row stays static.
        /// </summary>
        private void MakeExpandable(StackPanel headerRow, string detail, StackPanel container)
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
                Text = "▸", // ▸ collapsed
                FontSize = 11,
                Margin = new Thickness(6, 2, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("MaterialDesignBodyLight")
            };
            headerRow.Children.Add(chevron);

            headerBorder.Child = headerRow;
            container.Children.Add(headerBorder);

            var detailBlock = new TextBlock
            {
                Text = detail,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 4, 0, 2),
                FontSize = 12,
                Visibility = Visibility.Collapsed,
                Foreground = (Brush)FindResource("MaterialDesignBodyLight")
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
            "new"    => BrushNew,
            "fix"    => BrushFix,
            "change" => BrushChange,
            "info"   => BrushInfo,
            _        => BrushOther
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
