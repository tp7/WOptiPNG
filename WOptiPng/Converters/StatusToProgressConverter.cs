using System;
using System.Windows.Data;
using System.Windows.Media;

namespace WOptiPng.Converters
{
    [ValueConversion(typeof(OptimizationProcessStatus), typeof(double))]
    public class StatusToProgressConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof (double))
                throw new InvalidOperationException("The target must be a int");


            switch ((OptimizationProcessStatus)value)
            {
                case OptimizationProcessStatus.InProgress:
                case OptimizationProcessStatus.NotStarted:
                    return 0;
                default:
                    return 100;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}