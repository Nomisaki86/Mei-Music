using System;
using System.Globalization;
using System.Windows.Data;

namespace Mei_Music.Converters
{
    /// <summary>
    /// Converts a bool to its negation (true => false, false => true).
    /// </summary>
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }
}

