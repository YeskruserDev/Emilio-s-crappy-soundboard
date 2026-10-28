using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>Colours, fonts and small drawing/maths helpers shared by the whole app.</summary>
    internal static class Theme
    {
        public static bool ComicStyleMode { get; set; }

        private sealed class SavedFont
        {
            public string Family;
            public float Size;
            public FontStyle Style;
            public GraphicsUnit Unit;
            public byte CharSet;
            public bool Vertical;
            public Color BackColor;
            public Color ForeColor;
        }

        private static readonly ConditionalWeakTable<Control, SavedFont> savedFonts =
            new ConditionalWeakTable<Control, SavedFont>();

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

        // The main board draws in "logical" 96-dpi units and scales the whole picture to the screen,
        // so its fonts are sized in pixels (otherwise text would be scaled twice on high-DPI screens).
        public static Font MakeLogicalFont(float points, FontStyle style = FontStyle.Regular, string family = "Comic Sans MS")
        {
            if (ComicStyleMode)
            {
                family = "Comic Sans MS";
                style |= FontStyle.Bold;
            }
            return new Font(family, points * 96f / 72f, style, GraphicsUnit.Pixel);
        }

        /// <summary>Applies comic typography recursively while preserving each control's original font spec.</summary>
        public static void ApplyComicTypography(Control root)
        {
            if (root == null) return;
            var controls = new List<Control>();
            CollectControls(root, controls);
            // Snapshot every original font before replacing any parent font, so inherited child fonts
            // are not accidentally captured after a parent has already been restyled.
            foreach (Control control in controls)
            {
                if (control == null) continue;
                savedFonts.GetValue(control, c => new SavedFont
                {
                    Family = c.Font.Name,
                    Size = c.Font.Size,
                    Style = c.Font.Style,
                    Unit = c.Font.Unit,
                    CharSet = c.Font.GdiCharSet,
                    Vertical = c.Font.GdiVerticalFont,
                    BackColor = c.BackColor,
                    ForeColor = c.ForeColor
                });
            }
            foreach (Control control in controls)
            {
                if (control == null || control.IsDisposed) continue;
                SavedFont original;
                if (!savedFonts.TryGetValue(control, out original)) continue;
                string family = ComicStyleMode ? "Comic Sans MS" : original.Family;
                FontStyle style = ComicStyleMode ? (original.Style | FontStyle.Bold) : original.Style;
                try { control.Font = new Font(family, original.Size, style, original.Unit, original.CharSet, original.Vertical); }
                catch { control.Font = new Font("Comic Sans MS", 9.5f, ComicStyleMode ? FontStyle.Bold : FontStyle.Regular); }
                if (ComicStyleMode && (control is TextBoxBase || control is ComboBox || control is ListBox || control is ListView))
                {
                    control.BackColor = Color.White;
                    control.ForeColor = Color.Black;
                }
                else if (ComicStyleMode && (control is Label || control is LinkLabel))
                {
                    control.BackColor = original.BackColor;
                    control.ForeColor = Color.Black;
                }
                else
                {
                    control.BackColor = original.BackColor;
                    control.ForeColor = original.ForeColor;
                }
            }
        }

        private static void CollectControls(Control root, List<Control> controls)
        {
            controls.Add(root);
            foreach (Control child in root.Controls) CollectControls(child, controls);
        }

        /// <summary>Returns a vivid hue-cycled colour used by animated rainbow labels.</summary>
        public static Color RainbowColor(float hueDegrees)
        {
            double h = ((hueDegrees % 360f) + 360f) % 360f;
            double c = 1.0;
            double x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromArgb(255, (int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        // ------------------------------------------------------------------ maths

        public static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
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

        public static void DrawRainbowSpaced(Graphics g, string text, Font font, float x, float y, float spacing, float phase)
        {
            float cx = x;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch != ' ')
                {
                    using (var b = new SolidBrush(RainbowColor(phase + i * 25f)))
                        g.DrawString(ch.ToString(), font, b, cx, y, StringFormat.GenericTypographic);
                }
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

                int caption = (22 << 16) | (10 << 8) | 8; // Deep navy COLORREF (0x00BBGGRR).
                DwmSetWindowAttribute(form.Handle, 35, ref caption, 4); // DWMWA_CAPTION_COLOR
                int textColor = 0x00FFFFFF;
                DwmSetWindowAttribute(form.Handle, 36, ref textColor, 4); // DWMWA_TEXT_COLOR
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
