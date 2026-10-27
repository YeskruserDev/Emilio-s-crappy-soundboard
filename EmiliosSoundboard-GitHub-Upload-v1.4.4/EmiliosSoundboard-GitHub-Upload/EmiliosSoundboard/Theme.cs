using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>Colours, fonts and small drawing/maths helpers shared by the whole app.</summary>
    internal static class Theme
    {
        public static readonly Color Black = Color.FromArgb(2, 3, 8);
        public static readonly Color DeepNavy = Color.FromArgb(6, 10, 26);
        public static Color PanelFill = Color.FromArgb(11, 16, 38);
        public static Color Blue = Color.FromArgb(76, 141, 255);
        public static Color AccentSoft = Color.FromArgb(28, 40, 72);
        public static Color Violet = Color.FromArgb(152, 102, 255);
        public static readonly Color CoolWhite = Color.FromArgb(228, 237, 255);
        public static readonly Color Muted = Color.FromArgb(132, 146, 188);
        public static readonly Color Danger = Color.FromArgb(228, 58, 72);
        public static readonly Color Success = Color.FromArgb(70, 214, 160);

        public static Font MakeFont(float points, FontStyle style = FontStyle.Regular)
        {
            return new Font("Comic Sans MS", points, style, GraphicsUnit.Point);
        }

        public static Font MakeLightFont(float points)
        {
            // Falls back to the default font automatically if "Segoe UI Light" is not installed.
            return new Font("Comic Sans MS", points, FontStyle.Regular, GraphicsUnit.Point);
        }

        // The main board draws in "logical" 96-dpi units and scales the whole picture to the screen,
        // so its fonts are sized in pixels (otherwise text would be scaled twice on high-DPI screens).
        public static Font MakeLogicalFont(float points, FontStyle style = FontStyle.Regular, string family = "Comic Sans MS")
        {
            return new Font(family, points * 96f / 72f, style, GraphicsUnit.Pixel);
        }

        // ------------------------------------------------------------------ maths

        public static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        public static float EaseOut(float t)
        {
            t = Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        public static float EaseOutBack(float t)
        {
            t = Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>Frame-rate independent smoothing toward a target.</summary>
        public static float Smooth(float current, float target, float rate, float dt)
        {
            return current + (target - current) * (1f - (float)Math.Exp(-rate * dt));
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Clamp01(t);
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color WithAlpha(Color c, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c.R, c.G, c.B);
        }

        // ------------------------------------------------------------------ drawing

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static RectangleF Inflate(RectangleF r, float amount)
        {
            return new RectangleF(r.X - amount, r.Y - amount, r.Width + amount * 2f, r.Height + amount * 2f);
        }

        /// <summary>Draws text with extra spacing between letters, horizontally centred on centerX.</summary>
        public static void DrawSpacedCentered(Graphics g, string text, Font font, Brush brush, float centerX, float y, float spacing)
        {
            float total = MeasureSpaced(g, text, font, spacing);
            DrawSpaced(g, text, font, brush, centerX - total / 2f, y, spacing);
        }

        public static float MeasureSpaced(Graphics g, string text, Font font, float spacing)
        {
            float total = 0f;
            foreach (char ch in text)
                total += CharWidth(ch, font) + spacing;
            return Math.Max(0f, total - spacing);
        }

        public static void DrawSpaced(Graphics g, string text, Font font, Brush brush, float x, float y, float spacing)
        {
            float cx = x;
            foreach (char ch in text)
            {
                if (ch != ' ')
                    g.DrawString(ch.ToString(), font, brush, cx, y, StringFormat.GenericTypographic);
                cx += CharWidth(ch, font) + spacing;
            }
        }

        // Character widths are measured once on a private, unscaled surface and cached.
        private static readonly System.Collections.Generic.Dictionary<string, float> widthCache =
            new System.Collections.Generic.Dictionary<string, float>();
        private static Bitmap measureBitmap;
        private static Graphics measureGraphics;

        private static float CharWidth(char ch, Font font)
        {
            string key = font.Name + "|" + font.Size.ToString("0.##") + "|" + (int)font.Style + "|" + font.Unit + "|" + ch;
            float width;
            if (widthCache.TryGetValue(key, out width)) return width;

            if (measureGraphics == null)
            {
                measureBitmap = new Bitmap(1, 1);
                measureGraphics = Graphics.FromImage(measureBitmap);
                measureGraphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            }

            var fmt = StringFormat.GenericTypographic;
            if (ch == ' ')
            {
                // Trailing spaces are ignored by the typographic format, so measure a space between two letters.
                float with = measureGraphics.MeasureString("A A", font, 4000, fmt).Width;
                float without = measureGraphics.MeasureString("AA", font, 4000, fmt).Width;
                width = Math.Max(2f, with - without);
            }
            else
            {
                width = measureGraphics.MeasureString(ch.ToString(), font, 4000, fmt).Width;
            }

            widthCache[key] = width;
            return width;
        }

        public static string FormatTime(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            if (t.TotalHours >= 1)
                return string.Format("{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
            return string.Format("{0:00}:{1:00}", t.Minutes, t.Seconds);
        }

        // ------------------------------------------------------------------ window helpers

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        /// <summary>Makes the Windows title bar dark where supported (Windows 10 1809+ / Windows 11).</summary>
        public static void ApplyDarkTitleBar(Form form)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref on, 4) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref on, 4);

                int caption = (22 << 16) | (10 << 8) | 8; // COLORREF 0x00BBGGRR, Windows 11 only
                DwmSetWindowAttribute(form.Handle, 35, ref caption, 4);
            }
            catch
            {
                // Older Windows versions simply keep the normal title bar.
            }
        }

        public static float DpiScale()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    return Math.Max(1f, g.DpiX / 96f);
            }
            catch
            {
                return 1f;
            }
        }

        public static Icon CreateAppIcon()
        {
            // Prefer the user's supplied ES logo; retain a generated fallback if the file is missing.
            string logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "es-logo.png");
            using (var bmp = new Bitmap(64, 64))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Black);
                    if (System.IO.File.Exists(logoPath))
                    {
                        using (var logo = Image.FromFile(logoPath))
                        {
                            float ratio = Math.Min(60f / logo.Width, 60f / logo.Height);
                            float w = logo.Width * ratio, h = logo.Height * ratio;
                            g.DrawImage(logo, (64f-w)/2f, (64f-h)/2f, w, h);
                        }
                    }
                    else
                    {
                        using (var back = new LinearGradientBrush(new Rectangle(0, 0, 64, 64), Color.FromArgb(10, 14, 34), Violet, 55f))
                        using (var path = RoundedRect(new RectangleF(2, 2, 60, 60), 15)) g.FillPath(back, path);
                        var pts = new PointF[8];
                        for (int i = 0; i < 8; i++) { double angle = (-90 + 45 * i) * Math.PI / 180.0; float radius = (i % 2 == 0) ? 22f : 6.5f; pts[i] = new PointF(32f + (float)Math.Cos(angle) * radius, 32f + (float)Math.Sin(angle) * radius); }
                        using (var white = new SolidBrush(Color.FromArgb(240, 246, 255))) g.FillPolygon(white, pts);
                    }
                }
                IntPtr handle = bmp.GetHicon();
                try { using (var temp = Icon.FromHandle(handle)) return (Icon)temp.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }
}
