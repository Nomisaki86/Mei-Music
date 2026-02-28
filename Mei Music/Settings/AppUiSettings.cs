namespace Mei_Music
{
    /// <summary>
    /// Centralized UI behavior toggles used for fast visual A/B testing.
    /// </summary>
    internal static class AppUiSettings
    { 
        /// <summary>
        /// Accent color used for the now-playing indicator and the playing song title.
        /// </summary>
        public static System.Windows.Media.Color NowPlayingAccentColor { get; set; } =
            System.Windows.Media.Color.FromArgb(0xFF, 0xBC, 0x2E, 0x2E);

        /// <summary>
        /// Auto-hide duration for inline toast notifications in seconds.
        /// </summary>
        public static double InlineToastDurationSeconds { get; set; } = 2;

        /// <summary>
        /// Corner radius of the inline toast pill.
        /// </summary>
        public static double InlineToastCornerRadius { get; set; } = 12;

        /// <summary>
        /// Toast placement options for inline notifications.
        /// </summary>
        public enum InlineToastPlacementMode
        {
            Center,
            MouseAnchored,
            BottomCenterAbovePlayBar
        }

        /// <summary>
        /// Determines where inline toast notifications are placed.
        /// </summary>
        public static InlineToastPlacementMode ToastPlacementMode { get; set; } = InlineToastPlacementMode.BottomCenterAbovePlayBar;

        public static double MouseAnchoredToastOffsetX { get; set; } = 0;
        public static double MouseAnchoredToastOffsetY { get; set; } = 0;

        public static double BottomCenterToastOffsetX { get; set; } = 0;
        public static double BottomCenterToastOffsetY { get; set; } = 5;

        /// <summary>
        /// Left padding (in pixels) for the playlist page header icon card.
        /// </summary>
        public static double PlaylistHeaderIconCardPaddingLeft { get; set; } = 12;

        /// <summary>
        /// Top padding (in pixels) for the playlist page header icon card.
        /// </summary>
        public static double PlaylistHeaderIconCardPaddingTop { get; set; } = 12;

        /// <summary>
        /// Size (width and height in pixels) of the playlist page header icon card.
        /// </summary>
        public static double PlaylistHeaderIconCardSize { get; set; } = 150;
    }
}
