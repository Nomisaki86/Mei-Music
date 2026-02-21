using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Mei_Music
{
    /// <summary>
    /// Image crop dialog: lets the user pan and zoom an image to select the desired square crop.
    /// On Save, the current viewport is rendered into a square PNG using RenderTargetBitmap.
    /// </summary>
    public partial class ImageCropDialog : Window
    {
        // The output path of the cropped image (set on Save, null on Cancel)
        public string? CroppedImagePath { get; private set; }

        private Point _dragStart;
        private bool _isDragging;

        // Track translate offsets so we can apply constraints
        private double _translateX;
        private double _translateY;

        public ImageCropDialog(string sourceImagePath)
        {
            InitializeComponent();
            
            // Wait for window to load and acquire actual width/height
            Loaded += (s, e) => LoadImage(sourceImagePath);
        }

        /// <summary>
        /// Loads the source image and sets the initial scale so the image fills the viewport.
        /// </summary>
        private void LoadImage(string sourceImagePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(sourceImagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            PreviewImage.Source = bitmap;

            // Wait for 1 tick so the Image actual width is available
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                FitImageToViewport(bitmap);
            }));
        }

        /// <summary>
        /// Scales the image so that it completely fills (covers) the viewport, centered.
        /// </summary>
        private void FitImageToViewport(BitmapImage bitmap)
        {
            double vpW = ViewportBorder.ActualWidth;
            double vpH = ViewportBorder.ActualHeight;

            if (vpW <= 0 || vpH <= 0 || bitmap.Width <= 0 || bitmap.Height <= 0)
                return;

            // Scale to cover: choose the larger dimension ratio
            double scaleX = vpW / bitmap.Width;
            double scaleY = vpH / bitmap.Height;
            double initialScale = Math.Max(scaleX, scaleY);

            ApplyScale(initialScale);
            ZoomSlider.Minimum = initialScale;          // cannot zoom out beyond this
            ZoomSlider.Maximum = Math.Max(5.0, initialScale * 3.0); // allow plenty of zoom
            ZoomSlider.Value   = initialScale;

            // Center the image in the viewport
            CenterImage();
        }

        /// <summary>Applies a uniform scale to the image transform.</summary>
        private void ApplyScale(double scale)
        {
            ImgScale.ScaleX = scale;
            ImgScale.ScaleY = scale;
        }

        /// <summary>Centers the image so it is always filling the viewport (no gaps).</summary>
        private void CenterImage()
        {
            if (PreviewImage.Source is not BitmapImage bmp) return;

            double vpW = ViewportBorder.ActualWidth;
            double vpH = ViewportBorder.ActualHeight;

            double scaledW = bmp.Width  * ImgScale.ScaleX;
            double scaledH = bmp.Height * ImgScale.ScaleY;

            _translateX = (vpW - scaledW) / 2.0;
            _translateY = (vpH - scaledH) / 2.0;
            ApplyTranslate();
        }

        /// <summary>Writes _translateX / _translateY to the transform, clamped so no edge is exposed.</summary>
        private void ApplyTranslate()
        {
            if (PreviewImage.Source is not BitmapImage bmp) return;

            double vpW = ViewportBorder.ActualWidth;
            double vpH = ViewportBorder.ActualHeight;
            double scaledW = bmp.Width  * ImgScale.ScaleX;
            double scaledH = bmp.Height * ImgScale.ScaleY;

            // Clamp: left edge ≤ 0, right edge ≥ vpW
            double minX = vpW - scaledW;
            double minY = vpH - scaledH;

            _translateX = Math.Min(0, Math.Max(minX, _translateX));
            _translateY = Math.Min(0, Math.Max(minY, _translateY));

            ImgTranslate.X = _translateX;
            ImgTranslate.Y = _translateY;
        }

        // ─── Zoom Slider ───────────────────────────────────────────────────────

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PreviewImage?.Source == null) return;

            if (PreviewImage.Source is not BitmapImage bmp) return;

            double vpW = ViewportBorder.ActualWidth;
            double vpH = ViewportBorder.ActualHeight;

            // Keep the center of the viewport as the zoom anchor
            double oldScale = ImgScale.ScaleX;
            double newScale = e.NewValue;

            double centerX = vpW  / 2.0;
            double centerY = vpH / 2.0;

            // Adjust translate so the zoom appears centered on the viewport center
            double ratio = newScale / oldScale;
            _translateX = centerX - ratio * (centerX - _translateX);
            _translateY = centerY - ratio * (centerY - _translateY);

            ApplyScale(newScale);
            ApplyTranslate();
        }

        // ─── Pan (Drag) ─────────────────────────────────────────────────────────

        private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(ImageCanvas);
            ImageCanvas.CaptureMouse();
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;

            Point current = e.GetPosition(ImageCanvas);
            double dx = current.X - _dragStart.X;
            double dy = current.Y - _dragStart.Y;
            _dragStart = current;

            _translateX += dx;
            _translateY += dy;
            ApplyTranslate();
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            ImageCanvas.ReleaseMouseCapture();
        }

        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Allow zooming via mouse wheel (delta 120 per notch)
            double delta = e.Delta > 0 ? 0.1 : -0.1;
            double newValue = Math.Clamp(ZoomSlider.Value + delta, ZoomSlider.Minimum, ZoomSlider.Maximum);
            ZoomSlider.Value = newValue;
        }

        // ─── Save / Cancel ──────────────────────────────────────────────────────

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            CroppedImagePath = RenderCrop();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CroppedImagePath = null;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Renders the current viewport to a square PNG in the system temp folder.
        /// Returns the path to the rendered file.
        /// </summary>
        private string RenderCrop()
        {
            // Read the display's actual DPI so we produce full-resolution output
            // (e.g. 600×600 px on a 150 % / 144 DPI screen instead of always 400×400)
            double dpiX = 96, dpiY = 96;
            var source = PresentationSource.FromVisual(ImageCanvas);
            if (source?.CompositionTarget != null)
            {
                dpiX = 96 * source.CompositionTarget.TransformToDevice.M11;
                dpiY = 96 * source.CompositionTarget.TransformToDevice.M22;
            }

            int pixelW = (int)Math.Round(ViewportBorder.ActualWidth  * dpiX / 96);
            int pixelH = (int)Math.Round(ViewportBorder.ActualHeight * dpiY / 96);

            var renderBitmap = new RenderTargetBitmap(pixelW, pixelH, dpiX, dpiY, PixelFormats.Pbgra32);
            renderBitmap.Render(ImageCanvas);

            // Write to a temp PNG — lossless, no further downscaling
            string tempPath = Path.Combine(Path.GetTempPath(), $"MeiMusic_crop_{Guid.NewGuid():N}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
            using var stream = File.OpenWrite(tempPath);
            encoder.Save(stream);

            return tempPath;
        }
    }
}
