using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ICApp.Converters
{
    public class IntToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int b)
            {
                return b switch
                {
                    0 => Brushes.Green,
                    1 => Brushes.DarkGray,
                    _ => Brushes.Yellow
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
