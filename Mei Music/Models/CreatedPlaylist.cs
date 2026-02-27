using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace Mei_Music.Models
{
    /// <summary>
    /// Represents a user-created playlist with title, optional icon, and privacy flag.
    /// Icon is stored under app data; IconPath is the full path to the image file.
    /// </summary>
    public partial class CreatedPlaylist : ObservableObject
    {
        /// <summary>
        /// Stable unique identifier used for persistence and icon file naming.
        /// </summary>
        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Playlist name shown in the sidebar and header.
        /// </summary>
        [ObservableProperty]
        private string _title = string.Empty;

        /// <summary>
        /// Optional playlist description shown in the edit/info page.
        /// Persisted to playlists.json.
        /// </summary>
        [ObservableProperty]
        private string _description = string.Empty;

        /// <summary>
        /// Full filesystem path to the playlist icon image, or null when no icon is set.
        /// </summary>
        [ObservableProperty]
        private string? _iconPath;

        /// <summary>
        /// Indicates whether the playlist is intended as private to the local user.
        /// Reserved for future privacy filtering behavior.
        /// </summary>
        [ObservableProperty]
        private bool _isPrivate;

        /// <summary>
        /// Song identifiers that belong to this playlist in display order.
        /// </summary>
        [ObservableProperty]
        private List<string> _songIds = new();

        /// <summary>
        /// Legacy song-name membership field loaded from older playlist files.
        /// Used only for migration to <see cref="SongIds"/> and omitted from new saves when null.
        /// </summary>
        [JsonProperty("SongNames", NullValueHandling = NullValueHandling.Ignore)]
        public List<string>? LegacySongNames { get; set; }
    }
}
