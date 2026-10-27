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
        private readonly Timer timer = new Timer { Interval = 16 };
        private float hover;
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
                hover += (target - hover) * 0.28f;
                if (Math.Abs(target - hover) < 0.01f)
                {
                    hover = target;
                    timer.Stop();
                }
                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; timer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; pressed = false; timer.Start(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
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

            var r = new RectangleF(5, 5, Width - 10, Height - 10);
            if (pressed) r = Theme.Inflate(r, -1.5f);
            float radius = Math.Min(14f, r.Height / 2f);

            Color c1, c2, glow;
            switch (style)
            {
                case GlowStyle.Danger:
                    c1 = Color.FromArgb(236, 72, 86); c2 = Color.FromArgb(190, 36, 60); glow = Theme.Danger; break;
                case GlowStyle.Secondary:
                    c1 = Color.FromArgb(26, 34, 70); c2 = Color.FromArgb(20, 26, 56); glow = Theme.Blue; break;
                default:
                    c1 = Theme.Blue; c2 = Theme.Violet; glow = Theme.Blue; break;
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

                if (style == GlowStyle.Secondary)
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
            using (var textBrush = new SolidBrush(Theme.CoolWhite))
                g.DrawString(Text, Font, textBrush, r, fmt);
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
                Color off = Color.FromArgb(34, 42, 78);
                using (var br = new LinearGradientBrush(track, Theme.Mix(off, Theme.Blue, pos), Theme.Mix(off, Theme.Violet, pos), 0f))
                    g.FillPath(br, path);
                using (var pen = new Pen(Theme.Mix(Color.FromArgb(70, 84, 140), Color.FromArgb(160, 190, 255), pos), Focused ? 2f : 1f))
                    g.DrawPath(pen, path);
            }

            float knob = h - 6f;
            float x = track.X + 3f + (track.Width - knob - 6f) * pos;
            using (var br = new SolidBrush(Color.FromArgb(250, Theme.CoolWhite)))
                g.FillEllipse(br, x, track.Y + 3f, knob, knob);
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
            using (var br = new SolidBrush(Color.FromArgb(40, 48, 86)))
                g.FillPath(br, path);

            if (x - TrackLeft > 1f)
            {
                var fill = new RectangleF(TrackLeft, cy - 3f, x - TrackLeft, 6f);
                using (var path = Theme.RoundedRect(fill, 3f))
                using (var br = new LinearGradientBrush(new RectangleF(TrackLeft, 0, Math.Max(2f, TrackRight - TrackLeft), 10), Theme.Blue, Theme.Violet, 0f))
                    g.FillPath(br, path);
            }

            float glow = (dragging ? 1f : (hovering ? 0.6f : 0.25f));
            for (int i = 3; i >= 1; i--)
            {
                float rad = 8f + i * 3f;
                using (var br = new SolidBrush(Theme.WithAlpha(Theme.Blue, (int)(glow * 22f / i))))
                    g.FillEllipse(br, x - rad, cy - rad, rad * 2f, rad * 2f);
            }
            using (var br = new SolidBrush(Theme.CoolWhite))
                g.FillEllipse(br, x - 8f, cy - 8f, 16f, 16f);
            if (Focused)
                using (var pen = new Pen(Theme.Blue, 1.5f))
                    g.DrawEllipse(pen, x - 10f, cy - 10f, 20f, 20f);
        }
    }

    /// <summary>Base class for the app's dialogs: dark gradient, dark title bar, no designer needed.</summary>
    internal class ThemedForm : Form
    {
        protected readonly float Scale;

        protected int S(int pixels)
        {
            return (int)Math.Round(pixels * Scale);
        }

        public ThemedForm()
        {
            Scale = Theme.DpiScale();
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
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var br = new LinearGradientBrush(ClientRectangle, Color.FromArgb(4, 6, 16), Color.FromArgb(10, 14, 38), 90f))
                g.FillRectangle(br, ClientRectangle);

            using (var pen = new Pen(Color.FromArgb(70, Theme.Blue), 2f))
                g.DrawLine(pen, 0, 0, ClientSize.Width, 0);
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
