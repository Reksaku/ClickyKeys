using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace ClickyKeys
{
    public partial class ColorPickerViewModel : ObservableObject
    {
        private readonly IMessenger _bus;
        public ColorPickerViewModel(IMessenger bus = null) => _bus = bus ?? WeakReferenceMessenger.Default;

        [ObservableProperty] private Color? _backgroundColor;
        [ObservableProperty] private Color? _panelsColor;

        partial void OnBackgroundColorChanged(Color? value)
            => _bus.Send(new ColorChangedMessage(value, ColorTarget.Background));

        partial void OnPanelsColorChanged(Color? value)
            => _bus.Send(new ColorChangedMessage(value, ColorTarget.Panels));
    }
}
