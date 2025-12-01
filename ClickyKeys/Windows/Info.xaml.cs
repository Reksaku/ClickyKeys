using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ClickyKeys
{

    public partial class Info : Window
    {

        private readonly IOverlay _mainOverlay;

        public Info(IOverlay mainOverlay)
        {
            _mainOverlay = mainOverlay;

            InitializeComponent();
            textBoxVersion.Text = $"ClickyKeys · hobby project · v{ new Configuration().Version}";
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void Click_Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _mainOverlay.OnInfoClose();
        }
    }
}
