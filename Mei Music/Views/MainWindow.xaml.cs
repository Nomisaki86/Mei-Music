using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using AudioSwitcher.AudioApi;
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
using Mei_Music.ViewModels;

namespace Mei_Music
{
    public partial class MainWindow : Window
    {
        private bool isDragging = false;
        private Slider? currentSlider;
        private CoreAudioDevice? defaultPlaybackDevice;
        private readonly IFileService fileService;
        private readonly IPlaylistSortService playlistSortService;

        private string songDataFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "songData.json");
        private static readonly string PlaylistsFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlists.json");
        private static readonly string PlaylistIconsDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist-icons");

        public MainViewModel ViewModel => (MainViewModel)this.DataContext;

        /// <summary>List used for Prev/Next and auto-next; set when user starts playing from the list.</summary>
        private IList? _playbackList;
        /// <summary>Saved vertical scroll offset for the All (Songs) view when switching tabs.</summary>
        private double _allViewScrollOffset;
        /// <summary>Saved vertical scroll offset for the Liked view when switching tabs.</summary>
        private double _likedViewScrollOffset;
        /// <summary>When true, SelectionChanged is being caused by a programmatic tab switch and should not start playback.</summary>
        private bool _suppressSelectionChanged;
        /// <summary>The playlist currently shown in the right panel, or null for All/Liked views.</summary>
        private CreatedPlaylist? _activePlaylist;
        /// <summary>Saved vertical scroll offset for the last viewed playlist.</summary>
        private double _playlistViewScrollOffset;

        public MainWindow(MainViewModel viewModel, IFileService fileService, IPlaylistSortService playlistSortService)
        {
            this.fileService = fileService;
            this.playlistSortService = playlistSortService;

            InitializeComponent();
            this.DataContext = viewModel;

            string audioDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
            Directory.CreateDirectory(audioDirectory);
            Directory.CreateDirectory(PlaylistIconsDirectory);

            LoadSongData();
            LoadCreatedPlaylists();
            ApplyAllSongsView();
            LoadSongData();
            LoadCreatedPlaylists();
            ApplyAllSongsView();
            ViewModel.RefreshSongsInUI();
            LoadSongIndex();

            UpdateSongIndexes();



            if (UploadedSongList.SelectedIndex >= 0 && UploadedSongList.SelectedIndex < ViewModel.Songs.Count)
            {
                ViewModel.CurrentSong = ViewModel.Songs[UploadedSongList.SelectedIndex];
                ViewModel.Volume = ViewModel.CurrentSong.Volume;
            }

            InitializeMediaPlayer();
            this.PreviewMouseDown += OnMainWindowPreviewMouseDown; //tracks Position of mouse
            CreatePlaylistCard.DragMoveDelta += CreatePlaylistCard_DragMoveDelta;
            ViewModel.CreatedPlaylists.CollectionChanged += (_, _) => UpdatePlaylistSidebarVisibility();
            UpdatePlaylistSidebarVisibility();
        }

        /// <summary>Loads created playlists from disk into CreatedPlaylists.</summary>
        private void LoadCreatedPlaylists()
        {
            var loaded = fileService.LoadPlaylists(PlaylistsFilePath);
            ViewModel.CreatedPlaylists.Clear();
            foreach (var p in loaded)
                ViewModel.CreatedPlaylists.Add(p);
        }

        /// <summary>Persists CreatedPlaylists to disk.</summary>
        private void SaveCreatedPlaylists()
        {
            fileService.SavePlaylists(PlaylistsFilePath, ViewModel.CreatedPlaylists.ToList());
        }

        /// <summary>Shows the in-app create-playlist overlay; card raises CreateClicked or CloseRequested.</summary>
        private void OpenCreatePlaylistDialog(object sender, RoutedEventArgs e)
        {
            CreatePlaylistCard.Reset();
            CreatePlaylistCardTransform.X = 0;
            CreatePlaylistCardTransform.Y = 0;
            CreatePlaylistOverlay.Visibility = Visibility.Visible;
        }

