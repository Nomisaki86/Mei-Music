using Mei_Music.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Mei_Music.Services
{
    /// <summary>
    /// JSON-based persistence service for songs and user-created playlists.
    /// Handles disk I/O and minimal data normalization when loading older data.
    /// </summary>
    public class FileService : IFileService
    {
        /// <summary>
        /// Persists the in-memory song metadata to disk as formatted JSON.
        /// The parent folder is created on demand so first-run saves succeed.
        /// </summary>
        public void SaveSongs(string songDataFilePath, List<Song> songs)
        {
            string? parentDirectory = Path.GetDirectoryName(songDataFilePath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = JsonConvert.SerializeObject(songs, Formatting.Indented);
            File.WriteAllText(songDataFilePath, json);
        }

        /// <summary>
        /// Loads song metadata from disk and normalizes values that may have
        /// come from older files or manual edits.
        /// </summary>
        public List<Song> LoadSongs(string songDataFilePath)
        {
            if (!File.Exists(songDataFilePath))
            {
                return new List<Song>();
            }

            string json = File.ReadAllText(songDataFilePath);
            var loadedSongs = JsonConvert.DeserializeObject<List<Song>>(json) ?? new List<Song>();

            // Normalize loaded records so legacy/edited files do not break runtime assumptions.
            var normalizedSongs = new List<Song>();
            foreach (var loadedSong in loadedSongs)
            {
                normalizedSongs.Add(new Song
                {
                    Index = loadedSong.Index,
                    Name = loadedSong.Name,
                    IsLiked = loadedSong.IsLiked,
                    Duration = loadedSong.Duration ?? string.Empty,
                    // Keep persisted volume in a safe range; default to 50 when invalid.
                    Volume = (loadedSong.Volume > 100) ? 100 : (loadedSong.Volume < 0 ? 50 : loadedSong.Volume)
                });
            }

            return normalizedSongs;
        }

        /// <summary>
        /// Persists the list of created playlists to disk as JSON.
        /// Parent directory is created on demand.
        /// </summary>
        public void SavePlaylists(string playlistsFilePath, List<CreatedPlaylist> playlists)
        {
            string? parentDirectory = Path.GetDirectoryName(playlistsFilePath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            string json = JsonConvert.SerializeObject(playlists, Formatting.Indented);
            File.WriteAllText(playlistsFilePath, json);
        }

        /// <summary>
        /// Loads created playlists from disk. Returns an empty list if the file does not exist.
        /// </summary>
        public List<CreatedPlaylist> LoadPlaylists(string playlistsFilePath)
        {
            if (!File.Exists(playlistsFilePath))
            {
                return new List<CreatedPlaylist>();
            }

            string json = File.ReadAllText(playlistsFilePath);
            var loaded = JsonConvert.DeserializeObject<List<CreatedPlaylist>>(json) ?? new List<CreatedPlaylist>();
            return loaded;
        }
    }
}
