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
    public partial class AddToPlaylistCard : UserControl
    {
        private sealed class PlaylistRowItem
        {
            public PlaylistRowItem(CreatedPlaylist playlist, bool containsCurrentSong)
            {
                Playlist = playlist;
                ContainsCurrentSong = containsCurrentSong;
            }

            public CreatedPlaylist Playlist { get; }
            public bool ContainsCurrentSong { get; }
        }

        public event EventHandler? CloseRequested;
        public event EventHandler? CreatePlaylistRequested;
        public event EventHandler<PlaylistSelectedEventArgs>? PlaylistSelected;
        public event EventHandler<DragMoveDeltaEventArgs>? DragMoveDelta;

        private IReadOnlyList<CreatedPlaylist> _playlists = Array.Empty<CreatedPlaylist>();
        private bool _isSyncingOverlayScrollBar;
        private Point _dragStart;

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
            string? currentSongName = currentSong?.Name;
            var rows = _playlists
                .Select(playlist => new PlaylistRowItem(playlist, PlaylistContainsSong(playlist, currentSongName)))
                .ToList();

            PlaylistItemsControl.ItemsSource = rows;
            NoPlaylistsText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            Dispatcher.BeginInvoke(SyncOverlayScrollBar, DispatcherPriority.Loaded);
        }

        private static bool PlaylistContainsSong(CreatedPlaylist playlist, string? songName)
        {
            if (string.IsNullOrWhiteSpace(songName) || playlist.SongNames == null)
            {
                return false;
            }

            return playlist.SongNames.Any(name =>
                string.Equals(name, songName, StringComparison.OrdinalIgnoreCase));
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

        private void HeaderDragBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            HeaderDragBorder.CaptureMouse();
        }

        private void HeaderDragBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (HeaderDragBorder.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                Point current = e.GetPosition(null);
                double dx = current.X - _dragStart.X;
                double dy = current.Y - _dragStart.Y;
                _dragStart = current;
                DragMoveDelta?.Invoke(this, new DragMoveDeltaEventArgs(dx, dy));
            }
        }

        private void HeaderDragBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (HeaderDragBorder.IsMouseCaptured)
            {
                HeaderDragBorder.ReleaseMouseCapture();
            }
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
