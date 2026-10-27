using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    internal sealed partial class MainForm
    {
        private Font fTitleL, fTitleS, fCount, fAdd, fCardName, fCardSub, fEdit, fStatus, fTime, fToast, fEmptyBig, fEmptySmall;
        private readonly SolidBrush brush = new SolidBrush(Color.White);
        private readonly Pen pen = new Pen(Color.White, 1f);
        private readonly ImageAttributes fadeAttr = new ImageAttributes();
        private readonly StringFormat nameFormat = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
            LineAlignment = StringAlignment.Center
        };
        private readonly StringFormat centerFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        private void CreateFonts()
        {
            fTitleL = Theme.MakeLogicalFont(38f, FontStyle.Bold, "Comic Sans MS");
            fTitleS = Theme.MakeLogicalFont(29f, FontStyle.Bold, "Comic Sans MS");
            fCount = Theme.MakeLogicalFont(8.5f, FontStyle.Bold, "Comic Sans MS");
            fAdd = Theme.MakeLogicalFont(9f, FontStyle.Bold, "Comic Sans MS");
            fCardName = Theme.MakeLogicalFont(13f, FontStyle.Regular, "Comic Sans MS");
            fCardSub = Theme.MakeLogicalFont(8f, FontStyle.Bold, "Comic Sans MS");
            fEdit = Theme.MakeLogicalFont(7.5f, FontStyle.Bold, "Comic Sans MS");
            fStatus = Theme.MakeLogicalFont(9f, FontStyle.Bold, "Comic Sans MS");
            fTime = Theme.MakeLogicalFont(11f, FontStyle.Regular, "Comic Sans MS");
            fToast = Theme.MakeLogicalFont(10f, FontStyle.Regular, "Comic Sans MS");
            fEmptyBig = Theme.MakeLogicalFont(15f, FontStyle.Regular, "Comic Sans MS");
            fEmptySmall = Theme.MakeLogicalFont(10f, FontStyle.Regular, "Comic Sans MS");
        }

        private void DisposeFonts()
        {
            foreach (var f in new[] { fTitleL, fTitleS, fCount, fAdd, fCardName, fCardSub, fEdit, fStatus, fTime, fToast, fEmptyBig, fEmptySmall })
                if (f != null) f.Dispose();
            brush.Dispose();
            pen.Dispose();
            fadeAttr.Dispose();
            nameFormat.Dispose();
            centerFormat.Dispose();
        }

        private SolidBrush B(Color c)
        {
            brush.Color = c;
            return brush;
        }

        private Pen P(Color c, float width)
        {
            pen.Color = c;
            pen.Width = width;
            return pen;
        }

        // ------------------------------------------------------------------ main paint

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.ScaleTransform(scale, scale);

            float w = LW, h = LH;
            var set = store.Data.Settings;
            float k = set.Intensity / 100f * 1.6f;

            // Background
            var full = new RectangleF(-1, -1, w + 2, h + 2);
            using (var bg = new LinearGradientBrush(full, Color.FromArgb(1, 2, 5), Color.FromArgb(0, 0, 1), 90f))
                g.FillRectangle(bg, full);

            if (galaxyBackground != null)
            {
                float pan = (float)Math.Sin(hazeTime * 0.018f) * 8f;
                var dest = new RectangleF(-pan, -4f, w + 2f * pan, h + 8f);
                g.DrawImage(galaxyBackground, dest);
                using (var dim = new SolidBrush(Color.FromArgb(64, 0, 0, 5))) g.FillRectangle(dim, full);
            }
            field.DrawHaze(g, w, h, hazeTime);
            if (set.CoolMode) DrawRainbowRoad(g, w, h, hazeTime);
            field.Draw(g, bgLevel);
            field.DrawCursorGlow(g, glowX, glowY, glowLevel, k);

            // Cards (clipped to the scrolling area)
            var gridClip = new RectangleF(0, GridTop - 6f, w, Math.Max(1f, GridBottom - GridTop + 12f));
            var st = g.Save();
            g.SetClip(gridClip);
            if (VisibleCount() == 0) DrawEmptyState(g);
            foreach (Card c in OrderedVisibleCards()) DrawCard(g, c, k);
            g.Restore(st);

            DrawScrollBar(g);
            DrawHeader(g);
            DrawFooter(g);
            DrawToast(g);
        }

        private void DrawRainbowRoad(Graphics g, float w, float h, float t)
        {
            // Perspective Rainbow Road: raised polygonal ribbon with cursor-reactive S bends and a small looping Mario kart sprite.
            Color[] colors = { Color.Magenta, Color.Red, Color.Orange, Color.Yellow, Color.Lime, Color.Cyan, Color.DodgerBlue, Color.Violet };
            const int count = 100;
            var center = new PointF[count];
            var widths = new float[count];
            float horizon = h * .60f;
            float cursorX = hasMouse ? Math.Max(-1f, Math.Min(1f, (mx / Math.Max(1f, w) - .5f) * 2f)) : 0f;
            float cursorY = hasMouse ? Math.Max(-1f, Math.Min(1f, (my / Math.Max(1f, h) - .58f))) : 0f;
            for (int i = 0; i < count; i++)
            {
                float u = i / (float)(count - 1);
                float influence = (float)Math.Exp(-Math.Pow((u - (hasMouse ? mx / Math.Max(1f, w) : .5f)) * 2.3f, 2));
                float perspective = .35f + u * .95f;
                float x = -w * .10f + u * w * 1.20f;
                x += (float)Math.Sin(u * 5.2f + t * .38f) * w * (.018f + .014f * u);
                x += cursorX * influence * w * .075f;
                float y = horizon + (float)Math.Sin(u * 4.8f + t * .32f) * (8f + u * 24f) + u * u * h * .22f;
                y += cursorY * influence * h * .065f;
                center[i] = new PointF(x, y);
                widths[i] = (42f + 78f * u) * perspective;
            }

            PointF[] left = new PointF[count], right = new PointF[count];
            PointF[] sideLeft = new PointF[count], sideRight = new PointF[count];
            for (int i = 0; i < count; i++)
            {
                PointF before = center[Math.Max(0, i - 1)], after = center[Math.Min(count - 1, i + 1)];
                float dx = after.X - before.X, dy = after.Y - before.Y;
                float len = Math.Max(1f, (float)Math.Sqrt(dx * dx + dy * dy));
                float nx = -dy / len, ny = dx / len;
                float half = widths[i] * .5f;
                left[i] = new PointF(center[i].X + nx * half, center[i].Y + ny * half);
                right[i] = new PointF(center[i].X - nx * half, center[i].Y - ny * half);
                sideLeft[i] = new PointF(left[i].X, left[i].Y + 13f + 8f * (i / (float)count));
                sideRight[i] = new PointF(right[i].X, right[i].Y + 13f + 8f * (i / (float)count));
            }

            // Soft drop shadow and thick dark 3D underside.
            using (var shadow = new SolidBrush(Color.FromArgb(100, 0, 0, 0)))
            {
                PointF[] poly = new PointF[count * 2];
                for (int i = 0; i < count; i++) poly[i] = new PointF(sideLeft[i].X, sideLeft[i].Y + 8f);
                for (int i = 0; i < count; i++) poly[count + i] = new PointF(sideRight[count - 1 - i].X, sideRight[count - 1 - i].Y + 8f);
                g.FillPolygon(shadow, poly);
            }
            using (var underside = new SolidBrush(Color.FromArgb(255, 22, 18, 43)))
            using (var side = new SolidBrush(Color.FromArgb(255, 74, 67, 99)))
            using (var edge = new Pen(Color.FromArgb(235, 225, 225, 255), 2.2f))
            {
                PointF[] poly = new PointF[count * 2];
                for (int i = 0; i < count; i++) poly[i] = sideLeft[i];
                for (int i = 0; i < count; i++) poly[count + i] = sideRight[count - 1 - i];
                g.FillPolygon(underside, poly);
                // Exposed front lip makes the road look raised, not like a line.
                for (int i = 0; i < count - 1; i++)
                {
                    using (var face = new SolidBrush(Color.FromArgb(225, 66, 57, 88)))
                    {
                        g.FillPolygon(face, new[] { left[i], left[i + 1], sideLeft[i + 1], sideLeft[i] });
                        g.FillPolygon(face, new[] { right[i], right[i + 1], sideRight[i + 1], sideRight[i] });
                    }
                }
                PointF[] surface = new PointF[count * 2];
                for (int i = 0; i < count; i++) surface[i] = left[i];
                for (int i = 0; i < count; i++) surface[count + i] = right[count - 1 - i];
                using (var bed = new SolidBrush(Color.FromArgb(255, 12, 10, 29))) g.FillPolygon(bed, surface);
                g.DrawLines(edge, left); g.DrawLines(edge, right);
            }

            // Fill each rainbow lane as a ribbon polygon, following the road's changing width.
            for (int lane = 0; lane < colors.Length; lane++)
            {
                float f0 = .08f + lane * (.84f / colors.Length);
                float f1 = .08f + (lane + 1) * (.84f / colors.Length);
                PointF[] band = new PointF[count * 2];
                for (int i = 0; i < count; i++)
                {
                    float u = f0;
                    band[i] = new PointF(left[i].X + (right[i].X - left[i].X) * u, left[i].Y + (right[i].Y - left[i].Y) * u);
                }
                for (int i = 0; i < count; i++)
                {
                    float u = f1;
                    band[count + i] = new PointF(left[count - 1 - i].X + (right[count - 1 - i].X - left[count - 1 - i].X) * u,
                        left[count - 1 - i].Y + (right[count - 1 - i].Y - left[count - 1 - i].Y) * u);
                }
                using (var glow = new Pen(Color.FromArgb(90, colors[lane]), 7f))
                {
                    glow.LineJoin = LineJoin.Round;
                    var mid = new PointF[count];
                    for (int i = 0; i < count; i++) mid[i] = new PointF((band[i].X + band[count + (count - 1 - i)].X) * .5f, (band[i].Y + band[count + (count - 1 - i)].Y) * .5f);
                    g.DrawLines(glow, mid);
                }
                using (var fill = new SolidBrush(Color.FromArgb(245, colors[lane]))) g.FillPolygon(fill, band);
                using (var shine = new Pen(Color.FromArgb(95, 255, 255, 255), 1f))
                {
                    var line = new PointF[count];
                    for (int i = 0; i < count; i++) line[i] = band[i];
                    g.DrawLines(shine, line);
                }
            }
            DrawMarioKartOnRoad(g, w, center, t);
            using (var f = Theme.MakeLogicalFont(8f, FontStyle.Bold, "Comic Sans MS"))
            using (var b = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
                g.DrawString("COOL MODE • RAINBOW ROAD", f, b, 22f, h - 116f);
        }

        private void DrawMarioKartOnRoad(Graphics g, float w, PointF[] roadCenter, float t)
        {
            if (marioKartSprite == null || roadCenter == null || roadCenter.Length < 2) return;
            // Each lap lasts about 4.5 seconds, followed by a 15-second pause. Small deterministic
            // variations make subsequent laps slightly faster or slower without looking erratic.
            double lap = 19.5;
            double cycle = t % lap;
            int lapIndex = (int)(t / lap);
            float driveDuration = 4.4f + 0.32f * (float)Math.Sin(lapIndex * 1.73f + 0.4f);
            if (cycle >= driveDuration) return;
            float u = (float)(cycle / driveDuration);
            float spriteW = 56f + 48f * u;
            float spriteH = spriteW * marioKartSprite.Height / (float)marioKartSprite.Width;
            float x = -spriteW + (w + spriteW) * u;
            int idx = Math.Max(0, Math.Min(roadCenter.Length - 1, (int)(u * (roadCenter.Length - 1))));
            float y = roadCenter[idx].Y - spriteH * 0.76f;
            var dest = new RectangleF(x, y, spriteW, spriteH);
            // Soft shadow anchors the transparent PNG to the road surface.
            using (var shadow = new SolidBrush(Color.FromArgb(85, 0, 0, 0)))
                g.FillEllipse(shadow, x + spriteW * .16f, y + spriteH * .78f, spriteW * .68f, spriteH * .16f);
            g.DrawImage(marioKartSprite, dest);
        }

        // ------------------------------------------------------------------ header

        private void DrawHeader(Graphics g)
        {
            float w = LW;
            using (var creditFont = Theme.MakeLogicalFont(8.5f, FontStyle.Bold, "Comic Sans MS"))
            using (var creditBrush = new SolidBrush(Color.FromArgb(230, Theme.CoolWhite)))
                g.DrawString("Made by Emilio", creditFont, creditBrush, 16f, 12f);
            // Paint the GitHub link on the same owner-drawn surface so it persists after every restart
            // and cannot be hidden by transparent child-control painting.
            using (var linkFont = Theme.MakeLogicalFont(7.8f, FontStyle.Underline, "Comic Sans MS"))
            using (var linkBrush = new SolidBrush(Theme.Blue))
                g.DrawString("github.com/emilspaskevics1-max", linkFont, linkBrush, 16f, 29f);

            // Keep the header visually continuous with the starfield; no hard separator line.

            // Title stays centered on the actual window. Shrink it to fit between the side controls.
            const string title = "EMILIO'S SOUNDBOARD";
            float sideReserve = w < 760f ? 150f : 360f;
            float safeWidth = Math.Max(240f, w - sideReserve);
            float spacing = w < 760f ? 1.8f : 2.6f;
            float titleSize = w < 760f ? 29f : 38f;
            Font tf = null;
            float tw = 0f;
            while (titleSize >= 18f)
            {
                if (tf != null) tf.Dispose();
                tf = Theme.MakeLogicalFont(titleSize, FontStyle.Bold, "Comic Sans MS");
                tw = Theme.MeasureSpaced(g, title, tf, spacing);
                if (tw <= safeWidth) break;
                titleSize -= 1.5f;
            }
            if (tf == null) tf = fTitleS;
            float ty = 24f;
            float tx = (w - Math.Min(tw, safeWidth)) / 2f;
            var titleRect = new RectangleF(tx, ty, Math.Min(tw, safeWidth), tf.Height);
            using (var glow = new SolidBrush(Color.FromArgb(26, Theme.Blue)))
            {
                Theme.DrawSpaced(g, title, tf, glow, titleRect.X - 1f, ty + 1f, spacing);
                Theme.DrawSpaced(g, title, tf, glow, titleRect.X + 1f, ty - 1f, spacing);
            }
            using (var gb = new LinearGradientBrush(titleRect, Color.FromArgb(248, 250, 255), Color.FromArgb(172, 192, 255), 0f))
                Theme.DrawSpaced(g, title, tf, gb, titleRect.X, ty, spacing);
            if (!ReferenceEquals(tf, fTitleS) && !ReferenceEquals(tf, fTitleL)) tf.Dispose();

            // Sound count centered on the whole window, below the title.
            int n = VisibleCount();
            string count = n == 0 ? "NO SOUNDS YET" : (n == 1 ? "1 SOUND" : n + " SOUNDS");
            Theme.DrawSpacedCentered(g, count, fCount, B(Theme.CoolWhite), w / 2f, ty + 76f, 2.2f);

            DrawAddButton(g);
            DrawVoiceFxButton(g);
            DrawGear(g);
        }

        private void DrawVoiceFxButton(Graphics g)
        {
            RectangleF r = VoiceFxRect();
            using (var path = Theme.RoundedRect(r, 12f))
            using (var fill = new LinearGradientBrush(r, Theme.AccentSoft, Theme.PanelFill, 90f))
            using (var edge = new Pen(Theme.WithAlpha(Theme.Blue, 210), 1.4f))
            using (var font = Theme.MakeLogicalFont(10f, FontStyle.Bold, "Comic Sans MS"))
            using (var text = new SolidBrush(Theme.CoolWhite))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.FillPath(fill, path); g.DrawPath(edge, path);
                g.DrawString("VOICE FX", font, text, r, sf);
            }
        }

        private void DrawAddButton(Graphics g)
        {
            RectangleF r = AddRect();
            float s = 1f + 0.03f * addHover - 0.045f * addPress;
            var st = g.Save();
            g.TranslateTransform(r.X + r.Width / 2f, r.Y + r.Height / 2f);
            g.ScaleTransform(s, s);
            g.TranslateTransform(-r.Width / 2f, -r.Height / 2f);
            var local = new RectangleF(0, 0, r.Width, r.Height);

            float breathe = 0.5f + 0.5f * (float)Math.Sin(time * 1.6f);
            float glow = 0.35f + 0.25f * breathe + 0.7f * addHover;
            for (int i = 4; i >= 1; i--)
            {
                using (var gp = Theme.RoundedRect(Theme.Inflate(local, i * 2.2f), 12f + i * 1.6f))
                    g.DrawPath(P(Theme.WithAlpha(Theme.Blue, (int)(Math.Min(1f, glow) * 30f / i)), 2f), gp);
            }

            using (var path = Theme.RoundedRect(local, 12f))
            {
                Color a = Theme.Mix(Theme.Blue, Color.White, addHover * 0.16f);
                Color b = Theme.Mix(Theme.Violet, Color.White, addHover * 0.16f);
                using (var fill = new LinearGradientBrush(local, a, b, 0f))
                    g.FillPath(fill, path);
                g.DrawPath(P(Color.FromArgb(90, 255, 255, 255), 1f), path);
            }

            float fy = (r.Height - fAdd.Height) / 2f + 1f;
            Theme.DrawSpacedCentered(g, "+ ADD SOUND", fAdd, B(Color.White), r.Width / 2f, fy, 0.6f);
            g.Restore(st);
        }

        private void DrawGear(Graphics g)
        {
            PointF c = SettingsCenter();
            float r = 22f * (1f + 0.06f * setHover);

            for (int i = 3; i >= 1; i--)
                g.DrawEllipse(P(Theme.WithAlpha(Theme.Blue, (int)(setHover * 30f / i)), 2f),
                    c.X - r - i * 2f, c.Y - r - i * 2f, (r + i * 2f) * 2f, (r + i * 2f) * 2f);

            g.FillEllipse(B(Color.FromArgb(220, 12, 17, 40)), c.X - r, c.Y - r, r * 2f, r * 2f);
            g.DrawEllipse(P(Theme.Mix(Color.FromArgb(80, 96, 160), Theme.Blue, setHover), 1.2f),
                c.X - r, c.Y - r, r * 2f, r * 2f);

            var st = g.Save();
            g.TranslateTransform(c.X, c.Y);
            g.RotateTransform(gearAngle);
            Color icon = Theme.Mix(Theme.CoolWhite, Color.White, setHover);
            g.DrawEllipse(P(icon, 3.2f), -6.2f, -6.2f, 12.4f, 12.4f);
            SolidBrush tooth = B(icon);
            for (int i = 0; i < 8; i++)
            {
                g.FillRectangle(tooth, -2f, -11.5f, 4f, 5.5f);
                g.RotateTransform(45f);
            }
            g.Restore(st);
        }

        // ------------------------------------------------------------------ cards

        private void DrawCard(Graphics g, Card c, float k)
        {
            float app = c.Appear;
            if (app <= 0f) return;

            float sy = GridTop + c.Y - scrollY;
            if (sy > GridBottom + 40f || sy + CardH < GridTop - 40f) return;

            bool hoverAnim = store.Data.Settings.HoverAnimations;
            float appE = Theme.EaseOutBack(Math.Min(1f, app));
            float alpha = Theme.Clamp01(app * 1.6f);
            float s = 0.84f + 0.16f * appE;
            if (hoverAnim) s *= 1f + 0.022f * c.Hover;
            s *= 1f - 0.04f * c.Press;
            float lift = hoverAnim ? -3.5f * c.Hover : 0f;

            float w = c.W, h = CardH;
            var st = g.Save();
            g.TranslateTransform(c.X + w / 2f, sy + h / 2f + lift);
            g.ScaleTransform(s, s);
            g.TranslateTransform(-w / 2f, -h / 2f);

            var rect = new RectangleF(0, 0, w, h);
            float pulse = 0.5f + 0.5f * (float)Math.Sin(time * 3.2f);
            float hoverGlow = hoverAnim ? c.Hover : 0f;
            float glowAmt = Math.Max(hoverGlow, c.Active * (0.65f + 0.35f * pulse));
            Color glowColor = c.Active > 0.2f
                ? Theme.Mix(Theme.Blue, Theme.Violet, 0.5f + 0.5f * (float)Math.Sin(time * 1.3f))
                : Theme.Blue;

            // Glow rings
            if (glowAmt > 0.02f)
            {
                for (int i = 3; i >= 1; i--)
                {
                    using (var gp = Theme.RoundedRect(Theme.Inflate(rect, i * 1.8f), 18f + i * 1.8f))
                        g.DrawPath(P(Theme.WithAlpha(glowColor, (int)(alpha * glowAmt * 40f / i)), 2.2f), gp);
                }
            }

            using (var path = Theme.RoundedRect(rect, 18f))
            {
                // Artwork
                if (c.Cover != null)
                {
                    var dest = new[] { new PointF(0, 0), new PointF(w, 0), new PointF(0, h) };
                    var src = new RectangleF(0, 0, c.CoverW, c.CoverH);
                    if (alpha < 0.99f)
                    {
                        fadeAttr.SetColorMatrix(new ColorMatrix(new float[][]
                        {
                            new float[] { 1, 0, 0, 0, 0 },
                            new float[] { 0, 1, 0, 0, 0 },
                            new float[] { 0, 0, 1, 0, 0 },
                            new float[] { 0, 0, 0, alpha, 0 },
                            new float[] { 0, 0, 0, 0, 1 }
                        }));
                        g.DrawImage(c.Cover, dest, src, GraphicsUnit.Pixel, fadeAttr);
                    }
                    else g.DrawImage(c.Cover, dest, src, GraphicsUnit.Pixel);
                }
                else
                {
                    g.FillPath(B(Theme.WithAlpha(Color.FromArgb(10, 15, 36), (int)(alpha * 255f))), path);
                }

                // Hover brightening, cursor spotlight and click ripple
                if (c.Hover > 0.01f || c.RippleT < 1f)
                {
                    var clip = g.Save();
                    g.SetClip(path, CombineMode.Intersect);

                    if (c.Hover > 0.01f)
                    {
                        g.FillPath(B(Color.FromArgb((int)(alpha * 12f * c.Hover), 255, 255, 255)), path);
                        if (store.Data.Settings.CursorGlow && glowLevel > 0.01f)
                        {
                            float lx = (mx - (c.X + w / 2f)) / s + w / 2f;
                            float ly = (my - (sy + h / 2f + lift)) / s + h / 2f;
                            field.DrawCursorGlow(g, lx, ly, c.Hover * 0.9f, k);
                        }
                    }

                    if (c.RippleT < 1f)
                    {
                        float t = Theme.EaseOut(c.RippleT);
                        float rr = t * w * 0.85f;
                        int ra = (int)(alpha * (1f - c.RippleT) * 70f);
                        g.FillEllipse(B(Color.FromArgb(ra, 150, 190, 255)), c.RippleX - rr, c.RippleY - rr, rr * 2f, rr * 2f);
                    }
                    g.Restore(clip);
                }

                // Border
                float edge = Math.Max(c.Hover, c.Active);
                Color border = Theme.Mix(Color.FromArgb(78, 78, 100, 170), Theme.WithAlpha(glowColor, 235), edge);
                g.DrawPath(P(Theme.WithAlpha(border, (int)(alpha * border.A)), 1.3f), path);
            }

            DrawCardContent(g, c, alpha, pulse);
            g.Restore(st);
        }

        private void DrawCardContent(Graphics g, Card c, float alpha, float pulse)
        {
            float w = c.W, h = CardH;
            Func<int, int> A = a => (int)(a * alpha);

            // Playback line along the bottom while this sound is playing
            if (c.Active > 0.02f)
            {
                float lw = (w - 36f) * progressShown;
                var track = new RectangleF(18f, h - 9f, w - 36f, 3f);
                using (var tp = Theme.RoundedRect(track, 1.5f))
                    g.FillPath(B(Color.FromArgb(A((int)(60 * c.Active)), 255, 255, 255)), tp);
                if (lw > 2f)
                {
                    var fill = new RectangleF(18f, h - 9f, lw, 3f);
                    using (var fp = Theme.RoundedRect(fill, 1.5f))
                    using (var fb = new LinearGradientBrush(new RectangleF(18f, 0, Math.Max(2f, w - 36f), 4f), Theme.Blue, Theme.Violet, 0f))
                        g.FillPath(fb, fp);
                }
            }

            // Name + type
            float playCx = w - 44f, playCy = h - 44f;
            float textW = Math.Max(40f, playCx - 28f - 18f - 6f);
            var nameRect = new RectangleF(18f, playCy - 20f, textW, 22f);
            g.DrawString(c.Item.Name, fCardName, B(Color.FromArgb(A(170), 0, 0, 0)),
                new RectangleF(nameRect.X + 1f, nameRect.Y + 1f, nameRect.Width, nameRect.Height), nameFormat);
            g.DrawString(c.Item.Name, fCardName, B(Color.FromArgb(A(255), 244, 248, 255)), nameRect, nameFormat);

            string sub = c.Item.AudioMissing ? "FILE MISSING" : c.Ext;
            Color subColor = c.Item.AudioMissing ? Color.FromArgb(255, 120, 130) : Color.FromArgb(150, 168, 215);
            g.DrawString(sub, fCardSub, B(Theme.WithAlpha(subColor, A(235))),
                new RectangleF(18f, playCy + 4f, textW, 16f), nameFormat);

            // Play button
            float pr = 25f * (1f + 0.09f * c.PlayHover);
            float ringAmt = Math.Max(c.PlayHover, c.Active);
            for (int i = 3; i >= 1; i--)
            {
                float rr = pr + i * 3f;
                g.FillEllipse(B(Color.FromArgb(A((int)(ringAmt * 34f / i)), 110, 140, 255)), playCx - rr, playCy - rr, rr * 2f, rr * 2f);
            }
            var pb = new RectangleF(playCx - pr, playCy - pr, pr * 2f, pr * 2f);
            using (var pg = new LinearGradientBrush(pb, Theme.Mix(Theme.Blue, Color.White, c.PlayHover * 0.15f),
                                                    Theme.Mix(Theme.Violet, Color.White, c.PlayHover * 0.15f), 45f))
                g.FillEllipse(pg, pb);
            g.DrawEllipse(P(Color.FromArgb(A(80), 255, 255, 255), 1f), pb);

            if (c.Active > 0.5f)
            {
                for (int i = 0; i < 3; i++)
                {
                    float bh = 7f + 11f * Math.Abs((float)Math.Sin(time * 6.5f + i * 1.7f));
                    g.FillRectangle(B(Color.FromArgb(A(255), 255, 255, 255)), playCx - 8.5f + i * 6.5f, playCy - bh / 2f, 4f, bh);
                }
            }
            else
            {
                float t = 8.5f;
                g.FillPolygon(B(Color.FromArgb(A(255), 255, 255, 255)), new[]
                {
                    new PointF(playCx - t * 0.7f + 1.5f, playCy - t),
                    new PointF(playCx + t + 1.5f, playCy),
                    new PointF(playCx - t * 0.7f + 1.5f, playCy + t)
                });
            }

            // Edit pill (top-left)
            var er = new RectangleF(EditRect.X, EditRect.Y, EditRect.Width, EditRect.Height);
            using (var ep = Theme.RoundedRect(er, 12f))
            {
                g.FillPath(B(Theme.WithAlpha(Theme.Mix(Color.FromArgb(0, 4, 12), Color.FromArgb(40, 70, 150), c.EditHover), A(165))), ep);
                g.DrawPath(P(Theme.WithAlpha(Theme.Mix(Color.FromArgb(150, 170, 220), Theme.Blue, c.EditHover), A(120)), 1f), ep);
            }
            g.DrawString("EDIT", fEdit, B(Color.FromArgb(A(235), 225, 235, 255)), er, centerFormat);

            // Delete button (top-right, small and red)
            float dr = 12f * (1f + 0.14f * c.DeleteHover);
            float dx = w - 20f, dy = 20f;
            if (c.DeleteHover > 0.01f)
                g.FillEllipse(B(Color.FromArgb(A((int)(60 * c.DeleteHover)), 255, 70, 85)), dx - dr - 4f, dy - dr - 4f, (dr + 4f) * 2f, (dr + 4f) * 2f);
            g.FillEllipse(B(Theme.WithAlpha(Theme.Mix(Color.FromArgb(206, 44, 60), Color.FromArgb(255, 84, 98), c.DeleteHover), A(240))),
                dx - dr, dy - dr, dr * 2f, dr * 2f);
            using (var xp = new Pen(Color.FromArgb(A(255), 255, 255, 255), 1.9f))
            {
                xp.StartCap = LineCap.Round;
                xp.EndCap = LineCap.Round;
                g.DrawLine(xp, dx - 4f, dy - 4f, dx + 4f, dy + 4f);
                g.DrawLine(xp, dx - 4f, dy + 4f, dx + 4f, dy - 4f);
            }
        }

        private void DrawEmptyState(Graphics g)
        {
            RectangleF r = EmptyRect();
            float lift = emptyHover * 2f;
            r.Y -= lift;

            using (var path = Theme.RoundedRect(r, 22f))
            {
                g.FillPath(B(Color.FromArgb(110, 8, 12, 30)), path);
                using (var dash = new Pen(Theme.Mix(Color.FromArgb(70, 80, 110, 200), Theme.Blue, emptyHover), 1.4f))
                {
                    dash.DashStyle = DashStyle.Dash;
                    g.DrawPath(dash, path);
                }
            }

            // Larger glossy planet with layered orbital rings, a radial glow, and two orbiting moons.
            float cx = r.X + r.Width / 2f, cy = r.Y + 60f;
            using (var glow = new PathGradientBrush(new[] { new PointF(cx - 31, cy - 31), new PointF(cx + 31, cy - 31), new PointF(cx + 31, cy + 31), new PointF(cx - 31, cy + 31) }))
            {
                glow.CenterPoint = new PointF(cx, cy); glow.CenterColor = Color.FromArgb(65, Theme.Blue); glow.SurroundColors = new[] { Color.FromArgb(0, Theme.Blue), Color.FromArgb(0, Theme.Blue), Color.FromArgb(0, Theme.Blue), Color.FromArgb(0, Theme.Blue) };
                g.FillEllipse(glow, cx - 37f, cy - 37f, 74f, 74f);
            }
            using (var orbit = new Pen(Color.FromArgb(105, Theme.Blue), 1.4f)) g.DrawEllipse(orbit, cx - 46f, cy - 17f, 92f, 34f);
            using (var orbit2 = new Pen(Color.FromArgb(65, Theme.Violet), 1f)) { g.DrawEllipse(orbit2, cx - 42f, cy - 24f, 84f, 48f); }
            using (var planet = new LinearGradientBrush(new RectangleF(cx - 14f, cy - 14f, 28f, 28f), Color.FromArgb(255, 246, 251, 255), Theme.Blue, 45f))
                g.FillEllipse(planet, cx - 14f, cy - 14f, 28f, 28f);
            using (var shine = new SolidBrush(Color.FromArgb(175, 255, 255, 255))) g.FillEllipse(shine, cx - 8f, cy - 9f, 8f, 6f);
            float ang = time * 1.2f;
            g.FillEllipse(B(Color.White), cx + (float)Math.Cos(ang) * 46f - 4f, cy + (float)Math.Sin(ang) * 17f - 4f, 8f, 8f);
            float ang2 = -time * .72f + 1.8f;
            g.FillEllipse(B(Theme.Violet), cx + (float)Math.Cos(ang2) * 41f - 3f, cy + (float)Math.Sin(ang2) * 24f - 3f, 6f, 6f);

            bool searching = searchBox != null && !String.IsNullOrWhiteSpace(searchBox.Text);
            var t1 = new RectangleF(r.X, r.Y + 104f, r.Width, 28f);
            g.DrawString(searching ? "No matching sounds" : "No sounds yet", fEmptyBig, B(Theme.CoolWhite), t1, centerFormat);
            var t2 = new RectangleF(r.X + 20f, r.Y + 134f, r.Width - 40f, 50f);
            g.DrawString(searching ? "Try a different search term or choose ALL." : "Click here or press + ADD SOUND to import your first audio file.\nYou can pick several at once.",
                fEmptySmall, B(Theme.Muted), t2, new StringFormat { Alignment = StringAlignment.Center });
        }

        private void DrawScrollBar(Graphics g)
        {
            if (maxScroll <= 1f || scrollFade <= 0f) return;
            float view = GridBottom - GridTop;
            float total = view + maxScroll;
            float thumbH = Math.Max(36f, view * view / total);
            float thumbY = GridTop + (view - thumbH) * (scrollY / maxScroll);
            int a = (int)(110f * Theme.Clamp01(scrollFade));
            using (var p = Theme.RoundedRect(new RectangleF(LW - 12f, thumbY, 4f, thumbH), 2f))
                g.FillPath(B(Color.FromArgb(a, 150, 180, 255)), p);
        }

        // ------------------------------------------------------------------ footer and toast

        private RectangleF FooterRect()
        {
            float x = 150f;
            float pw = Math.Max(220f, LW - x - 28f);
            return new RectangleF(x, LH - 88f, pw, 70f);
        }

        private void DrawFooter(Graphics g)
        {
            RectangleF r = FooterRect();
            string status;
            Color dot;
            switch (state)
            {
                case PlayState.Playing:
                    status = "NOW PLAYING  ·  " + currentName.ToUpperInvariant();
                    dot = Theme.Mix(Theme.Blue, Color.White, 0.5f + 0.25f * (float)Math.Sin(time * 5f));
                    break;
                case PlayState.Complete: status = "PLAYBACK COMPLETE"; dot = Theme.Success; break;
                case PlayState.Error: status = "COULD NOT PLAY"; dot = Theme.Danger; break;
                default: status = "READY"; dot = Theme.Muted; break;
            }

            // No enclosing blue panel: center the playback status and time over the pill.
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                var statusRect = new RectangleF(r.X, r.Y - 1f, r.Width, 20f);
                g.DrawString(status, fStatus, B(Theme.CoolWhite), statusRect, sf);
                string timeText = Theme.FormatTime(lastPos) + " / " + Theme.FormatTime(lastDur);
                var timeRect = new RectangleF(r.X, r.Y + 17f, r.Width, 18f);
                g.DrawString(timeText, fTime, B(Theme.CoolWhite), timeRect, sf);
            }

            var track = new RectangleF(r.X + 2f, r.Y + 38f, r.Width - 4f, 30f);
            using (var outer = Theme.RoundedRect(track, 15f))
            using (var rim = new LinearGradientBrush(track, Color.FromArgb(105, 99, 105), Color.FromArgb(49, 43, 53), 90f))
                g.FillPath(rim, outer);
            var inner = new RectangleF(track.X + 3f, track.Y + 3f, track.Width - 6f, track.Height - 6f);
            using (var innerPath = Theme.RoundedRect(inner, 12f))
            using (var channel = new LinearGradientBrush(inner, Color.FromArgb(22, 22, 25), Color.FromArgb(67, 60, 69), 90f))
                g.FillPath(channel, innerPath);
            using (var edge = new Pen(Color.FromArgb(90, 215, 210, 220), 0.8f))
            using (var edgePath = Theme.RoundedRect(new RectangleF(track.X + 1f, track.Y + 1f, track.Width - 2f, track.Height - 2f), 14f))
                g.DrawPath(edge, edgePath);

            float fw = (inner.Width - 2f) * Theme.Clamp01(progressShown);
            if (fw >= 1f)
            {
                var fr = new RectangleF(inner.X + 1f, inner.Y + 1f, Math.Min(inner.Width - 2f, Math.Max(fw, inner.Height)), inner.Height - 2f);
                using (var fp = Theme.RoundedRect(fr, (inner.Height - 2f) / 2f))
                {
                    // Fill now follows the chosen accent colour instead of remaining silver.
                    Color accentEnd = Theme.Mix(Theme.Blue, Theme.Violet, 0.45f);
                    using (var fill = new LinearGradientBrush(fr, Theme.Mix(Theme.Blue, Color.White, 0.42f), accentEnd, 90f))
                        g.FillPath(fill, fp);
                    var save = g.Save(); g.SetClip(fp, CombineMode.Intersect);
                    using (var stripes = new Pen(Color.FromArgb(40, 255, 255, 255), 1.2f))
                        for (float x = fr.X - fr.Height; x < fr.Right + fr.Height; x += 13f)
                            g.DrawLine(stripes, x, fr.Bottom, x + fr.Height * 0.72f, fr.Y);
                    using (var sheen = new LinearGradientBrush(fr, Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                        g.FillPath(sheen, fp);
                    g.Restore(save);
                }
            }
        }

        private void DrawToast(Graphics g)
        {
            if (toastLife <= 0f || string.IsNullOrEmpty(toastText)) return;

            float fadeIn = Theme.Clamp01((3.8f - toastLife) / 0.2f);
            float fadeOut = Theme.Clamp01(toastLife / 0.5f);
            float a = Math.Min(fadeIn, fadeOut);

            float maxW = Math.Min(LW - 60f, 640f);
            SizeF size = g.MeasureString(toastText, fToast, (int)(maxW - 36f));
            float tw = Math.Min(maxW, size.Width + 36f);
            float th = size.Height + 18f;
            RectangleF footer = FooterRect();
            float ty = footer.Y - th - 14f - (1f - fadeIn) * -8f;
            var r = new RectangleF((LW - tw) / 2f, ty, tw, th);

            using (var path = Theme.RoundedRect(r, th / 2f > 18f ? 14f : th / 2f))
            {
                g.FillPath(B(Color.FromArgb((int)(a * 238f), 10, 14, 34)), path);
                g.DrawPath(P(Theme.WithAlpha(toastColor, (int)(a * 200f)), 1.2f), path);
            }
            g.DrawString(toastText, fToast, B(Color.FromArgb((int)(a * 255f), Theme.CoolWhite)),
                new RectangleF(r.X + 18f, r.Y + 9f, r.Width - 36f, r.Height - 12f));
        }
    }
}
