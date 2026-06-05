using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ClickyKeys.Windows
{
    public partial class AlreadyRunningToast : Window
    {
        public AlreadyRunningToast()
        {
            InitializeComponent();
            PositionBottomRight();

            // Auto-close after 3 s with a fade-out.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                FadeOutAndClose();
            };
            timer.Start();
        }

        private void PositionBottomRight()
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 16;
            Top  = area.Bottom - Height - 16;
        }

        private void FadeOutAndClose()
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(300));
            fade.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, fade);
        }
    }
}
