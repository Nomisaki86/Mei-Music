using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    /// <summary>
    /// Handles local media import and ffmpeg conversion workflows.
    /// </summary>
    public class MediaImportService : IMediaImportService
    {
        private readonly string _audioStorageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Mei Music",
            "playlist");

        /// <inheritdoc />
        public string AudioStorageDirectory => _audioStorageDirectory;

        /// <inheritdoc />
        public bool IsVideoExtension(string extension)
        {
            return string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".mkv", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public bool IsAudioExtension(string extension)
        {
            return string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc />
        public string ConvertVideoToAudio(string videoFilePath)
        {
            try
            {
                Directory.CreateDirectory(_audioStorageDirectory);

                string audioFilePath = Path.Combine(_audioStorageDirectory, Path.GetFileNameWithoutExtension(videoFilePath) + ".mp3");
                string? ffmpegPath = GetToolPath(@"Resources\ffmpeg\ffmpeg.exe");
                if (ffmpegPath == null)
                {
                    throw new InvalidOperationException("ffmpeg.exe is missing.");
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-i \"{videoFilePath}\" -q:a 0 -map a \"{audioFilePath}\" -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Process? process = Process.Start(startInfo);
                if (process == null)
                {
                    throw new InvalidOperationException("Fail to start the ffmpeg process.");
                }

                using (process)
                {
                    process.WaitForExit();
                }

                return audioFilePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to convert Video: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public ImportSongOutcome ImportFileIntoSongs(
            IList<Song> songs,
            string filePath,
            Func<DuplicateImportChoice> duplicateChoiceProvider)
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return new ImportSongOutcome(ImportSongOutcomeKind.Ignored);
            }

            Song? existingSong = songs.FirstOrDefault(song => song.Name == fileNameWithoutExtension);
            if (existingSong == null)
            {
                AddSongToList(songs, fileNameWithoutExtension, filePath);
                return new ImportSongOutcome(ImportSongOutcomeKind.Added);
            }

            DuplicateImportChoice duplicateChoice = duplicateChoiceProvider();
            switch (duplicateChoice)
            {
                case DuplicateImportChoice.Replace:
                    existingSong.Duration = GetMediaDuration(filePath);
                    return new ImportSongOutcome(ImportSongOutcomeKind.Replaced);
                case DuplicateImportChoice.Rename:
                    return new ImportSongOutcome(ImportSongOutcomeKind.RenameRequested, existingSong);
                default:
                    return new ImportSongOutcome(ImportSongOutcomeKind.Cancelled);
            }
        }

        /// <inheritdoc />
        public bool RefreshSongDurationFromFile(IList<Song> songs, string filePath)
        {
            string duration = GetMediaDuration(filePath);
            if (string.IsNullOrWhiteSpace(duration))
            {
                return false;
            }

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return false;
            }

            Song? song = songs.FirstOrDefault(s => s.Name == fileNameWithoutExtension);
            if (song == null || song.Duration == duration)
            {
                return false;
            }

            song.Duration = duration;
            return true;
        }

        private static void AddSongToList(IList<Song> songs, string name, string sourceFilePath)
        {
            Song song = new Song
            {
                Index = (songs.Count + 1).ToString("D2"),
                Name = name,
                Duration = GetMediaDuration(sourceFilePath)
            };

            songs.Add(song);
        }

        private static string GetMediaDuration(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return string.Empty;
            }

            try
            {
                using var mediaFile = TagLib.File.Create(filePath);
                return mediaFile.Properties.Duration.ToString(@"mm\:ss");
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string? GetToolPath(string relativeToolPath)
        {
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativeToolPath);
            if (!File.Exists(fullPath))
            {
                string toolName = Path.GetFileName(fullPath);
                MessageBox.Show(
                    $"Required tool '{toolName}' was not found at:\n{fullPath}\n\nPlease reinstall the application.",
                    "Missing Tool",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }

            return fullPath;
        }
    }
}
