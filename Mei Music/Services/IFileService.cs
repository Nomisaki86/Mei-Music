using System.Collections.Generic;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    public interface IFileService
    {
        List<Song> LoadSongs(string filePath);
        void SaveSongs(string filePath, List<Song> songs);
        List<CreatedPlaylist> LoadPlaylists(string filePath);
        void SavePlaylists(string filePath, List<CreatedPlaylist> playlists);
    }
}
