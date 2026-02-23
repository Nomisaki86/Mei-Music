using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Mei_Music
{
    /// <summary>
    /// Dialog window for adjusting per-song volume.
    /// Uses slider drag handling to provide click-to-seek behavior consistent with main UI sliders.
    /// </summary>
    public partial class SongVolumeController : Window
    {
        /// <summary>
        /// True while the user is actively dragging a slider thumb.
        /// </summary>
        private bool isDragging = false;

        /// <summary>
        /// Slider currently being dragged when <see cref="isDragging"/> is true.
        /// </summary>
        private Slider? currentSlider;

        /// <summary>
        /// Current volume value exposed for binding in the dialog.
        /// </summary>
        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        /// <summary>
        /// Dependency property backing <see cref="Volume"/>.
        /// </summary>
        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register("Volume", typeof(double), typeof(SongVolumeController), new PropertyMetadata(50.0));

        /// <summary>
        /// Callback invoked whenever the dialog volume slider changes.
        /// Owner window/view-model uses this to persist and apply song volume.
        /// </summary>
        public Action<double>? VolumeChangedCallback { get; set; }

        /// <summary>
        /// Initializes dialog with the currently stored song volume.
        /// </summary>
        public SongVolumeController(double initialVolume)
        {
            InitializeComponent();
            DataContext = this;
            Volume = initialVolume;
        }

        /// <summary>
        /// Forwards updated volume value to caller-provided callback.
        /// </summary>
        private void SongVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VolumeChangedCallback?.Invoke(Volume);
        }

        /// <summary>
        /// Starts slider drag interaction and moves thumb to current mouse position.
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
        /// Finishes slider drag interaction and releases mouse capture.
        /// </summary>
        private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Event handler for mouse up (stop dragging)
            isDragging = false;
            currentSlider?.ReleaseMouseCapture();
            currentSlider = null;
        }

        /// <summary>
        /// While dragging, continuously updates slider value from mouse position.
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
        /// Converts mouse X position into slider value and syncs dialog <see cref="Volume"/>.
        /// </summary>
        private void MoveSliderToMousePosition(Slider slider, MouseEventArgs e)
        {
            // Common method to move any slider to the mouse position
            var mousePosition = e.GetPosition(slider);
            double percentage = mousePosition.X / slider.ActualWidth;
            slider.Value = percentage * (slider.Maximum - slider.Minimum) + slider.Minimum;
            
            if (slider == SongVolumeSlider)
            {
                Volume = slider.Value;
            }
        }
    }
}
