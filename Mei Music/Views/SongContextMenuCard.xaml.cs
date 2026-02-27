using System;
using System.Windows;
using System.Windows.Controls;

namespace Mei_Music
{
    /// <summary>
    /// Popup card used for per-song context actions (add to playlist, adjust volume, rename, open folder, delete).
    /// This control only raises events; action handling is implemented by MainWindow/ViewModel.
    /// </summary>
    public partial class SongContextMenuCard : UserControl
    {
        /// <summary>
        /// Label shown for the destructive action row.
        /// Typically \"Delete Song\" in All view, \"Remove from Liked\" or \"Remove from Playlist\" in other views.
        /// </summary>
        public string DeleteActionLabel
        {
            get { return (string)GetValue(DeleteActionLabelProperty); }
            set { SetValue(DeleteActionLabelProperty, value); }
        }

        public static readonly DependencyProperty DeleteActionLabelProperty =
            DependencyProperty.Register(
                nameof(DeleteActionLabel),
                typeof(string),
                typeof(SongContextMenuCard),
                new PropertyMetadata("Delete Song"));

        /// <summary>Raised when user chooses "Add to Playlist".</summary>
        public event EventHandler? AddRequested;

        /// <summary>Raised when user chooses "Adjust Volume".</summary>
        public event EventHandler? VolumeRequested;

        /// <summary>Raised when user chooses "Rename".</summary>
        public event EventHandler? RenameRequested;

        /// <summary>Raised when user chooses "Open Folder".</summary>
        public event EventHandler? OpenFolderRequested;

        /// <summary>Raised when user chooses "Delete".</summary>
        public event EventHandler? DeleteRequested;

        /// <summary>
        /// Initializes menu card visual tree.
        /// </summary>
        public SongContextMenuCard()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Forwards add-to-playlist intent to host.
        /// </summary>
        private void AddToPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            AddRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Forwards adjust-volume intent to host.
        /// </summary>
        private void AdjustVolumeButton_Click(object sender, RoutedEventArgs e)
        {
            VolumeRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Forwards rename intent to host.
        /// </summary>
        private void RenameSongButton_Click(object sender, RoutedEventArgs e)
        {
            RenameRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Forwards open-folder intent to host.
        /// </summary>
        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Forwards delete intent to host.
        /// </summary>
        private void DeleteSongButton_Click(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
