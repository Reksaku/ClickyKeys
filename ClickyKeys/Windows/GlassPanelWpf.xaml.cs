using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;
using System.Windows.Data;
using ControlzEx.Standard;

namespace ClickyKeys
{
    public partial class GlassPanelWpf : UserControl, INotifyPropertyChanged
    {
        private readonly IOverlay _overlay;

        const double BASE_SCALE = 0.95;
        const double PEAK_SCALE = 1.1;
        const double RAMP_UP_LERP = 0.35;
        const double DECAY_LERP = 0.9;
        const double FLASH_DECAY = 0.9;

        public int _width = 200;
        public int _hight = 100;

        private double _flash = 0.0;
        private double _scale = BASE_SCALE;
        private double _target = BASE_SCALE;
        private bool _rampingUp = false;

        private readonly PanelsSettings newConfiguration = new();

        private readonly DispatcherTimer _anim = new()
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
        };

        public GlassPanelWpf(IOverlay overlay)
        {
            _overlay = overlay;
            InitializeComponent();
            DataContext = this;

            root.Height = PanelHeight;
            root.Width = PanelWidth;

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

                RaiseAnimBindings();
            };


        }

        // API
        public bool IsAnimating => _anim.IsEnabled;

        public void TriggerFlash()
        {
            if (!IsEditorOpen)
            {
                _flash = 1.0;
                _target = PEAK_SCALE;
                _rampingUp = true;
                _anim.Stop();
                _anim.Start();
                RaiseAnimBindings();
            }
        }

        public void CloseEditPanel()
        {
            DescriptionBox.Text = "";
            InputBtn.Content = "Input";
            IsEditorOpen = false;
            EditorBorder.Visibility = Visibility.Hidden;
        }


        // DependencyProperties
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(GlassPanelWpf),
                new PropertyMetadata(12.0));

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public static readonly DependencyProperty InsetProperty =
            DependencyProperty.Register(nameof(Inset), typeof(double), typeof(GlassPanelWpf),
                new PropertyMetadata(8.0, (d, e) =>
                {
                    ((GlassPanelWpf)d).OnPropertyChanged(nameof(InsetThickness));
                }));

        public double Inset
        {
            get => (double)GetValue(InsetProperty);
            set => SetValue(InsetProperty, value);
        }

        public Thickness InsetThickness => new(Math.Max(0, Inset));

        public static readonly DependencyProperty PanelColorProperty =
            DependencyProperty.Register(nameof(PanelColor), typeof(Color), typeof(GlassPanelWpf),
                new PropertyMetadata(Colors.White));

        public Color PanelColor
        {
            get => (Color)GetValue(PanelColorProperty);
            set => SetValue(PanelColorProperty, value);
        }

        public static readonly DependencyProperty KeyTextColorProperty =
            DependencyProperty.Register(nameof(KeyTextColor), typeof(Color), typeof(GlassPanelWpf),
                new PropertyMetadata(Colors.Black, OnBrushesChanged));

        public Color KeyTextColor
        {
            get => (Color)GetValue(KeyTextColorProperty);
            set => SetValue(KeyTextColorProperty, value);
        }

        public static readonly DependencyProperty ValueTextColorProperty =
            DependencyProperty.Register(nameof(ValueTextColor), typeof(Color), typeof(GlassPanelWpf),
                new PropertyMetadata(Colors.Black, OnBrushesChanged));

        public Color ValueTextColor
        {
            get => (Color)GetValue(ValueTextColorProperty);
            set => SetValue(ValueTextColorProperty, value);
        }

        public static readonly DependencyProperty PanelWidthProperty =
            DependencyProperty.Register(nameof(PanelWidth), typeof(int), typeof(GlassPanelWpf),
                new PropertyMetadata(200));

        public int PanelWidth
        {
            get => (int)GetValue(PanelWidthProperty);
            set => SetValue(PanelWidthProperty, value);
        }

        public static readonly DependencyProperty PanelHeightProperty =
            DependencyProperty.Register(nameof(PanelHeight), typeof(int), typeof(GlassPanelWpf),
                new PropertyMetadata(100));

        public int PanelHeight
        {
            get => (int)GetValue(PanelHeightProperty);
            set => SetValue(PanelHeightProperty, value);
        }

        private static void OnBrushesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (GlassPanelWpf)d;
            c.KeyTextBrush = new SolidColorBrush(c.KeyTextColor);
            c.ValueTextBrush = new SolidColorBrush(c.ValueTextColor);
            c.OnPropertyChanged(nameof(KeyTextBrush));
            c.OnPropertyChanged(nameof(ValueTextBrush));
        }

        public Brush KeyTextBrush { get; private set; } = new SolidColorBrush(Colors.Black);
        public Brush ValueTextBrush { get; private set; } = new SolidColorBrush(Colors.Black);

        public static readonly DependencyProperty PanelOpacityProperty =
            DependencyProperty.Register(nameof(PanelOpacity), typeof(int), typeof(GlassPanelWpf),
                new PropertyMetadata(80, (d, e) =>
                {
                    ((GlassPanelWpf)d).OnPropertyChanged(nameof(PanelFillOpacity));
                }));

        /// <summary>0..100</summary>
        public int PanelOpacity
        {
            get => (int)GetValue(PanelOpacityProperty);
            set => SetValue(PanelOpacityProperty, Math.Max(0, Math.Min(100, value)));
        }

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(GlassPanelWpf),
                new PropertyMetadata(""));

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(int), typeof(GlassPanelWpf),
                new PropertyMetadata(0));

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }


        public static readonly DependencyProperty TypeProperty =
            DependencyProperty.Register(nameof(Type), typeof(InputType), typeof(GlassPanelWpf),
                new PropertyMetadata(InputType.Key));

        public InputType Type
        {
            get => (InputType)GetValue(TypeProperty);
            set => SetValue(TypeProperty, value);
        }

        public static readonly DependencyProperty KeyFontProperty =
            DependencyProperty.Register(nameof(KeyFont), typeof(FontSettings), typeof(GlassPanelWpf),
            new PropertyMetadata());

        public FontSettings KeyFont
        {
            get => (FontSettings)GetValue(KeyFontProperty);
            set => SetValue(KeyFontProperty, value);
        }

        public static readonly DependencyProperty ValueFontProperty =
            DependencyProperty.Register(nameof(ValueFont), typeof(FontSettings), typeof(GlassPanelWpf),
                new PropertyMetadata());
            
        public FontSettings ValueFont
        {
            get => (FontSettings)GetValue(ValueFontProperty);
            set => SetValue(ValueFontProperty, value);
        }


        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.Register(nameof(Key), typeof(Key), typeof(GlassPanelWpf),
                new PropertyMetadata(Key.None));

        public Key Key
        {
            get => (Key)GetValue(KeyProperty);
            set => SetValue(KeyProperty, value);
        }

        public int ID { get; set; } = 0;

        public double Scale => _scale;
        public double PanelFillOpacity => Math.Max(0, Math.Min(1, PanelOpacity / 100.0 + 0.16 * _flash));
        public double ShadowOpacity => Math.Max(0, Math.Min(1, 0.35 + 0.2 * _flash));
        public double EdgeGlowOpacity => Math.Max(0, Math.Min(1, 0.47 * _flash));
        public double EdgeGlowThickness => 6.0 * _flash;

        private bool _isEditorOpen;
        public bool IsEditorOpen
        {
            get => _isEditorOpen;
            set { _isEditorOpen = value; OnPropertyChanged(nameof(IsEditorOpen)); }
        }

        public void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            EditorBorder.Height = MainRectangle.ActualHeight;
            EditorBorder.Width = MainRectangle.ActualWidth;
            OpenEditor();
        }

        public void OpenEditor()
        {
            EditorBorder.Visibility = Visibility.Visible;
            IsEditorOpen = true;
            TriggerFlash();
        }

        private void OnCloseEditor(object sender, RoutedEventArgs e) => CloseEditPanel();
        private void OnSaveEditor(object sender, RoutedEventArgs e) => SaveEditPanel();

        private void SaveEditPanel()
        {
            newConfiguration.Description = DescriptionBox.Text;
            newConfiguration.Index = ID;
            _overlay.SavePanelConfiguration(newConfiguration);
            CloseEditPanel();
        }

        private void OnInputKey(object sender, RoutedEventArgs e)
        {
            if (InputManager.Current.MostRecentInputDevice is not MouseDevice)
            {
                e.Handled = true;
                return;
            }
            InputKey(); 
        }

        private async void InputKey()
        {
            InputBtn.Content = "-";
            var input = await GlobalInputHook.Instance.CaptureNextInputAsync(
                keyFilter: k => k != Key.System,
                mouseFilter: b => true);

            
            if (input.IsKey)
            {
                newConfiguration.Input = InputType.Key;
                newConfiguration.KeyCode = input.Key;
                InputBtn.Content = $"{input.Key}";
            }
            else 
            {
                newConfiguration.Input = MapMouseButton(input.MouseButton);
                newConfiguration.KeyCode = Key.None;
                InputBtn.Content = $"{input.MouseButton}";
            }
            
        }
        static double Lerp(double from, double to, double t) => from + (to - from) * Math.Max(0, Math.Min(1, t));

        private void RaiseAnimBindings()
        {
            OnPropertyChanged(nameof(Scale));
            OnPropertyChanged(nameof(PanelFillOpacity));
            OnPropertyChanged(nameof(ShadowOpacity));
            OnPropertyChanged(nameof(EdgeGlowOpacity));
            OnPropertyChanged(nameof(EdgeGlowThickness));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private static InputType MapMouseButton(MouseButton btn) => btn switch
        {
            MouseButton.Left => InputType.MouseLeft,
            MouseButton.Right => InputType.MouseRight,
            MouseButton.Middle => InputType.MouseMiddle,
            MouseButton.XButton1 => InputType.MouseXButton1,
            MouseButton.XButton2 => InputType.MouseXButton2,
            _ => InputType.None
        };
    }
    
}
