using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>Settings window. Changes apply immediately to the running app.</summary>
    internal sealed class SettingsDialog : ThemedForm
    {
        private readonly DataStore store;
        private readonly AppSettings settings;

        private ToggleSwitch animToggle, glowToggle, starToggle, hoverToggle, coolModeToggle, petToggle;
        private GlowSlider intensity, soundVolume, musicVolume, petSize;
        private Label soundVolumeValue, musicVolumeValue, petSizeValue;
        private TextBox backgroundPath;
        private Label intensityValue;
        private ComboBox accentPicker;
        private TextBox startupVideoPath, startupPhotoPath, startupSoundPath;
        private Panel contentPanel;

        public SettingsDialog(DataStore store)
        {
            this.store = store;
            settings = store.Data.Settings;
            BackdropEffect = WindowEffects.Parse(settings.WindowEffect);

            Text = "Settings";
            FormBorderStyle = FormBorderStyle.Sizable;
            // The old fixed height put the data-folder path underneath the bottom buttons.
            // Extra vertical room keeps the library section and footer controls separate.
            ClientSize = new Size(S(620), S(820));
            MinimumSize = new Size(S(580), S(620));
            StartPosition = FormStartPosition.CenterParent;
            AutoScroll = false;
            contentPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Black, Padding = Padding.Empty };
            Controls.Add(contentPanel);

            int left = S(32);
            int width = ClientSize.Width - left * 2;
            int y = S(26);

            var title = MakeLabel("SETTINGS", 16f, FontStyle.Bold, Theme.CoolWhite);
            title.SetBounds(left, y, width, S(32));
            contentPanel.Controls.Add(title);
            y += S(46);

            animToggle = AddToggleRow(ref y, left, width, "Animated background",
                "Drifting, twinkling stars and floating dust.", settings.AnimatedBackground);
            glowToggle = AddToggleRow(ref y, left, width, "Cursor glow",
                "A soft blue light that follows your mouse.", settings.CursorGlow);
            starToggle = AddToggleRow(ref y, left, width, "Stars react to the cursor",
                "Nearby stars drift away from the pointer.", settings.StarReaction);
            hoverToggle = AddToggleRow(ref y, left, width, "Button hover animations",
                "Cards lift and glow when you point at them.", settings.HoverAnimations);

            var iTitle = MakeLabel("Animation intensity", 10.5f, FontStyle.Bold, Theme.CoolWhite);
            iTitle.SetBounds(left, y, width - S(70), S(24));
            contentPanel.Controls.Add(iTitle);

            intensityValue = MakeLabel(settings.Intensity + "%", 10.5f, FontStyle.Bold, Theme.Blue);
            intensityValue.TextAlign = ContentAlignment.TopRight;
            intensityValue.SetBounds(left + width - S(70), y, S(70), S(24));
            contentPanel.Controls.Add(intensityValue);
            y += S(26);

            var iDesc = MakeLabel("How strongly the background moves and reacts.", 9f, FontStyle.Regular, Theme.Muted);
            iDesc.SetBounds(left, y, width, S(20));
            contentPanel.Controls.Add(iDesc);
            y += S(24);

            intensity = new GlowSlider { Minimum = 0, Maximum = 100 };
            intensity.SetBounds(left - S(10), y, width + S(20), S(34));
            intensity.SetValueSilently(settings.Intensity);
            contentPanel.Controls.Add(intensity);
            y += S(48);

            var soundTitle = MakeLabel("SOUND EFFECT VOLUME", 10f, FontStyle.Bold, Theme.CoolWhite);
            soundTitle.SetBounds(left, y, width - S(70), S(22)); contentPanel.Controls.Add(soundTitle);
            soundVolumeValue = MakeLabel(settings.SoundVolume + "%", 10f, FontStyle.Bold, Theme.Blue);
            soundVolumeValue.TextAlign = ContentAlignment.TopRight; soundVolumeValue.SetBounds(left + width - S(70), y, S(70), S(22)); contentPanel.Controls.Add(soundVolumeValue);
            y += S(22);
            soundVolume = new GlowSlider { Minimum = 0, Maximum = 100 };
            soundVolume.SetBounds(left - S(10), y, width + S(20), S(30)); soundVolume.SetValueSilently(settings.SoundVolume); contentPanel.Controls.Add(soundVolume);
            soundVolume.ValueChanged += (s, e) => { settings.SoundVolume = soundVolume.Value; settings.Volume = soundVolume.Value; soundVolumeValue.Text = soundVolume.Value + "%"; };
            y += S(38);

            var musicTitle = MakeLabel("COOL MODE MUSIC VOLUME", 10f, FontStyle.Bold, Theme.CoolWhite);
            musicTitle.SetBounds(left, y, width - S(70), S(22)); contentPanel.Controls.Add(musicTitle);
            musicVolumeValue = MakeLabel(settings.MusicVolume + "%", 10f, FontStyle.Bold, Theme.Blue);
            musicVolumeValue.TextAlign = ContentAlignment.TopRight; musicVolumeValue.SetBounds(left + width - S(70), y, S(70), S(22)); contentPanel.Controls.Add(musicVolumeValue);
            y += S(22);
            musicVolume = new GlowSlider { Minimum = 0, Maximum = 100 };
            musicVolume.SetBounds(left - S(10), y, width + S(20), S(30)); musicVolume.SetValueSilently(settings.MusicVolume); contentPanel.Controls.Add(musicVolume);
            musicVolume.ValueChanged += (s, e) => { settings.MusicVolume = musicVolume.Value; musicVolumeValue.Text = musicVolume.Value + "%"; };
            y += S(46);

            // Easter egg mode
            var coolLabel = MakeLabel("COOL MODE  •  RAINBOW ROAD", 10f, FontStyle.Bold, Theme.Blue);
            coolLabel.SetBounds(left, y, width - S(78), S(24)); contentPanel.Controls.Add(coolLabel);
            coolModeToggle = new ToggleSwitch();
            coolModeToggle.SetBounds(left + width - S(60), y, S(60), S(34));
            coolModeToggle.SetCheckedSilently(settings.CoolMode); contentPanel.Controls.Add(coolModeToggle);
            var coolDesc = MakeLabel("Unlocks a colourful kart-racing-inspired cosmic theme.", 8.5f, FontStyle.Regular, Theme.Muted);
            coolDesc.SetBounds(left, y + S(23), width, S(19)); contentPanel.Controls.Add(coolDesc);
            coolModeToggle.CheckedChanged += (s, e) => settings.CoolMode = coolModeToggle.Checked;
            y += S(48);

            // Desktop pet: user-supplied image with slow wandering and occasional jump/spin animation.
            var petTitle = MakeLabel("DESKTOP PET", 10f, FontStyle.Bold, Theme.Blue);
            petTitle.SetBounds(left, y, width - S(80), S(24)); contentPanel.Controls.Add(petTitle);
            petToggle = new ToggleSwitch();
            petToggle.SetBounds(left + width - S(60), y - S(4), S(60), S(34));
            petToggle.SetCheckedSilently(settings.DesktopPetEnabled); contentPanel.Controls.Add(petToggle);
            petToggle.CheckedChanged += (s, e) => settings.DesktopPetEnabled = petToggle.Checked;
            y += S(28);
            var petDesc = MakeLabel("A custom image slowly wanders, then occasionally jumps and spins in mid-air.", 8.5f, FontStyle.Regular, Theme.Muted);
            petDesc.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(petDesc); y += S(24);
            var petPath = new TextBox { Text = settings.DesktopPetImagePath ?? "", BackColor = Color.FromArgb(14,18,31), ForeColor = Theme.CoolWhite, BorderStyle = BorderStyle.FixedSingle };
            petPath.SetBounds(left, y, width - S(204), S(28)); contentPanel.Controls.Add(petPath);
            var browsePet = new GlowButton { Text = "CHOOSE IMAGE", Style = GlowStyle.Secondary };
            browsePet.SetBounds(left + width - S(194), y - S(2), S(112), S(32));
            browsePet.Click += (s, e) => { using (var ofd = new OpenFileDialog { Title = "Choose your desktop pet image", Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*", CheckFileExists = true }) if (ofd.ShowDialog(this) == DialogResult.OK) petPath.Text = ofd.FileName; };
            contentPanel.Controls.Add(browsePet);
            var clearPet = new GlowButton { Text = "CLEAR", Style = GlowStyle.Secondary };
            clearPet.SetBounds(left + width - S(76), y - S(2), S(76), S(32));
            clearPet.Click += (s, e) => petPath.Text = ""; contentPanel.Controls.Add(clearPet);
            petPath.TextChanged += (s, e) => settings.DesktopPetImagePath = petPath.Text.Trim();
            y += S(38);
            var petSizeLabel = MakeLabel("PET SIZE", 9.5f, FontStyle.Bold, Theme.CoolWhite);
            petSizeLabel.SetBounds(left, y, width - S(80), S(22)); contentPanel.Controls.Add(petSizeLabel);
            petSizeValue = MakeLabel(Math.Max(40, Math.Min(300, settings.DesktopPetSize)) + "%", 9.5f, FontStyle.Bold, Theme.Blue);
            petSizeValue.TextAlign = ContentAlignment.TopRight; petSizeValue.SetBounds(left + width - S(70), y, S(70), S(22)); contentPanel.Controls.Add(petSizeValue);
            y += S(21);
            petSize = new GlowSlider { Minimum = 40, Maximum = 300 };
            petSize.SetBounds(left - S(10), y, width + S(20), S(30));
            petSize.SetValueSilently(Math.Max(40, Math.Min(300, settings.DesktopPetSize)));
            petSize.ValueChanged += (s, e) => { settings.DesktopPetSize = petSize.Value; petSizeValue.Text = petSize.Value + "%"; };
            contentPanel.Controls.Add(petSize);
            var petSizeDesc = MakeLabel("Adjust how large your pet appears in the soundboard.", 8.5f, FontStyle.Regular, Theme.Muted);
            petSizeDesc.SetBounds(left, y + S(30), width, S(20)); contentPanel.Controls.Add(petSizeDesc);
            y += S(58);

            // Future hardware mode scaffold; deliberately disabled until proper device integration is implemented.
            var goxlr = new GlowButton { Text = "GoXLR MODE  •  PLANNED", Style = GlowStyle.Secondary, Enabled = false };
            goxlr.SetBounds(left, y, S(235), S(34)); contentPanel.Controls.Add(goxlr);
            var goxlrDesc = MakeLabel("Future update: routing profiles and mixer controls for GoXLR.", 8f, FontStyle.Regular, Theme.Muted);
            goxlrDesc.SetBounds(left + S(245), y + S(2), width - S(245), S(34)); contentPanel.Controls.Add(goxlrDesc);
            y += S(44);

            // Appearance customization
            y += S(2);
            var appearanceTitle = MakeLabel("APPEARANCE", 8.5f, FontStyle.Bold, Theme.Blue);
            appearanceTitle.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(appearanceTitle); y += S(24);
            accentPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(14, 18, 31), ForeColor = Theme.CoolWhite };
            accentPicker.Items.AddRange(new object[] { "Blue", "Violet", "Cyan", "Green", "Pink", "Orange", "Red", "White" });
            accentPicker.SelectedItem = String.IsNullOrWhiteSpace(settings.AccentColor) ? "Blue" : settings.AccentColor;
            if (accentPicker.SelectedIndex < 0) accentPicker.SelectedIndex = 0;
            accentPicker.SetBounds(left, y, S(180), S(30));
            accentPicker.SelectedIndexChanged += (s, e) => { settings.AccentColor = Convert.ToString(accentPicker.SelectedItem); MainForm.ApplyAccent(settings.AccentColor); };
            contentPanel.Controls.Add(accentPicker);
            var accentLabel = MakeLabel("Changes highlights, borders, glow and progress accents", 9f, FontStyle.Regular, Theme.Muted);
            accentLabel.SetBounds(left + S(194), y + S(5), width - S(194), S(22)); contentPanel.Controls.Add(accentLabel); y += S(40);

            // Window material. Mica and Acrylic are DWM compositor effects, so they show on the
            // window frame and title bar; the painted content area stays opaque.
            var effectLabel = MakeLabel("WINDOW EFFECT", 8.5f, FontStyle.Bold, Theme.CoolWhite);
            effectLabel.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(effectLabel); y += S(22);
            var effectPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(14, 18, 31), ForeColor = Theme.CoolWhite };
            effectPicker.Items.AddRange(new object[] { "Mica", "Acrylic", "Off" });
            effectPicker.SelectedItem = WindowEffects.Describe(WindowEffects.Parse(settings.WindowEffect));
            effectPicker.SetBounds(left, y, S(180), S(30));
            effectPicker.SelectedIndexChanged += (s, e) =>
            {
                settings.WindowEffect = Convert.ToString(effectPicker.SelectedItem);
                BackdropEffect = WindowEffects.Parse(settings.WindowEffect);
                RefreshWindowEffect();
            };
            contentPanel.Controls.Add(effectPicker);
            var effectHint = MakeLabel(
                WindowEffects.Supported
                    ? "Windows 11 material for the window frame and title bar"
                    : "Needs Windows 11 22H2 or newer; ignored on this PC",
                9f, FontStyle.Regular, Theme.Muted);
            effectHint.SetBounds(left + S(194), y + S(5), width - S(194), S(34)); contentPanel.Controls.Add(effectHint);
            y += S(44);

            var bgLabel = MakeLabel("CUSTOM BACKGROUND IMAGE (OPTIONAL)", 8.5f, FontStyle.Bold, Theme.CoolWhite);
            bgLabel.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(bgLabel); y += S(22);
            backgroundPath = new TextBox { Text = settings.BackgroundImagePath ?? "", BackColor = Color.FromArgb(14,18,31), ForeColor = Theme.CoolWhite, BorderStyle = BorderStyle.FixedSingle };
            backgroundPath.SetBounds(left, y, width - S(204), S(28)); contentPanel.Controls.Add(backgroundPath);
            var browseBg = new GlowButton { Text = "BROWSE", Style = GlowStyle.Secondary };
            browseBg.SetBounds(left + width - S(194), y - S(2), S(92), S(32));
            browseBg.Click += (s, e) => { using (var ofd = new OpenFileDialog { Title = "Choose a background image", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*", CheckFileExists = true }) if (ofd.ShowDialog(this) == DialogResult.OK) backgroundPath.Text = ofd.FileName; };
            contentPanel.Controls.Add(browseBg);
            var clearBg = new GlowButton { Text = "REMOVE", Style = GlowStyle.Secondary };
            clearBg.SetBounds(left + width - S(96), y - S(2), S(96), S(32));
            clearBg.Click += (s, e) => { backgroundPath.Text = ""; settings.BackgroundImagePath = ""; };
            contentPanel.Controls.Add(clearBg);
            backgroundPath.TextChanged += (s, e) => settings.BackgroundImagePath = backgroundPath.Text.Trim(); y += S(38);
            var startupLabel = MakeLabel("OPTIONAL STARTUP VIDEO (PATH)", 8.5f, FontStyle.Bold, Theme.CoolWhite);
            startupLabel.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(startupLabel); y += S(22);
            startupVideoPath = new TextBox { Text = settings.StartupVideoPath ?? "", BackColor = Color.FromArgb(14,18,31), ForeColor = Theme.CoolWhite, BorderStyle = BorderStyle.FixedSingle };
            startupVideoPath.SetBounds(left, y, width - S(100), S(28)); contentPanel.Controls.Add(startupVideoPath);
            var browseVideo = new GlowButton { Text = "BROWSE", Style = GlowStyle.Secondary };
            browseVideo.SetBounds(left + width - S(92), y - S(2), S(96), S(32));
            browseVideo.Click += (s, e) => { using (var ofd = new OpenFileDialog { Title = "Choose a startup video", Filter = "Video files|*.mp4;*.wmv;*.avi;*.mov;*.mkv|All files|*.*", CheckFileExists = true }) if (ofd.ShowDialog(this) == DialogResult.OK) startupVideoPath.Text = ofd.FileName; };
            contentPanel.Controls.Add(browseVideo);
            startupVideoPath.TextChanged += (s, e) => { settings.StartupVideoPath = startupVideoPath.Text.Trim(); if (!String.IsNullOrWhiteSpace(startupVideoPath.Text) && startupPhotoPath != null && startupPhotoPath.Text.Length > 0) startupPhotoPath.Text = ""; }; y += S(38);

            var photoLabel = MakeLabel("OPTIONAL STARTUP PHOTO (CHOOSE VIDEO OR PHOTO)", 8.5f, FontStyle.Bold, Theme.CoolWhite);
            photoLabel.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(photoLabel); y += S(22);
            startupPhotoPath = new TextBox { Text = settings.StartupPhotoPath ?? "", BackColor = Color.FromArgb(14,18,31), ForeColor = Theme.CoolWhite, BorderStyle = BorderStyle.FixedSingle };
            startupPhotoPath.SetBounds(left, y, width - S(204), S(28)); contentPanel.Controls.Add(startupPhotoPath);
            var browsePhoto = new GlowButton { Text = "BROWSE", Style = GlowStyle.Secondary };
            browsePhoto.SetBounds(left + width - S(194), y - S(2), S(92), S(32));
            browsePhoto.Click += (s, e) => { using (var ofd = new OpenFileDialog { Title = "Choose a startup photo", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*", CheckFileExists = true }) if (ofd.ShowDialog(this) == DialogResult.OK) startupPhotoPath.Text = ofd.FileName; };
            contentPanel.Controls.Add(browsePhoto);
            var clearPhoto = new GlowButton { Text = "REMOVE", Style = GlowStyle.Secondary };
            clearPhoto.SetBounds(left + width - S(96), y - S(2), S(96), S(32));
            clearPhoto.Click += (s, e) => { startupPhotoPath.Text = ""; settings.StartupPhotoPath = ""; };
            contentPanel.Controls.Add(clearPhoto);
            startupPhotoPath.TextChanged += (s, e) => { settings.StartupPhotoPath = startupPhotoPath.Text.Trim(); if (!String.IsNullOrWhiteSpace(startupPhotoPath.Text) && startupVideoPath != null && startupVideoPath.Text.Length > 0) startupVideoPath.Text = ""; }; y += S(42);

            var startupNote = MakeLabel("The selected startup sound plays with your startup media.", 8.5f, FontStyle.Regular, Theme.Muted);
            startupNote.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(startupNote); y += S(24);

            var startupSoundLabel = MakeLabel("OPTIONAL STARTUP SOUND (MP3 / WAV / OGG)", 8.5f, FontStyle.Bold, Theme.CoolWhite);
            startupSoundLabel.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(startupSoundLabel); y += S(22);
            startupSoundPath = new TextBox { Text = settings.StartupSoundPath ?? "", BackColor = Color.FromArgb(14,18,31), ForeColor = Theme.CoolWhite, BorderStyle = BorderStyle.FixedSingle };
            startupSoundPath.SetBounds(left, y, width - S(204), S(28)); contentPanel.Controls.Add(startupSoundPath);
            var browseSound = new GlowButton { Text = "BROWSE", Style = GlowStyle.Secondary };
            browseSound.SetBounds(left + width - S(194), y - S(2), S(92), S(32));
            browseSound.Click += (s, e) => { using (var ofd = new OpenFileDialog { Title = "Choose a startup sound", Filter = "Audio files|*.mp3;*.wav;*.ogg;*.aiff;*.wma|All files|*.*", CheckFileExists = true }) if (ofd.ShowDialog(this) == DialogResult.OK) startupSoundPath.Text = ofd.FileName; };
            contentPanel.Controls.Add(browseSound);
            var clearSound = new GlowButton { Text = "DEFAULT", Style = GlowStyle.Secondary };
            clearSound.SetBounds(left + width - S(96), y - S(2), S(96), S(32));
            clearSound.Click += (s, e) => startupSoundPath.Text = "";
            contentPanel.Controls.Add(clearSound);
            startupSoundPath.TextChanged += (s, e) => settings.StartupSoundPath = startupSoundPath.Text.Trim();
            y += S(42);

            // Library information
            var infoTitle = MakeLabel("YOUR SOUND LIBRARY", 8.5f, FontStyle.Bold, Theme.Blue);
            infoTitle.SetBounds(left, y, width, S(20));
            contentPanel.Controls.Add(infoTitle);
            y += S(26);

            var info = MakeLabel(BuildInfoText(), 10f, FontStyle.Regular, Theme.CoolWhite);
            info.SetBounds(left, y, width, S(44));
            contentPanel.Controls.Add(info);
            y += S(46);

            var path = MakeLabel(store.Root, 8.5f, FontStyle.Regular, Theme.Muted);
            path.AutoEllipsis = true;
            path.SetBounds(left, y, width, S(20));
            contentPanel.Controls.Add(path);

            int btnY = ClientSize.Height - S(62);
            contentPanel.Padding = new Padding(0, 0, 0, S(72));

            var openBtn = new GlowButton { Text = "OPEN DATA FOLDER", Style = GlowStyle.Secondary };
            openBtn.SetBounds(left - S(5), btnY, S(190), S(46));
            openBtn.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            openBtn.Click += (s, e) => OpenFolder();
            Controls.Add(openBtn);

            var resetBtn = new GlowButton { Text = "RESET", Style = GlowStyle.Secondary };
            resetBtn.SetBounds(left + S(190), btnY, S(100), S(46));
            resetBtn.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
            resetBtn.Click += (s, e) => ResetDefaults();
            Controls.Add(resetBtn);

            var doneBtn = new GlowButton { Text = "DONE", Style = GlowStyle.Primary };
            doneBtn.SetBounds(ClientSize.Width - left - S(130) + S(5), btnY, S(135), S(46));
            doneBtn.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            doneBtn.Click += (s, e) => { DialogResult = DialogResult.OK; };
            Controls.Add(doneBtn);

            animToggle.CheckedChanged += (s, e) => settings.AnimatedBackground = animToggle.Checked;
            glowToggle.CheckedChanged += (s, e) => settings.CursorGlow = glowToggle.Checked;
            starToggle.CheckedChanged += (s, e) => settings.StarReaction = starToggle.Checked;
            hoverToggle.CheckedChanged += (s, e) => settings.HoverAnimations = hoverToggle.Checked;
            intensity.ValueChanged += (s, e) =>
            {
                settings.Intensity = intensity.Value;
                intensityValue.Text = intensity.Value + "%";
            };
        }

        private ToggleSwitch AddToggleRow(ref int y, int left, int width, string title, string description, bool value)
        {
            var t = MakeLabel(title, 10.5f, FontStyle.Bold, Theme.CoolWhite);
            t.SetBounds(left, y, width - S(70), S(24));
            contentPanel.Controls.Add(t);

            var d = MakeLabel(description, 9f, FontStyle.Regular, Theme.Muted);
            d.SetBounds(left, y + S(24), width - S(70), S(20));
            contentPanel.Controls.Add(d);

            var toggle = new ToggleSwitch();
            toggle.SetBounds(left + width - S(60), y + S(4), S(60), S(36));
            toggle.SetCheckedSilently(value);
            contentPanel.Controls.Add(toggle);

            y += S(58);
            return toggle;
        }

        private string BuildInfoText()
        {
            int count = store.Data.Sounds.Count;
            int missing = 0;
            foreach (var s in store.Data.Sounds)
                if (s.AudioMissing) missing++;

            string text = count + (count == 1 ? " sound" : " sounds") + "  -  " +
                          DataStore.FormatBytes(store.GetStorageBytes()) + " of audio and images stored";
            if (missing > 0)
                text += "\n" + missing + " sound file(s) are missing from the folder.";
            return text;
        }

        private void ResetDefaults()
        {
            settings.ResetToDefaults();
            animToggle.Checked = settings.AnimatedBackground;
            glowToggle.Checked = settings.CursorGlow;
            starToggle.Checked = settings.StarReaction;
            hoverToggle.Checked = settings.HoverAnimations;
            coolModeToggle.Checked = settings.CoolMode;
            if (petToggle != null) petToggle.Checked = settings.DesktopPetEnabled;
            if (petSize != null) { petSize.SetValueSilently(settings.DesktopPetSize); if (petSizeValue != null) petSizeValue.Text = settings.DesktopPetSize + "%"; }
            intensity.Value = settings.Intensity;
            accentPicker.SelectedItem = settings.AccentColor;
            startupVideoPath.Text = settings.StartupVideoPath ?? "";
            if (startupPhotoPath != null) startupPhotoPath.Text = settings.StartupPhotoPath ?? "";
            if (startupSoundPath != null) startupSoundPath.Text = settings.StartupSoundPath ?? "";
            if (backgroundPath != null) backgroundPath.Text = settings.BackgroundImagePath ?? "";
            if (soundVolume != null) soundVolume.SetValueSilently(settings.SoundVolume);
            if (soundVolumeValue != null) soundVolumeValue.Text = settings.SoundVolume + "%";
            if (musicVolume != null) musicVolume.SetValueSilently(settings.MusicVolume);
            if (musicVolumeValue != null) musicVolumeValue.Text = settings.MusicVolume + "%";
            MainForm.ApplyAccent(settings.AccentColor);
        }

        private void OpenFolder()
        {
            try
            {
                Process.Start("explorer.exe", "\"" + store.Root + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the folder:\n" + ex.Message, "Settings",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape || keyData == Keys.Enter)
            {
                if (!(ActiveControl is GlowButton) || keyData == Keys.Escape)
                {
                    DialogResult = DialogResult.OK;
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
