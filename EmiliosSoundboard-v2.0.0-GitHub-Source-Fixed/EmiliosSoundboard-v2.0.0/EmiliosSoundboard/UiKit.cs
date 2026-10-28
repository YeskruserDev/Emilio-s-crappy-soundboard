using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    internal enum GlowStyle
    {
        Primary,
        Secondary,
        Danger
    }

    /// <summary>A custom-painted button with a smooth hover glow. Used in the dialogs.</summary>
    internal sealed class GlowButton : Control
    {
        private readonly Timer timer = new Timer { Interval = 30 };
        private float hover;
        private float pressAmount;
        private bool hovering;
        private bool pressed;
        private GlowStyle style = GlowStyle.Primary;

        public GlowStyle Style
        {
            get { return style; }
            set { style = value; Invalidate(); }
        }

        public GlowButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable |
                     ControlStyles.StandardClick | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.MakeFont(9.5f, FontStyle.Bold);
            Size = new Size(150, 44);
            timer.Tick += (s, e) =>
            {
                float target = hovering ? 1f : 0f;
                float pressTarget = pressed ? 1f : 0f;
                hover += (target - hover) * 0.28f;
                pressAmount += (pressTarget - pressAmount) * 0.36f;
                if (Math.Abs(target - hover) < 0.01f && Math.Abs(pressTarget - pressAmount) < 0.01f)
                {
                    hover = target;
                    pressAmount = pressTarget;
                    timer.Stop();
                }
                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; timer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; pressed = false; timer.Start(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Focus(); timer.Start(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; timer.Start(); Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Space || keyData == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var saved = g.Save();
            float pressScale = 1f + hover * 0.012f - pressAmount * 0.035f;
            g.TranslateTransform(Width / 2f, Height / 2f);
            g.ScaleTransform(pressScale, pressScale);
            g.TranslateTransform(-Width / 2f, -Height / 2f);

            var r = new RectangleF(5, 5, Width - 10, Height - 10);
            if (pressed) r = Theme.Inflate(r, -1.5f);
            float radius = Theme.ComicStyleMode ? Math.Min(7f, r.Height / 4f) : Math.Min(14f, r.Height / 2f);

            Color c1, c2, glow;
            if (Theme.ComicStyleMode)
            {
                switch (style)
                {
                    case GlowStyle.Danger:
                        c1 = Color.FromArgb(225, 225, 225); c2 = Color.FromArgb(175, 175, 175); glow = Color.Black; break;
                    case GlowStyle.Secondary:
                        c1 = Color.White; c2 = Color.FromArgb(218, 218, 218); glow = Color.Black; break;
                    default:
                        c1 = Color.FromArgb(235, 235, 235); c2 = Color.FromArgb(155, 155, 155); glow = Color.Black; break;
                }
                var shadowRect = r; shadowRect.Offset(2f, 3f);
                using (var shadowPath = Theme.RoundedRect(shadowRect, radius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb(230, 0, 0, 0)))
                    g.FillPath(shadowBrush, shadowPath);
            }
            else
            {
                switch (style)
                {
                    case GlowStyle.Danger:
                        c1 = Color.FromArgb(236, 72, 86); c2 = Color.FromArgb(190, 36, 60); glow = Theme.Danger; break;
                    case GlowStyle.Secondary:
                        c1 = Color.FromArgb(26, 34, 70); c2 = Color.FromArgb(20, 26, 56); glow = Theme.Blue; break;
                    default:
                        c1 = Theme.Blue; c2 = Theme.Violet; glow = Theme.Blue; break;
                }
            }

            if (hover > 0.02f)
            {
                for (int i = 3; i >= 1; i--)
                {
                    using (var gp = Theme.RoundedRect(Theme.Inflate(r, i * 1.4f), radius + i))
                    using (var pen = new Pen(Theme.WithAlpha(glow, (int)(hover * 34f / i)), 2f))
                        g.DrawPath(pen, gp);
                }
            }

            using (var path = Theme.RoundedRect(r, radius))
            {
                Color a = Theme.Mix(c1, Color.White, hover * 0.14f);
                Color b = Theme.Mix(c2, Color.White, hover * 0.14f);
                using (var fill = new LinearGradientBrush(r, a, b, 0f))
                    g.FillPath(fill, path);

                if (Theme.ComicStyleMode)
                {
                    using (var comicInk = new Pen(Color.FromArgb(245, 0, 0, 0), 2.5f)) g.DrawPath(comicInk, path);
                    using (var highlight = new Pen(Color.FromArgb(180, 255, 255, 255), 1f))
                        g.DrawLine(highlight, r.X + radius, r.Y + 3f, r.Right - radius, r.Y + 3f);
                }
                else if (style == GlowStyle.Secondary)
                {
                    using (var pen = new Pen(Theme.Mix(Color.FromArgb(70, 84, 140), Theme.Blue, hover), 1.2f))
                        g.DrawPath(pen, path);
                }
                else if (Focused)
                {
                    using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1.2f))
                        g.DrawPath(pen, path);
                }
            }

            using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var textBrush = new SolidBrush(Theme.ComicStyleMode ? Color.Black : Theme.CoolWhite))
                g.DrawString(Text, Font, textBrush, r, fmt);
            g.Restore(saved);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>An animated on/off switch.</summary>
    internal sealed class ToggleSwitch : Control
    {
        private readonly Timer timer = new Timer { Interval = 16 };
        private bool isChecked;
        private float pos;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                timer.Start();
                Invalidate();
                var h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// <summary>Sets the state without animation or events (used while loading).</summary>
        public void SetCheckedSilently(bool value)
        {
            isChecked = value;
            pos = value ? 1f : 0f;
            Invalidate();
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Size = new Size(56, 32);
            timer.Tick += (s, e) =>
            {
                float target = isChecked ? 1f : 0f;
                pos += (target - pos) * 0.3f;
                if (Math.Abs(target - pos) < 0.01f)
                {
                    pos = target;
                    timer.Stop();
                }
                Invalidate();
            };
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Space || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { Checked = !Checked; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float h = Height - 8f;
            var track = new RectangleF(4, 4, Width - 8f, h);
            using (var path = Theme.RoundedRect(track, h / 2f))
            {
                Color off = Theme.ComicStyleMode ? Color.White : Color.FromArgb(34, 42, 78);
                Color on = Theme.ComicStyleMode ? Color.Black : Theme.Blue;
                Color on2 = Theme.ComicStyleMode ? Color.FromArgb(185, 185, 185) : Theme.Violet;
                using (var br = new LinearGradientBrush(track, Theme.Mix(off, on, pos), Theme.Mix(off, on2, pos), 0f))
                    g.FillPath(br, path);
                using (var pen = new Pen(Theme.ComicStyleMode ? Color.Black : Theme.Mix(Color.FromArgb(70, 84, 140), Color.FromArgb(160, 190, 255), pos), Theme.ComicStyleMode ? 2.5f : (Focused ? 2f : 1f)))
                    g.DrawPath(pen, path);
            }

            float knob = h - 6f;
            float x = track.X + 3f + (track.Width - knob - 6f) * pos;
            using (var br = new SolidBrush(Theme.ComicStyleMode ? Color.White : Color.FromArgb(250, Theme.CoolWhite)))
                g.FillEllipse(br, x, track.Y + 3f, knob, knob);
            if (Theme.ComicStyleMode)
                using (var ink = new Pen(Color.Black, 1.8f)) g.DrawEllipse(ink, x, track.Y + 3f, knob, knob);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>A simple horizontal slider (0 - 100) with a glowing handle.</summary>
    internal sealed class GlowSlider : Control
    {
        private int value = 50;
        private bool dragging;
        private bool hovering;

        public event EventHandler ValueChanged;

        public int Minimum { get; set; }
        public int Maximum { get; set; }

        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(Minimum, Math.Min(Maximum, value));
                if (v == this.value) return;
                this.value = v;
                Invalidate();
                var h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public void SetValueSilently(int v)
        {
            value = Math.Max(Minimum, Math.Min(Maximum, v));
            Invalidate();
        }

        public GlowSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Minimum = 0;
            Maximum = 100;
            Size = new Size(240, 32);
        }

        private float TrackLeft { get { return 14f; } }
        private float TrackRight { get { return Width - 14f; } }

        private void SetFromMouse(int x)
        {
            float t = (x - TrackLeft) / Math.Max(1f, TrackRight - TrackLeft);
            Value = Minimum + (int)Math.Round(Theme.Clamp01(t) * (Maximum - Minimum));
        }

        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { dragging = true; Focus(); SetFromMouse(e.X); } base.OnMouseDown(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) SetFromMouse(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) { Value -= 5; e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { Value += 5; e.Handled = true; }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cy = Height / 2f;
            float t = (value - Minimum) / (float)Math.Max(1, Maximum - Minimum);
            float x = TrackLeft + (TrackRight - TrackLeft) * t;

            var track = new RectangleF(TrackLeft, cy - 3f, TrackRight - TrackLeft, 6f);
            using (var path = Theme.RoundedRect(track, 3f))
            using (var br = new SolidBrush(Theme.ComicStyleMode ? Color.White : Color.FromArgb(40, 48, 86)))
            {
                g.FillPath(br, path);
                if (Theme.ComicStyleMode) using (var ink = new Pen(Color.Black, 1.8f)) g.DrawPath(ink, path);
            }

            if (x - TrackLeft > 1f)
            {
                var fill = new RectangleF(TrackLeft, cy - 3f, x - TrackLeft, 6f);
                using (var path = Theme.RoundedRect(fill, 3f))
                using (var br = new LinearGradientBrush(new RectangleF(TrackLeft, 0, Math.Max(2f, TrackRight - TrackLeft), 10), Theme.ComicStyleMode ? Color.Black : Theme.Blue, Theme.ComicStyleMode ? Color.FromArgb(150, 150, 150) : Theme.Violet, 0f))
                    g.FillPath(br, path);
            }

            float glow = (dragging ? 1f : (hovering ? 0.6f : 0.25f));
            for (int i = 3; i >= 1; i--)
            {
                float rad = 8f + i * 3f;
                Color glowColor = Theme.ComicStyleMode ? Color.Black : Theme.Blue;
                using (var br = new SolidBrush(Theme.WithAlpha(glowColor, (int)(glow * (Theme.ComicStyleMode ? 14f : 22f) / i))))
                    g.FillEllipse(br, x - rad, cy - rad, rad * 2f, rad * 2f);
            }
            using (var br = new SolidBrush(Theme.ComicStyleMode ? Color.Black : Theme.CoolWhite))
                g.FillEllipse(br, x - 8f, cy - 8f, 16f, 16f);
            if (Focused)
                using (var pen = new Pen(Theme.ComicStyleMode ? Color.Black : Theme.Blue, Theme.ComicStyleMode ? 2f : 1.5f))
                    g.DrawEllipse(pen, x - 10f, cy - 10f, 20f, 20f);
        }
    }

    /// <summary>A compact animated rainbow label used by the Rainbow Road mode indicator.</summary>
    internal sealed class RainbowTextLabel : Control
    {
        private readonly Timer rainbowTimer = new Timer { Interval = 45 };
        private float phase;
        private bool animated;

        public bool Animated
        {
            get { return animated; }
            set
            {
                animated = value;
                if (animated) rainbowTimer.Start(); else rainbowTimer.Stop();
                Invalidate();
            }
        }

        public RainbowTextLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Theme.Blue;
            Font = Theme.MakeFont(10f, FontStyle.Bold);
            rainbowTimer.Tick += (s, e) => { phase = (phase + 7f) % 360f; Invalidate(); };
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // SupportsTransparentBackColor delegates the background painting to the parent.
            base.OnPaintBackground(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float x = 0f;
            for (int i = 0; i < Text.Length; i++)
            {
                string ch = Text[i].ToString();
                float width = g.MeasureString(ch, Font, 400, StringFormat.GenericTypographic).Width;
                if (ch != " ")
                {
                    if (Theme.ComicStyleMode)
                    {
                        // Comic Book Mode is strict black-and-white, even while Cool Mode is active.
                        using (var ink = new SolidBrush(Color.Black))
                            g.DrawString(ch, Font, ink, x, 0f, StringFormat.GenericTypographic);
                    }
                    else if (animated)
                    {
                        using (var brush = new SolidBrush(Theme.RainbowColor(phase + i * 22f)))
                            g.DrawString(ch, Font, brush, x, 0f, StringFormat.GenericTypographic);
                    }
                    else
                    {
                        using (var brush = new SolidBrush(ForeColor))
                            g.DrawString(ch, Font, brush, x, 0f, StringFormat.GenericTypographic);
                    }
                }
                x += width;
            }
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnFontChanged(EventArgs e) { Invalidate(); base.OnFontChanged(e); }
        protected override void Dispose(bool disposing) { if (disposing) rainbowTimer.Dispose(); base.Dispose(disposing); }
    }

    /// <summary>Base class for the app's dialogs: dark gradient, dark title bar, no designer needed.</summary>
    internal class ThemedForm : Form
    {
        protected readonly float ScaleFactor;

        /// <summary>Window material for this dialog. Set before the handle is created.</summary>
        public BackdropKind BackdropEffect { get; set; } = BackdropKind.Off;

        protected int S(int pixels)
        {
            return (int)Math.Round(pixels * ScaleFactor);
        }

        /// <summary>Re-applies the current material, e.g. after the user changes the setting.</summary>
        protected void RefreshWindowEffect()
        {
            if (!IsHandleCreated) return;
            WindowEffects.Apply(this, BackdropEffect);
            WindowEffects.RefreshFrame(this);
        }

        public ThemedForm()
        {
            ScaleFactor = Theme.DpiScale();
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.DeepNavy;
            ForeColor = Theme.CoolWhite;
            Font = Theme.MakeFont(9.5f);
            DoubleBuffered = true;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkTitleBar(this);
            WindowEffects.Apply(this, BackdropEffect);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Theme.ComicStyleMode) Theme.ApplyComicTypography(this);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Keep the space backdrop full-bleed; no decorative outline around the window.
            SpaceBackdropPainter.Draw(e.Graphics, ClientRectangle, 0.8f);
        }

        protected Label MakeLabel(string text, float points, FontStyle style, Color color)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = color,
                Font = Theme.MakeFont(points, style)
            };
        }

        protected TextBox MakeTextBox()
        {
            return new TextBox
            {
                BackColor = Color.FromArgb(16, 22, 48),
                ForeColor = Theme.CoolWhite,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.MakeFont(11f)
            };
        }
    }

    /// <summary>Dark right-click menus.</summary>
    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        private static readonly Color Back = Color.FromArgb(12, 16, 36);
        public override Color ToolStripDropDownBackground { get { return Back; } }
        public override Color ImageMarginGradientBegin { get { return Back; } }
        public override Color ImageMarginGradientMiddle { get { return Back; } }
        public override Color ImageMarginGradientEnd { get { return Back; } }
        public override Color MenuBorder { get { return Color.FromArgb(56, 70, 130); } }
        public override Color MenuItemBorder { get { return Theme.Blue; } }
        public override Color MenuItemSelected { get { return Color.FromArgb(34, 48, 100); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(34, 48, 100); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(34, 48, 100); } }
        public override Color SeparatorDark { get { return Color.FromArgb(50, 62, 110); } }
        public override Color SeparatorLight { get { return Color.FromArgb(12, 16, 36); } }
    }

    internal sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool danger = (e.Item.Tag as string) == "danger";
            if (!e.Item.Enabled) e.TextColor = Color.FromArgb(88, 98, 138);
            else if (danger) e.TextColor = Color.FromArgb(255, 120, 130);
            else e.TextColor = Theme.CoolWhite;
            base.OnRenderItemText(e);
        }
    }

    internal static class DarkMenu
    {
        public static ContextMenuStrip Create()
        {
            return new ContextMenuStrip
            {
                Renderer = new DarkRenderer(),
                BackColor = Color.FromArgb(12, 16, 36),
                ForeColor = Theme.CoolWhite,
                ShowImageMargin = false,
                Font = Theme.MakeFont(10f)
            };
        }
    }
}
