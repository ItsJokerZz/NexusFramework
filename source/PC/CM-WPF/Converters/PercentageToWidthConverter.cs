using System;
using System.Globalization;
using System.Windows.Data;

namespace ConsoleManager.Converters
{
    public class PercentageToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] is not double percentage || values[1] is not double totalWidth)
                return 0.0;

            return (percentage / 100.0) * totalWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 