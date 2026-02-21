using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mei_Music.Models;
using Mei_Music.Services;

namespace Mei_Music.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IFileService _fileService;
        private readonly IPlaylistSortService _playlistSortService;
        private readonly IAudioPlayerService _audioPlayer;
        private readonly IDialogService _dialogService;

        private readonly string _songDataFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "songData.json");
        private readonly string _playlistsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlists.json");
        private readonly string _playlistIconsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist-icons");
        private readonly string _audioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");

        public MainViewModel(IFileService fileService, IPlaylistSortService playlistSortService, IAudioPlayerService audioPlayer, IDialogService dialogService)
        {
            _fileService = fileService;
            _playlistSortService = playlistSortService;
            _audioPlayer = audioPlayer;
            _dialogService = dialogService;

            Songs = new ObservableCollection<Song>();
            LikedSongs = new ObservableCollection<Song>();
            CreatedPlaylists = new ObservableCollection<CreatedPlaylist>();

            _audioPlayer.MediaOpened += OnMediaOpened;
            _audioPlayer.MediaEnded += OnMediaEnded;
            _audioPlayer.PositionChanged += OnPositionChanged;
        }

        // --- Core Collections ---
        public ObservableCollection<Song> Songs { get; }
        public ObservableCollection<Song> LikedSongs { get; }
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

        public void UpdatePlayerVolume()
        {
            if (CurrentSong != null)
            {
                _audioPlayer.Volume = CurrentSong.Volume;
            }
        }

        // --- Song Action Commands ---

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

        [RelayCommand]
        public void RenameSong(Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.Name)) return;

            string oldName = song.Name;
            string? newName = _dialogService.PromptInput("Enter a new name for the file:", "Rename Song", oldName);

            if (!string.IsNullOrEmpty(newName) && newName != oldName)
            {
                string newFilePath = Path.Combine(_audioDirectory, newName + ".mp3");

                if (File.Exists(newFilePath))
                {
                    _dialogService.ShowMessage($"A file named \"{newName}\" already exists in the playlist. Please choose a different name.", "Duplicate Name");
                    return;
                }

                // Update model
                var songToReplace = Songs.FirstOrDefault(s => s.Name == oldName);
                if (songToReplace != null)
                {
                    int index = Songs.IndexOf(songToReplace);
                    Songs.Remove(songToReplace);

                    var newSongInfo = new Song
                    {
                        Index = songToReplace.Index,
                        Name = newName,
                        Volume = songToReplace.Volume,
                        IsLiked = songToReplace.IsLiked
                    };

                    Songs.Insert(index, newSongInfo);
                    if (CurrentSong?.Name == oldName)
                    {
                        CurrentSong = newSongInfo;
                    }
                    SaveSongData();
                }

                // Rename files
                string oldMp3 = Path.Combine(_audioDirectory, oldName + ".mp3");
                string newMp3 = Path.Combine(_audioDirectory, newName + ".mp3");
                string oldWav = Path.Combine(_audioDirectory, oldName + ".wav");
                string newWav = Path.Combine(_audioDirectory, newName + ".wav");

                try
                {
                    if (File.Exists(oldMp3)) File.Move(oldMp3, newMp3);
                    if (File.Exists(oldWav)) File.Move(oldWav, newWav);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowMessage($"Error renaming file: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        public void DeleteSong(Song song)
        {
            if (song == null || string.IsNullOrEmpty(song.Name)) return;

            if (_dialogService.ShowDeleteSongConfirmation(song.Name))
            {
                var songToRemove = Songs.FirstOrDefault(s => s.Name == song.Name);
                if (songToRemove != null)
                {
                    Songs.Remove(songToRemove);
                    SaveSongData();
                }

                string mp3 = Path.Combine(_audioDirectory, song.Name + ".mp3");
                string wav = Path.Combine(_audioDirectory, song.Name + ".wav");

                if (File.Exists(mp3)) File.Delete(mp3);
                if (File.Exists(wav)) File.Delete(wav);

                RefreshSongsInUI();
            }
        }

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

        [RelayCommand]
        public void SongVolume(Song song)
        {
            if (song == null) return;

            _dialogService.ShowSongVolumeDialog(song, newVolume =>
            {
                song.Volume = newVolume;
                if (CurrentSong != null && CurrentSong.Name == song.Name)
                {
                    UpdatePlayerVolume();
                }
                SaveSongData();
            });
        }

        // --- Data Management & Refresh ---

        public void SaveSongData()
        {
            UpdateSongIndexes();
            try { _fileService.SaveSongs(_songDataFilePath, Songs.ToList()); }
            catch (Exception ex) { _dialogService.ShowMessage($"Error saving song data: {ex.Message}"); }
        }

        public void SaveCreatedPlaylists()
        {
            try { _fileService.SavePlaylists(_playlistsFilePath, CreatedPlaylists.ToList()); }
            catch (Exception ex) { _dialogService.ShowMessage($"Error saving playlists: {ex.Message}"); }
        }

        public void UpdateSongIndexes()
        {
            for (int i = 0; i < Songs.Count; i++)
            {
                Songs[i].Index = (i + 1).ToString("D2");
            }
        }

        [RelayCommand]
        public void RefreshSongsInUI()
        {
            bool nonAudioFilesFound = false;

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

            var filesInFolder = new System.Collections.Generic.HashSet<string>(
                Directory.GetFiles(_audioDirectory)
                .Where(f => Path.GetExtension(f).ToLower() == ".mp3" || Path.GetExtension(f).ToLower() == ".wav")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n != null)!
            );

            for (int i = Songs.Count - 1; i >= 0; i--)
            {
                string? songName = Songs[i]?.Name;
                if (songName != null && !filesInFolder.Contains(songName))
                {
                    Songs.RemoveAt(i);
                }
            }

            foreach (string filePath in Directory.GetFiles(_audioDirectory))
            {
                string extension = Path.GetExtension(filePath).ToLower();
                if (extension == ".mp3" || extension == ".wav")
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);
                    var existingSong = Songs.FirstOrDefault(s => s.Name == fileName);

                    if (existingSong != null) continue;

                    var newSong = new Song
                    {
                        Name = fileName,
                        Volume = 50
                    };
                    Songs.Add(newSong);
                }
            }

            SaveSongData();
        }
        [RelayCommand]
        public void CreatePlaylist(CreatedPlaylist playlist)
        {
            if (playlist == null) return;
            CreatedPlaylists.Add(playlist);
            SaveCreatedPlaylists();
        }

        [RelayCommand]
        public void DeletePlaylist(CreatedPlaylist playlist)
        {
            if (playlist == null) return;

            if (!string.IsNullOrEmpty(playlist.IconPath) && File.Exists(playlist.IconPath))
            {
                try { File.Delete(playlist.IconPath); } catch { /* Non-critical */ }
            }

            CreatedPlaylists.Remove(playlist);
            SaveCreatedPlaylists();
        }

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
