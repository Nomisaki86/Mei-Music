using System;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mei_Music.Models;
using Microsoft.Win32;

namespace Mei_Music.ViewModels
{
    public partial class EditPlaylistViewModel : ObservableObject
    {
        private readonly Action<EditPlaylistViewModel> _onSave;
        private readonly Action _onCancel;

        public CreatedPlaylist Playlist { get; }

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        /// <summary>
        /// The path currently shown as the icon preview in the editor UI.
        /// This can be the persisted playlist icon path, or a newly cropped temp file.
        /// </summary>
        [ObservableProperty]
        private string? _iconPreviewPath;

        /// <summary>
        /// Newly picked (cropped) icon file path. Copied into the app icon directory on Save.
        /// </summary>
        [ObservableProperty]
        private string? _selectedImageFilePath;

        public EditPlaylistViewModel(CreatedPlaylist playlist, Action<EditPlaylistViewModel> onSave, Action onCancel)
        {
            Playlist = playlist ?? throw new ArgumentNullException(nameof(playlist));
            _onSave = onSave ?? throw new ArgumentNullException(nameof(onSave));
            _onCancel = onCancel ?? throw new ArgumentNullException(nameof(onCancel));

            Title = playlist.Title ?? string.Empty;
            Description = playlist.Description ?? string.Empty;
            IconPreviewPath = playlist.IconPath;
        }

        [RelayCommand]
        private void PickImage()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Playlist Icon"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            // Open crop dialog so the user can pan/zoom to frame the icon before saving
            var cropDialog = new ImageCropDialog(openFileDialog.FileName)
            {
                Owner = Application.Current?.MainWindow
            };

            if (cropDialog.ShowDialog() == true && cropDialog.CroppedImagePath != null)
            {
                // Best-effort cleanup of previous cropped temp file to avoid temp folder buildup.
                if (!string.IsNullOrWhiteSpace(SelectedImageFilePath)
                    && !string.Equals(SelectedImageFilePath, openFileDialog.FileName, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(SelectedImageFilePath)
                    && SelectedImageFilePath.Contains(Path.GetTempPath()[..10], StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(SelectedImageFilePath); } catch { /* non-critical cleanup */ }
                }

                SelectedImageFilePath = cropDialog.CroppedImagePath;
                IconPreviewPath = SelectedImageFilePath;
            }
        }

        [RelayCommand]
        private void Save()
        {
            _onSave(this);
        }

        [RelayCommand]
        private void Cancel()
        {
            _onCancel();
        }
    }
}

