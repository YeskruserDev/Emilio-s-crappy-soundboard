using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace EmiliosSoundboard
{
    /// <summary>Hit testing and mouse/keyboard handling.</summary>
    internal sealed partial class MainForm : Form
    {
        // ------------------------------------------------------------------ hit testing and input

        private RectangleF AddRect()
        {
            return new RectangleF(22f, LH - 56f, 116f, 30f);
        }

        private PointF SettingsCenter()
        {
            return new PointF(LW - 46f, 44f);
        }

        private RectangleF EmptyRect()
        {
            float w = Math.Min(520f, LW - 48f);
            float h = 190f;
            float areaH = GridBottom - GridTop;
            return new RectangleF((LW - w) / 2f, GridTop + Math.Max(10f, (areaH - h) / 2f - 10f), w, h);
        }

        private static readonly RectangleF EditRect = new RectangleF(12f, 10f, 52f, 24f);

        private static float Dist(float x1, float y1, float x2, float y2)
        {
            float dx = x1 - x2, dy = y1 - y2;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private RectangleF StopPlaybackRect()
        {
            // Reserve a wider hit target so STOP SOUND never clips at common Windows DPI scales.
            return new RectangleF(Math.Max(8f, LW - 184f), LH - 94f, 164f, 26f);
        }

        private RectangleF VoiceFxRect()
        {
            float width = Math.Min(150f, Math.Max(108f, LW * 0.16f));
            return new RectangleF(LW - width - 74f, 20f, width, 44f);
        }

        private Hit HitTest(float x, float y)
        {
            var h = new Hit();
            if (AddRect().Contains(x, y)) { h.Kind = HitKind.Add; return h; }
            if (VoiceFxRect().Contains(x, y)) { h.Kind = HitKind.VoiceFx; return h; }
            if (StopPlaybackRect().Contains(x, y)) { h.Kind = HitKind.StopPlayback; return h; }
            var sc = SettingsCenter();
            if (Dist(x, y, sc.X, sc.Y) <= 24f) { h.Kind = HitKind.Settings; return h; }

            if (y >= GridTop && y <= GridBottom)
            {
                var ordered = OrderedVisibleCards().ToList();
                for (int i = ordered.Count - 1; i >= 0; i--)
                {
                    Card c = ordered[i];
                    if (c.Removing || c.Appear < 0.3f) continue;
                    float sy = GridTop + c.Y - scrollY;
                    if (x >= c.X && x <= c.X + c.W && y >= sy && y <= sy + CardH)
                    {
                        float lx = x - c.X, ly = y - sy;
                        h.Kind = HitKind.Card;
                        h.Card = c;
                        if (Dist(lx, ly, c.W - 20f, 20f) <= 15f) h.Part = CardPart.Delete;
                        else if (EditRect.Contains(lx, ly)) h.Part = CardPart.Edit;
                        else if (Dist(lx, ly, c.W - 44f, CardH - 44f) <= 29f) h.Part = CardPart.Play;
                        else h.Part = CardPart.Body;
                        return h;
                    }
                }

                if (VisibleCount() == 0 && EmptyRect().Contains(x, y)) h.Kind = HitKind.Empty;
            }
            return h;
        }

        private void UpdateHover()
        {
            hover = hasMouse ? HitTest(mx, my) : new Hit();
            Cursor = hover.Kind == HitKind.None ? Cursors.Default : Cursors.Hand;
            forceFrame = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            mx = e.X / scale;
            my = e.Y / scale;
            hasMouse = true;
            UpdateHover();
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hasMouse = false;
            hover = new Hit();
            forceFrame = true;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            mx = e.X / scale;
            my = e.Y / scale;
            hasMouse = true;
            if (e.Button == MouseButtons.Left)
            {
                pressed = HitTest(mx, my);
                pressing = pressed.Kind != HitKind.None;
            }
            forceFrame = true;
            base.OnMouseDown(e);
        }

        private bool IsGitHubLinkPoint(float x, float y)
        {
            // Match the link's painted location in logical coordinates, independent of DPI scaling.
            return x >= 14f && x <= 245f && y >= 27f && y <= 47f;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            mx = e.X / scale;
            my = e.Y / scale;
            Hit up = HitTest(mx, my);
            bool same = pressing && up.Same(pressed);
            pressing = false;
            forceFrame = true;

            if (e.Button == MouseButtons.Left && IsGitHubLinkPoint(mx, my))
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/emilspaskevics1-max") { UseShellExecute = true }); } catch { }
            }
            else if (e.Button == MouseButtons.Right && up.Kind == HitKind.Card)
                ShowCardMenu(up.Card, e.Location);
            else if (e.Button == MouseButtons.Left && same)
                ActivateHit(up);

            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            scrollTarget = Math.Max(0f, Math.Min(maxScroll, scrollTarget - e.Delta / 120f * 100f));
            scrollFade = 1.4f;
            forceFrame = true;
            base.OnMouseWheel(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            float page = Math.Max(100f, (GridBottom - GridTop) * 0.9f);
            switch (e.KeyCode)
            {
                case Keys.PageDown: scrollTarget = Math.Min(maxScroll, scrollTarget + page); break;
                case Keys.PageUp: scrollTarget = Math.Max(0f, scrollTarget - page); break;
                case Keys.Home: scrollTarget = 0f; break;
                case Keys.End: scrollTarget = maxScroll; break;
                case Keys.Down: scrollTarget = Math.Min(maxScroll, scrollTarget + 60f); break;
                case Keys.Up: scrollTarget = Math.Max(0f, scrollTarget - 60f); break;
                default:
                    base.OnKeyDown(e);
                    return;
            }
            scrollFade = 1.4f;
            forceFrame = true;
            e.Handled = true;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            forceFrame = true;
            Invalidate();
        }

        private void ActivateHit(Hit hit)
        {
            switch (hit.Kind)
            {
                case HitKind.Add:
                case HitKind.Empty:
                    AddSounds();
                    break;
                case HitKind.Settings:
                    ShowSettings();
                    break;
                case HitKind.VoiceFx:
                    using (var voice = new VoiceFxForm()) voice.ShowDialog(this);
                    forceFrame = true;
                    break;
                case HitKind.StopPlayback:
                    player.Stop();
                    state = PlayState.Ready;
                    lastPos = TimeSpan.Zero; lastDur = TimeSpan.Zero; progressShown = 0f;
                    currentName = string.Empty;
                    forceFrame = true;
                    break;
                case HitKind.Card:
                    if (hit.Part == CardPart.Delete) DeleteCard(hit.Card);
                    else if (hit.Part == CardPart.Edit) ShowCardMenu(hit.Card, PointToClient(Cursor.Position));
                    else
                    {
                        float sy = GridTop + hit.Card.Y - scrollY;
                        hit.Card.RippleT = 0f;
                        hit.Card.RippleX = mx - hit.Card.X;
                        hit.Card.RippleY = my - sy;
                        PlayCard(hit.Card);
                    }
                    break;
            }
        }
    }
}
