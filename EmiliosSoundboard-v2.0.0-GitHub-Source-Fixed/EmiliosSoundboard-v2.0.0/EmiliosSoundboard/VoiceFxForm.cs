using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Serialization;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;

namespace EmiliosSoundboard
{
    /// <summary>Live microphone monitor with realtime pitch shifting and locally saved presets.</summary>
    internal sealed class VoiceFxForm : Form
    {
        private ComboBox devices, outputs, presets;
        private AccentPitchTrackBar pitch, finePitch;
        private Label pitchValue, finePitchValue, status;
        private GlowButton start, delete, test;
        private MicLevelMeter meter;
        private bool testMode;
        private bool loadingDevices;
        private long lastMeterUiTicks;
        private readonly DataStore dataStore;
        private Icon windowIcon;
        // Keep the expensive starfield static in Voice FX. Repainting the full mixer every 65 ms
        // caused avoidable CPU usage and made child-control text appear to flicker on slower PCs.
        private const float BackgroundPhase = 1.25f;
        private WasapiCapture capture;
        private WasapiOut output;
        private MMDevice activeInputDevice;
        private MMDevice activeOutputDevice;
        private BufferedWaveProvider buffer;
        private SmbPitchShiftingSampleProvider shifter;
        private readonly List<VoicePreset> saved = new List<VoicePreset>();
        private readonly Dictionary<Control, Color> normalForeColors = new Dictionary<Control, Color>();
        private readonly Dictionary<Control, Color> normalBackColors = new Dictionary<Control, Color>();
        private string PresetPath { get { return Path.Combine(PlatformServices.GetAppDataDirectory(), "EmiliosSoundboard", "voice-presets.xml"); } }

        public VoiceFxForm(DataStore store = null)
        {
            dataStore = store;
            Log.Debug("voicefx", "constructor start");
            Text = "Emilio's Soundboard — VOICE FX"; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = true;
            ClientSize = new Size(900, 760); BackColor = Color.FromArgb(3, 4, 13); ForeColor = Theme.CoolWhite;
            DoubleBuffered = true;
            Font = VoiceUiFont(10f);
            try { windowIcon = Theme.CreateAppIcon(); Icon = windowIcon; } catch { windowIcon = null; }
            Log.Debug("voicefx", "building UI");
            BuildUi();
            CaptureNormalPalette(this);
            ApplyVoiceFxPalette(this);
            Log.Debug("voicefx", "loading presets");
            LoadPresets(); RefreshPresets();
            Log.Debug("voicefx", "loading devices");
            LoadDevices();
            // Voice FX uses stable Segoe UI text in every theme. Comic mode changes the palette
            // but avoids swapping all small UI fonts to Comic Sans, which looked jagged.
            outputs.SelectedIndexChanged += (s, e) =>
            {
                if (loadingDevices || !(outputs.SelectedItem is DeviceItem selected)) return;
                bool restartLive = capture != null && !testMode;
                if (dataStore != null)
                {
                    dataStore.Data.Settings.SoundOutputDeviceId = selected.Id;
                    dataStore.Save();
                }
                if (restartLive) { StopAudio(); StartAudio(); }
            };
            devices.SelectedIndexChanged += (s, e) =>
            {
                if (loadingDevices || !(devices.SelectedItem is DeviceItem)) return;
                bool wasLive = capture != null && !testMode;
                bool wasTesting = capture != null && testMode;
                if (wasLive || wasTesting)
                {
                    StopAudio();
                    if (wasLive) StartAudio(); else StartMicTest();
                }
            };
            FormClosing += (s,e) => StopAudio();
            Log.Debug("voicefx", "constructor finished: " + devices.Items.Count + " input(s), " + outputs.Items.Count + " output(s)");
        }

        internal void RefreshThemeAccent()
        {
            if (pitch != null) pitch.Accent = Theme.Blue;
            if (finePitch != null) finePitch.Accent = Theme.Blue;
            if (meter != null) meter.Accent = Theme.Blue;
            ApplyVoiceFxPalette(this);
            if (pitch != null) pitch.Invalidate();
            if (finePitch != null) finePitch.Invalidate();
            if (meter != null) meter.Invalidate();
            Invalidate();
        }

        private void CaptureNormalPalette(Control root)
        {
            if (root == null) return;
            if (!normalForeColors.ContainsKey(root)) normalForeColors[root] = root.ForeColor;
            if (!normalBackColors.ContainsKey(root)) normalBackColors[root] = root.BackColor;
            foreach (Control child in root.Controls) CaptureNormalPalette(child);
        }

