using System;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    /// <summary>
    /// Encapsulates all UI dialogs used by view-model logic.
    /// Keeps user prompts/message interactions abstracted from business logic.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows an informational message box to the user.
        /// </summary>
        void ShowMessage(string message, string caption = "");

        /// <summary>
        /// Shows a Yes/No confirmation dialog and returns true for Yes.
        /// </summary>
        bool ShowConfirmation(string message, string caption = "");

        /// <summary>
        /// Prompts the user for text input and returns the typed value.
        /// Returns null/empty when cancelled.
        /// </summary>
        string? PromptInput(string prompt, string title, string defaultValue = "");

        /// <summary>
        /// Opens a file picker and returns the selected file path.
        /// </summary>
        string? OpenFileDialog(string filter = "All files (*.*)|*.*");
        
        /// <summary>
        /// Shows duplicate-file handling options and returns the selected action.
        /// </summary>
        DuplicateFileDialog.DuplicateFileAction ShowDuplicateFileDialog();

        /// <summary>
        /// Shows a delete-song confirmation dialog.
        /// </summary>
        bool ShowDeleteSongConfirmation(string songName);

        /// <summary>
        /// Opens the URL import window used to download media from links.
        /// </summary>
        void ShowSearchThroughUrlDialog();

        /// <summary>
        /// Opens the per-song volume dialog and reports updated values via callback.
        /// </summary>
        void ShowSongVolumeDialog(Song song, Action<double> volumeChangedCallback);
    }
}
