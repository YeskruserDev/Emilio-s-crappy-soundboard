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

        private readonly DataStore store = new DataStore();
        private readonly AudioPlayer player = new AudioPlayer();
        private readonly AudioPlayer coolMusic = new AudioPlayer();
        private readonly AudioPlayer startupAudio = new AudioPlayer();
        private bool previousCoolMode;
        private string loadedBackgroundPath = "";
        private Button sidebarArrow;
        private Bitmap marioKartSprite;
        private bool sidebarOpen = true;
        private readonly SpaceField field = new SpaceField();
        private readonly List<Card> cards = new List<Card>();
        private readonly Timer timer = new Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly float scale;
        private Icon appIcon;
        private TextBox searchBox;
        private FlowLayoutPanel searchTags;
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
        private float ContentWidth { get { return Math.Max(220f, LW - ContentLeft - 18f); } }

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
            FormClosed += (s, e) => { try { startupAudio.Dispose(); } catch { } try { if (marioKartSprite != null) marioKartSprite.Dispose(); } catch { } };
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
                    var splash = new StartupVideoSplash(videoPath, () =>
                    {
                        try { Opacity = 1; Activate(); } catch { }
                    });
                    splash.Show();
                    if (File.Exists(soundPath)) startupAudio.Play("startup-sound", soundPath);
                }
                else if (!String.IsNullOrWhiteSpace(photoPath) && File.Exists(photoPath))
                {
                    var splash = new Form { Text = "Emilio's Soundboard", FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterScreen,
                        WindowState = FormWindowState.Maximized, BackColor = Color.Black, ShowInTaskbar = false, TopMost = true, ControlBox = false };
                    Image img = Image.FromFile(photoPath);
                    var view = new PictureBox { Dock = DockStyle.Fill, Image = img, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black, TabStop = false };
                    splash.Controls.Add(view);
                    var closeTimer = new Timer { Interval = 4500 };
                    closeTimer.Tick += (a, b) =>
                    {
                        closeTimer.Stop(); closeTimer.Dispose();
                        try { splash.Close(); splash.Dispose(); img.Dispose(); Opacity = 1; Activate(); } catch { }
                    };
                    splash.Shown += (a, b) => closeTimer.Start();
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
            if (searchBox != null) { searchBox.BackColor = Color.FromArgb(10, 12, 23); searchBox.ForeColor = Theme.CoolWhite; }
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

        // ------------------------------------------------------------------ cards

        private void AddCardFor(SoundItem item, int delayIndex)
        {
            var card = new Card(item);
            card.Appear = -0.07f * delayIndex;
            cards.Add(card);
            forceFrame = true;
        }

        private int LiveCount()
        {
            int n = 0;
            for (int i = 0; i < cards.Count; i++)
                if (!cards[i].Removing) n++;
            return n;
        }

        private void CreateSearchUi()
        {
            var searchTitle = new Label { Text = "FIND YOUR SOUND", ForeColor = Theme.CoolWhite, BackColor = Color.Transparent, Font = new Font("Comic Sans MS", 9f, FontStyle.Bold), AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(searchTitle);
            searchBox = new TextBox();
            searchBox.Font = new Font("Comic Sans MS", 10f, FontStyle.Regular);
            searchBox.BackColor = Color.FromArgb(10, 12, 23);
            searchBox.ForeColor = Theme.CoolWhite;
            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.PlaceholderText = "Search sounds...";
            searchBox.TextChanged += (s, e) => { scrollTarget = 0; LayoutCards(); forceFrame = true; Invalidate(); };
            Controls.Add(searchBox);

            searchTags = new FlowLayoutPanel();
            searchTags.FlowDirection = FlowDirection.LeftToRight;
            searchTags.WrapContents = true;
            searchTags.BackColor = Color.FromArgb(8, 10, 20);
            searchTags.Padding = new Padding((int)(3 * scale));
            searchTags.Margin = Padding.Empty;
            searchTags.AutoScroll = false;
            string[] tags = { "ALL", "NAME A-Z", "LENGTH", "TYPE" };
            foreach (string tag in tags)
            {
                var b = new Button(); b.Text = tag; b.Tag = tag; b.AutoSize = false; b.Size = new Size((int)(86 * scale), (int)(29 * scale));
                b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderColor = Theme.Blue; b.FlatAppearance.BorderSize = 1;
                b.BackColor = Color.FromArgb(17, 20, 37); b.ForeColor = Theme.CoolWhite;
                b.Font = new Font("Comic Sans MS", 8f, FontStyle.Bold); b.Margin = new Padding((int)(2 * scale));
                b.Click += (s, e) => { sortMode = Convert.ToString(((Button)s).Tag); LayoutCards(); Invalidate(); };
                searchTags.Controls.Add(b);
            }
            Controls.Add(searchTags);
            Resize += (s, e) => PositionSearchUi();
            PositionSearchUi();
        }

        private void PositionSearchUi()
        {
            if (searchBox == null || searchTags == null || IsDisposed) return;
            // Keep the search tools in a stable left-middle position. The edge toggle has its own
            // gutter, so it never covers the ALL/NAME/LENGTH/TYPE buttons on shorter windows.
            int width = (int)(LW >= 850f ? 190f * scale : 176f * scale);
            int x = (int)(36f * scale);
            int logicalY = (int)Math.Max(132f, Math.Min(LH * 0.34f, LH - 220f));
            int y = (int)(logicalY * scale);
            bool showSidebar = LW >= 850f && sidebarOpen;
            searchBox.Visible = showSidebar; searchTags.Visible = showSidebar;
            foreach (Control c in Controls)
                if (c is Label && c.Text == "FIND YOUR SOUND")
                { c.Visible = showSidebar; c.SetBounds(x, y - (int)(24f * scale), width, (int)(22f * scale)); c.BringToFront(); }
            searchBox.SetBounds(x, y, width, (int)(34f * scale));
            searchTags.SetBounds(x, y + (int)(40f * scale), width, (int)(76f * scale));
            if (sidebarArrow != null)
            {
                sidebarArrow.SetBounds(0, y + (int)(40f * scale), (int)(28f * scale), (int)(44f * scale));
                sidebarArrow.BringToFront();
            }
            searchBox.BringToFront(); searchTags.BringToFront();
            if (sidebarArrow != null) sidebarArrow.BringToFront();
        }

        private bool MatchesSearch(SoundItem item)
        {
            if (item == null) return false;
            string q = searchBox == null ? "" : (searchBox.Text ?? "").Trim();
            if (!String.IsNullOrEmpty(q) && (item.Name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                Path.GetExtension(item.AudioFile ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) return false;
            return true;
        }

        private int VisibleCount()
        {
            return cards.Count(c => !c.Removing && MatchesSearch(c.Item));
        }

        private long AudioFileBytes(SoundItem item)
        {
            // The LENGTH tag sorts by actual playback duration when NAudio can read the format.
            try
            {
                string path = Path.Combine(store.SoundsDir, item.AudioFile ?? "");
                using (var reader = new NAudio.Wave.AudioFileReader(path)) return reader.TotalTime.Ticks;
            }
            catch { try { return new FileInfo(Path.Combine(store.SoundsDir, item.AudioFile ?? "")).Length; } catch { return 0L; } }
        }

        private IEnumerable<Card> OrderedVisibleCards()
        {
            var q = cards.Where(c => !c.Removing && MatchesSearch(c.Item));
            if (sortMode == "NAME A-Z") q = q.OrderBy(c => c.Item.Name, StringComparer.OrdinalIgnoreCase);
            else if (sortMode == "LENGTH") q = q.OrderBy(c => AudioFileBytes(c.Item));
            else if (sortMode == "TYPE") q = q.OrderBy(c => Path.GetExtension(c.Item.AudioFile ?? ""), StringComparer.OrdinalIgnoreCase);
            return q;
        }

        private void LayoutCards()
        {
            const float margin = 36f, gap = 20f, minW = 250f, maxW = 400f;
            float left = 18f;
            float avail = Math.Max(200f, LW - 36f);
            int cols = Math.Max(1, (int)Math.Floor((avail + gap) / (minW + gap)));
            float cw = Math.Min(maxW, (avail - gap * (cols - 1)) / cols);

            int live = VisibleCount();
            // Avoid tiny cards on ultrawide/full-screen windows when only a few sounds exist.
            // The grid should adapt to the number of cards currently present, not reserve
            // empty columns for sounds that have not been added yet.
            if (live > 0 && live < cols)
            {
                cols = live;
                cw = Math.Min(maxW, (avail - gap * (cols - 1)) / cols);
            }
            int usedCols = Math.Min(cols, Math.Max(1, live));
            float gridW = usedCols * cw + (usedCols - 1) * gap;
            float x0 = (LW - gridW) / 2f;

            int idx = 0;
            foreach (Card c in OrderedVisibleCards())
            {
                c.TargetW = cw;
                int col = idx % cols;
                int row = idx / cols;
                c.TargetX = x0 + col * (cw + gap);
                c.TargetY = 14f + row * (CardH + gap);
                idx++;
            }

            int rows = live == 0 ? 0 : (live + cols - 1) / cols;
            float contentH = live == 0 ? 0f : 14f + rows * (CardH + gap) - gap + 24f;
            float viewH = Math.Max(0f, GridBottom - GridTop);
            maxScroll = Math.Max(0f, contentH - viewH);
            scrollTarget = Math.Max(0f, Math.Min(maxScroll, scrollTarget));
        }

        // ------------------------------------------------------------------ animation loop

        private static float Ease(float cur, float target, float rate, float dt, ref bool busy)
        {
            float v = Theme.Smooth(cur, target, rate, dt);
            if (Math.Abs(target - v) < 0.004f) return target;
            busy = true;
            return v;
        }

        private void OnCoolMusicEnded(object sender, PlaybackEndedEventArgs e)
        {
            if (!IsDisposed && store.Data.Settings.CoolMode)
            {
                string musicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RainbowRoad.mp3");
                if (File.Exists(musicPath)) { try { coolMusic.Play("cool-mode-music", musicPath); } catch { } }
            }
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized || !Visible || ClientSize.Width < 50)
            {
                clock.Restart();
                return;
            }

            float dt = (float)Math.Min(0.05, clock.Elapsed.TotalSeconds);
            clock.Restart();
            time += dt;

            bool busy = Step(dt);
            if (busy || forceFrame)
            {
                forceFrame = false;
                Invalidate();
            }
        }

        private bool Step(float dt)
        {
            bool busy = false;
            var set = store.Data.Settings;
            float k = set.Intensity / 100f * 1.6f;
            player.Volume = set.SoundVolume / 100f;
            coolMusic.Volume = set.MusicVolume / 100f;
            if (set.CoolMode != previousCoolMode)
            {
                previousCoolMode = set.CoolMode;
                if (set.CoolMode)
                {
                    string musicPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RainbowRoad.mp3");
                    if (File.Exists(musicPath)) { try { coolMusic.Play("cool-mode-music", musicPath); } catch { } }
                }
                else coolMusic.Stop();
                forceFrame = true;
            }
            string requestedBg = set.BackgroundImagePath ?? "";
            if (!String.Equals(requestedBg, loadedBackgroundPath, StringComparison.OrdinalIgnoreCase))
            {
                if (galaxyBackground != null) { galaxyBackground.Dispose(); galaxyBackground = null; }
                loadedBackgroundPath = requestedBg;
                try { if (File.Exists(requestedBg)) galaxyBackground = new Bitmap(requestedBg); } catch { galaxyBackground = null; }
                forceFrame = true;
            }
            bool hoverAnim = set.HoverAnimations;

            // --- background and glow
            // Keep the signature space background alive and cursor-reactive even if an older
            // saved settings file had these effects switched off.
            bgLevel = Ease(bgLevel, 1f, 4f, dt, ref busy);
            reactLevel = Ease(reactLevel, 1f, 4f, dt, ref busy);
            glowLevel = Ease(glowLevel, (set.CursorGlow && hasMouse) ? 1f : 0f, 6f, dt, ref busy);

            if (hasMouse)
            {
                if (!glowInit) { glowX = mx; glowY = my; glowInit = true; }
                else
                {
                    glowX = Theme.Smooth(glowX, mx, 14f, dt);
                    glowY = Theme.Smooth(glowY, my, 14f, dt);
                    if (Math.Abs(glowX - mx) > 0.3f || Math.Abs(glowY - my) > 0.3f) busy = true;
                }
            }
            else glowInit = false;

            bool fieldLive = bgLevel > 0.004f || reactLevel > 0.004f;
            if (fieldLive) fieldSettle = 1.5f; else fieldSettle -= dt;
            if (fieldSettle > 0f)
            {
                hazeTime += dt * bgLevel * (0.4f + k);
                field.Update(dt, LW, LH, hasMouse, mx, my, bgLevel, reactLevel, k);
                busy = true;
            }

            // --- layout and scrolling
            LayoutCards();
            float prevScroll = scrollY;
            scrollY = Theme.Smooth(scrollY, scrollTarget, 14f, dt);
            if (Math.Abs(scrollY - scrollTarget) < 0.3f) scrollY = scrollTarget;
            if (Math.Abs(scrollY - prevScroll) > 0.001f) { busy = true; scrollFade = 1.4f; }
            if (scrollFade > 0f) { scrollFade -= dt; busy = true; }

            // --- cards
            int rebuilt = 0;
            for (int i = cards.Count - 1; i >= 0; i--)
            {
                Card c = cards[i];

                if (c.Removing)
                {
                    c.Appear -= dt / 0.25f;
                    busy = true;
                    if (c.Appear <= 0f)
                    {
                        if (hover.Card == c) hover = new Hit();
                        if (pressed.Card == c) { pressed = new Hit(); pressing = false; }
                        c.Dispose();
                        cards.RemoveAt(i);
                        continue;
                    }
                }
                else if (c.Appear < 1f)
                {
                    c.Appear = Math.Min(1f, c.Appear + dt / 0.38f);
                    busy = true;
                }

                if (!c.PosInit)
                {
                    c.X = c.TargetX; c.Y = c.TargetY; c.W = c.TargetW;
                    c.PosInit = true;
                }
                else if (!c.Removing)
                {
                    c.X = Ease(c.X, c.TargetX, 11f, dt, ref busy);
                    c.Y = Ease(c.Y, c.TargetY, 11f, dt, ref busy);
                    c.W = Ease(c.W, c.TargetW, 14f, dt, ref busy);
                }

                bool isHover = hover.Kind == HitKind.Card && hover.Card == c;
                float hoverT = isHover ? 1f : 0f;
                if (hoverAnim) c.Hover = Ease(c.Hover, hoverT, 13f, dt, ref busy);
                else c.Hover = hoverT;

                bool pressOn = pressing && pressed.Kind == HitKind.Card && pressed.Card == c &&
                               (pressed.Part == CardPart.Body || pressed.Part == CardPart.Play);
                c.Press = Ease(c.Press, pressOn ? 1f : 0f, 24f, dt, ref busy);
                c.PlayHover = Ease(c.PlayHover, isHover && hover.Part == CardPart.Play ? 1f : 0f, 16f, dt, ref busy);
                c.DeleteHover = Ease(c.DeleteHover, isHover && hover.Part == CardPart.Delete ? 1f : 0f, 16f, dt, ref busy);
                c.EditHover = Ease(c.EditHover, isHover && hover.Part == CardPart.Edit ? 1f : 0f, 16f, dt, ref busy);

                bool isActive = player.IsPlaying && player.CurrentId == c.Item.Id;
                c.Active = Ease(c.Active, isActive ? 1f : 0f, 10f, dt, ref busy);
                if (c.Active > 0.01f) busy = true;

                if (c.RippleT < 1f)
                {
                    c.RippleT = Math.Min(1f, c.RippleT + dt * 1.7f);
                    busy = true;
                }

                // Artwork: only keep it for cards near the screen, rebuild it once the size has settled.
                float sy = GridTop + c.Y - scrollY;
                bool near = sy + CardH > GridTop - 250f && sy < GridBottom + 250f;
                if (!near)
                {
                    if (sy + CardH < GridTop - 900f || sy > GridBottom + 900f) c.DropCover();
                }
                else if (rebuilt < 4 && Math.Abs(c.W - c.TargetW) < 0.5f)
                {
                    int pw = (int)Math.Round(c.TargetW * scale);
                    int ph = (int)Math.Round(CardH * scale);
                    if (c.Cover == null || c.CoverW != pw || c.CoverH != ph)
                    {
                        c.BuildCover(store.ImagePath(c.Item), pw, ph, scale);
                        rebuilt++;
                        busy = true;
                    }
                }
            }

            // --- header buttons
            addHover = Ease(addHover, hover.Kind == HitKind.Add ? 1f : 0f, 14f, dt, ref busy);
            addPress = Ease(addPress, (pressing && pressed.Kind == HitKind.Add) ? 1f : 0f, 24f, dt, ref busy);
            setHover = Ease(setHover, hover.Kind == HitKind.Settings ? 1f : 0f, 14f, dt, ref busy);
            emptyHover = Ease(emptyHover, hover.Kind == HitKind.Empty ? 1f : 0f, 12f, dt, ref busy);
            if (setHover > 0.01f)
            {
                gearAngle = (gearAngle + dt * 90f * setHover) % 360f;
                busy = true;
            }

            // --- playback display
            if (player.IsPlaying)
            {
                state = PlayState.Playing;
                lastPos = player.Position;
                lastDur = player.Duration;
                float frac = lastDur.TotalSeconds > 0.001 ? (float)(lastPos.TotalSeconds / lastDur.TotalSeconds) : 0f;
                frac = Theme.Clamp01(frac);
                if (frac < progressShown - 0.3f) progressShown = frac;
                else progressShown = Theme.Smooth(progressShown, frac, 30f, dt);
                busy = true;
            }
            else if (state == PlayState.Complete)
            {
                progressShown = Theme.Smooth(progressShown, 1f, 12f, dt);
                stateTimer -= dt;
                busy = true;
                if (stateTimer <= 0f) { state = PlayState.Ready; lastPos = TimeSpan.Zero; lastDur = TimeSpan.Zero; }
            }
            else if (state == PlayState.Error)
            {
                stateTimer -= dt;
                progressShown = Ease(progressShown, 0f, 7f, dt, ref busy);
                busy = true;
                if (stateTimer <= 0f) state = PlayState.Ready;
            }
            else
            {
                progressShown = Ease(progressShown, 0f, 7f, dt, ref busy);
            }

            // --- toast
            if (toastLife > 0f) { toastLife -= dt; busy = true; }

            return busy;
        }

        // ------------------------------------------------------------------ hit testing and input

        private RectangleF AddRect()
        {
            return new RectangleF(22f, LH - 47f, 116f, 30f);
        }

        private PointF SettingsCenter()
        {
            return new PointF(LW - 46f, 44f);
        }

        private RectangleF EmptyRect()
        {
            float w = Math.Min(520f, LW - 48f);
            float h = 190f;
            float areaH = GridBottom - GridTop;
            return new RectangleF((LW - w) / 2f, GridTop + Math.Max(10f, (areaH - h) / 2f - 10f), w, h);
        }

        private static readonly RectangleF EditRect = new RectangleF(12f, 10f, 52f, 24f);

        private static float Dist(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2, dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private RectangleF VoiceFxRect()
        {
            return new RectangleF(LW - 224f, 20f, 150f, 44f);
        }

        private Hit HitTest(float x, float y)
        {
            var h = new Hit();
            if (AddRect().Contains(x, y)) { h.Kind = HitKind.Add; return h; }
            if (VoiceFxRect().Contains(x, y)) { h.Kind = HitKind.VoiceFx; return h; }
            var sc = SettingsCenter();
            if (Dist(x, y, sc.X, sc.Y) <= 24f) { h.Kind = HitKind.Settings; return h; }

            if (y >= GridTop && y <= GridBottom)
            {
                var ordered = OrderedVisibleCards().ToList();
                for (int i = ordered.Count - 1; i >= 0; i--)
                {
                    Card c = ordered[i];
                    if (c.Removing || c.Appear < 0.3f) continue;
                    float sy = GridTop + c.Y - scrollY;
                    if (x >= c.X && x <= c.X + c.W && y >= sy && y <= sy + CardH)
                    {
                        float lx = x - c.X, ly = y - sy;
                        h.Kind = HitKind.Card;
                        h.Card = c;
                        if (Dist(lx, ly, c.W - 20f, 20f) <= 15f) h.Part = CardPart.Delete;
                        else if (EditRect.Contains(lx, ly)) h.Part = CardPart.Edit;
                        else if (Dist(lx, ly, c.W - 44f, CardH - 44f) <= 29f) h.Part = CardPart.Play;
                        else h.Part = CardPart.Body;
                        return h;
                    }
                }

                if (VisibleCount() == 0 && EmptyRect().Contains(x, y)) h.Kind = HitKind.Empty;
            }
            return h;
        }

        private void UpdateHover()
        {
            hover = hasMouse ? HitTest(mx, my) : new Hit();
            Cursor = hover.Kind == HitKind.None ? Cursors.Default : Cursors.Hand;
            forceFrame = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            mx = e.X / scale;
            my = e.Y / scale;
            hasMouse = true;
            UpdateHover();
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hasMouse = false;
            hover = new Hit();
            forceFrame = true;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            mx = e.X / scale;
            my = e.Y / scale;
            hasMouse = true;
            if (e.Button == MouseButtons.Left)
            {
                pressed = HitTest(mx, my);
                pressing = pressed.Kind != HitKind.None;
            }
            forceFrame = true;
            base.OnMouseDown(e);
        }

        private bool IsGitHubLinkPoint(float x, float y)
        {
            // Match the link's painted location in logical coordinates, independent of DPI scaling.
            return x >= 14f && x <= 245f && y >= 27f && y <= 47f;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            mx = e.X / scale;
            my = e.Y / scale;
            Hit up = HitTest(mx, my);
            bool same = pressing && up.Same(pressed);
            pressing = false;
            forceFrame = true;

            if (e.Button == MouseButtons.Left && IsGitHubLinkPoint(mx, my))
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/emilspaskevics1-max") { UseShellExecute = true }); } catch { }
            }
            else if (e.Button == MouseButtons.Right && up.Kind == HitKind.Card)
                ShowCardMenu(up.Card, e.Location);
            else if (e.Button == MouseButtons.Left && same)
                ActivateHit(up);

            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scrollTarget = Math.Max(0f, Math.Min(maxScroll, scrollTarget - e.Delta / 120f * 100f));
            scrollFade = 1.4f;
            forceFrame = true;
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            float page = Math.Max(100f, (GridBottom - GridTop) * 0.9f);
            switch (e.KeyCode)
            {
                case Keys.PageDown: scrollTarget = Math.Min(maxScroll, scrollTarget + page); break;
                case Keys.PageUp: scrollTarget = Math.Max(0f, scrollTarget - page); break;
                case Keys.Home: scrollTarget = 0f; break;
                case Keys.End: scrollTarget = maxScroll; break;
                case Keys.Down: scrollTarget = Math.Min(maxScroll, scrollTarget + 60f); break;
                case Keys.Up: scrollTarget = Math.Max(0f, scrollTarget - 60f); break;
                default:
                    base.OnKeyDown(e);
                    return;
            }
            scrollFade = 1.4f;
            forceFrame = true;
            e.Handled = true;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            forceFrame = true;
            Invalidate();
        }

        private void ActivateHit(Hit hit)
        {
            switch (hit.Kind)
            {
                case HitKind.Add:
                case HitKind.Empty:
                    AddSounds();
                    break;
                case HitKind.Settings:
                    ShowSettings();
                    break;
                case HitKind.VoiceFx:
                    using (var voice = new VoiceFxForm()) voice.ShowDialog(this);
                    forceFrame = true;
                    break;
                case HitKind.Card:
                    if (hit.Part == CardPart.Delete) DeleteCard(hit.Card);
                    else if (hit.Part == CardPart.Edit) ShowCardMenu(hit.Card, PointToClient(Cursor.Position));
                    else
                    {
                        float sy = GridTop + hit.Card.Y - scrollY;
                        hit.Card.RippleT = 0f;
                        hit.Card.RippleX = mx - hit.Card.X;
                        hit.Card.RippleY = my - sy;
                        PlayCard(hit.Card);
                    }
                    break;
            }
        }

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

        private void SaveLibrary()
        {
            if (!store.Save())
                ShowToast("Could not save your library: " + store.LastSaveError, Theme.Danger);
        }

        // ------------------------------------------------------------------ adding sounds

        private static string CleanName(string raw)
        {
            string s = (raw ?? "").Replace('_', ' ').Trim();
            return s.Length == 0 ? "New sound" : s;
        }

        private async void AddSounds()
        {
            if (importing) return;
            importing = true;
            try
            {
                string[] files;
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "Choose one or more sound files";
                    ofd.Filter = "Audio files|*.wav;*.mp3;*.aiff;*.aif;*.m4a;*.aac;*.wma;*.flac;*.mp4|All files (*.*)|*.*";
                    ofd.Multiselect = true;
                    ofd.CheckFileExists = true;
                    if (ofd.ShowDialog(this) != DialogResult.OK) return;
                    files = ofd.FileNames;
                }

                var problems = new List<string>();
                int added = 0;
                string lastName = "";

                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string shortName = Path.GetFileName(file);

                    string probeError = null;
                    bool ok = await Task.Run(() =>
                    {
                        TimeSpan d;
                        string er;
                        bool r = AudioPlayer.TryProbe(file, out d, out er);
                        probeError = er;
                        return r;
                    });

                    if (!ok)
                    {
                        problems.Add(shortName + "\n    " + probeError);
                        continue;
                    }

                    string sub = files.Length > 1
                        ? "File " + (i + 1) + " of " + files.Length + "  -  " + shortName
                        : shortName;
                    string defaultName = store.UniqueName(CleanName(Path.GetFileNameWithoutExtension(file)), null);

                    string name, imageSource;
                    using (var dlg = new SoundDetailsDialog(store, "NAME YOUR SOUND", sub, defaultName, null,
                                                            files.Length > 1, "ADD SOUND", null))
                    {
                        DialogResult r = dlg.ShowDialog(this);
                        if (r == DialogResult.Cancel) break;
                        if (r == DialogResult.Ignore) continue;
                        name = store.UniqueName(dlg.SoundName, null);
                        imageSource = dlg.ChosenImagePath;
                    }

                    ImportResult res;
                    try
                    {
                        res = await Task.Run(() => store.ImportNew(file, name, imageSource));
                    }
                    catch (Exception ex)
                    {
                        problems.Add(shortName + "\n    Could not copy the file: " + ex.Message);
                        continue;
                    }

                    store.Data.Sounds.Add(res.Item);
                    AddCardFor(res.Item, 0);
                    added++;
                    lastName = res.Item.Name;
                    if (res.Warning != null) problems.Add(shortName + "\n    " + res.Warning);
                }

                if (added > 0)
                {
                    SaveLibrary();
                    scrollTarget = float.MaxValue;
                    ShowToast(added == 1 ? "Added \"" + lastName + "\"" : "Added " + added + " sounds", Theme.Success);
                }

                if (problems.Count > 0)
                {
                    string text = string.Join("\n\n", problems.Take(6));
                    if (problems.Count > 6) text += "\n\n...and " + (problems.Count - 6) + " more.";
                    MessageDialog.Show(this, "Some files could not be added", text);
                }
            }
            catch (Exception ex)
            {
                MessageDialog.Show(this, "Could not add the sound", ex.Message);
            }
            finally
            {
                importing = false;
                forceFrame = true;
            }
        }

        // ------------------------------------------------------------------ editing and deleting

        private void ShowCardMenu(Card c, Point clientPoint)
        {
            var menu = DarkMenu.Create();
            menu.Items.Add("Edit name and background...", null, (s, e) => EditCard(c));
            menu.Items.Add("Change background image...", null, (s, e) => ChangeImage(c));

            var revert = new ToolStripMenuItem("Use default background");
            revert.Enabled = !string.IsNullOrEmpty(c.Item.ImageFile);
            revert.Click += (s, e) => RevertImage(c);
            menu.Items.Add(revert);

            menu.Items.Add(new ToolStripSeparator());
            var del = new ToolStripMenuItem("Delete sound...") { Tag = "danger" };
            del.Click += (s, e) => DeleteCard(c);
            menu.Items.Add(del);

            menu.Closed += (s, e) => BeginInvoke(new Action(menu.Dispose));
            menu.Show(this, clientPoint);
        }

        private async void EditCard(Card c)
        {
            if (c.Removing) return;
            var item = c.Item;
            try
            {
                using (var dlg = new SoundDetailsDialog(store, "EDIT SOUND",
                    "Change the name or the background of this button.", item.Name,
                    store.ImagePath(item), false, "SAVE", item))
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;

                    item.Name = store.UniqueName(dlg.SoundName, item);
                    if (dlg.ChosenImagePath != null) await ApplyImage(c, dlg.ChosenImagePath);
                    else if (dlg.RevertImage) DropImage(c);

                    if (player.CurrentId == item.Id) currentName = item.Name;
                    SaveLibrary();
                    ShowToast("Saved \"" + item.Name + "\"", Theme.Success);
                }
            }
            catch (Exception ex)
            {
                MessageDialog.Show(this, "Could not save the changes", ex.Message);
            }
        }

        private async void ChangeImage(Card c)
        {
            if (c.Removing) return;
            string file;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Choose a background image for \"" + c.Item.Name + "\"";
                ofd.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files (*.*)|*.*";
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                file = ofd.FileName;
            }

            if (await ApplyImage(c, file))
            {
                SaveLibrary();
                ShowToast("Background updated", Theme.Success);
            }
        }

        private async Task<bool> ApplyImage(Card c, string source)
        {
            string newName;
            try
            {
                newName = await Task.Run(() => store.ImportImage(source));
            }
            catch (Exception ex)
            {
                MessageDialog.Show(this, "Could not use that image",
                    "Please choose a PNG, JPG or BMP picture.\n\n" + ex.Message);
                return false;
            }

            string old = c.Item.ImageFile;
            c.Item.ImageFile = newName;
            if (!string.IsNullOrEmpty(old)) store.DeleteImageFile(old);
            c.DropCover();
            forceFrame = true;
            return true;
        }

        private void DropImage(Card c)
        {
            string old = c.Item.ImageFile;
            c.Item.ImageFile = "";
            if (!string.IsNullOrEmpty(old)) store.DeleteImageFile(old);
            c.DropCover();
            forceFrame = true;
        }

        private void RevertImage(Card c)
        {
            if (c.Removing || string.IsNullOrEmpty(c.Item.ImageFile)) return;
            DropImage(c);
            SaveLibrary();
            ShowToast("Background reset to default", Theme.Success);
        }

        private void DeleteCard(Card c)
        {
            if (c.Removing) return;

            bool confirmed = MessageDialog.Show(this, "Delete this sound?",
                "\"" + c.Item.Name + "\" will be removed from your soundboard, together with the copy of its audio " +
                "and background that this app stored.\n\nYour original files on your computer are not touched.",
                "DELETE", "KEEP IT", true);
            if (!confirmed) return;

            if (player.CurrentId == c.Item.Id)
            {
                player.Stop();
                state = PlayState.Ready;
                lastPos = TimeSpan.Zero;
                lastDur = TimeSpan.Zero;
            }

            string name = c.Item.Name;
            c.Removing = true;
            if (c.Appear > 1f) c.Appear = 1f;
            bool clean = store.RemoveSound(c.Item);
            SaveLibrary();

            if (clean) ShowToast("Deleted \"" + name + "\"", Theme.Muted);
            else ShowToast("Deleted \"" + name + "\", but a stored file could not be removed.", Color.FromArgb(255, 190, 100));
            forceFrame = true;
        }

        private void ShowSettings()
        {
            using (var dlg = new SettingsDialog(store))
                dlg.ShowDialog(this);
            SaveLibrary();
            forceFrame = true;
        }

        // ------------------------------------------------------------------ shutdown

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
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
