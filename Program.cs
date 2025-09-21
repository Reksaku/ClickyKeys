using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClickyKeys
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();            

            var settings = new SettingsService();
            var panels_settings = new PanelsService();

            var overlay = new OverlayForm(settings, panels_settings);

            // Windows tray icon
            using var tray = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true,
                Text = "ClickyKeys",
                ContextMenuStrip = new ContextMenuStrip()
            };

            // App maximization 
            tray.ContextMenuStrip!.Items.Add(new ToolStripMenuItem("Show", null, (_, __) =>
            {
                if (!overlay.Visible) overlay.Show();
                overlay.WindowState = FormWindowState.Normal;
                overlay.BringToFront();
            }));
            // App settings
            tray.ContextMenuStrip!.Items.Add(new ToolStripMenuItem("Settings", null, (_, __) => overlay.ShowSettings()));
            // Separation
            tray.ContextMenuStrip!.Items.Add(new ToolStripSeparator());
            // Close app
            tray.ContextMenuStrip!.Items.Add(new ToolStripMenuItem("Exit", null, (_, __) => Application.Exit()));

            // Remove app from tray on closure
            Application.ApplicationExit += (_, __) => { tray.Visible = false; overlay._counter.Dispose();};


            Application.Run(overlay);
        }
    }
}