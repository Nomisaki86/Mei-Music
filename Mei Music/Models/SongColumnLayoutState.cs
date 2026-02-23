using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mei_Music.Models
{
    /// <summary>
    /// Identifies song list columns used by resize and layout logic.
    /// </summary>
    public enum SongColumnKey
    {
        Index,
        Title,
        Liked,
        Time,
        Volume
    }

    /// <summary>
    /// Stores persistent width state for song-list columns and provides constrained resize behavior.
    /// The title column is computed from remaining viewport width after fixed columns are applied.
    /// </summary>
    public sealed partial class SongColumnLayoutState : ObservableObject
    {
        // Includes ListViewItem margins + row internal margins + a small right-edge safety buffer.
        public const double HorizontalChrome = 52;

        public const double IndexMinWidth = 40;
        public const double TitleMinWidth = 80;
        public const double OptionsMinWidth = 0;
        public const double LikedMinWidth = 48;
        public const double VolumeMinWidth = 32;
        public const double TimeMinWidth = 62;

        public const double IndexDefaultWidth = 50;
        public const double TitleDefaultWidth = 350;
        public const double OptionsDefaultWidth = 0;
        public const double LikedDefaultWidth = 68;
        public const double VolumeDefaultWidth = 36;
        public const double TimeDefaultWidth = 64;

        [ObservableProperty] private GridLength _indexWidth = new(IndexDefaultWidth);
        [ObservableProperty] private GridLength _titleWidth = new(TitleDefaultWidth);
        [ObservableProperty] private GridLength _optionsWidth = new(OptionsDefaultWidth);
        [ObservableProperty] private GridLength _likedWidth = new(LikedDefaultWidth);
        [ObservableProperty] private GridLength _volumeWidth = new(VolumeDefaultWidth);
        [ObservableProperty] private GridLength _timeWidth = new(TimeDefaultWidth);

        public double IndexWidthValue => IndexWidth.Value;
        public double TitleWidthValue => TitleWidth.Value;
        public double OptionsWidthValue => OptionsWidth.Value;
        public double LikedWidthValue => LikedWidth.Value;
        public double VolumeWidthValue => VolumeWidth.Value;
        public double TimeWidthValue => TimeWidth.Value;

        /// <summary>
        /// Applies persisted fixed-width columns while enforcing each column minimum width.
        /// </summary>
        public void ApplyFixedWidths(double index, double options, double liked, double volume, double time)
        {
            IndexWidth = Pixel(Math.Max(index, IndexMinWidth));
            OptionsWidth = Pixel(OptionsDefaultWidth);
            LikedWidth = Pixel(Math.Max(liked, LikedMinWidth));
            VolumeWidth = Pixel(Math.Max(volume, VolumeMinWidth));
            TimeWidth = Pixel(Math.Max(time, TimeMinWidth));
        }

        /// <summary>
        /// Handles drag-resize operations for column boundaries using viewport-aware constraints.
        /// </summary>
        public void ResizeBoundary(SongColumnKey rightColumn, double horizontalDelta, double viewportWidth)
        {
            if (viewportWidth <= 0)
            {
                return;
            }

            // Only the left boundary of the Liked column is user-draggable.
            if (rightColumn != SongColumnKey.Liked)
            {
                return;
            }

            // Dragging the left boundary right should make the Liked column narrower.
            double maxLikedWidth = GetMaxLikedWidth(viewportWidth);
            double nextLiked = Clamp(LikedWidthValue - horizontalDelta, LikedMinWidth, maxLikedWidth);
            LikedWidth = Pixel(nextLiked);
            UpdateTitleWidth(viewportWidth);
        }

        /// <summary>
        /// Recomputes title width so total row width fits viewport while preserving minimums.
        /// </summary>
        public void UpdateTitleWidth(double viewportWidth)
        {
            if (viewportWidth <= 0)
            {
                return;
            }

            double fixedWidthTotal = IndexWidthValue + LikedWidthValue + TimeWidthValue + VolumeWidthValue;
            double computedTitle = Math.Max(TitleMinWidth, viewportWidth - fixedWidthTotal - HorizontalChrome);
            TitleWidth = Pixel(computedTitle);
        }

        /// <summary>
        /// Computes the maximum allowed liked-column width while still leaving minimum title width.
        /// </summary>
        private double GetMaxLikedWidth(double viewportWidth)
        {
            double max = viewportWidth - HorizontalChrome - IndexWidthValue - VolumeWidthValue - TimeWidthValue - TitleMinWidth;
            return Math.Max(LikedMinWidth, max);
        }

        /// <summary>
        /// Creates a pixel-based <see cref="GridLength"/> from a numeric width value.
        /// </summary>
        private static GridLength Pixel(double value)
        {
            return new GridLength(value);
        }

        /// <summary>
        /// Clamps a numeric value to the inclusive [min, max] range.
        /// </summary>
        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
