using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>
    /// The static part of the backdrop: the base gradient, an optional user photo and the drifting
    /// haze blobs. Stars, the cursor light and the rainbow road stay live.
    /// </summary>
    /// <remarks>
    /// This exists because of fullscreen cost. The haze alone was four oversized, bilinear-filtered,
    /// alpha-blended blits (about 22 megapixels at 1080p) on every single frame, which is what made
    /// the window laggy when maximised. The haze drifts at roughly 0.02 rad/s, so it is rendered
    /// into a cached bitmap a few times a second and the cache is blitted 1:1 in between.
    /// </remarks>
    internal sealed partial class MainForm : Form
    {
        private Bitmap backdropCache;
        private int backdropW, backdropH;
        private int lastPaintW, lastPaintH;
        private float backdropTime = float.MinValue;
        private bool backdropBroken;

        /// <summary>How stale the cached backdrop may get before it is re-rendered.</summary>
        private const float BackdropRefreshSeconds = 0.12f;

        private void InvalidateBackdrop()
        {
            if (backdropCache != null)
            {
                backdropCache.Dispose();
                backdropCache = null;
            }
            backdropTime = float.MinValue;
        }

        /// <summary>
        /// Makes sure the cached backdrop matches the current size and is recent enough.
        /// Returns false when no cache could be built, in which case the caller paints directly.
        /// </summary>
        private bool EnsureBackdrop(float w, float h)
        {
            if (backdropBroken) return false;

            int pw = Math.Max(1, (int)Math.Round(w * scale));
            int ph = Math.Max(1, (int)Math.Round(h * scale));

            // While the window is being dragged to a new size, drawing straight to the screen is
            // cheaper than allocating and filling a fresh fullscreen bitmap every frame.
            if (lastPaintW != pw || lastPaintH != ph)
            {
                bool first = lastPaintW == 0 && lastPaintH == 0;
                lastPaintW = pw;
                lastPaintH = ph;
                InvalidateBackdrop();
                if (!first) return false;
            }

            if (backdropCache != null && backdropW == pw && backdropH == ph &&
                Math.Abs(hazeTime - backdropTime) < BackdropRefreshSeconds)
                return true;

            InvalidateBackdrop();

            Bitmap bmp = null;
            try
            {
                bmp = new Bitmap(pw, ph, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.ScaleTransform(scale, scale);
                    PaintBackdrop(g, w, h);
                }
            }
            catch
            {
                // Out of memory or a broken driver surface: stop caching and paint directly.
                if (bmp != null) bmp.Dispose();
                backdropBroken = true;
                backdropCache = null;
                return false;
            }

            backdropCache = bmp;
            backdropW = pw;
            backdropH = ph;
            backdropTime = hazeTime;
            return true;
        }

        /// <summary>Draws the static backdrop into any graphics context (cache or screen).</summary>
        private void PaintBackdrop(Graphics g, float w, float h)
        {
            // Comic Book Mode switches to pure monochrome halftone paper.
            if (store.Data.Settings.ComicStyleMode)
            {
                using (var paper = new LinearGradientBrush(new RectangleF(-1, -1, w + 2, h + 2), Color.White, Color.FromArgb(224, 224, 224), 90f))
                    g.FillRectangle(paper, -1, -1, w + 2, h + 2);
                DrawComicOverlay(g, w, h, hazeTime);
                return;
            }

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
        }

        /// <summary>Paints gradient, user photo and haze: cached blit when possible.</summary>
        private void DrawBackdrop(Graphics g, float w, float h)
        {
            if (EnsureBackdrop(w, h))
                g.DrawImage(backdropCache, 0f, 0f, w, h);
            else
                PaintBackdrop(g, w, h);
        }
    }
}
