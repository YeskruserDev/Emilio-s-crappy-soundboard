using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EmiliosSoundboard
{
    internal static class ImageUtil
    {
        /// <summary>Loads an image into memory and releases the file immediately (no lingering file lock).</summary>
        public static Bitmap LoadCopy(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var img = Image.FromStream(fs, false, true))
            {
                return new Bitmap(img);
            }
        }

        /// <summary>Draws the image so it completely fills dest, cropping the overflow around the centre.</summary>
        public static void DrawCover(Graphics g, Image img, RectangleF dest)
        {
            float destAspect = dest.Width / Math.Max(1f, dest.Height);
            float imgAspect = img.Width / (float)Math.Max(1, img.Height);
            RectangleF src;
            if (imgAspect > destAspect)
            {
                float sw = img.Height * destAspect;
                src = new RectangleF((img.Width - sw) / 2f, 0, sw, img.Height);
            }
            else
            {
                float sh = img.Width / destAspect;
                src = new RectangleF(0, (img.Height - sh) / 2f, img.Width, sh);
            }
            g.DrawImage(img, dest, src, GraphicsUnit.Pixel);
        }

        /// <summary>The default space-themed card background (unique star pattern per seed).</summary>
        public static void DrawDefaultCard(Graphics g, RectangleF r, int seed)
        {
            using (var br = new LinearGradientBrush(r, Color.FromArgb(10, 16, 42), Color.FromArgb(32, 18, 68), 35f))
                g.FillRectangle(br, r);

            var rng = new Random(seed);
            using (var dot = new SolidBrush(Color.White))
            {
                for (int i = 0; i < 26; i++)
                {
                    float x = r.X + (float)rng.NextDouble() * r.Width;
                    float y = r.Y + (float)rng.NextDouble() * r.Height;
                    float s = 0.8f + (float)rng.NextDouble() * 1.6f;
                    int a = 50 + rng.Next(170);
                    dot.Color = rng.Next(3) == 0 ? Color.FromArgb(a, 170, 150, 255) : Color.FromArgb(a, 190, 215, 255);
                    g.FillEllipse(dot, x, y, s, s);
                }
            }

            // A faint, thin orbit line to give the default card a little character.
            using (var pen = new Pen(Color.FromArgb(22, 120, 150, 255), 1f))
            {
                float w = r.Width * 1.6f;
                float h = r.Height * 1.4f;
                g.DrawEllipse(pen, r.X - w * 0.25f, r.Y + r.Height * 0.55f, w, h);
            }
        }
    }
}