        private void CreatePlaylistCard_DragMoveDelta(object? sender, DragMoveDeltaEventArgs e)
        {
            // 1. Calculate the new potential position
            double newX = CreatePlaylistCardTransform.X + e.HorizontalChange;
            double newY = CreatePlaylistCardTransform.Y + e.VerticalChange;

            // 2. Determine Maximum allowed translate distance (Half the leftover space in the Overlay)
            double maxX = Math.Max(0, (CreatePlaylistOverlay.ActualWidth - CreatePlaylistCardHost.ActualWidth) / 2);
            double maxY = Math.Max(0, (CreatePlaylistOverlay.ActualHeight - CreatePlaylistCardHost.ActualHeight) / 2);

            // 3. Clamp the values so it cannot exceed the boundaries
            CreatePlaylistCardTransform.X = Math.Clamp(newX, -maxX, maxX);
            CreatePlaylistCardTransform.Y = Math.Clamp(newY, -maxY, maxY);
        }

        private void CreatePlaylistCard_CreateClicked(object? sender, CreatePlaylistClickedEventArgs e)
        {
            var playlist = new CreatedPlaylist { Title = e.Title };

            if (!string.IsNullOrEmpty(e.SelectedImageFilePath) && File.Exists(e.SelectedImageFilePath))
            {
                string ext = Path.GetExtension(e.SelectedImageFilePath);
                if (string.IsNullOrEmpty(ext)) ext = ".png";
                string destPath = Path.Combine(PlaylistIconsDirectory, playlist.Id + ext);
                try
                {
                    File.Copy(e.SelectedImageFilePath, destPath);
                    playlist.IconPath = destPath;
                }
                catch
                {
                    // Leave IconPath null if copy fails
                }
            }

            ViewModel.CreatePlaylist(playlist);
            CreatePlaylistOverlay.Visibility = Visibility.Collapsed;
        }

        // Tracks which playlist the popup is targeting
        private CreatedPlaylist? _contextMenuTargetPlaylist;

