using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace EmiliosSoundboard
{
    /// <summary>Audio playback, progress, toasts and the settings dialog.</summary>
    internal sealed partial class MainForm : Form
    {
        // ------------------------------------------------------------------ playback

        private void PlayCard(Card c)
        {
            string path = store.AudioPath(c.Item);
            if (!File.Exists(path))
            {
                c.Item.AudioMissing = true;
                ShowToast("The audio file for \"" + c.Item.Name + "\" is missing. Delete this button and add the sound again.", Theme.Danger);
                return;
            }

            try
            {
                // A per-sound device overrides the board-wide one, so one sound can go to a virtual
                // cable while everything else stays on the speakers.
                string device = string.IsNullOrWhiteSpace(c.Item.OutputDevice)
                    ? store.Data.Settings.OutputDevice
                    : c.Item.OutputDevice;

                bool mirrorToLocal = AudioOutput.NeedsLocalMonitor(device);
                Log.Debug("playback", "requested device '" + (string.IsNullOrWhiteSpace(device) ? "System default" : device) +
                                      "', local monitor=" + mirrorToLocal);
                localMonitorPlayer.Stop();
                player.Play(c.Item.Id, path, device);
                if (mirrorToLocal)
                {
                    // The routed copy is authoritative; the local copy just keeps the clip audible
                    // on the user's speakers while it also feeds the virtual chat endpoint.
                    try { localMonitorPlayer.Play(c.Item.Id, path); }
                    catch (Exception monitorError)
                    {
                        Log.Warn("playback", "the local monitor copy could not start", monitorError);
                        ShowToast("Clip routed to chat, but local monitoring failed: " + monitorError.Message, Theme.Muted);
                    }
                }
                currentName = c.Item.Name;
                state = PlayState.Playing;
                progressShown = 0f;
                lastPos = TimeSpan.Zero;
                lastDur = player.Duration;
            }
            catch (Exception ex)
            {
                // If a chosen endpoint vanishes between selection and playback, make a best effort
                // to keep the sound audible through the Windows default output instead of failing.
                try
                {
                    player.Stop();
                    localMonitorPlayer.Play(c.Item.Id, path);
                    currentName = c.Item.Name;
                    state = PlayState.Playing;
                    progressShown = 0f;
                    lastPos = TimeSpan.Zero;
                    lastDur = localMonitorPlayer.Duration;
                    ShowToast("Selected output failed; playing locally instead.", Color.FromArgb(255, 190, 100));
                    Log.Warn("playback", "routed playback failed; playing locally", ex);
                }
                catch (Exception localError)
                {
                    state = PlayState.Error;
                    stateTimer = 3.5f;
                    ShowToast("Could not play \"" + c.Item.Name + "\": " + ex.Message + " / " + localError.Message, Theme.Danger);
                    Log.Error("playback", "could not play \"" + c.Item.Name + "\" on any output", ex);
                }
            }
            forceFrame = true;
        }

        private void OnPlayerEnded(object sender, PlaybackEndedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnPlayerEnded(sender, e)));
                return;
            }

            if (e.Error != null)
            {
                localMonitorPlayer.Stop();
                state = PlayState.Error;
                stateTimer = 3.5f;
                ShowToast("Playback stopped unexpectedly: " + e.Error.Message, Theme.Danger);
            }
            else
            {
                state = PlayState.Complete;
                stateTimer = 1.6f;
                lastPos = lastDur;
            }
            forceFrame = true;
        }

        private void OnLocalMonitorEnded(object sender, PlaybackEndedEventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnLocalMonitorEnded(sender, e)));
                return;
            }
            // The routed output is authoritative when both streams are active. This handler only
            // updates the playback status when the routed output failed and PlayCard fell back to
            // local-only playback.
            if (player.IsPlaying) return;
            if (e.Error != null)
            {
                state = PlayState.Error;
                stateTimer = 3.5f;
                ShowToast("Local playback stopped unexpectedly: " + e.Error.Message, Theme.Danger);
            }
            else
            {
                state = PlayState.Complete;
                stateTimer = 1.6f;
                lastPos = lastDur;
            }
            forceFrame = true;
        }

        private void OpenVoiceFxWindow()
        {
            if (voiceFxWindow == null || voiceFxWindow.IsDisposed)
            {
                var voice = new VoiceFxForm(store);
                voiceFxWindow = voice;
                voice.FormClosed += (s, e) => { if (ReferenceEquals(voiceFxWindow, voice)) voiceFxWindow = null; forceFrame = true; };
                voice.Show(this); // Keep soundboard controls usable while live voice processing runs.
            }
            else
            {
                if (!voiceFxWindow.Visible) voiceFxWindow.Show(this);
                voiceFxWindow.WindowState = FormWindowState.Normal;
                voiceFxWindow.BringToFront();
                voiceFxWindow.Activate();
            }
        }

        private void ShowToast(string text, Color color)
        {
            toastText = text;
            toastColor = color;
            toastLife = 3.8f;
            forceFrame = true;
        }

        private void ShowSettings()
        {
            using (var dlg = new SettingsDialog(store))
                dlg.ShowDialog(this);
            SaveLibrary();
            forceFrame = true;
        }
    }
}
