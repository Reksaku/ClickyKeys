using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClickyKeys
{
    public partial class PanelEdit : UserControl
    {
        private readonly GlassPanel _panel;
        private bool waitingForKey = false;
        Keys selected_key = Keys.NoName;
        InputType input;

        private IKeyboardMouseEvents? _globalHook;
        private Thread? _hookThread;
        private readonly ManualResetEvent _started = new(false);
        private ApplicationContext _appCtx;

        public PanelEdit(GlassPanel panel)
        {
            InitializeComponent();

            _panel = panel;


            Invalidate();
        }
        public void StartKeyDownThread()
        {
            if (_globalHook != null || _hookThread != null) return;

            _hookThread = new Thread(() =>
            {
                try
                {
                    Application.SetHighDpiMode(HighDpiMode.SystemAware);
                    _globalHook = Hook.GlobalEvents();
                    _globalHook.MouseDownExt += OnMouseDown;

                    _appCtx = new ApplicationContext();
                    _started.Set();
                    Application.Run(_appCtx);
                }
                finally
                {
                    if (_globalHook != null)
                    {
                        _globalHook.MouseDownExt -= OnMouseDown;
                        _globalHook.Dispose();
                        _globalHook = null;
                    }
                }
            });

            _hookThread.IsBackground = true;
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.Start();

            _started.WaitOne(TimeSpan.FromSeconds(5));
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            _panel.CloseEditPanel();
        }

        private void btnInput_Click(object sender, EventArgs e)
        {
            btnInput.Text = "-";
            waitingForKey = true;
        }

        public void PanelKeyDown(KeyEventArgs e)
        {
            if (waitingForKey)
            {
                var nk = NormalizeKey(e.KeyCode);
                selected_key = nk;
                input = InputType.Key;
                btnInput.Text = $"{nk}";
                waitingForKey = false;
            }
        }

        private static Keys NormalizeKey(Keys k)
        {
            
            k &= Keys.KeyCode;

            return k switch
            {
                Keys.LShiftKey or Keys.RShiftKey or Keys.ShiftKey or Keys.Shift => Keys.ShiftKey,
                Keys.LControlKey or Keys.RControlKey or Keys.ControlKey or Keys.Control => Keys.ControlKey,
                Keys.LMenu or Keys.RMenu or Keys.Menu or Keys.Alt => Keys.Menu,
                _ => k
            };
        }

        private void OnMouseDown(object? sender, MouseEventExtArgs e)
        {
            if (waitingForKey)
            {
                if (e.Button == MouseButtons.Left)
                {
                    btnInput.UI(() => btnInput.Text = "LMB");
                    selected_key = Keys.None;
                    input = InputType.MouseLeft;
                }
                else if (e.Button == MouseButtons.Right)
                {
                    btnInput.UI(() => btnInput.Text = "RMB");
                    selected_key = Keys.None;
                    input = InputType.MouseRight;
                }
                waitingForKey = false;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (selected_key != Keys.NoName)
            {              
                _panel.OverridePanel(textDescription.Text, input, selected_key);
                _panel.CloseEditPanel();
            }            
            else
                _panel.CloseEditPanel();

            _appCtx?.ExitThread();
            if (_hookThread != null && _hookThread.IsAlive)
            {
                
                _hookThread.Join(1000);
            }
        }
    }
    public static class ControlExtensions
    {
        public static void UI(this Control c, Action action)
        {
            if (c.IsDisposed) return;
            if (c.InvokeRequired) c.BeginInvoke(action);
            else action();
        }
    }
}
