using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>Dark animated starfield shared by tool windows and settings surfaces.</summary>
    internal sealed class SpaceBackdropPanel : Panel
    {
        // Settings used to repaint the whole starfield on a 65 ms timer. A static background is
        // visually consistent and avoids fighting with foreground controls during text rendering.
        private const float BackgroundPhase = 1.25f;

        public SpaceBackdropPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Full-bleed space backdrop; intentionally static to reduce UI repaint load.
            SpaceBackdropPainter.Draw(e.Graphics, ClientRectangle, BackgroundPhase);
        }
    }

    internal static class SpaceBackdropPainter
    {
        private sealed class Star
        {
            public float X, Y, Size, Phase, Speed;
            public int Brightness, Kind;
        }

        private static readonly Star[] Stars = BuildStars();

        private static Star[] BuildStars()
        {
            var random = new Random(271027);
            var result = new Star[145];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new Star
                {
                    X = (float)random.NextDouble(),
                    Y = (float)random.NextDouble(),
                    Size = i < 12 ? 1.6f + (float)random.NextDouble() * 1.5f : 0.65f + (float)random.NextDouble() * 1.0f,
                    Phase = (float)random.NextDouble() * 6.28318f,
                    Speed = 0.35f + (float)random.NextDouble() * 1.8f,
                    Brightness = 65 + random.Next(130),
                    Kind = random.Next(100) < 76 ? 0 : (random.Next(2) == 0 ? 1 : 2)
                };
            }
            return result;
        }

        public static void Draw(Graphics graphics, Rectangle bounds, float phase)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            var g = graphics;
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SetClip(bounds);

            if (Theme.ComicStyleMode)
            {
                DrawMonochromeHalftone(g, bounds, phase);
                g.Restore(state);
                return;
            }

            using (var background = new LinearGradientBrush(bounds, Color.FromArgb(3, 4, 13), Color.FromArgb(8, 4, 23), 90f))
                g.FillRectangle(background, bounds);

            DrawNebula(g, bounds.Width * 0.23f + (float)Math.Sin(phase * 0.14f) * 16f,
                bounds.Height * 0.23f, Math.Max(130f, bounds.Width * 0.27f), Math.Max(100f, bounds.Height * 0.28f), Color.FromArgb(34, 92, 35, 174));
            DrawNebula(g, bounds.Width * 0.77f + (float)Math.Sin(phase * 0.11f + 1.5f) * 22f,
                bounds.Height * 0.62f, Math.Max(160f, bounds.Width * 0.32f), Math.Max(110f, bounds.Height * 0.3f), Color.FromArgb(24, 25, 69, 156));
            DrawNebula(g, bounds.Width * 0.52f, bounds.Height * 0.08f,
                Math.Max(110f, bounds.Width * 0.20f), Math.Max(80f, bounds.Height * 0.18f), Color.FromArgb(15, 151, 43, 166));

            for (int i = 0; i < Stars.Length; i++)
            {
                Star star = Stars[i];
                float twinkle = 0.65f + 0.35f * (float)Math.Sin(phase * star.Speed + star.Phase);
                int alpha = Math.Max(18, Math.Min(220, (int)(star.Brightness * twinkle)));
                Color color = star.Kind == 1 ? Color.FromArgb(alpha, 127, 177, 255)
                    : star.Kind == 2 ? Color.FromArgb(alpha, 198, 148, 255)
                    : Color.FromArgb(alpha, 231, 240, 255);
                float x = bounds.Left + star.X * bounds.Width + (float)Math.Sin(phase * 0.25f + star.Phase) * star.Size * 2f;
                float y = bounds.Top + star.Y * bounds.Height + (float)Math.Cos(phase * 0.2f + star.Phase) * star.Size;
                using (var brush = new SolidBrush(color))
                    g.FillEllipse(brush, x, y, star.Size, star.Size);
                if (i < 10)
                {
                    using (var glow = new SolidBrush(Color.FromArgb(Math.Max(4, alpha / 8), color.R, color.G, color.B)))
                        g.FillEllipse(glow, x - 3.5f, y - 3.5f, star.Size + 7f, star.Size + 7f);
                }
            }

            if (Theme.ComicStyleMode)
            {
                // Pop-art halftone field layered over the same animated space background.
                using (var pinkDots = new SolidBrush(Color.FromArgb(27, 255, 72, 174)))
                using (var cyanDots = new SolidBrush(Color.FromArgb(27, 45, 220, 255)))
                {
                    for (int yy = bounds.Top + 10; yy < bounds.Bottom; yy += 20)
                    {
                        for (int xx = bounds.Left + 10; xx < bounds.Right; xx += 20)
                        {
                            float wave = (float)Math.Sin(xx * 0.021f + yy * 0.017f + phase * 0.8f);
                            var halftone = ((xx / 20 + yy / 20) % 2 == 0) ? pinkDots : cyanDots;
                            g.FillEllipse(halftone, xx, yy, 2.1f + (wave + 1f) * 0.5f, 2.1f + (wave + 1f) * 0.5f);
                        }
                    }
                }
                using (var streak = new Pen(Color.FromArgb(38, 0, 0, 0), 2f))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        float x = bounds.Width * 0.58f + i * bounds.Width * 0.045f;
                        g.DrawLine(streak, x, bounds.Top, x - bounds.Width * 0.12f, bounds.Height * 0.2f);
                    }
                }
            }

            // Subtle orbital arcs add depth without covering controls or text.
            using (var arcPen = new Pen(Color.FromArgb(18, 132, 154, 255), 1f))
            {
                RectangleF orbit = new RectangleF(bounds.Width * 0.67f, bounds.Height * 0.1f,
                    Math.Max(90f, bounds.Width * 0.24f), Math.Max(45f, bounds.Height * 0.11f));
                g.DrawArc(arcPen, orbit, 8f + phase * 2f % 24f, 225f);
            }
            g.Restore(state);
        }

        private static void DrawMonochromeHalftone(Graphics g, Rectangle bounds, float phase)
        {
            using (var paper = new LinearGradientBrush(bounds, Color.White, Color.FromArgb(220, 220, 220), 90f))
                g.FillRectangle(paper, bounds);

            // Black-and-white manga halftone: a soft open center, dense ink dots near the edges.
            float cx = bounds.Left + bounds.Width * 0.50f;
            float cy = bounds.Top + bounds.Height * 0.40f;
            float rx = Math.Max(1f, bounds.Width * 0.58f);
            float ry = Math.Max(1f, bounds.Height * 0.68f);
            const int spacing = 13;
            using (var ink = new SolidBrush(Color.Black))
            using (var grey = new SolidBrush(Color.FromArgb(85, 85, 85)))
            {
                for (int y = bounds.Top + 4; y < bounds.Bottom; y += spacing)
                {
                    for (int x = bounds.Left + 4; x < bounds.Right; x += spacing)
                    {
                        float nx = (x - cx) / rx;
                        float ny = (y - cy) / ry;
                        float distance = (float)Math.Sqrt(nx * nx + ny * ny);
                        float wave = 0.5f + 0.5f * (float)Math.Sin(phase * 0.5f + x * 0.006f + y * 0.008f);
                        float diameter = Math.Max(0f, Math.Min(5.4f, (distance - 0.12f) * 6.0f + wave * 0.45f));
                        if (diameter < 0.75f) continue;
                        g.FillEllipse(distance > 0.75f ? ink : grey, x, y, diameter, diameter);
                    }
                }
            }
            using (var black = new Pen(Color.Black, 1.4f))
            {
                for (int i = 0; i < 7; i++)
                {
                    float x = bounds.Left + bounds.Width * (0.1f + i * 0.13f);
                    g.DrawLine(black, x, bounds.Top, x + 10f + (float)Math.Sin(phase + i) * 3f, bounds.Top + bounds.Height * 0.07f);
                }
            }
        }

        private static void DrawNebula(Graphics g, float cx, float cy, float width, float height, Color center)
        {
            var rect = new RectangleF(cx - width / 2f, cy - height / 2f, width, height);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(rect);
                using (var glow = new PathGradientBrush(path))
                {
                    glow.CenterColor = center;
                    glow.SurroundColors = new[] { Color.FromArgb(0, center.R, center.G, center.B) };
                    g.FillPath(glow, path);
                }
            }
        }
    }

    /// <summary>Rounded search-field shell with a subtle animated-theme-compatible accent edge.</summary>
    internal sealed class AccentSearchFrame : Panel
    {
        public Color Accent { get; set; } = Color.FromArgb(76, 141, 255);
        public AccentSearchFrame()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (Width < 2 || Height < 2) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
            using (var path = Theme.RoundedRect(bounds, Theme.ComicStyleMode ? 5f : Math.Min(11f, bounds.Height / 2f)))
            using (var fill = new SolidBrush(Theme.ComicStyleMode ? Color.White : Color.FromArgb(247, 10, 14, 27)))
            using (var edge = new Pen(Theme.ComicStyleMode ? Color.Black : Theme.WithAlpha(Accent, 210), Theme.ComicStyleMode ? 2.6f : 1.2f))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(edge, path);
                if (Theme.ComicStyleMode)
                {
                    using (var inner = new Pen(Color.Black, 1f))
                        e.Graphics.DrawPath(inner, Theme.RoundedRect(Theme.Inflate(bounds, -3f), 3f));
                }
            }
        }
    }
}

