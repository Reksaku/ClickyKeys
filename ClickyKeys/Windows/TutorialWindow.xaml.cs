using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ClickyKeys
{
    /// <summary>
    /// Floating tutorial card. Positions itself near the highlighted element
    /// using screen coordinates so the main window remains fully interactive
    /// throughout the tutorial — the user can practice on panels at any step.
    /// </summary>
    public partial class TutorialWindow : Window
    {
        // ---------------------------------------------------------------
        // Fields
        // ---------------------------------------------------------------

        private readonly IReadOnlyList<TutorialStep> _steps;
        private int _currentIndex = 0;

        private FrameworkElement? _panelGrid;
        private FrameworkElement? _singlePanel;
        private FrameworkElement? _appearanceButton;
        private FrameworkElement? _transparentButton;
        private FrameworkElement? _statsButton;
        private FrameworkElement? _infoButton;

        private readonly Window _ownerWindow;

        // ---------------------------------------------------------------
        // Default step sequence (English)
        // ---------------------------------------------------------------

        // Step copy is pulled from the active localized string dictionary via
        // LocalizationManager so the tutorial appears in the user's chosen
        // language. Only the per-step keys differ; the spotlight targets are
        // structural and stay in code. T() falls back to the key name (and the
        // English baseline merged in App.xaml) if a translation is missing.
        public static IReadOnlyList<TutorialStep> DefaultSteps() => new List<TutorialStep>
        {
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S1_Title"),
                Body   = LocalizationManager.T("Tutorial_S1_Body"),
                Target = TutorialTarget.PanelGrid,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S2_Title"),
                Body   = LocalizationManager.T("Tutorial_S2_Body"),
                Target = TutorialTarget.PanelGrid,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S3_Title"),
                Body   = LocalizationManager.T("Tutorial_S3_Body"),
                Target = TutorialTarget.SinglePanel,
                Hint   = LocalizationManager.T("Tutorial_S3_Hint"),
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S4_Title"),
                Body   = LocalizationManager.T("Tutorial_S4_Body"),
                Target = TutorialTarget.PanelEditor,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S5_Title"),
                Body   = LocalizationManager.T("Tutorial_S5_Body"),
                Target = TutorialTarget.PanelEditorConfirm,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S6_Title"),
                Body   = LocalizationManager.T("Tutorial_S6_Body"),
                Target = TutorialTarget.AppearanceButton,
                Hint   = LocalizationManager.T("Tutorial_S6_Hint"),
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S7_Title"),
                Body   = LocalizationManager.T("Tutorial_S7_Body"),
                Target = TutorialTarget.AppearanceButton,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S8_Title"),
                Body   = LocalizationManager.T("Tutorial_S8_Body"),
                Target = TutorialTarget.AppearanceButton,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S9_Title"),
                Body   = LocalizationManager.T("Tutorial_S9_Body"),
                Target = TutorialTarget.TransparentButton,
                Hint   = LocalizationManager.T("Tutorial_S9_Hint"),
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S10_Title"),
                Body   = LocalizationManager.T("Tutorial_S10_Body"),
                Target = TutorialTarget.StatsButton,
                Hint   = LocalizationManager.T("Tutorial_S10_Hint"),
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S11_Title"),
                Body   = LocalizationManager.T("Tutorial_S11_Body"),
                Target = TutorialTarget.InfoButton,
                Hint   = LocalizationManager.T("Tutorial_S11_Hint"),
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S12_Title"),
                Body   = LocalizationManager.T("Tutorial_S12_Body"),
                Target = TutorialTarget.PanelGrid,
            },
        };

        // ---------------------------------------------------------------
        // Constructor
        // ---------------------------------------------------------------

        public TutorialWindow(Window owner, IReadOnlyList<TutorialStep>? steps = null)
        {
            _ownerWindow = owner;
            _steps = steps ?? DefaultSteps();

            Owner = owner;
            InitializeComponent();

            Loaded += (_, _) => PositionAndRender();

            // Reposition when the owner moves or resizes.
            owner.LocationChanged += (_, _) => { if (IsLoaded) PositionAndRender(); };
            owner.SizeChanged     += (_, _) => { if (IsLoaded) PositionAndRender(); };
        }

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// Passes named UI elements from the owner window so the card can
        /// anchor itself next to each highlighted element. Call before Show().
        /// </summary>
        public void SetTargets(
            FrameworkElement panelGrid,
            FrameworkElement singlePanel,
            FrameworkElement appearanceButton,
            FrameworkElement transparentButton,
            FrameworkElement statsButton,
            FrameworkElement infoButton)
        {
            _panelGrid         = panelGrid;
            _singlePanel       = singlePanel;
            _appearanceButton    = appearanceButton;
            _transparentButton = transparentButton;
            _statsButton       = statsButton;
            _infoButton        = infoButton;
        }

        // ---------------------------------------------------------------
        // Button handlers
        // ---------------------------------------------------------------

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _steps.Count - 1)
            {
                _currentIndex++;
                PositionAndRender();
            }
            else
            {
                FinishTutorial();
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                PositionAndRender();
            }
        }

        private void Skip_Click(object sender, RoutedEventArgs e) => FinishTutorial();

        // ---------------------------------------------------------------
        // Rendering + positioning
        // ---------------------------------------------------------------

        private void PositionAndRender()
        {
            RenderStep();
            // Force a layout pass so ActualWidth/Height are updated before
            // we use them to compute the card position.
            UpdateLayout();
            PositionCard();
        }

        private void RenderStep()
        {
            var step  = _steps[_currentIndex];
            bool last = _currentIndex == _steps.Count - 1;

            StepIndicator.Text = LocalizationManager.Format("Tutorial_StepFormat", _currentIndex + 1, _steps.Count);

            if (string.IsNullOrEmpty(step.Title))
            {
                TitleText.Visibility = Visibility.Collapsed;
            }
            else
            {
                TitleText.Text = step.Title;
                TitleText.Visibility = Visibility.Visible;
            }

            BodyText.Text = step.Body;

            if (string.IsNullOrEmpty(step.Hint))
            {
                ArrowHint.Visibility = Visibility.Collapsed;
            }
            else
            {
                ArrowHint.Text = step.Hint;
                ArrowHint.Visibility = Visibility.Visible;
            }

            ProgressBar.Maximum = _steps.Count - 1;
            ProgressBar.Value   = _currentIndex;

            BackBtn.Visibility = _currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            NextBtn.Content    = last ? LocalizationManager.T("Tutorial_Finish") : LocalizationManager.T("Tutorial_Next");
        }

        private void PositionCard()
        {
            var target = ResolveTarget(_steps[_currentIndex].Target);

            // Screen bounds
            var screen     = SystemParameters.WorkArea;
            double cardW   = ActualWidth;
            double cardH   = ActualHeight;
            const double gap = 12;

            double left, top;

            if (target == null)
            {
                // No target — centre on screen.
                left = screen.Left + (screen.Width  - cardW) / 2;
                top  = screen.Top  + (screen.Height - cardH) / 2;
            }
            else
            {
                // Get the target rectangle in screen coordinates.
                var targetScreen = GetScreenRect(target);

                // Prefer placing the card below the element.
                left = targetScreen.X + (targetScreen.Width - cardW) / 2;
                top  = targetScreen.Bottom + gap;

                // Clamp horizontally within the work area.
                left = Math.Clamp(left,
                    screen.Left + 4,
                    screen.Right - cardW - 4);

                // Flip above the element if the card would be clipped at the bottom.
                if (top + cardH > screen.Bottom - 4)
                    top = targetScreen.Top - cardH - gap;

                top = Math.Clamp(top, screen.Top + 4, screen.Bottom - cardH - 4);
            }

            Left = left;
            Top  = top;
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private FrameworkElement? ResolveTarget(TutorialTarget t) => t switch
        {
            TutorialTarget.PanelGrid          => _panelGrid,
            TutorialTarget.SinglePanel        => _singlePanel,
            TutorialTarget.PanelEditor        => _singlePanel,
            TutorialTarget.PanelEditorConfirm => _singlePanel,
            TutorialTarget.AppearanceButton     => _appearanceButton,
            TutorialTarget.TransparentButton  => _transparentButton,
            TutorialTarget.StatsButton        => _statsButton,
            TutorialTarget.InfoButton         => _infoButton,
            _                                 => null,
        };

        /// <summary>
        /// Returns the element's bounding rectangle in WPF logical units (DIPs).
        /// <para>
        /// <see cref="UIElement.PointToScreen"/> returns physical pixels, while
        /// <see cref="Window.Left"/>/<see cref="Window.Top"/> and
        /// <see cref="SystemParameters.WorkArea"/> use logical pixels. On displays
        /// with DPI scaling (e.g. 4K at 150 %) these differ, so we divide the
        /// physical coordinates by the per-monitor DPI scale to get DIPs.
        /// </para>
        /// </summary>
        private static Rect GetScreenRect(FrameworkElement element)
        {
            try
            {
                var source = PresentationSource.FromVisual(element);
                double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                // PointToScreen → physical pixels
                var originPx = element.PointToScreen(new Point(0, 0));

                // Convert to logical pixels (DIPs)
                return new Rect(
                    originPx.X / scaleX,
                    originPx.Y / scaleY,
                    element.ActualWidth,   // already in DIPs
                    element.ActualHeight); // already in DIPs
            }
            catch
            {
                return new Rect(0, 0, 100, 40);
            }
        }

        private void FinishTutorial() => Close();
    }
}
