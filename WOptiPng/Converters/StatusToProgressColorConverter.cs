using System;
using System.Windows.Data;
using System.Windows.Media;

namespace WOptiPng.Converters
{
    [ValueConversion(typeof (OptimizationProcessStatus), typeof (Brush))]
    public class StatusToProgressColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof (Brush))
            {
                throw new InvalidOperationException("The target must be a brush");
            }

            switch ((OptimizationProcessStatus)value)
            {
                case OptimizationProcessStatus.DoneButSizeIsLarger:
                    return Brushes.LightYellow;
                case OptimizationProcessStatus.Error:
                    return Brushes.Crimson;
                default:
                    return Brushes.DeepSkyBlue;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}