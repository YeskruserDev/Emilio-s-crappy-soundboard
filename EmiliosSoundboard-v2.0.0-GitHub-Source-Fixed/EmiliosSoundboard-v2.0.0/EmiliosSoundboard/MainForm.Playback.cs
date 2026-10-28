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
                player.Play(c.Item.Id, path);
                currentName = c.Item.Name;
                state = PlayState.Playing;
                progressShown = 0f;
                lastPos = TimeSpan.Zero;
                lastDur = player.Duration;
            }
            catch (Exception ex)
            {
                state = PlayState.Error;
                stateTimer = 3.5f;
                ShowToast("Could not play \"" + c.Item.Name + "\": " + ex.Message, Theme.Danger);
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
