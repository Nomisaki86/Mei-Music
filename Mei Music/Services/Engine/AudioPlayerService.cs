using System;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mei_Music.Services
{
    /// <summary>
    /// MediaPlayer-based implementation of <see cref="IAudioPlayerService"/>.
    /// Owns low-level playback and emits normalized events for UI-facing layers.
    /// </summary>
    public class AudioPlayerService : IAudioPlayerService
    {
        /// <summary>
        /// WPF media engine responsible for decoding and playback.
        /// </summary>
        private readonly MediaPlayer _mediaPlayer;

        /// <summary>
        /// Periodic timer that publishes playback position updates while playing.
        /// </summary>
        private readonly DispatcherTimer _timer;

        public event EventHandler? MediaOpened;
        public event EventHandler? MediaEnded;
        public event EventHandler<TimeSpan>? PositionChanged;

        /// <summary>
        /// Creates the media player, wires media lifecycle events, and starts a 500ms position timer.
        /// </summary>
        public AudioPlayerService()
        {
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.MediaOpened += OnMediaOpened;
            _mediaPlayer.MediaEnded += OnMediaEnded;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += OnTimerTick;
        }

        /// <summary>
        /// Exposes volume in a UI-friendly 0-100 scale while MediaPlayer uses 0-1.
        /// </summary>
        public double Volume
        {
            get => _mediaPlayer.Volume * 100;
            set => _mediaPlayer.Volume = value / 100.0;
        }

        /// <summary>
        /// Gets/sets current playback position for seeking and progress display.
        /// </summary>
        public TimeSpan Position
        {
            get => _mediaPlayer.Position;
            set => _mediaPlayer.Position = value;
        }

        /// <summary>
        /// Duration of currently loaded media, or zero if metadata is not available yet.
        /// </summary>
        public TimeSpan NaturalDuration => _mediaPlayer.NaturalDuration.HasTimeSpan
            ? _mediaPlayer.NaturalDuration.TimeSpan
            : TimeSpan.Zero;

        /// <summary>
        /// True when MediaPlayer has an audio stream loaded.
        /// </summary>
        public bool HasAudio => _mediaPlayer.HasAudio;

        /// <summary>
        /// Opens the given file and starts playback.
        /// </summary>
        public void Play(string filePath)
        {
            _mediaPlayer.Open(new Uri(filePath));
            Play();
        }

        /// <summary>
        /// Resumes or starts playback for the already opened media source.
        /// </summary>
        public void Play()
        {
            _mediaPlayer.Play();
            _timer.Start();
        }

        /// <summary>
        /// Pauses playback and stops emitting timer-based position updates.
        /// </summary>
        public void Pause()
        {
            _mediaPlayer.Pause();
            _timer.Stop();
        }

        /// <summary>
        /// Stops playback and clears active timer updates.
        /// </summary>
        public void Stop()
        {
            _mediaPlayer.Stop();
            _timer.Stop();
        }

        /// <summary>
        /// Forwards MediaPlayer open notifications and pushes an immediate position update.
        /// </summary>
        private void OnMediaOpened(object? sender, EventArgs e)
        {
            MediaOpened?.Invoke(this, EventArgs.Empty);
            // Trigger an initial position update when media opens
            PositionChanged?.Invoke(this, Position);
        }

        /// <summary>
        /// Forwards end-of-playback notifications.
        /// </summary>
        private void OnMediaEnded(object? sender, EventArgs e)
        {
            MediaEnded?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Emits periodic playback position while media is loaded and duration is known.
        /// </summary>
        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_mediaPlayer.Source != null && _mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                PositionChanged?.Invoke(this, Position);
            }
        }
    }
}
