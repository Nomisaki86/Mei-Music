using System;
using System.Collections.Generic;
using Mei_Music.Models;

namespace Mei_Music.Services
{
    /// <summary>
    /// Defines import and conversion operations for local media files.
    /// Keeps file-system/tooling logic out of view code-behind.
    /// </summary>
    public interface IMediaImportService
    {
        /// <summary>
        /// Managed storage directory where imported audio files are kept.
        /// </summary>
        string AudioStorageDirectory { get; }

        /// <summary>
        /// Returns true when file extension is a supported video input for conversion.
        /// </summary>
        bool IsVideoExtension(string extension);

        /// <summary>
        /// Returns true when file extension is a supported direct-audio import.
        /// </summary>
        bool IsAudioExtension(string extension);

        /// <summary>
        /// Converts a video file to mp3 in managed storage and returns output path.
        /// </summary>
        string ConvertVideoToAudio(string videoFilePath);

        /// <summary>
        /// Imports the provided file path into the song list, optionally resolving duplicates via callback.
        /// </summary>
        ImportSongOutcome ImportFileIntoSongs(
            IList<Song> songs,
            string filePath,
            Func<DuplicateImportChoice> duplicateChoiceProvider);

        /// <summary>
        /// Refreshes duration metadata for the song matching the given file path.
        /// Returns true when a song was updated.
        /// </summary>
        bool RefreshSongDurationFromFile(IList<Song> songs, string filePath);
    }

    /// <summary>
    /// Duplicate-name resolution choices used by import flow.
    /// </summary>
    public enum DuplicateImportChoice
    {
        Replace,
        Rename,
        Cancel
    }

    /// <summary>
    /// Import outcome classification returned by media import operations.
    /// </summary>
    public enum ImportSongOutcomeKind
    {
        Added,
        Replaced,
        RenameRequested,
        Cancelled,
        Ignored
    }

    /// <summary>
    /// Result payload for import operations.
    /// </summary>
    public sealed class ImportSongOutcome
    {
        /// <summary>
        /// Outcome kind.
        /// </summary>
        public ImportSongOutcomeKind Kind { get; }

        /// <summary>
        /// Existing song that should be renamed when Kind is RenameRequested.
        /// </summary>
        public Song? ExistingSong { get; }

        /// <summary>
        /// Creates a new import outcome payload.
        /// </summary>
        public ImportSongOutcome(ImportSongOutcomeKind kind, Song? existingSong = null)
        {
            Kind = kind;
            ExistingSong = existingSong;
        }
    }
}
