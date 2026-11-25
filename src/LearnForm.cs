// NexaKord v3 - LearnForm: chord quiz.  The app plays a target chord, you copy it on the controller.
// Beginner: major/minor triads, the right button is highlighted.  Intermediate: + flip, 7ths, dominant 7ths (stick needed).
// Advanced: every flavour of the Default and Extended stick sets, and a required inversion.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace GamepadMidi
{
    class MissRec
    {
        public int Deg;
        public ChordInfo Chord;
        public int Inv;
    }

    class LearnForm : Form
    {
        Engine eng;
        PadStyle style;
        PadView view;
        CardPanel pnlRight, pnlMenu;
        Label lblTitle, lblProg, lblTarget, lblInfo, lblFeedback, lblHelp, lblMenuTxt;
        TextBox txtSum;
        PopButton btnPlay, btnBeg, btnInt, btnAdv, btnAgain, btnMenu, btnClose;
        CheckBox chkEar;
        System.Windows.Forms.Timer tm, offTimer;
        Random rnd = new Random();

        int state;                       // 0 menu, 1 asking, 2 feedback, 3 summary
        int diff;                        // 0 beginner, 1 intermediate, 2 advanced
        int total = 10, qNum, attempts, score;
        int[] focus;
        ChordInfo target;
        int tDeg, tInv;
        int[] tNotes = new int[0];
        int[] asked = new int[7];
        int[] missed = new int[7];
        List<MissRec> recs = new List<MissRec>();
        bool wasHeld;
        int[] snapNotes;
        int snapRootPc;
        string fb = "";
        Color fbCol = Theme.Text;
        int fbAt;
        bool fbNext;
        string baseTarget = "";
        int[] playing = new int[0];
        bool savedOn, savedVLead;
        int savedJoy, savedBass;

        public LearnForm(Engine e, PadStyle st)
        {
            eng = e;
            style = st;
            Text = "NexaKord - Learn chords";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg0;
            ForeColor = Theme.Text;
            ClientSize = new Size(1000, 500);
            Font = Theme.F(9f, FontStyle.Regular);

            lblTitle = new Label();
            lblTitle.Text = "LEARN MODE";
            lblTitle.SetBounds(14, 10, 600, 30);
            lblTitle.Font = Theme.F(16f, FontStyle.Bold);
            lblTitle.ForeColor = Theme.Pink;
            Controls.Add(lblTitle);

            view = new PadView();
            view.SetBounds(10, 50, 600, 300);
            view.SetLayout(style, eng.Pads, eng.Knobs);
            view.HiMode = true;
            view.ReadOnlyView = true;
            Controls.Add(view);

            pnlRight = new CardPanel();
            pnlRight.SetBounds(620, 50, 370, 300);
            Controls.Add(pnlRight);
            lblProg = MakeLabel(pnlRight, 14, 14, 340, 20, Theme.Cyan, 10f);
            lblTarget = MakeLabel(pnlRight, 14, 40, 344, 44, Theme.Text, 17f);
            lblInfo = MakeLabel(pnlRight, 14, 90, 344, 56, Theme.Amber, 9.5f);
            lblFeedback = MakeLabel(pnlRight, 14, 152, 344, 56, Theme.Lime, 14f);
            lblHelp = MakeLabel(pnlRight, 14, 214, 344, 80, Theme.Dim, 8.5f);
            lblHelp.Text = "Hold the chord button, push the stick for flavour, tap the inversion button, then LET GO to check your answer.";

            btnPlay = new PopButton();
            btnPlay.Text = "PLAY CHORD  (hear it again)";
            btnPlay.SetBounds(620, 360, 370, 56);
            btnPlay.Accent = Theme.Pink;
            btnPlay.Font = Theme.F(11f, FontStyle.Bold);
            btnPlay.Click += delegate { PlayTarget(); };
            Controls.Add(btnPlay);

            chkEar = new CheckBox();
            chkEar.Text = "Ear only (hide the chord name)";
            chkEar.SetBounds(14, 362, 300, 22);
            chkEar.ForeColor = Theme.Text;
            Controls.Add(chkEar);

            btnMenu = new PopButton();
            btnMenu.Text = "MENU"; btnMenu.SetBounds(14, 400, 120, 40); btnMenu.Accent = Theme.Violet; btnMenu.Ghost = true;
            btnMenu.Click += delegate { GoMenu(); };
            Controls.Add(btnMenu);

            btnAgain = new PopButton();
            btnAgain.Text = "PRACTICE AGAIN"; btnAgain.SetBounds(150, 400, 220, 40); btnAgain.Accent = Theme.Lime;
            btnAgain.Click += delegate { PracticeAgain(); };
            Controls.Add(btnAgain);

            btnClose = new PopButton();
            btnClose.Text = "CLOSE"; btnClose.SetBounds(880, 450, 110, 36); btnClose.Accent = Theme.Amber; btnClose.Ghost = true;
            btnClose.Click += delegate { Close(); };
            Controls.Add(btnClose);

            txtSum = new TextBox();
            txtSum.Multiline = true; txtSum.ReadOnly = true; txtSum.ScrollBars = ScrollBars.Vertical;
            txtSum.SetBounds(10, 50, 980, 340);
            txtSum.BackColor = Theme.Card; txtSum.ForeColor = Theme.Text; txtSum.BorderStyle = BorderStyle.FixedSingle;
            txtSum.Font = Theme.Mono(10f);
            Controls.Add(txtSum);

            // menu overlay
            pnlMenu = new CardPanel();
            pnlMenu.SetBounds(10, 50, 980, 340);
            Controls.Add(pnlMenu);
            lblMenuTxt = MakeLabel(pnlMenu, 20, 18, 940, 60, Theme.Text, 11f);
            lblMenuTxt.Text = "Pick a level. NexaKord plays a chord from your current key and scale, you copy it on the controller. 10 questions per round.";
            btnBeg = MakeMenuButton("BEGINNER", "Major and minor triads. The right button is highlighted.", 100, Theme.Lime);
            btnInt = MakeMenuButton("INTERMEDIATE", "Triads, 7ths and flips. Use the stick. No highlight.", 170, Theme.Cyan);
            btnAdv = MakeMenuButton("ADVANCED", "Every colour, plus a required inversion.", 240, Theme.Pink);
            btnBeg.Click += delegate { StartRound(0, null); };
            btnInt.Click += delegate { StartRound(1, null); };
            btnAdv.Click += delegate { StartRound(2, null); };
            pnlMenu.BringToFront();
            chkEar.BringToFront();

            tm = new System.Windows.Forms.Timer();
            tm.Interval = 33;
            tm.Tick += Tick;
            offTimer = new System.Windows.Forms.Timer();
            offTimer.Interval = 1300;
            offTimer.Tick += delegate { offTimer.Stop(); StopTarget(); };

            Shown += delegate
            {
                lock (eng.Lock)
                {
                    HiState h = eng.Hi;
                    savedOn = h.On; savedJoy = h.JoyMode; savedBass = h.Bass; savedVLead = h.VLead;
                    h.Bass = 0; h.VLead = false; h.JoyMode = 0;
                    for (int i = 0; i < 7; i++) { h.Lock[i] = null; h.Dirty[i] = true; }
                }
                eng.SetHi(true);
                GoMenu();
                tm.Start();
            };
            FormClosing += delegate
            {
                tm.Stop(); offTimer.Stop(); StopTarget();
                lock (eng.Lock)
                {
                    HiState h = eng.Hi;
                    h.JoyMode = savedJoy; h.Bass = savedBass; h.VLead = savedVLead;
                }
                eng.SetHi(savedOn);
            };
        }

        static Label MakeLabel(Control parent, int x, int y, int w, int h, Color c, float size)
        {
            Label l = new Label();
            l.SetBounds(x, y, w, h);
            l.ForeColor = c;
            l.BackColor = Color.Transparent;
            l.Font = Theme.F(size, FontStyle.Bold);
            parent.Controls.Add(l);
            return l;
        }

        PopButton MakeMenuButton(string text, string desc, int y, Color accent)
        {
            PopButton b = new PopButton();
            b.Text = text; b.SetBounds(20, y, 260, 56); b.Accent = accent;
            b.Font = Theme.F(12f, FontStyle.Bold);
            pnlMenu.Controls.Add(b);
            Label l = MakeLabel(pnlMenu, 300, y + 8, 650, 40, Theme.Dim, 10f);
            l.Text = desc;
            return b;
        }

        // ================================================================ screens
        void GoMenu()
        {
            state = 0;
            StopTarget();
            pnlMenu.Visible = true;
            txtSum.Visible = false;
            pnlRight.Visible = false; view.Visible = false;
            btnPlay.Visible = false; btnAgain.Visible = false; btnMenu.Visible = false;
            chkEar.Visible = true;
            lblProg.Text = "";
        }

        void StartRound(int d, int[] focusDegrees)
        {
            diff = d;
            focus = focusDegrees;
            qNum = 0; score = 0;
            for (int i = 0; i < 7; i++) { asked[i] = 0; missed[i] = 0; }
            recs.Clear();
            pnlMenu.Visible = false;
            txtSum.Visible = false;
            pnlRight.Visible = true; view.Visible = true;
            btnPlay.Visible = true; btnMenu.Visible = true; btnAgain.Visible = false;
            chkEar.Visible = true;
            NextQuestion();
        }

        void NextQuestion()
        {
            qNum++;
            if (qNum > total) { ShowSummary(); return; }
            PickTarget();
            attempts = 0;
            wasHeld = false;
            snapNotes = null;
            EnterAsking();
            PlayTarget();
        }

        void EnterAsking()
        {
            state = 1;
            fb = "";
            HiState h = eng.Hi;
            bool fl = Theory.UseFlats(h.Key);
            string name = target.Name(fl);
            string[] invName = { "root position", "1st inversion", "2nd inversion" };
            baseTarget = chkEar.Checked ? "Listen..." : "Play: " + name;
            lblProg.Text = "QUESTION " + qNum + " OF " + total + "      " + (diff == 0 ? "BEGINNER" : (diff == 1 ? "INTERMEDIATE" : "ADVANCED"));
            lblInfo.Text = "Chord " + Theory.Roman(Theory.Base(h, tDeg).Q, tDeg) + "  in " + Theory.PcName(h.Key, fl) + " " + Theory.ScaleNames[h.Scale] +
                (diff == 2 ? "\r\nRequired: " + invName[tInv] + (tInv > 0 ? "  (tap the inversion button)" : "") : "");
            lblFeedback.Text = "";
            view.Highlight = (diff == 0 && eng.Bind != null) ? eng.Bind.Chord[tDeg] : null;
        }

        void PickTarget()
        {
            HiState h = eng.Hi;
            int d = 0, mode = 0, dir = 0, inv = 0;
            ChordInfo t = null;
            for (int tries = 0; tries < 60; tries++)
            {
                d = (focus != null && focus.Length > 0) ? focus[rnd.Next(focus.Length)] : rnd.Next(7);
                ChordInfo bc = Theory.Base(h, d);
                mode = 0; dir = 0; inv = 0;
                if (diff == 0)
                {
                    if (bc.Q > 1) continue;                      // only major / minor triads
                }
                else if (diff == 1)
                {
                    int[] ds = { 0, 3, 1, 2 };                   // base, flip, 7th, dominant 7th
                    dir = ds[rnd.Next(ds.Length)];
                    if (dir == 0 && bc.Q > 1) continue;
                }
                else
                {
                    mode = rnd.Next(2);                          // Default or Extended stick set
                    dir = rnd.Next(9);
                    inv = rnd.Next(3);
                }
                t = Theory.Modify(bc, mode, dir);
                break;
            }
            if (t == null) { d = 0; t = Theory.Base(h, 0); mode = 0; inv = 0; }
            target = t; tDeg = d; tInv = inv;
            tNotes = Theory.Voice(t, inv, 0);
            lock (eng.Lock) { h.JoyMode = mode; }
        }

        // ================================================================ sound
        void PlayTarget()
        {
            if (state == 0 || state == 3 || !eng.Out.IsOpen) return;
            StopTarget();
            for (int i = 0; i < tNotes.Length; i++) eng.Out.NoteOn(eng.Channel, tNotes[i], 100);
            playing = tNotes;
            offTimer.Stop(); offTimer.Start();
        }

        void StopTarget()
        {
            for (int i = 0; i < playing.Length; i++) eng.Out.NoteOff(eng.Channel, playing[i]);
            playing = new int[0];
        }

        // ================================================================ watching the player
        void Tick(object sender, EventArgs ev)
        {
            // Manually update the button labels in Learn Mode
            if (eng.Bind != null)
            {
                bool fl = Theory.UseFlats(eng.Hi.Key);
                for (int i = 0; i < 7; i++)
                {
                    Pad p = eng.Bind.Chord[i];
                    if (p != null)
                    {
                        ChordInfo bc = Theory.Base(eng.Hi, i);
                        p.HiTag = Theory.Roman(bc.Q, i);
                        p.HiSub = (eng.Hi.Held[i] && eng.Hi.Cur[i] != null) ? eng.Hi.Cur[i] : bc.Name(fl);
                    }
                }
            }
            view.Invalidate();
            bool cursor = ((Environment.TickCount / 450) & 1) == 0;     // blinking cursor
            if (state == 1)
            {
                lblTarget.Text = baseTarget + (cursor ? "_" : " ");
                WatchPlayer();
            }
            else if (state == 2)
            {
                lblTarget.Text = baseTarget + " ";
                lblFeedback.Text = fb;
                lblFeedback.ForeColor = fbCol;
                if (Environment.TickCount - fbAt > 1600)
                {
                    if (fbNext) NextQuestion(); else EnterAsking();
                }
            }
        }

        void WatchPlayer()
        {
            HiState h = eng.Hi;
            int n = -1;
            for (int i = 0; i < 7; i++)
                if (h.Held[i] && !h.Suppressed[i] && (n < 0 || h.Stamp[i] > h.Stamp[n])) n = i;
            int[] nn = n >= 0 ? h.Notes[n] : null;
            if (n >= 0 && nn != null)
            {
                snapNotes = (int[])nn.Clone();                 // the last thing you were holding
                snapRootPc = ((h.LastRoot % 12) + 12) % 12;
                wasHeld = true;
            }
            else if (n < 0 && wasHeld)
            {
                wasHeld = false;
                Evaluate();
            }
        }

        static int Lowest(int[] a)
        {
            int m = 999;
            for (int i = 0; i < a.Length; i++) if (a[i] < m) m = a[i];
            return m;
        }

        static bool SamePcs(int[] a, int[] b)
        {
            bool[] pa = new bool[12];
            bool[] pb = new bool[12];
            for (int i = 0; i < a.Length; i++) pa[a[i] % 12] = true;
            for (int i = 0; i < b.Length; i++) pb[b[i] % 12] = true;
            for (int i = 0; i < 12; i++) if (pa[i] != pb[i]) return false;
            return true;
        }

        void Evaluate()
        {
            if (snapNotes == null || snapNotes.Length == 0) return;
            bool fl = Theory.UseFlats(eng.Hi.Key);
            bool pcSame = SamePcs(snapNotes, tNotes);
            bool bassOk = diff < 2 || (Lowest(snapNotes) % 12 == Lowest(tNotes) % 12);
            int tRoot = ((target.Root % 12) + 12) % 12;
            attempts++;
            if (pcSame && bassOk)
            {
                if (attempts == 1) score++;
                Finish(attempts == 1, "PERFECT!", Theme.Lime);
                return;
            }
            string msg;
            if (pcSame) msg = "CLOSE - right notes, check the inversion";
            else if (snapRootPc == tRoot) msg = "CLOSE - right root, wrong flavour";
            else msg = "TRY AGAIN";
            if (attempts >= 3)
            {
                Finish(false, msg + "  It was " + target.Name(fl), Theme.Amber);
                return;
            }
            fb = msg;
            fbCol = snapRootPc == tRoot ? Theme.Amber : Theme.Pink;
            fbAt = Environment.TickCount;
            fbNext = false;
            state = 2;
        }

        void Finish(bool firstTry, string msg, Color col)
        {
            asked[tDeg]++;
            if (!firstTry)
            {
                missed[tDeg]++;
                MissRec r = new MissRec();
                r.Deg = tDeg; r.Chord = target; r.Inv = tInv;
                recs.Add(r);
            }
            fb = msg;
            fbCol = col;
            fbAt = Environment.TickCount;
            fbNext = true;
            state = 2;
        }

        // ================================================================ summary
        int[] Struggled()
        {
            List<int> l = new List<int>();
            for (int d = 0; d < 7; d++) if (missed[d] > 0) l.Add(d);
            return l.ToArray();
        }

        void ShowSummary()
        {
            state = 3;
            StopTarget();
            view.Visible = false; pnlRight.Visible = false; pnlMenu.Visible = false;
            btnPlay.Visible = false; chkEar.Visible = false;
            txtSum.Visible = true;
            btnAgain.Visible = true; btnMenu.Visible = true;
            HiState h = eng.Hi;
            bool fl = Theory.UseFlats(h.Key);
            string[] inv = { "root position", "1st inversion", "2nd inversion" };
            StringBuilder sb = new StringBuilder();
            sb.Append("ROUND COMPLETE  -  ").Append(score).Append(" of ").Append(total).Append(" right first try\r\n");
            sb.Append("Level: ").Append(diff == 0 ? "Beginner" : (diff == 1 ? "Intermediate" : "Advanced"));
            sb.Append("   Key: ").Append(Theory.PcName(h.Key, fl)).Append(" ").Append(Theory.ScaleNames[h.Scale]).Append("\r\n\r\n");
            sb.Append("HOW EACH CHORD WENT\r\n");
            for (int d = 0; d < 7; d++)
            {
                if (asked[d] == 0) continue;
                ChordInfo bc = Theory.Base(h, d);
                sb.Append("  ").Append(Theory.Roman(bc.Q, d).PadRight(6)).Append(bc.Name(fl).PadRight(10));
                sb.Append((asked[d] - missed[d]) + "/" + asked[d]);
                if (missed[d] > 0) sb.Append("   <-- practise this one");
                sb.Append("\r\n");
            }
            int[] st = Struggled();
            if (st.Length == 0)
            {
                sb.Append("\r\nNo slips this round. Try the next level up!\r\n");
            }
            else
            {
                sb.Append("\r\nTHEORY FOR THE ONES YOU MISSED\r\n");
                for (int i = 0; i < st.Length; i++)
                {
                    MissRec rec = null;
                    for (int r = 0; r < recs.Count; r++) if (recs[r].Deg == st[i]) { rec = recs[r]; break; }
                    if (rec == null) continue;
                    ChordInfo bc = Theory.Base(h, st[i]);
                    sb.Append("\r\n").Append(Theory.Roman(bc.Q, st[i])).Append("  ").Append(rec.Chord.Name(fl)).Append("   (").Append(inv[rec.Inv]).Append(")\r\n");
                    sb.Append("   Formula : ").Append(Theory.Formula(rec.Chord.Iv)).Append("\r\n");
                    StringBuilder ivs = new StringBuilder();
                    for (int k = 0; k < rec.Chord.Iv.Length; k++) { if (k > 0) ivs.Append(' '); ivs.Append(rec.Chord.Iv[k]); }
                    sb.Append("   Semitones from the root: ").Append(ivs.ToString()).Append("\r\n");
                    sb.Append("   Notes   : ").Append(Theory.NoteList(Theory.Voice(rec.Chord, 0, 0))).Append("\r\n");
                    sb.Append("   Sound   : ").Append(Lore.Blurb(rec.Chord.Suf)).Append("\r\n");
                    sb.Append("   Degree  : ").Append(Theory.Func[st[i]]).Append(" (").Append(Theory.Feel[st[i]]).Append(")\r\n");
                }
            }
            txtSum.Text = sb.ToString();
            txtSum.SelectionStart = 0;
            txtSum.SelectionLength = 0;
        }

        void PracticeAgain()
        {
            int[] st = Struggled();
            StartRound(diff, st.Length > 0 ? st : null);
        }
    }
}
