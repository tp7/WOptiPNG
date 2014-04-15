using System;
using System.Windows.Data;
using System.Windows.Media;

namespace WOptiPNG.Converters
{
    [ValueConversion(typeof (OptimizationProcessStatus), typeof (bool))]
    public class StatusToProgressIntermediateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof (bool))
            {
                throw new InvalidOperationException("The target must be a boolean");
            }

            return (OptimizationProcessStatus)value == OptimizationProcessStatus.InProgress;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}