using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace POS.Converters
{
    // Returns Background Brush for the Pill
    public class StockToBadgeBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int stock)
            {
                if (stock <= 0) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCEBEB")); // Red bg
                if (stock <= 5) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF9DB")); // Amber bg
            }
            return Brushes.Transparent;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Returns Foreground Brush for the Pill
    public class StockToBadgeForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int stock)
            {
                if (stock <= 0) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A32D2D")); // Red text
                if (stock <= 5) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B7791F")); // Amber text
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111827")); // Dark text
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Returns Visibility for the Pill (Hidden if healthy)
    public class StockToPillVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int stock)
            {
                return stock <= 5 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Returns Visibility for the Delete Button (Visible only if <= 0)
    public class StockToDeleteVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int stock)
            {
                return stock <= 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Returns em-dash for 0 prices
    public class PriceToDashConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal price && price == 0) return "—";
            if (value == null) return "—";
            return $"Rs {value:N0}";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
