using System;
using System.IO;
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

        /// <summary>
        /// Starts playing from the beginning on the named output device (empty = system default).
        /// Throws if the file cannot be played.
        /// </summary>
        public void Play(string id, string path, string deviceName = null)
        {
            if (disposed) return;
            StopSilently();

            AudioFileReader reader = null;
            WaveOutEvent output = null;
            int deviceIndex = AudioOutput.Resolve(deviceName);
            string deviceLabel = AudioOutput.Describe(deviceIndex);
            try
            {
                reader = new AudioFileReader(path);
                output = new WaveOutEvent { DeviceNumber = deviceIndex };

                reader.Volume = volume;
                var session = new Session { Id = id, Output = output, Reader = reader, Total = reader.TotalTime };
                session.Handler = (sender, e) => OnStopped(session, e);
                output.PlaybackStopped += session.Handler;

                try
                {
                    output.Init(reader);
                }
                catch (Exception ex) when (deviceIndex != AudioOutputPicker.DefaultIndex)
                {
                    // A named device can refuse to open (taken exclusively, just unplugged, driver
                    // reset). Falling back to the default keeps the sound working.
                    Log.Warn("audio", "could not open output device \"" + deviceLabel + "\"; retrying on the system default", ex);

                    try { output.PlaybackStopped -= session.Handler; } catch { }
                    try { output.Dispose(); } catch { }

                    reader.Position = 0;
                    output = new WaveOutEvent();
                    session.Output = output;
                    session.Handler = (sender, e) => OnStopped(session, e);
                    output.PlaybackStopped += session.Handler;
                    deviceLabel = AudioOutputPicker.SystemDefault;
                    output.Init(reader);
                }

                current = session;
                output.Play();
                Log.Debug("audio", "playing \"" + Path.GetFileName(path) + "\" on " + deviceLabel);
            }
            catch (Exception ex)
            {
                current = null;
                if (output != null) { try { output.Dispose(); } catch { } }
                if (reader != null) { try { reader.Dispose(); } catch { } }
                Log.Error("audio", "playing \"" + Path.GetFileName(path) + "\" on " + deviceLabel + " failed", ex);
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
            if (e.Exception != null)
                Log.Error("audio", "playback of \"" + id + "\" stopped with an error", e.Exception);

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
