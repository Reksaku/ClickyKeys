namespace ClickyKeys
{
    /// <summary>
    /// Identifies which element in the main window should be highlighted
    /// by the tutorial spotlight for a given step. <c>None</c> means no
    /// spotlight is shown (intro / outro slides).
    /// </summary>
    public enum TutorialTarget
    {
        None,
        PanelGrid,
        SinglePanel,
        PanelEditor,
        PanelEditorConfirm,
        AppearanceButton,
        ResetButton,
        DisplayButton,
        StatsButton,
        MoreButton,
        SettingsButton,
    }

    /// <summary>
    /// An action the user must perform before a tutorial step can be advanced.
    /// The relevant code paths broadcast a matching
    /// <see cref="TutorialActionMessage"/>; <see cref="TutorialWindow"/> gates
    /// the Next button until the current step's gate is satisfied.
    /// <c>None</c> means the step is informational and can be advanced freely.
    /// </summary>
    public enum TutorialGate
    {
        None,
        PanelEditorOpened,
        PanelEditorClosed,
        AppearanceOpened,
        TransparentModeEntered,
        ResetPressed,
        ToolbarToggled,
        DisplayRevealed,
        MoreRevealed,
        StatsOpened,
        SettingsOpened,
        InfoOpened,
    }

    /// <summary>
    /// A single step in the tutorial sequence. All fields are data — the
    /// <see cref="TutorialWindow"/> reads them and drives its UI accordingly,
    /// so adding or reordering steps never touches window code.
    /// </summary>
    public sealed class TutorialStep
    {
        /// <summary>Heading shown above the body text. Leave empty to hide.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Main body copy for this step.</summary>
        public string Body { get; init; } = string.Empty;

        /// <summary>
        /// Which UI element to spotlight. <c>None</c> = no spotlight,
        /// card is centered on screen.
        /// </summary>
        public TutorialTarget Target { get; init; } = TutorialTarget.None;

        /// <summary>
        /// Optional short hint shown below the body in blue italic text,
        /// e.g. "👆 Click the highlighted panel to try it".
        /// Leave empty to hide the hint line.
        /// </summary>
        public string Hint { get; init; } = string.Empty;

        /// <summary>
        /// When <c>true</c> the floating card is pinned to the right side of
        /// the target element instead of being centered below it.
        /// Useful when the target is near the left edge.
        /// </summary>
        public bool CardOnRight { get; init; } = false;

        /// <summary>
        /// Optional action the user must perform before this step can be
        /// advanced. While unsatisfied the Next button is disabled; the gate is
        /// marked satisfied when a matching <see cref="TutorialActionMessage"/>
        /// arrives (including one fired earlier — completion is remembered).
        /// <c>None</c> = no gate.
        /// </summary>
        public TutorialGate RequiredAction { get; init; } = TutorialGate.None;
    }
}
