using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Drawing;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>Compact, non-interactive startup video popup sized to the soundboard window.</summary>
    internal sealed class StartupVideoSplash : Window
    {
        private readonly MediaElement media;
        private readonly Action finished;
        private readonly DispatcherTimer animationTimer;
        private readonly DateTime createdAt;
        private DateTime fadeOutAt;
        private bool done;
        private bool mediaStarted;
        private const double FadeInMs = 280.0;
        private const double FadeOutMs = 450.0;

        internal StartupVideoSplash(string videoPath, Form appWindow, Action onFinished)
        {
            finished = onFinished ?? (() => { });
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ShowInTaskbar = false;
            Topmost = true;
            ShowActivated = false;
            Focusable = false;
            IsHitTestVisible = false;
            Background = System.Windows.Media.Brushes.Black;
            Opacity = 0;

            var screen = Screen.FromControl(appWindow);
            double maxW = Math.Max(640, screen.WorkingArea.Width - 80);
            double maxH = Math.Max(440, screen.WorkingArea.Height - 100);
            Width = Math.Min(Math.Max(640, appWindow.Width), maxW);
            Height = Math.Min(Math.Max(440, appWindow.Height), maxH);
            Left = appWindow.Left + (appWindow.Width - Width) / 2.0;
            Top = appWindow.Top + (appWindow.Height - Height) / 2.0;

            media = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Stop,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false,
                Focusable = false,
                Volume = 0.0 // Startup sound is synchronized through the shared NAudio player.
            };
            Content = media;
            media.MediaOpened += (s, e) =>
            {
                try { media.Play(); mediaStarted = true; }
                catch { BeginFadeOut(); }
            };
            media.MediaEnded += (s, e) => BeginFadeOut();
            media.MediaFailed += (s, e) => BeginFadeOut();
            Closed += (s, e) => { try { animationTimer.Stop(); media.Stop(); media.Close(); } catch { } };
            SourceInitialized += (s, e) => { try { media.Source = new Uri(videoPath, UriKind.Absolute); } catch { BeginFadeOut(); } };

            createdAt = DateTime.UtcNow;
            animationTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
            animationTimer.Tick += (s, e) =>
            {
                if (done) return;
                var now = DateTime.UtcNow;
                if (!mediaStarted && (now - createdAt).TotalSeconds > 4.0) { BeginFadeOut(); return; }
                if (fadeOutAt != DateTime.MinValue)
                {
                    double progress = (now - fadeOutAt).TotalMilliseconds / FadeOutMs;
                    Opacity = Math.Max(0, 1.0 - progress);
                    if (progress >= 1.0) Finish();
                }
                else
                {
                    Opacity = Math.Min(1.0, (now - createdAt).TotalMilliseconds / FadeInMs);
                }
            };
            Loaded += (s, e) => animationTimer.Start();
        }

        private void BeginFadeOut()
        {
            if (done || fadeOutAt != DateTime.MinValue) return;
            fadeOutAt = DateTime.UtcNow;
        }

        private void Finish()
        {
            if (done) return;
            done = true;
            try { animationTimer.Stop(); } catch { }
            try { Close(); } catch { }
            try { finished(); } catch { }
        }
    }
}
