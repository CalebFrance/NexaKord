// GamepadMIDI v2 - Controls: theme, pop-styled controls, the controller picture, the piano, the HiChord circle monitor.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace GamepadMidi
{
    // ------------------------------------------------------------------ colours, fonts, drawing helpers
    static class Theme
    {
        public static bool WireframeMode = false;
        public static readonly Color Bg0 = Color.FromArgb(16, 12, 34);
        public static readonly Color Bg1 = Color.FromArgb(44, 24, 86);
        public static readonly Color Card = Color.FromArgb(34, 27, 66);
        public static readonly Color Card2 = Color.FromArgb(54, 43, 102);
        public static readonly Color Line = Color.FromArgb(92, 76, 160);
        public static readonly Color Pink = Color.FromArgb(255, 79, 154);
        public static readonly Color Cyan = Color.FromArgb(56, 225, 255);
        public static readonly Color Amber = Color.FromArgb(255, 196, 61);
        public static readonly Color Lime = Color.FromArgb(124, 255, 142);
        public static readonly Color Violet = Color.FromArgb(160, 120, 255);
        public static readonly Color Orange = Color.FromArgb(255, 140, 66);
        public static readonly Color Ice = Color.FromArgb(170, 195, 255);
        public static readonly Color Text = Color.FromArgb(244, 240, 255);
        public static readonly Color Dim = Color.FromArgb(166, 156, 208);
        public static readonly Color[] Deg = { Amber, Cyan, Violet, Lime, Pink, Orange, Ice };

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static StringFormat center;

        public static Font F(float size, FontStyle st)
        {
            string k = "S" + size + "/" + (int)st;
            Font f;
            if (!fonts.TryGetValue(k, out f)) { f = new Font("Segoe UI", size, st); fonts[k] = f; }
            return f;
        }

        public static Font Sym(float size)
        {
            string k = "Y" + size;
            Font f;
            if (!fonts.TryGetValue(k, out f)) { f = new Font("Segoe UI Symbol", size, FontStyle.Bold); fonts[k] = f; }
            return f;
        }

        public static Font Mono(float size)
        {
            string k = "M" + size;
            Font f;
            if (!fonts.TryGetValue(k, out f)) { f = new Font("Consolas", size, FontStyle.Bold); fonts[k] = f; }
            return f;
        }

        public static StringFormat Center
        {
            get
            {
                if (center == null)
                {
                    center = new StringFormat();
                    center.Alignment = StringAlignment.Center;
                    center.LineAlignment = StringAlignment.Center;
                    center.FormatFlags = StringFormatFlags.NoWrap;
                }
                return center;
            }
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color OnColor(Color c)
        {
            return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 140 ? Color.FromArgb(24, 14, 48) : Color.White;
        }

        public static GraphicsPath RR(RectangleF r, float d)
        {
            GraphicsPath p = new GraphicsPath();
            float dd = Math.Min(d, Math.Min(r.Width, r.Height));
            if (dd < 1f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, dd, dd, 180, 90);
            p.AddArc(r.Right - dd, r.Y, dd, dd, 270, 90);
            p.AddArc(r.Right - dd, r.Bottom - dd, dd, dd, 0, 90);
            p.AddArc(r.X, r.Bottom - dd, dd, dd, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static GraphicsPath Oval(RectangleF r)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddEllipse(r);
            return p;
        }

        public static void Glow(Graphics g, GraphicsPath gp, Color c, float w)
        {
            for (int i = 4; i >= 1; i--)
            {
                using (Pen p = new Pen(Color.FromArgb(18 * (5 - i), c), w + i * 3f))
                {
                    p.LineJoin = LineJoin.Round;
                    g.DrawPath(p, gp);
                }
            }
        }
    }

    // ------------------------------------------------------------------ pill button
    class PopButton : Control
    {
        public Color Accent = Theme.Pink;
        public bool Toggled;
        public bool Ghost;
        bool hover, down;

        public PopButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = Theme.F(9f, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            if (Width < 6 || Height < 6) return;
            RectangleF r = new RectangleF(1f, 1f, Width - 3f, Height - 3f);
            bool filled = !Ghost || Toggled;
            bool isWire = Theme.WireframeMode;
            Color a = Accent;

            using (GraphicsPath gp = Theme.RR(r, Height - 3f))
            {
                if (filled)
                {
                    if (Theme.WireframeMode)
                    {
                        // In wireframe mode, active buttons get a dark neon fill
                        using (Brush b = new SolidBrush(Color.FromArgb(80, a))) g.FillPath(b, gp);
                        using (Pen p = new Pen(a, 2.5f)) g.DrawPath(p, gp);
                    }
                    else
                    {
                        Color c1 = Theme.Mix(a, Color.White, hover ? 0.38f : 0.22f);
                        Color c2 = Theme.Mix(a, Color.Black, down ? 0.4f : 0.22f);
                        using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), c1, c2, 90f))
                            g.FillPath(lb, gp);
                    }
                }
                else
                {
                    using (Brush b = new SolidBrush(Theme.WireframeMode ? Color.Transparent : (hover ? Theme.Card2 : Color.FromArgb(150, Theme.Card))))
                        g.FillPath(b, gp);
                    using (Pen p = new Pen(a, Theme.WireframeMode ? 2.5f : 1.5f)) g.DrawPath(p, gp);
                }
            }
            Color tc = filled ? Theme.OnColor(a) : Theme.Text;
            using (Brush tb = new SolidBrush(tc))
                g.DrawString(Text, Font, tb, new RectangleF(0, down ? 1 : 0, Width, Height), Theme.Center);
        }
    }

    // ------------------------------------------------------------------ slider
    class PopSlider : Control
    {
        public int Min = 0, Max = 100;
        public Color Accent = Theme.Cyan;
        public event EventHandler ValueChanged;
        int val;

        public PopSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Height = 24;
        }

        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(Min, Math.Min(Max, value));
                if (v == val) return;
                val = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        void SetFromX(int x)
        {
            float t = (x - 10f) / Math.Max(1f, Width - 20f);
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            Value = Min + (int)(t * (Max - Min) + 0.5f);
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) SetFromX(e.X); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (e.Button == MouseButtons.Left) SetFromX(e.X); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Width < 30) return;
            float t = (val - Min) / (float)Math.Max(1, Max - Min);
            float x = 10f + t * (Width - 20f);
            float cy = Height / 2f;
            using (GraphicsPath tr = Theme.RR(new RectangleF(8f, cy - 3f, Width - 16f, 6f), 6f))
            using (Brush b = new SolidBrush(Theme.Card2)) g.FillPath(b, tr);
            if (x > 12f)
            {
                using (GraphicsPath fp = Theme.RR(new RectangleF(8f, cy - 3f, x - 8f, 6f), 6f))
                using (LinearGradientBrush lb = new LinearGradientBrush(new RectangleF(8f, cy - 3f, Math.Max(2f, x - 8f), 6f), Theme.Pink, Accent, 0f))
                    g.FillPath(lb, fp);
            }
            using (Brush kb = new SolidBrush(Color.White)) g.FillEllipse(kb, x - 8f, cy - 8f, 16f, 16f);
            using (Pen kp = new Pen(Accent, 2.5f)) g.DrawEllipse(kp, x - 8f, cy - 8f, 16f, 16f);
        }
    }

    // ------------------------------------------------------------------ card panel with a rainbow top edge
    class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Card;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.WireframeMode ? Color.Black : Theme.Card);

            if (Theme.WireframeMode)
            {
                using (Pen p = new Pen(Theme.Cyan, 2f)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
            else
            {
                using (Pen p = new Pen(Theme.Line)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                using (LinearGradientBrush lb = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(2, Width), 4), Theme.Pink, Theme.Cyan, 0f))
                    g.FillRectangle(lb, 0, 0, Width, 4);
            }
        }
    }

    // ------------------------------------------------------------------ "now playing" card
    class InfoCard : Control
    {
        string title = "", sub = "", formula = "", body = "", tag = "";
        Color accent = Theme.Pink;

        public InfoCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0; // (or Theme.Card)
        }

        public void Set(string t, string s, string f, string b, string tg, Color a)
        {
            if (t == title && s == sub && f == formula && b == body && tg == tag && a == accent) return;
            title = t; sub = s; formula = f; body = b; tag = tg; accent = a;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            g.Clear(Theme.WireframeMode ? Color.Black : Theme.Bg0);

            RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath gp = Theme.RR(r, 16f))
            {
                if (!Theme.WireframeMode)
                {
                    using (Brush b = new SolidBrush(Theme.Card)) g.FillPath(b, gp);
                    using (Pen p = new Pen(Theme.Line)) g.DrawPath(p, gp);
                }
                else
                {
                    using (Pen p = new Pen(accent, 2f)) g.DrawPath(p, gp);
                }
            }

            // Left accent bar
            using (GraphicsPath bar = Theme.RR(new RectangleF(0, 0, 8, Height - 1), 8f))
            using (Brush ab = new SolidBrush(accent)) g.FillPath(ab, bar);

            // Text
            using (Brush ab2 = new SolidBrush(accent)) g.DrawString(tag, Theme.F(8f, FontStyle.Bold), ab2, 22, 8);
            using (Brush tb = new SolidBrush(Theme.Text))
            {
                g.DrawString(title, Theme.F(20f, FontStyle.Bold), tb, 20, 22);
                g.DrawString(sub, Theme.F(10f, FontStyle.Bold), tb, 22, 52);
            }
            using (Brush fb = new SolidBrush(accent)) g.DrawString(formula, Theme.Mono(10f), fb, 22, 71);
            using (Brush db = new SolidBrush(Theme.Dim))
                g.DrawString(body, Theme.F(9f, FontStyle.Regular), db, new RectangleF(22, 92, Width - 40, Height - 96));
        }
    }

    // ------------------------------------------------------------------ the controller picture
    class PadView : Control
    {
        public List<Pad> Pads = new List<Pad>();
        public List<Knob> Knobs = new List<Knob>();
        public object Selected;
        public Pad Highlight;
        public bool ReadOnlyView, HiMode;
        public PadStyle Pstyle;
        public Rectangle DpadCenter;
        public event EventHandler SelectionChanged;

        public PadView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0;
        }

        public void SetLayout(PadStyle s, List<Pad> pads, List<Knob> knobs)
        {
            Pstyle = s; Pads = pads; Knobs = knobs; Selected = null; Highlight = null;
            DpadCenter = s == PadStyle.Classic ? new Rectangle(112, 162, 46, 46) : new Rectangle(91, 125, 38, 38);
            Invalidate();
        }

        static string MapText(Pad p)
        {
            if (!p.Notes) return "";
            Mapping m = p.Map;
            if (m.Kind == 1) return Music.Name(m.Root);
            if (m.Kind == 2) return Music.PcName(m.Root) + Music.ChordShort[m.Chord];
            if (m.Kind == 3) return "Oct \u2193";
            if (m.Kind == 4) return "Oct \u2191";
            return "";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.WireframeMode ? Color.Black : Theme.Bg0);
            if (!Theme.WireframeMode)
            {
                using (GraphicsPath panel = Theme.RR(new RectangleF(0, 0, Width - 1, Height - 1), 16f))
                {
                    using (Brush pb = new SolidBrush(Theme.Card)) g.FillPath(pb, panel);
                    using (Pen pp = new Pen(Theme.Line)) g.DrawPath(pp, panel);
                }
                //using (Brush b = new SolidBrush(Theme.Card2))
                // Draw the controller body
                using (Pen bp = new Pen(Theme.WireframeMode ? Theme.Line : Theme.Line, 2f))
                {
                    if (Pstyle == PadStyle.Classic)
                    {
                        using (GraphicsPath gp = Theme.RR(new RectangleF(20, 70, 560, 225), 70f))
                        {
                            if (!Theme.WireframeMode) using (Brush b = new SolidBrush(Theme.Card2)) g.FillPath(b, gp);
                            g.DrawPath(bp, gp);
                        }
                    }
                    else if (Pstyle == PadStyle.PS4 || Pstyle == PadStyle.PS5)
                    {
                        using (GraphicsPath gp = Theme.RR(new RectangleF(20, 64, 560, 150), 60f))
                        {
                            if (!Theme.WireframeMode) using (Brush b = new SolidBrush(Theme.Card2)) g.FillPath(b, gp);
                            g.DrawPath(bp, gp);
                        }
                        if (!Theme.WireframeMode)
                        {
                            using (Brush b = new SolidBrush(Theme.Card2))
                            {
                                g.FillEllipse(b, 40, 150, 170, 145);
                                g.FillEllipse(b, 390, 150, 170, 145);
                            }
                        }
                        else
                        {
                            g.DrawEllipse(bp, 40, 150, 170, 145);
                            g.DrawEllipse(bp, 390, 150, 170, 145);
                        }
                    }
                    else // Xbox & Generic Layout
                    {
                        // Draw a slightly different body shape (more rounded top, more angled grips)
                        using (GraphicsPath gp = Theme.RR(new RectangleF(20, 64, 560, 140), 50f))
                        {
                            if (!Theme.WireframeMode) using (Brush b = new SolidBrush(Theme.Card2)) g.FillPath(b, gp);
                            g.DrawPath(bp, gp);
                        }
                        if (!Theme.WireframeMode)
                        {
                            using (Brush b = new SolidBrush(Theme.Card2))
                            {
                                g.FillEllipse(b, 40, 140, 160, 155);
                                g.FillEllipse(b, 400, 140, 160, 155);
                            }
                        }
                        else
                        {
                            g.DrawEllipse(bp, 40, 140, 160, 155);
                            g.DrawEllipse(bp, 400, 140, 160, 155);
                        }
                    }
                }
            }

            if (!DpadCenter.IsEmpty)
            {
                if (!Theme.WireframeMode)
                    using (Brush b2 = new SolidBrush(Color.FromArgb(70, 58, 130))) g.FillRectangle(b2, DpadCenter);
                else
                    using (Pen p2 = new Pen(Theme.Line, 1f)) g.DrawRectangle(p2, DpadCenter);
            }

            for (int i = 0; i < Pads.Count; i++) if (Pads[i].Visible) DrawPad(g, Pads[i]);
            for (int i = 0; i < Knobs.Count; i++) DrawKnob(g, Knobs[i]);
        }

        void DrawPad(Graphics g, Pad p)
        {
            Rectangle r = p.R;
            Color col = (HiMode && p.HiCol.A != 0) ? p.HiCol : p.Col;
            bool dim = HiMode && string.IsNullOrEmpty(p.HiTag);
            bool hot = p.Pressed && !dim;
            Color outline = Color.FromArgb(dim ? 90 : 255, col);

            Color fill;
            float penSize = 2.5f;

            if (Theme.WireframeMode)
            {
                fill = hot ? Color.FromArgb(60, col) : Color.Transparent;
                penSize = hot ? 4f : 2f;
            }
            else
            {
                fill = hot ? col : Color.FromArgb(dim ? 70 : 255, Theme.Card);
            }

            RectangleF rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            using (GraphicsPath gp = p.Shape == 0 ? Theme.Oval(rf) : Theme.RR(rf, p.Shape == 2 ? 10f : 12f))
            {
                if (hot) Theme.Glow(g, gp, col, 3f);
                using (Brush b = new SolidBrush(fill)) g.FillPath(b, gp);
                using (Pen pen = new Pen(outline, penSize)) g.DrawPath(pen, gp);
            }
            if (p.DxP != null)
            {
                float dx = (p.DxP.Value - p.DxN.Value) * 14f;
                float dy = (p.DyP.Value - p.DyN.Value) * 14f;
                using (Brush db = new SolidBrush(Color.FromArgb(235, Theme.Pink)))
                    g.FillEllipse(db, r.X + r.Width / 2f - 5f + dx, r.Y + r.Height / 2f - 19f + dy, 10f, 10f);
            }
            string tag = HiMode ? p.HiTag : p.Label;
            string sub = HiMode ? p.HiSub : MapText(p);
            if (tag == null) tag = "";
            if (sub == null) sub = "";
            Color tc = hot ? Theme.OnColor(col) : (dim ? Color.FromArgb(120, Theme.Dim) : Theme.Text);
            Color sc = hot ? Theme.OnColor(col) : col;
            using (Brush tb = new SolidBrush(tc))
            using (Brush sb = new SolidBrush(sc))
            {
                if (r.Height < 36)
                {
                    string t = sub.Length > 0 ? tag + "  " + sub : tag;
                    g.DrawString(t, r.Width >= 100 ? Theme.F(9f, FontStyle.Bold) : Theme.F(7.5f, FontStyle.Bold), tb, new RectangleF(r.X, r.Y, r.Width, r.Height), Theme.Center);
                }
                else
                {
                    Font gf = tag.Length > 2 ? Theme.F(9f, FontStyle.Bold) : Theme.Sym(12f);
                    if (sub.Length == 0)
                        g.DrawString(tag, gf, tb, new RectangleF(r.X, r.Y, r.Width, r.Height), Theme.Center);
                    else
                    {
                        g.DrawString(tag, gf, tb, new RectangleF(r.X, r.Y + 2, r.Width, r.Height * 0.55f), Theme.Center);
                        g.DrawString(sub, Theme.F(7.5f, FontStyle.Bold), sb, new RectangleF(r.X - 5, r.Y + r.Height * 0.52f, r.Width + 10, r.Height * 0.4f), Theme.Center);
                    }
                }
            }
            if (p == Selected)
            {
                Color selCol = Theme.WireframeMode ? Theme.Lime : Color.White;
                using (Pen sp = new Pen(selCol, 3f)) DrawOutline(g, p, sp, 4);
            }
            if (Highlight == p && ((Environment.TickCount / 300) & 1) == 0)
                using (Pen hp = new Pen(Theme.Amber, 4f)) DrawOutline(g, p, hp, 6);
        }

        void DrawOutline(Graphics g, Pad p, Pen pen, int grow)
        {
            Rectangle r = Rectangle.Inflate(p.R, grow, grow);
            RectangleF rf = new RectangleF(r.X, r.Y, r.Width, r.Height);
            using (GraphicsPath gp = p.Shape == 0 ? Theme.Oval(rf) : Theme.RR(rf, 14f)) g.DrawPath(pen, gp);
        }

        void DrawKnob(Graphics g, Knob k)
        {
            bool dim = HiMode && k.HiBlocked;
            int a = dim ? 70 : 255;
            Rectangle rc = new Rectangle(k.Center.X - k.Radius, k.Center.Y - k.Radius, k.Radius * 2, k.Radius * 2);
            using (Pen track = new Pen(Color.FromArgb(a, Theme.Card2), 6f)) g.DrawArc(track, rc, 135, 270);
            float rest = k.Spring ? k.RestPos() : 0f;
            float sweep = 270f * (k.Pos - rest);
            if (!dim && Math.Abs(sweep) > 0.5f)
                using (Pen val = new Pen(Theme.Pink, 6f)) g.DrawArc(val, rc, 135f + 270f * rest, sweep);
            string centre = dim ? "\u2022" : (k.PitchBend ? "PB" : ((int)(k.Pos * 127f + 0.5f)).ToString());
            using (Brush tb = new SolidBrush(Color.FromArgb(a, Theme.Text)))
                g.DrawString(centre, Theme.F(9f, FontStyle.Bold), tb, new RectangleF(rc.X, rc.Y, rc.Width, rc.Height), Theme.Center);
            string l1 = dim ? "chord flavour" : k.Label;
            string l2 = dim ? "" : ((k.PitchBend ? "Pitch bend" : "CC " + k.CC) + (k.Spring ? " (snap)" : ""));
            using (Brush lb = new SolidBrush(Color.FromArgb(a, Theme.Text)))
                g.DrawString(l1, Theme.F(7.5f, FontStyle.Bold), lb, new RectangleF(k.Center.X - 50, k.Center.Y + k.Radius + 2, 100, 14), Theme.Center);
            using (Brush lb2 = new SolidBrush(Color.FromArgb(a, Theme.Dim)))
                g.DrawString(l2, Theme.F(7.5f, FontStyle.Bold), lb2, new RectangleF(k.Center.X - 50, k.Center.Y + k.Radius + 15, 100, 14), Theme.Center);
            if (k == Selected)
                using (Pen sp = new Pen(Color.White, 2f)) g.DrawEllipse(sp, Rectangle.Inflate(rc, 6, 6));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (ReadOnlyView) return;
            object hit = null;
            for (int i = 0; i < Knobs.Count && hit == null; i++)
            {
                Knob k = Knobs[i];
                int dx = e.X - k.Center.X, dy = e.Y - k.Center.Y;
                if (dx * dx + dy * dy <= (k.Radius + 6) * (k.Radius + 6)) hit = k;
            }
            for (int i = 0; i < Pads.Count && hit == null; i++)
            {
                Pad p = Pads[i];
                if (!p.Visible) continue;
                if (p.Shape == 0)
                {
                    float cx = p.R.X + p.R.Width / 2f, cy = p.R.Y + p.R.Height / 2f, rad = p.R.Width / 2f;
                    if ((e.X - cx) * (e.X - cx) + (e.Y - cy) * (e.Y - cy) <= rad * rad) hit = p;
                }
                else if (p.R.Contains(e.Location)) hit = p;
            }
            if (hit != null)
            {
                Selected = hit;
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                Invalidate();
            }
        }
    }

    // ------------------------------------------------------------------ the full piano (88 keys)
    class PianoView : Control
    {
        public const int Lo = 21, Hi = 108;
        public int Root = -1;
        public int[] Extra = new int[0];
        public bool[] Sounding;
        public event Action<int> NoteClicked;
        int[] wi = new int[128];
        int[] whiteNote = new int[52];

        public PianoView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0; // (or Theme.Card)
            int c = 0;
            for (int n = Lo; n <= Hi; n++)
                if (!Music.IsBlack(n)) { wi[n] = c; whiteNote[c] = n; c++; }
        }

        bool IsExtra(int n)
        {
            for (int i = 0; i < Extra.Length; i++) if (Extra[i] == n) return true;
            return false;
        }

        Color KeyColor(int n, bool black)
        {
            if (Sounding != null && Sounding[n]) return Theme.Cyan;
            if (n == Root) return Theme.Pink;
            if (IsExtra(n)) return Theme.Amber;
            return black ? Color.FromArgb(28, 20, 52) : Color.FromArgb(246, 242, 255);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.WireframeMode ? Color.Black : Theme.Bg0);
            float w = Width / 52f;
            float bh = Height * 0.62f;
            float bw = w * 0.6f;
            using (Pen edge = new Pen(Color.FromArgb(120, 100, 170)))
            {
                for (int n = Lo; n <= Hi; n++)
                {
                    if (Music.IsBlack(n)) continue;
                    float x = wi[n] * w;
                    using (Brush b = new SolidBrush(KeyColor(n, false))) g.FillRectangle(b, x, 0, w, Height - 1);
                    g.DrawRectangle(edge, x, 0, w, Height - 1);
                    if (n % 12 == 0)
                        using (Brush lb = new SolidBrush(Color.FromArgb(120, 100, 160)))
                            g.DrawString(Music.Name(n), Theme.F(7f, FontStyle.Regular), lb, x + 1, Height - 14);
                }
                for (int n = Lo; n <= Hi; n++)
                {
                    if (!Music.IsBlack(n)) continue;
                    float bx = (wi[n - 1] + 1) * w - bw / 2f;
                    using (Brush b = new SolidBrush(KeyColor(n, true))) g.FillRectangle(b, bx, 0, bw, bh);
                    g.DrawRectangle(edge, bx, 0, bw, bh);
                }
            }
        }

        int HitNote(int x, int y)
        {
            float w = Width / 52f;
            float bh = Height * 0.62f;
            float bw = w * 0.6f;
            if (y < bh)
            {
                for (int n = Lo; n <= Hi; n++)
                {
                    if (!Music.IsBlack(n)) continue;
                    float bx = (wi[n - 1] + 1) * w - bw / 2f;
                    if (x >= bx && x <= bx + bw) return n;
                }
            }
            int idx = (int)(x / w);
            if (idx < 0) idx = 0;
            if (idx > 51) idx = 51;
            return whiteNote[idx];
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (NoteClicked != null) NoteClicked(HitNote(e.X, e.Y));
        }
    }

    // ------------------------------------------------------------------ HiChord circle monitor
    // The 7 chords of the key sit on a ring around the stick. Pink arrows show the progression you just played.
    class HiView : Control
    {
        public Engine Eng;

        public HiView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0; // (or Theme.Card)
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.WireframeMode ? Color.Black : Theme.Bg0);
            using (GraphicsPath card = Theme.RR(new RectangleF(0, 0, Width - 1, Height - 1), 16f))
            {
                using (Brush cb = new SolidBrush(Theme.Card)) g.FillPath(cb, card);
                using (Pen cp = new Pen(Theme.Line)) g.DrawPath(cp, card);
            }
            if (Eng == null) return;
            HiState h = Eng.Hi;
            bool fl = Theory.UseFlats(h.Key);
            float cx = Width / 2f, cy = 218f, R = 152f, nr = 32f;

            using (Pen ring = new Pen(Color.FromArgb(70, Theme.Violet), 2f)) g.DrawEllipse(ring, cx - R, cy - R, R * 2f, R * 2f);

            PointF[] pos = new PointF[7];
            for (int i = 0; i < 7; i++)
            {
                double a = (-90.0 + i * 360.0 / 7.0) * Math.PI / 180.0;
                pos[i] = new PointF(cx + R * (float)Math.Cos(a), cy + R * (float)Math.Sin(a));
            }

            // progression arrows (older = fainter)
            int[] tr = h.TrailDegs();
            for (int k = 1; k < tr.Length; k++)
            {
                int a = tr[k - 1], b = tr[k];
                if (a == b || a < 0 || b < 0 || a > 6 || b > 6) continue;
                float dx = pos[b].X - pos[a].X, dy = pos[b].Y - pos[a].Y;
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 1f) continue;
                float ux = dx / len, uy = dy / len;
                int alpha = 60 + (int)(170f * k / tr.Length);
                using (Pen ap = new Pen(Color.FromArgb(alpha, Theme.Pink), 3f))
                {
                    ap.CustomEndCap = new AdjustableArrowCap(5, 5, true);
                    g.DrawLine(ap, pos[a].X + ux * (nr + 2), pos[a].Y + uy * (nr + 2), pos[b].X - ux * (nr + 6), pos[b].Y - uy * (nr + 6));
                }
            }

            // stick disc in the middle
            int rd = h.Ref;
            if (rd < 0 || rd > 6) rd = 0;
            ChordInfo rb = Theory.Base(h, rd);
            float dr = 104f;
            using (GraphicsPath disc = Theme.Oval(new RectangleF(cx - dr, cy - dr, dr * 2f, dr * 2f)))
            {
                using (Brush db = new SolidBrush(Color.FromArgb(150, Theme.Bg1))) g.FillPath(db, disc);
                using (Pen dp = new Pen(Theme.Line, 2f)) g.DrawPath(dp, disc);
            }
            for (int d = 1; d <= 8; d++)
            {
                double th = (d - 1) * 45.0 * Math.PI / 180.0;
                float px = cx + 68f * (float)Math.Cos(th), py = cy - 68f * (float)Math.Sin(th);
                ChordInfo m = Theory.Modify(rb, h.JoyMode, d);
                bool act = h.Dir == d;
                RectangleF lr = new RectangleF(px - 33f, py - 9f, 66f, 18f);
                if (act)
                {
                    using (GraphicsPath pill = Theme.RR(lr, 18f))
                    using (Brush pb = new SolidBrush(Theme.Pink)) g.FillPath(pb, pill);
                }
                using (Brush lb = new SolidBrush(act ? Color.White : Theme.Text))
                    g.DrawString(m.Name(fl), Theme.F(8f, FontStyle.Bold), lb, lr, Theme.Center);
            }
            using (Brush bb = new SolidBrush(Theme.Dim))
                g.DrawString("stick on " + rb.Name(fl), Theme.F(7.5f, FontStyle.Bold), bb, new RectangleF(cx - 60, cy + 84, 120, 14), Theme.Center);
            float jx = Math.Max(-1f, Math.Min(1f, h.Jx)), jy = Math.Max(-1f, Math.Min(1f, h.Jy));
            using (Brush kb = new SolidBrush(Color.FromArgb(230, Color.White)))
                g.FillEllipse(kb, cx + jx * 30f - 8f, cy - jy * 30f - 8f, 16f, 16f);
            using (Pen kp = new Pen(Theme.Pink, 2.5f)) g.DrawEllipse(kp, cx + jx * 30f - 8f, cy - jy * 30f - 8f, 16f, 16f);

            // the 7 chord nodes
            int last = h.TrailCount > 0 ? h.TrailDegs()[h.TrailCount - 1] : -1;
            for (int i = 0; i < 7; i++)
            {
                ChordInfo bc = Theory.Base(h, i);
                bool held = h.Held[i] && h.Cur[i] != null;
                string nm = held ? h.Cur[i] : bc.Name(fl);
                Color col = Theme.Deg[i];
                RectangleF nrct = new RectangleF(pos[i].X - nr, pos[i].Y - nr, nr * 2f, nr * 2f);
                using (GraphicsPath np = Theme.Oval(nrct))
                {
                    if (held) Theme.Glow(g, np, col, 4f);
                    using (Brush nb = new SolidBrush(held ? col : Theme.Card2)) g.FillPath(nb, np);
                    using (Pen npn = new Pen(col, 3f)) g.DrawPath(npn, np);
                }
                if (!held && i == last)
                    using (Pen lp = new Pen(Color.FromArgb(200, Color.White), 1.5f)) g.DrawEllipse(lp, pos[i].X - nr - 5f, pos[i].Y - nr - 5f, nr * 2f + 10f, nr * 2f + 10f);
                Color tc = held ? Theme.OnColor(col) : Theme.Text;
                using (Brush t1 = new SolidBrush(held ? tc : col))
                    g.DrawString(Theory.Roman(bc.Q, i), Theme.F(8f, FontStyle.Bold), t1, new RectangleF(nrct.X, nrct.Y + 6f, nrct.Width, 14f), Theme.Center);
                using (Brush t2 = new SolidBrush(tc))
                    g.DrawString(nm, Theme.F(9f, FontStyle.Bold), t2, new RectangleF(nrct.X - 6f, nrct.Y + 20f, nrct.Width + 12f, nrct.Height - 22f), Theme.Center);
            }

            // header chips and messages
            string head = "KEY " + Theory.PcName(h.Key, fl) + "    OCT " + h.Oct + "    " + Theory.ScaleNames[h.Scale] + "    STICK: " + Theory.JoyNames[h.JoyMode];
            using (Brush hb = new SolidBrush(Theme.Dim)) g.DrawString(head, Theme.F(8.5f, FontStyle.Bold), hb, 14, 10);
            if (Environment.TickCount - h.MsgAt < 2200 && h.Msg.Length > 0)
                using (Brush mb = new SolidBrush(Theme.Amber)) g.DrawString(h.Msg, Theme.F(9f, FontStyle.Bold), mb, new RectangleF(14, 28, Width - 28, 16));

            // progression text
            string[] names = h.TrailNames();
            string prog = names.Length == 0 ? "Play some chords to see your progression here" : string.Join("  \u2192  ", names);
            using (Brush pb2 = new SolidBrush(Theme.Text))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(prog, Theme.F(10f, FontStyle.Bold), pb2, new RectangleF(10, Height - 44, Width - 20, 34), sf);
                sf.Dispose();
            }
        }
    }

    // ------------------------------------------------------------------ list of the 8 stick directions with chord names
    class DirCard : Control
    {
        public Engine Eng;

        public DirCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.WireframeMode ? Color.Black : Theme.Bg0; // (or Theme.Card)
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Theme.WireframeMode ? Color.Black : Theme.Card);
            if (Eng == null) return;
            HiState h = Eng.Hi;
            bool fl = Theory.UseFlats(h.Key);
            int rd = h.Ref;
            if (rd < 0 || rd > 6) rd = 0;
            ChordInfo rb = Theory.Base(h, rd);
            using (Brush hb = new SolidBrush(Theme.Cyan))
                g.DrawString("Stick flavours on " + rb.Name(fl) + "  (" + Theory.JoyNames[h.JoyMode] + ")", Theme.F(9f, FontStyle.Bold), hb, 0, 0);
            int[] order = { 3, 2, 1, 8, 7, 6, 5, 4 };
            for (int n = 0; n < 8; n++)
            {
                int d = order[n];
                int col = n / 4, row = n % 4;
                float x = col * (Width / 2f), y = 22 + row * 26;
                bool act = h.Dir == d;
                if (act)
                    using (GraphicsPath pill = Theme.RR(new RectangleF(x, y - 2, Width / 2f - 8, 24), 12f))
                    using (Brush pb = new SolidBrush(Color.FromArgb(160, Theme.Pink))) g.FillPath(pb, pill);
                using (Brush ab = new SolidBrush(Theme.Amber)) g.DrawString(Theory.Arrow[d], Theme.Sym(11f), ab, x + 6, y);
                using (Brush wb = new SolidBrush(Theme.Dim)) g.DrawString(Theory.DirWords[h.JoyMode][d], Theme.F(8.5f, FontStyle.Regular), wb, x + 30, y + 3);
                ChordInfo m = Theory.Modify(rb, h.JoyMode, d);
                using (Brush nb = new SolidBrush(Theme.Text)) g.DrawString(m.Name(fl), Theme.F(9.5f, FontStyle.Bold), nb, x + 118, y + 2);
            }
        }
    }

    // ------------------------------------------------------------------ small "type a name" dialog
    class PromptForm : Form
    {
        TextBox tb;
        public string Result;

        public PromptForm(string title, string label, string def)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            BackColor = Theme.Bg1;
            ForeColor = Theme.Text;
            ClientSize = new Size(380, 130);
            Font = Theme.F(9f, FontStyle.Regular);
            Label l = new Label();
            l.Text = label; l.SetBounds(16, 14, 348, 18); l.ForeColor = Theme.Text;
            Controls.Add(l);
            tb = new TextBox();
            tb.SetBounds(16, 38, 348, 24); tb.Text = def; tb.MaxLength = 32;
            tb.BackColor = Theme.Card2; tb.ForeColor = Theme.Text; tb.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(tb);
            PopButton ok = new PopButton();
            ok.Text = "OK"; ok.SetBounds(188, 80, 84, 32); ok.Accent = Theme.Pink;
            ok.Click += delegate { Result = tb.Text; DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);
            PopButton cn = new PopButton();
            cn.Text = "Cancel"; cn.SetBounds(280, 80, 84, 32); cn.Accent = Theme.Violet; cn.Ghost = true;
            cn.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(cn);
            tb.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Result = tb.Text; DialogResult = DialogResult.OK; Close(); }
            };
            Shown += delegate { tb.Focus(); tb.SelectAll(); };
        }

        public static string Ask(IWin32Window owner, string title, string label, string def)
        {
            using (PromptForm f = new PromptForm(title, label, def))
            {
                if (f.ShowDialog(owner) == DialogResult.OK) return f.Result;
            }
            return null;
        }
    }
}
