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
            textBoxVersion.Text = $"ClickyKeys · v{ new Configuration().Version}";
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private const string ContactEmail = "contact@clickykeys.fun";

        // Copies the contact address to the clipboard and briefly swaps the
        // button label to a "copied" confirmation so the user gets feedback
        // without a modal popup. The label reverts after a short delay.
        private void Click_CopyEmail(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ContactEmail);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Click_CopyEmail: clipboard set failed: {ex}");
                return;
            }

            object original = CopyEmailButton.Content;
            CopyEmailButton.Content = TryFindResource("Info_EmailCopied") ?? "Copied!";

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.5)
            };
            timer.Tick += (_, __) =>
            {
                timer.Stop();
                CopyEmailButton.Content = original;
            };
            timer.Start();
        }

        private void Click_Close(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Click_ShowTutorial(object sender, RoutedEventArgs e)
        {
            // Close Info first (triggers OnInfoClose via Window_Closed),
            // then launch the tutorial through the overlay interface.
            Close();
            _mainOverlay.ShowTutorial();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            _mainOverlay.OnInfoClose();
        }
    }
}
