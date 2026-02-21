using CommunityToolkit.Mvvm.ComponentModel;

namespace Mei_Music.Models
{
    public partial class Song : ObservableObject
    {
        public string? Index { get; set; }
        public string? Name { get; set; }

        /// <summary>
        /// Whether this song is in the user's liked list. Persisted with song data and synced to LikedSongs.
        /// </summary>
        [ObservableProperty]
        private bool _isLiked;

        [ObservableProperty]
        private double _volume = 50.0;
    }
}
