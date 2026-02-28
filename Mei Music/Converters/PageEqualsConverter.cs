using System;
using System.Globalization;
using System.Windows.Data;
using Mei_Music.ViewModels;

namespace Mei_Music.Converters
{
    /// <summary>
    /// Returns true when the value equals the parameter; both are expected to be <see cref="RightPanelPage"/>.
    /// Used to bind sidebar RadioButton IsChecked to CurrentPage (one-way).
    /// </summary>
    public sealed class PageEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not RightPanelPage page || parameter is not RightPanelPage target)
                return false;
            return page == target;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
