using System;
using System.Windows.Data;

namespace WOptiPng.Converters
{
    [ValueConversion(typeof (double), typeof (string))]
    public class PercentToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof (string))
            {
                throw new InvalidOperationException("The target must be a string");
            }

            return value == null ? null : string.Format("{0:n1}%", value);
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}