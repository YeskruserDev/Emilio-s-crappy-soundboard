using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace EmiliosSoundboard
{
    /// <summary>Borderless, non-interactive startup video overlay. Closes only when playback ends or fails.</summary>
    internal sealed class StartupVideoSplash : Window
    {
        private readonly MediaElement media;
        private readonly Action finished;
        private bool done;

        internal StartupVideoSplash(string videoPath, Action onFinished)
        {
            finished = onFinished ?? (() => { });
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;
            Background = Brushes.Black;

            media = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Stop,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false,
                Focusable = false,
                Volume = 1.0
            };
            Content = media;
            media.MediaOpened += (s, e) => media.Play();
            media.MediaEnded += (s, e) => Finish();
            media.MediaFailed += (s, e) => Finish();
            Closed += (s, e) => { try { media.Stop(); media.Close(); } catch { } };
            SourceInitialized += (s, e) => { try { media.Source = new Uri(videoPath, UriKind.Absolute); } catch { Finish(); } };
        }

        private void Finish()
        {
            if (done) return;
            done = true;
            try { Close(); } catch { }
            try { finished(); } catch { }
        }
    }
}
