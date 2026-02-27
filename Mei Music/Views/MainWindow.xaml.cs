using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
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
using Newtonsoft.Json;

namespace Mei_Music
{
    /// <summary>
    /// Primary application shell window.
    /// Coordinates UI interactions, list views, popups, and delegates playback/persistence work to <see cref="MainViewModel"/> and services.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// True while a slider is actively dragged by mouse.
        /// Shared by system volume and song progress sliders.
        /// </summary>
        private bool isDragging = false;

        /// <summary>
        /// Slider currently being dragged when <see cref="isDragging"/> is true.
        /// </summary>
        private Slider? currentSlider;

        /// <summary>
        /// Active default playback device used to synchronize app volume with system volume.
        /// </summary>
        private CoreAudioDevice? defaultPlaybackDevice;

        /// <summary>
        /// Persistence service used by window-level save/load helpers.
        /// </summary>
        private readonly IFileService fileService;

        /// <summary>
        /// Sorting service injected for song ordering operations.
        /// </summary>
        private readonly IPlaylistSortService playlistSortService;

        /// <summary>
        /// Playback flow coordinator for transport navigation and song matching rules.
        /// </summary>
        private readonly IPlaybackCoordinator playbackCoordinator;

        /// <summary>
        /// Media import/conversion service used by local-file import handlers.
        /// </summary>
        private readonly IMediaImportService mediaImportService;

