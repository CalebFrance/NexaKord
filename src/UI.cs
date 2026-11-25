// NexaKord v3 - App: teach wizard, main window, Program.Main.
// Made by Caleb France - By Canexa Studios

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GamepadMidi
{
    // ------------------------------------------------------------------ "Teach me your buttons" wizard
    class TeachForm : Form
    {
        Engine eng;
        List<Pad> steps;
        int idx;
        PadView view;
        Label lblPrompt, lblCount;
        System.Windows.Forms.Timer tm;
        RawState neutral, basel;
        bool waitRelease;

        public TeachForm(Engine e, PadStyle style, List<Pad> pads, List<Knob> knobs)
        {
            eng = e;
            steps = pads;
            Text = "Teach me your controller";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg0;
            ForeColor = Theme.Text;
            ClientSize = new Size(620, 440);
            Font = Theme.F(9f, FontStyle.Regular);

            lblCount = new Label(); lblCount.SetBounds(10, 8, 600, 20); lblCount.ForeColor = Theme.Dim; Controls.Add(lblCount);
            lblPrompt = new Label(); lblPrompt.SetBounds(10, 30, 600, 48); lblPrompt.ForeColor = Theme.Amber;
            lblPrompt.Font = Theme.F(20f, FontStyle.Bold); Controls.Add(lblPrompt);

            view = new PadView();
            view.SetBounds(10, 85, 600, 300);
            view.ReadOnlyView = true;
            view.SetLayout(style, pads, knobs);
            Controls.Add(view);

            PopButton btnSkip = new PopButton(); btnSkip.Text = "Skip this one"; btnSkip.SetBounds(10, 396, 130, 32); btnSkip.Accent = Theme.Violet; btnSkip.Ghost = true;
            btnSkip.Click += delegate { idx++; Begin(); };
            Controls.Add(btnSkip);
            PopButton btnCancel = new PopButton(); btnCancel.Text = "Finish later"; btnCancel.SetBounds(480, 396, 130, 32); btnCancel.Accent = Theme.Pink;
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            tm = new System.Windows.Forms.Timer();
            tm.Interval = 15;
            tm.Tick += Tick;
            Shown += delegate
            {
                eng.SetPaused(true);
                neutral = eng.Snapshot();
                idx = 0;
                Begin();
                tm.Start();
            };
            FormClosing += delegate { tm.Stop(); eng.SetPaused(false); };
        }

        void Begin()
        {
            if (idx >= steps.Count) { DialogResult = DialogResult.OK; Close(); return; }
            Pad p = steps[idx];
            lblPrompt.Text = p.Verb + " the " + p.Name + " now";
            lblCount.Text = "Step " + (idx + 1) + " of " + steps.Count + "   (click 'Skip' if your controller doesn't have this one)";
            view.Highlight = p.ShowAs != null ? p.ShowAs : p;
            basel = eng.Snapshot();
            waitRelease = false;
            view.Invalidate();
        }

        bool IsNeutral(RawState s)
        {
            if (s.Buttons != 0) return false;
            for (int i = 0; i < 4; i++) if (s.Dirs[i]) return false;
            for (int i = 0; i < 6; i++) if (Math.Abs(s.Axes[i] - neutral.Axes[i]) > 0.3f) return false;
            return true;
        }

        static Source Detect(RawState b, RawState c)
        {
            uint nb = c.Buttons & ~b.Buttons;
            if (nb != 0)
                for (int i = 0; i < 32; i++) if (((nb >> i) & 1u) != 0) return new Source(SrcType.Button, i, 0);
            for (int d = 0; d < 4; d++) if (c.Dirs[d] && !b.Dirs[d]) return new Source(SrcType.Pov, d, 0);
            int best = -1; float bestAbs = 0.5f;
            for (int a = 0; a < 6; a++)
            {
                float delta = c.Axes[a] - b.Axes[a];
                if (Math.Abs(delta) > bestAbs) { bestAbs = Math.Abs(delta); best = a; }
            }
            if (best >= 0) return new Source(SrcType.Axis, best, (c.Axes[best] - b.Axes[best]) > 0 ? 1 : -1);
            return null;
        }

        void Tick(object sender, EventArgs ev)
        {
            view.Invalidate();
            if (!eng.Connected) { lblCount.Text = "I lost the controller. Check the cable or Bluetooth."; return; }
            RawState s = eng.Snapshot();
            if (waitRelease)
            {
                if (IsNeutral(s)) { idx++; Begin(); }
                return;
            }
            Source found = Detect(basel, s);
            if (found != null)
            {
                lock (eng.Lock) { steps[idx].Src = found; }
                lblPrompt.Text = "Got it!  Let go...";
                waitRelease = true;
            }
        }
    }

    // ------------------------------------------------------------------ Settings pane

    // ------------------------------------------------------------------ Settings pane
    class SettingsForm : Form
    {
        MainForm main;

        public SettingsForm(MainForm m)
        {
            main = m;
            Text = "NexaKord Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Theme.Bg0;
            ForeColor = Theme.Text;
            ClientSize = new Size(400, 520);
            Font = Theme.F(9f, FontStyle.Regular);

            this.Location = new Point(main.Location.X + 20, main.Location.Y + 120);

            int y = 20;

            // --- Profiles ---
            Label lblProf = new Label(); lblProf.Text = "USER PROFILES"; lblProf.SetBounds(20, y, 360, 16); lblProf.ForeColor = Theme.Cyan; lblProf.Font = Theme.F(10f, FontStyle.Bold); Controls.Add(lblProf); y += 24;

            ComboBox cbProf = new ComboBox(); cbProf.SetBounds(20, y, 160, 24); cbProf.DropDownStyle = ComboBoxStyle.DropDownList; cbProf.BackColor = Theme.Card2; cbProf.ForeColor = Theme.Text;
            main.PopulateProfileCombo(cbProf);
            cbProf.SelectedIndexChanged += delegate { main.LoadTpl(cbProf.SelectedItem.ToString()); };
            Controls.Add(cbProf);

            PopButton btnSave = new PopButton(); btnSave.Text = "Save As"; btnSave.SetBounds(190, y, 70, 24); btnSave.Accent = Theme.Violet; btnSave.Click += delegate { main.SaveAs(); main.PopulateProfileCombo(cbProf); }; Controls.Add(btnSave);
            PopButton btnNew = new PopButton(); btnNew.Text = "New"; btnNew.SetBounds(265, y, 50, 24); btnNew.Accent = Theme.Violet; btnNew.Click += delegate { main.NewBlank(); main.PopulateProfileCombo(cbProf); }; Controls.Add(btnNew);
            PopButton btnDel = new PopButton(); btnDel.Text = "Del"; btnDel.SetBounds(320, y, 50, 24); btnDel.Accent = Theme.Pink; btnDel.Click += delegate { main.DeleteProfile(); main.PopulateProfileCombo(cbProf); }; Controls.Add(btnDel);
            y += 40;

            // --- Controller ---
            Label lblCtrl = new Label(); lblCtrl.Text = "CONTROLLER & MIDI"; lblCtrl.SetBounds(20, y, 360, 16); lblCtrl.ForeColor = Theme.Cyan; lblCtrl.Font = Theme.F(10f, FontStyle.Bold); Controls.Add(lblCtrl); y += 24;

            main.cbDev = new ComboBox(); main.cbDev.SetBounds(20, y, 180, 24); main.cbDev.DropDownStyle = ComboBoxStyle.DropDownList; main.cbDev.BackColor = Theme.Card2; main.cbDev.ForeColor = Theme.Text;
            main.cbDev.SelectedIndexChanged += main.OnDevPicked; Controls.Add(main.cbDev);

            PopButton btnScan = new PopButton(); btnScan.Text = "Scan"; btnScan.SetBounds(210, y, 50, 24); btnScan.Accent = Theme.Lime; btnScan.Click += delegate { main.ForceScan(); }; Controls.Add(btnScan);

            main.cbStyle = new ComboBox(); main.cbStyle.SetBounds(270, y, 100, 24); main.cbStyle.DropDownStyle = ComboBoxStyle.DropDownList; main.cbStyle.BackColor = Theme.Card2; main.cbStyle.ForeColor = Theme.Text;
            main.cbStyle.Items.AddRange(new object[] { "PS Classic", "PS4", "PS5", "Xbox", "Generic" });
            main.cbStyle.SelectedIndexChanged += main.OnStylePicked; Controls.Add(main.cbStyle);
            y += 30;

            main.cbMidi = new ComboBox(); main.cbMidi.SetBounds(20, y, 180, 24); main.cbMidi.DropDownStyle = ComboBoxStyle.DropDownList; main.cbMidi.BackColor = Theme.Card2; main.cbMidi.ForeColor = Theme.Text;
            main.cbMidi.SelectedIndexChanged += main.OnMidiPicked; Controls.Add(main.cbMidi);

            PopButton btnTest = new PopButton(); btnTest.Text = "Test MIDI"; btnTest.SetBounds(210, y, 80, 24); btnTest.Accent = Theme.Amber; btnTest.Click += delegate { main.TestSound(); }; Controls.Add(btnTest);

            PopButton btnMap = new PopButton(); btnMap.Text = "Map Controller"; btnMap.SetBounds(295, y, 85, 24); btnMap.Accent = Theme.Cyan; btnMap.Click += delegate { main.RunTeach(); }; Controls.Add(btnMap);
            y += 40;

            // --- Channel & Note Names ---
            Label lblChan = new Label(); lblChan.Text = "MIDI CHANNEL"; lblChan.SetBounds(20, y, 100, 16); Controls.Add(lblChan);
            main.nudChan = new NumericUpDown(); main.nudChan.SetBounds(120, y - 3, 50, 24); main.nudChan.Minimum = 1; main.nudChan.Maximum = 16; main.nudChan.BackColor = Theme.Card2; main.nudChan.ForeColor = Theme.Text;
            int ch = 1; int.TryParse(main.st.Get("channel", "1"), out ch); main.nudChan.Value = ch;
            main.nudChan.ValueChanged += delegate { main.eng.Channel = (int)main.nudChan.Value - 1; main.st.Set("channel", ((int)main.nudChan.Value).ToString()); };
            Controls.Add(main.nudChan);

            main.chkFL = new CheckBox(); main.chkFL.Text = "FL Studio Note Names (middle C = C5)"; main.chkFL.SetBounds(190, y - 2, 200, 22); main.chkFL.ForeColor = Theme.Text;
            main.chkFL.Checked = Music.FL; main.chkFL.CheckedChanged += delegate { Music.FL = main.chkFL.Checked; main.st.Set("flNames", Music.FL ? "1" : "0"); main.view.Invalidate(); main.piano.Invalidate(); };
            Controls.Add(main.chkFL);
            y += 40;

            // --- System ---
            Label lblSys = new Label(); lblSys.Text = "SYSTEM"; lblSys.SetBounds(20, y, 100, 16); lblSys.ForeColor = Theme.Cyan; lblSys.Font = Theme.F(10f, FontStyle.Bold); Controls.Add(lblSys); y += 24;

            main.chkStart = new CheckBox(); main.chkStart.Text = "Start with Windows"; main.chkStart.SetBounds(20, y, 160, 22); main.chkStart.ForeColor = Theme.Text;
            main.chkStart.Checked = MainForm.GetAutoStart(); main.chkStart.CheckedChanged += delegate { MainForm.SetAutoStart(main.chkStart.Checked); };
            Controls.Add(main.chkStart);

            main.chkTray = new CheckBox(); main.chkTray.Text = "Keep running when closed"; main.chkTray.SetBounds(190, y, 200, 22); main.chkTray.ForeColor = Theme.Text;
            main.chkTray.Checked = main.st.Get("tray", "1") == "1"; main.chkTray.CheckedChanged += delegate { main.st.Set("tray", main.chkTray.Checked ? "1" : "0"); };
            Controls.Add(main.chkTray);
            y += 40;

            // --- Arp Settings ---
            Label lblArp = new Label(); lblArp.Text = "ARPEGGIATOR"; lblArp.SetBounds(20, y, 150, 16); lblArp.ForeColor = Theme.Cyan; lblArp.Font = Theme.F(10f, FontStyle.Bold); Controls.Add(lblArp); y += 24;

            // Speed Multiplier
            Label lblArpSpd = new Label(); lblArpSpd.Text = "Arp Speed"; lblArpSpd.SetBounds(20, y, 80, 16); Controls.Add(lblArpSpd);
            PopSlider sldArpSpd = new PopSlider(); sldArpSpd.Min = 10; sldArpSpd.Max = 100; sldArpSpd.Value = (int)(main.eng.ArpSpeedMultiplier * 10); sldArpSpd.SetBounds(100, y - 4, 150, 24); sldArpSpd.Accent = Theme.Cyan;
            Label lblArpSpdVal = new Label(); lblArpSpdVal.Text = main.eng.ArpSpeedMultiplier.ToString("0.0") + "x"; lblArpSpdVal.SetBounds(260, y, 60, 20); lblArpSpdVal.ForeColor = Theme.Text;
            sldArpSpd.ValueChanged += delegate {
                main.eng.ArpSpeedMultiplier = sldArpSpd.Value / 10f;
                lblArpSpdVal.Text = main.eng.ArpSpeedMultiplier.ToString("0.0") + "x";
            };
            Controls.Add(sldArpSpd); Controls.Add(lblArpSpdVal); y += 28;

            // Swing
            Label lblArpSw = new Label(); lblArpSw.Text = "Arp Swing"; lblArpSw.SetBounds(20, y, 80, 16); Controls.Add(lblArpSw);
            PopSlider sldArpSw = new PopSlider(); sldArpSw.Min = 0; sldArpSw.Max = 100; sldArpSw.Value = (int)main.eng.ArpSwing; sldArpSw.SetBounds(100, y - 4, 150, 24); sldArpSw.Accent = Theme.Pink;
            Label lblArpSwVal = new Label(); lblArpSwVal.Text = (int)main.eng.ArpSwing + "%"; lblArpSwVal.SetBounds(260, y, 60, 20); lblArpSwVal.ForeColor = Theme.Text;
            sldArpSw.ValueChanged += delegate { main.eng.ArpSwing = sldArpSw.Value; lblArpSwVal.Text = (int)main.eng.ArpSwing + "%"; };
            Controls.Add(sldArpSw); Controls.Add(lblArpSwVal); y += 35;

            // Arp Curve Selection
            Label lblCurve = new Label(); lblCurve.Text = "SPEED CURVE"; lblCurve.SetBounds(20, y, 100, 16); lblCurve.ForeColor = Theme.Cyan; Controls.Add(lblCurve); y += 24;

            ComboBox cbCurve = new ComboBox(); cbCurve.SetBounds(20, y, 200, 24); cbCurve.DropDownStyle = ComboBoxStyle.DropDownList; cbCurve.BackColor = Theme.Card2; cbCurve.ForeColor = Theme.Text;
            cbCurve.Items.AddRange(new object[] { "Linear (Steady)", "Log (Decelerate)", "Inverted Log (Accelerate)", "Sine (Tension)" });
            cbCurve.SelectedIndex = main.eng.ArpCurve;
            cbCurve.SelectedIndexChanged += delegate { main.eng.ArpCurve = cbCurve.SelectedIndex; };
            Controls.Add(cbCurve);
            y += 30;

            // Curve Intensity Slider
            Label lblInt = new Label(); lblInt.Text = "Curve Intensity"; lblInt.SetBounds(20, y, 100, 16); Controls.Add(lblInt);
            PopSlider sldInt = new PopSlider(); sldInt.Min = 0; sldInt.Max = 100; sldInt.Value = (int)main.eng.ArpCurveIntensity; sldInt.SetBounds(120, y - 4, 150, 24); sldInt.Accent = Theme.Amber;
            Label lblIntVal = new Label(); lblIntVal.Text = sldInt.Value + "%"; lblIntVal.SetBounds(280, y, 50, 20); lblIntVal.ForeColor = Theme.Text;
            sldInt.ValueChanged += delegate { main.eng.ArpCurveIntensity = sldInt.Value; lblIntVal.Text = sldInt.Value + "%"; };
            Controls.Add(sldInt); Controls.Add(lblIntVal);
            // Add a close button
            PopButton btnClose = new PopButton(); btnClose.Text = "Close"; btnClose.SetBounds(250, 460, 100, 32); btnClose.Accent = Theme.Pink; btnClose.Ghost = true;
            btnClose.Click += delegate { this.Close(); }; Controls.Add(btnClose);

            // Wait 200ms for the main form to finish scanning devices, then populate.
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 200;
            t.Tick += delegate {
                t.Stop();
                main.ForceScan();
                main.ForcePortScan();
                cbProf.Items.Clear();
                main.PopulateProfileCombo(cbProf);
            };
            t.Start();
        }

        protected override bool ShowWithoutActivation { get { return true; } }
    }

    // ------------------------------------------------------------------ main window
    class MainForm : Form
        {

            PopButton btnSettings;
            SettingsForm activeSettings; // <--- ADD THIS
                                         // --- TOUCHPAD MOUSE SIMULATION ---
            System.Windows.Forms.Timer touchpadTimer;
            Point lastTouchPos;
            bool touchpadActive = false;

            public Settings st;
            public Engine eng;
            PadStyle cur = PadStyle.Classic;
            Template tpl;
            string tplName = "Default";

            public PadView view;
            public PianoView piano;
            GuitarView guitar;
            InfoCard info;
            HiView hiView;
            DirCard dirCard;

            public ComboBox cbDev, cbStyle, cbMidi, cbProf, cbAction, cbChord, cbKType, cbRest, cbKey, cbScale, cbJoy, cbBass;
            PopButton btnRescan, btnTest, btnTeach, btnHelp, btnGet, btnWiggle, btnModePad, btnModeHi, btnMonitor, btnTranspose, btnLearn;
            PopButton btnSaveAs, btnNewBlank, btnSetDef, btnDelProf, btnOctM, btnOctP;


            Label lblPadSt, lblMidiSt, lblTitle, lblNone, lblChord, lblVel, lblHint, lblKDesc, lblCC, lblSpeed, lblRest, lblKVal, lblOct, lblOctV, lblHiVel, lblLegend;
            CardPanel pnlRight;
            Panel pnlPad, pnlKnob, pnlNone, pnlHi;
            PopSlider sldVel, sldSpeed, sldHiVel;
            public NumericUpDown nudCC, nudChan;
            public CheckBox chkRev, chkFL, chkStart, chkTray, chkVLead;
            NotifyIcon tray;
            System.Windows.Forms.Timer uiTimer, slowTimer, prevTimer, wigTimer;
            bool loading, quit, startHidden, profDirty, monitorOn;
            int hiSig = -1;
            List<DevInfo> devs = new List<DevInfo>();
            string devKey = null;
            List<string> ports = new List<string>();
            string portKey = null;
            int[] preview = new int[0];
            Knob wigKnob;
            int wigStep;
            static readonly int[] KindOfIndex = { 1, 2, 3, 4, 0 };
            static readonly string[] KeyNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            const string LoopMidiUrl = "https://www.tobias-erichsen.de/software/loopmidi.html";

            public void PopulateProfileCombo(ComboBox cb)
            {
                cb.Items.Clear();
                foreach (string n in Templates.List()) cb.Items.Add(n);
                int i = cb.Items.IndexOf(tplName);
                if (i >= 0) cb.SelectedIndex = i;
            }
            public void ForceScan() { devKey = null; RefreshDevices(); }
            public void ForcePortScan() { portKey = null; RefreshPorts(); }

            public MainForm(bool hidden)
            {
                startHidden = hidden;
                Text = "NexaKord";
                AutoScaleMode = AutoScaleMode.None;
                FormBorderStyle = FormBorderStyle.FixedSingle;
                MaximizeBox = false;
                ClientSize = new Size(1100, 700);
                BackColor = Theme.Bg0;
                ForeColor = Theme.Text;
                Font = Theme.F(9f, FontStyle.Regular);
                DoubleBuffered = true;
                Icon = MakeIcon();

                st = new Settings();
                Templates.EnsureDefault(st);
                eng = new Engine();
                Music.FL = st.Get("flNames", "1") == "1";

                BuildHeader();
                BuildMain();
                BuildBottom();


                tray = new NotifyIcon();
                tray.Icon = Icon; tray.Text = "NexaKord (running)"; tray.Visible = true;
                ContextMenuStrip cm = new ContextMenuStrip();
                cm.Items.Add("Open NexaKord", null, delegate { ShowMe(); });
                cm.Items.Add("Quit", null, delegate { quit = true; Application.Exit(); });
                tray.ContextMenuStrip = cm;
                tray.DoubleClick += delegate { ShowMe(); };

                uiTimer = new System.Windows.Forms.Timer(); uiTimer.Interval = 40; uiTimer.Tick += OnUiTick; uiTimer.Start();
                slowTimer = new System.Windows.Forms.Timer(); slowTimer.Interval = 1500; slowTimer.Tick += OnSlowTick; slowTimer.Start();
                prevTimer = new System.Windows.Forms.Timer(); prevTimer.Interval = 300; prevTimer.Tick += delegate { prevTimer.Stop(); StopPreview(); };
                wigTimer = new System.Windows.Forms.Timer(); wigTimer.Interval = 15; wigTimer.Tick += OnWigTick;
                // Touchpad Mouse Simulation Timer
                touchpadTimer = new System.Windows.Forms.Timer();
                touchpadTimer.Interval = 16; // ~60 FPS
                touchpadTimer.Tick += delegate
                {
                    // NOTE: This will only work once we swap to JoyShockLibrary
                    // For now, it checks if the touchpad button is pressed, 
                    // but it cannot read the X/Y coordinates.
                    if (eng.Dev != null && eng.Hi.On)
                    {
                        // Placeholder: Once RawState has TouchX/Y, we do:
                        // float tx = eng.Snapshot().TouchX; 
                        // float ty = eng.Snapshot().TouchY;
                        // Cursor.Position = new Point((int)(tx * Screen.PrimaryScreen.Bounds.Width), (int)(ty * Screen.PrimaryScreen.Bounds.Height));
                    }
                };
                touchpadTimer.Start();

                tplName = st.Get("profile", "Default");
                if (!Templates.Exists(tplName)) tplName = "Default";
                tpl = Templates.Load(tplName);
                if (tpl == null) { tpl = Templates.Factory(); tpl.Name = tplName; }
                RefreshProfiles(tplName);
                ApplyTemplate();
                eng.Start();
                Shown += delegate { RefreshPorts(); RefreshDevices(); };
            }

            // ================================================================ UI construction
            static Label Lbl(Control parent, string text, int x, int y, int w, int h)
            {
                Label l = new Label();
                l.Text = text; l.SetBounds(x, y, w, h);
                l.ForeColor = Theme.Dim; l.BackColor = Color.Transparent; l.Font = Theme.F(9f, FontStyle.Regular);
                parent.Controls.Add(l);
                return l;
            }

            static ComboBox Combo(Control parent, int x, int y, int w)
            {
                ComboBox c = new ComboBox();
                c.DropDownStyle = ComboBoxStyle.DropDownList;
                c.FlatStyle = FlatStyle.Flat;
                c.SetBounds(x, y, w, 24);
                c.BackColor = Theme.Card2; c.ForeColor = Theme.Text; c.Font = Theme.F(9f, FontStyle.Regular);
                parent.Controls.Add(c);
                return c;
            }

            static PopButton Btn(Control parent, string text, int x, int y, int w, int h, Color accent)
            {
                PopButton b = new PopButton();
                b.Text = text; b.SetBounds(x, y, w, h); b.Accent = accent;
                parent.Controls.Add(b);
                return b;
            }

            static PopSlider Sld(Control parent, int x, int y, int w, int min, int max, Color accent)
            {
                PopSlider s = new PopSlider();
                s.Min = min; s.Max = max; s.Accent = accent; s.SetBounds(x, y, w, 24);
                parent.Controls.Add(s);
                return s;
            }

            void BuildHeader()
            {
                btnModePad = Btn(this, "Pad mode", 14, 54, 100, 32, Theme.Cyan); btnModePad.Ghost = true;
                btnModePad.Click += delegate { SetHiMode(false); };
                btnModeHi = Btn(this, "HiChord", 118, 54, 100, 32, Theme.Pink); btnModeHi.Ghost = true;
                btnModeHi.Click += delegate { SetHiMode(true); };

                btnMonitor = Btn(this, "Monitor", 222, 54, 80, 32, Theme.Amber); btnMonitor.Ghost = true;
                btnMonitor.Click += delegate { monitorOn = !monitorOn; ApplyModeUi(); };

                btnLearn = Btn(this, "Learn", 306, 54, 60, 32, Theme.Lime); btnLearn.Ghost = true;
                btnLearn.Click += delegate { using (LearnForm lf = new LearnForm(eng, cur)) lf.ShowDialog(this); };

                btnSettings = Btn(this, "Settings", 852, 54, 80, 32, Theme.Violet); btnSettings.Ghost = true;
                btnSettings.Click += delegate
                {
                    if (activeSettings == null || activeSettings.IsDisposed)
                    {
                        // Force the scan BEFORE the window opens
                        portKey = null; // Force the cache to clear
                        RefreshPorts();
                        devKey = null;
                        RefreshDevices();

                        activeSettings = new SettingsForm(this);
                        activeSettings.Show(this);
                    }
                    else
                    {
                        activeSettings.BringToFront();
                    }
                };

                btnHelp = Btn(this, "Help", 938, 54, 60, 32, Theme.Lime); btnHelp.Ghost = true;
                btnHelp.Click += delegate { ShowHelp(); };

                // Status labels
                lblPadSt = Lbl(this, "", 14, 90, 500, 14); lblPadSt.Font = Theme.F(8.5f, FontStyle.Bold);
                lblMidiSt = Lbl(this, "", 520, 90, 440, 14); lblMidiSt.Font = Theme.F(8.5f, FontStyle.Bold);
            }

            void BuildMain()
            {
                view = new PadView();
                view.SetBounds(10, 114, 600, 300);
                view.SelectionChanged += OnSel;
                Controls.Add(view);

                info = new InfoCard();
                info.SetBounds(10, 420, 600, 124);
                Controls.Add(info);

                hiView = new HiView();
                hiView.Eng = eng;
                hiView.SetBounds(10, 114, 600, 430);
                hiView.Visible = false;
                Controls.Add(hiView);

                pnlRight = new CardPanel();
                pnlRight.SetBounds(620, 114, 470, 430);
                pnlRight.BackColor = Color.Transparent;
                Controls.Add(pnlRight);
                lblTitle = Lbl(pnlRight, "", 14, 12, 440, 28);
                lblTitle.Font = Theme.F(14f, FontStyle.Bold); lblTitle.ForeColor = Theme.Text;

                // ---- pad mode editors
                pnlPad = new Panel(); pnlPad.SetBounds(0, 48, 470, 380); pnlPad.BackColor = Color.Transparent; pnlRight.Controls.Add(pnlPad);
                Lbl(pnlPad, "What should it do?", 14, 0, 300, 16);
                cbAction = Combo(pnlPad, 14, 18, 440);
                cbAction.Items.AddRange(new object[] { "Play one note", "Play a chord", "Octave down", "Octave up", "Do nothing" });
                cbAction.SelectedIndexChanged += OnActionChanged;
                lblChord = Lbl(pnlPad, "Chord type", 14, 56, 300, 16);
                cbChord = Combo(pnlPad, 14, 74, 440);
                cbChord.Items.AddRange(Music.ChordNames);
                cbChord.SelectedIndexChanged += OnChordChanged;
                lblVel = Lbl(pnlPad, "Loudness", 14, 112, 300, 16);
                sldVel = Sld(pnlPad, 14, 130, 440, 1, 127, Theme.Cyan);
                sldVel.ValueChanged += OnVelChanged;
                lblHint = Lbl(pnlPad, "", 14, 168, 440, 70); lblHint.ForeColor = Theme.Amber; lblHint.Font = Theme.F(9.5f, FontStyle.Regular);

                pnlKnob = new Panel(); pnlKnob.SetBounds(0, 48, 470, 380); pnlKnob.BackColor = Color.Transparent; pnlRight.Controls.Add(pnlKnob);
                lblKDesc = Lbl(pnlKnob, "", 14, 0, 440, 32);
                cbKType = Combo(pnlKnob, 14, 34, 440);
                cbKType.Items.AddRange(new object[] { "Knob / slider (CC number)", "Pitch bend" });
                cbKType.SelectedIndexChanged += OnKnobChanged;
                lblCC = Lbl(pnlKnob, "CC number", 14, 74, 90, 16);
                nudCC = new NumericUpDown(); nudCC.SetBounds(104, 70, 60, 24); nudCC.Minimum = 0; nudCC.Maximum = 127;
                nudCC.BackColor = Theme.Card2; nudCC.ForeColor = Theme.Text; nudCC.BorderStyle = BorderStyle.FixedSingle;
                nudCC.ValueChanged += OnKnobChanged; pnlKnob.Controls.Add(nudCC);
                chkRev = new CheckBox(); chkRev.Text = "Reverse"; chkRev.SetBounds(200, 72, 140, 22); chkRev.ForeColor = Theme.Text;
                chkRev.CheckedChanged += OnKnobChanged; pnlKnob.Controls.Add(chkRev);
                lblSpeed = Lbl(pnlKnob, "Speed", 14, 108, 56, 16);
                sldSpeed = Sld(pnlKnob, 70, 104, 384, 1, 60, Theme.Lime);
                sldSpeed.ValueChanged += OnKnobChanged;
                lblRest = Lbl(pnlKnob, "When let go", 14, 142, 90, 16);
                cbRest = Combo(pnlKnob, 104, 138, 350);
                cbRest.Items.AddRange(new object[] { "Snap back to 0", "Snap back to the middle" });
                cbRest.SelectedIndexChanged += OnKnobChanged;
                lblKVal = Lbl(pnlKnob, "", 14, 174, 440, 18); lblKVal.ForeColor = Theme.Text;
                btnWiggle = Btn(pnlKnob, "Wiggle it (so FL Studio can learn it)", 14, 202, 440, 34, Theme.Pink);
                btnWiggle.Click += delegate { Wiggle(); };

                pnlNone = new Panel(); pnlNone.SetBounds(0, 48, 470, 380); pnlNone.BackColor = Color.Transparent; pnlRight.Controls.Add(pnlNone);
                lblNone = Lbl(pnlNone, "", 14, 6, 440, 130); lblNone.Font = Theme.F(10f, FontStyle.Regular);

                // ---- HiChord panel
                pnlHi = new Panel(); pnlHi.SetBounds(0, 8, 470, 420); pnlHi.BackColor = Color.Transparent; pnlRight.Controls.Add(pnlHi);
                Label hiTitle = Lbl(pnlHi, "HiChord", 14, 6, 300, 28); hiTitle.Font = Theme.F(14f, FontStyle.Bold); hiTitle.ForeColor = Theme.Pink;
                Lbl(pnlHi, "7 chords that always fit together. Stick = flavour.", 100, 13, 360, 16);
                Lbl(pnlHi, "Key", 14, 44, 60, 16);
                cbKey = Combo(pnlHi, 14, 62, 90); cbKey.Items.AddRange(KeyNames); cbKey.SelectedIndexChanged += OnHiChanged;
                Lbl(pnlHi, "Octave", 118, 44, 60, 16);
                btnOctM = Btn(pnlHi, "-", 118, 60, 30, 28, Theme.Violet); btnOctM.Click += delegate { ChangeOct(-1); };
                lblOctV = Lbl(pnlHi, "0", 150, 65, 36, 18); lblOctV.ForeColor = Theme.Text; lblOctV.TextAlign = ContentAlignment.MiddleCenter;
                btnOctP = Btn(pnlHi, "+", 188, 60, 30, 28, Theme.Violet); btnOctP.Click += delegate { ChangeOct(1); };
                Lbl(pnlHi, "Scale", 240, 44, 60, 16);
                cbScale = Combo(pnlHi, 240, 62, 214); cbScale.Items.AddRange(Theory.ScaleNames); cbScale.SelectedIndexChanged += OnHiChanged;
                Lbl(pnlHi, "Stick flavour set", 14, 98, 200, 16);
                cbJoy = Combo(pnlHi, 14, 116, 200); cbJoy.Items.AddRange(Theory.JoyNames); cbJoy.SelectedIndexChanged += OnHiChanged;
                Lbl(pnlHi, "Bass note", 240, 98, 200, 16);
                cbBass = Combo(pnlHi, 240, 116, 214); cbBass.Items.AddRange(Theory.BassNames); cbBass.SelectedIndexChanged += OnHiChanged;
                chkVLead = new CheckBox(); chkVLead.Text = "Smooth voice leading (auto inversion)"; chkVLead.SetBounds(14, 150, 340, 22); chkVLead.ForeColor = Theme.Text;
                chkVLead.CheckedChanged += OnHiChanged; pnlHi.Controls.Add(chkVLead);
                lblHiVel = Lbl(pnlHi, "Loudness: 100", 14, 180, 120, 16);
                sldHiVel = Sld(pnlHi, 130, 176, 324, 1, 127, Theme.Cyan); sldHiVel.Value = 100;
                sldHiVel.ValueChanged += OnHiVel;
                dirCard = new DirCard(); dirCard.Eng = eng; dirCard.SetBounds(14, 208, 440, 120); pnlHi.Controls.Add(dirCard);
                lblLegend = Lbl(pnlHi, "", 14, 332, 442, 86); lblLegend.Font = Theme.F(8f, FontStyle.Regular);

                piano = new PianoView();
                piano.SetBounds(10, 552, 1080, 90);
                piano.Sounding = eng.Sounding;
                piano.NoteClicked += OnPianoClicked;
                Controls.Add(piano);

                guitar = new GuitarView();
                guitar.SetBounds(10, 552, 1080, 90);
                guitar.Eng = eng;
                guitar.Visible = false;
                Controls.Add(guitar);
            }

            void BuildBottom()
            {
                btnTranspose = Btn(this, "Transpose This Shit", 14, 650, 140, 32, Theme.Lime);
                btnTranspose.Ghost = true;
                btnTranspose.Click += delegate
                {
                    guitar.Visible = !guitar.Visible;
                    piano.Visible = !guitar.Visible;
                    btnTranspose.Toggled = guitar.Visible;
                    btnTranspose.Invalidate();
                };

                PopButton btnHumanize = Btn(this, "Humanize", 160, 650, 100, 32, Theme.Amber);
                btnHumanize.Ghost = true;
                btnHumanize.Click += delegate
                {
                    eng.HumanizeOn = !eng.HumanizeOn;
                    btnHumanize.Toggled = eng.HumanizeOn;
                    btnHumanize.Invalidate();
                };

                Lbl(this, "Amount", 270, 658, 60, 16);
                PopSlider sldHuman = Sld(this, 320, 654, 100, 0, 100, Theme.Amber);
                sldHuman.Value = 30;
                sldHuman.ValueChanged += delegate { eng.HumanizeAmount = sldHuman.Value / 100f; };

                PopButton btnWireframe = Btn(this, "Wireframe", 980, 650, 80, 32, Theme.Cyan);
                btnWireframe.Ghost = true;
                btnWireframe.Click += delegate
                {
                    Theme.WireframeMode = !Theme.WireframeMode;
                    btnWireframe.Toggled = Theme.WireframeMode;
                    btnWireframe.Invalidate();

                    pnlPad.BackColor = Color.Transparent;
                    pnlKnob.BackColor = Color.Transparent;
                    pnlNone.BackColor = Color.Transparent;
                    pnlHi.BackColor = Color.Transparent;
                    view.BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0;
                    info.BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0;
                    hiView.BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0;
                    piano.BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0;
                    guitar.BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0;
                    dirCard.BackColor = Theme.WireframeMode ? Color.Black : Theme.Card;
                    pnlRight.BackColor = Theme.WireframeMode ? Color.Black : Theme.Card;
                    this.Invalidate(true);
                };

                lblOct = Lbl(this, "", 840, 680, 250, 16); lblOct.TextAlign = ContentAlignment.MiddleRight;
            }

            static System.Drawing.Icon MakeIcon()
            {
                Bitmap bmp = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (Brush b = new SolidBrush(Theme.Pink)) g.FillEllipse(b, 1, 1, 30, 30);
                    using (Brush w = new SolidBrush(Color.White))
                    {
                        g.FillRectangle(w, 6, 14, 10, 4);
                        g.FillRectangle(w, 9, 11, 4, 10);
                        g.FillEllipse(w, 19, 11, 5, 5);
                        g.FillEllipse(w, 23, 16, 5, 5);
                    }
                }
                return System.Drawing.Icon.FromHandle(bmp.GetHicon());
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                int w = ClientSize.Width;
                if (Theme.WireframeMode)
                {
                    g.Clear(Color.Black);
                }
                else
                {
                    using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, w, 106), Theme.Bg1, Theme.Bg0, 90f))
                        g.FillRectangle(lb, 0, 0, w, 106);
                }

                using (LinearGradientBrush bar = new LinearGradientBrush(new Rectangle(0, 104, w, 3), Theme.Pink, Theme.Cyan, 0f))
                    g.FillRectangle(bar, 0, 104, w, 3);
                Font f = Theme.F(20f, FontStyle.Bold);
                SizeF sz = g.MeasureString("Nexa", f);
                using (Brush tb = new SolidBrush(Theme.Text)) g.DrawString("Nexa", f, tb, 12, 8);
                using (Brush pb = new SolidBrush(Theme.Pink)) g.DrawString("Kord", f, pb, 12 + sz.Width - 4, 8);
                using (Brush db = new SolidBrush(Theme.Dim))
                {
                    g.DrawString("v3  \u00B7  controller to chords  \u00B7  by Canexa Studios", Theme.F(8f, FontStyle.Regular), db, 14, 40);
                }
            }

            protected override void SetVisibleCore(bool value)
            {
                if (startHidden) { startHidden = false; if (!IsHandleCreated) CreateHandle(); value = false; }
                base.SetVisibleCore(value);
            }

            void ShowMe() { Show(); WindowState = FormWindowState.Normal; Activate(); }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                if (!quit && e.CloseReason == CloseReason.UserClosing && chkTray.Checked)
                {
                    e.Cancel = true;
                    Hide();
                    tray.ShowBalloonTip(2500, "NexaKord", "Still running in the background. Click this icon to open it.", ToolTipIcon.Info);
                    return;
                }
                uiTimer.Stop(); slowTimer.Stop();
                StopPreview();
                SaveTpl();
                eng.Stop();
                eng.Out.Close();
                tray.Visible = false;
                tray.Dispose();
                base.OnFormClosing(e);
            }

            public static bool GetAutoStart()
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false))
                        return k != null && k.GetValue("NexaKord") != null;
                }
                catch { return false; }
            }

            public static void SetAutoStart(bool on)
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (k == null) return;
                        if (on) k.SetValue("NexaKord", "\"" + Application.ExecutablePath + "\" /minimized");
                        else k.DeleteValue("NexaKord", false);
                    }
                }
                catch { }
            }

            void UpdateTplFromEngine()
            {
                lock (eng.Lock)
                {
                    Profile.Store(tpl, cur, eng.Pads, eng.Knobs);
                    Profile.StoreHw(st, cur, eng.Pads);
                    Profile.StoreHi(tpl, eng.Hi);
                }
            }

            void SaveTpl() { UpdateTplFromEngine(); Templates.Save(tpl); st.Set("profile", tplName); st.Save(); }

            void RefreshProfiles(string select)
            {
                if (cbProf == null) return; // <--- ADD THIS LINE

                loading = true;
                cbProf.Items.Clear();
                foreach (string n in Templates.List()) cbProf.Items.Add(n);
                int i = cbProf.Items.IndexOf(select);
                if (i < 0) i = 0;
                if (cbProf.Items.Count > 0) cbProf.SelectedIndex = i;
                loading = false;
            }

            void OnProfPicked(object s, EventArgs e)
            {
                if (loading) return;
                string n = cbProf.SelectedItem as string;
                if (n == null || n == tplName) return;
                SaveTpl();
                LoadTpl(n);
            }

            public void LoadTpl(string name)
            {
                Template t = Templates.Load(name);
                if (t == null) { t = Templates.Factory(); t.Name = name; }
                tpl = t; tplName = name;
                st.Set("profile", name);
                ApplyTemplate();
                RefreshProfiles(name);
            }

            void ApplyTemplate()
            {
                bool on;
                lock (eng.Lock) { on = Profile.LoadHi(tpl, eng.Hi); }
                SwitchStyle(cur, false);
                eng.SetHi(on);
                hiSig = -1;
                ApplyModeUi();
                SyncHiUi();
            }

            public void SaveAs()
            {
                string n = PromptForm.Ask(this, "Save profile as", "Name for this profile:", "");
                if (n == null) return;
                n = Templates.Sanitize(n);
                if (n.Length == 0) { MessageBox.Show(this, "Please use letters or numbers for the name.", "Profile name"); return; }
                if (Templates.Exists(n) && MessageBox.Show(this, "A profile called '" + n + "' already exists. Replace it?", "Profile", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                UpdateTplFromEngine();
                Template t = tpl.Clone(n);
                Templates.Save(t);
                tpl = t; tplName = n;
                st.Set("profile", n);
                RefreshProfiles(n);
            }

            public void NewBlank()
            {
                string n = PromptForm.Ask(this, "New blank profile", "Name for the blank profile:", "");
                if (n == null) return;
                n = Templates.Sanitize(n);
                if (n.Length == 0) { MessageBox.Show(this, "Please use letters or numbers for the name.", "Profile name"); return; }
                if (Templates.Exists(n)) { MessageBox.Show(this, "A profile called '" + n + "' already exists. Pick another name.", "Profile name"); return; }
                SaveTpl();
                Template t = Templates.Blank();
                t.Name = n;
                Templates.Save(t);
                LoadTpl(n);
            }

            void SetDefault()
            {
                if (tplName == "Default") { MessageBox.Show(this, "You are already on the Default profile.", "Profile"); return; }
                if (MessageBox.Show(this, "Make the current notes, chords and knobs the new Default profile?\n(The old Default is replaced.)", "Set as default", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                UpdateTplFromEngine();
                Templates.Save(tpl.Clone("Default"));
                MessageBox.Show(this, "Done. 'Default' now matches '" + tplName + "'.", "Profile");
            }

            public void DeleteProfile()
            {
                if (tplName == "Default") { MessageBox.Show(this, "The Default profile can't be deleted. Use 'Set default' to change it.", "Profile"); return; }
                if (MessageBox.Show(this, "Delete the profile '" + tplName + "'?", "Delete profile", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                Templates.Delete(tplName);
                LoadTpl("Default");
            }

            void SetHiMode(bool on)
            {
                eng.SetHi(on);
                profDirty = true;
                ApplyModeUi();
            }

            void ApplyModeUi()
            {
                bool hi = eng.Hi.On;
                btnModePad.Toggled = !hi; btnModePad.Invalidate();
                btnModeHi.Toggled = hi; btnModeHi.Invalidate();
                btnMonitor.Visible = hi;
                btnMonitor.Toggled = monitorOn; btnMonitor.Invalidate();
                view.HiMode = hi;
                view.ReadOnlyView = hi;
                bool mon = hi && monitorOn;
                view.Visible = !mon;
                info.Visible = !mon;
                hiView.Visible = mon;
                pnlHi.Visible = hi;
                if (hi)
                {
                    view.Selected = null;
                    lblTitle.Visible = false;
                    pnlPad.Visible = false; pnlKnob.Visible = false; pnlNone.Visible = false;
                    piano.Root = -1; piano.Extra = new int[0];
                    lblLegend.Text = Layouts.HiLegend(cur);
                }
                else
                {
                    OnSel(null, EventArgs.Empty);
                }
                piano.Invalidate();
                view.Invalidate();
            }

            void SwitchStyle(PadStyle s, bool storeOld)
            {
                if (storeOld) UpdateTplFromEngine();
                List<Pad> pads = Layouts.BuildPads(s);
                List<Knob> knobs = Layouts.BuildKnobs(s, pads);
                Profile.LoadHw(st, s, pads);
                Profile.Load(tpl, s, pads, knobs);
                HiBind bind = Layouts.BindHi(s, pads, knobs);
                eng.SetLayout(pads, knobs, bind);
                cur = s;
                view.SetLayout(s, pads, knobs);
                view.HiMode = eng.Hi.On;
                loading = true;
                if (cbStyle != null) cbStyle.SelectedIndex = (int)s;
                loading = false;
                if (pnlHi != null) lblLegend.Text = Layouts.HiLegend(s);
                if (!eng.Hi.On) OnSel(null, EventArgs.Empty);
            }

            public void OnStylePicked(object s, EventArgs e)
            {
                if (loading) return;
                PadStyle ns = (PadStyle)cbStyle.SelectedIndex;
                if (eng.Dev != null) st.Set("style." + eng.Dev.Mid.ToString("X4") + eng.Dev.Pid.ToString("X4"), ((int)ns).ToString());
                SwitchStyle(ns, true);
            }

            void RefreshDevices()
            {
                List<DevInfo> found = Joy.Scan();
                StringBuilder kb = new StringBuilder();
                foreach (DevInfo d in found) kb.Append(d.Id).Append(d.Name).Append(';');
                if (kb.ToString() == devKey) return;
                devKey = kb.ToString();
                devs = found;
                int curId = eng.Dev != null ? eng.Dev.Id : -1;

                if (cbDev != null) // <--- ADD THIS CHECK
                {
                    loading = true;
                    cbDev.Items.Clear();
                    int sel = -1;
                    for (int i = 0; i < devs.Count; i++)
                    {
                        cbDev.Items.Add((i + 1) + ": " + devs[i].Name);
                        if (devs[i].Id == curId) sel = i;
                    }
                    if (sel < 0 && devs.Count > 0) sel = 0;
                    if (sel >= 0) cbDev.SelectedIndex = sel;
                    loading = false;
                }

                if (curId == -1 && devs.Count > 0) UseDevice(devs[0]);
                else if (eng.Dev == null) eng.SetDevice(null);

                UpdateStatus();
            }

            public void OnDevPicked(object s, EventArgs e)
            {
                if (loading) return;
                int i = cbDev.SelectedIndex;
                if (i >= 0 && i < devs.Count) UseDevice(devs[i]);
                UpdateStatus();
            }

            void UseDevice(DevInfo d)
            {
                if (eng.Dev == null || eng.Dev.Id != d.Id || eng.Dev.Name != d.Name) eng.SetDevice(d);
                PadStyle want = Joy.Detect(d);
                string ov = st.Get("style." + d.Mid.ToString("X4") + d.Pid.ToString("X4"), "");
                int ovInt;
                if (int.TryParse(ov, out ovInt) && ovInt >= 0 && ovInt <= 4) want = (PadStyle)ovInt;
                if (want != cur) SwitchStyle(want, true);
                if (st.Get((cur == PadStyle.Classic ? "classic." : "ps4.") + "asked", "") != "1" && cur == PadStyle.Classic && Visible)
                    BeginInvoke(new MethodInvoker(AskTeach));
            }

            void AskTeach()
            {
                string key = (cur == PadStyle.Classic ? "classic." : "ps4.") + "asked";
                if (st.Get(key, "") == "1") return;
                st.Set(key, "1");
                if (MessageBox.Show(this, "First time with this controller!\n\nI need to learn which button is which. It takes about 30 seconds: I'll show a picture and you press the button that lights up.\n\nReady?",
                    "Welcome", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) RunTeach();
            }

            public void RunTeach()
            {
                if (eng.Dev == null || !eng.Connected) { MessageBox.Show(this, "I can't see a controller yet. Plug it in (or pair it with Bluetooth) and try again.", "No controller"); return; }
                using (TeachForm f = new TeachForm(eng, cur, eng.Pads, eng.Knobs)) f.ShowDialog(this);
                profDirty = true;
                view.Invalidate();
            }

            static int BestPort(List<string> p)
            {
                string[] want = { "gamepadmidi", "nexakord", "loopmidi", "midi cable", "loopback", "virtual" };
                for (int w = 0; w < want.Length; w++)
                    for (int i = 0; i < p.Count; i++)
                        if (p[i].ToLowerInvariant().Contains(want[w])) return i;
                return -1;
            }

            void RefreshPorts()
            {
                List<string> p = MidiOut.List();
                string key = string.Join("|", p.ToArray());
                if (key == portKey) return;
                portKey = key; ports = p;

                if (cbMidi != null) // <--- ADD THIS CHECK
                {
                    string current = eng.Out.IsOpen ? eng.Out.Name : st.Get("midiPort", "");
                    loading = true;
                    cbMidi.Items.Clear();
                    foreach (string n in p) cbMidi.Items.Add(n);
                    int sel = p.IndexOf(current);
                    if (sel < 0) sel = BestPort(p);
                    if (sel >= 0) cbMidi.SelectedIndex = sel;
                    loading = false;
                    if (sel >= 0) OpenPort(sel); else eng.Out.Close();
                }
                UpdateStatus();
            }

            public void OnMidiPicked(object s, EventArgs e)
            {
                if (loading) return;
                if (cbMidi.SelectedIndex >= 0) OpenPort(cbMidi.SelectedIndex);
            }

            void OpenPort(int i)
            {
                eng.Out.Close();
                if (i >= 0 && i < ports.Count && eng.Out.Open(i)) { eng.Out.Name = ports[i]; st.Set("midiPort", ports[i]); }
                UpdateStatus();
            }

            void UpdateStatus()
            {
                bool padOk = eng.Dev != null && eng.Connected;
                if (padOk) lblPadSt.Text = "\u25CF Controller ready: " + eng.Dev.Name;
                else lblPadSt.Text = devs.Count == 0 ? "\u25CF No controller found. Plug it in (or pair it by Bluetooth)." : "\u25CF Pick your controller above.";
                lblPadSt.ForeColor = padOk ? Theme.Lime : Theme.Amber;
                bool midiOk = eng.Out.IsOpen;
                bool noCable = BestPort(ports) < 0;
                if (midiOk) lblMidiSt.Text = "\u25CF MIDI cable ready: " + eng.Out.Name;
                else if (noCable) lblMidiSt.Text = "\u25CF No MIDI cable yet. Click 'Get MIDI cable' \u2192";
                else lblMidiSt.Text = "\u25CF Pick the MIDI cable above.";
                lblMidiSt.ForeColor = midiOk ? Theme.Lime : Theme.Amber;
                //btnGet.Visible = !midiOk && noCable;
            }

            void OnUiTick(object s, EventArgs e)
            {
                if (!Visible || WindowState == FormWindowState.Minimized) return;
                HiState h = eng.Hi;
                bool hi = h.On;
                if (hi)
                {
                    UpdateHiLabels();
                    SyncHiUi();
                    if (monitorOn) hiView.Invalidate();
                    dirCard.Invalidate();
                }
                if (!(hi && monitorOn)) { view.Invalidate(); UpdateInfo(); }
                piano.Invalidate();

                if (guitar != null && guitar.Visible) { guitar.Eng = eng; guitar.Invalidate(); }

                if (hi) lblOct.Text = "HiChord  \u00B7  octave " + h.Oct;
                else lblOct.Text = "Octave shift: " + (eng.Octave > 0 ? "+" : "") + eng.Octave;
                Knob k = view.Selected as Knob;
                if (!hi && k != null) lblKVal.Text = "Right now: " + (k.PitchBend ? ((int)(k.Pos * 16383f + 0.5f) - 8192).ToString() : ((int)(k.Pos * 127f + 0.5f)).ToString());
            }

            void OnSlowTick(object s, EventArgs e)
            {
                RefreshDevices();
                RefreshPorts();
                UpdateStatus();
                if (profDirty) { SaveTpl(); profDirty = false; }
                else if (st.Dirty) st.Save();
            }

            void UpdateInfo()
            {
                HiState h = eng.Hi;
                if (h.On)
                {
                    int n = -1;
                    for (int i = 0; i < 7; i++)
                        if (h.Held[i] && !h.Suppressed[i] && (n < 0 || h.Stamp[i] > h.Stamp[n])) n = i;
                    string name = (n >= 0 && h.Cur[n] != null) ? h.Cur[n] : h.LastName;
                    if (string.IsNullOrEmpty(name))
                    {
                        info.Set("Press a chord button", "Hold the stick while you hold a chord to change its flavour", "",
                            "Every button is a chord from the same key, so there are no wrong notes. Try I, then V, then vi, then IV.", "HICHORD", Theme.Pink);
                        return;
                    }
                    int deg = h.LastDeg;
                    Color acc = Theme.Pink;
                    string tag = "HICHORD";
                    if (deg >= 0 && deg < 7)
                    {
                        ChordInfo bc = Theory.Base(h, deg);
                        tag = "HICHORD  \u00B7  " + Theory.Roman(bc.Q, deg) + "  \u00B7  " + Theory.Func[deg] + "  \u00B7  " + Theory.Feel[deg];
                        acc = Theme.Deg[deg];
                    }
                    int[] notes = (n >= 0 && h.Notes[n] != null) ? h.Notes[n] : h.Last;
                    string sub = notes != null ? Theory.NoteList(notes) : "";
                    string f = h.LastIv != null ? "Formula: " + Theory.Formula(h.LastIv) : "";
                    info.Set(name, sub, f, Lore.Blurb(h.LastSuf), tag, acc);
                    return;
                }
                int kind = eng.LastKind;
                int[] ln = eng.LastNotesPad;
                if (kind == 0 || ln == null || ln.Length == 0)
                {
                    info.Set("Press a button", "Your notes and chords show up here", "",
                        "Tip: click a button or knob on the picture, then click a piano key to choose its note.", "PAD MODE", Theme.Cyan);
                    return;
                }
                if (kind == 1)
                {
                    int n1 = ln[0];
                    double hz = 440.0 * Math.Pow(2.0, (n1 - 69) / 12.0);
                    info.Set(Music.Name(n1), "MIDI note " + n1 + "  \u00B7  " + hz.ToString("0.0") + " Hz", "",
                        "A single note. Several notes pressed together make a chord: set a button to 'Play a chord' to try it.", "NOTE", Theme.Cyan);
                }
                else
                {
                    int ci = eng.LastChordIdx;
                    info.Set(Music.PcName(eng.LastRoot) + Music.ChordShort[ci], Theory.NoteList(ln), "Formula: " + Theory.Formula(Music.ChordIv[ci]),
                        Lore.Blurb(Music.ChordCanon[ci]), "CHORD", Theme.Pink);
                }
            }

            void UpdateHiLabels()
            {
                HiBind b = eng.Bind;
                if (b == null) return;
                HiState h = eng.Hi;
                bool fl = Theory.UseFlats(h.Key);
                for (int i = 0; i < 7; i++)
                {
                    Pad p = b.Chord[i];
                    if (p == null) continue;
                    ChordInfo bc = Theory.Base(h, i);
                    p.HiTag = Theory.Roman(bc.Q, i);
                    p.HiSub = (h.Held[i] && h.Cur[i] != null) ? h.Cur[i] : bc.Name(fl);

                    // Visually show if locked
                    if (h.Lock[i] != null)
                    {
                        p.HiTag = "🔒 " + p.HiTag;
                        p.HiCol = Theme.Amber; // Locked chords glow amber
                    }
                    else
                    {
                        p.HiCol = Theme.Deg[i]; // Normal color
                    }
                }
            }

            void SyncHiUi()
            {
                HiState h = eng.Hi;
                int sig = h.Key | ((h.Oct + 2) << 4) | (h.Scale << 8) | (h.JoyMode << 12) | (h.Bass << 16) | (h.VLead ? (1 << 20) : 0) | (h.Vel << 21);
                if (sig == hiSig) return;
                bool first = hiSig == -1;
                hiSig = sig;
                loading = true;
                cbKey.SelectedIndex = h.Key;
                lblOctV.Text = h.Oct.ToString();
                cbScale.SelectedIndex = h.Scale;
                cbJoy.SelectedIndex = h.JoyMode;
                cbBass.SelectedIndex = h.Bass;
                chkVLead.Checked = h.VLead;
                sldHiVel.Value = h.Vel;
                lblHiVel.Text = "Loudness: " + h.Vel;
                loading = false;
                if (!first) profDirty = true;
            }

            void OnHiChanged(object s, EventArgs e)
            {
                if (loading) return;
                HiState h = eng.Hi;
                lock (eng.Lock)
                {
                    if (cbKey.SelectedIndex >= 0) h.Key = cbKey.SelectedIndex;
                    if (cbScale.SelectedIndex >= 0)
                    {
                        if (h.Scale != cbScale.SelectedIndex) for (int i = 0; i < 7; i++) { h.Lock[i] = null; h.Dirty[i] = true; }
                        h.Scale = cbScale.SelectedIndex;
                    }
                    if (cbJoy.SelectedIndex >= 0) h.JoyMode = cbJoy.SelectedIndex;
                    if (cbBass.SelectedIndex >= 0) h.Bass = cbBass.SelectedIndex;
                    h.VLead = chkVLead.Checked;
                }
                profDirty = true;
            }

            void OnHiVel(object s, EventArgs e)
            {
                lblHiVel.Text = "Loudness: " + sldHiVel.Value;
                if (loading) return;
                lock (eng.Lock) { eng.Hi.Vel = sldHiVel.Value; }
                profDirty = true;
            }

            void ChangeOct(int d)
            {
                HiState h = eng.Hi;
                lock (eng.Lock) { h.Oct = Math.Max(-2, Math.Min(2, h.Oct + d)); }
                profDirty = true;
            }

            void OnSel(object s, EventArgs e)
            {
                if (eng.Hi.On) return;
                Pad p = view.Selected as Pad;
                Knob k = view.Selected as Knob;
                loading = true;
                lblTitle.Visible = true;
                pnlPad.Visible = p != null && p.Notes;
                pnlKnob.Visible = k != null;
                pnlNone.Visible = (p == null && k == null) || (p != null && !p.Notes);
                if (p != null && p.Notes)
                {
                    lblTitle.Text = p.Name.Substring(0, 1).ToUpper() + p.Name.Substring(1);
                    int idx = 4;
                    for (int i = 0; i < KindOfIndex.Length; i++) if (KindOfIndex[i] == p.Map.Kind) idx = i;
                    cbAction.SelectedIndex = idx;
                    cbChord.SelectedIndex = p.Map.Chord;
                    sldVel.Value = Math.Max(1, Math.Min(127, p.Map.Vel));
                    lblVel.Text = "Loudness: " + sldVel.Value;
                    RefreshPadEditor(p);
                }
                else if (k != null)
                {
                    lblTitle.Text = k.Label;
                    cbKType.SelectedIndex = k.PitchBend ? 1 : 0;
                    nudCC.Value = k.CC;
                    nudCC.Enabled = !k.PitchBend;
                    sldSpeed.Value = Math.Max(1, Math.Min(60, (int)(k.Speed * 10f + 0.5f)));
                    chkRev.Checked = k.Reverse;
                    bool showRest = k.Spring && !k.PitchBend;
                    lblRest.Visible = showRest; cbRest.Visible = showRest;
                    cbRest.SelectedIndex = k.RestMid;
                    lblKDesc.Text = k.Spring ? "Springs back when you let go." : "Moves while held. Stays where you leave it.";
                    piano.Root = -1; piano.Extra = new int[0];
                }
                else if (p != null)
                {
                    lblTitle.Text = p.Name.Substring(0, 1).ToUpper() + p.Name.Substring(1);
                    lblNone.Text = "These directions drive the knobs.\r\n\r\nClick a knob circle to change it.";
                    piano.Root = -1; piano.Extra = new int[0];
                }
                else
                {
                    lblTitle.Text = "Pick something";
                    lblNone.Text = "Click a button or a knob circle on the controller picture to change what it does.\r\n\r\nNote and chord buttons: then click a piano key below.\r\n\r\nWant instant chord-playing with no wrong notes? Switch to HiChord at the top.";
                    piano.Root = -1; piano.Extra = new int[0];
                }
                loading = false;
                piano.Invalidate();
            }

            void RefreshPadEditor(Pad p)
            {
                int kind = p.Map.Kind;
                lblChord.Visible = kind == 2; cbChord.Visible = kind == 2;
                lblVel.Visible = kind == 1 || kind == 2; sldVel.Visible = kind == 1 || kind == 2;
                if (kind == 1) lblHint.Text = "Click a piano key below to pick the note.";
                else if (kind == 2) lblHint.Text = "Click a piano key below to pick the chord's first note. The rest of the chord is built on top of it.";
                else if (kind == 3 || kind == 4) lblHint.Text = "Moves every note and chord you play one octave at a time (up to 4 either way).";
                else lblHint.Text = "This button does nothing.";
                piano.Root = -1; piano.Extra = new int[0];
                if (kind == 1 || kind == 2)
                {
                    piano.Root = p.Map.Root;
                    if (kind == 2)
                    {
                        int[] iv = Music.ChordIv[p.Map.Chord];
                        List<int> ex = new List<int>();
                        for (int j = 1; j < iv.Length; j++) if (p.Map.Root + iv[j] <= PianoView.Hi) ex.Add(p.Map.Root + iv[j]);
                        piano.Extra = ex.ToArray();
                    }
                }
                piano.Invalidate();
            }

            void OnActionChanged(object s, EventArgs e)
            {
                if (loading) return;
                Pad p = view.Selected as Pad;
                if (p == null || cbAction.SelectedIndex < 0) return;
                lock (eng.Lock) { p.Map.Kind = KindOfIndex[cbAction.SelectedIndex]; }
                RefreshPadEditor(p); profDirty = true; view.Invalidate();
            }

            void OnChordChanged(object s, EventArgs e)
            {
                if (loading) return;
                Pad p = view.Selected as Pad;
                if (p == null || cbChord.SelectedIndex < 0) return;
                lock (eng.Lock) { p.Map.Chord = cbChord.SelectedIndex; }
                RefreshPadEditor(p); profDirty = true; view.Invalidate();
            }

            void OnVelChanged(object s, EventArgs e)
            {
                lblVel.Text = "Loudness: " + sldVel.Value;
                if (loading) return;
                Pad p = view.Selected as Pad;
                if (p == null) return;
                lock (eng.Lock) { p.Map.Vel = sldVel.Value; }
                profDirty = true;
            }

            void OnKnobChanged(object s, EventArgs e)
            {
                if (loading) return;
                Knob k = view.Selected as Knob;
                if (k == null) return;
                lock (eng.Lock)
                {
                    bool pb = cbKType.SelectedIndex == 1;
                    bool typeChanged = pb != k.PitchBend || (cbRest.SelectedIndex >= 0 && cbRest.SelectedIndex != k.RestMid);
                    k.PitchBend = pb;
                    k.CC = (int)nudCC.Value;
                    k.Speed = sldSpeed.Value / 10f;
                    k.Reverse = chkRev.Checked;
                    if (cbRest.SelectedIndex >= 0) k.RestMid = cbRest.SelectedIndex;
                    if (typeChanged) k.ResetPos();
                }
                nudCC.Enabled = !k.PitchBend;
                bool showRest = k.Spring && !k.PitchBend;
                lblRest.Visible = showRest; cbRest.Visible = showRest;
                profDirty = true; view.Invalidate();
            }

            void OnPianoClicked(int note)
            {
                Pad p = view.Selected as Pad;
                int[] play = new int[] { note };
                if (!eng.Hi.On && p != null && p.Notes && (p.Map.Kind == 1 || p.Map.Kind == 2))
                {
                    lock (eng.Lock) { p.Map.Root = note; }
                    RefreshPadEditor(p); profDirty = true; view.Invalidate();
                    if (p.Map.Kind == 2)
                    {
                        int[] iv = Music.ChordIv[p.Map.Chord];
                        List<int> l = new List<int>();
                        for (int j = 0; j < iv.Length; j++) if (note + iv[j] <= 127) l.Add(note + iv[j]);
                        play = l.ToArray();
                    }
                }
                PreviewNotes(play);
            }

            void PreviewNotes(int[] notes)
            {
                if (!eng.Out.IsOpen) return;
                StopPreview();
                for (int i = 0; i < notes.Length; i++) eng.Out.NoteOn(eng.Channel, notes[i], 100);
                preview = notes;
                prevTimer.Stop(); prevTimer.Start();
            }

            void StopPreview()
            {
                for (int i = 0; i < preview.Length; i++) eng.Out.NoteOff(eng.Channel, preview[i]);
                preview = new int[0];
            }

            public void TestSound()
            {
                if (!eng.Out.IsOpen) { MessageBox.Show(this, "No MIDI cable is open yet. Click Help for the 2-minute setup.", "MIDI cable"); return; }
                PreviewNotes(new int[] { 60, 64, 67 });
            }

            void Wiggle()
            {
                Knob k = view.Selected as Knob;
                if (k == null) return;
                if (!eng.Out.IsOpen) { MessageBox.Show(this, "No MIDI cable is open yet.", "MIDI cable"); return; }
                wigKnob = k; wigStep = 0; wigTimer.Start();
            }

            void OnWigTick(object s, EventArgs e)
            {
                Knob k = wigKnob;
                if (k == null) { wigTimer.Stop(); return; }
                wigStep++;
                int val;
                if (wigStep >= 80)
                {
                    wigTimer.Stop();
                    val = k.Sent >= 0 ? k.Sent : (k.PitchBend ? 8192 : (int)(k.RestPos() * 127f + 0.5f));
                }
                else
                {
                    double v = 0.5 - 0.5 * Math.Cos(wigStep / 80.0 * Math.PI * 4.0);
                    val = k.PitchBend ? (int)(v * 16383.0) : (int)(v * 127.0);
                }
                if (k.PitchBend) eng.Out.Bend(eng.Channel, val); else eng.Out.CC(eng.Channel, k.CC, val);
            }

            void ShowHelp()
            {
                MessageBox.Show(this,
                    "SETUP (only the first time)\n\n" +
                    "1. MIDI cable: Windows has no built-in virtual MIDI cable that apps can plug into, so you need one. " +
                    "Click 'Get MIDI cable' (free loopMIDI), install it, press + to make a port and name it GamepadMIDI. " +
                    "(Newer Windows 11 updates also have a built-in 'MIDI Loopback Setup'.)\n\n" +
                    "2. Plug in your PS Classic controller, or pair your PS4 controller in Windows Bluetooth settings.\n\n" +
                    "3. Press 'Test' to hear three notes.\n\n" +
                    "4. Start FL Studio AFTER the cable exists. F10 > MIDI > click GamepadMIDI under Input > Enable.\n\n" +
                    "PAD MODE: click a button on the picture, then a piano key.\n" +
                    "HICHORD MODE: 7 chord buttons. Use the stick to change flavours. Touchpad sliding (coming soon) will control XY effects in your DAW. 'Monitor' shows your progression on a circle.\n\n" +
                    "PROFILES: every change saves to the current profile. 'Save as' copies it, 'New blank' starts empty, 'Set default' makes it the Default.\n\n" +
                    "Closing the window keeps the app running near the clock. Tray icon > Quit to stop it.",
                    "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // ------------------------------------------------------------------ entry point
        static class Program
        {
            static string CrashLog()
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NexaKord");
                try { Directory.CreateDirectory(dir); } catch { }
                return Path.Combine(dir, "crash.txt");
            }

            static void Report(Exception ex)
            {
                try { File.WriteAllText(CrashLog(), DateTime.Now + "\r\n" + ex); } catch { }
                try { MessageBox.Show("NexaKord hit a problem:\n\n" + ex.Message + "\n\nDetails were saved to:\n" + CrashLog(), "NexaKord"); } catch { }
            }


            [STAThread]
            static void Main(string[] args)
            {
                bool created;
                using (Mutex m = new Mutex(true, "NexaKord_single_instance", out created))
                {
                    if (!created)
                    {
                        MessageBox.Show("NexaKord is already running. Look for its icon near the clock.", "NexaKord");
                        return;
                    }
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                    Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e) { Report(e.Exception); };
                    bool hidden = false;
                    foreach (string a in args) if (a == "/minimized") hidden = true;
                    try { Application.Run(new MainForm(hidden)); }
                    catch (Exception ex) { Report(ex); }
                }
            }
        }
    
}