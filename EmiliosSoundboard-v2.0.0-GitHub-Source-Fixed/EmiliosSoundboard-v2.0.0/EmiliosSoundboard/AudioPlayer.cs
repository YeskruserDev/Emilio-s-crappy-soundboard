using System;
using NAudio.Wave;

namespace EmiliosSoundboard
{
    public sealed class PlaybackEndedEventArgs : EventArgs
    {
        public string Id { get; private set; }
        public Exception Error { get; private set; }

        public PlaybackEndedEventArgs(string id, Exception error)
        {
            Id = id;
            Error = error;
        }
    }

    /// <summary>
    /// Plays one sound at a time, once from start to finish. Starting a new sound silently replaces
    /// the previous one. The Ended event fires only when a sound finishes by itself (or fails).
    /// </summary>
    public sealed class AudioPlayer : IDisposable
    {
        private sealed class Session
        {
            public string Id;
            public WaveOutEvent Output;
            public AudioFileReader Reader;
            public EventHandler<StoppedEventArgs> Handler;
            public TimeSpan Total;
        }

        private Session current;
        private bool disposed;

        public event EventHandler<PlaybackEndedEventArgs> Ended;

        public bool IsPlaying { get { return current != null; } }

        private float volume = 1f;
        public float Volume { get { return volume; } set { volume = Math.Max(0f, Math.Min(1f, value)); var s = current; if (s != null && s.Reader != null) { try { s.Reader.Volume = volume; } catch { } } } }

        public string CurrentId { get { var s = current; return s == null ? null : s.Id; } }

        public TimeSpan Duration { get { var s = current; return s == null ? TimeSpan.Zero : s.Total; } }

        public TimeSpan Position
        {
            get
            {
                var s = current;
                if (s == null) return TimeSpan.Zero;
                try
                {
                    double bytesPerSecond = s.Reader.WaveFormat.AverageBytesPerSecond;
                    double seconds = s.Output.GetPosition() / bytesPerSecond;
                    var pos = TimeSpan.FromSeconds(Math.Max(0, seconds));
                    return pos > s.Total ? s.Total : pos;
                }
                catch
                {
                    return TimeSpan.Zero;
                }
            }
        }

        /// <summary>Checks that a file can be opened as audio and returns its length.</summary>
        public static bool TryProbe(string path, out TimeSpan duration, out string error)
        {
            duration = TimeSpan.Zero;
            error = null;
            try
            {
                var info = new System.IO.FileInfo(path);
                if (!info.Exists) { error = "The file could not be found."; return false; }
                if (info.Length == 0) { error = "The file is empty."; return false; }

                using (var reader = new AudioFileReader(path))
                {
                    duration = reader.TotalTime;
                }

                if (duration <= TimeSpan.Zero) { error = "The file does not contain any audio."; return false; }
                return true;
            }
            catch (Exception ex)
            {
                error = "It is not a supported audio file, or it is damaged. (" + ex.Message + ")";
                return false;
            }
        }

        /// <summary>Starts playing from the beginning. Throws if the file cannot be played.</summary>
        public void Play(string id, string path)
        {
            if (disposed) return;
            StopSilently();

            AudioFileReader reader = null;
            WaveOutEvent output = null;
            try
            {
                reader = new AudioFileReader(path);
                output = new WaveOutEvent();

                reader.Volume = volume;
                var session = new Session { Id = id, Output = output, Reader = reader, Total = reader.TotalTime };
                session.Handler = (sender, e) => OnStopped(session, e);
                output.PlaybackStopped += session.Handler;
                output.Init(reader);

                current = session;
                output.Play();
            }
            catch
            {
                current = null;
                if (output != null) { try { output.Dispose(); } catch { } }
                if (reader != null) { try { reader.Dispose(); } catch { } }
                throw;
            }
        }

        /// <summary>Stops the current sound without raising Ended.</summary>
        public void Stop()
        {
            StopSilently();
        }

        private void StopSilently()
        {
            var s = current;
            current = null;
            if (s != null) DisposeSession(s);
        }

        private void OnStopped(Session session, StoppedEventArgs e)
        {
            // Ignore late events from sessions that were already replaced or stopped.
            if (!ReferenceEquals(current, session)) return;

            current = null;
            string id = session.Id;
            DisposeSession(session);

            var handler = Ended;
            if (handler != null) handler(this, new PlaybackEndedEventArgs(id, e.Exception));
        }

        private static void DisposeSession(Session s)
        {
            try { s.Output.PlaybackStopped -= s.Handler; } catch { }
            try { s.Output.Dispose(); } catch { }
            try { s.Reader.Dispose(); } catch { }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            StopSilently();
        }
    }
}
