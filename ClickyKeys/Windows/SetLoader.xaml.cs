using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>
    /// Logika interakcji dla klasy SetLoader.xaml
    /// </summary>
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
            foreach (string currentFile in jsonFiles)
            {
                string[] names = Regex.Split(currentFile, @"ClickyKeys\\settings\\");
                string name = Regex.Replace(names[1], @".json", " ");
                list += name;

                AddButton(filesList, row, 0, name, currentFile);
                this.Height += 50;
                row++;
            }
            this.MaxHeight = this.Height;
        }

        public void AddButton(Grid grid, int row, int col, string text, string path)
        {
            var btn = new Button { Content = text};
            btn.Width = 200;
            btn.Height = 40;
            Thickness myThickness = new Thickness();
            myThickness.Bottom = 5;
            myThickness.Left = 0;
            myThickness.Right = 0;
            myThickness.Top = 5;
            btn.Margin = myThickness;

            btn.Background = new SolidColorBrush( (Color)ColorConverter.ConvertFromString("#FFB4B4B4") );
            btn.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB4B4B4"));
            btn.Click += (sender, e) => ButtonClicked(path);

            grid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            Grid.SetRow(btn, row);
            Grid.SetColumn(btn, col);
            grid.Children.Add(btn);
        }
        private void ButtonClicked(string filePath)
        {
            loadedFile = filePath;
            _settingsOverlay.LoadSettingsFile(filePath);
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
