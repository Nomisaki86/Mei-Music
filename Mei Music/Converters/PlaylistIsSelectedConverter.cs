using System;
using System.Globalization;
using System.Windows.Data;
using Mei_Music.Models;
using Mei_Music.ViewModels;

namespace Mei_Music.Converters
{
    /// <summary>
    /// Multi-value converter: (CurrentPage, ActivePlaylist, ThisPlaylist) -> true when
    /// CurrentPage is PlaylistSongs or EditPlaylist and ActivePlaylist is this playlist (reference or Id match).
    /// Used to bind playlist sidebar RadioButton IsChecked from ViewModel state.
    /// </summary>
    public sealed class PlaylistIsSelectedConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3)
                return false;
            if (values[0] is not RightPanelPage page)
                return false;
            if (page != RightPanelPage.PlaylistSongs && page != RightPanelPage.EditPlaylist)
                return false;
            if (values[1] is not CreatedPlaylist active || values[2] is not CreatedPlaylist thisPlaylist)
                return false;
            return ReferenceEquals(active, thisPlaylist)
                || (!string.IsNullOrWhiteSpace(active.Id) && active.Id == thisPlaylist.Id);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
