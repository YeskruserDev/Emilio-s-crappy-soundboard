using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>
    /// The living background: twinkling, drifting stars and dust that gently move away from the cursor,
    /// plus a soft light that follows it. All coordinates are in logical (96-dpi) units.
    /// </summary>
    internal sealed class SpaceField : IDisposable
    {
        private sealed class Star
        {
            public float X, Y;            // position, 0..1 of the window
            public float Depth;           // 0.2 (far) .. 1 (near)
            public float Size;
            public float Base;            // base brightness
            public float Phase, TwinkleSpeed, TwinkleAmount;
            public float Vx, Vy;          // drift in px/sec
            public float Ox, Oy;          // offset caused by the cursor, in px
            public float Boost;           // extra brightness when the cursor is near
            public int Kind;              // 0 white, 1 blue, 2 violet
            public bool Halo;
        }

        private readonly List<Star> stars = new List<Star>();
        private readonly List<Star> dust = new List<Star>();
        private sealed class ShootingStar
        {
            public float X, Y, Vx, Vy, Life, MaxLife, Width;
        }

        private readonly List<ShootingStar> shootingStars = new List<ShootingStar>();
        private float nextShootingStar = 1.8f;
        private readonly Random rng = new Random();
        private readonly SolidBrush brush = new SolidBrush(Color.White);

        private float parX, parY;
        private float width = 1, height = 1;

        private Bitmap glowBitmap;
        private ImageAttributes glowAttr;
        private Color glowAccent = Color.Empty;
        private float glowAttrAlpha = -1f;

        private const float ReactRadius = 150f;

        // ------------------------------------------------------------------ setup

        private Star CreateStar(bool isDust)
        {
            var s = new Star();
            s.X = (float)rng.NextDouble();
            s.Y = (float)rng.NextDouble();
            s.Phase = (float)(rng.NextDouble() * Math.PI * 2);
            s.Kind = rng.Next(100) < 70 ? 0 : (rng.Next(2) == 0 ? 1 : 2);

            if (isDust)
            {
                s.Depth = 0.5f + (float)rng.NextDouble() * 0.5f;
                s.Size = 3f + (float)rng.NextDouble() * 5f;
                s.Base = 0.10f + (float)rng.NextDouble() * 0.12f;
                s.Kind = rng.Next(2) + 1;
                s.TwinkleSpeed = 0.2f + (float)rng.NextDouble() * 0.4f;
                s.TwinkleAmount = 0.4f;
                s.Vx = (-5f + (float)rng.NextDouble() * -6f) * s.Depth;
                s.Vy = (-2f + (float)rng.NextDouble() * 4f) * s.Depth;
            }
            else
            {
                s.Depth = 0.2f + (float)rng.NextDouble() * 0.8f;
                double roll = rng.NextDouble();
                s.Size = roll < 0.80 ? 0.9f + (float)rng.NextDouble() * 0.7f
                       : roll < 0.96 ? 1.7f + (float)rng.NextDouble() * 0.8f
                       : 2.5f + (float)rng.NextDouble() * 1.0f;
                s.Base = 0.25f + (float)rng.NextDouble() * 0.7f * s.Depth + 0.1f;
                s.TwinkleSpeed = 0.6f + (float)rng.NextDouble() * 2.2f;
                s.TwinkleAmount = 0.15f + (float)rng.NextDouble() * 0.55f;
                s.Vx = (-2.2f + (float)rng.NextDouble() * -1.5f) * s.Depth;
                s.Vy = (-0.5f + (float)rng.NextDouble()) * s.Depth;
                s.Halo = s.Size > 2.3f;
            }
            return s;
        }

        private void EnsureCount(float w, float h)
        {
            int target = (int)Math.Max(90, Math.Min(340, w * h / 5200f));
            while (stars.Count < target) stars.Add(CreateStar(false));
            if (stars.Count > target + target / 4) stars.RemoveRange(target, stars.Count - target);
            while (dust.Count < 22) dust.Add(CreateStar(true));
        }

        // ------------------------------------------------------------------ update

        /// <param name="bgLevel">0..1, how "animated" the background currently is (eases when toggled).</param>
        /// <param name="reactLevel">0..1, how strongly stars react to the cursor.</param>
        /// <param name="k">Overall intensity multiplier from the settings slider.</param>
        public void Update(float dt, float w, float h, bool hasMouse, float mx, float my,
                           float bgLevel, float reactLevel, float k)
        {
            width = Math.Max(1f, w);
            height = Math.Max(1f, h);
            EnsureCount(width, height);

            float targetX = hasMouse ? (mx / width - 0.5f) : 0f;
            float targetY = hasMouse ? (my / height - 0.5f) : 0f;
            parX = Theme.Smooth(parX, targetX, 2.5f, dt);
            parY = Theme.Smooth(parY, targetY, 2.5f, dt);

            UpdateList(stars, dt, hasMouse, mx, my, bgLevel, reactLevel, k, 1f);
            UpdateList(dust, dt, hasMouse, mx, my, bgLevel, reactLevel, k, 1.6f);

            // A rare streak crossing the dark sky. It only spawns while the background is animated.
            nextShootingStar -= dt * Math.Max(0.15f, bgLevel);
            if (bgLevel > 0.12f && nextShootingStar <= 0f && shootingStars.Count < 2)
            {
                shootingStars.Add(new ShootingStar {
                    X = (float)(rng.NextDouble() * 0.9 + 0.05) * width,
                    Y = (float)(rng.NextDouble() * 0.42) * height,
                    Vx = -(360f + (float)rng.NextDouble() * 300f),
                    Vy = 150f + (float)rng.NextDouble() * 180f,
                    Life = 0.75f, MaxLife = 0.75f, Width = 70f + (float)rng.NextDouble() * 65f
                });
                nextShootingStar = 2.5f + (float)rng.NextDouble() * 5.5f;
            }
            for (int i = shootingStars.Count - 1; i >= 0; i--)
            {
                var star = shootingStars[i];
                star.X += star.Vx * dt; star.Y += star.Vy * dt; star.Life -= dt;
                if (star.Life <= 0f || star.X < -200f || star.Y > height + 100f) shootingStars.RemoveAt(i);
            }
        }

        private void UpdateList(List<Star> list, float dt, bool hasMouse, float mx, float my,
                                float bg, float react, float k, float reactScale)
        {
            float follow = 1f - (float)Math.Exp(-dt * 8f);
            float r2 = ReactRadius * ReactRadius;

            for (int i = 0; i < list.Count; i++)
            {
                Star s = list[i];

                s.X += s.Vx * dt * bg * k / width;
                s.Y += s.Vy * dt * bg * k / height;
                if (s.X < 0f) s.X += 1f; else if (s.X > 1f) s.X -= 1f;
                if (s.Y < 0f) s.Y += 1f; else if (s.Y > 1f) s.Y -= 1f;

                s.Phase += dt * s.TwinkleSpeed;

                float tx = 0f, ty = 0f, prox = 0f;
                if (react > 0.001f)
                {
                    tx = -parX * 26f * s.Depth * react * k * reactScale;
                    ty = -parY * 18f * s.Depth * react * k * reactScale;

                    if (hasMouse)
                    {
                        float dx = s.X * width - mx;
                        float dy = s.Y * height - my;
                        float d2 = dx * dx + dy * dy;
                        if (d2 < r2 && d2 > 0.01f)
                        {
                            float d = (float)Math.Sqrt(d2);
                            float f = 1f - d / ReactRadius;
                            f *= f;
                            prox = f;
                            float push = f * 46f * s.Depth * react * k * reactScale;
                            tx += dx / d * push;
                            ty += dy / d * push;
                        }
                    }
                }

                s.Ox += (tx - s.Ox) * follow;
                s.Oy += (ty - s.Oy) * follow;
                s.Boost += (prox * react - s.Boost) * follow;
            }
        }

        // ------------------------------------------------------------------ drawing

        public void Draw(Graphics g, float bgLevel)
        {
            DrawList(g, dust, bgLevel, true);
            DrawList(g, stars, bgLevel, false);
            DrawShootingStars(g, bgLevel);
        }

        private void DrawShootingStars(Graphics g, float bgLevel)
        {
            if (bgLevel < 0.05f) return;
            foreach (var s in shootingStars)
            {
                float life = Theme.Clamp01(s.Life / s.MaxLife) * bgLevel;
                if (life <= 0.01f) continue;
                using (var p = new Pen(Color.FromArgb((int)(170 * life), 190, 218, 255), 1.8f))
                {
                    p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                    p.CustomEndCap = new AdjustableArrowCap(2f, 3f, true);
                    p.Width = 1.5f + 1.3f * life;
                    g.DrawLine(p, s.X, s.Y, s.X + s.Width, s.Y - s.Width * 0.38f);
                }
                using (var p = new Pen(Color.FromArgb((int)(34 * life), 105, 150, 255), 7f))
                    g.DrawLine(p, s.X, s.Y, s.X + s.Width * 0.8f, s.Y - s.Width * 0.3f);
            }
        }

        private void DrawList(Graphics g, List<Star> list, float bgLevel, bool isDust)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Star s = list[i];

                float twinkle = 1f - s.TwinkleAmount * bgLevel * (0.5f + 0.5f * (float)Math.Sin(s.Phase));
                float b = Theme.Clamp01(s.Base * twinkle + s.Boost * (isDust ? 0.25f : 0.55f));
                int a = (int)(b * 255f);
                if (a < 5) continue;

                float x = s.X * width + s.Ox;
                float y = s.Y * height + s.Oy;
                if (x < -10f || y < -10f || x > width + 10f || y > height + 10f) continue;

                Color c = StarColor(s.Kind);

                if (isDust)
                {
                    brush.Color = Color.FromArgb(Math.Min(a, 46), c);
                    g.FillEllipse(brush, x - s.Size, y - s.Size, s.Size * 2f, s.Size * 2f);
                    brush.Color = Color.FromArgb(Math.Min(a, 70), c);
                    g.FillEllipse(brush, x - s.Size / 2f, y - s.Size / 2f, s.Size, s.Size);
                    continue;
                }

                if (s.Halo || s.Boost > 0.25f)
                {
                    brush.Color = Color.FromArgb(Math.Max(4, a / 7), c);
                    float hs = s.Size * 4.5f;
                    g.FillEllipse(brush, x - hs / 2f, y - hs / 2f, hs, hs);
                }

                float size = s.Size * (1f + s.Boost * 0.35f);
                brush.Color = Color.FromArgb(a, c);
                g.FillEllipse(brush, x - size / 2f, y - size / 2f, size, size);
            }
        }

        private static Color StarColor(int kind)
        {
            // Cached per accent colour: ControlPaint.Light is far too slow to run per star per frame,
            // but the tint still has to follow an accent change.
            if (starKindAccent != Theme.Blue)
            {
                starKindAccent = Theme.Blue;
                starKindColors[0] = Color.FromArgb(228, 238, 255);
                starKindColors[1] = ControlPaint.Light(Theme.Blue, .10f);
                starKindColors[2] = ControlPaint.Light(Theme.Violet, .06f);
            }
            return starKindColors[kind >= 0 && kind < 3 ? kind : 0];
        }

        private static Color starKindAccent = Color.Empty;
        private static readonly Color[] starKindColors = new Color[3];

        private Bitmap hazeBlue, hazeViolet, hazeRose, hazeDeep;
        private Color hazeAccent = Color.Empty;

        /// <summary>Two very faint, slowly drifting colour washes that give the black a little depth.</summary>
        public void DrawHaze(Graphics g, float w, float h, float t)
        {
            if (hazeBlue == null || hazeAccent != Theme.Blue)
            {
                if (hazeBlue != null) hazeBlue.Dispose();
                if (hazeViolet != null) hazeViolet.Dispose();
                if (hazeRose != null) hazeRose.Dispose();
                if (hazeDeep != null) hazeDeep.Dispose();
                hazeAccent = Theme.Blue;
                Color a = Theme.Blue;
                Color b = Theme.Violet;
                hazeBlue = MakeHaze(Color.FromArgb(23, a.R, a.G, a.B));
                hazeViolet = MakeHaze(Color.FromArgb(20, b.R, b.G, b.B));
                hazeRose = MakeHaze(Color.FromArgb(11, 120, 55, 145));
                hazeDeep = MakeHaze(Color.FromArgb(15, 32, 52, 110));
            }

            float size = Math.Max(w, h) * 1.22f;
            float bx = w * (0.20f + 0.045f * (float)Math.Sin(t * 0.035)) - size / 2f;
            float by = h * (0.36f + 0.045f * (float)Math.Cos(t * 0.027)) - size / 2f;
            float vx = w * (0.72f + 0.055f * (float)Math.Cos(t * 0.031)) - size / 2f;
            float vy = h * (0.67f + 0.055f * (float)Math.Sin(t * 0.025)) - size / 2f;
            float rx = w * (0.48f + 0.035f * (float)Math.Sin(t * 0.021)) - size / 2f;
            float ry = h * (0.52f + 0.045f * (float)Math.Cos(t * 0.024)) - size / 2f;
            float dx = w * (0.88f + 0.025f * (float)Math.Sin(t * 0.019)) - size / 2f;
            float dy = h * (0.22f + 0.04f * (float)Math.Cos(t * 0.022)) - size / 2f;

            var old = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(hazeDeep, dx, dy, size, size);
            g.DrawImage(hazeBlue, bx, by, size, size);
            g.DrawImage(hazeRose, rx, ry, size * 0.82f, size * 0.82f);
            g.DrawImage(hazeViolet, vx, vy, size, size);
            // Two very faint, drifting orbital contours add depth without competing with the sound cards.
            using (var orbit = new Pen(Color.FromArgb(13, Theme.Blue.R, Theme.Blue.G, Theme.Blue.B), 1f))
            {
                float orbitW = w * 0.70f;
                float orbitH = Math.Max(55f, h * 0.22f);
                float ox = w * 0.58f - orbitW * 0.5f + (float)Math.Sin(t * 0.012f) * 8f;
                float oy = h * 0.64f - orbitH * 0.5f + (float)Math.Cos(t * 0.009f) * 5f;
                g.DrawArc(orbit, ox, oy, orbitW, orbitH, 198f, 118f);
                orbit.Color = Color.FromArgb(9, Theme.Violet.R, Theme.Violet.G, Theme.Violet.B);
                g.DrawArc(orbit, ox + 18f, oy + 14f, orbitW - 36f, orbitH - 28f, 18f, 118f);
            }
            g.InterpolationMode = old;

        }

        private static Bitmap MakeHaze(Color center)
        {
            const int size = 256;
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                FillRadial(g, size / 2f, size / 2f - 1f, center);
            }
            return bmp;
        }

        public void DrawCursorGlow(Graphics g, float x, float y, float level, float k)
        {
            if (level <= 0.01f) return;
            EnsureGlowBitmap();

            float alpha = Theme.Clamp01(level * (0.5f + 0.5f * k));
            if (glowAttr == null || Math.Abs(alpha - glowAttrAlpha) > 0.015f)
            {
                if (glowAttr != null) glowAttr.Dispose();
                glowAttr = new ImageAttributes();
                glowAttr.SetColorMatrix(new ColorMatrix(new float[][]
                {
                    new float[] { 1, 0, 0, 0, 0 },
                    new float[] { 0, 1, 0, 0, 0 },
                    new float[] { 0, 0, 1, 0, 0 },
                    new float[] { 0, 0, 0, alpha, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                }));
                glowAttrAlpha = alpha;
            }

            int size = glowBitmap.Width;
            var dest = new Rectangle((int)(x - size / 2f), (int)(y - size / 2f), size, size);
            g.DrawImage(glowBitmap, dest, 0, 0, size, size, GraphicsUnit.Pixel, glowAttr);
        }

        public void NotifyAccentChanged()
        {
            if (glowBitmap != null) { glowBitmap.Dispose(); glowBitmap = null; }
            if (glowAttr != null) { glowAttr.Dispose(); glowAttr = null; }
            glowAccent = Color.Empty;
            glowAttrAlpha = -1f;
        }

        private void EnsureGlowBitmap()
        {
            if (glowBitmap != null && glowAccent == Theme.Blue) return;
            if (glowBitmap != null) { glowBitmap.Dispose(); glowBitmap = null; }
            glowAccent = Theme.Blue;

            const int size = 560;
            glowBitmap = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(glowBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                FillRadial(g, size / 2f, 270f, Color.FromArgb(30, Theme.Violet.R, Theme.Violet.G, Theme.Violet.B));
                FillRadial(g, size / 2f, 130f, Color.FromArgb(54, Theme.Blue.R, Theme.Blue.G, Theme.Blue.B));
                FillRadial(g, size / 2f, 38f, Color.FromArgb(34, 210, 225, 255));   // tiny cool-white centre
            }
        }

        private static void FillRadial(Graphics g, float center, float radius, Color centerColor)
        {
            var rect = new RectangleF(center - radius, center - radius, radius * 2f, radius * 2f);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(rect);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = centerColor;
                    pgb.SurroundColors = new[] { Color.FromArgb(0, centerColor.R, centerColor.G, centerColor.B) };
                    g.FillPath(pgb, path);
                }
            }
        }

        public void Dispose()
        {
            brush.Dispose();
            if (glowBitmap != null) { glowBitmap.Dispose(); glowBitmap = null; }
            glowAccent = Color.Empty;
            glowAttrAlpha = -1f;
            if (hazeBlue != null) { hazeBlue.Dispose(); hazeBlue = null; }
            if (hazeViolet != null) { hazeViolet.Dispose(); hazeViolet = null; }
            if (hazeRose != null) { hazeRose.Dispose(); hazeRose = null; }
            if (hazeDeep != null) { hazeDeep.Dispose(); hazeDeep = null; }
            if (glowAttr != null) { glowAttr.Dispose(); glowAttr = null; }
        }
    }
}
