using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mei_Music.Models;
using Mei_Music.Services;

namespace Mei_Music.ViewModels
{
    /// <summary>
    /// Enumerates the different high-level views that can be shown in the right panel.
    /// </summary>
    public enum RightPanelPage
    {
        None,
        AllSongs,
        LikedSongs,
        PlaylistSongs,
        EditPlaylist
    }

    /// <summary>
    /// Describes what the destructive action in the song context menu should do for the current view.
    /// This is used as a single source of truth for both the button label and its behavior.
    /// </summary>
    public enum SongContextDeleteMode
    {
        None,
        DeleteFromLibrary,
        RemoveFromLiked,
        RemoveFromPlaylist
    }

    /// <summary>
    /// Main application view-model.
    /// Coordinates song library state, playback state, and user commands invoked from the UI.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        /// <summary>Service used to persist songs and playlists.</summary>
        private readonly IFileService _fileService;

        /// <summary>Service that provides song ordering strategies.</summary>
        private readonly IPlaylistSortService _playlistSortService;

        /// <summary>Service that controls low-level audio playback.</summary>
        private readonly IAudioPlayerService _audioPlayer;

        /// <summary>Service that opens user dialogs (prompt, confirmation, etc.).</summary>
        private readonly IDialogService _dialogService;

        /// <summary>Path to persisted song metadata JSON.</summary>
        private readonly string _songDataFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "songData.json");

        /// <summary>Path to persisted playlist metadata JSON.</summary>
        private readonly string _playlistsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlists.json");

        /// <summary>Directory where copied playlist icon files are stored.</summary>
        private readonly string _playlistIconsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist-icons");

        /// <summary>Directory where managed audio files are stored.</summary>
        private readonly string _audioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");

        /// <summary>
        /// Initializes services, collections, and audio event subscriptions.
        /// </summary>
        public MainViewModel(IFileService fileService, IPlaylistSortService playlistSortService, IAudioPlayerService audioPlayer, IDialogService dialogService)
        {
            _fileService = fileService;
            _playlistSortService = playlistSortService;
            _audioPlayer = audioPlayer;
            _dialogService = dialogService;

            Songs = new ObservableCollection<Song>();
            LikedSongs = new ObservableCollection<Song>();
            ActivePlaylistSongs = new ObservableCollection<Song>();
            CreatedPlaylists = new ObservableCollection<CreatedPlaylist>();

            _audioPlayer.MediaOpened += OnMediaOpened;
            _audioPlayer.MediaEnded += OnMediaEnded;
            _audioPlayer.PositionChanged += OnPositionChanged;
        }

        // --- Core Collections ---
        public ObservableCollection<Song> Songs { get; }
        public ObservableCollection<Song> LikedSongs { get; }
        /// <summary>Currently displayed playlist's songs; kept in sync when active playlist or Songs change. Same pattern as LikedSongs.</summary>
        public ObservableCollection<Song> ActivePlaylistSongs { get; }

        /// <summary>The playlist currently shown in the main list, or null when showing All/Liked.</summary>
        public CreatedPlaylist? ActivePlaylist { get; private set; }

        /// <summary>
        /// Title shown in the right-panel header label (All / Liked / playlist title).
        /// Kept in sync by view navigation and by playlist edits.
        /// </summary>
        [ObservableProperty]
        private string _currentHeaderTitle = "All";

        /// <summary>
        /// Current high-level page shown in the right panel (All, Liked, playlist songs, edit playlist, etc.).
        /// </summary>
        [ObservableProperty]
        private RightPanelPage _currentPage = RightPanelPage.None;

        /// <summary>
        /// Effective delete/remove mode for the song context menu based on the current page.
        /// </summary>
        public SongContextDeleteMode CurrentSongDeleteMode
        {
            get
            {
                switch (CurrentPage)
                {
                    case RightPanelPage.AllSongs:
                        return SongContextDeleteMode.DeleteFromLibrary;
                    case RightPanelPage.LikedSongs:
                        return SongContextDeleteMode.RemoveFromLiked;
                    case RightPanelPage.PlaylistSongs:
                        return SongContextDeleteMode.RemoveFromPlaylist;
                    default:
                        return SongContextDeleteMode.None;
                }
            }
        }

        /// <summary>
        /// Human-readable label for the destructive song action, bound by the song context menu.
        /// </summary>
        public string CurrentSongDeleteLabel
        {
            get
            {
                switch (CurrentSongDeleteMode)
                {
                    case SongContextDeleteMode.DeleteFromLibrary:
                        return "Delete Song";
                    case SongContextDeleteMode.RemoveFromLiked:
                        return "Remove from Liked";
                    case SongContextDeleteMode.RemoveFromPlaylist:
                        return "Remove from Playlist";
                    default:
                        return "Delete Song";
                }
            }
        }

        /// <summary>
        /// Keeps dependent delete-mode properties in sync when the current page changes.
        /// </summary>
        partial void OnCurrentPageChanged(RightPanelPage oldValue, RightPanelPage newValue)
        {
            OnPropertyChanged(nameof(CurrentSongDeleteMode));
            OnPropertyChanged(nameof(CurrentSongDeleteLabel));
        }

        /// <summary>
        /// Last non-edit page visited before entering EditPlaylist mode.
        /// Used to restore the previous view after Save/Cancel.
        /// </summary>
        private RightPanelPage _previousPage = RightPanelPage.None;

        /// <summary>Editor state for the playlist currently being edited, or null when not editing.</summary>
        [ObservableProperty]
        private EditPlaylistViewModel? _playlistEditor;

        public ObservableCollection<CreatedPlaylist> CreatedPlaylists { get; }

        // --- Playback State ---
        [ObservableProperty]
        private Song? _currentSong;

        [ObservableProperty]
        private bool _isPlaying;

        /// <summary>
        /// Set to true while the user is dragging or clicking the progress slider.
        /// Prevents the position timer from overwriting the slider value mid-seek.
        /// </summary>
        public bool IsSeeking { get; set; }

        [ObservableProperty]
        private double _playProgress;

        [ObservableProperty]
        private string _currentTimeText = "00:00";

        [ObservableProperty]
        private string _totalTimeText = "00:00";

        [ObservableProperty]
        private double _totalTimeSeconds;

        [ObservableProperty]
        private double _volume = 50; // Default volume (represents system volume)

        // --- Playback Commands ---

        /// <summary>
        /// Starts playback for the selected song and updates current-song state.
        /// </summary>
        [RelayCommand]
        public void PlaySong(Song song)
        {
            if (song == null) return;
            CurrentSong = song;

            _audioPlayer.Volume = song.Volume;

            string audioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
            string mp3FilePath = Path.Combine(audioDirectory, song.Name + ".mp3");
            string wavFilePath = Path.Combine(audioDirectory, song.Name + ".wav");
            string? audioFilePath = File.Exists(mp3FilePath) ? mp3FilePath : (File.Exists(wavFilePath) ? wavFilePath : null);

            if (audioFilePath == null)
            {
                IsPlaying = false;
                return;
            }

            _audioPlayer.Play(audioFilePath);
            IsPlaying = true;
        }

        /// <summary>
        /// Toggles pause/resume for currently loaded media.
        /// If no media is loaded but CurrentSong exists, starts that song.
        /// </summary>
        [RelayCommand]
        public void TogglePlay()
        {
            if (IsPlaying)
            {
                _audioPlayer.Pause();
                IsPlaying = false;
            }
            else
            {
                if (_audioPlayer.HasAudio)
                {
                    _audioPlayer.Play();
                    IsPlaying = true;
                }
                else if (CurrentSong != null)
                {
                    PlaySong(CurrentSong);
                }
            }
        }

        /// <summary>
        /// Seeks playback to the requested absolute position (in seconds).
        /// </summary>
        [RelayCommand]
        public void Seek(double positionSeconds)
        {
            _audioPlayer.Position = TimeSpan.FromSeconds(positionSeconds);
            CurrentTimeText = _audioPlayer.Position.ToString(@"mm\:ss");
        }

        // --- Event Handlers ---

        private void OnMediaOpened(object? sender, EventArgs e)
        {
            TimeSpan totalDuration = _audioPlayer.NaturalDuration;
            TotalTimeText = totalDuration.ToString(@"mm\:ss");
            TotalTimeSeconds = totalDuration.TotalSeconds;
        }

        public event EventHandler? MediaEnded;

        /// <summary>
        /// Forwards media-ended event to code-behind until navigation logic is fully moved to VM.
        /// </summary>
        private void OnMediaEnded(object? sender, EventArgs e)
        {
            // Forward event to code-behind for playlist advancing (until that's moved to VM)
            MediaEnded?.Invoke(this, EventArgs.Empty);
        }

        private void OnPositionChanged(object? sender, TimeSpan position)
        {
            // Skip timer updates while the user is dragging/clicking the slider
            // to prevent the binding from snapping back to the old position.
            if (IsSeeking) return;

            CurrentTimeText = position.ToString(@"mm\:ss");
            PlayProgress = position.TotalSeconds;
        }

        /// <summary>
        /// Applies the currently playing song's per-song volume to the player engine.
        /// </summary>
        public void UpdatePlayerVolume()
        {
            if (CurrentSong != null)
            {
                _audioPlayer.Volume = CurrentSong.Volume;
            }
        }

        // --- Song Action Commands ---

        /// <summary>
        /// Toggles liked status and keeps LikedSongs collection synchronized.
        /// </summary>
        [RelayCommand]
        public void ToggleLike(Song song)
        {
            if (song == null) return;
            song.IsLiked = !song.IsLiked;
            if (song.IsLiked)
            {
                if (!LikedSongs.Contains(song))
                    LikedSongs.Add(song);
            }
            else
            {
                LikedSongs.Remove(song);
            }
            SaveSongData();
        }

        /// <summary>
        /// Renames a song in metadata and on disk, preserving track attributes.
        /// </summary>
        [RelayCommand]
        public void RenameSong(Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.Name)) return;

            string oldName = song.Name;
            string? newName = _dialogService.PromptInput("Enter a new name for the file:", "Rename Song", oldName);

            if (!string.IsNullOrEmpty(newName) && newName != oldName)
            {
                bool duplicateSongName = Songs.Any(existingSong =>
                    !AreSameSongById(existingSong, song)
                    && string.Equals(existingSong.Name, newName, StringComparison.OrdinalIgnoreCase));
                if (duplicateSongName)
                {
                    _dialogService.ShowMessage($"A file named \"{newName}\" already exists in the playlist. Please choose a different name.", "Duplicate Name");
                    return;
                }

                // Rename files
                string oldMp3 = Path.Combine(_audioDirectory, oldName + ".mp3");
                string newMp3 = Path.Combine(_audioDirectory, newName + ".mp3");
                string oldWav = Path.Combine(_audioDirectory, oldName + ".wav");
                string newWav = Path.Combine(_audioDirectory, newName + ".wav");

                bool targetPathExists =
                    (!string.Equals(oldMp3, newMp3, StringComparison.OrdinalIgnoreCase) && File.Exists(newMp3))
                    || (!string.Equals(oldWav, newWav, StringComparison.OrdinalIgnoreCase) && File.Exists(newWav));
                if (targetPathExists)
                {
                    _dialogService.ShowMessage($"A file named \"{newName}\" already exists in the playlist. Please choose a different name.", "Duplicate Name");
                    return;
                }

                try
                {
                    if (File.Exists(oldMp3)) File.Move(oldMp3, newMp3);
                    if (File.Exists(oldWav)) File.Move(oldWav, newWav);

                    song.Name = newName;
                    SaveSongData();
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error renaming file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Deletes a song after confirmation and removes corresponding files from disk.
        /// </summary>
        [RelayCommand]
        public void DeleteSong(Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.Name)) return;

            if (_dialogService.ShowDeleteSongConfirmation(song.Name))
            {
                var songToRemove = Songs.FirstOrDefault(existingSong => AreSameSongById(existingSong, song));
                if (songToRemove != null)
                {
                    //Remove from all Playlists
                    bool playlistsChanged = RemoveSongFromAllPlaylists(songToRemove);
                    //Remove from All list
                    Songs.Remove(songToRemove);
                    //Remove from Liked list
                    LikedSongs.Remove(songToRemove);
                    if (AreSameSongById(CurrentSong, songToRemove))
                    {
                        CurrentSong = null;
                        IsPlaying = false;
                    }

                    if (playlistsChanged)
                    {
                        SaveCreatedPlaylists();
                    }

                    SaveSongData();
                }

                string mp3 = Path.Combine(_audioDirectory, song.Name + ".mp3");
                string wav = Path.Combine(_audioDirectory, song.Name + ".wav");

                if (File.Exists(mp3)) File.Delete(mp3);
                if (File.Exists(wav)) File.Delete(wav);

                RefreshSongsInUI();
            }
        }

        /// <summary>
        /// Opens File Explorer and highlights the backing audio file for the song.
        /// </summary>
        [RelayCommand]
        public void OpenFolder(Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.Name)) return;

            string mp3 = Path.Combine(_audioDirectory, song.Name + ".mp3");
            string wav = Path.Combine(_audioDirectory, song.Name + ".wav");
            string? targetPath = File.Exists(mp3) ? mp3 : (File.Exists(wav) ? wav : null);

            if (targetPath != null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{targetPath}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                _dialogService.ShowMessage("File not found in storage.");
            }
        }

        /// <summary>
        /// Opens per-song volume dialog and persists updated volume.
        /// </summary>
        [RelayCommand]
        public void SongVolume(Song song)
        {
            if (song == null) return;

            _dialogService.ShowSongVolumeDialog(song, newVolume =>
            {
                song.Volume = newVolume;
                if (AreSameSongById(CurrentSong, song))
                {
                    UpdatePlayerVolume();
                }
                SaveSongData();
            });
        }

        // --- Data Management & Refresh ---

        /// <summary>
        /// Re-indexes songs and persists song metadata.
        /// </summary>
        public void SaveSongData()
        {
            UpdateSongIndexes();
            try { _fileService.SaveSongs(_songDataFilePath, Songs.ToList()); }
            catch (Exception ex) { _dialogService.ShowMessage($"Error saving song data: {ex.Message}"); }
        }

        /// <summary>
        /// Persists created playlists.
        /// </summary>
        public void SaveCreatedPlaylists()
        {
            try { _fileService.SavePlaylists(_playlistsFilePath, CreatedPlaylists.ToList()); }
            catch (Exception ex) { _dialogService.ShowMessage($"Error saving playlists: {ex.Message}"); }
        }

        /// <summary>
        /// Updates display index strings (01, 02, ...) based on current Songs order.
        /// </summary>
        public void UpdateSongIndexes()
        {
            for (int i = 0; i < Songs.Count; i++)
            {
                Songs[i].Index = (i + 1).ToString("D2");
            }
        }

        /// <summary>
        /// Reconciles in-memory song list with files on disk.
        /// Removes unsupported files, resolves duplicate format cases, deletes stale rows,
        /// and adds newly detected audio files.
        /// </summary>
        [RelayCommand]
        public void RefreshSongsInUI()
        {
            bool nonAudioFilesFound = false;

            // 1) Remove non-audio files from managed storage.
            foreach (string filePath in Directory.GetFiles(_audioDirectory))
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext != ".mp3" && ext != ".wav")
                {
                    nonAudioFilesFound = true;
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            filePath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowMessage($"Failed to move file '{filePath}' to the Recycle Bin: {ex.Message}.");
                    }
                }
            }

            if (nonAudioFilesFound)
            {
                _dialogService.ShowMessage("Some Non-mp3/wav files are detected; they have been moved to the trash bin.");
            }

            // 2) When both .mp3 and .wav exist with same base name, keep .mp3 and recycle .wav.
            var mp3Files = new System.Collections.Generic.HashSet<string>();
            foreach (string filePath in Directory.GetFiles(_audioDirectory))
            {
                string extension = Path.GetExtension(filePath).ToLower();
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                if (extension == ".mp3")
                {
                    mp3Files.Add(fileName);
                }
                else if (extension == ".wav" && mp3Files.Contains(fileName))
                {
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            filePath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                        _dialogService.ShowMessage($"Duplicate found for '{fileName}'. The .wav version has been deleted.", "Duplicate Format");
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowMessage($"Failed to delete duplicate .wav file '{filePath}': {ex.Message}");
                    }
                }
            }

            // 3) Remove songs whose files were deleted outside the app.
            var filesInFolder = new System.Collections.Generic.HashSet<string>(
                Directory.GetFiles(_audioDirectory)
                .Where(f => Path.GetExtension(f).ToLower() == ".mp3" || Path.GetExtension(f).ToLower() == ".wav")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != null)!
            );

            bool playlistsChangedByMissingFiles = false;
            for (int i = Songs.Count - 1; i >= 0; i--)
            {
                Song song = Songs[i];
                string? songName = song.Name;
                if (songName != null && !filesInFolder.Contains(songName))
                {
                    bool playlistsChanged = RemoveSongFromAllPlaylists(song);
                    Songs.RemoveAt(i);
                    LikedSongs.Remove(song);
                    if (AreSameSongById(CurrentSong, song))
                    {
                        CurrentSong = null;
                        IsPlaying = false;
                    }

                    if (playlistsChanged)
                    {
                        playlistsChangedByMissingFiles = true;
                    }
                }
            }

            if (playlistsChangedByMissingFiles)
            {
                SaveCreatedPlaylists();
            }

            // 4) Add new files and backfill missing duration metadata.
            foreach (string filePath in Directory.GetFiles(_audioDirectory))
            {
                string extension = Path.GetExtension(filePath).ToLower();
                if (extension == ".mp3" || extension == ".wav")
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    var existingSong = Songs.FirstOrDefault(s =>
                        string.Equals(s.Name, fileName, StringComparison.OrdinalIgnoreCase));

                    if (existingSong != null)
                    {
                        if (string.IsNullOrWhiteSpace(existingSong.Duration))
                        {
                            existingSong.Duration = GetAudioDuration(filePath);
                        }
                        continue;
                    }

                    var newSong = new Song
                    {
                        Name = fileName,
                        Volume = 50,
                        Duration = GetAudioDuration(filePath)
                    };
                    Songs.Add(newSong);
                }
            }

            SaveSongData();
            SyncActivePlaylistSongs();
        }

        /// <summary>
        /// Reads audio duration from media metadata.
        /// Returns an empty string if metadata cannot be read.
        /// </summary>
        private static string GetAudioDuration(string filePath)
        {
            try
            {
                using var tagFile = TagLib.File.Create(filePath);
                var duration = tagFile.Properties.Duration;
                return duration.ToString(@"mm\:ss");
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Removes the specified song from the currently active playlist view without deleting it from the library.
        /// No-op when not viewing a playlist or when identifiers are missing.
        /// </summary>
        [RelayCommand]
        public void RemoveSongFromActivePlaylist(Song song)
        {
            if (song == null || ActivePlaylist == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(song.Id) || string.IsNullOrWhiteSpace(ActivePlaylist.Id))
            {
                return;
            }

            if (ActivePlaylist.SongIds == null)
            {
                return;
            }

            bool removedFromPlaylist = ActivePlaylist.SongIds.RemoveAll(songId =>
                string.Equals(songId, song.Id, StringComparison.Ordinal)) > 0;

            if (!removedFromPlaylist)
            {
                return;
            }

            if (song.PlaylistIds != null)
            {
                song.PlaylistIds.RemoveAll(playlistId =>
                    string.Equals(playlistId, ActivePlaylist.Id, StringComparison.Ordinal));
            }

            SyncActivePlaylistSongs();
            SaveCreatedPlaylists();
            SaveSongData();
        }

        private bool RemoveSongFromAllPlaylists(Song song)
        {
            if (string.IsNullOrWhiteSpace(song.Id))
            {
                return false;
            }

            bool changed = false;
            foreach (CreatedPlaylist playlist in CreatedPlaylists)
            {
                if (playlist.SongIds.RemoveAll(songId => string.Equals(songId, song.Id, StringComparison.Ordinal)) > 0)
                {
                    changed = true;
                }
            }

            if (song.PlaylistIds.Count > 0)
            {
                song.PlaylistIds.Clear();
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Sets the playlist currently shown in the main list and syncs ActivePlaylistSongs from it.
        /// Pass null when switching to All or Liked.
        /// </summary>
        public void SetActivePlaylist(CreatedPlaylist? playlist)
        {
            ActivePlaylist = playlist;
            SyncActivePlaylistSongs();
        }

        /// <summary>
        /// Rebuilds ActivePlaylistSongs from ActivePlaylist's SongIds and Songs. Call after Songs or playlist membership changes.
        /// </summary>
        public void SyncActivePlaylistSongs()
        {
            ActivePlaylistSongs.Clear();
            if (ActivePlaylist?.SongIds == null) return;
            foreach (string songId in ActivePlaylist.SongIds)
            {
                var song = Songs.FirstOrDefault(s =>
                    !string.IsNullOrWhiteSpace(s.Id)
                    && string.Equals(s.Id, songId, StringComparison.Ordinal));
                if (song != null)
                    ActivePlaylistSongs.Add(song);
            }
        }

        /// <summary>
        /// Switches the right panel to show all songs and clears any active playlist/editor state.
        /// </summary>
        public void ShowAllSongs()
        {
            ActivePlaylist = null;
            PlaylistEditor = null;
            CurrentHeaderTitle = "All";
            CurrentPage = RightPanelPage.AllSongs;
        }

        /// <summary>
        /// Switches the right panel to show liked songs and clears any active playlist/editor state.
        /// </summary>
        public void ShowLikedSongs()
        {
            ActivePlaylist = null;
            PlaylistEditor = null;
            CurrentHeaderTitle = "Liked";
            CurrentPage = RightPanelPage.LikedSongs;
        }

        /// <summary>
        /// Switches the right panel to show the given playlist's songs.
        /// </summary>
        public void ShowPlaylist(CreatedPlaylist playlist)
        {
            if (playlist == null) return;

            ActivePlaylist = playlist;
            SyncActivePlaylistSongs();
            CurrentHeaderTitle = playlist.Title;
            CurrentPage = RightPanelPage.PlaylistSongs;
        }

        /// <summary>
        /// Enters edit mode for the given playlist and prepares an editor view-model.
        /// </summary>
        public void BeginEditPlaylist(CreatedPlaylist playlist)
        {
            if (playlist == null) return;

            // Remember which page we were on before entering edit mode.
            _previousPage = CurrentPage;

            ActivePlaylist = playlist;
            PlaylistEditor = new EditPlaylistViewModel(
                playlist,
                onSave: CommitPlaylistEdits,
                onCancel: CancelEditPlaylist);
            CurrentPage = RightPanelPage.EditPlaylist;
        }

        private void CommitPlaylistEdits(EditPlaylistViewModel editor)
        {
            if (editor == null) return;
            var playlist = editor.Playlist;

            string newTitle = (editor.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(newTitle))
            {
                _dialogService.ShowMessage("Playlist name cannot be empty.");
                return;
            }

            playlist.Title = newTitle;
            playlist.Description = editor.Description ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(editor.SelectedImageFilePath) && File.Exists(editor.SelectedImageFilePath))
            {
                Directory.CreateDirectory(_playlistIconsDirectory);

                string ext = Path.GetExtension(editor.SelectedImageFilePath);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
                string destPath = Path.Combine(_playlistIconsDirectory, playlist.Id + ext);

                // Remove old icon file if we are replacing it with a new path.
                if (!string.IsNullOrWhiteSpace(playlist.IconPath)
                    && File.Exists(playlist.IconPath)
                    && !string.Equals(playlist.IconPath, destPath, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(playlist.IconPath); } catch { /* non-critical cleanup */ }
                }

                try
                {
                    File.Copy(editor.SelectedImageFilePath, destPath, overwrite: true);

                    // Force WPF bindings to refresh the image even when the path string is unchanged.
                    playlist.IconPath = null;
                    playlist.IconPath = destPath;
                }
                catch
                {
                    // Leave IconPath unchanged if copy fails.
                }
            }

            SaveCreatedPlaylists();

            // Keep header title in sync when user edits the currently active playlist.
            if (ActivePlaylist != null && string.Equals(ActivePlaylist.Id, playlist.Id, StringComparison.Ordinal))
            {
                CurrentHeaderTitle = playlist.Title;
            }

            CancelEditPlaylist();
        }

        public void CancelEditPlaylist()
        {
            PlaylistEditor = null;

            // Default to going back to the page we were on before editing.
            var targetPage = _previousPage;

            // Safety: if previous page is invalid or still EditPlaylist, choose a sensible default.
            if (targetPage == RightPanelPage.None || targetPage == RightPanelPage.EditPlaylist)
            {
                targetPage = ActivePlaylist != null
                    ? RightPanelPage.PlaylistSongs
                    : RightPanelPage.AllSongs;
            }

            CurrentPage = targetPage;
            _previousPage = RightPanelPage.None;
        }

        private static bool AreSameSongById(Song? left, Song? right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (ReferenceEquals(left, right))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(left.Id)
                && !string.IsNullOrWhiteSpace(right.Id)
                && string.Equals(left.Id, right.Id, StringComparison.Ordinal);
        }

        /// <summary>
        /// Adds a new playlist to the collection and persists updated playlist data.
        /// </summary>
        [RelayCommand]
        public void CreatePlaylist(CreatedPlaylist playlist)
        {
            if (playlist == null) return;
            if (string.IsNullOrWhiteSpace(playlist.Id))
            {
                playlist.Id = Guid.NewGuid().ToString("N");
            }

            playlist.SongIds ??= new List<string>();
            playlist.LegacySongNames = null;
            CreatedPlaylists.Add(playlist);
            SaveCreatedPlaylists();
        }

        /// <summary>
        /// Deletes a playlist and its icon file (if present), then persists changes.
        /// </summary>
        [RelayCommand]
        public void DeletePlaylist(CreatedPlaylist playlist)
        {
            if (playlist == null) return;

            bool songsChanged = false;
            if (!string.IsNullOrWhiteSpace(playlist.Id))
            {
                foreach (Song song in Songs)
                {
                    if (song.PlaylistIds.RemoveAll(id => string.Equals(id, playlist.Id, StringComparison.Ordinal)) > 0)
                    {
                        songsChanged = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(playlist.IconPath) && File.Exists(playlist.IconPath))
            {
                try { File.Delete(playlist.IconPath); } catch { /* Non-critical */ }
            }

            CreatedPlaylists.Remove(playlist);
            SaveCreatedPlaylists();
            if (ReferenceEquals(playlist, ActivePlaylist))
            {
                SetActivePlaylist(null);
            }
            if (songsChanged)
            {
                SaveSongData();
            }
        }

        /// <summary>
        /// Sorts Songs by the selected strategy and persists the re-ordered list.
        /// </summary>
        [RelayCommand]
        public void SortPlaylist(string sortType)
        {
            List<Song> sortedItems = new List<Song>();

            if (sortType == "Alphabetical")
            {
                sortedItems = _playlistSortService.SortAlphabetically(Songs.ToList());
            }
            else if (sortType == "ModificationDate")
            {
                sortedItems = _playlistSortService.SortByModificationDate(Songs.ToList(), _audioDirectory);
            }

            Songs.Clear();
            int count = 1;
            foreach (var song in sortedItems)
            {
                song.Index = count.ToString("D2");
                Songs.Add(song);
                count++;
            }
            SaveSongData();
        }
    }
}
