using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
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
        private static readonly SolidColorBrush BrushChange = new(Color.FromRgb(33, 150, 243));  // blue
        private static readonly SolidColorBrush BrushOther  = new(Color.FromRgb(158, 158, 158)); // grey

        public Changelog(string sinceVersion)
        {
            _sinceVersion = sinceVersion;
            InitializeComponent();
            Loaded += async (_, __) => await FetchAndDisplayAsync();
        }

        private async System.Threading.Tasks.Task FetchAndDisplayAsync()
        {
            string url = $"https://clickykeys.fun/api/changelog.php?since={Uri.EscapeDataString(_sinceVersion)}";

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

                // Change items
                foreach (var item in entry.Changes)
                {
                    var row = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 3, 0, 0),
                        VerticalAlignment = VerticalAlignment.Top
                    };

                    // Type badge
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

                    var summaryBlock = new TextBlock
                    {
                        Text = item.Summary,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        MaxWidth = 380
                    };

                    row.Children.Add(badge);
                    row.Children.Add(summaryBlock);
                    cardContent.Children.Add(row);
                }

                card.Content = cardContent;
                EntriesPanel.Children.Add(card);
            }

            LoadingPanel.Visibility = Visibility.Collapsed;
            ContentScroller.Visibility = Visibility.Visible;
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
