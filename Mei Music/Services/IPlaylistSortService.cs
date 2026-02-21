using System.Collections.Generic;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    public interface IPlaylistSortService
    {
        List<Song> SortAlphabetically(IList<Song> originalList);
        List<Song> SortByModificationDate(IList<Song> originalList, string directoryPath);
    }
}
