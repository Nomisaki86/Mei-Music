using Mei_Music.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mei_Music.Services
{
    public class PlaylistSortService : IPlaylistSortService
    {
        /// <summary>
        /// Returns a new list sorted by song name in ascending order.
        /// </summary>
        public List<Song> SortAlphabetically(IList<Song> songs)
        {
            return songs
                .OrderBy(song => song.Name)
                .ToList();
        }

        /// <summary>
        /// Sorts by newest file update time first, checking mp3 then wav.
        /// </summary>
        public List<Song> SortByModificationDate(IList<Song> songs, string outputDirectory)
        {
            return songs
                .OrderByDescending(item =>
                {
                    string mp3Path = Path.Combine(outputDirectory, item.Name + ".mp3");
                    string wavPath = Path.Combine(outputDirectory, item.Name + ".wav");

                    DateTime lastWriteTime = File.Exists(mp3Path) ? File.GetLastWriteTime(mp3Path) :
                                              File.Exists(wavPath) ? File.GetLastWriteTime(wavPath) :
                                              // Missing files are treated as oldest so they sink to bottom.
                                              DateTime.MinValue;

                    return lastWriteTime;
                })
                .ToList();
        }
    }
}
