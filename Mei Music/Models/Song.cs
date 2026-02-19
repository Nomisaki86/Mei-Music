using System.ComponentModel;

namespace Mei_Music.Models
{
    public class Song : INotifyPropertyChanged
    {
        public string? Index { get; set; }
        public string? Name { get; set; }

        private bool isLiked;
        /// <summary>
        /// Whether this song is in the user's liked list. Persisted with song data and synced to LikedSongs.
        /// </summary>
        public bool IsLiked
        {
            get => isLiked;
            set
            {
                if (isLiked != value)
                {
                    isLiked = value;
                    OnPropertyChanged(nameof(IsLiked));
                }
            }
        }

        private double volume = 50.0;
        public double Volume
        {
            get => volume;
            set
            {
                if (volume != value)
                {
                    volume = value;
                    OnPropertyChanged(nameof(Volume));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
