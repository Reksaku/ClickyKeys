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

            // Snapshot the read set BEFORE marking anything read below, so
            // already-read messages render collapsed and unread ones expanded.
            var readIds = new HashSet<int>(App.Messages?.GetReadIds() ?? new List<int>());

            foreach (var msg in messages)
            {
                bool wasRead = readIds.Contains(msg.Id);
                EntriesPanel.Children.Add(BuildCard(msg, startCollapsed: wasRead));

                // Opening the inbox counts as reading. Marking here (rather than
                // per-click) clears the unread badge once the user has looked;
                // next time the window opens this message starts collapsed.
                App.Messages?.MarkRead(msg.Id);
            }

            EmptyPanel.Visibility = Visibility.Collapsed;
            ContentScroller.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Builds one message card. The header (type badge + title + chevron) is
        /// always visible and clickable; the body (date + text + optional link)
        /// collapses under it. <paramref name="startCollapsed"/> is true for
        /// messages the user had already read.
        /// </summary>
        private UIElement BuildCard(MessageEntry msg, bool startCollapsed)
        {
            var card = new MaterialDesignThemes.Wpf.Card
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(12)
            };

            var content = new StackPanel();

            // ── Header row: type badge + title + collapse chevron ──────────
            var headerRow = new DockPanel { LastChildFill = true };

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
            DockPanel.SetDock(badge, Dock.Left);
            headerRow.Children.Add(badge);

            var chevron = new TextBlock
            {
                Text = startCollapsed ? "▸" : "▾",   // ▸ collapsed / ▾ expanded
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("MaterialDesignBodyLight")
            };
            DockPanel.SetDock(chevron, Dock.Right);
            headerRow.Children.Add(chevron);

            // "Displayed" marker for messages the user has already seen. Added
            // before the title so it docks just left of the chevron.
            if (startCollapsed)
            {
                var readLabel = new TextBlock
                {
                    Text = LocalizationManager.T("Inbox_Read"),
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)FindResource("MaterialDesignBodyLight")
                };
                DockPanel.SetDock(readLabel, Dock.Right);
                headerRow.Children.Add(readLabel);
            }

            // Fills the remaining space between badge and chevron.
            headerRow.Children.Add(new TextBlock
            {
                Text = msg.Title,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            // Whole header is a hit-testable toggle.
            var headerBorder = new Border
            {
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = headerRow
            };
            content.Children.Add(headerBorder);

            // ── Collapsible body: date + text + optional link ──────────────
            var body = new StackPanel
            {
                Margin = new Thickness(0, 6, 0, 0),
                Visibility = startCollapsed ? Visibility.Collapsed : Visibility.Visible
            };

            body.Children.Add(new TextBlock
            {
                Text = FormatDate(msg.PublishAt),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 6),
                Foreground = (Brush)FindResource("MaterialDesignBodyLight")
            });

            body.Children.Add(new TextBlock
            {
                Text = msg.Body,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            });

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
                body.Children.Add(button);
            }

            // Delete button, bottom-right of the expanded body (so it only shows
            // once the message is expanded). Removes the message from the local
            // cache and drops the card from the list.
            var deleteButton = new Button
            {
                Content = LocalizationManager.T("Inbox_Delete"),
                Style = (Style)FindResource("MaterialDesignFlatButton"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)) // red
            };
            deleteButton.Click += (_, __) =>
            {
                App.Messages?.Delete(msg.Id);
                EntriesPanel.Children.Remove(card);
                UpdateAfterDelete();
            };
            body.Children.Add(deleteButton);

            content.Children.Add(body);

            headerBorder.MouseLeftButtonUp += (_, __) =>
            {
                bool show = body.Visibility != Visibility.Visible;
                body.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                chevron.Text = show ? "▾" : "▸";
            };

            card.Content = content;
            return card;
        }

        /// <summary>
        /// Refreshes the subtitle count / empty state after a card is deleted.
        /// </summary>
        private void UpdateAfterDelete()
        {
            int remaining = EntriesPanel.Children.Count;

            if (remaining == 0)
            {
                SubtitleText.Text = LocalizationManager.T("Inbox_Empty");
                EmptyPanel.Visibility = Visibility.Visible;
                ContentScroller.Visibility = Visibility.Collapsed;
            }
            else
            {
                SubtitleText.Text = LocalizationManager.Format(
                    remaining == 1 ? "Inbox_CountOne" : "Inbox_CountMany", remaining);
            }
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
