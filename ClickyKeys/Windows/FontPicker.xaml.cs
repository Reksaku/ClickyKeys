using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ClickyKeys.Controls
{
    public partial class FontPicker : UserControl
    {
        // Public DP
        public static readonly DependencyProperty AppearanceProperty =
            DependencyProperty.Register(
                nameof(AppearanceParameter),
                typeof(FontAppearance),
                typeof(FontPicker),
                new PropertyMetadata(null));

        public FontAppearance AppearanceParameter
        {
            get => (FontAppearance)GetValue(AppearanceProperty);
            set => SetValue(AppearanceProperty, value);
        }

        // Sorted fonts source
        public List<FontFamily> FontFamilies { get; } =
            Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();

        public FontPicker()
        {
            InitializeComponent();
        }
    }
}
