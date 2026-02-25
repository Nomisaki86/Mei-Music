using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Mei_Music
{
    /// <summary>Constants used by the create-playlist card UI behavior.</summary>
    internal static class CreatePlaylistCardConstants
    {
        /// <summary>Max length for the playlist title input; also used for character count display.</summary>
        public const int TitleMaxLength = 40;
    }
    /// <summary>Payload raised when user confirms playlist creation from the card.</summary>
    public sealed class CreatePlaylistClickedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates payload containing the playlist title and optional selected icon path.
        /// </summary>
        public CreatePlaylistClickedEventArgs(string title, string? selectedImageFilePath)
        {
            Title = title;
            SelectedImageFilePath = selectedImageFilePath;
        }

        /// <summary>
        /// Playlist title entered by the user.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Optional icon path selected/cropped by the user.
        /// </summary>
        public string? SelectedImageFilePath { get; }
    }

    /// <summary>Payload for drag-move: horizontal and vertical delta in device-independent pixels.</summary>
    public sealed class DragMoveDeltaEventArgs : EventArgs
    {
        /// <summary>
        /// Creates drag delta payload from the card header drag interaction.
        /// </summary>
        public DragMoveDeltaEventArgs(double horizontalChange, double verticalChange)
        {
            HorizontalChange = horizontalChange;
            VerticalChange = verticalChange;
        }

        /// <summary>
        /// Horizontal movement in device-independent pixels.
        /// </summary>
        public double HorizontalChange { get; }

        /// <summary>
        /// Vertical movement in device-independent pixels.
        /// </summary>
        public double VerticalChange { get; }
    }

    /// <summary>
    /// Overlay card for creating playlists.
    /// Handles title input, icon selection/cropping, drag-move, and emits high-level events.
    /// </summary>
    public partial class CreatePlaylistCard : UserControl
    {
        /// <summary>
        /// Raised when user confirms playlist creation.
        /// </summary>
        public event EventHandler<CreatePlaylistClickedEventArgs>? CreateClicked;

        /// <summary>
        /// Raised when user requests to close the card.
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// Raised continuously while user drags the header to move the card.
        /// </summary>
        public event EventHandler<DragMoveDeltaEventArgs>? DragMoveDelta;

        /// <summary>
        /// Last mouse position captured for drag delta computation.
        /// </summary>
        private Point _dragStart;

        /// <summary>
        /// Currently selected/cropped icon path for playlist creation.
        /// </summary>
        private string? _selectedImagePath;

        /// <summary>
        /// Initializes card visuals and default UI state.
        /// </summary>
        public CreatePlaylistCard()
        {
            InitializeComponent();
            UpdateCharCount();
            UpdateCreateButtonState();
        }

        /// <summary>
        /// Starts header drag operation by storing initial pointer position.
        /// </summary>
        private void HeaderDragBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            HeaderDragBorder.CaptureMouse();
        }

        /// <summary>
        /// Publishes drag delta while header is captured and left button is pressed.
        /// </summary>
        private void HeaderDragBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (HeaderDragBorder.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                Point current = e.GetPosition(null);
                double dx = current.X - _dragStart.X;
                double dy = current.Y - _dragStart.Y;
                _dragStart = current;
                DragMoveDelta?.Invoke(this, new DragMoveDeltaEventArgs(dx, dy));
            }
        }

        /// <summary>
        /// Ends header drag operation.
        /// </summary>
        private void HeaderDragBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (HeaderDragBorder.IsMouseCaptured)
                HeaderDragBorder.ReleaseMouseCapture();
        }

        /// <summary>
        /// Opens image picker and optional crop flow for playlist icon selection.
        /// </summary>
        private void ImageSelector_Click(object sender, MouseButtonEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Title = "Select Playlist Icon"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            // Open crop dialog so the user can pan/zoom to frame the icon before saving
            var cropDialog = new ImageCropDialog(openFileDialog.FileName)
            {
                Owner = Window.GetWindow(this)
            };

            if (cropDialog.ShowDialog() == true && cropDialog.CroppedImagePath != null)
            {
                // Replace any previous temp crop file to avoid temp folder buildup
                if (_selectedImagePath != null && _selectedImagePath != openFileDialog.FileName
                    && File.Exists(_selectedImagePath) && _selectedImagePath.Contains(Path.GetTempPath()[..10]))
                {
                    try { File.Delete(_selectedImagePath); } catch { /* Non-critical cleanup */ }
                }

                _selectedImagePath = cropDialog.CroppedImagePath;
                SelectedImage.Source = new BitmapImage(new Uri(_selectedImagePath));
                EmptyImagePlaceholder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Shows hover overlay when pointer enters image selector area.
        /// </summary>
        private void ImageSelector_MouseEnter(object sender, MouseEventArgs e)
        {
            HoverOverlay.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Hides hover overlay when pointer leaves image selector area.
        /// </summary>
        private void ImageSelector_MouseLeave(object sender, MouseEventArgs e)
        {
            HoverOverlay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Emits close request to host overlay.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Refreshes placeholder visibility when title box gains focus.
        /// </summary>
        private void TitleTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        /// <summary>
        /// Refreshes placeholder visibility when title box loses focus.
        /// </summary>
        private void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        /// <summary>
        /// Updates character count, placeholder, and create-button enabled state on text changes.
        /// </summary>
        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCharCount();
            UpdatePlaceholderVisibility();
            UpdateCreateButtonState();
        }

        /// <summary>
        /// Shows placeholder only when title input is empty.
        /// </summary>
        private void UpdatePlaceholderVisibility()
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(TitleTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Updates remaining character count based on title max length.
        /// </summary>
        private void UpdateCharCount()
        {
            int remaining = CreatePlaylistCardConstants.TitleMaxLength - TitleTextBox.Text.Length;
            CharCountText.Text = remaining.ToString();
        }

        /// <summary>
        /// Enables create action only when title contains non-whitespace text.
        /// </summary>
        private void UpdateCreateButtonState()
        {
            CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(TitleTextBox.Text);
        }

        /// <summary>
        /// Validates title and emits create event with current card input state.
        /// </summary>
        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            string title = TitleTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(title))
                return;
            CreateClicked?.Invoke(this, new CreatePlaylistClickedEventArgs(title, _selectedImagePath));
        }

        /// <summary>Resets card state before each open.</summary>
        public void Reset()
        {
            TitleTextBox.Text = string.Empty;
            _selectedImagePath = null;
            SelectedImage.Source = null;
            EmptyImagePlaceholder.Visibility = Visibility.Visible;
            HoverOverlay.Visibility = Visibility.Collapsed;
            UpdatePlaceholderVisibility();
            UpdateCharCount();
            UpdateCreateButtonState();
        }
    }
}
