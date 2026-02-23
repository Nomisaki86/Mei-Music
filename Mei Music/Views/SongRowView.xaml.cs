using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Mei_Music.Models;

namespace Mei_Music
{
    /// <summary>
    /// Event payload for opening a song-row options menu.
    /// Includes song model and optional UI anchor for popup placement.
    /// </summary>
    public sealed class SongOptionsRequestedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates payload for a song-options request.
        /// </summary>
        public SongOptionsRequestedEventArgs(Song song, FrameworkElement? anchorElement)
        {
            Song = song;
            AnchorElement = anchorElement;
        }

        /// <summary>
        /// Song associated with the requested options action.
        /// </summary>
        public Song Song { get; }

        /// <summary>
        /// UI element used as popup placement anchor.
        /// </summary>
        public FrameworkElement? AnchorElement { get; }
    }

    /// <summary>
    /// Event payload requesting play/pause behavior for a specific song row.
    /// </summary>
    public sealed class SongPlayPauseRequestedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates payload for a row play/pause request.
        /// </summary>
        public SongPlayPauseRequestedEventArgs(Song song)
        {
            Song = song;
        }

        /// <summary>
        /// Song targeted by play/pause request.
        /// </summary>
        public Song Song { get; }
    }

    /// <summary>
    /// Reusable row control for song header and song items.
    /// Owns row-level interaction events (options click, play/pause click, and column resize drag).
    /// </summary>
    public partial class SongRowView : UserControl
    {
        /// <summary>
        /// Dependency property binding for the song represented by this row.
        /// </summary>
        public static readonly DependencyProperty SongProperty = DependencyProperty.Register(
            nameof(Song),
            typeof(Song),
            typeof(SongRowView),
            new PropertyMetadata(null));

        /// <summary>
        /// Dependency property binding for shared column-layout state.
        /// </summary>
        public static readonly DependencyProperty ColumnLayoutProperty = DependencyProperty.Register(
            nameof(ColumnLayout),
            typeof(SongColumnLayoutState),
            typeof(SongRowView),
            new PropertyMetadata(null));

        /// <summary>
        /// Indicates whether this instance is rendering the static header row.
        /// </summary>
        public static readonly DependencyProperty IsHeaderProperty = DependencyProperty.Register(
            nameof(IsHeader),
            typeof(bool),
            typeof(SongRowView),
            new PropertyMetadata(false));

        /// <summary>
        /// Song model bound to this row.
        /// </summary>
        public Song? Song
        {
            get => (Song?)GetValue(SongProperty);
            set => SetValue(SongProperty, value);
        }

        /// <summary>
        /// Shared column layout used to keep header and rows aligned.
        /// </summary>
        public SongColumnLayoutState? ColumnLayout
        {
            get => (SongColumnLayoutState?)GetValue(ColumnLayoutProperty);
            set => SetValue(ColumnLayoutProperty, value);
        }

        /// <summary>
        /// True when this control instance is being used as a header row.
        /// </summary>
        public bool IsHeader
        {
            get => (bool)GetValue(IsHeaderProperty);
            set => SetValue(IsHeaderProperty, value);
        }

        /// <summary>
        /// Raised when user requests row options for the current song.
        /// </summary>
        public event EventHandler<SongOptionsRequestedEventArgs>? OptionsRequested;

        /// <summary>
        /// Raised when user clicks play/pause affordance in the row index area.
        /// </summary>
        public event EventHandler<SongPlayPauseRequestedEventArgs>? PlayPauseRequested;

        /// <summary>
        /// Initializes song-row visual tree.
        /// </summary>
        public SongRowView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Emits options-request event for the row song.
        /// </summary>
        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (Song == null)
            {
                return;
            }

            OptionsRequested?.Invoke(this, new SongOptionsRequestedEventArgs(Song, sender as FrameworkElement));
            e.Handled = true;
        }

        /// <summary>
        /// Emits play/pause-request event for the row song.
        /// </summary>
        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (Song == null)
            {
                return;
            }

            PlayPauseRequested?.Invoke(this, new SongPlayPauseRequestedEventArgs(Song));
            e.Handled = true;
        }

        /// <summary>
        /// Applies drag delta from column boundary thumbs to shared layout state.
        /// </summary>
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (ColumnLayout == null || sender is not FrameworkElement handle || handle.Tag == null)
            {
                return;
            }

            if (!Enum.TryParse(handle.Tag.ToString(), ignoreCase: true, out SongColumnKey rightColumn))
            {
                return;
            }

            ColumnLayout.ResizeBoundary(rightColumn, e.HorizontalChange, ActualWidth);
        }
    }
}
