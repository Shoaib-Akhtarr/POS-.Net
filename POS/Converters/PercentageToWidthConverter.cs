using System;
using System.Globalization;
using System.Windows.Data;

namespace POS.Converters
{
    public sealed class PercentageToWidthConverter : IValueConverter
    {
        public double MaxWidth { get; set; } = 200d;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return 0d;
            if (double.TryParse(value.ToString(), NumberStyles.Any, culture, out double percent))
            {
                percent = Math.Clamp(percent, 0d, 100d);
                return (percent / 100d) * MaxWidth;
            }
            return 0d;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}