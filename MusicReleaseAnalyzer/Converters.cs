using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace MusicReleaseAnalyzer
{
    // Code edited from: https://stackoverflow.com/a/345515. All rights reserved
    [ValueConversion(typeof(List<string>), typeof(string))]
    public class ListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(string)) throw new InvalidOperationException("The target must be a String");

            StringBuilder builder = new StringBuilder();
            foreach (var item in (List<string>)value)
            {
                builder.AppendLine(item);
            }

            if (builder.Length > 0) builder.Length -= 1;

            return builder.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Code edited from: https://stackoverflow.com/a/21863054. All rights reserved
    [ValueConversion(typeof(FrameworkElement), typeof(Visibility))]
    public class TrimmedTextBlockVisibilityConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;

            FrameworkElement textBlock = (FrameworkElement)value;

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            return ((FrameworkElement)value).ActualWidth < ((FrameworkElement)value).DesiredSize.Width
                ? Visibility.Visible
                : (object)Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
