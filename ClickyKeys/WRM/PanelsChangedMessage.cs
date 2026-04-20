using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Collections.Generic;
using System.Windows.Input;

namespace ClickyKeys
{
    /// <summary>
    /// Broadcast when the main window has committed a change to the panel
    /// layout (via <c>SavePanelConfiguration</c>). Delivered to the
    /// transparent sub-window so its grid and counter bindings stay in sync
    /// without requiring it to re-read the JSON file from disk.
    ///
    /// The payload is a <see cref="PanelState"/> deep-cloned from the
    /// publisher so the two windows don't end up sharing mutable references
    /// into the same list of panel entries.
    /// </summary>
    public sealed class PanelsChangedMessage : ValueChangedMessage<PanelState>
    {
        public PanelsChangedMessage(PanelState value) : base(Clone(value)) { }

        private static PanelState Clone(PanelState source)
        {
            if (source == null) return new PanelState();

            var copy = new PanelState
            {
                Version = source.Version
            };

            if (source.Panels == null) return copy;

            var list = new List<PanelsSettings>(source.Panels.Count);
            foreach (var p in source.Panels)
            {
                list.Add(new PanelsSettings
                {
                    Index = p.Index,
                    KeyCode = p.KeyCode,
                    Input = p.Input,
                    Description = p.Description
                });
            }
            copy.Panels = list;
            return copy;
        }
    }
}
