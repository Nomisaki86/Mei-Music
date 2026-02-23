using System.Collections.Generic;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    /// <summary>
    /// Defines persistence operations for song and playlist metadata.
    /// Implementations hide storage format details (currently JSON files).
    /// </summary>
    public interface IFileService
    {
        /// <summary>
        /// Loads the song catalog from the given storage file.
        /// Returns an empty list when no song data is available.
        /// </summary>
        List<Song> LoadSongs(string filePath);

        /// <summary>
        /// Persists the full in-memory song catalog to the given storage file.
        /// </summary>
        void SaveSongs(string filePath, List<Song> songs);

        /// <summary>
        /// Loads user-created playlists from the given storage file.
        /// </summary>
        List<CreatedPlaylist> LoadPlaylists(string filePath);

        /// <summary>
        /// Persists user-created playlists to the given storage file.
        /// </summary>
        void SavePlaylists(string filePath, List<CreatedPlaylist> playlists);
    }
}
