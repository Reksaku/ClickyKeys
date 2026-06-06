using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace ClickyKeys
{

    public partial class SetLoader : Window
    {
        private readonly AppearanceOverlay _appearanceOverlay;
        private string loadedFile = string.Empty;
        private bool loaded = false;

        // The delete button currently "armed" (waiting for the green confirm
        // click) together with its icon and confirmation message. Null when no
        // deletion is pending. Clicking anywhere other than this exact button
        // cancels the pending deletion.
        private Button? _armedDeleteButton;
        private Image? _armedDeleteImage;
        private TextBlock? _armedConfirmLabel;

        // How many DIPs the window was enlarged by to fit the visible
        // confirmation message. 0 when no message is shown.
        private double _messageHeight = 0;

        public SetLoader(AppearanceOverlay appearanceOverlay)
        {
            _appearanceOverlay = appearanceOverlay;

            InitializeComponent();

            // Cancel a pending deletion as soon as the user clicks elsewhere.
            // PreviewMouseDown tunnels first, so it runs before the armed
            // button's own Click and can tell whether that button was hit.
            PreviewMouseDown += Window_PreviewMouseDown;

            string appName = "ClickyKeys\\settings";
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appName);

            var jsonFiles = Directory.EnumerateFiles(appDataDir, "*.json", SearchOption.AllDirectories);
            this.Height = 140;
            
            string list = "";
            int row = 0;

            filesList.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            filesList.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });

            foreach (string currentFile in jsonFiles)
            {
                string[] names = Regex.Split(currentFile, @"ClickyKeys\\settings\\");
                string name = Regex.Replace(names[1], @".json", "");
                list += name;

                var btn = new Button { };
                var confirmLabel = CreateConfirmLabel();

                // Row 1: the confirmation message, spanning both columns so it
                // appears above the profile without disturbing button alignment.
                filesList.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                Grid.SetRow(confirmLabel, row);
                Grid.SetColumn(confirmLabel, 0);
                Grid.SetColumnSpan(confirmLabel, 2);
                filesList.Children.Add(confirmLabel);
                row++;

                // Row 2: the profile button and its delete controls, side by side.
                filesList.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
                AddFileButton(filesList, btn, row, 0, name, currentFile);
                AddDeleteButton(filesList, btn, confirmLabel, row, 1, name);
                row++;

                this.Height += 50;
            }
            this.MaxHeight = this.Height;
        }

        /// <summary>
        /// Builds the red "confirm deletion" message shown above a profile
        /// while its delete button is armed. Hidden until the first X click.
        /// The text is bound to the localized resource so it follows the
        /// selected language.
        /// </summary>
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
            // DynamicResource-style binding so the message respects the
            // currently selected language.
            label.SetResourceReference(TextBlock.TextProperty, "SetLoader_ConfirmDelete");
            return label;
        }

        public void AddFileButton(Grid grid, Button btn, int row, int col, string text, string path)
        {

            btn.Content = text;
            btn.Width = 265;
            btn.Height = 40;
            Thickness myThickness = new Thickness();
            myThickness.Bottom = 5;
            myThickness.Left = 15;
            myThickness.Right = 5;
            myThickness.Top = 5;
            btn.Margin = myThickness;

            btn.Background = new SolidColorBrush( (Color)ColorConverter.ConvertFromString("#FFB4B4B4") );
            btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB4B4B4"));
            btn.Click += (sender, e) => FileButtonClicked(path);

            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            grid.Children.Add(btn);
        }
        private void FileButtonClicked(string filePath)
        {
            loadedFile = filePath;
            _appearanceOverlay.LoadAppearanceFile(filePath);
        }
        private void AddDeleteButton(Grid grid, Button fileButton, TextBlock confirmLabel, int row, int col, string fileName)
        {
            var btn = new Button { };
            btn.Width = 30;
            btn.Height = 30;
            btn.Content = "dell";
            btn.Tag = "delete";
            Thickness myThickness = new Thickness();
            myThickness.Bottom = 5;
            myThickness.Left = 5;
            myThickness.Right = 0;
            myThickness.Top = 5;
            btn.Margin = myThickness;
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FFFFFF"));
            btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F29C93"));

            var symbolImage = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/Resources/X_button.png")),
                Width = 25,
                Height = 25,
                Stretch = Stretch.Uniform,
                Opacity = 0.7,
                IsHitTestVisible = false,
                Margin = myThickness
            };

            btn.Click += (sender, e) => DeletaButtonClicked(fileName, btn, fileButton, symbolImage, confirmLabel);

            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            grid.Children.Add(btn);

            Grid.SetRow(symbolImage, row);
            Grid.SetColumn(symbolImage, col);
            grid.Children.Add(symbolImage);
        }
        private void DeletaButtonClicked(string fileName, Button btn, Button fileButton, Image symbolImage, TextBlock confirmLabel)
        {
            if (btn.Tag.ToString() == "delete")
            {
                symbolImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Y_button.png"));
                btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0B3DFBD"));
                btn.Tag = "confirm";

                // Show the "confirm deletion" message above the profile.
                confirmLabel.Visibility = Visibility.Visible;

                // Remember this as the pending deletion. The green confirm
                // click must come directly after, with nothing else clicked
                // in between.
                _armedDeleteButton = btn;
                _armedDeleteImage = symbolImage;
                _armedConfirmLabel = confirmLabel;

                // Make the window taller so the message isn't just scrolled.
                GrowForMessage(confirmLabel);
            }
            else if (btn.Tag.ToString() == "confirm")
            {
                btn.Visibility = Visibility.Collapsed;
                symbolImage.Visibility = Visibility.Collapsed;
                fileButton.Visibility = Visibility.Collapsed;

                // The profile is gone — remove its confirmation message too.
                confirmLabel.Visibility = Visibility.Collapsed;

                // Nothing is pending anymore.
                _armedDeleteButton = null;
                _armedDeleteImage = null;
                _armedConfirmLabel = null;

                // The message is gone, so give back the extra height.
                ShrinkAfterMessage();

                string appName = "ClickyKeys\\settings";
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    appName, fileName+".json");
                File.Delete(appDataDir);
            }

        }

        /// <summary>
        /// Cancels a pending deletion whenever the user clicks anything other
        /// than the armed (green) delete button itself. Letting the click on
        /// that button through allows the deletion to be confirmed.
        /// </summary>
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_armedDeleteButton == null)
                return;

            // Click on the armed button (or its visuals) → let it confirm.
            if (IsWithin(e.OriginalSource, _armedDeleteButton))
                return;

            // Clicked elsewhere on the panel → undo the arming.
            ResetArmedDelete();
        }

        /// <summary>
        /// Restores the armed delete button to its initial "X" state and hides
        /// its confirmation message, aborting the pending deletion.
        /// </summary>
        private void ResetArmedDelete()
        {
            if (_armedDeleteButton == null)
                return;

            if (_armedDeleteImage != null)
                _armedDeleteImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/X_button.png"));

            _armedDeleteButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F29C93"));
            _armedDeleteButton.Tag = "delete";

            if (_armedConfirmLabel != null)
                _armedConfirmLabel.Visibility = Visibility.Collapsed;

            // Give back the height that was added for the message.
            ShrinkAfterMessage();

            _armedDeleteButton = null;
            _armedDeleteImage = null;
            _armedConfirmLabel = null;
        }

        /// <summary>
        /// Enlarges the window vertically by the height the confirmation
        /// message needs, so it expands rather than scrolling. Any previously
        /// added height is released first.
        /// </summary>
        private void GrowForMessage(FrameworkElement label)
        {
            ShrinkAfterMessage();

            double available = Math.Max(50, ActualWidth - 24);
            label.Measure(new Size(available, double.PositiveInfinity));
            _messageHeight = label.DesiredSize.Height;

            MaxHeight += _messageHeight;
            Height += _messageHeight;
        }

        /// <summary>
        /// Removes the extra height previously added for the confirmation
        /// message, restoring the window to its prior size.
        /// </summary>
        private void ShrinkAfterMessage()
        {
            if (_messageHeight <= 0)
                return;

            Height -= _messageHeight;
            MaxHeight -= _messageHeight;
            _messageHeight = 0;
        }

        /// <summary>
        /// Walks up the visual/logical tree from <paramref name="source"/> to
        /// determine whether it lives inside <paramref name="target"/>.
        /// </summary>
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

        void Window_Closed(object sender, EventArgs e)
        {
            if(loaded == false)
                _appearanceOverlay.RevertAppearanceFile();
        }
        private void Click_Load(object sender, EventArgs e)
        {
            _appearanceOverlay.SelectAppearanceFile(loadedFile);
            loaded = true;
            this.Close();
        }
        private void Click_Exit(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
