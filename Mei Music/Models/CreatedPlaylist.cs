using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mei_Music.Models
{
    /// <summary>
    /// Represents a user-created playlist with title, optional icon, and privacy flag.
    /// Icon is stored under app data; IconPath is the full path to the image file.
    /// </summary>
    public partial class CreatedPlaylist : ObservableObject
    {
        [ObservableProperty]
        private string _id = Guid.NewGuid().ToString("N");

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string? _iconPath;

        [ObservableProperty]
        private bool _isPrivate;

        [ObservableProperty]
        private List<string> _songNames = new();
    }
}
