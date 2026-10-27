using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>Small live preview of how the sound card will look.</summary>
    internal sealed class CoverPreview : Control
    {
        private Bitmap image;
        private string text = "";

        public string PreviewText
        {
            get { return text; }
            set { text = value ?? ""; Invalidate(); }
        }

        public CoverPreview()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void SetImage(Bitmap bmp)
        {
            if (image != null) image.Dispose();
            image = bmp;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Theme.DeepNavy);

            var r = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = Theme.RoundedRect(r, 16f))
            {
                var oldClip = g.Clip;
                g.SetClip(path, CombineMode.Intersect);

                if (image != null) ImageUtil.DrawCover(g, image, r);
                else ImageUtil.DrawDefaultCard(g, r, 7);

                using (var shade = new LinearGradientBrush(r, Color.FromArgb(60, 0, 0, 0), Color.FromArgb(200, 2, 4, 12), 90f))
                    g.FillRectangle(shade, r);
                g.Clip = oldClip;

                using (var pen = new Pen(Color.FromArgb(110, Theme.Blue), 1.2f))
                    g.DrawPath(pen, path);
            }

            float cr = Math.Min(22f, r.Height * 0.22f);
            var play = new RectangleF(r.Right - cr * 2f - 14f, r.Bottom - cr * 2f - 14f, cr * 2f, cr * 2f);
            using (var br = new LinearGradientBrush(play, Theme.Blue, Theme.Violet, 45f))
                g.FillEllipse(br, play);
            float cx = play.X + play.Width / 2f + 2f, cy = play.Y + play.Height / 2f, t = cr * 0.42f;
            using (var white = new SolidBrush(Color.White))
                g.FillPolygon(white, new[] { new PointF(cx - t * 0.8f, cy - t), new PointF(cx + t, cy), new PointF(cx - t * 0.8f, cy + t) });

            using (var font = Theme.MakeFont(12f, FontStyle.Bold))
            using (var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
            using (var white = new SolidBrush(Color.White))
                g.DrawString(string.IsNullOrWhiteSpace(text) ? "Sound name" : text, font, white,
                    new RectangleF(r.X + 16f, play.Y, play.X - r.X - 24f, play.Height), fmt);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && image != null)
            {
                image.Dispose();
                image = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>Asks for a sound's name and background. Used both when adding and when editing.</summary>
    internal sealed class SoundDetailsDialog : ThemedForm
    {
        private readonly DataStore store;
        private readonly SoundItem editing;
        private readonly TextBox nameBox;
        private readonly Label hint;
        private readonly CoverPreview preview;
        private string chosenImage;
        private bool revertImage;

        public string SoundName { get; private set; }

        /// <summary>Full path of a newly chosen image, or null.</summary>
        public string ChosenImagePath { get { return chosenImage; } }

        /// <summary>True if the user asked to go back to the default background (edit mode).</summary>
        public bool RevertImage { get { return revertImage; } }

        public SoundDetailsDialog(DataStore store, string headline, string subtitle, string initialName,
                                  string existingImagePath, bool allowSkip, string primaryText, SoundItem editing)
        {
            this.store = store;
            this.editing = editing;

            Text = headline;
            ClientSize = new Size(S(540), S(allowSkip ? 556 : 540));

            int left = S(30);
            int width = ClientSize.Width - left * 2;
            int y = S(24);

            var title = MakeLabel(headline, 16f, FontStyle.Bold, Theme.CoolWhite);
            title.SetBounds(left, y, width, S(32));
            Controls.Add(title);
            y += S(34);

            var sub = MakeLabel(subtitle, 9f, FontStyle.Regular, Theme.Muted);
            sub.SetBounds(left, y, width, S(22));
            Controls.Add(sub);
            y += S(36);

            var nameLabel = MakeLabel("BUTTON NAME", 8.5f, FontStyle.Bold, Theme.Blue);
            nameLabel.SetBounds(left, y, width, S(20));
            Controls.Add(nameLabel);
            y += S(22);

            nameBox = MakeTextBox();
            nameBox.SetBounds(left, y, width, S(32));
            nameBox.MaxLength = 60;
            nameBox.Text = initialName ?? "";
            Controls.Add(nameBox);
            y += S(36);

            hint = MakeLabel("", 8.5f, FontStyle.Regular, Theme.Muted);
            hint.SetBounds(left, y, width, S(20));
            Controls.Add(hint);
            y += S(30);

            var bgLabel = MakeLabel("BUTTON BACKGROUND", 8.5f, FontStyle.Bold, Theme.Blue);
            bgLabel.SetBounds(left, y, width, S(20));
            Controls.Add(bgLabel);
            y += S(24);

            preview = new CoverPreview();
            preview.SetBounds(left, y, width, S(150));
            Controls.Add(preview);
            y += S(160);

            if (!string.IsNullOrEmpty(existingImagePath) && File.Exists(existingImagePath))
            {
                try { preview.SetImage(ImageUtil.LoadCopy(existingImagePath)); }
                catch { /* shows the default look instead */ }
            }

            var chooseBtn = new GlowButton { Text = "CHOOSE IMAGE...", Style = GlowStyle.Secondary };
            chooseBtn.SetBounds(left - S(5), y, S(170), S(44));
            chooseBtn.Click += (s, e) => ChooseImage();
            Controls.Add(chooseBtn);

            var defaultBtn = new GlowButton { Text = "USE DEFAULT", Style = GlowStyle.Secondary };
            defaultBtn.SetBounds(left + S(170), y, S(150), S(44));
            defaultBtn.Click += (s, e) => UseDefault();
            Controls.Add(defaultBtn);
            y += S(66);

            int btnY = ClientSize.Height - S(70);

            var okBtn = new GlowButton { Text = primaryText, Style = GlowStyle.Primary };
            okBtn.SetBounds(ClientSize.Width - left - S(170) + S(5), btnY, S(175), S(46));
            okBtn.Click += (s, e) => Confirm();
            Controls.Add(okBtn);

            var cancelBtn = new GlowButton { Text = allowSkip ? "CANCEL ALL" : "CANCEL", Style = GlowStyle.Secondary };
            cancelBtn.SetBounds(left - S(5), btnY, S(140), S(46));
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            Controls.Add(cancelBtn);

            if (allowSkip)
            {
                var skipBtn = new GlowButton { Text = "SKIP", Style = GlowStyle.Secondary };
                skipBtn.SetBounds(left + S(140), btnY, S(110), S(46));
                skipBtn.Click += (s, e) => { DialogResult = DialogResult.Ignore; };
                Controls.Add(skipBtn);
            }

            nameBox.TextChanged += (s, e) => OnNameChanged();
            Shown += (s, e) => { nameBox.Focus(); nameBox.SelectAll(); };
            OnNameChanged();
        }

        private void OnNameChanged()
        {
            string typed = nameBox.Text.Trim();
            preview.PreviewText = typed;

            if (typed.Length == 0)
            {
                hint.ForeColor = Theme.Muted;
                hint.Text = "Type the name that will be shown on the button.";
                return;
            }

            string unique = store.UniqueName(typed, editing);
            if (!string.Equals(unique, typed, StringComparison.Ordinal))
            {
                hint.ForeColor = Color.FromArgb(255, 200, 110);
                hint.Text = "That name is already used - it will be saved as \"" + unique + "\".";
            }
            else
            {
                hint.ForeColor = Theme.Muted;
                hint.Text = "";
            }
        }

        private void ChooseImage()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Choose a background image for this button";
                ofd.Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files (*.*)|*.*";
                ofd.CheckFileExists = true;
                if (ofd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var bmp = ImageUtil.LoadCopy(ofd.FileName);
                    preview.SetImage(bmp);
                    chosenImage = ofd.FileName;
                    revertImage = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this,
                        "That file could not be opened as an image.\n\nPlease choose a PNG, JPG or BMP file.\n\n(" + ex.Message + ")",
                        "Image problem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void UseDefault()
        {
            preview.SetImage(null);
            chosenImage = null;
            revertImage = true;
        }

        private void Confirm()
        {
            string typed = nameBox.Text.Trim();
            if (typed.Length == 0)
            {
                hint.ForeColor = Color.FromArgb(255, 120, 130);
                hint.Text = "Please type a name first.";
                nameBox.Focus();
                return;
            }

            SoundName = typed;
            DialogResult = DialogResult.OK;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Enter && !(ActiveControl is GlowButton))
            {
                Confirm();
                return true;
            }
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
