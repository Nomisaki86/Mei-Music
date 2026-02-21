using System;
using System.Windows.Media;

namespace Mei_Music.Services
{
    /// <summary>
    /// Service for controlling audio playback, volume, and tracking progress.
    /// </summary>
    public interface IAudioPlayerService
    {
        double Volume { get; set; }
        TimeSpan Position { get; set; }
        TimeSpan NaturalDuration { get; }
        bool HasAudio { get; }

        event EventHandler MediaOpened;
        event EventHandler MediaEnded;
        event EventHandler<TimeSpan> PositionChanged;

        void Play(string filePath);
        void Play();
        void Pause();
        void Stop();
    }
}
