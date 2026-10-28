using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace EmiliosSoundboard
{
    internal enum HitKind { None, Add, Settings, Card, Empty, VoiceFx, StopPlayback, Title }

    internal enum CardPart { Body, Play, Delete, Edit }

    internal struct Hit
    {
        public HitKind Kind;
        public Card Card;
        public CardPart Part;

        public bool Same(Hit other)
        {
            return Kind == other.Kind && ReferenceEquals(Card, other.Card) && Part == other.Part;
        }
    }

    /// <summary>The on-screen state of one sound button (positions, animation values, cached artwork).</summary>
    internal sealed class Card : IDisposable
    {
        public SoundItem Item;
        public int Seed;
        public string Ext = "";

        // Layout (logical units). Y is measured inside the scrolling content.
        public float X, Y, W;
        public float TargetX, TargetY, TargetW;
        public bool PosInit;

        // Animation values, 0..1
        public float Appear;          // < 0 = waiting to appear
        public float Hover, Press, PlayHover, DeleteHover, EditHover, Active;
        public float RippleT = 1f, RippleX, RippleY;
        public bool Removing;

        // Cached artwork (physical pixels)
        public Bitmap Cover;
        public int CoverW, CoverH;

        public Card(SoundItem item)
        {
            Item = item;
            Seed = StableHash(item.Id);
            RefreshExt();
        }

        public void RefreshExt()
        {
            string e = Path.GetExtension(Item.AudioFile ?? "");
            Ext = string.IsNullOrEmpty(e) ? "AUDIO" : e.TrimStart('.').ToUpperInvariant();
        }

        public static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                if (s != null)
                    foreach (char c in s) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        public void DropCover()
        {
            if (Cover != null)
            {
                Cover.Dispose();
                Cover = null;
            }
        }

        /// <summary>Builds the rounded card artwork: image (or default space art) plus a dark overlay for readable text.</summary>
        public void BuildCover(string imagePath, int pw, int ph, float scale)
        {
            DropCover();
            pw = Math.Max(8, pw);
            ph = Math.Max(8, ph);

            using (var content = new Bitmap(pw, ph, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(content))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    var r = new RectangleF(0, 0, pw, ph);

                    bool drawn = false;
                    if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                    {
                        try
                        {
                            using (var src = ImageUtil.LoadCopy(imagePath))
                                ImageUtil.DrawCover(g, src, r);
                            drawn = true;
                        }
                        catch
                        {
                            drawn = false;
                        }
                    }
                    if (!drawn) ImageUtil.DrawDefaultCard(g, r, Seed);

                    // Darken custom artwork substantially so embedded image text and shapes do not
                    // compete with the app's own title, metadata and controls.
                    using (var shade = new LinearGradientBrush(r, Color.FromArgb(115, 2, 3, 9), Color.FromArgb(232, 2, 3, 9), 90f))
                        g.FillRectangle(shade, r);
                    var bottom = new RectangleF(0f, ph * 0.42f, pw, ph * 0.58f);
                    using (var lowerShade = new LinearGradientBrush(bottom, Color.FromArgb(35, 0, 0, 0), Color.FromArgb(180, 0, 0, 0), 90f))
                        g.FillRectangle(lowerShade, bottom);
                }

                var cover = new Bitmap(pw, ph, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(cover))
                using (var tex = new TextureBrush(content))
                using (var path = Theme.RoundedRect(new RectangleF(0, 0, pw, ph), 18f * scale))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    g.FillPath(tex, path);
                }

                Cover = cover;
                CoverW = pw;
                CoverH = ph;
            }
        }

        public void Dispose()
        {
            DropCover();
        }
    }
}
