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
            if (value != null && decimal.TryParse(value.ToString(), out decimal dues))
            {
                // Positive TotalDues means customer owes money
                if (dues > 0)
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));

                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DuesLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && decimal.TryParse(value.ToString(), out decimal dues))
                return dues > 0m ? "RECEIVABLE · MAME LENA HAI" : dues < 0m ? "CUSTOMER CREDIT · JAMA" : "BALANCE SETTLED";
            return "KHATA BALANCE";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
