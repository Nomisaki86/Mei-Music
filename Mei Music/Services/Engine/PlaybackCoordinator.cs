using System.Collections;
using System.Collections.Generic;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    /// <summary>
    /// Default playback coordinator used by the main window transport controls.
    /// Holds lightweight playback-list and click-tracking state for the shell lifetime.
    /// </summary>
    public class PlaybackCoordinator : IPlaybackCoordinator
    {
        private Song? _lastSongRowClickedSong;
        private long _lastSongRowClickTimestampMs;

        /// <inheritdoc />
        public IList? PlaybackList { get; private set; }

        /// <inheritdoc />
        public void SetPlaybackList(IList? playbackList)
        {
            PlaybackList = playbackList;
        }

        /// <inheritdoc />
        public bool IsCurrentSong(Song? currentSong, Song candidateSong)
        {
            return currentSong != null
                && (ReferenceEquals(currentSong, candidateSong) || currentSong.Name == candidateSong.Name);
        }

        /// <inheritdoc />
        public void SyncCurrentSongFlags(IEnumerable<Song> songs, Song? currentSong)
        {
            foreach (Song song in songs)
            {
                bool isCurrent = IsCurrentSong(currentSong, song);
                if (song.IsCurrent != isCurrent)
                {
                    song.IsCurrent = isCurrent;
                }
            }
        }

        /// <inheritdoc />
        public int IndexOfSongInList(IList list, Song? song)
        {
            if (song == null)
            {
                return -1;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Song listSong && (ReferenceEquals(listSong, song) || listSong.Name == song.Name))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <inheritdoc />
        public Song? GetPreviousSong(IList list, Song? currentSong)
        {
            if (list.Count == 0)
            {
                return null;
            }

            int currentIndex = IndexOfSongInList(list, currentSong);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int previousIndex = (currentIndex - 1 + list.Count) % list.Count;
            return list[previousIndex] as Song;
        }

        /// <inheritdoc />
        public Song? GetNextSong(IList list, Song? currentSong)
        {
            if (list.Count == 0)
            {
                return null;
            }

            int currentIndex = IndexOfSongInList(list, currentSong);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = (currentIndex + 1) % list.Count;
            return list[nextIndex] as Song;
        }

        /// <inheritdoc />
        public bool IsSongRowDoubleClick(Song clickedSong, long nowMs, int thresholdMs)
        {
            bool sameSongAsPrevious = ReferenceEquals(_lastSongRowClickedSong, clickedSong);
            bool withinThreshold = nowMs - _lastSongRowClickTimestampMs <= thresholdMs;

            _lastSongRowClickedSong = clickedSong;
            _lastSongRowClickTimestampMs = nowMs;

            return sameSongAsPrevious && withinThreshold;
        }

        /// <inheritdoc />
        public void ResetSongRowClickTracking()
        {
            _lastSongRowClickedSong = null;
            _lastSongRowClickTimestampMs = 0;
        }
    }
}