        private void PlaylistItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not RadioButton btn) return;
            if (btn.DataContext is not CreatedPlaylist playlist) return;

            _contextMenuTargetPlaylist = playlist;

            // Position the card in the overlay coordinate space
            Point clickPos = e.GetPosition(PlaylistContextMenuOverlay);
            double cardWidth = PlaylistContextMenuCard.ActualWidth > 0 ? PlaylistContextMenuCard.ActualWidth : 180;
            double cardHeight = PlaylistContextMenuCard.ActualHeight > 0 ? PlaylistContextMenuCard.ActualHeight : 60;

            // Open top-right of cursor
            double left = clickPos.X + 4;
            double top = clickPos.Y - cardHeight - 4;

            // Clamp so it doesn't leave the window
            left = Math.Min(left, ActualWidth - cardWidth - 8);
            top = Math.Max(top, 8);

            PlaylistContextMenuCard.Margin = new Thickness(left, top, 0, 0);
            PlaylistContextMenuOverlay.Visibility = Visibility.Visible;

            e.Handled = true;
        }

        private void PlaylistContextMenuDismiss_Click(object sender, MouseButtonEventArgs e)
        {
            PlaylistContextMenuOverlay.Visibility = Visibility.Collapsed;
            _contextMenuTargetPlaylist = null;
        }

        private void ContextMenuDeletePlaylist_Click(object? sender, EventArgs e)
        {
            PlaylistContextMenuOverlay.Visibility = Visibility.Collapsed;

            if (_contextMenuTargetPlaylist == null) return;
            var playlist = _contextMenuTargetPlaylist;
            _contextMenuTargetPlaylist = null;

            ViewModel.DeletePlaylist(playlist);

            if (ViewModel.CreatedPlaylists.Count == 0 && LikedSongsButton != null)
            {
                LikedSongsButton.IsChecked = true;
            }
        }

        private void CreatePlaylistCard_CloseRequested(object? sender, EventArgs e)
        {
            CreatePlaylistOverlay.Visibility = Visibility.Collapsed;
        }

        private void CreatePlaylistOverlayDim_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CreatePlaylistOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>Updates visibility of the "Create" card vs playlist list in the sidebar based on CreatedPlaylists count.</summary>
        private void UpdatePlaylistSidebarVisibility()
        {
            if (CreateFirstPlaylistPanel == null || PlaylistListPanel == null)
                return;
            bool hasPlaylists = ViewModel.CreatedPlaylists.Count > 0;
            CreateFirstPlaylistPanel.Visibility = hasPlaylists ? Visibility.Collapsed : Visibility.Visible;
            PlaylistListPanel.Visibility = hasPlaylists ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Binds the right panel to the complete song collection.
        /// This is the source used when the sidebar "All" option is selected.
        /// Skip Refresh() when switching source so tab change is instant; Refresh only when re-applying same source.
        /// Saves/restores scroll position per tab so returning to All keeps the previous scroll.
        /// </summary>
        private void ApplyAllSongsView()
        {
            if (UploadedSongList == null)
            {
                return;
            }

            SaveCurrentViewScrollOffset();

            _activePlaylist = null;
            if (PlaylistHeaderLabel != null)
                PlaylistHeaderLabel.Content = "All";

            bool switchedToSongs = !ReferenceEquals(UploadedSongList.ItemsSource, ViewModel.Songs);
            if (switchedToSongs)
            {
                UploadedSongList.ItemsSource = ViewModel.Songs;
            }
            else
            {
                UploadedSongList.Items.Refresh();
            }
            _suppressSelectionChanged = true;
            SyncSelectionToCurrentSong();
            _suppressSelectionChanged = false;

            if (switchedToSongs)
            {
                double offsetToRestore = _allViewScrollOffset;
                Dispatcher.BeginInvoke(() =>
                {
                    var scrollViewer = FindScrollViewer(UploadedSongList);
                    if (scrollViewer != null)
                    {
                        double target = Math.Min(offsetToRestore, Math.Max(0, scrollViewer.ScrollableHeight));
                        scrollViewer.ScrollToVerticalOffset(target);
                    }
                }, DispatcherPriority.Loaded);
            }
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
            if (ViewModel.CurrentSong == null)
            {
                UploadedSongList.SelectedIndex = -1;
                return;
            }
            int index = IndexOfSongInList(list, ViewModel.CurrentSong);
            UploadedSongList.SelectedIndex = index >= 0 ? index : -1;
        }

        /// <summary>
        /// Binds the right panel to user-liked songs.
        /// Skip Refresh() when switching source so tab change is instant; Refresh only when re-applying same source.
        /// Saves/restores scroll position per tab so returning to Liked keeps the previous scroll.
        /// </summary>
        private void ApplyLikedSongsView()
        {
            if (UploadedSongList == null)
            {
                return;
            }

            SaveCurrentViewScrollOffset();

            _activePlaylist = null;
            if (PlaylistHeaderLabel != null)
                PlaylistHeaderLabel.Content = "Liked";

            bool switchedToLiked = !ReferenceEquals(UploadedSongList.ItemsSource, ViewModel.LikedSongs);
            if (switchedToLiked)
            {
                UploadedSongList.ItemsSource = ViewModel.LikedSongs;
            }
            else
            {
                UploadedSongList.Items.Refresh();
            }
            _suppressSelectionChanged = true;
            SyncSelectionToCurrentSong();
            _suppressSelectionChanged = false;

            if (switchedToLiked)
            {
                double offsetToRestore = _likedViewScrollOffset;
                Dispatcher.BeginInvoke(() =>
                {
                    var scrollViewer = FindScrollViewer(UploadedSongList);
                    if (scrollViewer != null)
                    {
                        double target = Math.Min(offsetToRestore, Math.Max(0, scrollViewer.ScrollableHeight));
                        scrollViewer.ScrollToVerticalOffset(target);
                    }
                }, DispatcherPriority.Loaded);
            }
        }

        /// <summary>Saves the scroll offset of the current view before switching away.</summary>
        private void SaveCurrentViewScrollOffset()
        {
            var sv = FindScrollViewer(UploadedSongList);
            if (sv == null) return;

            if (ReferenceEquals(UploadedSongList.ItemsSource, ViewModel.Songs))
                _allViewScrollOffset = sv.VerticalOffset;
            else if (ReferenceEquals(UploadedSongList.ItemsSource, ViewModel.LikedSongs))
                _likedViewScrollOffset = sv.VerticalOffset;
            else if (_activePlaylist != null)
                _playlistViewScrollOffset = sv.VerticalOffset;
        }

        /// <summary>Switches the right panel to show the given playlist's songs.</summary>
        private void ApplyPlaylistView(CreatedPlaylist playlist)
        {
            if (UploadedSongList == null) return;

            SaveCurrentViewScrollOffset();

            _activePlaylist = playlist;
            if (PlaylistHeaderLabel != null)
                PlaylistHeaderLabel.Content = playlist.Title;

            // Uncheck All / Liked radio buttons
            AllSongsButton.IsChecked = false;
            LikedSongsButton.IsChecked = false;

            // Build a filtered collection of songs that belong to this playlist
            var playlistSongs = new ObservableCollection<Song>();
            foreach (var name in playlist.SongNames)
            {
                var song = ViewModel.Songs.FirstOrDefault(s => s.Name == name);
                if (song != null)
                    playlistSongs.Add(song);
            }

            UploadedSongList.ItemsSource = playlistSongs;

            _suppressSelectionChanged = true;
            SyncSelectionToCurrentSong();
            _suppressSelectionChanged = false;
        }

        private void PlaylistItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton btn) return;
            if (btn.DataContext is not CreatedPlaylist playlist) return;

            ApplyPlaylistView(playlist);
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
            ViewModel.MediaEnded += MediaPlayer_MediaEnded;   //detect for ended media

            var controller = new CoreAudioController();
            defaultPlaybackDevice = controller.DefaultPlaybackDevice;
            if (defaultPlaybackDevice != null)
            {
                ViewModel.Volume = defaultPlaybackDevice.Volume; // Set initial value from system volume

                // Subscribe to external volume changes out of the app
                defaultPlaybackDevice.VolumeChanged.Subscribe(new VolumeObserver(args =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (Math.Abs(ViewModel.Volume - args.Volume) > 0.1)
                        {
                            ViewModel.Volume = args.Volume;
                        }
                    });
                }));
            }
        }

        private class VolumeObserver : IObserver<DeviceVolumeChangedArgs>
        {
            private readonly Action<DeviceVolumeChangedArgs> _onNext;
            public VolumeObserver(Action<DeviceVolumeChangedArgs> onNext) { _onNext = onNext; }
            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(DeviceVolumeChangedArgs value) { _onNext(value); }
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

            string iconPath = ViewModel.IsPlaying
                ? "/Resources/Images/play_button.png"
                : "/Resources/Images/pause_button.png";

            playbackIcon.Source = new BitmapImage(new Uri(iconPath, UriKind.Relative));
        }

        //------------------------- Add Audio Implementation -------------------------------
        internal void AddSongToList(string name)
        {
            var song = new Song
            {
                Index = (ViewModel.Songs.Count + 1).ToString("D2"),
                Name = name
            };

            ViewModel.Songs.Add(song);
            UpdateSongData();
        }
        private void RemoveSongFromList(string name)
        {
            // Find the song with the specified name in the list
            var songToRemove = ViewModel.Songs.OfType<Song>().FirstOrDefault(song => song.Name == name);

            if (songToRemove != null)
            {
                ViewModel.Songs.Remove(songToRemove);
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

            bool isDuplicate = ViewModel.Songs
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
        private void PromptForNewName(string filePath)
        {
            ViewModel.RenameSongCommand.Execute(ViewModel.Songs.FirstOrDefault(s => s.Name == System.IO.Path.GetFileNameWithoutExtension(filePath)) ?? new Song());
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
            ViewModel.PlaySong(song);
            UpdatePlaybackButtonIcon();
            return true;
        }

        /// <summary>
        /// Resolves the selected song to an existing local file, updates player state,
        /// and starts playback while restoring the per-song volume.
        /// </summary>
        private void PlaySelectedSong(object sender, SelectionChangedEventArgs? e)
        {
            System.IO.File.AppendAllText("upload_bug_log.txt", "PlaySelectedSong triggered. Stack:\n" + new System.Diagnostics.StackTrace().ToString() + "\n\n");

            if (_suppressSelectionChanged)
                return;

            if (UploadedSongList.SelectedItem is not Song selectedSong)
                return;

            bool isAlreadyPlaying = false;
            string? songName = selectedSong.Name;
            if (ViewModel.CurrentSong != null && ViewModel.CurrentSong.Name == songName)
            {
                isAlreadyPlaying = true;
            }

            // Sync-only selection (e.g. after tab switch): same song already playing — do not restart.
            if (isAlreadyPlaying)
            {
                return;
            }
            else
            {
                _playbackList = GetCurrentSongList(UploadedSongList) ?? ViewModel.Songs;
                PlaySong(selectedSong);
                SaveSongIndex();
            }
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

        /// <summary>Finds the first descendant of type T in the visual tree (e.g. ScrollViewer inside ListBox template).</summary>
        private static T? FindVisualChildByType<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;
                var result = FindVisualChildByType<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static ScrollViewer? FindScrollViewer(ListBox listBox)
        {
            return FindVisualChildByType<ScrollViewer>(listBox);
        }
        private void PreviousSongClicked(object sender, RoutedEventArgs e)
        {
            var list = _playbackList ?? GetCurrentSongList(UploadedSongList) ?? ViewModel.Songs;
            if (list == null || list.Count == 0) return;
            int currentIndex = IndexOfSongInList(list, ViewModel.CurrentSong);
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
            ViewModel.TogglePlay();
            UpdatePlaybackButtonIcon();
        }
        private void NextSongClicked(object sender, RoutedEventArgs e)
        {
            var list = _playbackList ?? GetCurrentSongList(UploadedSongList) ?? ViewModel.Songs;
            if (list == null || list.Count == 0) return;
            int currentIndex = IndexOfSongInList(list, ViewModel.CurrentSong);
            if (currentIndex < 0) currentIndex = 0;
            int nextIndex = (currentIndex + 1) % list.Count;
            if (list[nextIndex] is not Song nextSong) return;
            PlaySong(nextSong);
            SyncSelectionToCurrentSong();
            if (ReferenceEquals(GetCurrentSongList(UploadedSongList), _playbackList))
                SaveSongIndex();
        }

        private void MediaPlayer_MediaEnded(object? sender, EventArgs e)
        {
            System.IO.File.AppendAllText("upload_bug_log.txt", "MediaEnded triggered. Stack:\n" + new System.Diagnostics.StackTrace().ToString() + "\n\n");
            var list = _playbackList ?? GetCurrentSongList(UploadedSongList) ?? ViewModel.Songs;
            if (list == null || list.Count == 0) return;
            int currentIndex = IndexOfSongInList(list, ViewModel.CurrentSong);
            if (currentIndex < 0) currentIndex = 0;
            int nextIndex = (currentIndex + 1) % list.Count;
            if (list[nextIndex] is not Song nextSong) return;
            PlaySong(nextSong);
            SyncSelectionToCurrentSong();
            if (ReferenceEquals(GetCurrentSongList(UploadedSongList), _playbackList))
                SaveSongIndex();
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
            if (SongIndex >= 0 && SongIndex < ViewModel.Songs.Count)
            {
                UploadedSongList.SelectedIndex = SongIndex;
            }

            UploadedSongList.SelectionChanged += PlaySelectedSong;
            ViewModel.IsPlaying = false;
            UpdatePlaybackButtonIcon();
        }

        //--------------------------- Slider And Volume ------------------------------------
        private void SetSystemVolume(double volume)
        {
            // Helper method to set system volume
            if (defaultPlaybackDevice != null && Math.Abs(defaultPlaybackDevice.Volume - volume) > 0.1)
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
            ViewModel.Seek(SongProgressSlider.Value); // Seek to the selected position
            SongProgressSlider.ReleaseMouseCapture();
        }
        private void SongProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDragging || ViewModel.TotalTimeSeconds > 0)
            {
                TimeSpan currentTime = TimeSpan.FromSeconds(SongProgressSlider.Value);
                ViewModel.CurrentTimeText = currentTime.ToString(@"mm\:ss");
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

        //----------------------------------------------------------------------------------



        //------------------------- Sorting Playlist Implementation ------------------------

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
                ViewModel.Songs.Clear();
                foreach (var loadedSong in loadedSongs)
                {
                    ViewModel.Songs.Add(loadedSong);
                }

                // Keep LikedSongs in sync with Songs that have IsLiked set (e.g. after load or refresh).
                ViewModel.LikedSongs.Clear();
                foreach (var song in ViewModel.Songs)
                {
                    if (song.IsLiked)
                    {
                        ViewModel.LikedSongs.Add(song);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading song data: {ex.Message}");
            }
        }

        private void UpdateSongData()
        {
            UpdateSongIndexes();
            SaveSongData(ViewModel.Songs.ToList());  // Save the current Songs collection to the JSON file
        }
        private void UpdateSongIndexes()
        {
            int indexCounter = 1;
            foreach (var song in ViewModel.Songs)
            {
                song.Index = indexCounter.ToString("D2"); // Set or update the index property
                indexCounter++;
            }
        }

        private void SongItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Song song)
            {
                _playbackList = GetCurrentSongList(UploadedSongList) ?? ViewModel.Songs;
                PlaySong(song);
                SaveSongIndex();
            }
        }

        //------------------------- Icon bar implementation --------------------------------
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Dismiss the custom context menu if open
            if (PlaylistContextMenuOverlay.Visibility == Visibility.Visible)
            {
                PlaylistContextMenuOverlay.Visibility = Visibility.Collapsed;
            }

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
            ViewModel.SaveSongData();

            this.Close();
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            SaveSongIndex();
            ViewModel.SaveSongData();
        }

        //----------------------------------------------------------------------------------
    }
}
