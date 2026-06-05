using System.Windows;

namespace ClickyKeys.Windows
{
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Updates the status label text (call from the UI thread).
        /// </summary>
        public void SetStatus(string text)
        {
            StatusText.Text = text;
        }
    }
}
