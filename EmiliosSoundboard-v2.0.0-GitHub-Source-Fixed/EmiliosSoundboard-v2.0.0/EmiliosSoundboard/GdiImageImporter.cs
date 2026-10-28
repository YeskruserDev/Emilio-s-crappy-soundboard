using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EmiliosSoundboard
{
    /// <summary>
    /// Windows implementation of the background-image pipeline: reads the file without keeping it
    /// locked, applies the EXIF orientation, shrinks oversized photos and stores a PNG copy.
    /// </summary>
    internal sealed class GdiImageImporter : IImageImporter
    {
        /// <summary>Longest edge of a stored background; keeps the library folder small.</summary>
        private const int MaxSide = 1400;

        public string Import(string sourcePath, string imagesDir)
        {
            using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var src = Image.FromStream(fs, false, true))
            {
                ApplyExifOrientation(src);

                float scale = Math.Min(1f, MaxSide / (float)Math.Max(src.Width, src.Height));
                int w = Math.Max(1, (int)(src.Width * scale));
                int h = Math.Max(1, (int)(src.Height * scale));

                using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, w, h);
                    }

                    string fileName = DataStore.NewImageName(".png");
                    bmp.Save(Path.Combine(imagesDir, fileName), ImageFormat.Png);
                    return fileName;
                }
            }
        }

        private static void ApplyExifOrientation(Image img)
        {
            try
            {
                if (Array.IndexOf(img.PropertyIdList, 0x0112) < 0) return;
                var prop = img.GetPropertyItem(0x0112);
                int orientation = BitConverter.ToUInt16(prop.Value, 0);
                RotateFlipType t;
                switch (orientation)
                {
                    case 2: t = RotateFlipType.RotateNoneFlipX; break;
                    case 3: t = RotateFlipType.Rotate180FlipNone; break;
                    case 4: t = RotateFlipType.Rotate180FlipX; break;
                    case 5: t = RotateFlipType.Rotate90FlipX; break;
                    case 6: t = RotateFlipType.Rotate90FlipNone; break;
                    case 7: t = RotateFlipType.Rotate270FlipX; break;
                    case 8: t = RotateFlipType.Rotate270FlipNone; break;
                    default: return;
                }
                img.RotateFlip(t);
            }
            catch
            {
                // Orientation is cosmetic; ignore problems.
            }
        }
    }
}
