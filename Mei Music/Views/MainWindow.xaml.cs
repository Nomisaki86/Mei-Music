using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using AudioSwitcher.AudioApi.CoreAudio;
using Mei_Music.Properties;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using Mei_Music.Models;
using Mei_Music.Services;




namespace Mei_Music
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private bool isPlaying = false;
        private bool isDragging = false;
        private Slider? currentSlider;   
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private readonly DispatcherTimer timer = new DispatcherTimer();
        private CoreAudioDevice? defaultPlaybackDevice;
        private readonly FileService fileService = new FileService();
        private readonly PlaylistSortService playlistSortService = new PlaylistSortService();

        private Song? currentSong;
        public Song? CurrentSong
        {
            get => currentSong;
            set
            {
                currentSong = value;
                if (currentSong != null)
                {
                    mediaPlayer.Volume = currentSong.Volume;
                }
                OnPropertyChanged(nameof(CurrentSong));
            }
        }
        public ObservableCollection<Song> Songs { get; set; } = new ObservableCollection<Song>();
        public ObservableCollection<Song> LikedSongs { get; } = new ObservableCollection<Song>();
        public event PropertyChangedEventHandler? PropertyChanged;
        private string songDataFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "songData.json");
        /// <summary>List used for Prev/Next and auto-next; set when user starts playing from the list.</summary>
        private IList? _playbackList;

        public MainWindow()
        {

            InitializeComponent();

            string audioDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
            Directory.CreateDirectory(audioDirectory);

            LoadSongData();
            ApplyAllSongsView();
            RefreshSongsInUI();
            LoadSongIndex();

            UpdateSongIndexes();



            if (UploadedSongList.SelectedIndex >= 0 && UploadedSongList.SelectedIndex < Songs.Count)
            {
                CurrentSong = Songs[UploadedSongList.SelectedIndex];
                mediaPlayer.Volume = CurrentSong.Volume / 100.0;
            }

            InitializeMediaPlayer();
            UpdatePlaybackButtonIcon();
            this.PreviewMouseDown += OnMainWindowPreviewMouseDown; //tracks Position of mouse
        }

        /// <summary>
        /// Binds the right panel to the complete song collection.
        /// This is the source used when the sidebar "All" option is selected.
        /// Skip Refresh() when switching source so tab change is instant; Refresh only when re-applying same source.
        /// </summary>
        private void ApplyAllSongsView()
        {
            if (UploadedSongList == null)
            {
                return;
            }

            if (!ReferenceEquals(UploadedSongList.ItemsSource, Songs))
            {
                UploadedSongList.ItemsSource = Songs;
            }
            else
            {
                UploadedSongList.Items.Refresh();
            }
            SyncSelectionToCurrentSong();
        }

        /// <summary>
        /// Returns the list currently bound to the song list (Songs or LikedSongs), or null.
        /// </summary>
        private static IList? GetCurrentSongList(ItemsControl listBox)
        {
            return listBox?.ItemsSource as IList;
        }

        /// <summary>
        /// Finds the index of the given song in the list by reference or by Name, or -1 if not found.
        /// </summary>
        private static int IndexOfSongInList(IList list, Song? song)
        {
            if (list == null || song == null) return -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Song s && (ReferenceEquals(s, song) || s.Name == song.Name))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// After changing the list (All/Liked) or after Prev/Next, sync selection to the currently playing song.
        /// If CurrentSong is in the visible list, select it; otherwise clear selection so no row is highlighted.
        /// Does not restart playback.
        /// </summary>
        private void SyncSelectionToCurrentSong()
        {
            if (UploadedSongList == null) return;
            var list = GetCurrentSongList(UploadedSongList);
            if (list == null || list.Count == 0) return;
            if (CurrentSong == null)
            {
                UploadedSongList.SelectedIndex = -1;
                return;
            }
            int index = IndexOfSongInList(list, CurrentSong);
            UploadedSongList.SelectedIndex = index >= 0 ? index : -1;
        }

        /// <summary>
        /// Binds the right panel to user-liked songs.
        /// Skip Refresh() when switching source so tab change is instant; Refresh only when re-applying same source.
        /// </summary>
        private void ApplyLikedSongsView()
        {
            if (UploadedSongList == null)
            {
                return;
            }

            if (!ReferenceEquals(UploadedSongList.ItemsSource, LikedSongs))
            {
                UploadedSongList.ItemsSource = LikedSongs;
            }
            else
            {
                UploadedSongList.Items.Refresh();
            }
            SyncSelectionToCurrentSong();
        }

        private void AllSongsButton_Checked(object sender, RoutedEventArgs e)
        {
            // Keep the right-side list in sync with the selected sidebar scope.
            ApplyAllSongsView();
        }

        private void LikedSongsButton_Checked(object sender, RoutedEventArgs e)
        {
            // This view is scaffolded now; like/unlike wiring will populate it later.
            ApplyLikedSongsView();
        }

        /// <summary>
        /// Wires media/timer events and syncs the volume slider to the current
        /// default playback device so app controls match system state.
        /// </summary>
        private void InitializeMediaPlayer()
        {
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened; //detect for opened media
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;   //detect for ended media

            var controller = new CoreAudioController();
            defaultPlaybackDevice = controller.DefaultPlaybackDevice;
            if (defaultPlaybackDevice != null)
            {
                VolumeSlider.Value = defaultPlaybackDevice.Volume; // Set initial value from system volume
            }
        }
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Updates the transport icon to reflect current playback state.
        /// In this project UI: pause icon = idle/paused, play icon = currently playing.
        /// </summary>
        private void UpdatePlaybackButtonIcon()
        {
            if (StopSongButton.Content is not Image playbackIcon)
            {
                return;
            }

            string iconPath = isPlaying
                ? "/Resources/Images/play_button.png"
                : "/Resources/Images/pause_button.png";

            playbackIcon.Source = new BitmapImage(new Uri(iconPath, UriKind.Relative));
        }

        //------------------------- Add Audio Implementation -------------------------------
        internal void AddSongToList(string name)
        {
            var song = new Song
            {
                Index = (Songs.Count + 1).ToString("D2"),
                Name = name
            };

            Songs.Add(song);
            UpdateSongData();
        }
        private void RemoveSongFromList(string name)
        {
            // Find the song with the specified name in the list
            var songToRemove = Songs.OfType<Song>().FirstOrDefault(song => song.Name == name);

            if (songToRemove != null)
            {
                Songs.Remove(songToRemove);
                UpdateSongData();
            }
        }
        private void UploadFromComputer_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Media Files (*.mp3;*.wav;*.mp4,*.mkv;) | *.mp3;*.wav;*.mp4;*.mkv", // description | files shown
                Multiselect = false
            };

            if (ofd.ShowDialog() == true) //if the user chosed a file or 
            {
                string selectedFile = ofd.FileName;
                string fileExtension = System.IO.Path.GetExtension(selectedFile).ToLower(); //allows selection of file from local folder
                string outputDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
                Directory.CreateDirectory(outputDirectory); // Ensure the directory structure exists

                //is video file
                if (fileExtension == ".mp4" || fileExtension == ".mkv")
                {
                    string audioFilePath = ConvertVideoToAudio(selectedFile); //get the path to audio file
                    if (audioFilePath != null)       
                    {
                        AddFileToUI(audioFilePath);
                    }
                }
                //is audio file
                else if (fileExtension == ".wav" || fileExtension == ".mp3")
                {
                    string audioFilePath = System.IO.Path.Combine(outputDirectory, System.IO.Path.GetFileName(selectedFile));
                    AddFileToUI(audioFilePath);
                    File.Copy(selectedFile, audioFilePath, overwrite: true); // Copy file to output directory
                }
            }
        }
        private void SearchThroughURL_Click(object sender, RoutedEventArgs e)
        {
            SearchThroughURLWindow window = new SearchThroughURLWindow(this);
            window.Show();
        }
        internal string ConvertVideoToAudio(string videoFilePath) //perform conversion from video to audio
        {
            try
            {
                string outputDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
                Directory.CreateDirectory(outputDirectory); // Ensure the directory structure exists

                string audioFilePath = System.IO.Path.Combine(outputDirectory, System.IO.Path.GetFileNameWithoutExtension(videoFilePath) + ".mp3");

                string ffmpegPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "ffmpeg", "ffmpeg.exe");

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    //q:a 0 set audio quality to best. map a allows extraction of only audio -y allows to overwrite output files
                    Arguments = $"-i \"{videoFilePath}\" -q:a 0 -map a \"{audioFilePath}\" -y",
                    RedirectStandardOutput = true, //capture processing info
                    RedirectStandardError = true,  //capture processing error
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process? process = Process.Start(startInfo); //the ? indicate that process may be null
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

        /// <summary>
        /// Adds a track entry to the playlist UI and handles name collisions by
        /// prompting the user to replace, rename, or cancel.
        /// </summary>
        internal void AddFileToUI(string filePath)
        {
            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath); //get file name

            bool isDuplicate = Songs
                                .OfType<Song>()
                                .Any(song => song.Name == fileNameWithoutExtension); 

            if (isDuplicate) //if name already exists in the list
            {
                DuplicateFileDialog dialog = new DuplicateFileDialog();
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    // Keep playlist metadata aligned with the duplicate action selected by the user.
                    switch (dialog.SelectedAction)
                    {
                        case DuplicateFileDialog.DuplicateFileAction.Replace:
                            ReplaceFileInUI(filePath);
                            break;

                        case DuplicateFileDialog.DuplicateFileAction.Rename:
                            PromptForNewName(filePath);
                            break;

                        case DuplicateFileDialog.DuplicateFileAction.Cancel:
                            return;
                    }
                }
            }
            else //no duplicate
            {
                AddSongToList(fileNameWithoutExtension); //add the file name of the audio path to the viewport list
            }
        }
        private void ReplaceFileInUI(string filePath)
        {
            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
            RemoveSongFromList(fileNameWithoutExtension);
            AddSongToList(fileNameWithoutExtension);
        }
        private string PromptForNewName(string filePath)
        {
            string originalName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            string outputDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
            
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a new name for the file:",
                "Create New Entry",
                 originalName);

            if (!string.IsNullOrEmpty(newName))
            {
                // create file name.mp3
                string newFilePath = System.IO.Path.Combine(outputDirectory, newName + ".mp3");

                // Check if the new name already exists in the directory
                if (File.Exists(newFilePath))
                {
                    MessageBox.Show($"A file named \"{newName}\" already exists in the playlist. Please choose a different name.", "Duplicate Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return originalName;
                }
                else
                {
                    // Copy the original file to the new file path with the new name
                    File.Copy(filePath, newFilePath);

                    // Add the new name to the playlist in the UI
                    AddSongToList(newName);
                    return newName;
                }
            }
            return originalName; // Return original name if no change
        }
        private void PlusButton_Click(object sender, RoutedEventArgs e)
        {
            // Open the dropdown and disable the button
            PlusPopupMenu.IsOpen = true;
            PlusButton.IsEnabled = false;
        }
        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            SortPopupMenu.IsOpen = true;
            SortButton.IsEnabled = false;
        }

        //----------------------------------------------------------------------------------


        //-------------------------  Song Functionality Implementation ---------------------
        /// <summary>
        /// Loads and plays the given song. Does not set _playbackList or call SaveSongIndex.
        /// Returns true if playback started, false if file not found.
        /// </summary>
        private bool PlaySong(Song song)
        {
            CurrentSong = song;
            if (CurrentSong != null)
            {
                mediaPlayer.Volume = CurrentSong.Volume;
                UpdateSongVolumeSlider(CurrentSong.Volume * 100);
            }
            string? fileName = song.Name;
            string audioDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
            string mp3FilePath = System.IO.Path.Combine(audioDirectory, fileName + ".mp3");
            string wavFilePath = System.IO.Path.Combine(audioDirectory, fileName + ".wav");
            string? audioFilePath = File.Exists(mp3FilePath) ? mp3FilePath : (File.Exists(wavFilePath) ? wavFilePath : null);
            if (audioFilePath == null)
            {
                isPlaying = false;
                UpdatePlaybackButtonIcon();
                MessageBox.Show("The selected file could not be found.");
                return false;
            }
            mediaPlayer.Stop();
            mediaPlayer.Open(new Uri(audioFilePath));
            mediaPlayer.Volume = song.Volume / 100.0;
            mediaPlayer.Play();
            isPlaying = true;
            UpdatePlaybackButtonIcon();
            return true;
        }

        /// <summary>
        /// Resolves the selected song to an existing local file, updates player state,
        /// and starts playback while restoring the per-song volume.
        /// </summary>
        private void PlaySelectedSong(object sender, SelectionChangedEventArgs? e)
        {
            if (UploadedSongList.SelectedItem is not Song selectedSong)
                return;

            // Sync-only selection (e.g. after tab switch): same song already playing — do not restart.
            if ((ReferenceEquals(selectedSong, CurrentSong) || selectedSong.Name == CurrentSong?.Name) && mediaPlayer.Source != null)
            {
                if (CurrentSong != null)
                {
                    mediaPlayer.Volume = CurrentSong.Volume / 100.0;
                    UpdateSongVolumeSlider(CurrentSong.Volume * 100);
                }
                return;
            }

            _playbackList = GetCurrentSongList(UploadedSongList);
            if (PlaySong(selectedSong))
                SaveSongIndex();
        }
        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }

                var result = FindVisualChild<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }
        private void PreviousSongClicked(object sender, RoutedEventArgs e)
        {
            var list = _playbackList ?? GetCurrentSongList(UploadedSongList) ?? Songs;
            if (list == null || list.Count == 0) return;
            int currentIndex = IndexOfSongInList(list, CurrentSong);
            if (currentIndex < 0) currentIndex = 0;
            int prevIndex = (currentIndex - 1 + list.Count) % list.Count;
            if (list[prevIndex] is not Song prevSong) return;
            PlaySong(prevSong);
            SyncSelectionToCurrentSong();
            if (ReferenceEquals(GetCurrentSongList(UploadedSongList), _playbackList))
                SaveSongIndex();
        }
        private void StopSongClicked(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
            {
                mediaPlayer.Pause();
                isPlaying = false;
                UpdatePlaybackButtonIcon();
            }
            else
            {
                if (mediaPlayer.Source != null)
                {
                    mediaPlayer.Play();
                    isPlaying = true;
                    UpdatePlaybackButtonIcon();
                }
                else
                {
                    if (UploadedSongList.SelectedItem == null)
                    {
                        MessageBox.Show("Please select a song to play.");
                        return;
                    }
                    PlaySelectedSong(this, null);
                }
            }
        }
        private void NextSongClicked(object sender, RoutedEventArgs e)
        {
            var list = _playbackList ?? GetCurrentSongList(UploadedSongList) ?? Songs;
            if (list == null || list.Count == 0) return;
            int currentIndex = IndexOfSongInList(list, CurrentSong);
            if (currentIndex < 0) currentIndex = 0;
            int nextIndex = (currentIndex + 1) % list.Count;
            if (list[nextIndex] is not Song nextSong) return;
            PlaySong(nextSong);
            SyncSelectionToCurrentSong();
            if (ReferenceEquals(GetCurrentSongList(UploadedSongList), _playbackList))
                SaveSongIndex();
        }
        private void MediaPlayer_MediaOpened(object? sender, EventArgs e)
        {
            //get info from opened media
            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                TimeSpan totalDuration = mediaPlayer.NaturalDuration.TimeSpan;
                SongLength_Timer.Content = totalDuration.ToString(@"mm\:ss");
                SongProgressSlider.Maximum = totalDuration.TotalSeconds;
            }
        }
        private void MediaPlayer_MediaEnded(object? sender, EventArgs e)
        {
            var list = _playbackList ?? GetCurrentSongList(UploadedSongList) ?? Songs;
            if (list == null || list.Count == 0) return;
            int currentIndex = IndexOfSongInList(list, CurrentSong);
            if (currentIndex < 0) currentIndex = 0;
            int nextIndex = (currentIndex + 1) % list.Count;
            if (list[nextIndex] is not Song nextSong) return;
            PlaySong(nextSong);
            SyncSelectionToCurrentSong();
            if (ReferenceEquals(GetCurrentSongList(UploadedSongList), _playbackList))
                SaveSongIndex();
        }
        private void Timer_Tick(object? sender, EventArgs e)
        {
            //for updating timer value and progress slider
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                SongProgressSlider.Value = mediaPlayer.Position.TotalSeconds;
                SongProgress_Timer.Content = mediaPlayer.Position.ToString(@"mm\:ss");
                SongLength_Timer.Content = mediaPlayer.NaturalDuration.TimeSpan.ToString(@"mm\:ss");
            }
        }
        private void DeleteSong_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button deleteSong && deleteSong.Tag is Song song)
            {
                string? fileNameWithoutExtension = song.Name;

                if (string.IsNullOrEmpty(fileNameWithoutExtension)) return;

                var dialog = new DeleteSongConfirmationWindow($"Are you sure you want to delete '{fileNameWithoutExtension}'?");
                dialog.Owner = this;
                dialog.ShowDialog();

                if (dialog.IsConfirmed)
                {
                    var songToRemove = Songs.FirstOrDefault(song => song.Name == fileNameWithoutExtension);
                    if (songToRemove != null)
                    {
                        Songs.Remove(songToRemove); // Removes directly from the ObservableCollection
                        UpdateSongData();
                    }
                    DeleteSong(fileNameWithoutExtension);

                }
            }
        }
        private void DeleteSong(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension)) return;

            string audioDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
            string audioFile_mp3 = System.IO.Path.Combine(audioDirectory, fileNameWithoutExtension + ".mp3");
            string audioFile_wav = System.IO.Path.Combine(audioDirectory, fileNameWithoutExtension + ".wav");

            string? audioFilePath = null;

            if (File.Exists(audioFile_mp3))
            {
                audioFilePath = audioFile_mp3;
            }
            if (File.Exists(audioFile_wav))
            {
                audioFilePath = audioFile_wav;
            }

            if (audioFilePath != null)
            {
                File.Delete(audioFilePath);
                RefreshSongsInUI();
            }
        }
        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button folderButton && folderButton.Tag is Song song)
            {
                // Retrieve the file name from the Song object's Name property
                string? fileNameWithoutExtension = song.Name;

                // Construct the full file path
                string audioDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
                string audioFile_mp3 = System.IO.Path.Combine(audioDirectory, fileNameWithoutExtension + ".mp3");
                string audioFile_wav = System.IO.Path.Combine(audioDirectory, fileNameWithoutExtension + ".wav");

                string? audioFilePath = null;

                if (File.Exists(audioFile_mp3))
                {
                    audioFilePath = audioFile_mp3;
                }
                if (File.Exists(audioFile_wav))
                {
                    audioFilePath = audioFile_wav;
                }

                if (audioFilePath != null)
                {
                    // Use Process to open the file location with the file selected
                    var psi = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{audioFilePath}\""
                    };
                    Process.Start(psi);
                }
                else
                {
                    MessageBox.Show("File not found in storage.");
                }
            }
        }
        private void RenameSong_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button renameButton && renameButton.Tag is string fileName)
            {
                // Define the output directory
                string outputDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");

                // Construct the full path to the file
                string filePath = System.IO.Path.Combine(outputDirectory, fileName + ".mp3");

                // Call PromptForNewName with the constructed file path
                string newFileName = PromptForNewName(filePath);

                // Check if the new name is different from the old name
                if (!string.IsNullOrEmpty(newFileName) && newFileName != fileName)
                {
                    // Delete the old song file only if the name has changed
                    DeleteSong(fileName);
                }
            }
            else
            {
                MessageBox.Show("File name not found in the Tag property.");
            }
        }

        /// <summary>
        /// Toggles the song's liked state, updates LikedSongs, and persists to disk.
        /// Same Song instance is kept in Songs and LikedSongs so UI stays in sync.
        /// </summary>
        private void ToggleLike_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button likeButton || likeButton.Tag is not Song song)
            {
                return;
            }

            song.IsLiked = !song.IsLiked;
            if (song.IsLiked)
            {
                if (!LikedSongs.Contains(song))
                {
                    LikedSongs.Add(song);
                }
            }
            else
            {
                LikedSongs.Remove(song);
            }

            SaveSongData(Songs.ToList());
        }

        private void SaveSongIndex()
        {
            Properties.Settings.Default.LastSelectedIndex = UploadedSongList.SelectedIndex;
            Properties.Settings.Default.Save();
        }
        private void LoadSongIndex()
        {
            UploadedSongList.SelectionChanged -= PlaySelectedSong;

            int SongIndex = Properties.Settings.Default.LastSelectedIndex;
            if (SongIndex >= 0 && SongIndex < Songs.Count)
            {
                UploadedSongList.SelectedIndex = SongIndex;
            }

            UploadedSongList.SelectionChanged += PlaySelectedSong;
            isPlaying = false;
            UpdatePlaybackButtonIcon();
        }

        //--------------------------- Slider And Volume ------------------------------------
        private void SetSystemVolume(double volume)
        {
            // Helper method to set system volume
            if (defaultPlaybackDevice != null)
            {
                defaultPlaybackDevice.Volume = volume;
            }
        }
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!isDragging) // Update volume only when not dragging
            {
                SetSystemVolume(VolumeSlider.Value);
            }
        }
        private void SongProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Slider_PreviewMouseLeftButtonDown(sender, e);
            SongProgressSlider.CaptureMouse();
        }
        private void SongProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Slider_PreviewMouseLeftButtonUp(sender, e);
            mediaPlayer.Position = TimeSpan.FromSeconds(SongProgressSlider.Value); // Seek to the selected position
            SongProgressSlider.ReleaseMouseCapture();
        }
        private void SongProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(isDragging || mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                TimeSpan currentTime = TimeSpan.FromSeconds(SongProgressSlider.Value);
                SongProgress_Timer.Content = currentTime.ToString(@"mm\:ss");
            }
        }
        private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Event handler for mouse down (start dragging)
            if (sender is Slider slider)
            {
                isDragging = true;
                currentSlider = slider;
                MoveSliderToMousePosition(slider, e);
                slider.CaptureMouse(); // Capture the mouse to receive events outside the bounds
            }
        }
        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Event handler for mouse up (stop dragging)
            isDragging = false;
            currentSlider?.ReleaseMouseCapture();
            currentSlider = null;
        }
        private void Slider_MouseMove(object sender, MouseEventArgs e)
        {
            // Event handler for mouse move (dragging)
            if (isDragging && currentSlider != null)
            {
                MoveSliderToMousePosition(currentSlider, e);
                if (currentSlider == VolumeSlider)
                {
                    // Update volume in real-time as the slider is dragged
                    SetSystemVolume(VolumeSlider.Value);
                }
            }
        }
        private void MoveSliderToMousePosition(Slider slider, MouseEventArgs e)
        {
            // Common method to move any slider to the mouse position
            var mousePosition = e.GetPosition(slider);
            double percentage = mousePosition.X / slider.ActualWidth;
            slider.Value = percentage * (slider.Maximum - slider.Minimum) + slider.Minimum;
            // Update media volume if we're working with VolumeSlider
            if (slider == VolumeSlider && defaultPlaybackDevice != null)
            {
                SetSystemVolume(slider.Value);
            }
        }
        private void SongVolumeButton_Click(object sender, RoutedEventArgs e)
        {

            if (sender is Button button && button.DataContext is Song song)
            {
               
                // Initialize SongVolumeController with the correct volume
                var songVolumeController = new SongVolumeController(song.Volume)
                {
                    Owner = this,
                    VolumeChangedCallback = newVolume =>
                    {
                        // Update the song’s volume
                        song.Volume = newVolume;

                        // Apply to mediaPlayer if this song is playing
                        string? songName = song.Name;
                        if (!string.IsNullOrEmpty(songName) &&
                            mediaPlayer.Source?.OriginalString.Contains(songName) == true)
                        {
                            mediaPlayer.Volume = newVolume / 100.0;
                        }

                        // Save updated song data
                        SaveSongData(Songs.ToList());
                    }
                };

                songVolumeController.ShowDialog();
            }
        }
        //----------------------------------------------------------------------------------



        //------------------------- Sorting Playlist Implementation ------------------------
        private void SortAlphabetically_Click(object sender, RoutedEventArgs e)
        {
            var sortedItems = playlistSortService.SortAlphabetically(Songs);
            RefreshPlaylist(sortedItems);
        }
        private void SortByModification_Click(object sender, RoutedEventArgs e)
        {
            string outputDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
            var sortedItems = playlistSortService.SortByModificationDate(Songs, outputDirectory);
            RefreshPlaylist(sortedItems);
        }
        private void RefreshPlaylist(List<Song> sortedItems)
        {
            Songs.Clear();

            int count = 1;
            foreach (var song in sortedItems)
            {
                song.Index = count.ToString("D2");  // Update index based on sorted order
                Songs.Add(song);
                count++;
            }
            UpdateSongData();
        }

        /// <summary>
        /// Reconciles playlist UI data with files in the playlist folder by cleaning
        /// invalid files, resolving duplicate formats, removing stale entries, and
        /// adding newly discovered songs.
        /// </summary>
        private void RefreshSongsInUI()
        {
            // Step 1: Check for any non audio file. Delete them
            string audioDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
            bool nonAudioFilesFound = false;

            foreach (string filePath in Directory.GetFiles(audioDirectory))
            {
                string extention = System.IO.Path.GetExtension(filePath).ToLower();
                if(extention != ".mp3" && extention != ".wav")
                {
                    nonAudioFilesFound = true;
                    try
                    {
                        // Send to Recycle Bin instead of hard delete to keep recovery possible.
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            filePath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to move file '{filePath}' to the Recycle Bin: {ex.Message}.\nPlease keep user/AppData/Local/MeiMusic/playlist folder clean of only audio files to allow proper functionality.");
                    }
                }
            }

            if (nonAudioFilesFound)
            {
                MessageBox.Show("Some Non-mp3/wav files are detected; they have been moved to the trash bin.\nPlease keep user/AppData/Local/MeiMusic/playlist folder clean of only audio files to allow proper functionality.");
            }

            // Step 2: Delete duplicate file
            var mp3Files = new HashSet<string>();
            foreach (string filePath in Directory.GetFiles(audioDirectory))
            {
                string extension = System.IO.Path.GetExtension(filePath).ToLower();
                string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);

                if (extension == ".mp3")
                {
                    mp3Files.Add(fileNameWithoutExtension);
                }
                else if (extension == ".wav" && mp3Files.Contains(fileNameWithoutExtension))
                {
                    // If .wav file has a corresponding .mp3 file, delete the .wav version
                    try
                    {
                        Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                            filePath,
                            Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                            Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);

                        MessageBox.Show($"Duplicate found for '{fileNameWithoutExtension}'. The .wav version has been deleted.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete duplicate .wav file '{filePath}': {ex.Message}\nPlease don't have duplicate file name in the user/AppData/Local/MeiMusic/playlist folder to allow proper functionality.");
                    }
                }
            }

            // Step 3: Sync UI with Folder - Remove items from the UI if their corresponding files no longer exist
            var filesInFolder = new HashSet<string>(Directory.GetFiles(audioDirectory)
                .Where(file => System.IO.Path.GetExtension(file).ToLower() == ".mp3" || System.IO.Path.GetExtension(file).ToLower() == ".wav")
                .Select(file => System.IO.Path.GetFileNameWithoutExtension(file)));

            for (int i = Songs.Count - 1; i >= 0; i--)
            {
                string? songName = Songs[i]?.Name;
                if (songName != null && !filesInFolder.Contains(songName))
                {
                    Songs.RemoveAt(i);
                    UpdateSongData();
                }
            }

            // Step 4: Add missing MP3/WAV files to UI
            foreach (string filePath in Directory.GetFiles(audioDirectory))
            {
                string extension = System.IO.Path.GetExtension(filePath).ToLower();
                if (extension == ".mp3" || extension == ".wav")
                {
                    string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
                    var existingSong = Songs.FirstOrDefault(s => s.Name == fileNameWithoutExtension);

                    // If song already exists, skip adding a new one to preserve data
                    if (existingSong != null) continue;

                    // Create a new song object only if it doesn't already exist
                    var newSong = new Song
                    {
                        Name = fileNameWithoutExtension,
                        Volume = 50 // Set a default value if the volume is not in the loaded data
                    };

                    Songs.Add(newSong);
                    UpdateSongData();
                }
            }
        }
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // Event handler for the Refresh Button
            RefreshSongsInUI();
        }
        //----------------------------------------------------------------------------------


        //------------------------- Manage Close Drop Down ---------------------------------
        private void OnMainWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Close the Popup if the click is outside the Popup
            if (PlusPopupMenu.IsOpen && !PlusPopupMenu.IsMouseOver)
            {
                PlusPopupMenu.IsOpen = false;
                PlusButton.IsEnabled = true; // Re-enable the button when dropdown closes
            }
            if (SortPopupMenu.IsOpen && !SortPopupMenu.IsMouseOver)
            {
                SortPopupMenu.IsOpen = false;
                SortButton.IsEnabled = true;
            }
        }
        private void Window_Deactivated(object sender, EventArgs e)
        {
            // Close the dropdown if the application loses focus
            if (PlusPopupMenu.IsOpen)
            {
                PlusPopupMenu.IsOpen = false;
                PlusButton.IsEnabled = true; // Ensure the button is re-enabled
            }
            if (SortPopupMenu.IsOpen)
            {
                SortPopupMenu.IsOpen = false;
                SortButton.IsEnabled = true;
            }
        }
        private void SaveSongData(List<Song> songs)
        {
            try
            {
                fileService.SaveSongs(songDataFilePath, songs);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving song data: {ex.Message}");
            }
        }
        private void LoadSongData()
        {
            try
            {
                var loadedSongs = fileService.LoadSongs(songDataFilePath);
                Songs.Clear();
                foreach (var loadedSong in loadedSongs)
                {
                    Songs.Add(loadedSong);
                }

                // Keep LikedSongs in sync with Songs that have IsLiked set (e.g. after load or refresh).
                LikedSongs.Clear();
                foreach (var song in Songs)
                {
                    if (song.IsLiked)
                    {
                        LikedSongs.Add(song);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading song data: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies index updates and persists the normalized playlist back to disk.
        /// Reloading immediately keeps the in-memory collection consistent with
        /// serialization rules from FileService.
        /// </summary>
        private void UpdateSongData()
        {
            UpdateSongIndexes();
            SaveSongData(Songs.ToList());  // Save the current Songs collection to the JSON file
            LoadSongData();                // Reload Songs collection from the updated JSON file
        }
        private void UpdateSongIndexes()
        {
            int indexCounter = 1;
            foreach (var song in Songs)
            {
                song.Index = indexCounter.ToString("D2"); // Set or update the index property
                indexCounter++;
            }
        }
        private void UpdateSongVolumeSlider(double? volume)
        {
            // Attempt to locate the SongVolumeSlider dynamically if it’s not directly accessible
            var songVolumeSlider = FindName("SongVolumeSlider") as Slider;
            if (songVolumeSlider != null)
            {
                songVolumeSlider.Value = volume ?? 50;
            }
        }

        //------------------------- Icon bar implementation --------------------------------
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SaveSongIndex(); // Save before closing
            SaveSongData(Songs.ToList());

            this.Close();
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            SaveSongIndex();
            SaveSongData(Songs.ToList());
        }

        //----------------------------------------------------------------------------------
    }
}