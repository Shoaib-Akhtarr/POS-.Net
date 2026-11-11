using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace POS.Converters
{
    public class StockToBadgeBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && decimal.TryParse(value.ToString(), out decimal stock))
            {
                if (stock <= 0m) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                if (stock <= 10m) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECFDF5"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StockToBadgeForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && decimal.TryParse(value.ToString(), out decimal stock))
            {
                if (stock <= 0m) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B91C1C"));
                if (stock <= 10m) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B45309"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#047857"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StockToPillVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && decimal.TryParse(value.ToString(), out decimal stock))
            {
                return stock <= 5m ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class StockToDeleteVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && decimal.TryParse(value.ToString(), out decimal stock))
            {
                return stock <= 0m ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PriceToDashConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "—";
            if (decimal.TryParse(value.ToString(), out decimal price))
            {
                if (price == 0m) return "—";
                return $"Rs {price:N0}";
            }
            return "—";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class QuantityFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "0";
            if (decimal.TryParse(value.ToString(), out decimal qty))
            {
                return qty % 1m == 0 ? qty.ToString("N0") : qty.ToString("0.##");
            }
            return value.ToString() ?? "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class DepartmentBadgeBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string cat = (value?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            string hex = cat switch
            {
                "electric" => "#FEF3C7",
                "sanitary" => "#E0F2FE",
                "hardware" => "#F1F5F9",
                "tiles" => "#EDE9FE",
                "paint" => "#FCE7F3",
                "solar system" or "solar" => "#ECFDF5",
                "electronics" => "#DBEAFE",
                _ => "#F1F5F9"
            };
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class DepartmentBadgeForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string cat = (value?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            string hex = cat switch
            {
                "electric" => "#B45309",
                "sanitary" => "#0369A1",
                "hardware" => "#334155",
                "tiles" => "#6D28D9",
                "paint" => "#BE185D",
                "solar system" or "solar" => "#047857",
                "electronics" => "#1D4ED8",
                _ => "#475569"
            };
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
