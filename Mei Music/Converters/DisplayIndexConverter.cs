using System.Globalization;
using System.Windows.Data;

namespace Mei_Music.Converters
{
    /// <summary>
    /// Converts (item, ListBox) to a 1-based sequential display index string (e.g. "01", "02").
    /// Used so All, Liked, and any playlist list show numbers from 1 in display order.
    /// </summary>
    public class DisplayIndexConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return string.Empty;

            var item = values[0];
            if (item == null)
                return string.Empty;

            if (values[1] is not System.Windows.Controls.ListBox listBox)
                return string.Empty;

            var items = listBox.Items;
            if (items == null)
                return string.Empty;

            int index = items.IndexOf(item);
            if (index < 0)
                return string.Empty;

            return (index + 1).ToString("D2", culture);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