namespace EmiliosSoundboard
{
    /// <summary>Animated rounded chip used to switch sound-library sorting modes.</summary>
    internal sealed class SearchSortChip : Control
    {
        private readonly Timer animationTimer = new Timer { Interval = 16 };
        private float hoverAmount;
        private bool hovering;
        private bool pressed;

        public Color Accent { get; set; } = Color.FromArgb(76, 141, 255);
        public bool Selected { get; set; }

        public SearchSortChip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable |
                     ControlStyles.StandardClick | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold);
            Size = new Size(82, 28);
            animationTimer.Tick += (s, e) =>
            {
                float target = hovering ? 1f : 0f;
                hoverAmount += (target - hoverAmount) * 0.28f;
                if (Math.Abs(target - hoverAmount) < 0.015f)
                {
                    hoverAmount = target;
                    animationTimer.Stop();
                }
                Invalidate();
            };
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; animationTimer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; pressed = false; animationTimer.Start(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            var r = new RectangleF(2f, 2f, Math.Max(1f, Width - 4f), Math.Max(1f, Height - 4f));
            Color baseColor = Theme.ComicStyleMode
                ? (Selected ? Color.Black : Color.White)
                : (Selected ? Accent : Color.FromArgb(17, 23, 40));
            Color mix = Theme.Mix(baseColor, Color.White, hoverAmount * (Selected ? 0.12f : 0.06f));
            if (pressed) mix = Theme.Mix(mix, Color.Black, 0.18f);
            using (var path = Theme.RoundedRect(r, Theme.ComicStyleMode ? 4f : r.Height / 2f))
            using (var fill = new LinearGradientBrush(r, mix, Theme.ComicStyleMode ? Theme.Mix(mix, Selected ? Color.FromArgb(65, 65, 65) : Color.FromArgb(235, 235, 235), hoverAmount * 0.16f) : Theme.Mix(mix, Selected ? Color.White : Accent, hoverAmount * 0.16f), 0f))
            using (var border = new Pen(Theme.ComicStyleMode ? Color.Black : Theme.WithAlpha(Selected ? Color.White : Accent, (int)(Selected ? 190 : 130 + 85 * hoverAmount)), Theme.ComicStyleMode ? 2.2f : (Selected ? 1.1f : 0.8f)))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            using (var text = new SolidBrush(Theme.ComicStyleMode ? (Selected ? Color.White : Color.Black) : Color.FromArgb(245, 246, 255)))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
                g.DrawString(Text, Font, text, r, format);
            if (Focused)
            {
                using (var focus = new Pen(Theme.ComicStyleMode ? Color.Black : Theme.WithAlpha(Accent, 190), Theme.ComicStyleMode ? 2f : 1f))
                using (var path = Theme.RoundedRect(Theme.Inflate(r, -2f), Math.Max(2f, r.Height / 2f - 2f)))
                    g.DrawPath(focus, path);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) animationTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
