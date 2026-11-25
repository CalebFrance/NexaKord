// GamepadMIDI v3 - GuitarView: a 6-string, 22-fret neck that shows whatever is sounding ("transpose" mode).
// Root = orange, other notes = cyan. Open string = hollow circle, muted string = X. Pure drawing: it never touches MIDI output.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

namespace GamepadMidi
{
    class GuitarView : Control
    {
        public Engine Eng;
        int[] lastNotes = new int[0];
        int lastRoot = -1;
        string key = "";
        bool live;
        FretResult res = new FretResult();
        static readonly string[] StrName = { "E", "A", "D", "G", "B", "e" };
        const float Left = 70f;

        public GuitarView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg0;
        }

        int CurrentRoot()
        {
            HiState h = Eng.Hi;
            if (h.On) return h.Last != null ? ((h.LastRoot % 12) + 12) % 12 : -1;
            if (Eng.LastKind == 1 || Eng.LastKind == 2) return ((Eng.LastRoot % 12) + 12) % 12;
            return -1;
        }

        void Refresh()
        {
            List<int> ns = new List<int>();
            bool[] snd = Eng.Sounding;
            for (int n = 0; n < 128; n++) if (snd[n]) ns.Add(n);
            live = ns.Count > 0;
            if (live)
            {
                lastNotes = ns.ToArray();
                lastRoot = CurrentRoot();
                if (lastRoot < 0) lastRoot = lastNotes[0] % 12;
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lastNotes.Length; i++) sb.Append(lastNotes[i]).Append(',');
            sb.Append(lastRoot);
            string k = sb.ToString();
            if (k != key)
            {
                key = k;
                string suffix = "";
                if (Eng.Hi.On && Eng.Hi.LastSuf != null) suffix = Eng.Hi.LastSuf;
                else if (Eng.LastKind == 2 && Eng.LastChordIdx >= 0) suffix = Music.ChordCanon[Eng.LastChordIdx];
                res = GuitarSolver.Solve(lastNotes, lastRoot, suffix);
            }
        }

        float FX(int n, float w)
        {
            float norm = 1f - (float)Math.Pow(2.0, -22.0 / 12.0);
            return Left + w * (1f - (float)Math.Pow(2.0, -n / 12.0)) / norm;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.Bg0);
            if (Eng == null || Width < 200 || Height < 60) return;
            Refresh();

            float right = Width - 12f, top = 16f, bottom = Height - 24f;
            float w = right - Left;
            float sp = (bottom - top) / 5f;
            int alpha = live ? 255 : 120;

            using (Brush bb = new SolidBrush(Theme.Card)) g.FillRectangle(bb, Left, top - 8f, w, bottom - top + 16f);
            using (Pen border = new Pen(Theme.Line, 1f)) g.DrawRectangle(border, Left, top - 8f, w, bottom - top + 16f);

            // inlays
            int[] inlay = { 3, 5, 7, 9, 15, 17, 19, 21 };
            using (Brush ib = new SolidBrush(Color.FromArgb(70, Theme.Violet)))
            {
                for (int i = 0; i < inlay.Length; i++)
                {
                    float cx = (FX(inlay[i] - 1, w) + FX(inlay[i], w)) / 2f;
                    g.FillRectangle(ib, cx - 4f, (top + bottom) / 2f - 4f, 8f, 8f);
                }
                float c12 = (FX(11, w) + FX(12, w)) / 2f;
                g.FillRectangle(ib, c12 - 4f, top + sp * 1.5f - 4f, 8f, 8f);
                g.FillRectangle(ib, c12 - 4f, top + sp * 3.5f - 4f, 8f, 8f);
            }

