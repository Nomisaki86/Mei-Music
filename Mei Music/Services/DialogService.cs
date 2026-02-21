using System;
using System.Windows;
using Microsoft.Win32;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    public class DialogService : IDialogService
    {
        public void ShowMessage(string message, string caption = "")
        {
            MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public bool ShowConfirmation(string message, string caption = "")
        {
            var result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }

        public string? PromptInput(string prompt, string title, string defaultValue = "")
        {
            return Microsoft.VisualBasic.Interaction.InputBox(prompt, title, defaultValue);
        }

        public string? OpenFileDialog(string filter = "All files (*.*)|*.*")
        {
            var ofd = new OpenFileDialog { Filter = filter, Multiselect = false };
            if (ofd.ShowDialog() == true)
            {
                return ofd.FileName;
            }
            return null;
        }

        public DuplicateFileDialog.DuplicateFileAction ShowDuplicateFileDialog()
        {
            var dialog = new DuplicateFileDialog();
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
            return dialog.SelectedAction;
        }

        public bool ShowDeleteSongConfirmation(string songName)
        {
            var dialog = new DeleteSongConfirmationWindow($"Are you sure you want to delete '{songName}'?");
            dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
            return dialog.IsConfirmed;
        }

        public void ShowSearchThroughUrlDialog()
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                var window = new SearchThroughURLWindow(mainWindow);
                window.Show();
            }
        }

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
