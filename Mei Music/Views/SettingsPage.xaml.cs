using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Mei_Music
{
    public partial class SettingsPage : UserControl
    {
        private bool _isInitializing;
        private bool _isSyncingSettingsOverlayScrollBar;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;

            Color baseColor = GetCurrentAccentColor();
            AccentColorCanvas.SelectedColor = baseColor;

            _isInitializing = false;

            // Sync overlay scrollbar after layout (same pattern as song list).
            Dispatcher.BeginInvoke(new Action(SyncSettingsOverlayScrollBar), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void SettingsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            SyncSettingsOverlayScrollBar();
        }

        private void SyncSettingsOverlayScrollBar()
        {
            if (SettingsOverlayScrollBar == null || SettingsScrollViewer == null)
            {
                return;
            }

            _isSyncingSettingsOverlayScrollBar = true;
            try
            {
                double max = Math.Max(0, SettingsScrollViewer.ScrollableHeight);
                SettingsOverlayScrollBar.Visibility = max > 0 ? Visibility.Visible : Visibility.Collapsed;
                SettingsOverlayScrollBar.Maximum = max;
                SettingsOverlayScrollBar.ViewportSize = Math.Max(0, SettingsScrollViewer.ViewportHeight);
                SettingsOverlayScrollBar.LargeChange = Math.Max(1, SettingsScrollViewer.ViewportHeight * 0.9);
                SettingsOverlayScrollBar.Value = Math.Clamp(SettingsScrollViewer.VerticalOffset, 0, max);
            }
            finally
            {
                _isSyncingSettingsOverlayScrollBar = false;
            }
        }

        private void SettingsOverlayScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isSyncingSettingsOverlayScrollBar || SettingsScrollViewer == null)
            {
                return;
            }

            SettingsScrollViewer.ScrollToVerticalOffset(e.NewValue);
        }

        private static Color GetCurrentAccentColor()
        {
            if (Application.Current.Resources["NowPlayingAccentBrush"] is SolidColorBrush brush)
            {
                return brush.Color;
            }

            return AppUiSettings.NowPlayingAccentColor;
        }

        private void AccentColorCanvas_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e)
        {
            if (_isInitializing || e.NewValue == null)
            {
                return;
            }

            var color = e.NewValue.Value;

            // Reusing existing app UI brush configuration logic from AppUiSettings
            var solidColorBrush = new SolidColorBrush(color);
            Application.Current.Resources["NowPlayingAccentBrush"] = solidColorBrush;
            AppUiSettings.NowPlayingAccentColor = color;
        }
    }
}