            // frets (fret 0 = nut) and fret numbers
            for (int n = 0; n <= 22; n++)
            {
                float x = FX(n, w);
                using (Pen fp = new Pen(n == 0 ? Theme.Cyan : Color.FromArgb(150, Theme.Dim), n == 0 ? 3f : 1f))
                    g.DrawLine(fp, x, top - 8f, x, bottom + 8f);
            }
            int[] nums = { 1, 3, 5, 7, 9, 12, 15, 17, 19, 21 };
            using (Brush nb = new SolidBrush(Theme.Dim))
            {
                for (int i = 0; i < nums.Length; i++)
                {
                    float cx = (FX(nums[i] - 1, w) + FX(nums[i], w)) / 2f;
                    g.DrawString(nums[i].ToString(), Theme.F(7f, FontStyle.Bold), nb, new RectangleF(cx - 12f, bottom + 9f, 24f, 12f), Theme.Center);
                }
            }

            // strings (low E at the bottom)
            for (int s = 0; s < 6; s++)
            {
                float y = bottom - s * sp;
                using (Pen sp2 = new Pen(Color.FromArgb(210, 200, 190, 230), 3.2f - s * 0.45f)) g.DrawLine(sp2, Left, y, right, y);
                using (Brush lb = new SolidBrush(Theme.Dim))
                    g.DrawString(StrName[s], Theme.F(8f, FontStyle.Bold), lb, new RectangleF(4f, y - 8f, 18f, 16f), Theme.Center);
            }

            // constellation lines
            if (res.Constellation && res.Dots.Count > 1)
            {
                List<PointF> pts = new List<PointF>();
                for (int i = 0; i < res.Dots.Count; i++) pts.Add(DotCenter(res.Dots[i], w, bottom, sp));
                using (Pen lp = new Pen(Color.FromArgb(alpha / 2, Theme.Cyan), 2f)) g.DrawLines(lp, pts.ToArray());
            }

            // markers: open = hollow circle, muted = X (only meaningful for a single shape)
            if (res.Constellation)
            {
                for (int s = 0; s < 6; s++)
                {
                    float y = bottom - s * sp;
                    if (res.Frets[s] < 0)
                    {
                        using (Pen xp = new Pen(Color.FromArgb(alpha, Theme.Pink), 2f))
                        {
                            g.DrawLine(xp, Left - 24f, y - 5f, Left - 14f, y + 5f);
                            g.DrawLine(xp, Left - 24f, y + 5f, Left - 14f, y - 5f);
                        }
                    }
                }
            }

            // dots
            for (int i = 0; i < res.Dots.Count; i++)
            {
                FretPos d = res.Dots[i];
                PointF c = DotCenter(d, w, bottom, sp);
                Color col = d.Root ? Theme.Amber : Theme.Cyan;
                Color ca = Color.FromArgb(alpha, col);
                int pc = (GuitarSolver.Tuning[d.Str] + d.Fret) % 12;
                if (d.Fret == 0)
                {
                    using (Pen op = new Pen(ca, 2f)) g.DrawEllipse(op, c.X - 6f, c.Y - 6f, 12f, 12f);
                }
                else
                {
                    using (Brush fb = new SolidBrush(ca)) g.FillEllipse(fb, c.X - 8f, c.Y - 8f, 16f, 16f);
                    using (Brush tb = new SolidBrush(Color.FromArgb(alpha, Color.Black)))
                        g.DrawString(Theory.PcName(pc, false), Theme.F(6.5f, FontStyle.Bold), tb, new RectangleF(c.X - 10f, c.Y - 7f, 20f, 14f), Theme.Center);
                }
            }

            string head = lastNotes.Length == 0 ? "FRETBOARD  -  play something" :
                (lastNotes.Length == 1 ? "ALL POSITIONS OF " + Music.Name(lastNotes[0]) : (res.Constellation ? "CHORD SHAPE" : "NO SINGLE SHAPE - ALL POSITIONS"));
            using (Brush hb = new SolidBrush(Theme.Pink)) g.DrawString(head, Theme.F(7.5f, FontStyle.Bold), hb, Left + 6f, 1f);
        }

        PointF DotCenter(FretPos d, float w, float bottom, float sp)
        {
            float x = d.Fret == 0 ? Left - 14f : (FX(d.Fret - 1, w) + FX(d.Fret, w)) / 2f;
            return new PointF(x, bottom - d.Str * sp);
        }
    }
}
