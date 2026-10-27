using System;
using System.Drawing;
using System.Windows.Forms;

namespace EmiliosSoundboard
{
    /// <summary>A dark, themed replacement for MessageBox (information, errors and yes/no questions).</summary>
    internal sealed class MessageDialog : ThemedForm
    {
        private bool primaryIsDanger;

        private MessageDialog(string title, string message, string okText, string cancelText, bool danger)
        {
            primaryIsDanger = danger;
            Text = "Emilio's Soundboard";

            int left = S(30);
            int width = S(460);
            int y = S(26);

            var titleLabel = MakeLabel(title, 14f, FontStyle.Bold, danger ? Color.FromArgb(255, 130, 140) : Theme.CoolWhite);
            titleLabel.SetBounds(left, y, width, S(30));
            Controls.Add(titleLabel);
            y += S(42);

            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
            Size measured = TextRenderer.MeasureText(message, Theme.MakeFont(10f), new Size(width, 0), flags);
            int textHeight = Math.Min(S(300), measured.Height + S(6));

            var body = MakeLabel(message, 10f, FontStyle.Regular, Color.FromArgb(205, 216, 245));
            body.SetBounds(left, y, width, textHeight);
            Controls.Add(body);
            y += textHeight + S(26);

            ClientSize = new Size(S(520), y + S(46) + S(26));

            int btnY = y;
            var ok = new GlowButton { Text = okText, Style = danger ? GlowStyle.Danger : GlowStyle.Primary };
            ok.SetBounds(ClientSize.Width - left - S(150) + S(5), btnY, S(155), S(46));
            ok.Click += (s, e) => { DialogResult = DialogResult.OK; };
            Controls.Add(ok);

            if (cancelText != null)
            {
                var cancel = new GlowButton { Text = cancelText, Style = GlowStyle.Secondary };
                cancel.SetBounds(ClientSize.Width - left - S(150) - S(150) + S(5), btnY, S(150), S(46));
                cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
                Controls.Add(cancel);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                return true;
            }
            if (keyData == Keys.Enter && !primaryIsDanger && !(ActiveControl is GlowButton))
            {
                DialogResult = DialogResult.OK;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Shows the dialog. Returns true when the main (right-hand) button was pressed.</summary>
        public static bool Show(IWin32Window owner, string title, string message,
                                string okText = "OK", string cancelText = null, bool danger = false)
        {
            using (var dlg = new MessageDialog(title, message, okText, cancelText, danger))
            {
                return dlg.ShowDialog(owner) == DialogResult.OK;
            }
        }
    }
}
