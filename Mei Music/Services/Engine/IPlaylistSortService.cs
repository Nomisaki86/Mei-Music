using System.Collections.Generic;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    /// <summary>
    /// Defines sorting strategies used by the playlist/song list view.
    /// Methods return new ordered lists without mutating the incoming collection.
    /// </summary>
    public interface IPlaylistSortService
    {
        /// <summary>
        /// Orders songs alphabetically by display name.
        /// </summary>
        List<Song> SortAlphabetically(IList<Song> originalList);

        /// <summary>
        /// Orders songs by file modification time (newest first) using files in the given directory.
        /// </summary>
        List<Song> SortByModificationDate(IList<Song> originalList, string directoryPath);
    }
}
