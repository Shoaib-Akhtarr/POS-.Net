using System;
using System.Globalization;
using System.Windows.Data;

namespace POS.Converters
{
    public class CurrencyFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal amount)
            {
                string prefix = parameter as string ?? "Rs";
                string sign = amount < 0 ? "− " : ""; // Unicode minus U+2212
                decimal absoluteAmount = Math.Abs(amount);
                
                return $"{sign}{prefix} {absoluteAmount:N0}";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
