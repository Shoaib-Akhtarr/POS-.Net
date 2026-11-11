using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS.Converters
{
    public class CurrencyFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string prefix = parameter as string ?? "Rs";
            if (value == null || value == DependencyProperty.UnsetValue)
            {
                return $"{prefix} 0";
            }

            if (decimal.TryParse(value.ToString(), out decimal amount))
            {
                string sign = amount < 0 ? "− " : "";
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
