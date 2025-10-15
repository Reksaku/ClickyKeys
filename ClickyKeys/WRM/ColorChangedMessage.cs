using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ClickyKeys
{
    public sealed class ColorChangedMessage : ValueChangedMessage<Color?>
    {
        public ColorTarget Target { get; }
        public ColorChangedMessage(Color? value, ColorTarget target) : base(value)
            => Target = target;
    }

}
