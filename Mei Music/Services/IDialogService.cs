using System;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    public interface IDialogService
    {
        void ShowMessage(string message, string caption = "");
        bool ShowConfirmation(string message, string caption = "");
        string? PromptInput(string prompt, string title, string defaultValue = "");
        string? OpenFileDialog(string filter = "All files (*.*)|*.*");
        
        DuplicateFileDialog.DuplicateFileAction ShowDuplicateFileDialog();
        bool ShowDeleteSongConfirmation(string songName);
        void ShowSearchThroughUrlDialog();
        void ShowSongVolumeDialog(Song song, Action<double> volumeChangedCallback);
    }
}
