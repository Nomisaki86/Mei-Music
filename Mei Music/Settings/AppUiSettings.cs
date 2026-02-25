namespace Mei_Music
{
    /// <summary>
    /// Centralized UI behavior toggles used for fast visual A/B testing.
    /// </summary>
    internal static class AppUiSettings
    { 
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
    }
}