        /// <summary>
        /// Path to serialized song metadata persisted across sessions.
        /// </summary>
        private string songDataFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "songData.json");

        /// <summary>
        /// Path to serialized user-created playlists.
        /// </summary>
        private static readonly string PlaylistsFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlists.json");

        /// <summary>
        /// Directory where playlist icon images are copied and managed.
        /// </summary>
        private static readonly string PlaylistIconsDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist-icons");

        /// <summary>
        /// Path to persisted song-column width layout snapshot.
        /// </summary>
        private static readonly string SongColumnLayoutFilePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "song-column-layout.json");

        /// <summary>
        /// Typed accessor to DataContext for view-model command/state access.
        /// </summary>
        public MainViewModel ViewModel => (MainViewModel)this.DataContext;

        /// <summary>
        /// Shared mutable song-row column layout state used by header and rows.
        /// </summary>
        public SongColumnLayoutState SongColumnLayout { get; } = new SongColumnLayoutState();

        /// <summary>
        /// App-specific threshold for considering two row clicks a double click.
        /// </summary>
        private const int SongRowDoubleClickThresholdMs = 300;

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
        /// <summary>Prevents feedback loops while syncing overlay scrollbar with ListView ScrollViewer.</summary>
        private bool _isSyncingSongOverlayScrollBar;
        /// <summary>Prevents feedback loops while syncing overlay scrollbar with playlist sidebar ScrollViewer.</summary>
        private bool _isSyncingPlaylistOverlayScrollBar;

        /// <summary>
        /// Initializes services, loads persisted app state, wires UI events,
        /// and prepares playback/list controls for first render.
        /// </summary>
        public MainWindow(
            MainViewModel viewModel,
            IFileService fileService,
            IPlaylistSortService playlistSortService,
            IPlaybackCoordinator playbackCoordinator,
            IMediaImportService mediaImportService)
        {
            this.fileService = fileService;
            this.playlistSortService = playlistSortService;
            this.playbackCoordinator = playbackCoordinator;
            this.mediaImportService = mediaImportService;

            InitializeComponent();
            this.DataContext = viewModel;
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.Songs.CollectionChanged += (_, _) => UpdateCurrentSongState();

            string audioDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mei Music", "playlist");
            Directory.CreateDirectory(audioDirectory);
            Directory.CreateDirectory(PlaylistIconsDirectory);
            LoadSongColumnLayout();

            LoadSongData();
            LoadCreatedPlaylists();
            NormalizeSongAndPlaylistReferences();
            ApplyAllSongsView();
            ViewModel.RefreshSongsInUI();
            LoadSongIndex();

            UpdateSongIndexes();

            if (UploadedSongList.SelectedItem is Song selectedSong)
            {
                SetCurrentSong(selectedSong);
            }
            else
            {
                UpdateCurrentSongState();
            }

            InitializeMediaPlayer();
            this.PreviewMouseDown += OnMainWindowPreviewMouseDown; //tracks Position of mouse
            CreatePlaylistCard.DragMoveDelta += CreatePlaylistCard_DragMoveDelta;
            ViewModel.CreatedPlaylists.CollectionChanged += (_, _) => UpdatePlaylistSidebarVisibility();
            UpdatePlaylistSidebarVisibility();
            _inlineToastHideTimer.Tick += InlineToastHideTimer_Tick;
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Reacts to view-model property changes that affect row-level current-song visuals.
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentSong))
            {
                UpdateCurrentSongState();
            }
        }

        /// <summary>
        /// Sets the view-model current song and re-flags row state for now-playing visuals.
        /// </summary>
        private void SetCurrentSong(Song song)
        {
            ViewModel.CurrentSong = song;
            UpdateCurrentSongState();
        }

        /// <summary>
        /// Synchronizes each song row's IsCurrent flag against <see cref="MainViewModel.CurrentSong"/>.
        /// Uses reference first and stable song-ID matching for deserialized/recreated objects.
        /// </summary>
        private void UpdateCurrentSongState()
        {
            playbackCoordinator.SyncCurrentSongFlags(ViewModel.Songs, ViewModel.CurrentSong);
        }

        /// <summary>
        /// Runs after first visual tree load to compute initial responsive song-column widths.
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateSongColumnLayout();
            Dispatcher.BeginInvoke(SyncPlaylistOverlayScrollBar, DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Computes available song-list viewport width and updates shared column layout state.
        /// Also keeps custom overlay scrollbar synchronized with native ScrollViewer metrics.
        /// </summary>
        private void UpdateSongColumnLayout()
        {
            if (UploadedSongList == null)
            {
                return;
            }

            double viewportWidth = UploadedSongList.ActualWidth;
            var scrollViewer = FindScrollViewer(UploadedSongList);
            if (scrollViewer != null && scrollViewer.ViewportWidth > 0)
            {
                viewportWidth = scrollViewer.ViewportWidth;
            }

            SyncSongOverlayScrollBar(scrollViewer);

            SongColumnLayout.UpdateTitleWidth(viewportWidth);
        }

        /// <summary>
        /// Re-evaluates column layout whenever the internal list scrolls.
        /// </summary>
        private void UploadedSongList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateSongColumnLayout();
        }

        /// <summary>
        /// Mirrors ListView ScrollViewer state into a styled overlay scrollbar.
        /// Guarded by <see cref="_isSyncingSongOverlayScrollBar"/> to prevent recursive updates.
        /// </summary>
        private void SyncSongOverlayScrollBar(ScrollViewer? scrollViewer)
        {
            if (SongOverlayScrollBar == null)
            {
                return;
            }

            _isSyncingSongOverlayScrollBar = true;
            try
            {
                if (scrollViewer == null)
                {
                    SongOverlayScrollBar.Visibility = Visibility.Collapsed;
                    SongOverlayScrollBar.Maximum = 0;
                    SongOverlayScrollBar.ViewportSize = 0;
                    SongOverlayScrollBar.Value = 0;
                    return;
                }

                double max = Math.Max(0, scrollViewer.ScrollableHeight);
                SongOverlayScrollBar.Visibility = max > 0 ? Visibility.Visible : Visibility.Collapsed;
                SongOverlayScrollBar.Maximum = max;
                SongOverlayScrollBar.ViewportSize = Math.Max(0, scrollViewer.ViewportHeight);
                SongOverlayScrollBar.LargeChange = Math.Max(1, scrollViewer.ViewportHeight * 0.9);
                SongOverlayScrollBar.Value = Math.Clamp(scrollViewer.VerticalOffset, 0, max);
            }
            finally
            {
                _isSyncingSongOverlayScrollBar = false;
            }
        }

        /// <summary>
        /// Applies user interaction from the overlay scrollbar back to the list ScrollViewer.
        /// </summary>
        private void SongOverlayScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingSongOverlayScrollBar)
            {
                return;
            }

            var scrollViewer = FindScrollViewer(UploadedSongList);
            if (scrollViewer == null)
            {
                return;
            }

            scrollViewer.ScrollToVerticalOffset(e.NewValue);
        }

        /// <summary>
        /// Keeps the playlist sidebar overlay scrollbar synchronized when the underlying ScrollViewer moves.
        /// </summary>
        private void PlaylistSidebarScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            SyncPlaylistOverlayScrollBar();
        }

        /// <summary>
        /// Mirrors playlist sidebar ScrollViewer state into a styled overlay scrollbar.
        /// Guarded by <see cref="_isSyncingPlaylistOverlayScrollBar"/> to prevent recursive updates.
        /// </summary>
        private void SyncPlaylistOverlayScrollBar()
        {
            if (PlaylistOverlayScrollBar == null)
            {
                return;
            }

            _isSyncingPlaylistOverlayScrollBar = true;
            try
            {
                if (PlaylistSidebarScrollViewer == null)
                {
                    PlaylistOverlayScrollBar.Visibility = Visibility.Collapsed;
                    PlaylistOverlayScrollBar.Maximum = 0;
                    PlaylistOverlayScrollBar.ViewportSize = 0;
                    PlaylistOverlayScrollBar.Value = 0;
                    return;
                }

                double max = Math.Max(0, PlaylistSidebarScrollViewer.ScrollableHeight);
                PlaylistOverlayScrollBar.Visibility = max > 0 ? Visibility.Visible : Visibility.Collapsed;
                PlaylistOverlayScrollBar.Maximum = max;
                PlaylistOverlayScrollBar.ViewportSize = Math.Max(0, PlaylistSidebarScrollViewer.ViewportHeight);
                PlaylistOverlayScrollBar.LargeChange = Math.Max(1, PlaylistSidebarScrollViewer.ViewportHeight * 0.9);
                PlaylistOverlayScrollBar.Value = Math.Clamp(PlaylistSidebarScrollViewer.VerticalOffset, 0, max);
            }
            finally
            {
                _isSyncingPlaylistOverlayScrollBar = false;
            }
        }

        /// <summary>
        /// Applies user interaction from the playlist overlay scrollbar back to its ScrollViewer.
        /// </summary>
        private void PlaylistOverlayScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingPlaylistOverlayScrollBar)
            {
                return;
            }

            PlaylistSidebarScrollViewer?.ScrollToVerticalOffset(e.NewValue);
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

        /// <summary>
        /// Normalizes song/playlist identity data and migrates legacy SongNames membership to ID-based links.
        /// Ensures playlists reference songs by <see cref="Song.Id"/> and songs track reverse membership via PlaylistIds.
        /// </summary>
        private void NormalizeSongAndPlaylistReferences()
        {
            bool songsChanged = false;
            bool playlistsChanged = false;

            var songsById = new Dictionary<string, Song>(StringComparer.Ordinal);
            var songsByName = new Dictionary<string, Song>(StringComparer.OrdinalIgnoreCase);

            foreach (Song song in ViewModel.Songs)
            {
                if (string.IsNullOrWhiteSpace(song.Id))
                {
                    song.Id = Guid.NewGuid().ToString("N");
                    songsChanged = true;
                }

                while (songsById.ContainsKey(song.Id))
                {
                    song.Id = Guid.NewGuid().ToString("N");
                    songsChanged = true;
                }

                var normalizedPlaylistIds = (song.PlaylistIds ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (song.PlaylistIds == null || !song.PlaylistIds.SequenceEqual(normalizedPlaylistIds))
                {
                    song.PlaylistIds = normalizedPlaylistIds;
                    songsChanged = true;
                }

                songsById[song.Id] = song;

                if (!string.IsNullOrWhiteSpace(song.Name) && !songsByName.ContainsKey(song.Name))
                {
                    songsByName[song.Name] = song;
                }
            }

            var seenPlaylistIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CreatedPlaylist playlist in ViewModel.CreatedPlaylists)
            {
                if (string.IsNullOrWhiteSpace(playlist.Id))
                {
                    playlist.Id = Guid.NewGuid().ToString("N");
                    playlistsChanged = true;
                }

                while (!seenPlaylistIds.Add(playlist.Id))
                {
                    playlist.Id = Guid.NewGuid().ToString("N");
                    playlistsChanged = true;
                }

                var normalizedSongIds = (playlist.SongIds ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (normalizedSongIds.Count == 0 && playlist.LegacySongNames != null)
                {
                    foreach (string legacySongName in playlist.LegacySongNames)
                    {
                        if (songsByName.TryGetValue(legacySongName, out Song? mappedSong)
                            && !normalizedSongIds.Contains(mappedSong.Id, StringComparer.Ordinal))
                        {
                            normalizedSongIds.Add(mappedSong.Id);
                        }
                    }
                }

                int beforeFilterCount = normalizedSongIds.Count;
                normalizedSongIds = normalizedSongIds
                    .Where(songId => songsById.ContainsKey(songId))
                    .ToList();

                if (beforeFilterCount != normalizedSongIds.Count)
                {
                    playlistsChanged = true;
                }

                if (playlist.SongIds == null || !playlist.SongIds.SequenceEqual(normalizedSongIds))
                {
                    playlist.SongIds = normalizedSongIds;
                    playlistsChanged = true;
                }

                if (playlist.LegacySongNames != null)
                {
                    playlist.LegacySongNames = null;
                    playlistsChanged = true;
                }
            }

            var playlistMembershipBySongId = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (CreatedPlaylist playlist in ViewModel.CreatedPlaylists)
            {
                foreach (string songId in playlist.SongIds)
                {
                    if (!playlistMembershipBySongId.TryGetValue(songId, out List<string>? playlistIds))
                    {
                        playlistIds = new List<string>();
                        playlistMembershipBySongId[songId] = playlistIds;
                    }

                    if (!playlistIds.Contains(playlist.Id, StringComparer.Ordinal))
                    {
                        playlistIds.Add(playlist.Id);
                    }
                }
            }

            foreach (Song song in ViewModel.Songs)
            {
                var desiredPlaylistIds = playlistMembershipBySongId.TryGetValue(song.Id, out List<string>? playlistIds)
                    ? playlistIds
                    : new List<string>();

                if (song.PlaylistIds == null || !song.PlaylistIds.SequenceEqual(desiredPlaylistIds))
                {
                    song.PlaylistIds = desiredPlaylistIds;
                    songsChanged = true;
                }
            }

            if (playlistsChanged)
            {
                ViewModel.SaveCreatedPlaylists();
            }

            if (songsChanged)
            {
                ViewModel.SaveSongData();
            }

            ViewModel.SyncActivePlaylistSongs();
        }

        /// <summary>Resets and shows the create-playlist overlay card.</summary>
        private void OpenCreatePlaylistOverlay()
        {
            CreatePlaylistCard.Reset();
            CreatePlaylistCardTransform.X = 0;
            CreatePlaylistCardTransform.Y = 0;
            CreatePlaylistOverlay.Visibility = Visibility.Visible;
        }

        /// <summary>Shows the in-app create-playlist overlay; card raises CreateClicked or CloseRequested.</summary>
        private void OpenCreatePlaylistDialog(object sender, RoutedEventArgs e)
        {
            _pendingAddToPlaylistSong = null;
            OpenCreatePlaylistOverlay();
        }

        /// <summary>
        /// Moves the create-playlist card within overlay bounds while user drags its header.
        /// </summary>
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

        /// <summary>
        /// Handles create-playlist confirmation by constructing playlist model,
        /// optionally copying selected icon, and delegating creation to the view-model.
        /// </summary>
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

            Song? pendingSong = _pendingAddToPlaylistSong;
            if (pendingSong != null)
            {
                bool wasAdded = TryAddSongToPlaylist(playlist, pendingSong);
                if (wasAdded)
                {
                    ShowInlineToast($"Added to \"{playlist.Title}\"", isSuccess: true);
                }
                else
                {
                    ShowInlineToast("Song already in playlist", isSuccess: false);
                }

                _pendingAddToPlaylistSong = null;
            }

            CreatePlaylistOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Playlist currently targeted by the playlist context menu card.
        /// </summary>
        private CreatedPlaylist? _contextMenuTargetPlaylist;

        /// <summary>
        /// Song currently targeted by the song context menu card.
        /// </summary>
        private Song? _contextMenuTargetSong;

        /// <summary>
        /// Song currently being added from the add-to-playlist flow.
        /// </summary>
        private Song? _pendingAddToPlaylistSong;
        /// <summary>Auto-hide timer for non-blocking inline toast notifications.</summary>
        private readonly DispatcherTimer _inlineToastHideTimer = new DispatcherTimer();
        /// <summary>
        /// Monotonic token used to ignore stale deferred show callbacks when toasts are triggered in quick succession.
        /// </summary>
        private int _inlineToastShowRequestVersion;
        private static readonly SolidColorBrush InlineToastSuccessBrush = CreateInlineToastBrush(0x3F, 0xC7, 0x6A);
        private static readonly SolidColorBrush InlineToastErrorBrush = CreateInlineToastBrush(0xF0, 0x4E, 0x4E);

        // Pointer Gap
        private const double ContextMenuPointerGap = 15;
        private const double ContextMenuWindowPadding = 8;
        private static readonly Size PlaylistContextMenuFallbackSize = new Size(180, 60);
        private static readonly Size SongContextMenuFallbackSize = new Size(190, 250);

        /// <summary>
        /// Rounds a point to the nearest physical pixel for the current visual's DPI scale.
        /// Prevents fractional placement that can soften text and glyph edges.
        /// </summary>
        private static Point SnapPointToDevicePixels(Point point, Visual visual)
        {
            var source = PresentationSource.FromVisual(visual);
            if (source?.CompositionTarget == null)
            {
                return new Point(Math.Round(point.X), Math.Round(point.Y));
            }

            Matrix toDevice = source.CompositionTarget.TransformToDevice;
            double x = toDevice.M11 > 0
                ? Math.Round(point.X * toDevice.M11) / toDevice.M11
                : Math.Round(point.X);
            double y = toDevice.M22 > 0
                ? Math.Round(point.Y * toDevice.M22) / toDevice.M22
                : Math.Round(point.Y);

            return new Point(x, y);
        }

        /// <summary>
        /// Shared host helper used by both song/playlist context cards.
        /// </summary>
        private static void ShowContextMenuCard(Grid overlay, FrameworkElement card, double left, double top)
        {
            card.Margin = new Thickness(left, top, 0, 0);
            overlay.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Opens any context menu card near an anchor point with shared edge-aware placement logic.
        /// </summary>
        private void OpenContextMenuCardAtWindowPoint(
            Grid overlay,
            FrameworkElement card,
            Point anchorInWindow,
            Size cardSize,
            double pointerGap,
            double windowPadding)
        {
            double overlayWidth = overlay.ActualWidth > 0 ? overlay.ActualWidth : ActualWidth;
            double overlayHeight = overlay.ActualHeight > 0 ? overlay.ActualHeight : ActualHeight;

            double availableRight = Math.Max(0, overlayWidth - anchorInWindow.X);
            double availableLeft = Math.Max(0, anchorInWindow.X);
            double availableBottom = Math.Max(0, overlayHeight - anchorInWindow.Y);
            double availableTop = Math.Max(0, anchorInWindow.Y);

            bool openRight = availableRight >= cardSize.Width + pointerGap || availableRight >= availableLeft;
            bool openDown = availableBottom >= cardSize.Height + pointerGap || availableBottom >= availableTop;

            double left = anchorInWindow.X + (openRight ? pointerGap : -(cardSize.Width + pointerGap));
            double top = anchorInWindow.Y + (openDown ? pointerGap : -(cardSize.Height + pointerGap));

            double maxLeft = Math.Max(windowPadding, overlayWidth - cardSize.Width - windowPadding);
            double maxTop = Math.Max(windowPadding, overlayHeight - cardSize.Height - windowPadding);

            left = Math.Clamp(left, windowPadding, maxLeft);
            top = Math.Clamp(top, windowPadding, maxTop);

            Point snappedMenuOrigin = SnapPointToDevicePixels(new Point(left, top), overlay);
            ShowContextMenuCard(overlay, card, snappedMenuOrigin.X, snappedMenuOrigin.Y);
        }

        /// <summary>
        /// Hides song context menu overlay and optionally clears targeted song.
        /// </summary>
        private void CloseSongContextMenu(bool clearTarget = false)
        {
            SongContextMenuOverlay.Visibility = Visibility.Collapsed;
            if (clearTarget)
            {
                _contextMenuTargetSong = null;
            }
        }

        /// <summary>
        /// Hides playlist context menu overlay and optionally clears targeted playlist.
        /// </summary>
        private void ClosePlaylistContextMenu(bool clearTarget = true)
        {
            PlaylistContextMenuOverlay.Visibility = Visibility.Collapsed;
            if (clearTarget)
            {
                _contextMenuTargetPlaylist = null;
            }
        }

        /// <summary>
        /// Opens the add-to-playlist picker for a target song.
        /// </summary>
        private void OpenAddToPlaylistOverlay(Song song)
        {
            _pendingAddToPlaylistSong = song;
            AddToPlaylistCardView.LoadPlaylists(ViewModel.CreatedPlaylists, song);
            AddToPlaylistCardTransform.X = 0;
            AddToPlaylistCardTransform.Y = 0;
            AddToPlaylistOverlay.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides add-to-playlist overlay and optionally clears pending target song.
        /// </summary>
        private void CloseAddToPlaylistOverlay(bool clearPendingSong = true)
        {
            AddToPlaylistOverlay.Visibility = Visibility.Collapsed;
            if (clearPendingSong)
            {
                _pendingAddToPlaylistSong = null;
            }
        }

        /// <summary>
        /// Adds the provided song to the playlist if it is not already present.
        /// Persists playlist changes and refreshes active playlist view when needed.
        /// </summary>
        private bool TryAddSongToPlaylist(CreatedPlaylist playlist, Song song)
        {
            if (string.IsNullOrWhiteSpace(song.Id) || string.IsNullOrWhiteSpace(playlist.Id))
            {
                return false;
            }

            playlist.SongIds ??= new List<string>();
            song.PlaylistIds ??= new List<string>();

            bool alreadyInPlaylist = playlist.SongIds.Any(songId =>
                string.Equals(songId, song.Id, StringComparison.Ordinal));
            if (alreadyInPlaylist)
            {
                if (!song.PlaylistIds.Contains(playlist.Id, StringComparer.Ordinal))
                {
                    song.PlaylistIds.Add(playlist.Id);
                    ViewModel.SaveSongData();
                }

                return false;
            }

            playlist.SongIds.Add(song.Id);
            playlist.LegacySongNames = null;
            if (!song.PlaylistIds.Contains(playlist.Id, StringComparer.Ordinal))
            {
                song.PlaylistIds.Add(playlist.Id);
            }

            ViewModel.SaveCreatedPlaylists();
            ViewModel.SaveSongData();

            if (ViewModel.ActivePlaylist != null && string.Equals(ViewModel.ActivePlaylist.Id, playlist.Id, StringComparison.Ordinal))
            {
                ViewModel.SyncActivePlaylistSongs();
            }

            return true;
        }

        /// <summary>
        /// Opens playlist context menu near the right-click position.
        /// </summary>
        private void PlaylistItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not RadioButton btn) return;
            if (btn.DataContext is not CreatedPlaylist playlist) return;

            _contextMenuTargetPlaylist = playlist;
            CloseSongContextMenu(clearTarget: true);
            CloseAddToPlaylistOverlay(clearPendingSong: true);

            Point anchorPoint = e.GetPosition(this);
            Size popupSize = GetPlaylistContextMenuSize();
            OpenContextMenuCardAtWindowPoint(
                PlaylistContextMenuOverlay,
                PlaylistContextMenuCard,
                anchorPoint,
                popupSize,
                ContextMenuPointerGap,
                ContextMenuWindowPadding);

            e.Handled = true;
        }

        /// <summary>
        /// Deletes the playlist selected in context menu and restores fallback selection when needed.
        /// </summary>
        private void ContextMenuDeletePlaylist_Click(object? sender, EventArgs e)
        {
            if (_contextMenuTargetPlaylist == null) return;
            var playlist = _contextMenuTargetPlaylist;
            bool wasViewingThisPlaylist = ReferenceEquals(_activePlaylist, playlist);
            ClosePlaylistContextMenu();

            ViewModel.DeletePlaylist(playlist);

            if (wasViewingThisPlaylist)
            {
                ApplyAllSongsView();
            }

            if (ViewModel.CreatedPlaylists.Count == 0 && LikedSongsButton != null)
            {
                LikedSongsButton.IsChecked = true;
            }
        }

        /// <summary>
        /// Opens the edit-playlist page for the playlist selected in the context menu.
        /// </summary>
        private void ContextMenuEditPlaylist_Click(object? sender, EventArgs e)
        {
            if (_contextMenuTargetPlaylist == null) return;
            var playlist = _contextMenuTargetPlaylist;
            ClosePlaylistContextMenu();

            ViewModel.BeginEditPlaylist(playlist);
        }

        /// <summary>
        /// Measures current playlist context card and returns best-known size for edge-aware placement.
        /// </summary>
        private Size GetPlaylistContextMenuSize()
        {
            if (PlaylistContextMenuCard.ActualWidth > 0 && PlaylistContextMenuCard.ActualHeight > 0)
            {
                return new Size(PlaylistContextMenuCard.ActualWidth, PlaylistContextMenuCard.ActualHeight);
            }

            PlaylistContextMenuCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size desired = PlaylistContextMenuCard.DesiredSize;
            if (desired.Width > 0 && desired.Height > 0)
            {
                return desired;
            }

            return PlaylistContextMenuFallbackSize;
        }

        /// <summary>
        /// Selects a song row without triggering playback side effects.
        /// </summary>
        private void SelectSongForContextMenu(Song song)
        {
            _suppressSelectionChanged = true;
            try
            {
                UploadedSongList.SelectedItem = song;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        /// <summary>
        /// Measures current song context card and returns best-known size for edge-aware placement.
        /// </summary>
        private Size GetSongContextMenuSize()
        {
            // Actual size is stable and does not include positional margin offsets.
            // Using DesiredSize after we set Margin(left, top, 0, 0) can inflate width/height.
            if (SongContextMenu.ActualWidth > 0 && SongContextMenu.ActualHeight > 0)
            {
                return new Size(SongContextMenu.ActualWidth, SongContextMenu.ActualHeight);
            }

            SongContextMenu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size desired = SongContextMenu.DesiredSize;
            if (desired.Width > 0 && desired.Height > 0)
            {
                return desired;
            }

            return SongContextMenuFallbackSize;
        }

        /// <summary>
        /// Opens song context menu near an anchor point with smart edge-aware direction.
        /// Prefers lower-right placement with breathing room, then flips when needed.
        /// </summary>
        private void OpenSongContextMenuAtWindowPoint(Point anchorInWindow, FrameworkElement? focusTarget)
        {
            ClosePlaylistContextMenu();
            CloseSongContextMenu();
            CloseAddToPlaylistOverlay(clearPendingSong: true);

            Size popupSize = GetSongContextMenuSize();
            OpenContextMenuCardAtWindowPoint(
                SongContextMenuOverlay,
                SongContextMenu,
                anchorInWindow,
                popupSize,
                ContextMenuPointerGap,
                ContextMenuWindowPadding);

            focusTarget?.Focus();
        }

        /// <summary>
        /// Opens the song context card anchored to the song-row option button.
        /// </summary>
        private void SongOptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Song song)
            {
                SelectSongForContextMenu(song);
                _contextMenuTargetSong = song;
                Point anchorPoint = btn.TranslatePoint(new Point(btn.ActualWidth, btn.ActualHeight), this);
                OpenSongContextMenuAtWindowPoint(anchorPoint, btn);
            }
        }

        /// <summary>
        /// Opens song context card from <see cref="SongRowView"/> options event.
        /// </summary>
        private void SongRow_OptionsRequested(object? sender, SongOptionsRequestedEventArgs e)
        {
            SelectSongForContextMenu(e.Song);
            _contextMenuTargetSong = e.Song;
            Point anchorPoint = e.AnchorElement != null
                ? e.AnchorElement.TranslatePoint(new Point(e.AnchorElement.ActualWidth, e.AnchorElement.ActualHeight), this)
                : Mouse.GetPosition(this);
            OpenSongContextMenuAtWindowPoint(anchorPoint, e.AnchorElement ?? UploadedSongList);
        }

        /// <summary>
        /// Handles play/pause requests emitted by song-row control.
        /// Selects the row, then toggles current song or starts requested song.
        /// </summary>
        private void SongRow_PlayPauseRequested(object? sender, SongPlayPauseRequestedEventArgs e)
        {
            playbackCoordinator.SetPlaybackList(GetCurrentSongList() ?? ViewModel.Songs);

            _suppressSelectionChanged = true;
            try
            {
                UploadedSongList.SelectedItem = e.Song;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            bool isCurrentSong = playbackCoordinator.IsCurrentSong(ViewModel.CurrentSong, e.Song);

            if (isCurrentSong)
            {
                ViewModel.TogglePlay();
                UpdatePlaybackButtonIcon();
                SaveSongIndex();
                return;
            }

            PlaySong(e.Song);
            SaveSongIndex();
        }

        /// <summary>
        /// On right-button press, selects the song row under cursor without triggering playback.
        /// </summary>
        private void UploadedSongList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            var listViewItem = FindVisualParent<ListViewItem>(source);
            if (listViewItem?.DataContext is not Song song)
            {
                return;
            }

            _suppressSelectionChanged = true;
            try
            {
                UploadedSongList.SelectedItem = song;
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            _contextMenuTargetSong = song;
        }

        /// <summary>
        /// Opens song context card at mouse pointer when right click is released on a song row.
        /// </summary>
        private void UploadedSongList_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            var listViewItem = FindVisualParent<ListViewItem>(source);
            if (listViewItem?.DataContext is not Song song)
            {
                return;
            }

            _contextMenuTargetSong = song;
            Point anchorPoint = e.GetPosition(this);
            OpenSongContextMenuAtWindowPoint(anchorPoint, UploadedSongList);
            e.Handled = true;
        }

        /// <summary>
        /// Opens add-to-playlist card for the song currently targeted by context menu.
        /// </summary>
        private void SongContextMenu_AddRequested(object? sender, EventArgs e)
        {
            Song? targetSong = _contextMenuTargetSong;
            CloseSongContextMenu(clearTarget: true);
            if (targetSong == null)
            {
                return;
            }

            OpenAddToPlaylistOverlay(targetSong);
        }

        /// <summary>
        /// Closes add-to-playlist picker without taking action.
        /// </summary>
        private void AddToPlaylistCard_CloseRequested(object? sender, EventArgs e)
        {
            CloseAddToPlaylistOverlay(clearPendingSong: true);
        }

        /// <summary>
        /// Moves the add-to-playlist card within overlay bounds while user drags its header.
        /// </summary>
        private void AddToPlaylistCard_DragMoveDelta(object? sender, DragMoveDeltaEventArgs e)
        {
            double newX = AddToPlaylistCardTransform.X + e.HorizontalChange;
            double newY = AddToPlaylistCardTransform.Y + e.VerticalChange;

            double maxX = Math.Max(0, (AddToPlaylistOverlay.ActualWidth - AddToPlaylistCardHost.ActualWidth) / 2);
            double maxY = Math.Max(0, (AddToPlaylistOverlay.ActualHeight - AddToPlaylistCardHost.ActualHeight) / 2);

            AddToPlaylistCardTransform.X = Math.Clamp(newX, -maxX, maxX);
            AddToPlaylistCardTransform.Y = Math.Clamp(newY, -maxY, maxY);
        }

        /// <summary>
        /// Opens create-playlist flow from add-to-playlist picker.
        /// If playlist is created, pending song will be added automatically.
        /// </summary>
        private void AddToPlaylistCard_CreatePlaylistRequested(object? sender, EventArgs e)
        {
            CloseAddToPlaylistOverlay(clearPendingSong: false);
            OpenCreatePlaylistOverlay();
        }

        /// <summary>
        /// Adds pending song to selected playlist and notifies user.
        /// </summary>
        private void AddToPlaylistCard_PlaylistSelected(object? sender, PlaylistSelectedEventArgs e)
        {
            Song? targetSong = _pendingAddToPlaylistSong;
            if (targetSong == null)
            {
                CloseAddToPlaylistOverlay(clearPendingSong: true);
                return;
            }

            bool wasAdded = TryAddSongToPlaylist(e.Playlist, targetSong);
            if (wasAdded)
            {
                ShowInlineToast($"Added to \"{e.Playlist.Title}\"", isSuccess: true);
                CloseAddToPlaylistOverlay(clearPendingSong: true);
            }
            else
            {
                ShowInlineToast("Song already in playlist", isSuccess: false);
            }
        }

        private void InlineToastHideTimer_Tick(object? sender, EventArgs e)
        {
            HideInlineToast();
        }

        private void ShowInlineToast(string message, bool isSuccess)
        {
            if (InlineToastOverlay == null || InlineToastHost == null || InlineToastText == null || InlineToastStatusDot == null)
            {
                return;
            }

            int showRequestVersion = ++_inlineToastShowRequestVersion;
            _inlineToastHideTimer.Stop();
            InlineToastText.Text = message;
            InlineToastStatusDot.Fill = isSuccess ? InlineToastSuccessBrush : InlineToastErrorBrush;
            InlineToastHost.CornerRadius = new CornerRadius(Math.Max(0, AppUiSettings.InlineToastCornerRadius));
            _inlineToastHideTimer.Interval = TimeSpan.FromSeconds(Math.Max(0.1, AppUiSettings.InlineToastDurationSeconds));
            InlineToastHost.Opacity = 0;
            InlineToastOverlay.Visibility = Visibility.Visible;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (showRequestVersion != _inlineToastShowRequestVersion)
                {
                    return;
                }

                if (InlineToastOverlay?.Visibility != Visibility.Visible || InlineToastHost == null)
                {
                    return;
                }

                PositionInlineToast();
                InlineToastHost.Opacity = 1;
                _inlineToastHideTimer.Start();
            }));
        }

        private void HideInlineToast()
        {
            _inlineToastHideTimer.Stop();
            if (InlineToastOverlay == null || InlineToastHost == null)
            {
                return;
            }

            InlineToastHost.Opacity = 0;
            InlineToastOverlay.Visibility = Visibility.Collapsed;
        }

        private void PositionInlineToast()
        {
            if (InlineToastHost == null || InlineToastOverlay == null)
            {
                return;
            }

            double toastWidth = InlineToastHost.ActualWidth;
            double toastHeight = InlineToastHost.ActualHeight;

            if (toastWidth <= 0) toastWidth = 220;
            if (toastHeight <= 0) toastHeight = 44;

            double left;
            double top;

            switch (AppUiSettings.ToastPlacementMode)
            {
                case AppUiSettings.InlineToastPlacementMode.MouseAnchored:
                {
                    Point mousePoint = Mouse.GetPosition(this);
                    left = mousePoint.X - (toastWidth / 2) + AppUiSettings.MouseAnchoredToastOffsetX;
                    top = mousePoint.Y - (toastHeight / 2) + AppUiSettings.MouseAnchoredToastOffsetY;
                    break;
                }
                case AppUiSettings.InlineToastPlacementMode.BottomCenterAbovePlayBar:
                {
                    Point playBarTopLeft = PlayBarHost?.TranslatePoint(new Point(0, 0), this) ?? new Point(0, ActualHeight - 68);
                    double playBarTop = playBarTopLeft.Y;
                    left = ((ActualWidth - toastWidth) / 2) + AppUiSettings.BottomCenterToastOffsetX;
                    top = (playBarTop - toastHeight - 10) + AppUiSettings.BottomCenterToastOffsetY;
                    break;
                }
                default:
                    left = (ActualWidth - toastWidth) / 2;
                    top = (ActualHeight - toastHeight) / 2;
                    break;
            }

            double maxLeft = Math.Max(0, ActualWidth - toastWidth - 8);
            double maxTop = Math.Max(0, ActualHeight - toastHeight - 8);

            left = Math.Clamp(left, 8, maxLeft);
            top = Math.Clamp(top, 8, maxTop);

            InlineToastHost.HorizontalAlignment = HorizontalAlignment.Left;
            InlineToastHost.VerticalAlignment = VerticalAlignment.Top;
            InlineToastHost.Margin = new Thickness(left, top, 0, 0);
        }

        private static SolidColorBrush CreateInlineToastBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }

        /// <summary>
        /// Opens per-song volume editor for the song currently targeted by context menu.
        /// </summary>
        private void SongContextMenu_VolumeRequested(object? sender, EventArgs e)
        {
            Song? targetSong = _contextMenuTargetSong;
            CloseSongContextMenu(clearTarget: true);
            if (targetSong != null)
            {
                ViewModel.SongVolumeCommand.Execute(targetSong);
            }
        }

        /// <summary>
        /// Executes rename command for the song currently targeted by context menu.
        /// </summary>
        private void SongContextMenu_RenameRequested(object? sender, EventArgs e)
        {
            Song? targetSong = _contextMenuTargetSong;
            CloseSongContextMenu(clearTarget: true);
            if (targetSong != null)
            {
                ViewModel.RenameSongCommand.Execute(targetSong);
            }
        }

        /// <summary>
        /// Executes open-folder command for the song currently targeted by context menu.
        /// </summary>
        private void SongContextMenu_OpenFolderRequested(object? sender, EventArgs e)
        {
            Song? targetSong = _contextMenuTargetSong;
            CloseSongContextMenu(clearTarget: true);
            if (targetSong != null)
            {
                ViewModel.OpenFolderCommand.Execute(targetSong);
            }
        }

        /// <summary>
        /// Executes delete command for the song currently targeted by context menu.
        /// </summary>
        private void SongContextMenu_DeleteRequested(object? sender, EventArgs e)
        {
            Song? targetSong = _contextMenuTargetSong;
            CloseSongContextMenu(clearTarget: true);
            if (targetSong != null)
            {
                ViewModel.DeleteSongCommand.Execute(targetSong);
            }
        }

        /// <summary>
        /// Closes create-playlist overlay when card emits close event.
        /// </summary>
        private void CreatePlaylistCard_CloseRequested(object? sender, EventArgs e)
        {
            CreatePlaylistOverlay.Visibility = Visibility.Collapsed;
            _pendingAddToPlaylistSong = null;
        }

        /// <summary>
        /// Closes create-playlist overlay when user clicks dimmed backdrop.
        /// </summary>
        private void CreatePlaylistOverlayDim_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CreatePlaylistOverlay.Visibility = Visibility.Collapsed;
            _pendingAddToPlaylistSong = null;
        }

        /// <summary>Updates visibility of the "Create" card vs playlist list in the sidebar based on CreatedPlaylists count.</summary>
        private void UpdatePlaylistSidebarVisibility()
        {
            if (CreateFirstPlaylistPanel == null || PlaylistListPanel == null)
                return;
            bool hasPlaylists = ViewModel.CreatedPlaylists.Count > 0;
            CreateFirstPlaylistPanel.Visibility = hasPlaylists ? Visibility.Collapsed : Visibility.Visible;
            PlaylistListPanel.Visibility = hasPlaylists ? Visibility.Visible : Visibility.Collapsed;
            Dispatcher.BeginInvoke(SyncPlaylistOverlayScrollBar, DispatcherPriority.Loaded);
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
            ViewModel.SetActivePlaylist(null);
            ViewModel.CurrentHeaderTitle = "All";

            bool switchedToSongs = SongDataContainer == null || !ReferenceEquals(SongDataContainer.Collection, ViewModel.Songs);
            _suppressSelectionChanged = true;
            try
            {
                if (switchedToSongs)
                {
                    if (SongDataContainer != null)
                    {
                        SongDataContainer.Collection = ViewModel.Songs;
                    }
                    else
                    {
                        UploadedSongList.ItemsSource = ViewModel.Songs;
                    }
                }
                else
                {
                    UploadedSongList.Items.Refresh();
                }

                SyncSelectionToCurrentSong();
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

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
        /// Returns the active song list (Songs, LikedSongs, or playlist songs) used by transport/navigation.
        /// Uses the CompositeCollection container when available so header rows are excluded from indexing.
        /// </summary>
        private IList? GetCurrentSongList()
        {
            if (SongDataContainer?.Collection is IList listFromContainer)
            {
                return listFromContainer;
            }

            return UploadedSongList?.ItemsSource as IList;
        }

        /// <summary>
        /// After changing the list (All/Liked) or after Prev/Next, sync selection to the currently playing song.
        /// If CurrentSong is in the visible list, select it; otherwise clear selection so no row is highlighted.
        /// Does not restart playback.
        /// </summary>
        private void SyncSelectionToCurrentSong()
        {
            if (UploadedSongList == null) return;
            var list = GetCurrentSongList();
            if (list == null || list.Count == 0) return;
            if (ViewModel.CurrentSong == null)
            {
                UploadedSongList.SelectedIndex = -1;
                return;
            }

            int index = playbackCoordinator.IndexOfSongInList(list, ViewModel.CurrentSong);
            if (index >= 0 && list[index] is Song visibleSong)
            {
                UploadedSongList.SelectedItem = visibleSong;
            }
            else
            {
                UploadedSongList.SelectedIndex = -1;
            }
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
            ViewModel.SetActivePlaylist(null);
            ViewModel.CurrentHeaderTitle = "Liked";

            bool switchedToLiked = SongDataContainer == null || !ReferenceEquals(SongDataContainer.Collection, ViewModel.LikedSongs);
            _suppressSelectionChanged = true;
            try
            {
                if (switchedToLiked)
                {
                    if (SongDataContainer != null)
                    {
                        SongDataContainer.Collection = ViewModel.LikedSongs;
                    }
                    else
                    {
                        UploadedSongList.ItemsSource = ViewModel.LikedSongs;
                    }
                }
                else
                {
                    UploadedSongList.Items.Refresh();
                }

                SyncSelectionToCurrentSong();
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

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

            IEnumerable? currentCollection = SongDataContainer?.Collection;

            if (ReferenceEquals(currentCollection, ViewModel.Songs))
                _allViewScrollOffset = sv.VerticalOffset;
            else if (ReferenceEquals(currentCollection, ViewModel.LikedSongs))
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
            ViewModel.SetActivePlaylist(playlist);
            ViewModel.CurrentHeaderTitle = playlist.Title;

            // Uncheck All / Liked radio buttons
            AllSongsButton.IsChecked = false;
            LikedSongsButton.IsChecked = false;

            if (SongDataContainer != null)
            {
                SongDataContainer.Collection = ViewModel.ActivePlaylistSongs;
            }
            else
            {
                UploadedSongList.ItemsSource = ViewModel.ActivePlaylistSongs;
            }

            _suppressSelectionChanged = true;
            SyncSelectionToCurrentSong();
            _suppressSelectionChanged = false;
        }

        /// <summary>
        /// Switches right panel to selected playlist when a playlist sidebar item is clicked.
        /// </summary>
        private void PlaylistItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not RadioButton btn) return;
            if (btn.DataContext is not CreatedPlaylist playlist) return;

            ApplyPlaylistView(playlist);
        }

        /// <summary>
        /// Sidebar handler that activates the complete songs view.
        /// </summary>
        private void AllSongsButton_Checked(object sender, RoutedEventArgs e)
        {
            // Keep the right-side list in sync with the selected sidebar scope.
            ApplyAllSongsView();
        }

        /// <summary>
        /// Sidebar handler that activates the liked songs view.
        /// </summary>
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

        /// <summary>
        /// Minimal observer adapter used to handle AudioSwitcher volume notifications.
        /// </summary>
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
        /// <summary>
        /// Refreshes an existing song's duration after external conversion/import steps.
        /// </summary>
        internal void RefreshSongDurationFromFile(string filePath)
        {
            if (mediaImportService.RefreshSongDurationFromFile(ViewModel.Songs, filePath))
            {
                UpdateSongData();
            }
        }

        /// <summary>
        /// Handles local file import (audio direct copy or video-to-audio conversion).
        /// </summary>
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
                string fileExtension = Path.GetExtension(selectedFile).ToLower();
                Directory.CreateDirectory(mediaImportService.AudioStorageDirectory);

                //is video file
                if (mediaImportService.IsVideoExtension(fileExtension))
                {
                    string audioFilePath = mediaImportService.ConvertVideoToAudio(selectedFile);
                    AddFileToUI(audioFilePath);
                }
                //is audio file
                else if (mediaImportService.IsAudioExtension(fileExtension))
                {
                    string audioFilePath = Path.Combine(mediaImportService.AudioStorageDirectory, Path.GetFileName(selectedFile));
                    AddFileToUI(selectedFile);
                    File.Copy(selectedFile, audioFilePath, overwrite: true); // Copy file to output directory
                }
            }
        }

        /// <summary>
        /// Opens URL import window for online media download flow.
        /// </summary>
        private void SearchThroughURL_Click(object sender, RoutedEventArgs e)
        {
            SearchThroughURLWindow window = new SearchThroughURLWindow(this);
            window.Show();
        }

        /// <summary>
        /// Adds a track entry to the playlist UI and handles name collisions by
        /// prompting the user to replace, rename, or cancel.
        /// </summary>
        internal void AddFileToUI(string filePath)
        {
            ImportSongOutcome importOutcome = mediaImportService.ImportFileIntoSongs(
                ViewModel.Songs,
                filePath,
                ResolveDuplicateImportChoice);

            switch (importOutcome.Kind)
            {
                case ImportSongOutcomeKind.Added:
                case ImportSongOutcomeKind.Replaced:
                    UpdateSongData();
                    break;

                case ImportSongOutcomeKind.RenameRequested:
                    if (importOutcome.ExistingSong != null)
                    {
                        ViewModel.RenameSongCommand.Execute(importOutcome.ExistingSong);
                    }
                    break;
            }
        }

        private DuplicateImportChoice ResolveDuplicateImportChoice()
        {
            DuplicateFileDialog dialog = new DuplicateFileDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return DuplicateImportChoice.Cancel;
            }

            return dialog.SelectedAction switch
            {
                DuplicateFileDialog.DuplicateFileAction.Replace => DuplicateImportChoice.Replace,
                DuplicateFileDialog.DuplicateFileAction.Rename => DuplicateImportChoice.Rename,
                _ => DuplicateImportChoice.Cancel
            };
        }

        /// <summary>
        /// Opens add/import popup menu.
        /// </summary>
        private void PlusButton_Click(object sender, RoutedEventArgs e)
        {
            // Open the dropdown and disable the button
            PlusPopupMenu.IsOpen = true;
            PlusButton.IsEnabled = false;
        }

        /// <summary>
        /// Opens sorting popup menu.
        /// </summary>
        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            SortPopupMenu.IsOpen = true;
            SortButton.IsEnabled = false;
        }

        //----------------------------------------------------------------------------------


        //-------------------------  Song Functionality Implementation ---------------------
        /// <summary>
        /// Loads and plays the given song. Does not set the playback list or call SaveSongIndex.
        /// Returns true if playback started, false if file not found.
        /// </summary>
        private bool PlaySong(Song song)
        {
            SetCurrentSong(song);
            ViewModel.PlaySong(song);
            UpdatePlaybackButtonIcon();
            return ViewModel.IsPlaying;
        }

        /// <summary>
        /// Keeps the shared song row layout in sync with available width.
        /// Column order is fixed while widths stay user-adjustable.
        /// </summary>
        private void SongList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSongColumnLayout();
        }


        /// <summary>
        /// Handles list selection changes without mutating current playback state.
        /// Selection and playback are intentionally decoupled.
        /// </summary>
        private void PlaySelectedSong(object sender, SelectionChangedEventArgs? e)
        {
            if (_suppressSelectionChanged)
                return;

            if (UploadedSongList.SelectedItem is not Song selectedSong)
                return;

            playbackCoordinator.SetPlaybackList(GetCurrentSongList() ?? ViewModel.Songs);

            UpdatePlaybackButtonIcon();
            SaveSongIndex();
        }

        /// <summary>
        /// Handles app-level double-click detection for song rows.
        /// Double-click toggles play/pause for the current song, or starts a different song.
        /// </summary>
        private void UploadedSongList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            // Keep row action buttons (like/options/play-pause) in control of their own interactions.
            if (FindVisualParent<Button>(source) != null)
            {
                playbackCoordinator.ResetSongRowClickTracking();
                return;
            }

            var listViewItem = FindVisualParent<ListViewItem>(source);
            if (listViewItem?.DataContext is not Song song)
            {
                playbackCoordinator.ResetSongRowClickTracking();
                return;
            }

            bool isDoubleClick = playbackCoordinator.IsSongRowDoubleClick(
                song,
                Environment.TickCount64,
                SongRowDoubleClickThresholdMs);
            if (!isDoubleClick)
            {
                return;
            }

            playbackCoordinator.SetPlaybackList(GetCurrentSongList() ?? ViewModel.Songs);
            bool isCurrentSong = playbackCoordinator.IsCurrentSong(ViewModel.CurrentSong, song);

            if (isCurrentSong)
            {
                ViewModel.TogglePlay();
                UpdatePlaybackButtonIcon();
            }
            else
            {
                PlaySong(song);
            }

            SaveSongIndex();
            e.Handled = true;
            playbackCoordinator.ResetSongRowClickTracking();
        }

        /// <summary>
        /// Finds the nearest visual parent of the requested type.
        /// Used for locating ListViewItem from low-level input events.
        /// </summary>
        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T parent)
                {
                    return parent;
                }

                child = VisualTreeHelper.GetParent(child);
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

        /// <summary>
        /// Returns the internal ScrollViewer used by the song list control template.
        /// </summary>
        private static ScrollViewer? FindScrollViewer(ListBox listBox)
        {
            return FindVisualChildByType<ScrollViewer>(listBox);
        }

        /// <summary>
        /// Moves playback cursor to previous song in the active playback list.
        /// Wraps to end when currently at first song.
        /// </summary>
        private void PreviousSongClicked(object sender, RoutedEventArgs e)
        {
            var list = playbackCoordinator.PlaybackList ?? GetCurrentSongList() ?? ViewModel.Songs;
            if (list == null || list.Count == 0) return;
            Song? prevSong = playbackCoordinator.GetPreviousSong(list, ViewModel.CurrentSong);
            if (prevSong == null) return;
            PlaySong(prevSong);
            _suppressSelectionChanged = true;
            SyncSelectionToCurrentSong();
            _suppressSelectionChanged = false;
            if (ReferenceEquals(GetCurrentSongList(), playbackCoordinator.PlaybackList))
                SaveSongIndex();
        }

        /// <summary>
        /// Toggles play/pause for current song, or starts selected song if nothing is current yet.
        /// </summary>
        private void StopSongClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CurrentSong == null && UploadedSongList.SelectedItem is Song selectedSong)
            {
                playbackCoordinator.SetPlaybackList(GetCurrentSongList() ?? ViewModel.Songs);
                PlaySong(selectedSong);
                SaveSongIndex();
                return;
            }

            ViewModel.TogglePlay();
            UpdatePlaybackButtonIcon();
        }

        /// <summary>
        /// Moves playback cursor to next song in the active playback list.
        /// Wraps to beginning after last song.
        /// </summary>
        private void NextSongClicked(object sender, RoutedEventArgs e)
        {
            var list = playbackCoordinator.PlaybackList ?? GetCurrentSongList() ?? ViewModel.Songs;
            if (list == null || list.Count == 0) return;
            Song? nextSong = playbackCoordinator.GetNextSong(list, ViewModel.CurrentSong);
            if (nextSong == null) return;
            PlaySong(nextSong);
            _suppressSelectionChanged = true;
            SyncSelectionToCurrentSong();
            _suppressSelectionChanged = false;
            if (ReferenceEquals(GetCurrentSongList(), playbackCoordinator.PlaybackList))
                SaveSongIndex();
        }

        /// <summary>
        /// Auto-advances playback to next song when current media reaches end.
        /// </summary>
        private void MediaPlayer_MediaEnded(object? sender, EventArgs e)
        {
            var list = playbackCoordinator.PlaybackList ?? GetCurrentSongList() ?? ViewModel.Songs;
            if (list == null || list.Count == 0) return;
            Song? nextSong = playbackCoordinator.GetNextSong(list, ViewModel.CurrentSong);
            if (nextSong == null) return;
            PlaySong(nextSong);
            _suppressSelectionChanged = true;
            SyncSelectionToCurrentSong();
            _suppressSelectionChanged = false;
            if (ReferenceEquals(GetCurrentSongList(), playbackCoordinator.PlaybackList))
                SaveSongIndex();
        }



        /// <summary>
        /// Persists currently selected song index to user settings.
        /// </summary>
        private void SaveSongIndex()
        {
            var list = GetCurrentSongList() ?? ViewModel.Songs;
            int selectedSongIndex = playbackCoordinator.IndexOfSongInList(list, UploadedSongList.SelectedItem as Song);
            Properties.Settings.Default.LastSelectedIndex = selectedSongIndex;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Restores previously selected song index during startup.
        /// Selection handler is temporarily detached to avoid side effects while restoring.
        /// </summary>
        private void LoadSongIndex()
        {
            UploadedSongList.SelectionChanged -= PlaySelectedSong;

            int SongIndex = Properties.Settings.Default.LastSelectedIndex;
            var list = GetCurrentSongList() ?? ViewModel.Songs;
            if (SongIndex >= 0 && SongIndex < list.Count && list[SongIndex] is Song songToSelect)
            {
                UploadedSongList.SelectedItem = songToSelect;
            }
            else
            {
                UploadedSongList.SelectedIndex = -1;
            }

            UploadedSongList.SelectionChanged += PlaySelectedSong;
            ViewModel.IsPlaying = false;
            UpdatePlaybackButtonIcon();
        }

        //--------------------------- Slider And Volume ------------------------------------
        /// <summary>
        /// Applies requested value to default system playback device when change is significant.
        /// </summary>
        private void SetSystemVolume(double volume)
        {
            // Helper method to set system volume
            if (defaultPlaybackDevice != null && Math.Abs(defaultPlaybackDevice.Volume - volume) > 0.1)
            {
                defaultPlaybackDevice.Volume = volume;
            }
        }

        /// <summary>
        /// Writes system volume only while user is actively dragging the master volume slider.
        /// </summary>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDragging && currentSlider == VolumeSlider)
            {
                SetSystemVolume(VolumeSlider.Value);
            }
        }

        /// <summary>
        /// Starts seek interaction and captures mouse for smooth progress dragging.
        /// </summary>
        private void SongProgressSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ViewModel.IsSeeking = true;
            Slider_PreviewMouseLeftButtonDown(sender, e);
            SongProgressSlider.CaptureMouse();
        }

        /// <summary>
        /// Finishes seek interaction and applies final slider value to playback position.
        /// </summary>
        private void SongProgressSlider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Slider_PreviewMouseLeftButtonUp(sender, e);
            ViewModel.Seek(SongProgressSlider.Value);
            ViewModel.IsSeeking = false;
            SongProgressSlider.ReleaseMouseCapture();
        }

        /// <summary>
        /// Updates displayed current-time text while progress slider value changes.
        /// </summary>
        private void SongProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isDragging || ViewModel.TotalTimeSeconds > 0)
            {
                TimeSpan currentTime = TimeSpan.FromSeconds(SongProgressSlider.Value);
                ViewModel.CurrentTimeText = currentTime.ToString(@"mm\:ss");
            }
        }

        /// <summary>
        /// Shared slider drag-start handler used by volume/progress sliders.
        /// </summary>
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

        /// <summary>
        /// Shared slider drag-end handler used by volume/progress sliders.
        /// </summary>
        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Event handler for mouse up (stop dragging)
            isDragging = false;
            currentSlider?.ReleaseMouseCapture();
            currentSlider = null;
        }

        /// <summary>
        /// Shared slider drag-move handler used by volume/progress sliders.
        /// </summary>
        private void Slider_MouseMove(object sender, MouseEventArgs e)
        {
            // Event handler for mouse move (dragging)
            if (isDragging && currentSlider != null)
            {
                MoveSliderToMousePosition(currentSlider, e);
            }
        }

        /// <summary>
        /// Converts mouse X position to slider value within slider min/max range.
        /// </summary>
        private void MoveSliderToMousePosition(Slider slider, MouseEventArgs e)
        {
            // Common method to move any slider to the mouse position
            var mousePosition = e.GetPosition(slider);
            double percentage = mousePosition.X / slider.ActualWidth;
            slider.Value = percentage * (slider.Maximum - slider.Minimum) + slider.Minimum;
        }

        //----------------------------------------------------------------------------------



        //------------------------- Sorting Playlist Implementation ------------------------

        //----------------------------------------------------------------------------------


        //------------------------- Manage Close Drop Down ---------------------------------
        /// <summary>
        /// Determines whether a context menu overlay should close for the current click.
        /// </summary>
        private static bool ShouldDismissContextMenu(Grid overlay, FrameworkElement card)
        {
            return overlay.Visibility == Visibility.Visible && !card.IsMouseOver;
        }

        /// <summary>
        /// Global click handler that dismisses popups/menus when user clicks outside them.
        /// </summary>
        private void OnMainWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (InlineToastOverlay.Visibility == Visibility.Visible)
            {
                HideInlineToast();
            }

            if (ShouldDismissContextMenu(SongContextMenuOverlay, SongContextMenu))
            {
                CloseSongContextMenu(clearTarget: true);
            }

            if (ShouldDismissContextMenu(PlaylistContextMenuOverlay, PlaylistContextMenuCard))
            {
                ClosePlaylistContextMenu();
            }

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

        /// <summary>
        /// Closes transient popups when app loses focus.
        /// </summary>
        private void Window_Deactivated(object sender, EventArgs e)
        {
            CloseSongContextMenu(clearTarget: true);
            ClosePlaylistContextMenu();

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

        /// <summary>
        /// Persists provided song list snapshot to disk through file service.
        /// </summary>
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

        /// <summary>
        /// Serializable DTO for persisting user-customized song column widths.
        /// </summary>
        private sealed class SongColumnLayoutSnapshot
        {
            public double IndexWidth { get; set; } = SongColumnLayoutState.IndexDefaultWidth;
            public double OptionsWidth { get; set; } = SongColumnLayoutState.OptionsDefaultWidth;
            public double LikedWidth { get; set; } = SongColumnLayoutState.LikedDefaultWidth;
            public double VolumeWidth { get; set; } = SongColumnLayoutState.VolumeDefaultWidth;
            public double TimeWidth { get; set; } = SongColumnLayoutState.TimeDefaultWidth;
        }

        /// <summary>
        /// Loads persisted song-column widths from disk and applies them to shared layout state.
        /// </summary>
        private void LoadSongColumnLayout()
        {
            try
            {
                if (!File.Exists(SongColumnLayoutFilePath))
                {
                    return;
                }

                string json = File.ReadAllText(SongColumnLayoutFilePath);
                var snapshot = JsonConvert.DeserializeObject<SongColumnLayoutSnapshot>(json);
                if (snapshot == null)
                {
                    return;
                }

                SongColumnLayout.ApplyFixedWidths(
                    snapshot.IndexWidth,
                    snapshot.OptionsWidth,
                    snapshot.LikedWidth,
                    Math.Min(snapshot.VolumeWidth, SongColumnLayoutState.VolumeDefaultWidth),
                    Math.Min(snapshot.TimeWidth, SongColumnLayoutState.TimeDefaultWidth));
            }
            catch
            {
                // Non-critical: fall back to defaults when loading custom column widths fails.
            }
        }

        /// <summary>
        /// Persists current song-column width configuration to disk.
        /// </summary>
        private void SaveSongColumnLayout()
        {
            try
            {
                string? parentDirectory = Path.GetDirectoryName(SongColumnLayoutFilePath);
                if (!string.IsNullOrWhiteSpace(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                var snapshot = new SongColumnLayoutSnapshot
                {
                    IndexWidth = SongColumnLayout.IndexWidthValue,
                    OptionsWidth = SongColumnLayout.OptionsWidthValue,
                    LikedWidth = SongColumnLayout.LikedWidthValue,
                    VolumeWidth = SongColumnLayout.VolumeWidthValue,
                    TimeWidth = SongColumnLayout.TimeWidthValue
                };

                string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
                File.WriteAllText(SongColumnLayoutFilePath, json);
            }
            catch
            {
                // Non-critical: failing to persist custom column widths should not block app close.
            }
        }

        /// <summary>
        /// Loads songs from persistent storage and rebuilds liked-song projection.
        /// </summary>
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

        /// <summary>
        /// Re-indexes songs and saves updated song metadata to disk.
        /// </summary>
        private void UpdateSongData()
        {
            UpdateSongIndexes();
            SaveSongData(ViewModel.Songs.ToList());  // Save the current Songs collection to the JSON file
        }

        /// <summary>
        /// Recomputes song display indexes (01, 02, ...) from current in-memory order.
        /// </summary>
        private void UpdateSongIndexes()
        {
            int indexCounter = 1;
            foreach (var song in ViewModel.Songs)
            {
                song.Index = indexCounter.ToString("D2"); // Set or update the index property
                indexCounter++;
            }
        }

        /// <summary>
        /// Double-click/row-click handler that starts playback from clicked song.
        /// </summary>
        private void SongItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Song song)
            {
                playbackCoordinator.SetPlaybackList(GetCurrentSongList() ?? ViewModel.Songs);
                PlaySong(song);
                SaveSongIndex();
            }
        }

        //------------------------- Icon bar implementation --------------------------------
        /// <summary>
        /// Enables click-and-drag window movement from custom title bar and dismisses context menu.
        /// </summary>
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseSongContextMenu(clearTarget: true);
            ClosePlaylistContextMenu();
            CloseAddToPlaylistOverlay(clearPendingSong: true);

            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        /// <summary>
        /// Minimizes window.
        /// </summary>
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Toggles maximize/restore window state.
        /// </summary>
        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
                this.WindowState = WindowState.Normal;
            else
                this.WindowState = WindowState.Maximized;
        }

        /// <summary>
        /// Persists state and closes window from custom close button.
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SaveSongIndex(); // Save before closing
            SaveSongColumnLayout();
            ViewModel.SaveSongData();

            this.Close();
        }

        /// <summary>
        /// Ensures indexes, layout, and song metadata are persisted during window close lifecycle.
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            SaveSongIndex();
            SaveSongColumnLayout();
            ViewModel.SaveSongData();
        }

        //----------------------------------------------------------------------------------
    }
}
