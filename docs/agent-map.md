# MeiMusic Agent Map

Purpose: quick routing guide so agents start in the right files and avoid broad scanning.

## Core Architecture

- `Mei Music/Views/MainWindow.xaml`
  Main shell layout and overlay hosts (context cards, create playlist card, add-to-playlist card).

- `Mei Music/Views/MainWindow.xaml.cs`
  Primary UI orchestration and event handling: song list interactions, context menus, overlays, playlist view switching, persistence wiring.

- `Mei Music/ViewModels/MainViewModel.cs`
  Main app state and commands: playback control, song actions, playlist CRUD, refresh/sort, persistence delegation.

- `Mei Music/Services/Engine/FileService.cs`
  JSON persistence for songs/playlists.

- `Mei Music/Services/Engine/AudioPlayerService.cs`
  Playback engine abstraction implementation.

- `Mei Music/Services/Engine/PlaybackCoordinator.cs`
  Playback flow coordination for list-based transport logic (current-song matching, prev/next wrap, row double-click tracking).

- `Mei Music/Services/MediaImportService.cs`
  Local import/conversion pipeline (extension checks, duplicate policy hooks, ffmpeg audio extraction, metadata duration refresh).

- `Mei Music/Services/UI/DialogService.cs`
  App dialogs and prompt interactions.

## Models

- `Mei Music/Models/Song.cs`
  Song metadata and row-level state.

- `Mei Music/Models/CreatedPlaylist.cs`
  Playlist metadata; playlist membership is `SongNames`.

- `Mei Music/Models/SongColumnLayoutState.cs`
  Shared song row column width/layout state.

## Views and Cards

- `Mei Music/Views/SongRowView.xaml` + `.cs`
  Song row UI, row actions (options/play-pause), emits row-level events.

- `Mei Music/Views/SongContextMenuCard.xaml` + `.cs`
  Song context menu UI and events (add/volume/rename/open/delete).

- `Mei Music/Views/PlaylistContextMenuCard.xaml` + `.cs`
  Playlist context menu UI and events.

- `Mei Music/Views/CreatePlaylistCard.xaml` + `.cs`
  Create playlist card UI, image select/crop, title input, create event payload.

- `Mei Music/Views/AddToPlaylistCard.xaml` + `.cs`
  Add-to-playlist picker card: select existing playlist or open create flow.

- `Mei Music/Views/SongVolumeController.xaml` + `.cs`
  Per-song volume dialog.

- `Mei Music/Views/SearchThroughURLWindow.xaml` + `.cs`
  URL import/download workflow UI.

- `Mei Music/Views/Dialogs/*.xaml` + `.cs`
  Support dialogs (duplicate file, delete confirm, image crop).

## Themes and Shared Styles

- `Mei Music/Themes/Colors.xaml`
  Color tokens and brushes.

- `Mei Music/Themes/Buttons.xaml`
  Shared button styles.

- `Mei Music/Themes/ContextMenuCards.xaml`
  Reusable context card styles/visual primitives.

- `Mei Music/Themes/SidebarCards.xaml`
  Sidebar card and playlist item styles.

- `Mei Music/Themes/ListBoxes.xaml`
  ListView/ListBox styles.

- `Mei Music/Themes/Sliders.xaml`
  Slider styles.

- `Mei Music/App.xaml`
  Global merged dictionaries and app-level resources.

## Converters

- `Mei Music/Converters/DisplayIndexConverter.cs`
  Row display index conversion.

- `Mei Music/Converters/PathToImageSourceConverter.cs`
  File path to image source conversion (playlist icons).

## Task-to-File Routing

Use this section first before searching.

- Song context menu behavior
  - Start: `MainWindow.xaml.cs`, `SongContextMenuCard.xaml`, `SongContextMenuCard.xaml.cs`

- Playlist sidebar behavior (select/create/delete/switch view)
  - Start: `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainViewModel.cs`, `CreatedPlaylist.cs`

- Create playlist flow (title/icon/crop + persistence)
  - Start: `CreatePlaylistCard.xaml`, `CreatePlaylistCard.xaml.cs`, `MainWindow.xaml.cs`, `MainViewModel.cs`

- Add song to playlist flow
  - Start: `AddToPlaylistCard.xaml`, `AddToPlaylistCard.xaml.cs`, `MainWindow.xaml.cs`, `CreatedPlaylist.cs`

- Song row action buttons and row visuals
  - Start: `SongRowView.xaml`, `SongRowView.xaml.cs`, `MainWindow.xaml.cs`

- Playback issues (play/pause/seek/ended/volume)
  - Start: `MainViewModel.cs`, `AudioPlayerService.cs`, `PlaybackCoordinator.cs`, `MainWindow.xaml.cs`

- Persistence/data loading issues (songs/playlists)
  - Start: `FileService.cs`, `IFileService.cs`, `MainWindow.xaml.cs`, `MainViewModel.cs`

- Local file import/conversion issues
  - Start: `MediaImportService.cs`, `IMediaImportService.cs`, `MainWindow.xaml.cs`, `SearchThroughURLWindow.xaml.cs`

- Styling tweaks for context menus/cards
  - Start: `Themes/ContextMenuCards.xaml`, then specific card XAML in `Views/`

- Global style or color tweaks
  - Start: `App.xaml`, then `Themes/*.xaml`

## Working Rules for Agents

- Prefer targeted reads in mapped files before any broad search.
- For large files (for example `MainWindow.xaml.cs`), read only relevant ranges first.
- Reuse recently gathered context; avoid duplicate reads.
- Ask one clarifying question if two or more routes are equally likely.
- Run full build/tests only when requested or when risk is high.

## Keep This Map Fresh

When adding a major feature, update:

- affected file entries above
- at least one Task-to-File route
- any new UI card/dialog/service path

## Post-Implementation Map Sync Checklist

Use this quick check at the end of each coding task:

1. Did we add a new file with behavioral logic?  
   - If yes, add it to the correct section above.
2. Did ownership change (where logic now lives)?  
   - If yes, revise the affected bullet descriptions.
3. Did event flow entry points change?  
   - If yes, update the matching Task-to-File route.
4. Did we introduce a new recurring task type?  
   - If yes, add a new Task-to-File route.
5. If none of the above changed, no map edit is needed.
