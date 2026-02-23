using System;
using System.Windows.Media;

namespace Mei_Music.Services
{
    /// <summary>
    /// Abstraction over the concrete media engine used by the app.
    /// Exposes playback, seeking, duration, and progress notifications to the ViewModel layer.
    /// </summary>
    public interface IAudioPlayerService
    {
        /// <summary>
        /// Playback volume in the 0-100 range used by app UI and persisted song settings.
        /// </summary>
        double Volume { get; set; }

        /// <summary>
        /// Current playback position within the loaded media.
        /// </summary>
        TimeSpan Position { get; set; }

        /// <summary>
        /// Total duration for the loaded media, or <see cref="TimeSpan.Zero"/> when unknown.
        /// </summary>
        TimeSpan NaturalDuration { get; }

        /// <summary>
        /// Indicates whether a playable audio source is currently loaded.
        /// </summary>
        bool HasAudio { get; }

        /// <summary>
        /// Raised once media metadata is available after loading/opening a file.
        /// </summary>
        event EventHandler MediaOpened;

        /// <summary>
        /// Raised when playback reaches the end of the loaded media.
        /// </summary>
        event EventHandler MediaEnded;

        /// <summary>
        /// Raised periodically while playing to publish the latest playback position.
        /// </summary>
        event EventHandler<TimeSpan> PositionChanged;

        /// <summary>
        /// Loads the specified file and starts playback.
        /// </summary>
        void Play(string filePath);

        /// <summary>
        /// Resumes playback of the currently loaded media.
        /// </summary>
        void Play();

        /// <summary>
        /// Pauses playback while keeping current position.
        /// </summary>
        void Pause();

        /// <summary>
        /// Stops playback and resets to media start.
        /// </summary>
        void Stop();
    }
}
