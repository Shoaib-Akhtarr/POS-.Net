using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS.Converters
{
    public class DecimalPositiveToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && decimal.TryParse(value.ToString(), out decimal amount))
            {
                bool invert = string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase);
                bool visible = invert ? amount <= 0m : amount > 0m;
                return visible ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool hasText = !string.IsNullOrWhiteSpace(value?.ToString());
            bool invert = string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase);
            return (invert ? !hasText : hasText) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNotNull = value != null;
            bool invert = string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase);
            return (invert ? !isNotNull : isNotNull) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class EqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool matches = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);
            return matches ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}

