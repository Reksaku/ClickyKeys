using CommunityToolkit.Mvvm.Messaging.Messages;

namespace ClickyKeys
{
    /// <summary>
    /// Broadcast whenever the user performs an action the tutorial can verify
    /// (opening the panel editor, saving a panel, opening Appearance, entering
    /// transparent mode, …). <see cref="TutorialWindow"/> listens for these to
    /// mark the current step's <see cref="TutorialGate"/> satisfied and unlock
    /// the Next button. Sent unconditionally by the relevant code paths — when
    /// no tutorial is open, nothing is listening, so it's a cheap no-op.
    /// </summary>
    public sealed class TutorialActionMessage : ValueChangedMessage<TutorialGate>
    {
        public TutorialActionMessage(TutorialGate action) : base(action) { }
    }
}
