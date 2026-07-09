using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClickyKeys
{
    /// <summary>
    /// The inbox window: lists the announcements/news already fetched and cached
    /// locally by <see cref="MessagesService"/>. Phase 2 UI over the Phase 1
    /// pipeline — it reads <see cref="MessagesService.GetCachedMessages"/>, and
    /// marks everything it shows as read via
    /// <see cref="MessagesService.MarkRead"/> so the unread badge clears once the
    /// user has looked. Styled to mirror the Changelog window (MaterialDesign
    /// cards + type badges).
    /// </summary>
    public partial class Inbox : Window
    {
        private readonly IOverlay _mainOverlay;

        // Change-type badge colours, matching the Changelog window.
        private static readonly SolidColorBrush BrushNew          = new(Color.FromRgb(76, 175, 80));   // green
        private static readonly SolidColorBrush BrushAnnouncement = new(Color.FromRgb(33, 150, 243));  // blue
        private static readonly SolidColorBrush BrushOther        = new(Color.FromRgb(158, 158, 158)); // grey

        public Inbox(IOverlay mainOverlay)
        {
            _mainOverlay = mainOverlay;
            InitializeComponent();
            Loaded += (_, __) => BuildList();
        }

        private void BuildList()
        {
            var messages = App.Messages?.GetCachedMessages() ?? new List<MessageEntry>();

            if (messages.Count == 0)
            {
                SubtitleText.Text = LocalizationManager.T("Inbox_Empty");
                EmptyPanel.Visibility = Visibility.Visible;
                ContentScroller.Visibility = Visibility.Collapsed;
                return;
            }

            SubtitleText.Text = LocalizationManager.Format(
                messages.Count == 1 ? "Inbox_CountOne" : "Inbox_CountMany", messages.Count);

            foreach (var msg in messages)
            {
                EntriesPanel.Children.Add(BuildCard(msg));

                // Opening the inbox counts as reading. Marking here (rather than
                // per-click) clears the unread badge once the user has looked.
                App.Messages?.MarkRead(msg.Id);
            }

            EmptyPanel.Visibility = Visibility.Collapsed;
            ContentScroller.Visibility = Visibility.Visible;
        }

        private UIElement BuildCard(MessageEntry msg)
        {
            var card = new MaterialDesignThemes.Wpf.Card
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(12)
            };

            var content = new StackPanel();

            // ── Header row: title + type badge + date ──────────────────────
            var headerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };

            var badge = new Border
            {
                Background = BadgeColor(msg.Type),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(0, 2, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = (msg.Type ?? "").ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            headerRow.Children.Add(badge);

            headerRow.Children.Add(new TextBlock
            {
                Text = msg.Title,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 380
            });

            content.Children.Add(headerRow);

            content.Children.Add(new TextBlock
            {
                Text = FormatDate(msg.PublishAt),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = (Brush)FindResource("MaterialDesignBodyLight")
            });

            // ── Body ───────────────────────────────────────────────────────
            content.Children.Add(new TextBlock
            {
                Text = msg.Body,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            });

            // ── Optional call-to-action (https only) ───────────────────────
            if (msg.Link is { } link
                && !string.IsNullOrWhiteSpace(link.Url)
                && Uri.TryCreate(link.Url, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps)
            {
                var button = new Button
                {
                    Content = string.IsNullOrWhiteSpace(link.Label) ? link.Url : link.Label,
                    Style = (Style)FindResource("MaterialDesignFlatButton"),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 6, 0, 0),
                    Tag = uri.AbsoluteUri
                };
                button.Click += Link_Click;
                content.Children.Add(button);
            }

            card.Content = content;
            return card;
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string url }) return;

            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Inbox link open failed: {ex}");
            }
        }

        private static SolidColorBrush BadgeColor(string? type) => (type ?? "").ToLowerInvariant() switch
        {
            "news"         => BrushNew,
            "announcement" => BrushAnnouncement,
            _              => BrushOther
        };

        private static string FormatDate(DateTime utc)
            => utc.ToLocalTime().ToString("d MMM yyyy");

        private void Click_Close(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object sender, System.ComponentModel.CancelEventArgs e)
            => _mainOverlay.OnMessagesClose();
    }
}
