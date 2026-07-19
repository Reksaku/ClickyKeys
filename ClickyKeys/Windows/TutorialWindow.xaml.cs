using CommunityToolkit.Mvvm.Messaging;
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

        // Actions the user has performed so far (verified via
        // TutorialActionMessage). A step whose RequiredAction is in this set is
        // considered satisfied — including actions done before that step was
        // reached, so the user is never asked to repeat something.
        private readonly HashSet<TutorialGate> _completed = new();

        private FrameworkElement? _panelGrid;
        private FrameworkElement? _singlePanel;
        private FrameworkElement? _appearanceButton;
        private FrameworkElement? _resetButton;
        private FrameworkElement? _displayButton;
        private FrameworkElement? _statsButton;
        private FrameworkElement? _moreButton;

        private readonly Window _ownerWindow;

        // ---------------------------------------------------------------
        // Default step sequence (English)
        // ---------------------------------------------------------------

        // Step copy is pulled from the active localized string dictionary via
        // LocalizationManager so the tutorial appears in the user's chosen
        // language. Only the per-step keys differ; the spotlight targets are
        // structural and stay in code. T() falls back to the key name (and the
        // English baseline merged in App.xaml) if a translation is missing.
        /// <param name="afterUpdate">
        /// When true the opening step uses the "app was updated, worth going
        /// through this again" copy instead of the first-run welcome, because
        /// the tutorial is being replayed for a user who already saw it
        /// (see <c>BuildInfo.ForceTutorialOnUpdate</c>).
        /// </param>
        public static IReadOnlyList<TutorialStep> DefaultSteps(
            string resetKeyLabel = "(F12)", string toggleKeyLabel = "(F11)",
            bool afterUpdate = false) => new List<TutorialStep>
        {
            new()
            {
                Title  = LocalizationManager.T(afterUpdate ? "Tutorial_UpdateIntro_Title" : "Tutorial_S1_Title"),
                Body   = LocalizationManager.T(afterUpdate ? "Tutorial_UpdateIntro_Body"  : "Tutorial_S1_Body"),
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
                RequiredAction = TutorialGate.PanelEditorOpened,
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
                RequiredAction = TutorialGate.PanelEditorClosed,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S6_Title"),
                Body   = LocalizationManager.T("Tutorial_S6_Body"),
                Target = TutorialTarget.AppearanceButton,
                Hint   = LocalizationManager.T("Tutorial_S6_Hint"),
                RequiredAction = TutorialGate.AppearanceOpened,
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
                Body   = LocalizationManager.Format("Tutorial_S9_Body", resetKeyLabel),
                Target = TutorialTarget.ResetButton,
                Hint   = LocalizationManager.T("Tutorial_S9_Hint"),
                RequiredAction = TutorialGate.ResetPressed,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S10_Title"),
                Body   = LocalizationManager.T("Tutorial_S10_Body"),
                Target = TutorialTarget.DisplayButton,
                Hint   = LocalizationManager.T("Tutorial_S10_Hint"),
                RequiredAction = TutorialGate.DisplayRevealed,
            },
            // Display sub-steps: hide toolbar (gated), transparent mode (gated),
            // and a descriptive step for Always on top / Click-through.
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S11_Title"),
                Body   = LocalizationManager.Format("Tutorial_S11_Body", toggleKeyLabel),
                Target = TutorialTarget.DisplayButton,
                Hint   = LocalizationManager.T("Tutorial_S11_Hint"),
                RequiredAction = TutorialGate.ToolbarToggled,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S12_Title"),
                Body   = LocalizationManager.T("Tutorial_S12_Body"),
                Target = TutorialTarget.DisplayButton,
                Hint   = LocalizationManager.T("Tutorial_S12_Hint"),
                RequiredAction = TutorialGate.TransparentModeEntered,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S13_Title"),
                Body   = LocalizationManager.T("Tutorial_S13_Body"),
                Target = TutorialTarget.DisplayButton,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S14_Title"),
                Body   = LocalizationManager.T("Tutorial_S14_Body"),
                Target = TutorialTarget.StatsButton,
                Hint   = LocalizationManager.T("Tutorial_S14_Hint"),
                RequiredAction = TutorialGate.StatsOpened,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S15_Title"),
                Body   = LocalizationManager.T("Tutorial_S15_Body"),
                Target = TutorialTarget.MoreButton,
                Hint   = LocalizationManager.T("Tutorial_S15_Hint"),
                RequiredAction = TutorialGate.MoreRevealed,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S16_Title"),
                Body   = LocalizationManager.T("Tutorial_S16_Body"),
                Target = TutorialTarget.SettingsButton,
                Hint   = LocalizationManager.T("Tutorial_S16_Hint"),
                RequiredAction = TutorialGate.SettingsOpened,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S17_Title"),
                Body   = LocalizationManager.T("Tutorial_S17_Body"),
                Target = TutorialTarget.MoreButton,
                Hint   = LocalizationManager.T("Tutorial_S17_Hint"),
                RequiredAction = TutorialGate.InfoOpened,
            },
            new()
            {
                Title  = LocalizationManager.T("Tutorial_S18_Title"),
                Body   = LocalizationManager.T("Tutorial_S18_Body"),
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

            // Listen for verified user actions so gated steps can unlock the
            // Next button. Unregister on close so the window can be collected.
            WeakReferenceMessenger.Default.Register<TutorialActionMessage>(
                this, (_, m) => OnTutorialAction(m.Value));
            Closed += (_, _) => WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        // "Transient" gates (revealing a hover dropdown) must be performed WHILE
        // the step that asks for them is on screen — otherwise every incidental
        // hover would pre-satisfy the step. They are never banked in _completed;
        // instead _transientSatisfied tracks the current step only and resets on
        // navigation.
        private bool _transientSatisfied = false;

        private static bool IsTransient(TutorialGate g) =>
            g == TutorialGate.DisplayRevealed || g == TutorialGate.MoreRevealed;

        // Records a verified action and, if it satisfies the current step's
        // gate, refreshes the Next button so the user can proceed.
        private void OnTutorialAction(TutorialGate action)
        {
            if (action == TutorialGate.None) return;

            var current = _steps[_currentIndex].RequiredAction;

            if (IsTransient(action))
            {
                // Only counts for the step currently on screen; not remembered.
                if (action == current)
                {
                    _transientSatisfied = true;
                    UpdateNextGate();
                }
                return;
            }

            _completed.Add(action);
            if (action == current)
                UpdateNextGate();
        }

        // Enables/disables Next based on whether the current step's gate (if any)
        // has been satisfied, and surfaces the matching cues: a green "done"
        // line once satisfied, or a "skip this step" escape hatch while not.
        private void UpdateNextGate()
        {
            var step = _steps[_currentIndex];
            bool gated = step.RequiredAction != TutorialGate.None;

            bool satisfied;
            if (!gated)
                satisfied = true;
            else if (IsTransient(step.RequiredAction))
                satisfied = _transientSatisfied;              // must happen on this step
            else
                satisfied = _completed.Contains(step.RequiredAction);

            NextBtn.IsEnabled = satisfied;
            GateDoneText.Visibility = (gated && satisfied) ? Visibility.Visible : Visibility.Collapsed;
            // Offer a per-step skip only while the action is still pending, so a
            // user who can't or won't perform it isn't stuck behind a disabled
            // Next (the whole-tutorial Skip stays available regardless).
            SkipStepBtn.Visibility = (gated && !satisfied) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Advances past a gated step without performing its action.
        private void SkipStep_Click(object sender, RoutedEventArgs e)
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
            FrameworkElement resetButton,
            FrameworkElement displayButton,
            FrameworkElement statsButton,
            FrameworkElement moreButton)
        {
            _panelGrid        = panelGrid;
            _singlePanel      = singlePanel;
            _appearanceButton = appearanceButton;
            _resetButton      = resetButton;
            _displayButton    = displayButton;
            _statsButton      = statsButton;
            _moreButton       = moreButton;
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

        /// <summary>
        /// Resource strings encode hard line breaks as the literal two-character
        /// token "\n", because XAML collapses real newlines and &#10; inside
        /// &lt;sys:String&gt; content into a single space. Convert that token
        /// into an actual newline so the tutorial card renders multi-line copy.
        /// </summary>
        private static string WithLineBreaks(string text) =>
            string.IsNullOrEmpty(text) ? text : text.Replace("\\n", "\n");

        private void RenderStep()
        {
            var step  = _steps[_currentIndex];
            bool last = _currentIndex == _steps.Count - 1;

            // Each step starts with any transient (hover) gate unsatisfied, so
            // the reveal must be performed while this step is shown.
            _transientSatisfied = false;

            StepIndicator.Text = LocalizationManager.Format("Tutorial_StepFormat", _currentIndex + 1, _steps.Count);

            if (string.IsNullOrEmpty(step.Title))
            {
                TitleText.Visibility = Visibility.Collapsed;
            }
            else
            {
                TitleText.Text = WithLineBreaks(step.Title);
                TitleText.Visibility = Visibility.Visible;
            }

            BodyText.Text = WithLineBreaks(step.Body);

            if (string.IsNullOrEmpty(step.Hint))
            {
                ArrowHint.Visibility = Visibility.Collapsed;
            }
            else
            {
                ArrowHint.Text = WithLineBreaks(step.Hint);
                ArrowHint.Visibility = Visibility.Visible;
            }

            ProgressBar.Maximum = _steps.Count - 1;
            ProgressBar.Value   = _currentIndex;

            BackBtn.Visibility = _currentIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
            NextBtn.Content    = last ? LocalizationManager.T("Tutorial_Finish") : LocalizationManager.T("Tutorial_Next");

            // Gate Next: if this step requires an action, Next stays disabled
            // until that action has been verified (Skip always remains available).
            UpdateNextGate();
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
            TutorialTarget.AppearanceButton   => _appearanceButton,
            TutorialTarget.ResetButton        => _resetButton,
            TutorialTarget.DisplayButton      => _displayButton,
            TutorialTarget.StatsButton        => _statsButton,
            // Info and Settings live inside the "More" hover popup, so the
            // spotlight points at the More tab — the user hovers it to reveal
            // the items the step describes.
            TutorialTarget.MoreButton         => _moreButton,
            TutorialTarget.SettingsButton     => _moreButton,
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
