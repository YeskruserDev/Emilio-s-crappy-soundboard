using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace EmiliosSoundboard
{
    /// <summary>Settings window. Changes apply immediately to the running app.</summary>
    internal sealed class SettingsDialog : ThemedForm
    {
        private readonly DataStore store;
        private readonly AppSettings settings;

        private ToggleSwitch animToggle, glowToggle, starToggle, hoverToggle, coolModeToggle, comicModeToggle, petToggle, verboseToggle;
        private RainbowTextLabel coolModeLabel;
        private GlowSlider intensity, soundVolume, musicVolume, petSize;
        private Label soundVolumeValue, musicVolumeValue, petSizeValue;
        private TextBox backgroundPath;
        private Label intensityValue;
        private ComboBox accentPicker, resolutionPicker, soundOutputPicker;
        private ComboBox outputPicker;
        private Label outputHint, logSummary;
        private TextBox startupVideoPath, startupPhotoPath, startupSoundPath;
        private SpaceBackdropPanel contentPanel;

        public SettingsDialog(DataStore store)
        {
            this.store = store;
            settings = store.Data.Settings;
            Log.Debug("settings", "building settings dialog");
            BackdropEffect = WindowEffects.Parse(settings.WindowEffect);

            Text = "Settings";
            FormBorderStyle = FormBorderStyle.Sizable;
            // The old fixed height put the data-folder path underneath the bottom buttons.
            // Extra vertical room keeps the library section and footer controls separate.
            ClientSize = new Size(S(620), S(820));
            MinimumSize = new Size(S(580), S(620));
            StartPosition = FormStartPosition.CenterParent;
            AutoScroll = false;
            contentPanel = new SpaceBackdropPanel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.Transparent, Padding = Padding.Empty };
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

            var performanceTitle = MakeLabel("PERFORMANCE / WINDOW RESOLUTION", 9.5f, FontStyle.Bold, Theme.Blue);
            performanceTitle.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(performanceTitle); y += S(23);
            resolutionPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(14, 18, 31), ForeColor = Theme.CoolWhite, FlatStyle = FlatStyle.Flat };
            resolutionPicker.Items.AddRange(new object[] { "Unlocked (resizable)", "1280 x 720 (standard)", "1024 x 576 (performance)", "960 x 540 (low performance)" });
            resolutionPicker.SetBounds(left, y, S(270), S(30));
            string initialResolution = String.IsNullOrWhiteSpace(settings.WindowResolution) ? "Unlocked" : settings.WindowResolution;
            resolutionPicker.SelectedItem = ResolutionLabel(initialResolution);
            if (resolutionPicker.SelectedIndex < 0) resolutionPicker.SelectedIndex = 0;
            resolutionPicker.SelectedIndexChanged += (s, e) =>
            {
                string selected = ResolutionValue(Convert.ToString(resolutionPicker.SelectedItem));
                settings.WindowResolution = selected;
                foreach (Form form in Application.OpenForms)
                    if (form is MainForm main) main.ApplyWindowResolution(selected);
            };
            contentPanel.Controls.Add(resolutionPicker);
            var performanceDesc = MakeLabel("Fixed window sizes can reduce rendering load on slower PCs. Choose unlocked to resize freely.", 8.5f, FontStyle.Regular, Theme.Muted);
            performanceDesc.SetBounds(left + S(280), y + S(1), width - S(280), S(42)); contentPanel.Controls.Add(performanceDesc);
            y += S(42);

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

            // Output device routing: the difference between "plays on my speakers" and "plays into
            // Discord". Names are stored, so a renumbered device list does not break the setting.
            var outTitle = MakeLabel("SOUND OUTPUT DEVICE", 10f, FontStyle.Bold, Theme.CoolWhite);
            outTitle.SetBounds(left, y, width - S(126), S(22)); contentPanel.Controls.Add(outTitle);
            var refreshOut = new GlowButton { Text = "REFRESH", Style = GlowStyle.Secondary };
            refreshOut.SetBounds(left + width - S(120), y - S(4), S(120), S(32));
            contentPanel.Controls.Add(refreshOut);
            y += S(24);
            outputPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(14, 18, 31), ForeColor = Theme.CoolWhite };
            outputPicker.SetBounds(left, y, width, S(30));
            contentPanel.Controls.Add(outputPicker);
            y += S(34);
            outputHint = MakeLabel("", 9f, FontStyle.Regular, Theme.Muted);
            outputHint.SetBounds(left, y, width, S(34)); contentPanel.Controls.Add(outputHint);
            y += S(38);
            FillOutputPicker(settings.OutputDevice);
            outputPicker.SelectedIndexChanged += (s, e) =>
            {
                settings.OutputDevice = SelectedOutputValue();
                Log.Info("audio", "sound output device set to " + (string.IsNullOrEmpty(settings.OutputDevice) ? "system default" : settings.OutputDevice));
            };
            refreshOut.Click += (s, e) =>
            {
                AudioOutput.Refresh();
                FillOutputPicker(settings.OutputDevice);
                Log.Info("audio", "playback device list refreshed");
            };

            var voiceOutputTitle = MakeLabel("VOICE FX OUTPUT (CHAT)", 10f, FontStyle.Bold, Theme.CoolWhite);
            voiceOutputTitle.SetBounds(left, y, width, S(22)); contentPanel.Controls.Add(voiceOutputTitle); y += S(24);
            soundOutputPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(14, 18, 31), ForeColor = Theme.CoolWhite, FlatStyle = FlatStyle.Flat, DropDownWidth = 440 };
            soundOutputPicker.SetBounds(left, y, width, S(30));
            PopulateSoundOutputPicker();
            soundOutputPicker.SelectedIndexChanged += (s, e) =>
            {
                var selected = soundOutputPicker.SelectedItem as SoundOutputChoice;
                settings.SoundOutputDeviceId = selected == null ? "" : selected.Id;
            };
            contentPanel.Controls.Add(soundOutputPicker); y += S(34);
            var voiceOutputDesc = MakeLabel("Processed microphone audio is sent to this endpoint. For Discord / WhatsApp, choose the paired virtual recording device as the microphone; GoXLR needs a mixer route to the mic mix. Soundboard routing above stays independent.", 8.5f, FontStyle.Regular, Theme.Muted);
            voiceOutputDesc.SetBounds(left, y, width, S(42)); contentPanel.Controls.Add(voiceOutputDesc); y += S(48);

            // Easter egg mode
            coolModeLabel = new RainbowTextLabel
            {
                Text = "COOL MODE  •  RAINBOW ROAD",
                Font = Theme.MakeFont(10f, FontStyle.Bold),
                ForeColor = Theme.Blue,
                BackColor = Color.Transparent,
                Animated = settings.CoolMode
            };
            coolModeLabel.SetBounds(left, y, width - S(78), S(24)); contentPanel.Controls.Add(coolModeLabel);
            coolModeToggle = new ToggleSwitch();
            coolModeToggle.SetBounds(left + width - S(60), y, S(60), S(34));
            coolModeToggle.SetCheckedSilently(settings.CoolMode); contentPanel.Controls.Add(coolModeToggle);
            var coolDesc = MakeLabel("Unlocks a colourful kart-racing-inspired cosmic theme.", 8.5f, FontStyle.Regular, Theme.Muted);
            coolDesc.SetBounds(left, y + S(23), width, S(19)); contentPanel.Controls.Add(coolDesc);
            coolModeToggle.CheckedChanged += (s, e) =>
            {
                settings.CoolMode = coolModeToggle.Checked;
                if (coolModeLabel != null) coolModeLabel.Animated = settings.CoolMode;
            };
            y += S(48);

            // Optional pop-art skin applied across the main window and themed dialogs.
            var comicTitle = MakeLabel("COMIC BOOK MODE", 10f, FontStyle.Bold, Color.FromArgb(255, 220, 75));
            comicTitle.SetBounds(left, y, width - S(78), S(24)); contentPanel.Controls.Add(comicTitle);
            comicModeToggle = new ToggleSwitch();
            comicModeToggle.SetBounds(left + width - S(60), y - S(2), S(60), S(34));
            comicModeToggle.SetCheckedSilently(settings.ComicStyleMode); contentPanel.Controls.Add(comicModeToggle);
            var comicDesc = MakeLabel("Ink outlines, halftone space dots, punchy pop-art buttons and bolder comic lettering.", 8.5f, FontStyle.Regular, Theme.Muted);
            comicDesc.SetBounds(left, y + S(22), width, S(20)); contentPanel.Controls.Add(comicDesc);
            comicModeToggle.CheckedChanged += (s, e) =>
            {
                settings.ComicStyleMode = comicModeToggle.Checked;
                MainForm.ApplyComicStyle(settings.ComicStyleMode);
            };
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

            // GoXLR support uses the device's normal Windows audio endpoints. It does not
            // directly control physical faders, lighting or GoXLR profiles.
            var goxlr = new GlowButton { Text = "GOXLR AUDIO SETUP", Style = GlowStyle.Secondary };
            goxlr.SetBounds(left, y, S(235), S(34));
            goxlr.Click += (s, e) => MessageBox.Show(this,
                "GoXLR audio endpoint support is available in Voice FX.\n\n" +
                "1. Connect your GoXLR and make sure Windows detects its recording and playback devices.\n" +
                "2. Open Voice FX and click REFRESH. GoXLR / TC-Helicon endpoints are marked [GoXLR].\n" +
                "3. Choose the microphone/input endpoint and the playback endpoint you want the processed voice sent to.\n" +
                "4. In Discord or another app, select the matching GoXLR virtual recording device as that app's microphone.\n\n" +
                "Use headphones and avoid sending the processed output back into the selected input. This is audio-device routing support; direct control of GoXLR faders, lighting and mixer profiles is not included.",
                "GoXLR audio setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
            contentPanel.Controls.Add(goxlr);
            var goxlrDesc = MakeLabel("Voice FX detects GoXLR / TC-Helicon audio endpoints. Direct mixer controls are not included.", 8f, FontStyle.Regular, Theme.Muted);
            goxlrDesc.SetBounds(left + S(245), y + S(2), width - S(245), S(34)); contentPanel.Controls.Add(goxlrDesc);
            y += S(44);

            var cableSetup = new GlowButton { Text = "GET VB-CABLE DRIVER", Style = GlowStyle.Secondary };
            cableSetup.SetBounds(left, y, S(200), S(34));
            cableSetup.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://vb-audio.com/Cable/") { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, "Open https://vb-audio.com/Cable/ to install a virtual audio cable.\n\n" + ex.Message, "Virtual microphone setup", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            };
            contentPanel.Controls.Add(cableSetup);
            var cableDesc = MakeLabel("A virtual audio driver is required for chat apps to receive processed audio. It must be installed separately; it cannot be bundled as a normal app file.", 8f, FontStyle.Regular, Theme.Muted);
            cableDesc.SetBounds(left + S(210), y + S(1), width - S(210), S(38)); contentPanel.Controls.Add(cableDesc);
            y += S(46);

            // Appearance customization
            y += S(2);
            var appearanceTitle = MakeLabel("APPEARANCE", 8.5f, FontStyle.Bold, Theme.Blue);
            appearanceTitle.SetBounds(left, y, width, S(20)); contentPanel.Controls.Add(appearanceTitle); y += S(24);
            accentPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(14, 18, 31), ForeColor = Theme.CoolWhite, FlatStyle = FlatStyle.Flat };
            accentPicker.Items.AddRange(new object[] { "Blue", "Violet", "Cyan", "Green", "Pink", "Orange", "Red", "White", "Custom" });
            string savedAccent = String.IsNullOrWhiteSpace(settings.AccentColor) ? "Blue" : settings.AccentColor;
            string[] namedAccents = { "Blue", "Violet", "Cyan", "Green", "Pink", "Orange", "Red", "White" };
            accentPicker.SelectedItem = Array.Exists(namedAccents, a => String.Equals(a, savedAccent, StringComparison.OrdinalIgnoreCase)) ? savedAccent : "Custom";
            if (accentPicker.SelectedIndex < 0) accentPicker.SelectedItem = "Blue";
            accentPicker.SetBounds(left, y, S(150), S(30));
            accentPicker.SelectedIndexChanged += (s, e) =>
            {
                string selected = Convert.ToString(accentPicker.SelectedItem);
                if (!String.Equals(selected, "Custom", StringComparison.OrdinalIgnoreCase))
                {
                    settings.AccentColor = selected;
                    MainForm.ApplyAccent(settings.AccentColor);
                }
            };
            contentPanel.Controls.Add(accentPicker);
            var pickAccent = new GlowButton { Text = "COLOUR WHEEL", Style = GlowStyle.Secondary };
            pickAccent.SetBounds(left + S(160), y - S(1), S(126), S(32));
            pickAccent.Click += (s, e) =>
            {
                using (var picker = new ColorWheelDialog(Theme.Blue))
                {
                    if (picker.ShowDialog(this) == DialogResult.OK)
                    {
                        settings.AccentColor = ColorTranslator.ToHtml(picker.SelectedColor).ToUpperInvariant();
                        if (accentPicker.Items.Contains("Custom")) accentPicker.SelectedItem = "Custom";
                        MainForm.ApplyAccent(settings.AccentColor);
                    }
                }
            };
            contentPanel.Controls.Add(pickAccent);
            var accentLabel = MakeLabel("Choose a preset or pick any custom accent colour. Search, buttons, glow and progress use the accent.", 8.5f, FontStyle.Regular, Theme.Muted);
            accentLabel.SetBounds(left, y + S(33), width, S(22)); contentPanel.Controls.Add(accentLabel); y += S(60);

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

            // Diagnostics: everything a bug report needs lives here, so nobody has to hunt for a folder.
            var diagTitle = MakeLabel("DIAGNOSTICS", 10f, FontStyle.Bold, Theme.Blue);
            diagTitle.SetBounds(left, y, width, S(22));
            contentPanel.Controls.Add(diagTitle);
            y += S(24);

            verboseToggle = AddToggleRow(ref y, left, width, "Detailed logging",
                "Also records audio devices, imports and painting details. Use this when reporting a bug.", settings.VerboseLogging);
            verboseToggle.CheckedChanged += (s, e) =>
            {
                settings.VerboseLogging = verboseToggle.Checked;
                ApplyLogLevel();
                RefreshLogSummary();
                Log.Info("log", "detailed logging " + (settings.VerboseLogging ? "enabled" : "disabled"));
            };

            logSummary = MakeLabel(LogSummaryText(), 8.5f, FontStyle.Regular, Theme.Muted);
            logSummary.AutoEllipsis = true;
            logSummary.SetBounds(left, y, width, S(20));
            contentPanel.Controls.Add(logSummary);
            y += S(22);

            var openLog = new GlowButton { Text = "OPEN LOG FOLDER", Style = GlowStyle.Secondary };
            openLog.SetBounds(left - S(5), y, S(190), S(38));
            openLog.Click += (s, e) => OpenLogFolder();
            contentPanel.Controls.Add(openLog);

            var copyLog = new GlowButton { Text = "COPY RECENT LOG", Style = GlowStyle.Secondary };
            copyLog.SetBounds(left + S(190), y, S(190), S(38));
            copyLog.Click += (s, e) => CopyRecentLog();
            contentPanel.Controls.Add(copyLog);
            y += S(48);

            var info = MakeLabel(BuildInfoText(), 10f, FontStyle.Regular, Theme.CoolWhite);
            info.SetBounds(left, y, width, S(44));
            contentPanel.Controls.Add(info);
            y += S(46);

            var path = MakeLabel(store.Root, 8.5f, FontStyle.Regular, Theme.Muted);
            path.AutoEllipsis = true;
            path.SetBounds(left, y, width, S(20));
            contentPanel.Controls.Add(path);
            y += S(24);

            var versionTitle = MakeLabel("EMILIO'S SOUNDBOARD  •  VERSION 2.0.1 BETA", 8.5f, FontStyle.Bold, Theme.CoolWhite);
            versionTitle.SetBounds(left, y, width, S(22));
            contentPanel.Controls.Add(versionTitle);
            var versionNote = MakeLabel("BETA build — features and behaviour may change.", 8f, FontStyle.Regular, Theme.Muted);
            versionNote.SetBounds(left, y + S(20), width, S(18));
            contentPanel.Controls.Add(versionNote);

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
            Log.Debug("settings", "settings dialog built");
        }

        private void PopulateSoundOutputPicker()
        {
            soundOutputPicker.Items.Clear();
            soundOutputPicker.Items.Add(new SoundOutputChoice("", "Default Windows output"));
            var endpoints = new List<SoundOutputChoice>();
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                    for (int i = 0; i < devices.Count; i++)
                    {
                        using (var endpoint = devices[i]) endpoints.Add(new SoundOutputChoice(endpoint.ID, endpoint.FriendlyName));
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine("Could not enumerate Voice FX output devices: " + ex); }

            foreach (var endpoint in endpoints.OrderByDescending(x => x.IsChatRoute).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                soundOutputPicker.Items.Add(endpoint);

            string selectedId = settings.SoundOutputDeviceId ?? "";
            for (int i = 0; i < soundOutputPicker.Items.Count; i++)
            {
                var item = soundOutputPicker.Items[i] as SoundOutputChoice;
                if (item != null && String.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                { soundOutputPicker.SelectedIndex = i; return; }
            }
            settings.SoundOutputDeviceId = ""; // A previously selected endpoint may have been unplugged.
            soundOutputPicker.SelectedIndex = 0;
        }

        private sealed class SoundOutputChoice
        {
            public string Id { get; private set; }
            public string Name { get; private set; }
            public bool IsChatRoute
            {
                get
                {
                    return Name.IndexOf("cable input", StringComparison.OrdinalIgnoreCase) >= 0
                        || Name.IndexOf("vb-audio", StringComparison.OrdinalIgnoreCase) >= 0
                        || Name.IndexOf("virtual cable", StringComparison.OrdinalIgnoreCase) >= 0
                        || Name.IndexOf("voicemeeter input", StringComparison.OrdinalIgnoreCase) >= 0
                        || Name.IndexOf("goxlr", StringComparison.OrdinalIgnoreCase) >= 0
                        || Name.IndexOf("tc-helicon", StringComparison.OrdinalIgnoreCase) >= 0
                        || Name.IndexOf("tc helicon", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            public SoundOutputChoice(string id, string name) { Id = id ?? ""; Name = name ?? "Unknown output"; }
            public override string ToString()
            {
                if (String.IsNullOrWhiteSpace(Id)) return Name;
                return IsChatRoute ? "[VIRTUAL / MIXER ROUTE] " + Name : Name;
            }
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
            if (coolModeLabel != null) coolModeLabel.Animated = settings.CoolMode;
            if (comicModeToggle != null) comicModeToggle.Checked = settings.ComicStyleMode;
            if (petToggle != null) petToggle.Checked = settings.DesktopPetEnabled;
            if (petSize != null) { petSize.SetValueSilently(settings.DesktopPetSize); if (petSizeValue != null) petSizeValue.Text = settings.DesktopPetSize + "%"; }
            intensity.Value = settings.Intensity;
            accentPicker.SelectedItem = settings.AccentColor;
            if (resolutionPicker != null) resolutionPicker.SelectedItem = "Unlocked (resizable)";
            if (soundOutputPicker != null) soundOutputPicker.SelectedIndex = 0;
            foreach (Form form in Application.OpenForms)
                if (form is MainForm main) main.ApplyWindowResolution("Unlocked");
            startupVideoPath.Text = settings.StartupVideoPath ?? "";
            if (startupPhotoPath != null) startupPhotoPath.Text = settings.StartupPhotoPath ?? "";
            if (startupSoundPath != null) startupSoundPath.Text = settings.StartupSoundPath ?? "";
            if (backgroundPath != null) backgroundPath.Text = settings.BackgroundImagePath ?? "";
            if (soundVolume != null) soundVolume.SetValueSilently(settings.SoundVolume);
            if (soundVolumeValue != null) soundVolumeValue.Text = settings.SoundVolume + "%";
            if (musicVolume != null) musicVolume.SetValueSilently(settings.MusicVolume);
            if (musicVolumeValue != null) musicVolumeValue.Text = settings.MusicVolume + "%";
            if (verboseToggle != null) verboseToggle.Checked = settings.VerboseLogging;
            ApplyLogLevel();
            RefreshLogSummary();
            FillOutputPicker(settings.OutputDevice);
            MainForm.ApplyAccent(settings.AccentColor);
            MainForm.ApplyComicStyle(settings.ComicStyleMode);
        }

        private void OpenFolder()
        {
            try
            {
                PlatformServices.OpenFolder(store.Root);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open the folder:\n" + ex.Message, "Settings",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static string ResolutionLabel(string value)
        {
            switch ((value ?? "Unlocked").Trim())
            {
                case "1280 x 720": return "1280 x 720 (standard)";
                case "1024 x 576": return "1024 x 576 (performance)";
                case "960 x 540": return "960 x 540 (low performance)";
                default: return "Unlocked (resizable)";
            }
        }

        private static string ResolutionValue(string label)
        {
            if (label != null && label.StartsWith("1280 x 720", StringComparison.Ordinal)) return "1280 x 720";
            if (label != null && label.StartsWith("1024 x 576", StringComparison.Ordinal)) return "1024 x 576";
            if (label != null && label.StartsWith("960 x 540", StringComparison.Ordinal)) return "960 x 540";
            return "Unlocked";
        }

        // ------------------------------------------------------------------ output device

        /// <summary>Fills the device list, keeping a vanished device visible instead of hiding it.</summary>
        private void FillOutputPicker(string selected)
        {
            if (outputPicker == null) return;

            string want = string.IsNullOrWhiteSpace(selected) ? AudioOutputPicker.SystemDefault : selected.Trim();
            outputPicker.Items.Clear();
            foreach (string name in AudioOutputPicker.Options(AudioOutput.Names(false))) outputPicker.Items.Add(name);

            int index = IndexOfItem(want);
            if (index < 0)
            {
                // The device was unplugged or renamed since it was chosen. Show it so the user can see
                // what is saved, and let playback fall back to the default.
                outputPicker.Items.Add(want);
                index = outputPicker.Items.Count - 1;
            }

            outputPicker.SelectedIndex = index;
            UpdateOutputHint(want, index == outputPicker.Items.Count - 1);
        }

        private int IndexOfItem(string value)
        {
            for (int i = 0; i < outputPicker.Items.Count; i++)
            {
                if (string.Equals(Convert.ToString(outputPicker.Items[i]), value, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>The value to persist: empty for the system default, otherwise the device name.</summary>
        private string SelectedOutputValue()
        {
            string selected = Convert.ToString(outputPicker == null ? null : outputPicker.SelectedItem);
            if (string.IsNullOrWhiteSpace(selected) ||
                string.Equals(selected, AudioOutputPicker.SystemDefault, StringComparison.OrdinalIgnoreCase))
                return "";
            return selected.Trim();
        }

        private void UpdateOutputHint(string selected, bool missing)
        {
            if (outputHint == null) return;
            int count = AudioOutput.Names(false).Count;

            if (missing)
            {
                outputHint.ForeColor = Color.FromArgb(255, 200, 110);
                outputHint.Text = "That device is not connected right now. Sounds will play on the system default until it comes back.";
                return;
            }

            outputHint.ForeColor = Theme.Muted;
            if (count == 0)
            {
                outputHint.Text = "No playback devices were reported. Check that Windows has an audio output enabled.";
                return;
            }

            outputHint.Text = string.Equals(selected, AudioOutputPicker.SystemDefault, StringComparison.OrdinalIgnoreCase)
                ? "Follows whatever Windows uses as the default output. Pick a virtual cable here to send sounds to Discord or OBS."
                : "Sounds play on this device and are mirrored to your Windows default output so you can still hear them locally. Use a virtual cable to feed Discord while your mic stays clean.";
        }

        // ------------------------------------------------------------------ diagnostics

        private void ApplyLogLevel()
        {
            var logger = Log.Current;
            if (logger != null)
                logger.MinimumLevel = (Program.VerboseRequested || settings.VerboseLogging)
                    ? LogLevel.Debug
                    : LogLevel.Info;
        }

        private string LogSummaryText()
        {
            var logger = Log.Current;
            if (logger == null) return "Logging is not active in this session.";
            return logger.Describe();
        }

        private void RefreshLogSummary()
        {
            if (logSummary != null) logSummary.Text = LogSummaryText();
        }

        private void OpenLogFolder()
        {
            var logger = Log.Current;
            if (logger == null)
            {
                MessageBox.Show(this, "Logging is not active in this session.", "Diagnostics",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Process.Start("explorer.exe", "\"" + logger.Directory + "\"");
            }
            catch (Exception ex)
            {
                Log.Warn("log", "could not open the log folder", ex);
                MessageBox.Show(this, "Could not open the log folder:\n" + logger.Directory + "\n\n" + ex.Message,
                    "Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CopyRecentLog()
        {
            var logger = Log.Current;
            if (logger == null)
            {
                MessageBox.Show(this, "Logging is not active in this session.", "Diagnostics",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                string text = logger.Tail(200);
                if (string.IsNullOrWhiteSpace(text)) text = "(the log is empty)";
                Clipboard.SetText(text);
                MessageBox.Show(this, "The last 200 log lines were copied to the clipboard.\n\nPaste them into the bug report.",
                    "Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log.Warn("log", "copying the log to the clipboard failed", ex);
                MessageBox.Show(this, "Could not copy the log:\n" + ex.Message, "Diagnostics",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
