using System;
using System.Windows;
using Microsoft.Win32;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    /// <summary>
    /// Concrete dialog adapter used by view-models to interact with WPF UI dialogs.
    /// Centralizing these calls keeps view-models testable and UI-framework agnostic.
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <summary>
        /// Shows a standard informational message.
        /// </summary>
        public void ShowMessage(string message, string caption = "")
        {
            MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Shows a confirmation prompt and returns true only when the user clicks Yes.
        /// </summary>
        public bool ShowConfirmation(string message, string caption = "")
        {
            var result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Shows a simple text input prompt.
        /// </summary>
        public string? PromptInput(string prompt, string title, string defaultValue = "")
        {
            return Microsoft.VisualBasic.Interaction.InputBox(prompt, title, defaultValue);
        }

        /// <summary>
        /// Opens a file picker and returns the selected file path.
        /// </summary>
        public string? OpenFileDialog(string filter = "All files (*.*)|*.*")
        {
            var ofd = new OpenFileDialog { Filter = filter, Multiselect = false };
            if (ofd.ShowDialog() == true)
            {
                return ofd.FileName;
            }
            return null;
        }

        /// <summary>
        /// Displays duplicate-file options used when importing files with conflicting names.
        /// </summary>
        public DuplicateFileDialog.DuplicateFileAction ShowDuplicateFileDialog()
        {
            var dialog = new DuplicateFileDialog();
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
            return dialog.SelectedAction;
        }

        /// <summary>
        /// Displays the custom delete-song confirmation dialog.
        /// </summary>
        public bool ShowDeleteSongConfirmation(string songName)
        {
            var dialog = new DeleteSongConfirmationWindow($"Are you sure you want to delete '{songName}'?");
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
            return dialog.IsConfirmed;
        }

        /// <summary>
        /// Opens the URL-import window and passes the main window for callback integration.
        /// </summary>
        public void ShowSearchThroughUrlDialog()
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                var window = new SearchThroughURLWindow(mainWindow);
                window.Show();
            }
        }

        /// <summary>
        /// Opens song volume controller and relays live volume updates through callback.
        /// </summary>
        public void ShowSongVolumeDialog(Song song, Action<double> volumeChangedCallback)
        {
            var dialog = new SongVolumeController(song.Volume)
            {
                Owner = Application.Current.MainWindow,
                VolumeChangedCallback = volumeChangedCallback
            };
            dialog.ShowDialog();
        }
    }
}
