using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Mei_Music.Converters
{
    /// <summary>
    /// Converts a file path string to an ImageSource for binding to Image.Source.
    /// Returns null if the path is null, empty, or the file does not exist.
    /// </summary>
    public class PathToImageSourceConverter : IValueConverter
    {
        /// <summary>
        /// Converts an image file path into a loaded <see cref="BitmapImage"/> for binding.
        /// Returns null if path is invalid, missing, or loading fails.
        /// </summary>
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Not supported because this converter is one-way from path to image source.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
