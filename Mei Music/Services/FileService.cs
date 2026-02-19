using Mei_Music.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace Mei_Music.Services
{
    public class FileService
    {
        /// <summary>
        /// Persists the in-memory song metadata to disk as formatted JSON.
        /// The parent folder is created on demand so first-run saves succeed.
        /// </summary>
        public void SaveSongs(string songDataFilePath, IEnumerable<Song> songs)
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

            var normalizedSongs = new List<Song>();
            foreach (var loadedSong in loadedSongs)
            {
                normalizedSongs.Add(new Song
                {
                    Index = loadedSong.Index,
                    Name = loadedSong.Name,
                    IsLiked = loadedSong.IsLiked,
                    // Keep persisted volume in a safe range; default to 50 when invalid.
                    Volume = (loadedSong.Volume > 100) ? 100 : (loadedSong.Volume < 0 ? 50 : loadedSong.Volume)
                });
            }

            return normalizedSongs;
        }
    }
}
