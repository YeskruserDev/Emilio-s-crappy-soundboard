using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>A lightweight HSV colour-wheel picker used for the custom accent setting.</summary>
    internal sealed class ColorWheelDialog : Form
    {
        private readonly ColorWheelPicker wheel;
        private readonly TrackBar brightness;
        private readonly Panel preview;
        private readonly Label hexLabel;

        public Color SelectedColor { get; private set; }

        public ColorWheelDialog(Color initial)
        {
            Text = "Choose Accent Colour";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Black;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(450, 352);

            wheel = new ColorWheelPicker { Location = new Point(14, 14), Size = new Size(300, 300), SelectedColor = initial };
            Controls.Add(wheel);

            var brightnessLabel = new Label { Text = "BRIGHTNESS", Location = new Point(324, 28), Size = new Size(112, 22), ForeColor = Theme.CoolWhite, Font = new Font("Segoe UI", 8f, FontStyle.Bold) };
            Controls.Add(brightnessLabel);
            brightness = new TrackBar { Orientation = Orientation.Vertical, Minimum = 25, Maximum = 100, TickFrequency = 15, Value = 85, Location = new Point(346, 50), Size = new Size(50, 180), BackColor = Theme.Black };
            Controls.Add(brightness);

            preview = new Panel { Location = new Point(324, 238), Size = new Size(112, 34), BackColor = initial, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(preview);
            hexLabel = new Label { Location = new Point(324, 276), Size = new Size(112, 22), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.CoolWhite };
            Controls.Add(hexLabel);

            var cancel = new GlowButton { Text = "CANCEL", Style = GlowStyle.Secondary, Location = new Point(236, 310), Size = new Size(94, 34) };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cancel);
            var apply = new GlowButton { Text = "USE COLOUR", Style = GlowStyle.Primary, Location = new Point(334, 310), Size = new Size(104, 34) };
            apply.Click += (s, e) => { SelectedColor = wheel.SelectedColor; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(apply);

            wheel.ColorChanged += (s, e) => UpdatePreview();
            brightness.ValueChanged += (s, e) => { wheel.Brightness = brightness.Value / 100f; UpdatePreview(); };
            wheel.Brightness = brightness.Value / 100f;
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            SelectedColor = wheel.SelectedColor;
            preview.BackColor = SelectedColor;
            hexLabel.Text = ColorTranslator.ToHtml(SelectedColor).ToUpperInvariant();
        }
    }

    internal sealed class ColorWheelPicker : Control
    {
        private Bitmap wheelBitmap;
        private float hue = 220f;
        private float saturation = 0.72f;
        private float brightness = 0.85f;

        public event EventHandler ColorChanged;
        public float Brightness
        {
            get { return brightness; }
            set { brightness = Math.Max(0.25f, Math.Min(1f, value)); Invalidate(); ColorChanged?.Invoke(this, EventArgs.Empty); }
        }

        public Color SelectedColor
        {
            get { return FromHsv(hue, saturation, brightness); }
            set
            {
                ToHsv(value, out hue, out saturation, out float v);
                brightness = Math.Max(0.25f, Math.Min(1f, v));
                Invalidate();
            }
        }

        public ColorWheelPicker()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Black;
            Cursor = Cursors.Hand;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            EnsureWheelBitmap();
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawImageUnscaled(wheelBitmap, 0, 0);

            float cx = Width / 2f, cy = Height / 2f;
            float radius = Math.Min(Width, Height) / 2f - 8f;
            using (var darken = new SolidBrush(Color.FromArgb((int)(255f * (1f - brightness)), 0, 0, 0)))
                g.FillEllipse(darken, cx - radius, cy - radius, radius * 2f, radius * 2f);

            double angle = hue * Math.PI / 180.0;
            float markerRadius = saturation * radius;
            float mx = cx + (float)Math.Cos(angle) * markerRadius;
            float my = cy + (float)Math.Sin(angle) * markerRadius;
            using (var outer = new Pen(Color.FromArgb(230, 0, 0, 0), 4f)) g.DrawEllipse(outer, mx - 8f, my - 8f, 16f, 16f);
            using (var inner = new Pen(Color.White, 2f)) g.DrawEllipse(inner, mx - 8f, my - 8f, 16f, 16f);
            using (var brush = new SolidBrush(SelectedColor)) g.FillEllipse(brush, mx - 4f, my - 4f, 8f, 8f);
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Pick(e.Location); }
        protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) Pick(e.Location); base.OnMouseMove(e); }

        private void Pick(Point point)
        {
            float cx = Width / 2f, cy = Height / 2f;
            float dx = point.X - cx, dy = point.Y - cy;
            float radius = Math.Min(Width, Height) / 2f - 8f;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance > radius) return;
            hue = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
            if (hue < 0f) hue += 360f;
            saturation = Math.Max(0f, Math.Min(1f, distance / radius));
            Invalidate();
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureWheelBitmap()
        {
            if (wheelBitmap != null && wheelBitmap.Width == Width && wheelBitmap.Height == Height) return;
            if (wheelBitmap != null) { wheelBitmap.Dispose(); wheelBitmap = null; }
            wheelBitmap = new Bitmap(Math.Max(1, Width), Math.Max(1, Height), PixelFormat.Format32bppArgb);
            float cx = Width / 2f, cy = Height / 2f;
            float radius = Math.Min(Width, Height) / 2f - 8f;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float dx = x - cx, dy = y - cy;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                if (distance > radius) { wheelBitmap.SetPixel(x, y, Color.Transparent); continue; }
                float h = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                if (h < 0f) h += 360f;
                float s = Math.Max(0f, Math.Min(1f, distance / radius));
                wheelBitmap.SetPixel(x, y, FromHsv(h, s, 1f));
            }
        }

        private static Color FromHsv(float hue, float saturation, float value)
        {
            hue = (hue % 360f + 360f) % 360f;
            saturation = Math.Max(0f, Math.Min(1f, saturation));
            value = Math.Max(0f, Math.Min(1f, value));
            float chroma = value * saturation;
            float x = chroma * (1f - Math.Abs((hue / 60f) % 2f - 1f));
            float m = value - chroma;
            float r = 0, g = 0, b = 0;
            if (hue < 60f) { r = chroma; g = x; }
            else if (hue < 120f) { r = x; g = chroma; }
            else if (hue < 180f) { g = chroma; b = x; }
            else if (hue < 240f) { g = x; b = chroma; }
            else if (hue < 300f) { r = x; b = chroma; }
            else { r = chroma; b = x; }
            return Color.FromArgb(255, (int)((r + m) * 255f), (int)((g + m) * 255f), (int)((b + m) * 255f));
        }

        private static void ToHsv(Color color, out float hue, out float saturation, out float value)
        {
            float r = color.R / 255f, g = color.G / 255f, b = color.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;
            if (delta < 0.0001f) hue = 0f;
            else if (max == r) hue = 60f * (((g - b) / delta) % 6f);
            else if (max == g) hue = 60f * (((b - r) / delta) + 2f);
            else hue = 60f * (((r - g) / delta) + 4f);
            if (hue < 0f) hue += 360f;
            saturation = max <= 0f ? 0f : delta / max;
            value = max;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && wheelBitmap != null) { wheelBitmap.Dispose(); wheelBitmap = null; }
            base.Dispose(disposing);
        }
    }
}
