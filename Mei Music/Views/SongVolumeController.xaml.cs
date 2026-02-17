using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Mei_Music
{
    /// <summary>
    /// Interaction logic for SongVolumeController.xaml
    /// </summary>
    public partial class SongVolumeController : Window
    {
        private bool isDragging = false;
        private Slider? currentSlider;

        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        public static readonly DependencyProperty VolumeProperty =
            DependencyProperty.Register("Volume", typeof(double), typeof(SongVolumeController), new PropertyMetadata(50.0));

        public Action<double>? VolumeChangedCallback { get; set; }

        public SongVolumeController(double initialVolume)
        {
            InitializeComponent();
            DataContext = this;
            Volume = initialVolume;
        }
        private void SongVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VolumeChangedCallback?.Invoke(Volume);
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
            }
        }
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
