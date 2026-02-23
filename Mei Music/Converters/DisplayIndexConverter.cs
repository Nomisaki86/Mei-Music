using System.Globalization;
using System.Windows.Data;
using Mei_Music.Models;

namespace Mei_Music.Converters
{
    /// <summary>
    /// Converts (item, ListBox) to a 1-based sequential display index string (e.g. "01", "02").
    /// Used so All, Liked, and any playlist list show numbers from 1 in display order.
    /// </summary>
    public class DisplayIndexConverter : IMultiValueConverter
    {
        /// <summary>
        /// Converts a bound item plus owning ListBox into a zero-padded 1-based index string.
        /// Returns empty string when required inputs are missing.
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return string.Empty;

            if (values[0] is not Song targetSong)
                return string.Empty;

            if (values[1] is not System.Windows.Controls.ListBox listBox)
                return string.Empty;

            var items = listBox.Items;
            if (items == null)
                return string.Empty;

            int songIndex = 0;
            foreach (var entry in items)
            {
                if (entry is not Song song)
                    continue;

                songIndex++;
                if (ReferenceEquals(song, targetSong) || song.Name == targetSong.Name)
                    return songIndex.ToString("D2", culture);
            }

            return string.Empty;
        }

        /// <summary>
        /// Not supported because list indices are display-only.
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
