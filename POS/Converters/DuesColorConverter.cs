using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POS.Converters
{
    public class DuesColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal dues)
            {
                // In this system, negative values represent debt (Dues)
                if (dues < 0)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D63031")); // Red
                
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00B894")); // green
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
