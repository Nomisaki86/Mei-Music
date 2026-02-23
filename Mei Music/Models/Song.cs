using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace Mei_Music.Models
{
    /// <summary>
    /// Represents a single track in the library/playlists.
    /// Properties are observable so row UI and playback UI stay in sync.
    /// </summary>
    public partial class Song : ObservableObject
    {
        /// <summary>
        /// Display index string shown in list views (for example: "01", "02").
        /// Recomputed whenever the song list ordering changes.
        /// </summary>
        public string? Index { get; set; }

        /// <summary>
        /// Display name and logical identifier of the song (matches file name without extension).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Human-readable duration string (e.g. "03:45").
        /// </summary>
        [ObservableProperty]
        private string _duration = string.Empty;

        /// <summary>
        /// Whether this song is in the user's liked list. Persisted with song data and synced to LikedSongs.
        /// </summary>
        [ObservableProperty]
        private bool _isLiked;

        /// <summary>
        /// Per-song playback volume in the 0-100 range.
        /// This is independent from system/master volume.
        /// </summary>
        [ObservableProperty]
        private double _volume = 50.0;

        /// <summary>
        /// Flags the song currently targeted by playback controls.
        /// Runtime-only state; excluded from persistence to avoid stale sessions.
        /// </summary>
        [ObservableProperty]
        [property: JsonIgnore]
        private bool _isCurrent;
    }
}
