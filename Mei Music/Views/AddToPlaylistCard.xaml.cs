using Mei_Music.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Mei_Music
{
    /// <summary>
    /// Event payload for selecting a target playlist from the add-to-playlist card.
    /// </summary>
    public sealed class PlaylistSelectedEventArgs : EventArgs
    {
        public PlaylistSelectedEventArgs(CreatedPlaylist playlist)
        {
            Playlist = playlist;
        }

        public CreatedPlaylist Playlist { get; }
    }

    /// <summary>
    /// Overlay card that lets the user pick a playlist or create a new playlist.
    /// </summary>
    public partial class AddToPlaylistCard : UserControl
    {
        public event EventHandler? CloseRequested;
        public event EventHandler? CreatePlaylistRequested;
        public event EventHandler<PlaylistSelectedEventArgs>? PlaylistSelected;

        private static readonly Brush ActiveSortBrush = new SolidColorBrush(Color.FromRgb(74, 83, 101));
        private static readonly Brush InactiveSortBrush = new SolidColorBrush(Color.FromRgb(160, 167, 181));
        private IReadOnlyList<CreatedPlaylist> _playlists = Array.Empty<CreatedPlaylist>();
        private bool _sortByMostSongs;

        static AddToPlaylistCard()
        {
            if (ActiveSortBrush.CanFreeze)
            {
                ActiveSortBrush.Freeze();
            }

            if (InactiveSortBrush.CanFreeze)
            {
                InactiveSortBrush.Freeze();
            }
        }

        public AddToPlaylistCard()
        {
            InitializeComponent();
            SetSortMode(sortByMostSongs: false);
        }

        /// <summary>
        /// Loads playlists and refreshes the card list with default sorting.
        /// </summary>
        public void LoadPlaylists(IEnumerable<CreatedPlaylist> playlists)
        {
            _playlists = playlists.ToList();
            SetSortMode(sortByMostSongs: false);
        }

        private static int SongCount(CreatedPlaylist playlist)
        {
            return playlist.SongNames?.Count ?? 0;
        }

        private void SetSortMode(bool sortByMostSongs)
        {
            _sortByMostSongs = sortByMostSongs;

            IEnumerable<CreatedPlaylist> ordered = _sortByMostSongs
                ? _playlists
                    .OrderByDescending(SongCount)
                    .ThenBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase)
                : _playlists
                    .OrderBy(p => p.Title, StringComparer.CurrentCultureIgnoreCase);

            List<CreatedPlaylist> orderedList = ordered.ToList();
            PlaylistItemsControl.ItemsSource = orderedList;
            NoPlaylistsText.Visibility = orderedList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            DefaultSortText.FontWeight = _sortByMostSongs ? FontWeights.Normal : FontWeights.SemiBold;
            DefaultSortText.Foreground = _sortByMostSongs ? InactiveSortBrush : ActiveSortBrush;
            MostSongsSortText.FontWeight = _sortByMostSongs ? FontWeights.SemiBold : FontWeights.Normal;
            MostSongsSortText.Foreground = _sortByMostSongs ? ActiveSortBrush : InactiveSortBrush;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CreatePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            CreatePlaylistRequested?.Invoke(this, EventArgs.Empty);
        }

        private void PlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: CreatedPlaylist playlist })
            {
                PlaylistSelected?.Invoke(this, new PlaylistSelectedEventArgs(playlist));
            }
        }

        private void DefaultSortButton_Click(object sender, RoutedEventArgs e)
        {
            SetSortMode(sortByMostSongs: false);
        }

        private void MostSongsSortButton_Click(object sender, RoutedEventArgs e)
        {
            SetSortMode(sortByMostSongs: true);
        }
    }
}
