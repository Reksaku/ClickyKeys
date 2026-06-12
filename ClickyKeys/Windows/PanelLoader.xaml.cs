using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClickyKeys
{
    /// <summary>
    /// Callbacks a panels-profile loader uses to apply its selection. Mirrors
    /// <see cref="AppearanceOverlay"/> but for the panels-layout profiles in
    /// <see cref="PanelsService.PanelsDirectory"/>. Implemented by MainWindow,
    /// where the live panel state and counter live.
    /// </summary>
    public interface PanelOverlay
    {
        /// <summary>Preview a profile: load it and apply it live.</summary>
        void LoadPanelsFile(string fullPath);

        /// <summary>Commit a profile as the active one (persists the choice).</summary>
        void SelectPanelsFile(string fullPath);

        /// <summary>Cancel: restore the previously committed active profile.</summary>
        void RevertPanelsFile();

        /// <summary>Save the current panel layout as a new named profile and
        /// make it active.</summary>
        void SavePanelsProfileAs(string name);

        /// <summary>Display name (no extension) of the active panels profile.</summary>
        string ActivePanelsProfileName { get; }
    }

    /// <summary>
    /// Profile picker for panel layouts — the panels-folder counterpart of
    /// <see cref="SetLoader"/>. Clicking a profile previews it live; "Load"
    /// commits it (so it stays active across restarts); "Exit" reverts to the
    /// previously active profile. Profiles can be deleted with the X button.
    /// </summary>
    public partial class PanelLoader : Window
    {
        private readonly PanelOverlay _panelOverlay;
        private string loadedFile = string.Empty;
        private bool loaded = false;

        private Button? _armedDeleteButton;
        private TextBlock? _armedConfirmLabel;
        private double _messageHeight = 0;

        public PanelLoader(PanelOverlay panelOverlay)
        {
            _panelOverlay = panelOverlay;

            InitializeComponent();

            PreviewMouseDown += Window_PreviewMouseDown;

            this.Height = 230;

            filesList.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            filesList.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            int row = 0;
            foreach (string currentFile in PanelsService.EnumerateProfilePaths())
            {
                string name = Path.GetFileNameWithoutExtension(currentFile);

                var btn = new Button { };
                var confirmLabel = CreateConfirmLabel();

                filesList.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                Grid.SetRow(confirmLabel, row);
                Grid.SetColumn(confirmLabel, 0);
                Grid.SetColumnSpan(confirmLabel, 2);
                filesList.Children.Add(confirmLabel);
                row++;

                filesList.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                AddFileButton(filesList, btn, row, 0, name, currentFile);
                AddDeleteButton(filesList, btn, confirmLabel, row, 1, currentFile);
                row++;

                this.Height += 50;
            }
            this.MaxHeight = this.Height;
        }

        private static TextBlock CreateConfirmLabel()
        {
            var label = new TextBlock
            {
                Visibility = Visibility.Collapsed,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D32F2F")),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(15, 4, 5, 0),
            };
            label.SetResourceReference(TextBlock.TextProperty, "SetLoader_ConfirmDelete");
            return label;
        }

        public void AddFileButton(Grid grid, Button btn, int row, int col, string text, string path)
        {
            btn.Content = text;
            btn.Width = 230;
            btn.Height = 40;
            btn.Margin = new Thickness(8, 5, 5, 5);
            btn.Style = (Style)FindResource("MaterialDesignOutlinedButton");
            btn.HorizontalContentAlignment = HorizontalAlignment.Center;
            btn.Click += (sender, e) => FileButtonClicked(path);

            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            grid.Children.Add(btn);
        }

        private void FileButtonClicked(string filePath)
        {
            loadedFile = filePath;
            _panelOverlay.LoadPanelsFile(filePath);
        }

        /// <summary>
        /// Borderless icon button matching GlassPanelWpf: the X/Y image is the
        /// button's Background; border/foreground are transparent.
        /// </summary>
        private static ImageBrush MakeIconBrush(string fileName) =>
            new ImageBrush(new BitmapImage(new Uri($"pack://application:,,,/Resources/{fileName}")))
            {
                Stretch = Stretch.Uniform
            };

        private void AddDeleteButton(Grid grid, Button fileButton, TextBlock confirmLabel, int row, int col, string fullPath)
        {
            var btn = new Button
            {
                Width = 30,
                Height = 30,
                Tag = "delete",
                Margin = new Thickness(5, 5, 0, 5),
                Background = MakeIconBrush("X_button.png"),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            btn.Click += (sender, e) => DeleteButtonClicked(fullPath, btn, fileButton, confirmLabel);

            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            grid.Children.Add(btn);
        }

        private void DeleteButtonClicked(string fullPath, Button btn, Button fileButton, TextBlock confirmLabel)
        {
            if (btn.Tag.ToString() == "delete")
            {
                btn.Background = MakeIconBrush("Y_button.png");
                btn.Tag = "confirm";
                confirmLabel.Visibility = Visibility.Visible;

                _armedDeleteButton = btn;
                _armedConfirmLabel = confirmLabel;

                GrowForMessage(confirmLabel);
            }
            else if (btn.Tag.ToString() == "confirm")
            {
                btn.Visibility = Visibility.Collapsed;
                fileButton.Visibility = Visibility.Collapsed;
                confirmLabel.Visibility = Visibility.Collapsed;

                _armedDeleteButton = null;
                _armedConfirmLabel = null;

                ShrinkAfterMessage();

                try { File.Delete(fullPath); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"PanelLoader delete failed: {ex}"); }
            }
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_armedDeleteButton == null)
                return;

            if (IsWithin(e.OriginalSource, _armedDeleteButton))
                return;

            ResetArmedDelete();
        }

        private void ResetArmedDelete()
        {
            if (_armedDeleteButton == null)
                return;

            _armedDeleteButton.Background = MakeIconBrush("X_button.png");
            _armedDeleteButton.Tag = "delete";

            if (_armedConfirmLabel != null)
                _armedConfirmLabel.Visibility = Visibility.Collapsed;

            ShrinkAfterMessage();

            _armedDeleteButton = null;
            _armedConfirmLabel = null;
        }

        private void GrowForMessage(FrameworkElement label)
        {
            ShrinkAfterMessage();

            double available = Math.Max(50, ActualWidth - 24);
            label.Measure(new Size(available, double.PositiveInfinity));
            _messageHeight = label.DesiredSize.Height;

            MaxHeight += _messageHeight;
            Height += _messageHeight;
        }

        private void ShrinkAfterMessage()
        {
            if (_messageHeight <= 0)
                return;

            Height -= _messageHeight;
            MaxHeight -= _messageHeight;
            _messageHeight = 0;
        }

        private static bool IsWithin(object? source, DependencyObject target)
        {
            DependencyObject? node = source as DependencyObject;
            while (node != null)
            {
                if (ReferenceEquals(node, target))
                    return true;

                DependencyObject? parent = null;
                if (node is Visual || node is System.Windows.Media.Media3D.Visual3D)
                    parent = VisualTreeHelper.GetParent(node);
                parent ??= LogicalTreeHelper.GetParent(node);
                node = parent;
            }
            return false;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Cancelled (closed without committing) → restore the previously
            // active profile.
            if (loaded == false)
                _panelOverlay.RevertPanelsFile();
        }

        private void Click_Load(object sender, EventArgs e)
        {
            // Nothing previewed yet → just close (keeps the current profile).
            if (!string.IsNullOrEmpty(loadedFile))
            {
                _panelOverlay.SelectPanelsFile(loadedFile);
                loaded = true;
            }
            this.Close();
        }

        private void Click_Exit(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
