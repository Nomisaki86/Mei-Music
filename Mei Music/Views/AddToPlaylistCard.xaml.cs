using Mei_Music.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

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
    public partial class AddToPlaylistCard : DraggableOverlayCardBase
    {
        private sealed class PlaylistRowItem
        {
            public PlaylistRowItem(CreatedPlaylist playlist, bool containsCurrentSong)
            {
                Playlist = playlist;
                ContainsCurrentSong = containsCurrentSong;
                SongCount = GetSongCount(playlist);
            }

            public CreatedPlaylist Playlist { get; }
            public bool ContainsCurrentSong { get; }
            public int SongCount { get; }

            private static int GetSongCount(CreatedPlaylist playlist)
            {
                if (playlist == null)
                {
                    return 0;
                }

                // Prefer the current identifier-based membership list.
                if (playlist.SongIds != null && playlist.SongIds.Count > 0)
                {
                    return playlist.SongIds.Count;
                }

                // Fallback for legacy playlist files that still populate SongNames.
                return playlist.LegacySongNames?.Count ?? 0;
            }
        }

        public event EventHandler? CloseRequested;
        public event EventHandler? CreatePlaylistRequested;
        public event EventHandler<PlaylistSelectedEventArgs>? PlaylistSelected;

        private IReadOnlyList<CreatedPlaylist> _playlists = Array.Empty<CreatedPlaylist>();
        private bool _isSyncingOverlayScrollBar;

        public AddToPlaylistCard()
        {
            InitializeComponent();
            Loaded += AddToPlaylistCard_Loaded;
        }

        /// <summary>
        /// Loads playlists and refreshes the card list.
        /// Preserves the incoming playlist order and marks rows that already contain the provided song.
        /// </summary>
        public void LoadPlaylists(IEnumerable<CreatedPlaylist> playlists, Song? currentSong)
        {
            _playlists = playlists.ToList();
            string? currentSongId = currentSong?.Id;
            var rows = _playlists
                .Select(playlist => new PlaylistRowItem(playlist, PlaylistContainsSong(playlist, currentSongId)))
                .ToList();

            PlaylistItemsControl.ItemsSource = rows;
            NoPlaylistsText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            Dispatcher.BeginInvoke(SyncOverlayScrollBar, DispatcherPriority.Loaded);
        }

        private static bool PlaylistContainsSong(CreatedPlaylist playlist, string? songId)
        {
            if (string.IsNullOrWhiteSpace(songId) || playlist.SongIds == null)
            {
                return false;
            }

            return playlist.SongIds.Any(id =>
                string.Equals(id, songId, StringComparison.Ordinal));
        }

        private void AddToPlaylistCard_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(SyncOverlayScrollBar, DispatcherPriority.Loaded);
        }

        private void CardScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            SyncOverlayScrollBar();
        }

        private void SyncOverlayScrollBar()
        {
            if (CardOverlayScrollBar == null)
            {
                return;
            }

            _isSyncingOverlayScrollBar = true;
            try
            {
                if (CardScrollViewer == null)
                {
                    CardOverlayScrollBar.Visibility = Visibility.Collapsed;
                    CardOverlayScrollBar.Maximum = 0;
                    CardOverlayScrollBar.ViewportSize = 0;
                    CardOverlayScrollBar.Value = 0;
                    return;
                }

                double max = Math.Max(0, CardScrollViewer.ScrollableHeight);
                CardOverlayScrollBar.Visibility = max > 0 ? Visibility.Visible : Visibility.Collapsed;
                CardOverlayScrollBar.Maximum = max;
                CardOverlayScrollBar.ViewportSize = Math.Max(0, CardScrollViewer.ViewportHeight);
                CardOverlayScrollBar.LargeChange = Math.Max(1, CardScrollViewer.ViewportHeight * 0.9);
                CardOverlayScrollBar.Value = Math.Clamp(CardScrollViewer.VerticalOffset, 0, max);
            }
            finally
            {
                _isSyncingOverlayScrollBar = false;
            }
        }

        private void CardOverlayScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingOverlayScrollBar)
            {
                return;
            }

            CardScrollViewer?.ScrollToVerticalOffset(e.NewValue);
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

    }
}
