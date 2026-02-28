using System.Windows;
using System.Windows.Controls;

namespace Mei_Music
{
    public partial class PlaylistPageHeader : UserControl
    {
        public PlaylistPageHeader()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyUiSettings();
        }

        private void ApplyUiSettings()
        {
            double left = AppUiSettings.PlaylistHeaderIconCardPaddingLeft;
            double top = AppUiSettings.PlaylistHeaderIconCardPaddingTop;
            double size = AppUiSettings.PlaylistHeaderIconCardSize;

            const double rightMargin = 24;
            const double bottomMargin = 8;
            const double textRightMargin = 8;

            RootGrid.Margin = new System.Windows.Thickness(left, top, rightMargin, bottomMargin);
            TextStackPanel.Margin = new System.Windows.Thickness(left, top, textRightMargin, bottomMargin);

            IconColumn.Width = new GridLength(size + 20);

            IconCardBorder.Width = size;
            IconCardBorder.Height = size;
            IconCardBorder.CornerRadius = new CornerRadius(CornerRadiusForSize(size));

            PlaylistImageBorder.Width = size;
            PlaylistImageBorder.Height = size;
            PlaylistImageBorder.CornerRadius = new CornerRadius(CornerRadiusForSize(size));

            double glyphSize = 72.0 / 150.0 * size;
            AllSongsGlyph.Width = glyphSize;
            AllSongsGlyph.Height = glyphSize;
            LikedSongsGlyph.Width = glyphSize;
            LikedSongsGlyph.Height = glyphSize;
        }

        private static double CornerRadiusForSize(double size)
        {
            return 22.0 / 150.0 * size;
        }
    }
}

