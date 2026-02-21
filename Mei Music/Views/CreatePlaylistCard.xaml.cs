using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Mei_Music
{
    /// <summary>Single source of truth for card corner radius (change once to affect main card and header).</summary>
    internal static class CreatePlaylistCardConstants
    {
        public const double CornerRadius = 15;
        /// <summary>Max length for the playlist title input; also used for character count display.</summary>
        public const int TitleMaxLength = 40;
    }
    /// <summary>Payload raised when user confirms playlist creation from the card.</summary>
    public sealed class CreatePlaylistClickedEventArgs : EventArgs
    {
        public CreatePlaylistClickedEventArgs(string title, string? selectedImageFilePath)
        {
            Title = title;
            SelectedImageFilePath = selectedImageFilePath;
        }

        public string Title { get; }
        public string? SelectedImageFilePath { get; }
    }

    /// <summary>Payload for drag-move: horizontal and vertical delta in device-independent pixels.</summary>
    public sealed class DragMoveDeltaEventArgs : EventArgs
    {
        public DragMoveDeltaEventArgs(double horizontalChange, double verticalChange)
        {
            HorizontalChange = horizontalChange;
            VerticalChange = verticalChange;
        }

        public double HorizontalChange { get; }
        public double VerticalChange { get; }
    }

    public partial class CreatePlaylistCard : UserControl
    {
        public event EventHandler<CreatePlaylistClickedEventArgs>? CreateClicked;
        public event EventHandler? CloseRequested;
        public event EventHandler<DragMoveDeltaEventArgs>? DragMoveDelta;

        private Point _dragStart;
        private string? _selectedImagePath;

        public CreatePlaylistCard()
        {
            InitializeComponent();
            double r = CreatePlaylistCardConstants.CornerRadius;
            CardBorder.CornerRadius = new CornerRadius(r);
            HeaderDragBorder.CornerRadius = new CornerRadius(r, r, 0, 0);
            UpdateCharCount();
            UpdateCreateButtonState();
        }

        private void HeaderDragBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            HeaderDragBorder.CaptureMouse();
        }

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

        private void HeaderDragBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (HeaderDragBorder.IsMouseCaptured)
                HeaderDragBorder.ReleaseMouseCapture();
        }

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

        private void ImageSelector_MouseEnter(object sender, MouseEventArgs e)
        {
            HoverOverlay.Visibility = Visibility.Visible;
        }

        private void ImageSelector_MouseLeave(object sender, MouseEventArgs e)
        {
            HoverOverlay.Visibility = Visibility.Collapsed;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void TitleTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdatePlaceholderVisibility();
        }

        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCharCount();
            UpdatePlaceholderVisibility();
            UpdateCreateButtonState();
        }

        private void UpdatePlaceholderVisibility()
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(TitleTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateCharCount()
        {
            int remaining = CreatePlaylistCardConstants.TitleMaxLength - TitleTextBox.Text.Length;
            CharCountText.Text = remaining.ToString();
        }

        private void UpdateCreateButtonState()
        {
            CreateButton.IsEnabled = !string.IsNullOrWhiteSpace(TitleTextBox.Text);
        }

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
