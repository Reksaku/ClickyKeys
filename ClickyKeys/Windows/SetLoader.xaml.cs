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
        private readonly SettingsOverlay _settingsOverlay;
        private string loadedFile = string.Empty;
        private bool loaded = false;
        public SetLoader(SettingsOverlay settingsOverlay)
        {
            _settingsOverlay = settingsOverlay;

            InitializeComponent();

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
                AddFileButton(filesList, btn, row, 0, name, currentFile);
                AddDeleteButton(filesList, btn, row, 1, name);
                this.Height += 50;
                row++;
            }
            this.MaxHeight = this.Height;
        }

        public void AddFileButton(Grid grid, Button btn, int row, int col, string text, string path)
        {
            
            btn.Content = text;
            btn.Width = 200;
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

            grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            grid.Children.Add(btn);
        }
        private void FileButtonClicked(string filePath)
        {
            loadedFile = filePath;
            _settingsOverlay.LoadSettingsFile(filePath);
        }
        private void AddDeleteButton(Grid grid, Button fileButton ,int row, int col, string fileName)
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
            btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE75858"));
            btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE75858"));
            
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

            btn.Click += (sender, e) => DeletaButtonClicked(fileName, btn, fileButton, symbolImage);

            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            grid.Children.Add(btn);

            Grid.SetRow(symbolImage, row);
            Grid.SetColumn(symbolImage, col);
            grid.Children.Add(symbolImage);
        }
        private void DeletaButtonClicked(string fileName, Button btn, Button fileButton, Image symbolImage)
        {
            if (btn.Tag.ToString() == "delete")
            {
                symbolImage.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/Y_button.png"));
                btn.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF58E759"));
                btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF58E759"));
                btn.Tag = "confirm";
            }
            else if (btn.Tag.ToString() == "confirm") 
            {
                btn.Visibility = Visibility.Collapsed;
                symbolImage.Visibility = Visibility.Collapsed;
                fileButton.Visibility = Visibility.Collapsed;

                string appName = "ClickyKeys\\settings";
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    appName, fileName+".json");
                File.Delete(appDataDir);
            }

        }

        void Window_Closed(object sender, EventArgs e)
        {
            if(loaded == false)
                _settingsOverlay.RevertSettingsFile();
        }
        private void Click_Load(object sender, EventArgs e)
        {
            _settingsOverlay.SelectSettingsFile(loadedFile);
            loaded = true;
            this.Close();
        }
        private void Click_Exit(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
