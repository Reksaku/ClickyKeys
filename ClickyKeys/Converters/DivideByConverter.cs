using System;
using System.Globalization;
using System.Windows.Data;

namespace ClickyKeys.Converters
{
    public class DivideByConverter : IValueConverter
    {
        public double Divider { get; set; } = 1;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && Divider != 0)
                return d / Divider;
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}