        private void ApplyVoiceFxPalette(Control root)
        {
            if (root == null) return;
            bool comic = Theme.ComicStyleMode;
            if (root is Label)
            {
                Color original;
                if (comic) root.ForeColor = Color.Black;
                else if (normalForeColors.TryGetValue(root, out original)) root.ForeColor = original;
            }
            else if (root is ComboBox || root is TextBoxBase || root is ListBox || root is ListView)
            {
                Color originalFore, originalBack;
                if (comic)
                {
                    root.ForeColor = Color.Black;
                    root.BackColor = Color.White;
                }
                else
                {
                    if (normalForeColors.TryGetValue(root, out originalFore)) root.ForeColor = originalFore;
                    if (normalBackColors.TryGetValue(root, out originalBack)) root.BackColor = originalBack;
                }
            }
            foreach (Control child in root.Controls) ApplyVoiceFxPalette(child);

            if (status != null)
            {
                if (comic) status.ForeColor = Color.Black;
                else if (status.Text.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         status.Text.IndexOf("NO VIRTUAL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         status.Text.IndexOf("INCOMPLETE", StringComparison.OrdinalIgnoreCase) >= 0)
                    status.ForeColor = Theme.Danger;
                else if (status.Text.IndexOf("ACTIVE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         status.Text.IndexOf("ROUTE SELECTED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         status.Text.IndexOf("DETECTED", StringComparison.OrdinalIgnoreCase) >= 0)
                    status.ForeColor = Color.FromArgb(105, 230, 180);
                else status.ForeColor = Theme.Blue;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyDarkTitleBar(this);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            SpaceBackdropPainter.Draw(e.Graphics, ClientRectangle, BackgroundPhase);
            DrawMixerPanels(e.Graphics);
        }

        private void DrawMixerPanels(Graphics graphics)
        {
            float width = ClientSize.Width - 36f;
            DrawMixerPanel(graphics, new RectangleF(18f, 86f, width, 94f));
            DrawMixerPanel(graphics, new RectangleF(18f, 184f, width, 148f));
            DrawMixerPanel(graphics, new RectangleF(18f, 336f, width, 46f));
            DrawMixerPanel(graphics, new RectangleF(18f, 384f, width, 78f));
            DrawMixerPanel(graphics, new RectangleF(18f, 466f, width, 68f));
            DrawMixerPanel(graphics, new RectangleF(18f, 538f, width, 142f));
        }

        private void DrawMixerPanel(Graphics graphics, RectangleF bounds)
        {
            // Quiet, borderless panel fills keep sections visually grouped without the purple
            // outlines; avoiding stroked paths also reduces work during background painting.
            using (var path = Theme.RoundedRect(bounds, Theme.ComicStyleMode ? 4f : 12f))
            using (var fill = new SolidBrush(Theme.ComicStyleMode
                ? Color.FromArgb(238, 255, 255, 255)
                : Color.FromArgb(142, 7, 11, 25)))
                graphics.FillPath(fill, path);
        }

        private static Font VoiceUiFont(float points, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", points, style, GraphicsUnit.Point);
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(26), BackColor = Color.Transparent };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);
            var title = new Label { Text = "VOICE FX", Dock = DockStyle.Fill, Font = VoiceUiFont(25f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(title,0,0);
            var deviceRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent };
            deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            deviceRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16));
            devices = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(10, 15, 29), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DropDownWidth = 360, Font = VoiceUiFont(9.5f) };
            outputs = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(10, 15, 29), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, DropDownWidth = 360, Font = VoiceUiFont(9.5f) };
            var deviceActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 0), Margin = Padding.Empty };
            var refreshDevices = MakeButton("REFRESH", LoadDevices); refreshDevices.Size = new Size(112, 32); refreshDevices.Margin = Padding.Empty;
            var autoRoute = MakeButton("AUTO ROUTE", AutoRoute); autoRoute.Size = new Size(112, 32); autoRoute.Margin = new Padding(0, 2, 0, 0);
            deviceActions.Controls.Add(refreshDevices); deviceActions.Controls.Add(autoRoute);
            deviceRow.Controls.Add(Labeled("MICROPHONE INPUT", devices), 0, 0);
            deviceRow.Controls.Add(Labeled("CHAT OUTPUT (SOUNDBOARD + VOICE FX)", outputs), 1, 0);
            deviceRow.Controls.Add(deviceActions, 2, 0);
            root.Controls.Add(deviceRow, 0, 1);
            var pitchPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var pitchTitle = new Label { Text = "PITCH SHIFT  /  COARSE", ForeColor = Theme.Muted, BackColor = Color.Transparent, Location = new Point(0, 3), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            pitchValue = new Label { Text = "0 semitones", ForeColor = Color.White, BackColor = Color.Transparent, Location = new Point(0, 3), Width = 190, Height = 24, Anchor = AnchorStyles.Top | AnchorStyles.Right, TextAlign = ContentAlignment.TopRight };
            pitch = new AccentPitchTrackBar { Minimum = -24, Maximum = 24, Value = 0, TickFrequency = 4, Location = new Point(0, 25), Size = new Size(820, 52), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = BackColor, Accent = Theme.Blue };
            var fineTitle = new Label { Text = "FINE TUNE", ForeColor = Theme.Muted, BackColor = Color.Transparent, Location = new Point(0, 80), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
            finePitchValue = new Label { Text = "0 cents", ForeColor = Color.White, BackColor = Color.Transparent, Location = new Point(0, 80), Width = 190, Height = 22, Anchor = AnchorStyles.Top | AnchorStyles.Right, TextAlign = ContentAlignment.TopRight };
            finePitch = new AccentPitchTrackBar { Minimum = -100, Maximum = 100, Value = 0, TickFrequency = 25, Location = new Point(0, 101), Size = new Size(820, 43), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BackColor = BackColor, Accent = Theme.Blue };
            pitch.ValueChanged += (s,e) => UpdatePitchControls();
            finePitch.ValueChanged += (s,e) => UpdatePitchControls();
            pitchPanel.Resize += (s,e) => { int rightWidth = Math.Min(190, Math.Max(120, pitchPanel.ClientSize.Width / 3)); pitchValue.Width = finePitchValue.Width = rightWidth; pitchValue.Left = pitchPanel.ClientSize.Width - rightWidth; finePitchValue.Left = pitchPanel.ClientSize.Width - rightWidth; pitch.Width = finePitch.Width = Math.Max(100, pitchPanel.ClientSize.Width - 4); };
            pitchPanel.Controls.Add(pitch); pitchPanel.Controls.Add(pitchTitle); pitchPanel.Controls.Add(pitchValue); pitchPanel.Controls.Add(fineTitle); pitchPanel.Controls.Add(finePitch); pitchPanel.Controls.Add(finePitchValue); root.Controls.Add(pitchPanel,0,2);
            meter = new MicLevelMeter { Dock = DockStyle.Fill, Accent = Theme.Blue, Margin = new Padding(0, 6, 0, 0) };
            root.Controls.Add(meter, 0, 3);
            var presetsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0,12,0,0) };
            presets = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(10, 15, 29), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = VoiceUiFont(9.5f) };
            presets.SelectedIndexChanged += (s,e) => ApplyPreset();
            presetsPanel.Controls.Add(presets); presetsPanel.Controls.Add(MakeButton("DEEP", () => ApplyQuick("Deep", -12))); presetsPanel.Controls.Add(MakeButton("HIGH", () => ApplyQuick("High", 12))); presetsPanel.Controls.Add(MakeButton("ALIEN", () => ApplyQuick("Alien", 18)));
            root.Controls.Add(Labeled("VOICE PRESETS", presetsPanel),0,4);
            var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0,10,0,0), BackColor = Color.Transparent };
            start = MakeButton("VOICE CHANGER OFF — TURN ON", StartAudio, GlowStyle.Secondary); start.Size = new Size(250, 44); start.Font = VoiceUiFont(9.5f, FontStyle.Bold); controls.Controls.Add(start);
            test = MakeButton("TEST MIC", StartMicTest); test.Size = new Size(110, 44); controls.Controls.Add(test);
            var savePreset = MakeButton("SAVE PRESET", SavePreset); savePreset.Size = new Size(130, 44); controls.Controls.Add(savePreset); delete = MakeButton("DELETE PRESET", DeletePreset); delete.Size = new Size(130, 44); controls.Controls.Add(delete);
            var cableHelp = MakeButton("GET VB-CABLE", OpenVirtualCablePage); cableHelp.Size = new Size(118, 44); controls.Controls.Add(cableHelp);
            root.Controls.Add(controls,0,5);
            var note = new Label { Dock = DockStyle.Fill, BackColor = Color.Transparent, ForeColor = Color.FromArgb(174, 190, 225), Text = "QUICK SETUP: Select your real microphone above. Set CHAT OUTPUT to CABLE Input (VB-CABLE), turn VOICE CHANGER ON, and move PITCH away from 0 to hear the effect. In Discord, set Input Device to CABLE Output — NOT your real microphone or GoXLR Chat Mic. If Discord still sounds unchanged, it is listening to the raw mic or the GoXLR is mixing the raw mic directly. For GoXLR, route the app's playback channel into the chat mic mix and avoid sending the raw mic as the chat input. GET VB-CABLE opens the official driver page; Windows needs a virtual cable or mixer route.", TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0,8,0,0) };
            root.Controls.Add(note,0,6);
            status = new Label { Dock = DockStyle.Fill, ForeColor = Theme.Blue, BackColor = Color.Transparent, Text = "VOICE FX OFF  •  READY FOR CHAT ROUTING", TextAlign = ContentAlignment.MiddleLeft, Font = VoiceUiFont(8.5f, FontStyle.Bold) };
            root.Controls.Add(status,0,7);
        }

        private Control Labeled(string label, Control child)
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            p.RowStyles.Add(new RowStyle(SizeType.Absolute,22)); p.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            p.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, ForeColor = Theme.Muted, BackColor = Color.Transparent, Font = VoiceUiFont(8.5f, FontStyle.Bold) },0,0); p.Controls.Add(child,0,1); return p;
        }
        private GlowButton MakeButton(string text, Action action, GlowStyle style = GlowStyle.Secondary)
        {
            var button = new GlowButton
            {
                Text = text,
                Style = style,
                Font = VoiceUiFont(9f, FontStyle.Bold),
                Margin = new Padding(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                TabStop = true
            };
            button.Click += (s, e) => action();
            return button;
        }

        private void LoadDevices()
        {
            Log.Debug("voicefx", "LoadDevices: enumerating WASAPI endpoints");
            bool resumeLive = capture != null && !testMode;
            bool resumeTest = capture != null && testMode;
            loadingDevices = true;
            // Release endpoint handles before enumerating; resume the previous mode after a successful refresh.
            if (capture != null || output != null) StopAudio();
            devices.Items.Clear();
            outputs.Items.Clear();
            string defaultCaptureId = null;
            string defaultRenderId = null;
            bool enumerationFailed = false;
            var captureItems = new List<DeviceItem>();
            var renderItems = new List<DeviceItem>();
            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    try { using (var endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console)) defaultCaptureId = endpoint.ID; } catch { }
                    try { using (var endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console)) defaultRenderId = endpoint.ID; } catch { }

                    var captureEndpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                    for (int i = 0; i < captureEndpoints.Count; i++)
                    {
                        using (var endpoint = captureEndpoints[i]) captureItems.Add(new DeviceItem(endpoint.ID, endpoint.FriendlyName));
                    }
                    var renderEndpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                    for (int i = 0; i < renderEndpoints.Count; i++)
                    {
                        using (var endpoint = renderEndpoints[i]) renderItems.Add(new DeviceItem(endpoint.ID, endpoint.FriendlyName));
                    }
                }
            }
            catch (Exception ex)
            {
                enumerationFailed = true;
                System.Diagnostics.Debug.WriteLine("Voice FX device enumeration failed: " + ex);
            }

            foreach (var item in captureItems.OrderByDescending(x => x.IsGoXlr).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) devices.Items.Add(item);
            foreach (var item in renderItems.OrderByDescending(x => x.IsVirtualRoute || x.IsGoXlr).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) outputs.Items.Add(item);

            SelectDeviceById(devices, defaultCaptureId);
            string preferredOutputId = dataStore == null ? null : dataStore.Data.Settings.SoundOutputDeviceId;
            SelectDeviceById(outputs, !String.IsNullOrWhiteSpace(preferredOutputId) ? preferredOutputId : defaultRenderId);
            if (devices.SelectedIndex < 0 && devices.Items.Count > 0) devices.SelectedIndex = 0;
            if (outputs.SelectedIndex < 0 && !String.IsNullOrWhiteSpace(preferredOutputId))
            {
                // The selected virtual cable / mixer endpoint may have been unplugged.
                if (dataStore != null) { dataStore.Data.Settings.SoundOutputDeviceId = ""; dataStore.Save(); }
                SelectDeviceById(outputs, defaultRenderId);
            }
            if (outputs.SelectedIndex < 0 && outputs.Items.Count > 0) outputs.SelectedIndex = 0;
            loadingDevices = false;

            if (enumerationFailed)
            {
                status.Text = "DEVICE LIST INCOMPLETE — CHECK WINDOWS AUDIO SETTINGS";
                status.ForeColor = Theme.ComicStyleMode ? Color.Black : Theme.Danger;
                return;
            }
            bool goxlrFound = captureItems.Any(x => x.IsGoXlr) || renderItems.Any(x => x.IsGoXlr);
            bool virtualRouteFound = renderItems.Any(x => x.IsVirtualRoute || x.IsGoXlr);
            status.Text = goxlrFound ? "GOXLR DETECTED — CONFIGURE OR AUTO-SELECT CHAT ROUTE" :
                virtualRouteFound ? "VIRTUAL CHAT ROUTE DETECTED — CLICK AUTO ROUTE" :
                "NO VIRTUAL CHAT ROUTE DETECTED — INSTALL VB-CABLE";
            status.ForeColor = Theme.ComicStyleMode ? Color.Black : (virtualRouteFound ? Color.FromArgb(105, 230, 180) : Theme.Danger);
            Log.Debug("voicefx", "device scan: " + captureItems.Count + " input(s), " + renderItems.Count +
                                  " output(s), goxlr=" + goxlrFound + ", virtualRoute=" + virtualRouteFound +
                                  ", enumerationFailed=" + enumerationFailed);
            if (resumeLive) StartAudio();
            else if (resumeTest) StartMicTest();
        }

        private void AutoRoute()
        {
            var route = outputs.Items.Cast<object>()
                .OfType<DeviceItem>()
                .Where(item => item.IsVirtualRoute || item.IsGoXlr)
                .OrderByDescending(item => item.IsCableInput)
                .ThenByDescending(item => item.IsVoiceMeeterInput)
                .ThenByDescending(item => item.IsGoXlr)
                .FirstOrDefault();

            if (route == null)
            {
                status.Text = "NO CHAT ROUTE FOUND — INSTALL VB-CABLE OR CONFIGURE GOXLR";
                status.ForeColor = Theme.ComicStyleMode ? Color.Black : Theme.Danger;
                MessageBox.Show(this,
                    "No virtual-cable or GoXLR playback endpoint was detected. Install a virtual audio cable such as VB-CABLE, or connect and configure GoXLR. The app cannot create a virtual microphone driver by itself.",
                    "Chat audio routing", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            outputs.SelectedItem = route;
            if (dataStore != null)
            {
                dataStore.Data.Settings.SoundOutputDeviceId = route.Id;
                dataStore.Save();
            }
            status.Text = capture != null && !testMode
                ? "VOICE FX ACTIVE — CHAT ROUTE SELECTED"
                : "CHAT ROUTE SELECTED — SOUNDBOARD + VOICE FX WILL USE IT";
            status.ForeColor = Theme.ComicStyleMode ? Color.Black : Color.FromArgb(105, 230, 180);
        }

        private static void SelectDeviceById(ComboBox combo, string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                var item = combo.Items[i] as DeviceItem;
                if (item != null && String.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private static bool LooksLikeGoXlr(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return false;
            return name.IndexOf("goxlr", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("tc-helicon", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("tc helicon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void StartAudio()
        {
            string startupStage = "preparing audio devices";
            if (capture != null)
            {
                if (!testMode) { StopAudio(); return; }
                StopAudio(); // switch directly from the silent microphone test to live processing
            }
            if (devices.SelectedItem == null) { MessageBox.Show(this, "No microphone was found. Connect a microphone and reopen Voice FX."); return; }
            try
            {
                testMode = false;
                var device = (DeviceItem)devices.SelectedItem;
                startupStage = "opening the selected microphone";
                capture = CreateCapture(device);
                buffer = new BufferedWaveProvider(capture.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromSeconds(1),
                    DiscardOnBufferOverflow = true,
                    ReadFully = true
                };

                // The capture stream is explicitly requested as 16-bit PCM. WaveToSampleProvider
                // only accepts IEEE-float input and causes "Must be already floating point" here.
                // Pcm16BitToSampleProvider is the correct PCM16-to-float converter for NAudio 2.2.1.
                ISampleProvider source = new Pcm16BitToSampleProvider(buffer);
                shifter = new SmbPitchShiftingSampleProvider(source)
                {
                    PitchFactor = (float)Math.Pow(2.0, (pitch.Value + finePitch.Value / 100.0) / 12.0)
                };
                if (!(outputs.SelectedItem is DeviceItem)) throw new InvalidOperationException("Select a playback output before starting Voice FX.");
                var outputDevice = (DeviceItem)outputs.SelectedItem;
                startupStage = "opening the selected chat-output device";
                activeOutputDevice = GetEndpoint(outputDevice);
                // Use shared-mode WASAPI with timer-driven buffering. Some Windows drivers fail
                // to initialize the event-driven path when another app has recently used the endpoint.
                ISampleProvider stereoOutput = new MonoToStereoSampleProvider(shifter);
                Exception outputInitError = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        output = new WasapiOut(activeOutputDevice, AudioClientShareMode.Shared, false, 150);
                        output.PlaybackStopped += (sender, args) =>
                        {
                            if (args.Exception != null && !IsDisposed && IsHandleCreated)
                            {
                                try { BeginInvoke((Action)(() => { status.Text = "AUDIO OUTPUT ERROR — STOP AND CHECK THE OUTPUT DEVICE"; status.ForeColor = Theme.ComicStyleMode ? Color.Black : Theme.Danger; })); }
                                catch (InvalidOperationException) { }
                            }
                        };
                        output.Init(new SampleToWaveProvider16(stereoOutput));
                        outputInitError = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        outputInitError = ex;
                        if (output != null) { output.Dispose(); output = null; }
                        if (FindHResult(ex, unchecked((int)0x8889000A)) != null && attempt < 2)
                            System.Threading.Thread.Sleep(250);
                        else
                            throw;
                    }
                }
                if (outputInitError != null) throw outputInitError;
                AttachCaptureHandlers();
                startupStage = "starting audio playback";
                output.Play();
                startupStage = "starting microphone capture";
                capture.StartRecording();
                start.Text = "VOICE CHANGER ON — TURN OFF";
                start.Style = GlowStyle.Primary;
                test.Text = "TEST MIC";
                bool chatRoute = outputDevice.IsVirtualRoute || outputDevice.IsGoXlr;
                status.Text = chatRoute ? "VOICE FX ACTIVE — CHAT ROUTE SELECTED" : "VOICE FX ACTIVE — LOCAL OUTPUT ONLY";
                status.ForeColor = Theme.ComicStyleMode ? Color.Black : (chatRoute ? Color.FromArgb(105, 230, 180) : Theme.Blue);
            }
            catch (Exception ex)
            {
                StopAudio();
                string details;
                if (FindHResult(ex, unchecked((int)0x8889000A)) != null)
                {
                    details = "Windows says an audio device is already in use (0x8889000A).\n\n" +
                        "Try this: close other audio apps. Open Windows Settings > System > Sound > More sound settings. " +
                        "For BOTH your microphone (Recording tab) and selected output (Playback tab), open Properties > Advanced and uncheck both options that allow applications to take exclusive control. Click Apply, then reopen Emilio's Soundboard.\n\n" +
                        "The error happened while " + startupStage + ".\n\nTechnical details: " + ex.Message;
                }
                else
                {
                    details = "Could not start live voice processing while " + startupStage + ". Check the selected microphone and output device.\n\n" + ex.Message;
                }
                MessageBox.Show(this, details, "Voice FX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static Exception FindHResult(Exception ex, int expectedHResult)
        {
            for (Exception current = ex; current != null; current = current.InnerException)
                if (current.HResult == expectedHResult) return current;
            return null;
        }

        private void StartMicTest()
        {
            if (capture != null && testMode) { StopAudio(); return; }
            if (capture != null) StopAudio();
            if (devices.SelectedItem == null) { MessageBox.Show(this, "No microphone was found. Connect a microphone and reopen Voice FX."); return; }
            try
            {
                testMode = true;
                var device = (DeviceItem)devices.SelectedItem;
                capture = CreateCapture(device);
                buffer = null;
                output = null;
                AttachCaptureHandlers();
                capture.StartRecording();
                start.Text = "VOICE CHANGER OFF — TURN ON";
                test.Text = "STOP MIC TEST";
                status.Text = "MIC TEST ACTIVE — SILENT INPUT CHECK";
                status.ForeColor = Theme.ComicStyleMode ? Color.Black : Theme.Blue;
            }
            catch (Exception ex)
            {
                StopAudio();
                MessageBox.Show(this, "Could not test this microphone. Choose another input device or check Windows microphone permissions.\n\n" + ex.Message,
                    "Voice FX", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private WasapiCapture CreateCapture(DeviceItem device)
        {
            activeInputDevice = GetEndpoint(device);
            var wasapi = new WasapiCapture(activeInputDevice, true, 50)
            {
                // Ask the Windows audio engine for a predictable PCM format so the processing
                // graph and level meter receive 16-bit mono rather than a driver-specific float layout.
                WaveFormat = new WaveFormat(44100, 16, 1),
                ShareMode = AudioClientShareMode.Shared
            };
            return wasapi;
        }

        private static MMDevice GetEndpoint(DeviceItem device)
        {
            using (var enumerator = new MMDeviceEnumerator())
                return enumerator.GetDevice(device.Id);
        }

        private void AttachCaptureHandlers()
        {
            capture.DataAvailable += (sender, args) =>
            {
                BufferedWaveProvider currentBuffer = buffer;
                if (currentBuffer != null && args.BytesRecorded > 0)
                {
                    try { currentBuffer.AddSamples(args.Buffer, 0, args.BytesRecorded); }
                    catch (ObjectDisposedException) { }
                }
                UpdateInputMeter(args.Buffer, args.BytesRecorded);
            };
            capture.RecordingStopped += (sender, args) =>
            {
                if (args.Exception != null && !IsDisposed && IsHandleCreated)
                {
                    try { BeginInvoke((Action)(() => { status.Text = "MICROPHONE ERROR — CHECK DEVICE PERMISSIONS"; status.ForeColor = Theme.ComicStyleMode ? Color.Black : Theme.Danger; })); }
                    catch (InvalidOperationException) { }
                }
            };
        }

        private void UpdateInputMeter(byte[] bytes, int count)
        {
            if (bytes == null || count < 2) return;
            long now = Environment.TickCount64;
            if (now - System.Threading.Interlocked.Read(ref lastMeterUiTicks) < 40) return;
            System.Threading.Interlocked.Exchange(ref lastMeterUiTicks, now);
            int samples = count / 2;
            double sum = 0d;
            for (int i = 0; i < samples; i++)
            {
                short sample = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                double normalized = sample / 32768d;
                sum += normalized * normalized;
            }
            float level = (float)Math.Min(1d, Math.Sqrt(sum / Math.Max(1, samples)) * 3.2d);
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (meter != null) meter.Level = level;
                    if (testMode && status != null)
                    {
                        status.Text = level > 0.025f ? "MIC TEST — INPUT DETECTED" : "MIC TEST — WAITING FOR SOUND";
                        status.ForeColor = Theme.ComicStyleMode ? Color.Black : (level > 0.025f ? Color.FromArgb(105, 230, 180) : Theme.Blue);
                    }
                }));
            }
            catch (InvalidOperationException) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (windowIcon != null) { windowIcon.Dispose(); windowIcon = null; }
            }
            base.Dispose(disposing);
        }

        private void StopAudio()
        {
            try { if (capture != null) { capture.StopRecording(); capture.Dispose(); } } catch { }
            capture = null;
            try { if (output != null) { output.Stop(); output.Dispose(); } } catch { }
            output = null;
            try { if (activeInputDevice != null) activeInputDevice.Dispose(); } catch { }
            activeInputDevice = null;
            try { if (activeOutputDevice != null) activeOutputDevice.Dispose(); } catch { }
            activeOutputDevice = null;
            buffer = null;
            shifter = null;
            testMode = false;
            if (meter != null) meter.Level = 0f;
            if (start != null) { start.Text = "VOICE CHANGER OFF — TURN ON"; start.Style = GlowStyle.Secondary; }
            if (test != null) test.Text = "TEST MIC";
            if (status != null) { status.Text = "VOICE FX OFF  •  TEST MIC IS SILENT"; status.ForeColor = Theme.ComicStyleMode ? Color.Black : Theme.Blue; }
        }
        private void UpdatePitchControls()
        {
            double semitones = pitch.Value + finePitch.Value / 100.0;
            pitchValue.Text = pitch.Value.ToString("+0;-0;0") + " semitones";
            finePitchValue.Text = finePitch.Value.ToString("+0;-0;0") + " cents";
            if (shifter != null) shifter.PitchFactor = (float)Math.Pow(2.0, semitones / 12.0);
        }

        private void OpenVirtualCablePage()
        {
            try { Process.Start(new ProcessStartInfo("https://vb-audio.com/Cable/") { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, "Visit https://vb-audio.com/Cable/ to install a virtual audio cable.\n\n" + ex.Message, "Virtual microphone setup", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }

        private void ApplyQuick(string name, int value) { pitch.Value = Math.Max(pitch.Minimum, Math.Min(pitch.Maximum, value)); finePitch.Value = 0; }
        private void SavePreset()
        {
            string name=Prompt.Show(this,"Preset name:","Save Voice Preset","My Voice"); if(string.IsNullOrWhiteSpace(name)) return;
            var old=saved.FirstOrDefault(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase)); if(old!=null) saved.Remove(old);
            saved.Add(new VoicePreset { Name=name.Trim(), Pitch=pitch.Value, FinePitchCents=finePitch.Value }); SavePresets(); RefreshPresets(); presets.SelectedItem=saved.FirstOrDefault(p=>p.Name==name.Trim());
        }
        private void ApplyPreset() { var p=presets.SelectedItem as VoicePreset; if(p!=null) { pitch.Value=Math.Max(pitch.Minimum,Math.Min(pitch.Maximum,p.Pitch)); finePitch.Value=Math.Max(finePitch.Minimum,Math.Min(finePitch.Maximum,p.FinePitchCents)); } }
        private void DeletePreset() { var p=presets.SelectedItem as VoicePreset; if(p==null)return; if(MessageBox.Show(this,"Delete preset '"+p.Name+"'?","Delete preset",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return; saved.Remove(p); SavePresets(); RefreshPresets(); }
        private void RefreshPresets() { presets.Items.Clear(); foreach(var p in saved.OrderBy(p=>p.Name)) presets.Items.Add(p); if(presets.Items.Count>0)presets.SelectedIndex=0; }
        private void LoadPresets() { try { if(File.Exists(PresetPath)) using(var f=File.OpenRead(PresetPath)) saved.AddRange((List<VoicePreset>)new XmlSerializer(typeof(List<VoicePreset>)).Deserialize(f)); } catch { saved.Clear(); } }
        private void SavePresets() { try { Directory.CreateDirectory(Path.GetDirectoryName(PresetPath)); using(var f=File.Create(PresetPath)) new XmlSerializer(typeof(List<VoicePreset>)).Serialize(f,saved); } catch(Exception ex) { MessageBox.Show(this,"Could not save presets: "+ex.Message); } }
        private sealed class DeviceItem
        {
            public string Id { get; private set; }
            public string Name { get; private set; }
            public bool IsGoXlr { get { return LooksLikeGoXlr(Name); } }
            public bool IsCableInput
            {
                get { return Name.IndexOf("cable input", StringComparison.OrdinalIgnoreCase) >= 0; }
            }
            public bool IsVoiceMeeterInput
            {
                get { return Name.IndexOf("voicemeeter input", StringComparison.OrdinalIgnoreCase) >= 0 || Name.IndexOf("voicemeeter aux input", StringComparison.OrdinalIgnoreCase) >= 0; }
            }
            public bool IsVirtualRoute
            {
                get
                {
                    return IsCableInput || IsVoiceMeeterInput;
                }
            }
            public DeviceItem(string id, string name) { Id = id; Name = name; }
            public override string ToString()
            {
                if (IsGoXlr) return "[GoXLR] " + Name;
                if (IsVirtualRoute) return "[VIRTUAL CABLE ROUTE] " + Name;
                return Name;
            }
        }
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
            using (var track = new SolidBrush(Theme.ComicStyleMode ? Color.White : Color.FromArgb(38, 43, 68)))
            using (var p = new System.Drawing.Drawing2D.GraphicsPath())
            { p.AddArc(left, cy - 3, 6, 6, 90, 180); p.AddArc(right - 6, cy - 3, 6, 6, 270, 180); p.CloseFigure(); g.FillPath(track, p); if (Theme.ComicStyleMode) using (var ink = new Pen(Color.Black, 2f)) g.DrawPath(ink, p); }
            float pct = Maximum == Minimum ? 0f : (Value - Minimum) / (float)(Maximum - Minimum);
            float x = left + (right - left) * pct;
            if (x > left + 1f)
            { using (var fill = new System.Drawing.Drawing2D.LinearGradientBrush(new PointF(left, cy), new PointF(x, cy), Theme.ComicStyleMode ? Color.Black : Color.FromArgb(255, 91, 177), Theme.ComicStyleMode ? Color.FromArgb(210, 210, 210) : Accent)) g.FillRectangle(fill, left, cy - 2f, x - left, 4f); }
            int ticks = Math.Max(2, (Maximum - Minimum) / Math.Max(1, TickFrequency));
            for (int i = 0; i <= ticks; i++) { float tx = left + (right - left) * i / ticks; using (var p = new Pen(Theme.ComicStyleMode ? Color.Black : Color.FromArgb(95, 160, 175, 220), Theme.ComicStyleMode ? 1.5f : 1f)) g.DrawLine(p, tx, cy + 7f, tx, cy + 10f); }
            using (var glow = new SolidBrush(Theme.ComicStyleMode ? Color.FromArgb(45, 0, 0, 0) : Color.FromArgb(40, Accent.R, Accent.G, Accent.B))) g.FillEllipse(glow, x - 14f, cy - 14f, 28f, 28f);
            using (var edge = new Pen(Theme.ComicStyleMode ? Color.Black : Accent, Theme.ComicStyleMode ? 3f : 2f)) g.DrawEllipse(edge, x - 8f, cy - 8f, 16f, 16f);
            using (var thumb = new SolidBrush(Theme.ComicStyleMode ? Color.Black : Color.FromArgb(240, 244, 255))) g.FillEllipse(thumb, x - 5f, cy - 5f, 10f, 10f);
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

    /// <summary>Compact animated input-level indicator for the silent microphone test and live monitor.</summary>
    internal sealed class MicLevelMeter : Control
    {
        private float level;
        public Color Accent { get; set; } = Color.FromArgb(76, 141, 255);
        public float Level
        {
            get { return level; }
            set { level = Math.Max(0f, Math.Min(1f, value)); Invalidate(); }
        }

        public MicLevelMeter()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(8, 12, 22);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 2, Math.Max(1, Width - 1), Math.Max(8, Height - 4));
            using (var bg = new SolidBrush(Theme.ComicStyleMode ? Color.White : Color.FromArgb(22, 29, 48)))
            using (var path = Theme.RoundedRect(track, track.Height / 2f))
            {
                g.FillPath(bg, path);
                if (Theme.ComicStyleMode) using (var ink = new Pen(Color.Black, 2.2f)) g.DrawPath(ink, path);
            }

            const int gap = 3;
            int count = Math.Max(1, (track.Width + gap) / 9);
            int segmentWidth = Math.Max(2, (track.Width - (count - 1) * gap) / count);
            int active = (int)Math.Round(level * count);
            for (int i = 0; i < count; i++)
            {
                int x = 4 + i * (segmentWidth + gap);
                if (x + segmentWidth > Width - 2) break;
                Color c = i >= active
                    ? (Theme.ComicStyleMode ? Color.FromArgb(215, 215, 215) : Color.FromArgb(37, 48, 72))
                    : (Theme.ComicStyleMode
                        ? (i > count * 0.82f ? Color.Black : (i > count * 0.62f ? Color.FromArgb(100, 100, 100) : Color.Black))
                        : (i > count * 0.82f ? Color.FromArgb(255, 98, 89) : (i > count * 0.62f ? Color.FromArgb(255, 184, 90) : Accent)));
                using (var p = Theme.RoundedRect(new RectangleF(x, 5, segmentWidth, Math.Max(4, Height - 10)), 3f))
                using (var b = new SolidBrush(c)) g.FillPath(b, p);
            }
        }
    }

    /// <summary>Serializable user-saved voice pitch preset.</summary>
    public sealed class VoicePreset
    {
        public string Name { get; set; }
        public int Pitch { get; set; }
        public int FinePitchCents { get; set; }
        public override string ToString() { return Name; }
    }
}
