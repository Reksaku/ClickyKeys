using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Drawing.Drawing2D;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace ClickyKeys
{
    public partial class GlassPanel : UserControl
    {
        private readonly OverlayForm _overlay;
        public readonly PanelEdit settingsPanel;

        // Animation parameters
        const double BASE_SCALE = 0.87;
        const double PEAK_SCALE = 0.97;
        const double RAMP_UP_LERP = 0.35;
        const double DECAY_LERP = 0.88;
        const double FLASH_DECAY = 0.86;

        double _flash = 0.0;
        double _scale = BASE_SCALE;
        double _target = BASE_SCALE;
        bool _rampingUp = false;

        readonly WinFormsTimer _anim = new WinFormsTimer { Interval = 1000 / 60 }; // ~60 FPS

        public GlassPanel(OverlayForm overlay)
        {
            InitializeComponent();

            _overlay = overlay;
            settingsPanel = new PanelEdit(this);

            // smooth painting
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);

            // start graphitic
            _anim.Tick += (_, __) =>
            {
                _flash = Math.Max(0.0, _flash * FLASH_DECAY);

                if (_rampingUp)
                {
                    _scale = Lerp(_scale, _target, RAMP_UP_LERP);
                    if (Math.Abs(_scale - _target) < 0.01)
                    {
                        _rampingUp = false;
                        _target = BASE_SCALE;
                    }
                }
                else
                {
                    _scale = Lerp(_scale, _target, 1.0 - DECAY_LERP);
                }

                if (Math.Abs(_scale - BASE_SCALE) < 0.01 && _flash < 0.03)
                {
                    _scale = BASE_SCALE;
                    _flash = 0.0;
                    _anim.Stop();
                }

                Invalidate();
            };

        }

        // ---------- PROPERTIES visible in Designer ----------
        [Category("Appearance"), Description("Corner radius (px)")]
        public int CornerRadius { get; set; } = 12;

        [Category("Appearance"), Description("Inner margin (px)")]
        public int Inset { get; set; } = 8;

        [Category("Appearance"), Description("Glass panel color (tint)")]
        public Color PanelColor { get; set; } = Color.White;

        [Category("Appearance"), Description("Key text color on the panel")]
        public Color KeyTextColor { get; set; } = Color.Black;

        [Category("Appearance"), Description("Value text color on the panel")]
        public Color ValueTextColor { get; set; } = Color.Black;

        [Category("Appearance"), Description("Panel opacity 0..255")]
        public int PanelOpacity { get; set; } = 180;

        [Category("Setup"), Description("Key assigned to the panel")]
        public string Key { get; set; } = "?";

        [Category("Setup"), Description("Panel value")]
        public int Value { get; set; } = 0;

        [Category("Setup"), Description("Panel ID")]
        public int ID { get; set; } = 0;

        // ---------- API callable from form  ----------
        [Browsable(false)]
        public bool IsAnimating => _anim.Enabled;

        public void TriggerFlash()
        {
            _flash = 1.0;
            _target = PEAK_SCALE;
            _rampingUp = true;
            _anim.Stop();
            _anim.Start();
            Invalidate();
        }

        // ---------- DRAWING ----------
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            lblKey.Text = Key;
            lblValue.Text = Value.ToString();

            lblKey.ForeColor = KeyTextColor;
            lblValue.ForeColor = ValueTextColor;

            var r = this.ClientRectangle;
            if (r.Width <= 0 || r.Height <= 0) return;

            // safe margin for glow
            r.Inflate(-Math.Max(0, Inset), -Math.Max(0, Inset));

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // scale transform around center
            float cx = r.Left + r.Width / 2f;
            float cy = r.Top + r.Height / 2f;
            var st = g.Save();
            g.TranslateTransform(cx, cy);
            g.ScaleTransform((float)_scale, (float)_scale);
            g.TranslateTransform(-cx, -cy);

            using var path = Rounded(r, CornerRadius);

            // shadow (slight offset)
            using (var shadow = new SolidBrush(Color.FromArgb((int)(PanelOpacity * 0.35 + 50 * _flash), 0, 0, 0)))
            {
                var sRect = r; sRect.Offset(3, 3);
                using var sPath = Rounded(sRect, CornerRadius);
                g.FillPath(shadow, sPath);
            }

            // glass fill
            int aFill = Math.Clamp(PanelOpacity + (int)(40 * _flash), 0, 255);
            using (var fill = new SolidBrush(Color.FromArgb(aFill, PanelColor)))
                g.FillPath(fill, path);

            // edge glow on “flash”
            if (_flash > 0)
            {
                using var glow = new Pen(Color.FromArgb((int)(120 * _flash), 255, 255, 255), 6);
                g.DrawPath(glow, path);
            }

            g.Restore(st);
        }

        // ---------- helpers ----------
        static double Lerp(double from, double to, double t) => from + (to - from) * Math.Clamp(t, 0, 1);

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            if (radius <= 0) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        private void GlassPanel_Click(object sender, EventArgs e)
        {
             
            
            panel.Visible = true;
            panel.Size = new Size(200, 120);
            settingsPanel.StartKeyDownThread();
            panel.Controls.Add(settingsPanel);
            _overlay.KeyPreview = true;
        }
        public void CloseEditPanel()
        {
            panel.Controls.Clear();
            panel.Visible = false;
        }

        public void OverridePanel(string description, InputType input, Keys key_code)
        {
            Key = description;
            _overlay.EditPanel(ID, description, input, key_code);
        }

        public void GlassPanelKeyDown(KeyEventArgs e)
        {
            settingsPanel.PanelKeyDown(e);
        }
    }
}
