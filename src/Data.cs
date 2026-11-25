// GamepadMIDI v2 - Data: settings file, profile templates, default layouts and the HiChord control bindings.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace GamepadMidi
{
    // ------------------------------------------------------------------ hardware / app settings (settings.txt)
    class Settings
    {
        public Dictionary<string, string> D = new Dictionary<string, string>();
        public bool Dirty;
        string path;

        public Settings()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GamepadMIDI");
            try { Directory.CreateDirectory(dir); } catch { }
            path = Path.Combine(dir, "settings.txt");
            try
            {
                if (File.Exists(path))
                {
                    foreach (string line in File.ReadAllLines(path))
                    {
                        int i = line.IndexOf('=');
                        if (i > 0) D[line.Substring(0, i)] = line.Substring(i + 1);
                    }
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string> kv in D) sb.AppendLine(kv.Key + "=" + kv.Value);
                File.WriteAllText(path, sb.ToString());
            }
            catch { }
            Dirty = false;
        }

        public string Get(string k, string def) { string v; return D.TryGetValue(k, out v) ? v : def; }
        public void Set(string k, string v) { D[k] = v; Dirty = true; }
    }

    // ------------------------------------------------------------------ a profile = a template of musical settings
    class Template
    {
        public string Name = "Default";
        public Dictionary<string, string> D = new Dictionary<string, string>();

        public string Get(string k, string def) { string v; return D.TryGetValue(k, out v) ? v : def; }
        public void Set(string k, string v) { D[k] = v; }

        public int GetInt(string k, int def)
        {
            string v;
            int r;
            if (D.TryGetValue(k, out v) && int.TryParse(v, out r)) return r;
            return def;
        }

        public Template Clone(string newName)
        {
            Template t = new Template();
            t.Name = newName;
            foreach (KeyValuePair<string, string> kv in D) t.D[kv.Key] = kv.Value;
            return t;
        }
    }

    static class Templates
    {
        public static string Dir
        {
            get
            {
                return Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GamepadMIDI"), "profiles");
            }
        }

        public static string Sanitize(string n)
        {
            if (n == null) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in n.Trim())
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_') sb.Append(c);
            string s = sb.ToString().Trim();
            if (s.Length > 32) s = s.Substring(0, 32).Trim();
            return s;
        }

        static string PathOf(string name) { return Path.Combine(Dir, Sanitize(name) + ".gmp"); }

        public static bool Exists(string name) { try { return File.Exists(PathOf(name)); } catch { return false; } }

        public static List<string> List()
        {
            List<string> r = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(Dir, "*.gmp")) r.Add(Path.GetFileNameWithoutExtension(f));
            }
            catch { }
            r.Sort(StringComparer.OrdinalIgnoreCase);
            r.Remove("Default");
            r.Insert(0, "Default");
            return r;
        }

        public static Template Load(string name)
        {
            try
            {
                string p = PathOf(name);
                if (!File.Exists(p)) return null;
                Template t = new Template();
                t.Name = name;
                foreach (string line in File.ReadAllLines(p))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) t.D[line.Substring(0, i)] = line.Substring(i + 1);
                }
                return t;
            }
            catch { return null; }
        }

        public static void Save(Template t)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                StringBuilder sb = new StringBuilder();
                foreach (KeyValuePair<string, string> kv in t.D) sb.AppendLine(kv.Key + "=" + kv.Value);
                File.WriteAllText(PathOf(t.Name), sb.ToString());
            }
            catch { }
        }

        public static void Delete(string name)
        {
            try { File.Delete(PathOf(name)); } catch { }
        }

        // the factory notes and chords
        public static Template Factory()
        {
            Template t = new Template();
            PadStyle[] styles = { PadStyle.Classic, PadStyle.PS4 };
            foreach (PadStyle s in styles)
            {
                List<Pad> p = Layouts.BuildPads(s);
                List<Knob> k = Layouts.BuildKnobs(s, p);
                Profile.Store(t, s, p, k);
            }
            Profile.StoreHi(t, new HiState());
            return t;
        }

        // every button does nothing until the user assigns it
        public static Template Blank()
        {
            Template t = new Template();
            PadStyle[] styles = { PadStyle.Classic, PadStyle.PS4 };
            foreach (PadStyle s in styles)
            {
                List<Pad> p = Layouts.BuildPads(s);
                List<Knob> k = Layouts.BuildKnobs(s, p);
                foreach (Pad pd in p) pd.Map.Kind = 0;
                Profile.Store(t, s, p, k);
            }
            Profile.StoreHi(t, new HiState());
            return t;
        }

        // first run of v2: whatever notes/chords/knobs you had become the "Default" profile
        public static void EnsureDefault(Settings st)
        {
            if (Exists("Default")) return;
            Template t = Factory();
            t.Name = "Default";
            foreach (KeyValuePair<string, string> kv in st.D)
            {
                string k = kv.Key;
                if (k.StartsWith("classic.map.") || k.StartsWith("classic.knob.") || k.StartsWith("ps4.map.") || k.StartsWith("ps4.knob."))
                    t.D[k] = kv.Value;
            }
            Save(t);
        }
    }

    // ------------------------------------------------------------------ moving settings in and out of pads/knobs
    static class Profile
    {
        static string Pfx(PadStyle s) { return s == PadStyle.Classic ? "classic." : "ps4."; }
        static int I(string[] a, int i, int def) { int r; if (i < a.Length && int.TryParse(a[i], out r)) return r; return def; }
        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        // ---- musical part: stored in the profile template
        public static void Store(Template t, PadStyle s, List<Pad> pads, List<Knob> knobs)
        {
            string p = Pfx(s);
            foreach (Pad pd in pads)
            {
                Mapping m = pd.Map;
                t.Set(p + "map." + pd.Id, m.Kind + "," + m.Root + "," + m.Chord + "," + m.Vel);
            }
            foreach (Knob k in knobs)
                t.Set(p + "knob." + k.Id, (k.PitchBend ? 1 : 0) + "," + k.CC + "," + (int)(k.Speed * 10f + 0.5f) + "," + (k.Reverse ? 1 : 0) + "," + k.RestMid);
        }

        public static void Load(Template t, PadStyle s, List<Pad> pads, List<Knob> knobs)
        {
            string p = Pfx(s);
            foreach (Pad pd in pads)
            {
                string v = t.Get(p + "map." + pd.Id, null);
                if (v != null)
                {
                    string[] a = v.Split(',');
                    pd.Map.Kind = Clamp(I(a, 0, pd.Map.Kind), 0, 4);
                    pd.Map.Root = Clamp(I(a, 1, pd.Map.Root), 0, 127);
                    pd.Map.Chord = Clamp(I(a, 2, pd.Map.Chord), 0, Music.ChordIv.Length - 1);
                    pd.Map.Vel = Clamp(I(a, 3, pd.Map.Vel), 1, 127);
                }
            }
            foreach (Knob k in knobs)
            {
                string v = t.Get(p + "knob." + k.Id, null);
                if (v != null)
                {
                    string[] a = v.Split(',');
                    k.PitchBend = I(a, 0, k.PitchBend ? 1 : 0) == 1;
                    k.CC = Clamp(I(a, 1, k.CC), 0, 127);
                    k.Speed = Clamp(I(a, 2, (int)(k.Speed * 10f + 0.5f)), 1, 60) / 10f;
                    k.Reverse = I(a, 3, 0) == 1;
                    k.RestMid = Clamp(I(a, 4, k.RestMid), 0, 1);
                    k.ResetPos();
                }
            }
        }

        // ---- hardware part (which physical button is which): stays in settings.txt, shared by all profiles
        public static void StoreHw(Settings st, PadStyle s, List<Pad> pads)
        {
            string p = Pfx(s);
            foreach (Pad pd in pads) st.Set(p + "src." + pd.Id, pd.Src.Ser());
        }

        public static void LoadHw(Settings st, PadStyle s, List<Pad> pads)
        {
            string p = Pfx(s);
            foreach (Pad pd in pads)
            {
                string v = st.Get(p + "src." + pd.Id, null);
                if (v != null) pd.Src = Source.Parse(v);
            }
        }

        // ---- HiChord settings
        public static void StoreHi(Template t, HiState h)
        {
            t.Set("hi.on", h.On ? "1" : "0");
            t.Set("hi.key", h.Key.ToString());
            t.Set("hi.oct", h.Oct.ToString());
            t.Set("hi.scale", h.Scale.ToString());
            t.Set("hi.joy", h.JoyMode.ToString());
            t.Set("hi.bass", h.Bass.ToString());
            t.Set("hi.vlead", h.VLead ? "1" : "0");
            t.Set("hi.vel", h.Vel.ToString());
        }

        // returns whether HiChord mode was on in the profile
        public static bool LoadHi(Template t, HiState h)
        {
            h.Key = Clamp(t.GetInt("hi.key", 0), 0, 11);
            h.Oct = Clamp(t.GetInt("hi.oct", 0), -2, 2);
            h.Scale = Clamp(t.GetInt("hi.scale", 0), 0, Theory.Scales.Length - 1);
            h.JoyMode = Clamp(t.GetInt("hi.joy", 0), 0, 4);
            h.Bass = Clamp(t.GetInt("hi.bass", 0), 0, 2);
            h.VLead = t.GetInt("hi.vlead", 0) == 1;
            h.Vel = Clamp(t.GetInt("hi.vel", 100), 1, 127);
            for (int i = 0; i < 7; i++) { h.Lock[i] = null; h.Dirty[i] = true; }
            return t.GetInt("hi.on", 0) == 1;
        }
    }

    // ------------------------------------------------------------------ default layouts (what is drawn + default mappings)
    static class Layouts
    {
        static Pad Mk(string id, string label, string name, bool notes, bool knob, int shape, int x, int y, int w, int h, Color c, Source s, int kind, int root, int chord)
        {
            Pad p = new Pad();
            p.Id = id; p.Label = label; p.Name = name; p.Notes = notes; p.KnobInput = knob; p.Shape = shape;
            p.R = new Rectangle(x, y, w, h); p.Col = c; p.Src = s;
            p.Map.Kind = kind; p.Map.Root = root; p.Map.Chord = chord; p.Map.Vel = 100;
            return p;
        }

        static Source B(int i) { return new Source(SrcType.Button, i, 0); }
        static Source Pv(int i) { return new Source(SrcType.Pov, i, 0); }
        static Source Ax(int i, int sign) { return new Source(SrcType.Axis, i, sign); }

        public static List<Pad> BuildPads(PadStyle s)
        {
            List<Pad> L = new List<Pad>();
            Color blue = Theme.Cyan, red = Color.FromArgb(255, 99, 110), pink = Color.FromArgb(255, 130, 220),
                  green = Theme.Lime, grey = Color.FromArgb(176, 168, 214);

            if (s == PadStyle.Classic)
            {
                L.Add(Mk("Cross", "\u2715", "X (cross) button", true, false, 0, 441, 211, 48, 48, blue, B(2), 1, 60, 0));
                L.Add(Mk("Circle", "\u25CB", "circle button", true, false, 0, 491, 161, 48, 48, red, B(1), 1, 62, 0));
                L.Add(Mk("Square", "\u25A1", "square button", true, false, 0, 391, 161, 48, 48, pink, B(3), 1, 64, 0));
                L.Add(Mk("Triangle", "\u25B3", "triangle button", true, false, 0, 441, 111, 48, 48, green, B(0), 1, 67, 0));
                L.Add(Mk("L1", "L1", "L1 button", true, false, 1, 60, 38, 120, 26, grey, B(6), 2, 48, 0));
                L.Add(Mk("L2", "L2", "L2 button", true, false, 1, 60, 8, 120, 26, grey, B(4), 2, 57, 5));
                L.Add(Mk("R1", "R1", "R1 button", true, false, 1, 420, 38, 120, 26, grey, B(7), 2, 53, 0));
                L.Add(Mk("R2", "R2", "R2 button", true, false, 1, 420, 8, 120, 26, grey, B(5), 2, 55, 4));
                L.Add(Mk("Select", "SELECT", "SELECT button", true, false, 1, 225, 262, 70, 24, grey, B(8), 3, 60, 0));
                L.Add(Mk("Start", "START", "START button", true, false, 1, 305, 262, 70, 24, grey, B(9), 4, 60, 0));
                L.Add(Mk("DUp", "\u25B2", "D-pad UP", false, true, 2, 112, 116, 46, 46, grey, Pv(0), 0, 60, 0));
                L.Add(Mk("DDown", "\u25BC", "D-pad DOWN", false, true, 2, 112, 208, 46, 46, grey, Pv(2), 0, 60, 0));
                L.Add(Mk("DLeft", "\u25C0", "D-pad LEFT", false, true, 2, 66, 162, 46, 46, grey, Pv(3), 0, 60, 0));
                L.Add(Mk("DRight", "\u25B6", "D-pad RIGHT", false, true, 2, 158, 162, 46, 46, grey, Pv(1), 0, 60, 0));
            }
            else if (s == PadStyle.PS4 || s == PadStyle.PS5)
            {
                L.Add(Mk("Cross", "\u2715", "X (cross) button", true, false, 0, 471, 163, 38, 38, blue, B(1), 1, 60, 0));
                L.Add(Mk("Circle", "\u25CB", "circle button", true, false, 0, 509, 125, 38, 38, red, B(2), 1, 62, 0));
                L.Add(Mk("Square", "\u25A1", "square button", true, false, 0, 433, 125, 38, 38, pink, B(0), 1, 64, 0));
                L.Add(Mk("Triangle", "\u25B3", "triangle button", true, false, 0, 471, 87, 38, 38, green, B(3), 1, 67, 0));
                L.Add(Mk("L1", "L1", "L1 button", true, false, 1, 50, 36, 110, 24, grey, B(4), 2, 48, 0));
                L.Add(Mk("L2", "L2", "L2 trigger", true, false, 1, 50, 6, 110, 26, grey, B(6), 2, 57, 5));
                L.Add(Mk("R1", "R1", "R1 button", true, false, 1, 440, 36, 110, 24, grey, B(5), 2, 53, 0));
                L.Add(Mk("R2", "R2", "R2 trigger", true, false, 1, 440, 6, 110, 26, grey, B(7), 2, 55, 4));
                L.Add(Mk("Share", "SHARE", "SHARE button", true, false, 1, 180, 80, 40, 20, grey, B(8), 3, 60, 0));
                L.Add(Mk("Options", "OPT", "OPTIONS button", true, false, 1, 380, 80, 40, 20, grey, B(9), 4, 60, 0));
                L.Add(Mk("L3", "L3", "left stick (push it in)", true, false, 0, 172, 212, 76, 76, grey, B(10), 0, 60, 0));
                L.Add(Mk("R3", "R3", "right stick (push it in)", true, false, 0, 352, 212, 76, 76, grey, B(11), 0, 60, 0));
                L.Add(Mk("PS", "PS", "PS button", true, false, 0, 286, 236, 28, 28, grey, B(12), 0, 60, 0));
                L.Add(Mk("Touch", "TOUCHPAD", "touchpad (press it in)", true, false, 1, 230, 72, 140, 24, grey, B(13), 0, 60, 0));
                L.Add(Mk("DUp", "\u25B2", "D-pad UP", true, false, 2, 91, 87, 38, 38, grey, Pv(0), 1, 72, 0));
                L.Add(Mk("DRight", "\u25B6", "D-pad RIGHT", true, false, 2, 129, 125, 38, 38, grey, Pv(1), 1, 74, 0));
                L.Add(Mk("DDown", "\u25BC", "D-pad DOWN", true, false, 2, 91, 163, 38, 38, grey, Pv(2), 1, 76, 0));
                L.Add(Mk("DLeft", "\u25C0", "D-pad LEFT", true, false, 2, 53, 125, 38, 38, grey, Pv(3), 1, 79, 0));
                Pad lu = Mk("LUp", "", "left stick UP", false, true, 0, 0, 0, 0, 0, grey, Ax(1, -1), 0, 60, 0);
                Pad ld = Mk("LDown", "", "left stick DOWN", false, true, 0, 0, 0, 0, 0, grey, Ax(1, 1), 0, 60, 0);
                Pad ll = Mk("LLeft", "", "left stick LEFT", false, true, 0, 0, 0, 0, 0, grey, Ax(0, -1), 0, 60, 0);
                Pad lr = Mk("LRight", "", "left stick RIGHT", false, true, 0, 0, 0, 0, 0, grey, Ax(0, 1), 0, 60, 0);
                Pad ru = Mk("RUp", "", "right stick UP", false, true, 0, 0, 0, 0, 0, grey, Ax(3, -1), 0, 60, 0);
                Pad rd = Mk("RDown", "", "right stick DOWN", false, true, 0, 0, 0, 0, 0, grey, Ax(3, 1), 0, 60, 0);
                Pad rl = Mk("RLeft", "", "right stick LEFT", false, true, 0, 0, 0, 0, 0, grey, Ax(2, -1), 0, 60, 0);
                Pad rr = Mk("RRight", "", "right stick RIGHT", false, true, 0, 0, 0, 0, 0, grey, Ax(2, 1), 0, 60, 0);
                Pad[] sticks = { lu, ld, ll, lr, ru, rd, rl, rr };
                Pad l3 = null, r3 = null;
                foreach (Pad p in L) { if (p.Id == "L3") l3 = p; if (p.Id == "R3") r3 = p; }
                for (int i = 0; i < sticks.Length; i++)
                {
                    sticks[i].Visible = false;
                    sticks[i].Verb = "Push";
                    sticks[i].ShowAs = i < 4 ? l3 : r3;
                    L.Add(sticks[i]);
                }
                l3.DyN = lu; l3.DyP = ld; l3.DxN = ll; l3.DxP = lr;
                r3.DyN = ru; r3.DyP = rd; r3.DxN = rl; r3.DxP = rr;
            }
            else // Xbox and Generic
            {
                L.Add(Mk("Cross", "A", "A button", true, false, 0, 471, 163, 38, 38, green, B(1), 1, 60, 0));
                L.Add(Mk("Circle", "B", "B button", true, false, 0, 509, 125, 38, 38, red, B(2), 1, 62, 0));
                L.Add(Mk("Square", "X", "X button", true, false, 0, 433, 125, 38, 38, blue, B(0), 1, 64, 0));
                L.Add(Mk("Triangle", "Y", "Y button", true, false, 0, 471, 87, 38, 38, Theme.Amber, B(3), 1, 67, 0));
                L.Add(Mk("L1", "LB", "Left Bumper", true, false, 1, 50, 36, 110, 24, grey, B(4), 2, 48, 0));
                L.Add(Mk("L2", "LT", "Left Trigger", true, false, 1, 50, 6, 110, 26, grey, B(6), 2, 57, 5));
                L.Add(Mk("R1", "RB", "Right Bumper", true, false, 1, 440, 36, 110, 24, grey, B(5), 2, 53, 0));
                L.Add(Mk("R2", "RT", "Right Trigger", true, false, 1, 440, 6, 110, 26, grey, B(7), 2, 55, 4));
                L.Add(Mk("Share", "VIEW", "View button", true, false, 1, 180, 80, 40, 20, grey, B(8), 3, 60, 0));
                L.Add(Mk("Options", "MENU", "Menu button", true, false, 1, 380, 80, 40, 20, grey, B(9), 4, 60, 0));
                L.Add(Mk("L3", "L3", "left stick (push it in)", true, false, 0, 172, 212, 76, 76, grey, B(10), 0, 60, 0));
                L.Add(Mk("R3", "R3", "right stick (push it in)", true, false, 0, 352, 212, 76, 76, grey, B(11), 0, 60, 0));
                L.Add(Mk("PS", "\u2302", "Home button", true, false, 0, 286, 236, 28, 28, grey, B(12), 0, 60, 0));
                L.Add(Mk("DUp", "\u25B2", "D-pad UP", true, false, 2, 91, 87, 38, 38, grey, Pv(0), 1, 72, 0));
                L.Add(Mk("DRight", "\u25B6", "D-pad RIGHT", true, false, 2, 129, 125, 38, 38, grey, Pv(1), 1, 74, 0));
                L.Add(Mk("DDown", "\u25BC", "D-pad DOWN", true, false, 2, 91, 163, 38, 38, grey, Pv(2), 1, 76, 0));
                L.Add(Mk("DLeft", "\u25C0", "D-pad LEFT", true, false, 2, 53, 125, 38, 38, grey, Pv(3), 1, 79, 0));
                Pad lu = Mk("LUp", "", "left stick UP", false, true, 0, 0, 0, 0, 0, grey, Ax(1, -1), 0, 60, 0);
                Pad ld = Mk("LDown", "", "left stick DOWN", false, true, 0, 0, 0, 0, 0, grey, Ax(1, 1), 0, 60, 0);
                Pad ll = Mk("LLeft", "", "left stick LEFT", false, true, 0, 0, 0, 0, 0, grey, Ax(0, -1), 0, 60, 0);
                Pad lr = Mk("LRight", "", "left stick RIGHT", false, true, 0, 0, 0, 0, 0, grey, Ax(0, 1), 0, 60, 0);
                Pad ru = Mk("RUp", "", "right stick UP", false, true, 0, 0, 0, 0, 0, grey, Ax(3, -1), 0, 60, 0);
                Pad rd = Mk("RDown", "", "right stick DOWN", false, true, 0, 0, 0, 0, 0, grey, Ax(3, 1), 0, 60, 0);
                Pad rl = Mk("RLeft", "", "right stick LEFT", false, true, 0, 0, 0, 0, 0, grey, Ax(2, -1), 0, 60, 0);
                Pad rr = Mk("RRight", "", "right stick RIGHT", false, true, 0, 0, 0, 0, 0, grey, Ax(2, 1), 0, 60, 0);
                Pad[] sticks = { lu, ld, ll, lr, ru, rd, rl, rr };
                Pad l3 = null, r3 = null;
                foreach (Pad p in L) { if (p.Id == "L3") l3 = p; if (p.Id == "R3") r3 = p; }
                for (int i = 0; i < sticks.Length; i++)
                {
                    sticks[i].Visible = false;
                    sticks[i].Verb = "Push";
                    sticks[i].ShowAs = i < 4 ? l3 : r3;
                    L.Add(sticks[i]);
                }
                l3.DyN = lu; l3.DyP = ld; l3.DxN = ll; l3.DxP = lr;
                r3.DyN = ru; r3.DyP = rd; r3.DxN = rl; r3.DxP = rr;
            }
            return L;
        }

        static Knob MkKnob(string id, string label, string inc, string dec, bool spring, bool pb, int cc, float speed, int restMid, int cx, int cy, int r, List<Pad> pads)
        {
            Knob k = new Knob();
            k.Id = id; k.Label = label; k.Spring = spring; k.PitchBend = pb; k.CC = cc; k.Speed = speed; k.RestMid = restMid;
            k.Center = new Point(cx, cy); k.Radius = r;
            foreach (Pad p in pads) { if (p.Id == inc) k.Inc = p; if (p.Id == dec) k.Dec = p; }
            k.ResetPos();
            return k;
        }

        public static List<Knob> BuildKnobs(PadStyle s, List<Pad> pads)
        {
            List<Knob> K = new List<Knob>();
            if (s == PadStyle.Classic)
            {
                K.Add(MkKnob("K1", "D-pad \u2191\u2193", "DUp", "DDown", false, false, 1, 1f, 0, 250, 150, 30, pads));
                K.Add(MkKnob("K2", "D-pad \u2190\u2192", "DRight", "DLeft", true, true, 1, 1f, 0, 350, 150, 30, pads));
            }
            else
            {
                K.Add(MkKnob("K1", "Left stick \u2195", "LUp", "LDown", false, false, 1, 1f, 0, 232, 150, 22, pads));
                K.Add(MkKnob("K2", "Left stick \u2194", "LRight", "LLeft", true, true, 1, 1f, 0, 288, 150, 22, pads));
                K.Add(MkKnob("K3", "Right stick \u2195", "RUp", "RDown", false, false, 74, 1f, 0, 344, 150, 22, pads));
                K.Add(MkKnob("K4", "Right stick \u2194", "RRight", "RLeft", true, false, 71, 1f, 0, 400, 150, 22, pads));
            }
            return K;
        }

        // ---------------------------------------------------------------- HiChord: which pad does what
        static Pad Find(List<Pad> pads, string id)
        {
            foreach (Pad p in pads) if (p.Id == id) return p;
            return null;
        }

        static void Tag(Pad p, string tag, string sub)
        {
            if (p == null) return;
            p.HiTag = tag; p.HiSub = sub;
        }

        static bool IsJoy(HiBind b, Pad p)
        {
            return p != null && (p == b.JL || p == b.JR || p == b.JU || p == b.JD);
        }

        public static HiBind BindHi(PadStyle s, List<Pad> pads, List<Knob> knobs)
        {
            HiBind b = new HiBind();
            string[] ids;
            if (s == PadStyle.PS4 || s == PadStyle.PS5)
                ids = new string[] { "Square", "Triangle", "Circle", "Cross", "L1", "R1", "R2" };
            else if (s == PadStyle.Classic)
                ids = new string[] { "Square", "Triangle", "Circle", "Cross", "L1", "R1", "R2" };
            else // Xbox / Generic
                ids = new string[] { "Square", "Triangle", "Circle", "Cross", "L1", "R1", "R2" }; // Maps to X, Y, B, A, LB, RB, RT

            for (int i = 0; i < 7; i++)
            {
                b.Chord[i] = Find(pads, ids[i]);
                if (b.Chord[i] != null) { b.Chord[i].HiCol = Theme.Deg[i]; b.Chord[i].HiTag = Theory.Roman(0, i); b.Chord[i].HiSub = ""; }
            }
            b.Inv = Find(pads, "L2");
            Tag(b.Inv, "INV", "inversion");

            if (s == PadStyle.Classic)
            {
                b.Lock = Find(pads, "Start"); Tag(b.Lock, "LOCK", "");
                b.Gray = Find(pads, "Select"); Tag(b.Gray, "KEY/OCT", "");
                b.JL = Find(pads, "DLeft"); b.JR = Find(pads, "DRight"); b.JU = Find(pads, "DUp"); b.JD = Find(pads, "DDown");
                Tag(b.JL, "\u25C0", ""); Tag(b.JR, "\u25B6", ""); Tag(b.JU, "\u25B2", ""); Tag(b.JD, "\u25BC", "");
            }
            else // PS4, PS5, Xbox, Generic
            {
                b.Lock = Find(pads, "L3"); Tag(b.Lock, "LOCK", "click");
                b.Panic = Find(pads, "R3"); Tag(b.Panic, "PANIC", "click");
                b.ModeP = Find(pads, "Options"); Tag(b.ModeP, "MODE", "");
                b.ScaleP = Find(pads, "Share"); Tag(b.ScaleP, "SCALE", "");
                b.JL = Find(pads, "LLeft"); b.JR = Find(pads, "LRight"); b.JU = Find(pads, "LUp"); b.JD = Find(pads, "LDown");
                b.KeyL = Find(pads, "DLeft"); Tag(b.KeyL, "KEY-", "");
                b.KeyR = Find(pads, "DRight"); Tag(b.KeyR, "KEY+", "");
                b.OctU = Find(pads, "DUp"); Tag(b.OctU, "OCT+", "");
                b.OctD = Find(pads, "DDown"); Tag(b.OctD, "OCT-", "");

                if (s == PadStyle.PS4 || s == PadStyle.PS5)
                {
                    b.Touch = Find(pads, "Touch");
                }
            }
            foreach (Knob k in knobs) k.HiBlocked = IsJoy(b, k.Inc) || IsJoy(b, k.Dec);
            return b;
        }

        // short text shown in the HiChord panel
        public static string HiLegend(PadStyle s)
        {
            if (s == PadStyle.PS4)
                return "Square \u25B8 Triangle \u25B8 Circle \u25B8 X \u25B8 L1 \u25B8 R1 \u25B8 R2 = chords I to VII.   Left stick = chord flavour (let go to snap back).   L2 = inversion.   Click L3 while holding a chord + the stick = lock.   D-pad: left/right = key, up/down = octave.   Options = stick mode, Share = scale, R3 = panic.   Hold one chord, press another = slash chord.";
            return "Square \u25B8 Triangle \u25B8 Circle \u25B8 X \u25B8 L1 \u25B8 R1 \u25B8 R2 = chords I to VII.   D-pad = chord flavour (diagonals work).   L2 = inversion.   START while holding a chord + D-pad = lock.   Hold SELECT + D-pad: left/right = key, up/down = octave.   SELECT + START = stick mode, SELECT + L2 = scale.   Hold one chord, press another = slash chord.";
        }
    }
}
