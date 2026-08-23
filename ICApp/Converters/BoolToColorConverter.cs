using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ICApp.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return b
                    ? new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47))  // green
                    : new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD));  // gray
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
