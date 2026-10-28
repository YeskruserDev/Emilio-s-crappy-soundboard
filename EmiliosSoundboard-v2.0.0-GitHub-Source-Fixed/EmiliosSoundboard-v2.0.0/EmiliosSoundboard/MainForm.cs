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
    internal enum PlayState { Ready, Playing, Complete, Error }

    /// <summary>
    /// The main window. Everything (background, title, cards, progress bar) is painted by hand in one
    /// double-buffered surface, so there are no designer files and nothing to flicker.
    /// </summary>
    internal sealed partial class MainForm : Form
    {
        private const float HeaderH = 150f;
        private const float CardH = 150f;
        private const float FooterPanelH = 88f;
        private const float FooterMargin = 24f;

        private readonly DataStore store = new DataStore { ImageImporter = new GdiImageImporter() };
        private readonly AudioPlayer player = new AudioPlayer();
        // When clips are routed to a virtual chat cable/mixer endpoint, mirror them to the normal
        // Windows output as well so the user can still hear the sound effects locally.
        private readonly AudioPlayer localMonitorPlayer = new AudioPlayer();
        private readonly AudioPlayer coolMusic = new AudioPlayer();
        private readonly AudioPlayer startupAudio = new AudioPlayer();
        private VoiceFxForm voiceFxWindow;
        private readonly DiscordPresence discordPresence = new DiscordPresence();
        private bool previousCoolMode;
        private string loadedBackgroundPath = "";
        private Button sidebarArrow;
        private Bitmap marioKartSprite;
        private Bitmap desktopPetSprite;
        private string loadedPetPath = "";
        private float petX = 100f;
        private float petSpin;
        private float petJump;
        private float petJumpVelocity;
        private float petNextJump = 5.2f;
        private float petDirection = 1f;
        private readonly Random petRandom = new Random();
        private enum PetBehaviour { Walking, Pausing, Looking }
        private PetBehaviour petBehaviour = PetBehaviour.Walking;
        private float petBehaviourTimer = 5f;
        private float petLookAngle;
        private float petWalkPhase;
        private float petWalkBob;
        private float petLandingSquash;
        private bool sidebarOpen = true;
        private readonly SpaceField field = new SpaceField();
        private readonly List<Card> cards = new List<Card>();
        // Visible + sorted card list, rebuilt once per frame instead of on every paint and tick.
        private readonly List<Card> view = new List<Card>();
        private bool viewReady;
        // Cached audio durations so the LENGTH sort never opens a file per comparison.
        private readonly Dictionary<string, long> durationCache = new Dictionary<string, long>();
        private readonly Timer timer = new Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly float scale;
        private Icon appIcon;
        private TextBox searchBox;
        private Panel searchBoxFrame;
        private Label searchTitle;
        private FlowLayoutPanel searchTags;
        private RectangleF searchGlassRect;
        private string sortMode = "ALL";
        private Bitmap galaxyBackground;

        private float time, hazeTime;
        private float mx, my;
        private bool hasMouse;
        private float glowX, glowY, glowLevel, bgLevel = 1f, reactLevel = 1f, fieldSettle = 2f;
        private bool glowInit;
        private bool forceFrame = true;

        private Hit hover, pressed;
        private bool pressing;
        private float addHover, addPress, setHover, gearAngle, emptyHover;
        private float titleSpinTime, titleSpinElapsed, titleSpinAngle;

        private float scrollY, scrollTarget, maxScroll, scrollFade;

        private PlayState state = PlayState.Ready;
        private string currentName = "";
        private float progressShown, stateTimer;
        private TimeSpan lastPos = TimeSpan.Zero, lastDur = TimeSpan.Zero;

        private string toastText = "";
        private Color toastColor = Theme.Blue;
        private float toastLife;

        private bool importing;

        private float LW { get { return ClientSize.Width / scale; } }
        private float LH { get { return ClientSize.Height / scale; } }
        private float GridTop { get { return HeaderH; } }
        private float GridBottom { get { return LH - FooterMargin - FooterPanelH - 14f; } }
        private float ContentLeft { get { return LW >= 850f && sidebarOpen ? 238f : 18f; } }


        public MainForm()
        {
            scale = Theme.DpiScale();
            Log.Debug("mainform", "constructor start (dpi scale " + scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")");
            AutoScaleMode = AutoScaleMode.None;
            Text = "Emilio's Soundboard";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Black;
            ClientSize = new Size((int)(1040 * scale), (int)(740 * scale));
            MinimumSize = SizeFromClientSize(new Size((int)(640 * scale), (int)(560 * scale)));

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;

            try
            {
                appIcon = Theme.CreateAppIcon();
                Icon = appIcon;
            }
            catch
            {
                // The default icon is fine.
            }

            // Build the base font snapshot in normal mode first, then apply the saved comic skin
            // after the controls have been created so switching it off can restore typography.
            Theme.ComicStyleMode = false;
            CreateFonts();
            // Default background is the original animated starfield. A custom image is only loaded when the user chooses one.

            store.Load();
            var set = store.Data.Settings;
            ApplyLogLevel();
            Log.Info("library", store.Data.Sounds.Count + " sound(s) loaded from " + store.Root);
            if (store.LoadWarning != null) Log.Warn("library", "the saved library could not be read: " + store.LoadWarning);
            if (!string.IsNullOrEmpty(set.OutputDevice)) Log.Info("audio", "board output device: " + set.OutputDevice);
            // Keep the actual soundboard hidden until a configured startup splash finishes.
            bool hasStartupSplash = (!String.IsNullOrWhiteSpace(set.StartupPhotoPath) && File.Exists(set.StartupPhotoPath)) ||
                                    (!String.IsNullOrWhiteSpace(set.StartupVideoPath) && File.Exists(set.StartupVideoPath));
            if (hasStartupSplash) Opacity = 0;
            ApplyAccent(set.AccentColor);
            ApplyWindowResolution(set.WindowResolution);
            bgLevel = set.AnimatedBackground ? 1f : 0f;
            reactLevel = set.StarReaction ? 1f : 0f;
            CreateSearchUi();
            sidebarArrow = new Button { Text = "�?�", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 8, 11, 22), ForeColor = Theme.CoolWhite, Font = new Font("Comic Sans MS", 15f, FontStyle.Bold), Size = new Size((int)(28 * scale), (int)(44 * scale)), TabStop = false };
            sidebarArrow.FlatAppearance.BorderColor = Theme.Blue; sidebarArrow.FlatAppearance.BorderSize = 1;
            sidebarArrow.Click += (s, e) => { sidebarOpen = !sidebarOpen; sidebarArrow.Text = sidebarOpen ? "�?�" : "�?�"; PositionSearchUi(); LayoutCards(); Invalidate(); };
            Controls.Add(sidebarArrow); sidebarArrow.BringToFront(); PositionSearchUi();
            if (set.ComicStyleMode)
            {
                Theme.ComicStyleMode = true;
                Theme.ApplyComicTypography(this);
                RefreshComicTheme();
            }
            try { marioKartSprite = new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarioKart.png")); marioKartSprite.RotateFlip(RotateFlipType.RotateNoneFlipX); } catch { marioKartSprite = null; }
            player.Volume = set.SoundVolume / 100f; localMonitorPlayer.Volume = set.SoundVolume / 100f; coolMusic.Volume = set.MusicVolume / 100f; startupAudio.Volume = 0.8f; previousCoolMode = false;
            coolMusic.Ended += OnCoolMusicEnded;

            int index = 0;
            foreach (var item in store.Data.Sounds)
                AddCardFor(item, Math.Min(index++, 10));

            player.Ended += OnPlayerEnded;
            localMonitorPlayer.Ended += OnLocalMonitorEnded;

            timer.Interval = 30; // ~33 FPS reduces idle CPU usage while keeping motion smooth
            timer.Tick += OnTick;
            timer.Start();
            Shown += (s, e) => StartStartupExperience();
            FormClosed += (s, e) => { try { startupAudio.Dispose(); } catch { } try { if (marioKartSprite != null) marioKartSprite.Dispose(); } catch { } try { if (desktopPetSprite != null) desktopPetSprite.Dispose(); } catch { } };
            Log.Debug("mainform", "constructor finished: " + cards.Count + " card(s), comic=" + set.ComicStyleMode + ", resolution=" + set.WindowResolution);
        }

        private void StartStartupExperience()
        {
            string defaultSoundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WindowsXPStartup.mp3");
            string customSoundPath = store.Data.Settings.StartupSoundPath ?? "";
            string soundPath = !String.IsNullOrWhiteSpace(customSoundPath) && File.Exists(customSoundPath) ? customSoundPath : defaultSoundPath;
            string photoPath = store.Data.Settings.StartupPhotoPath ?? "";
            string videoPath = store.Data.Settings.StartupVideoPath ?? "";
            try
            {
                if (!String.IsNullOrWhiteSpace(videoPath) && File.Exists(videoPath))
                {
                    // A borderless, non-interactive splash owns the screen until the video ends.
                    // The main soundboard stays transparent so it cannot be used early.
                    var splash = new StartupVideoSplash(videoPath, this, () =>
                    {
                        try { startupAudio.Stop(); } catch { }
                        try { Opacity = 1; Activate(); } catch { }
                    });
                    splash.Show();
                    if (File.Exists(soundPath)) startupAudio.Play("startup-sound", soundPath, store.Data.Settings.OutputDevice);
                }
                else if (!String.IsNullOrWhiteSpace(photoPath) && File.Exists(photoPath))
                {
                    // Show the photo in a compact, app-sized popup rather than taking over the whole screen.
                    int popupW = Math.Min(Math.Max(640, Width), Screen.FromControl(this).WorkingArea.Width - 80);
                    int popupH = Math.Min(Math.Max(440, Height), Screen.FromControl(this).WorkingArea.Height - 100);
                    var splash = new Form { Text = "Emilio's Soundboard", FormBorderStyle = FormBorderStyle.None,
                        StartPosition = FormStartPosition.Manual, Size = new Size(popupW, popupH), BackColor = Color.Black,
                        ShowInTaskbar = false, TopMost = true, ControlBox = false, Opacity = 0.0 };
                    var ownerBounds = Bounds;
                    splash.Location = new Point(ownerBounds.Left + (ownerBounds.Width - popupW) / 2, ownerBounds.Top + (ownerBounds.Height - popupH) / 2);
                    Image img = Image.FromFile(photoPath);
                    var view = new PictureBox { Dock = DockStyle.Fill, Image = img, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black, TabStop = false };
                    splash.Controls.Add(view);
                    var life = new Stopwatch();
                    var closeTimer = new Timer { Interval = 30 };
                    closeTimer.Tick += (a, b) =>
                    {
                        double elapsed = life.Elapsed.TotalMilliseconds;
                        if (elapsed < 300) splash.Opacity = Math.Min(1.0, elapsed / 300.0);
                        else if (elapsed > 4000) splash.Opacity = Math.Max(0.0, 1.0 - (elapsed - 4000) / 500.0);
                        if (elapsed >= 4500)
                        {
                            closeTimer.Stop(); closeTimer.Dispose();
                            try { startupAudio.Stop(); splash.Close(); splash.Dispose(); img.Dispose(); Opacity = 1; Activate(); } catch { }
                        }
                    };
                    splash.Shown += (a, b) => { life.Restart(); closeTimer.Start(); };
                    splash.FormClosed += (a, b) => { try { closeTimer.Stop(); closeTimer.Dispose(); } catch { } };
                    splash.Show();
                    if (File.Exists(soundPath)) startupAudio.Play("startup-sound", soundPath, store.Data.Settings.OutputDevice);
                }
                else
                {
                    Opacity = 1;
                }
            }
            catch (Exception ex)
            {
                Opacity = 1;
                try { MessageBox.Show(this, "Could not play the custom startup media:\n" + ex.Message, "Startup", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }
            }
        }

        internal static void ApplyAccent(string name)
        {
            Color accent;
            string key = (name ?? "Blue").Trim();
            if (key.StartsWith("#", StringComparison.Ordinal) && (key.Length == 7 || key.Length == 9))
            {
                try { accent = ColorTranslator.FromHtml(key); }
                catch { accent = Color.FromArgb(76, 141, 255); }
            }
            else
            {
                switch (key.ToLowerInvariant())
                {
                    case "violet": accent = Color.FromArgb(158, 105, 255); break;
                    case "cyan": accent = Color.FromArgb(52, 220, 245); break;
                    case "green": accent = Color.FromArgb(67, 220, 155); break;
                    case "pink": accent = Color.FromArgb(255, 91, 177); break;
                    case "orange": accent = Color.FromArgb(255, 157, 64); break;
                    case "red": accent = Color.FromArgb(255, 75, 92); break;
                    case "white": accent = Color.FromArgb(225, 233, 248); break;
                    default: accent = Color.FromArgb(76, 141, 255); break;
                }
            }
            Theme.Blue = accent;
            Theme.Violet = ControlPaint.Light(accent, .18f);
            Theme.PanelFill = Color.FromArgb(11, Math.Max(12, accent.G / 9), Math.Max(20, accent.B / 4));
            Theme.AccentSoft = Color.FromArgb(30, accent.R / 3, accent.G / 3, accent.B / 3);
            Log.Debug("theme", "accent '" + key + "' -> #" + accent.R.ToString("X2") + accent.G.ToString("X2") + accent.B.ToString("X2"));
            foreach (Form form in Application.OpenForms)
            {
                // Accent colour is also applied to the real Windows title-bar border.
                Theme.ApplyDarkTitleBar(form);
                if (form is MainForm main) { main.InvalidateBackdrop(); main.RefreshSearchTheme(); }
                else if (form is VoiceFxForm voiceFx) voiceFx.RefreshThemeAccent();
                else form.Invalidate(true);
            }
        }

        internal static void ApplyComicStyle(bool enabled)
        {
            Log.Info("theme", "comic book mode " + (enabled ? "enabled" : "disabled"));
            Theme.ComicStyleMode = enabled;
            foreach (Form form in Application.OpenForms)
            {
                Theme.ApplyDarkTitleBar(form);
                Theme.ApplyComicTypography(form);
                form.Invalidate(true);
                if (form is MainForm main) main.RefreshComicTheme();
            }
        }

        private void RefreshComicTheme()
        {
            // Recreate only font objects; the persistent drawing helpers remain alive.
            foreach (var f in new[] { fTitleL, fTitleS, fCount, fAdd, fCardName, fCardSub, fEdit, fStatus, fTime, fToast, fEmptyBig, fEmptySmall,
                                      fCoolLabel, fCredit, fLink, fVoiceFx, fStop })
                if (f != null) f.Dispose();
            CreateFonts();
            InvalidateBackdrop();
            RefreshSearchTheme();
            Invalidate(true);
        }

        internal void ApplyWindowResolution(string resolution)
        {
            string selected = String.IsNullOrWhiteSpace(resolution) ? "Unlocked" : resolution.Trim();
            Log.Debug("window", "resolution set to " + selected);
            store.Data.Settings.WindowResolution = selected;
            bool locked = !String.Equals(selected, "Unlocked", StringComparison.OrdinalIgnoreCase) &&
                          !String.Equals(selected, "Unlocked (resizable)", StringComparison.OrdinalIgnoreCase);
            if (!locked)
            {
                FormBorderStyle = FormBorderStyle.Sizable;
                MaximizeBox = true;
                MaximumSize = Size.Empty;
                MinimumSize = SizeFromClientSize(new Size((int)(640 * scale), (int)(560 * scale)));
                ClientSize = new Size((int)(1040 * scale), (int)(740 * scale));
                return;
            }

            int logicalWidth = 1280, logicalHeight = 720;
            if (selected.StartsWith("1024", StringComparison.Ordinal)) { logicalWidth = 1024; logicalHeight = 576; }
            else if (selected.StartsWith("960", StringComparison.Ordinal)) { logicalWidth = 960; logicalHeight = 540; }
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MaximumSize = Size.Empty;
            MinimumSize = SizeFromClientSize(new Size((int)(640 * scale), (int)(560 * scale)));
            ClientSize = new Size((int)(logicalWidth * scale), (int)(logicalHeight * scale));
            MinimumSize = Size;
            MaximumSize = Size;
        }

        private void ApplyLogLevel()
        {
            var logger = Log.Current;
            if (logger != null)
                logger.MinimumLevel = (Program.VerboseRequested || store.Data.Settings.VerboseLogging)
                    ? LogLevel.Debug
                    : LogLevel.Info;
        }

        private void RefreshSearchTheme()
        {
            if (searchTags != null)
            {
                searchTags.BackColor = Color.Transparent;
                foreach (Control control in searchTags.Controls)
                {
                    if (control is SearchSortChip chip)
                    {
                        chip.Selected = String.Equals(Convert.ToString(chip.Tag), sortMode, StringComparison.OrdinalIgnoreCase);
                        chip.Accent = Theme.Blue;
                        chip.Invalidate();
                    }
                }
            }
            if (searchBoxFrame is AccentSearchFrame searchFrame) { searchFrame.Accent = Theme.Blue; searchFrame.Invalidate(); }
            if (searchBox != null)
            {
                searchBox.BackColor = Theme.ComicStyleMode ? Color.White : Theme.Mix(Color.FromArgb(12, 17, 30), Theme.Blue, 0.15f);
                searchBox.ForeColor = Theme.ComicStyleMode ? Color.Black : Theme.CoolWhite;
            }
            if (searchTitle != null) searchTitle.ForeColor = Theme.Blue;
            if (sidebarArrow != null)
            {
                sidebarArrow.FlatAppearance.BorderColor = Theme.Blue;
                sidebarArrow.BackColor = Theme.Mix(Color.FromArgb(10, 14, 27), Theme.Blue, 0.12f);
            }
            field.NotifyAccentChanged();
            Invalidate();
        }

        /// <summary>
        /// Runs a short smoke test of the UI paths that can be exercised without playing a user's
        /// sounds. Started only with <c>--selftest</c>; the window is closed when it finishes.
        /// </summary>
        internal void RunSelfTest()
        {
            Log.Info("selftest", "begin");
            var settings = store.Data.Settings;
            string originalAccent = settings.AccentColor;
            string originalResolution = settings.WindowResolution;
            bool originalComic = settings.ComicStyleMode;
            int passed = 0, failed = 0;

            void Step(string name, Action action)
            {
                try
                {
                    action();
                    passed++;
                    Log.Info("selftest", "PASS " + name);
                }
                catch (Exception ex)
                {
                    failed++;
                    Log.Error("selftest", "FAIL " + name, ex);
                }
            }

            try
            {
                Step("enumerate playback devices", () =>
                {
                    var names = AudioOutput.Names(true);
                    Log.Info("selftest", names.Count + " playback device(s): " + string.Join(" | ", names.ToArray()));
                });
                Step("probe + play silent test clip", () =>
                {
                    string path = Path.Combine(Path.GetTempPath(), "emilios-selftest.wav");
                    try
                    {
                        using (var writer = new NAudio.Wave.WaveFileWriter(path, new NAudio.Wave.WaveFormat(44100, 16, 1)))
                        {
                            byte[] silence = new byte[44100 * 2];
                            writer.Write(silence, 0, silence.Length);
                        }

                        TimeSpan duration;
                        string error;
                        if (!AudioPlayer.TryProbe(path, out duration, out error))
                            throw new InvalidOperationException("probe failed: " + error);
                        Log.Info("selftest", "probe duration " + duration.TotalMilliseconds.ToString("0") + " ms");

                        using (var testPlayer = new AudioPlayer { Volume = 0f })
                        {
                            testPlayer.Play("selftest", path);
                            System.Threading.Thread.Sleep(200);
                            testPlayer.Stop();
                        }
                    }
                    finally
                    {
                        try { File.Delete(path); } catch { }
                    }
                });
                Step("custom accent", () => ApplyAccent("#33CCFF"));
                Step("comic mode on + paint", () => { ApplyComicStyle(true); Invalidate(); Update(); });
                Step("comic mode off + paint", () => { ApplyComicStyle(false); Invalidate(); Update(); });
                Step("fixed resolution", () => { ApplyWindowResolution("1024 x 576"); Update(); });
                Step("unlocked resolution", () => { ApplyWindowResolution("Unlocked"); Update(); });
                Step("construct + render Settings", () =>
                {
                    Log.Info("selftest", "constructing SettingsDialog");
                    using (var dialog = new SettingsDialog(store))
                    {
                        _ = dialog.Handle; // creates the native window without showing it
                        using (var bmp = new Bitmap(Math.Max(1, dialog.Width), Math.Max(1, dialog.Height)))
                            dialog.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    }
                    Log.Info("selftest", "SettingsDialog disposed");
                });
                Step("construct + render Voice FX", () =>
                {
                    Log.Info("selftest", "constructing VoiceFxForm");
                    using (var dialog = new VoiceFxForm(store))
                    {
                        _ = dialog.Handle;
                        using (var bmp = new Bitmap(Math.Max(1, dialog.Width), Math.Max(1, dialog.Height)))
                            dialog.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    }
                    Log.Info("selftest", "VoiceFxForm disposed");
                });
                Step("final paint", () => { Invalidate(); Update(); });
            }
            finally
            {
                ApplyAccent(originalAccent);
                ApplyComicStyle(originalComic);
                ApplyWindowResolution(originalResolution);
            }

            Log.Info("selftest", "finished: " + passed + " passed, " + failed + " failed");
            Environment.ExitCode = failed == 0 ? 0 : 1;
            BeginInvoke(new Action(Close));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkTitleBar(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (store.LoadWarning != null)
            {
                MessageDialog.Show(this, "Could not read your saved list", store.LoadWarning);
            }
            else
            {
                int missing = store.Data.Sounds.Count(s => s.AudioMissing);
                if (missing > 0)
                    ShowToast(missing + (missing == 1 ? " sound file is" : " sound files are") +
                              " missing from the app folder. Delete and re-add them.", Color.FromArgb(255, 190, 100));
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Everything is painted in OnPaint; skipping this avoids flicker.
        }

        // ------------------------------------------------------------------ shutdown

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            try { player.Stop(); } catch { }
            try { localMonitorPlayer.Stop(); } catch { }
            if (voiceFxWindow != null && !voiceFxWindow.IsDisposed)
            { try { voiceFxWindow.Close(); } catch { } voiceFxWindow = null; }
            discordPresence.Dispose();
            store.Save();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                player.Ended -= OnPlayerEnded;
                localMonitorPlayer.Ended -= OnLocalMonitorEnded;
                player.Dispose();
                localMonitorPlayer.Dispose();
                coolMusic.Ended -= OnCoolMusicEnded; coolMusic.Stop(); coolMusic.Dispose();
                field.Dispose();
                foreach (var c in cards) c.Dispose();
                cards.Clear();
                DisposeFonts();
                InvalidateBackdrop();
                if (appIcon != null) appIcon.Dispose();
                if (galaxyBackground != null) galaxyBackground.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
