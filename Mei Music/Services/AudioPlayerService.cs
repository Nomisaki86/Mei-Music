using System;
using System.Windows.Media;
using System.Windows.Threading;

namespace Mei_Music.Services
{
    public class AudioPlayerService : IAudioPlayerService
    {
        private readonly MediaPlayer _mediaPlayer;
        private readonly DispatcherTimer _timer;

        public event EventHandler? MediaOpened;
        public event EventHandler? MediaEnded;
        public event EventHandler<TimeSpan>? PositionChanged;

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

        public double Volume
        {
            get => _mediaPlayer.Volume * 100;
            set => _mediaPlayer.Volume = value / 100.0;
        }

        public TimeSpan Position
        {
            get => _mediaPlayer.Position;
            set => _mediaPlayer.Position = value;
        }

        public TimeSpan NaturalDuration => _mediaPlayer.NaturalDuration.HasTimeSpan 
            ? _mediaPlayer.NaturalDuration.TimeSpan 
            : TimeSpan.Zero;

        public bool HasAudio => _mediaPlayer.HasAudio;

        public void Play(string filePath)
        {
            _mediaPlayer.Open(new Uri(filePath));
            Play();
        }

        public void Play()
        {
            _mediaPlayer.Play();
            _timer.Start();
        }

        public void Pause()
        {
            _mediaPlayer.Pause();
            _timer.Stop();
        }

        public void Stop()
        {
            _mediaPlayer.Stop();
            _timer.Stop();
        }

        private void OnMediaOpened(object? sender, EventArgs e)
        {
            MediaOpened?.Invoke(this, EventArgs.Empty);
            // Trigger an initial position update when media opens
            PositionChanged?.Invoke(this, Position);
        }

        private void OnMediaEnded(object? sender, EventArgs e)
        {
            MediaEnded?.Invoke(this, EventArgs.Empty);
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_mediaPlayer.Source != null && _mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                PositionChanged?.Invoke(this, Position);
            }
        }
    }
}
