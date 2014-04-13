using System;
using System.Windows.Data;

namespace WOptiPng.Converters
{
    [ValueConversion(typeof (long), typeof (string))]
    public class SizeHumanizerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof (string))
            {
                throw new InvalidOperationException("The target must be a string");
            }

            return value == null ? null : string.Format(culture, "{0:###,###,###.##} KB", ((long)value)/1024.0f);
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}