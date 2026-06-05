using System;
using System.Windows;

namespace ClickyKeys
{
    /// <summary>
    /// Program settings view. Mirrors the lifecycle/ownership pattern of
    /// <see cref="Info"/> and <see cref="Stats"/>: created by
    /// <see cref="MainWindow"/> with itself as the <see cref="IOverlay"/>,
    /// shown non-modally, and signals back via
    /// <see cref="IOverlay.OnSettingsClose"/> on close.
    ///
    /// Currently a placeholder — no options are wired up yet. Configuration
    /// controls will be added to Settings.xaml over time.
    /// </summary>
    public partial class Settings : Window
    {
        private readonly IOverlay _mainOverlay;

        public Settings(IOverlay mainOverlay)
        {
            _mainOverlay = mainOverlay;
            InitializeComponent();
        }

        private void Click_Close(object sender, RoutedEventArgs e) => Close();

        private void Window_Closed(object? sender, EventArgs e) =>
            _mainOverlay.OnSettingsClose();
    }
}
