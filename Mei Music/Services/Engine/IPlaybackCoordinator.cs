using System.Collections;
using System.Collections.Generic;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    /// <summary>
    /// Encapsulates playback-list navigation and current-song matching rules used by the main shell.
    /// Keeps transport behavior consistent across row actions, transport buttons, and media-ended events.
    /// </summary>
    public interface IPlaybackCoordinator
    {
        /// <summary>
        /// Playback list currently used by transport actions (prev/next/auto-next).
        /// </summary>
        IList? PlaybackList { get; }

        /// <summary>
        /// Sets the playback list that transport actions should use.
        /// </summary>
        void SetPlaybackList(IList? playbackList);

        /// <summary>
        /// Returns true when the candidate matches the current song by reference or stable song ID.
        /// </summary>
        bool IsCurrentSong(Song? currentSong, Song candidateSong);

        /// <summary>
        /// Synchronizes each row model's IsCurrent flag against the currently playing song.
        /// </summary>
        void SyncCurrentSongFlags(IEnumerable<Song> songs, Song? currentSong);

        /// <summary>
        /// Finds a song index in a non-generic list by reference/ID, or -1 when not present.
        /// </summary>
        int IndexOfSongInList(IList list, Song? song);

        /// <summary>
        /// Resolves previous song in the list (wraps to end).
        /// </summary>
        Song? GetPreviousSong(IList list, Song? currentSong);

        /// <summary>
        /// Resolves next song in the list (wraps to beginning).
        /// </summary>
        Song? GetNextSong(IList list, Song? currentSong);

        /// <summary>
        /// Returns true when the click sequence should be treated as a row double-click.
        /// </summary>
        bool IsSongRowDoubleClick(Song clickedSong, long nowMs, int thresholdMs);

        /// <summary>
        /// Clears internal row-click tracking state.
        /// </summary>
        void ResetSongRowClickTracking();
    }
}
