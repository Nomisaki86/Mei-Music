using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Mei_Music.Converters
{
    /// <summary>
    /// Converts a bool to a Visibility value (true => Visible, false => Collapsed).
    /// </summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

