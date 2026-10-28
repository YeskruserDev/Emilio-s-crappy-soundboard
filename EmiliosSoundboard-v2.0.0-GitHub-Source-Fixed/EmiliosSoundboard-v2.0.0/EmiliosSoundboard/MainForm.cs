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
        private readonly AudioPlayer coolMusic = new AudioPlayer();
        private readonly AudioPlayer startupAudio = new AudioPlayer();
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
        private readonly Timer timer = new Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly float scale;
        private Icon appIcon;
        private TextBox searchBox;
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

            CreateFonts();
            // Default background is the original animated starfield. A custom image is only loaded when the user chooses one.

            store.Load();
            var set = store.Data.Settings;
            // Keep the actual soundboard hidden until a configured startup splash finishes.
            bool hasStartupSplash = (!String.IsNullOrWhiteSpace(set.StartupPhotoPath) && File.Exists(set.StartupPhotoPath)) ||
                                    (!String.IsNullOrWhiteSpace(set.StartupVideoPath) && File.Exists(set.StartupVideoPath));
            if (hasStartupSplash) Opacity = 0;
            ApplyAccent(set.AccentColor);
            bgLevel = set.AnimatedBackground ? 1f : 0f;
            reactLevel = set.StarReaction ? 1f : 0f;
            CreateSearchUi();
            sidebarArrow = new Button { Text = "‹", FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 8, 11, 22), ForeColor = Theme.CoolWhite, Font = new Font("Comic Sans MS", 15f, FontStyle.Bold), Size = new Size((int)(28 * scale), (int)(44 * scale)), TabStop = false };
            sidebarArrow.FlatAppearance.BorderColor = Theme.Blue; sidebarArrow.FlatAppearance.BorderSize = 1;
            sidebarArrow.Click += (s, e) => { sidebarOpen = !sidebarOpen; sidebarArrow.Text = sidebarOpen ? "‹" : "›"; PositionSearchUi(); LayoutCards(); Invalidate(); };
            Controls.Add(sidebarArrow); sidebarArrow.BringToFront(); PositionSearchUi();
            try { marioKartSprite = new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MarioKart.png")); marioKartSprite.RotateFlip(RotateFlipType.RotateNoneFlipX); } catch { marioKartSprite = null; }
            player.Volume = set.SoundVolume / 100f; coolMusic.Volume = set.MusicVolume / 100f; startupAudio.Volume = 0.8f; previousCoolMode = false;
            coolMusic.Ended += OnCoolMusicEnded;

            int index = 0;
            foreach (var item in store.Data.Sounds)
                AddCardFor(item, Math.Min(index++, 10));

            player.Ended += OnPlayerEnded;

            timer.Interval = 30; // ~33 FPS reduces idle CPU usage while keeping motion smooth
            timer.Tick += OnTick;
            timer.Start();
            Shown += (s, e) => StartStartupExperience();
            FormClosed += (s, e) => { try { startupAudio.Dispose(); } catch { } try { if (marioKartSprite != null) marioKartSprite.Dispose(); } catch { } try { if (desktopPetSprite != null) desktopPetSprite.Dispose(); } catch { } };
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
                    if (File.Exists(soundPath)) startupAudio.Play("startup-sound", soundPath);
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
                    if (File.Exists(soundPath)) startupAudio.Play("startup-sound", soundPath);
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
            switch ((name ?? "Blue").Trim().ToLowerInvariant())
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
            Theme.Blue = accent;
            Theme.Violet = ControlPaint.Light(accent, .18f);
            Theme.PanelFill = Color.FromArgb(11, Math.Max(12, accent.G / 9), Math.Max(20, accent.B / 4));
            Theme.AccentSoft = Color.FromArgb(30, accent.R / 3, accent.G / 3, accent.B / 3);
            foreach (Form form in Application.OpenForms)
                if (form is MainForm main) main.RefreshSearchTheme();
        }

        private void RefreshSearchTheme()
        {
            if (searchTags != null)
            {
                searchTags.BackColor = Theme.PanelFill;
                foreach (Control control in searchTags.Controls)
                {
                    if (control is Button b)
                    {
                        b.FlatAppearance.BorderColor = Theme.Blue;
                        b.BackColor = Theme.PanelFill;
                        b.ForeColor = Theme.CoolWhite;
                    }
                }
            }
            if (searchBox != null) { searchBox.BackColor = Color.FromArgb(18, 27, 47); searchBox.ForeColor = Theme.CoolWhite; }
            if (searchTitle != null) searchTitle.ForeColor = Theme.CoolWhite;
            if (searchTags != null)
            {
                foreach (Control control in searchTags.Controls)
                {
                    if (control is Button b)
                    {
                        bool selected = String.Equals(Convert.ToString(b.Tag), sortMode, StringComparison.OrdinalIgnoreCase);
                        b.BackColor = selected ? Color.FromArgb(52, Theme.Blue.R / 2, Theme.Blue.G / 2, Theme.Blue.B / 2) : Color.FromArgb(24, 35, 57);
                        b.FlatAppearance.BorderColor = selected ? Theme.CoolWhite : Theme.WithAlpha(Theme.Blue, 190);
                    }
                }
            }
            if (sidebarArrow != null) sidebarArrow.FlatAppearance.BorderColor = Theme.Blue;
            Invalidate();
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
                player.Dispose();
                coolMusic.Ended -= OnCoolMusicEnded; coolMusic.Stop(); coolMusic.Dispose();
                field.Dispose();
                foreach (var c in cards) c.Dispose();
                cards.Clear();
                DisposeFonts();
                if (appIcon != null) appIcon.Dispose();
                if (galaxyBackground != null) galaxyBackground.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
