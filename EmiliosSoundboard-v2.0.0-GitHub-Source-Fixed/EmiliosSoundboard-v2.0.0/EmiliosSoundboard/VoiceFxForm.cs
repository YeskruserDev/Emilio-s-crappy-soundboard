using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace EmiliosSoundboard
{
    /// <summary>Live microphone monitor with realtime pitch shifting and locally saved presets.</summary>
    internal sealed class VoiceFxForm : Form
    {
        private ComboBox devices, outputs, presets;
        private AccentPitchTrackBar pitch;
        private TrackBar wet;
        private Label pitchValue, status;
        private Button start, save, delete;
        private WaveInEvent capture;
        private WaveOutEvent output;
        private BufferedWaveProvider buffer;
        private SmbPitchShiftingSampleProvider shifter;
        private readonly List<VoicePreset> saved = new List<VoicePreset>();
        private string PresetPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EmiliosSoundboard", "voice-presets.xml"); } }

        public VoiceFxForm()
        {
            Text = "Emilio's Soundboard — VOICE FX"; StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 590); MinimumSize = new Size(680, 520); BackColor = Theme.Black; ForeColor = Theme.CoolWhite;
            Font = new Font("Comic Sans MS", 10f); BuildUi(); LoadPresets(); RefreshPresets(); LoadDevices();
            FormClosing += (s,e) => StopAudio();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(28), BackColor = BackColor };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);
            var title = new Label { Text = "VOICE FX", Dock = DockStyle.Fill, Font = new Font("Comic Sans MS", 26, FontStyle.Bold), ForeColor = Color.White, TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(title,0,0);
            var deviceRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = BackColor }; deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            devices = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.PanelFill, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            outputs = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.PanelFill, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            deviceRow.Controls.Add(Labeled("MICROPHONE INPUT", devices),0,0); deviceRow.Controls.Add(Labeled("OUTPUT DEVICE (VIRTUAL CABLE FOR CHAT)", outputs),1,0); root.Controls.Add(deviceRow,0,1);
            var pitchPanel = new Panel { Dock = DockStyle.Fill };
            var pitchTitle = new Label { Text = "PITCH", ForeColor = Theme.Muted, Location = new Point(0,4), AutoSize = true, Font = new Font(Font,FontStyle.Bold) };
            pitchValue = new Label { Text = "0 semitones", ForeColor = Color.White, Dock = DockStyle.Right, Width = 140, TextAlign = ContentAlignment.TopRight };
            pitch = new AccentPitchTrackBar { Minimum = -12, Maximum = 12, Value = 0, TickFrequency = 3, Dock = DockStyle.Bottom, Height = 58, BackColor = BackColor, Accent = Theme.Blue };
            pitch.ValueChanged += (s,e) => { pitchValue.Text = pitch.Value + " semitones"; if (shifter != null) shifter.PitchFactor = (float)Math.Pow(2.0, pitch.Value / 12.0); };
            pitchPanel.Controls.Add(pitch); pitchPanel.Controls.Add(pitchTitle); pitchPanel.Controls.Add(pitchValue); root.Controls.Add(pitchPanel,0,2);
            var presetsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = BackColor, Padding = new Padding(0,12,0,0) };
            presets = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.PanelFill, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            presets.SelectedIndexChanged += (s,e) => ApplyPreset();
            presetsPanel.Controls.Add(presets); presetsPanel.Controls.Add(MakeButton("DEEP", () => ApplyQuick("Deep", -5))); presetsPanel.Controls.Add(MakeButton("HIGH", () => ApplyQuick("High", 5))); presetsPanel.Controls.Add(MakeButton("ALIEN", () => ApplyQuick("Alien", 9)));
            root.Controls.Add(Labeled("VOICE PRESETS", presetsPanel),0,3);
            var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0,12,0,0) };
            start = MakeButton("START LIVE VOICE", StartAudio); start.Width = 220; start.Height = 44; start.Font = new Font("Comic Sans MS", 10f, FontStyle.Bold); controls.Controls.Add(start);
            controls.Controls.Add(MakeButton("SAVE PRESET", SavePreset)); delete = MakeButton("DELETE PRESET", DeletePreset); controls.Controls.Add(delete);
            root.Controls.Add(controls,0,4);
            var note = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Muted, Text = "Live mic monitor with pitch shifting. Use headphones to avoid feedback. Select the app's output/virtual cable as your chat microphone route if you want others to hear it.", TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0,8,0,0) };
            root.Controls.Add(note,0,5);
            status = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Blue, Text = "●  VOICE FX OFF", TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(status,0,6);
        }

        private Control Labeled(string label, Control child)
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = BackColor };
            p.RowStyles.Add(new RowStyle(SizeType.Absolute,22)); p.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            p.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = Theme.Muted, Font = new Font(Font,FontStyle.Bold) },0,0); p.Controls.Add(child,0,1); return p;
        }
        private Button MakeButton(string text, Action action)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(18,24,40), ForeColor = Color.White, Margin = new Padding(0,0,10,0), Padding = new Padding(12,0,12,0) };
            b.FlatAppearance.BorderColor = Color.FromArgb(65,86,140); b.Click += (s,e) => action(); return b;
        }
        private void LoadDevices()
        {
            devices.Items.Clear(); outputs.Items.Clear();
            try
            {
                for (int i=0;i<WaveInEvent.DeviceCount;i++) { var caps=WaveInEvent.GetCapabilities(i); devices.Items.Add(new DeviceItem(i,caps.ProductName)); }
            }
            catch { /* Device enumeration can fail when Windows audio services are restarting. */ }
            try
            {
                for (int i=0;i<WaveOut.DeviceCount;i++) { var caps=WaveOut.GetCapabilities(i); outputs.Items.Add(new DeviceItem(i,caps.ProductName)); }
            }
            catch { /* Keep the form open so the user can see the input/output issue. */ }
            if (devices.Items.Count>0) devices.SelectedIndex=0;
            if (outputs.Items.Count>0) outputs.SelectedIndex=0;
        }
        private void StartAudio()
        {
            if (capture != null) { StopAudio(); return; }
            if (devices.SelectedItem == null) { MessageBox.Show(this,"No microphone was found. Connect a microphone and reopen Voice FX."); return; }
            try
            {
                var device=(DeviceItem)devices.SelectedItem;
                capture = new WaveInEvent { DeviceNumber=device.Index, WaveFormat=new WaveFormat(44100,1), BufferMilliseconds=20, NumberOfBuffers=3 };
                buffer = new BufferedWaveProvider(capture.WaveFormat) { BufferDuration=TimeSpan.FromSeconds(2), DiscardOnBufferOverflow=true };
                // WaveInEvent captures 16-bit PCM. Convert it explicitly to IEEE float samples
                // before the SMB pitch shifter; the shifter rejects non-floating-point providers.
                ISampleProvider source = new WaveToSampleProvider(buffer);
                if (source.WaveFormat.Encoding != WaveFormatEncoding.IeeeFloat || source.WaveFormat.BitsPerSample != 32)
                    throw new InvalidOperationException("Voice FX expected 32-bit IEEE floating-point samples but received " + source.WaveFormat.Encoding + ", " + source.WaveFormat.BitsPerSample + "-bit. Please send this message so the conversion can be fixed for your device.");
                shifter = new SmbPitchShiftingSampleProvider(source) { PitchFactor=(float)Math.Pow(2.0,pitch.Value/12.0) };
                int outputIndex = outputs.SelectedItem is DeviceItem ? ((DeviceItem)outputs.SelectedItem).Index : -1;
                output = new WaveOutEvent { DesiredLatency=120, NumberOfBuffers=3, DeviceNumber=outputIndex };
                output.PlaybackStopped += (sender, args) =>
                {
                    if (args.Exception != null && !IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)(() => { status.Text = "●  AUDIO OUTPUT ERROR"; status.ForeColor = Theme.Danger; }));
                };
                output.Init(new SampleToWaveProvider16(shifter));
                capture.DataAvailable += (sender, args) =>
                {
                    var currentBuffer = buffer;
                    if (currentBuffer != null && args.BytesRecorded > 0)
                    {
                        try { currentBuffer.AddSamples(args.Buffer, 0, args.BytesRecorded); }
                        catch (ObjectDisposedException) { }
                    }
                };
                capture.RecordingStopped += (sender, args) =>
                {
                    if (args.Exception != null && !IsDisposed && IsHandleCreated)
                        BeginInvoke((Action)(() => { status.Text = "●  MICROPHONE ERROR"; status.ForeColor = Theme.Danger; }));
                };
                output.Play();
                capture.StartRecording();
                start.Text="STOP LIVE VOICE"; status.Text="●  LIVE VOICE ACTIVE"; status.ForeColor=Color.FromArgb(105,230,180);
            }
            catch(Exception ex) { StopAudio(); MessageBox.Show(this,"Could not start live voice processing. Check your microphone and audio device.\n\n"+ex.Message,"Voice FX",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        }
        private void StopAudio()
        {
            try { if(capture!=null) { capture.StopRecording(); capture.Dispose(); } } catch {} capture=null;
            try { if(output!=null) { output.Stop(); output.Dispose(); } } catch {} output=null; buffer=null; shifter=null;
            if(start!=null) start.Text="START LIVE VOICE"; if(status!=null) { status.Text="●  VOICE FX OFF"; status.ForeColor=Theme.Blue; }
        }
        private void ApplyQuick(string name,int value) { pitch.Value=value; }
        private void SavePreset()
        {
            string name=Prompt.Show(this,"Preset name:","Save Voice Preset","My Voice"); if(string.IsNullOrWhiteSpace(name)) return;
            var old=saved.FirstOrDefault(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase)); if(old!=null) saved.Remove(old);
            saved.Add(new VoicePreset { Name=name.Trim(), Pitch=pitch.Value }); SavePresets(); RefreshPresets(); presets.SelectedItem=saved.FirstOrDefault(p=>p.Name==name.Trim());
        }
        private void ApplyPreset() { var p=presets.SelectedItem as VoicePreset; if(p!=null) pitch.Value=Math.Max(pitch.Minimum,Math.Min(pitch.Maximum,p.Pitch)); }
        private void DeletePreset() { var p=presets.SelectedItem as VoicePreset; if(p==null)return; if(MessageBox.Show(this,"Delete preset '"+p.Name+"'?","Delete preset",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return; saved.Remove(p); SavePresets(); RefreshPresets(); }
        private void RefreshPresets() { presets.Items.Clear(); foreach(var p in saved.OrderBy(p=>p.Name)) presets.Items.Add(p); if(presets.Items.Count>0)presets.SelectedIndex=0; }
        private void LoadPresets() { try { if(File.Exists(PresetPath)) using(var f=File.OpenRead(PresetPath)) saved.AddRange((List<VoicePreset>)new XmlSerializer(typeof(List<VoicePreset>)).Deserialize(f)); } catch { saved.Clear(); } }
        private void SavePresets() { try { Directory.CreateDirectory(Path.GetDirectoryName(PresetPath)); using(var f=File.Create(PresetPath)) new XmlSerializer(typeof(List<VoicePreset>)).Serialize(f,saved); } catch(Exception ex) { MessageBox.Show(this,"Could not save presets: "+ex.Message); } }
        private sealed class DeviceItem { public int Index; public string Name; public DeviceItem(int i,string n){Index=i;Name=n;} public override string ToString(){return Name;} }
        private static class Prompt
        {
            public static string Show(IWin32Window owner,string text,string caption,string initial)
            { using(var f=new Form{Text=caption,Size=new Size(360,150),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(8,10,18),ForeColor=Color.White,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false}) { var l=new Label{Text=text,Left=14,Top=12,Width=310}; var t=new TextBox{Text=initial,Left=14,Top=38,Width=310}; var ok=new Button{Text="Save",DialogResult=DialogResult.OK,Left=244,Top=72,Width=80}; f.Controls.AddRange(new Control[]{l,t,ok});f.AcceptButton=ok;return f.ShowDialog(owner)==DialogResult.OK?t.Text:null; } }
        }
    }

    /// <summary>A custom painted accent-coloured pitch slider with a pill track and luminous thumb.</summary>
    internal sealed class AccentPitchTrackBar : Control
    {
        private int value;
        public int Minimum { get; set; } = -12;
        public int Maximum { get; set; } = 12;
        public int TickFrequency { get; set; } = 3;
        public Color Accent { get; set; } = Color.FromArgb(158, 105, 255);
        public event EventHandler ValueChanged;
        public int Value
        {
            get { return value; }
            set { int v = Math.Max(Minimum, Math.Min(Maximum, value)); if (this.value == v) return; this.value = v; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty); }
        }
        public AccentPitchTrackBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 58; Cursor = Cursors.Hand;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float left = 10f, right = Math.Max(left + 1f, Width - 10f), cy = Height / 2f + 7f;
            using (var track = new SolidBrush(Color.FromArgb(38, 43, 68)))
            using (var p = new System.Drawing.Drawing2D.GraphicsPath())
            { p.AddArc(left, cy - 3, 6, 6, 90, 180); p.AddArc(right - 6, cy - 3, 6, 6, 270, 180); p.CloseFigure(); g.FillPath(track, p); }
            float pct = Maximum == Minimum ? 0f : (Value - Minimum) / (float)(Maximum - Minimum);
            float x = left + (right - left) * pct;
            if (x > left + 1f)
            { using (var fill = new System.Drawing.Drawing2D.LinearGradientBrush(new PointF(left, cy), new PointF(x, cy), Color.FromArgb(255, 91, 177), Accent)) g.FillRectangle(fill, left, cy - 2f, x - left, 4f); }
            int ticks = Math.Max(2, (Maximum - Minimum) / Math.Max(1, TickFrequency));
            for (int i = 0; i <= ticks; i++) { float tx = left + (right - left) * i / ticks; using (var p = new Pen(Color.FromArgb(95, 160, 175, 220), 1f)) g.DrawLine(p, tx, cy + 7f, tx, cy + 10f); }
            using (var glow = new SolidBrush(Color.FromArgb(40, Accent.R, Accent.G, Accent.B))) g.FillEllipse(glow, x - 14f, cy - 14f, 28f, 28f);
            using (var edge = new Pen(Accent, 2f)) g.DrawEllipse(edge, x - 8f, cy - 8f, 16f, 16f);
            using (var thumb = new SolidBrush(Color.FromArgb(240, 244, 255))) g.FillEllipse(thumb, x - 5f, cy - 5f, 10f, 10f);
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); SetFromX(e.X); }
        protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) SetFromX(e.X); base.OnMouseMove(e); }
        protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Home || keyData == Keys.End || base.IsInputKey(keyData); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) Value--; else if (e.KeyCode == Keys.Right) Value++; else if (e.KeyCode == Keys.Home) Value = Minimum; else if (e.KeyCode == Keys.End) Value = Maximum; else { base.OnKeyDown(e); return; }
            e.Handled = true;
        }
        private void SetFromX(int px) { float left = 10f, right = Math.Max(left + 1f, Width - 10f); float p = Math.Max(0f, Math.Min(1f, (px - left) / (right - left))); Value = Minimum + (int)Math.Round(p * (Maximum - Minimum)); Focus(); }
    }

    /// <summary>Serializable user-saved voice pitch preset.</summary>
    public sealed class VoicePreset
    {
        public string Name { get; set; }
        public int Pitch { get; set; }
        public override string ToString() { return Name; }
    }
}
