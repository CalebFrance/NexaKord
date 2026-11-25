// GamepadMIDI v2 - Theory: scales, diatonic chords, the five HiChord joystick modes, voicing and the little theory blurbs.

using System;
using System.Collections.Generic;
using System.Text;

namespace GamepadMidi
{
    static class Theory
    {
        public const int BaseMidi = 48; // C3: chords are built around here

        public static readonly string[] ScaleNames =
        {
            "Major", "Minor", "Harmonic Minor", "Melodic Minor", "Major Pentatonic",
            "Minor Pentatonic", "Blues", "Dorian", "Mixolydian", "Lydian"
        };

        public static readonly int[][] Scales =
        {
            new int[] { 0, 2, 4, 5, 7, 9, 11 },
            new int[] { 0, 2, 3, 5, 7, 8, 10 },
            new int[] { 0, 2, 3, 5, 7, 8, 11 },
            new int[] { 0, 2, 3, 5, 7, 9, 11 },
            new int[] { 0, 2, 4, 7, 9 },
            new int[] { 0, 3, 5, 7, 10 },
            new int[] { 0, 3, 5, 6, 7, 10 },
            new int[] { 0, 2, 3, 5, 7, 9, 10 },
            new int[] { 0, 2, 4, 5, 7, 9, 10 },
            new int[] { 0, 2, 4, 6, 7, 9, 11 }
        };

        public static readonly string[] JoyNames = { "Default", "Extended", "Chromatic", "Borrow", "OG" };
        public static readonly string[] BassNames = { "Off", "Root (2 octaves down)", "Slash (auto)" };

        static readonly string[] Sharps = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        static readonly string[] Flats = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };
        static readonly string[] Num = { "I", "II", "III", "IV", "V", "VI", "VII" };

        // 0 = centre, then counter-clockwise from the right
        public static readonly string[] Arrow = { "\u2022", "\u2192", "\u2197", "\u2191", "\u2196", "\u2190", "\u2199", "\u2193", "\u2198" };

        public static readonly string[] Feel = { "Home", "Soft", "Dark", "Lift", "Tension", "Emotional", "Suspense" };
        public static readonly string[] Func = { "Tonic", "Supertonic", "Mediant", "Subdominant", "Dominant", "Submediant", "Leading tone" };

        public static readonly string[][] DirWords =
        {
            new string[] { "Base", "Jazzy", "Bluesy", "Flip", "Dreamy", "Dark", "Sweet", "Open", "Lush" },
            new string[] { "Base", "Open", "Funk", "Flip", "Bittersweet", "Tense", "Sparkle", "Crunch", "Modal" },
            new string[] { "Base", "+1 semitone", "Spicy", "Film noir", "Spanish", "-1 semitone", "Smooth", "Warm", "Bittersweet" },
            new string[] { "Base", "Passing", "Backdoor", "Push home", "Slick sub", "Flat-side", "Borrowed", "Parallel", "Minor ii-V" },
            new string[] { "Base", "Jazzy", "Bluesy", "Flip", "Dreamy", "Dark", "Sweet", "Open", "Lush" }
        };

        public static readonly string[] ModeBlurb =
        {
            "Pop, rock and soul colours: flip, 7ths, 9ths, sus and dim.",
            "Jazz and R&B colours: 11ths, funk 9ths, the Hendrix chord.",
            "Left/right slide the whole chord a semitone; the rest are altered jazz chords.",
            "Chords from outside the key that still resolve home (secondary dominants, tritone subs).",
            "The old Rev 2.8 voicings: 9ths use a doubled 3rd instead of a true 9th."
        };

        public static string PcName(int n, bool flats) { return (flats ? Flats : Sharps)[((n % 12) + 12) % 12]; }

        // keys that are normally written with flats
        public static bool UseFlats(int key) { return key == 1 || key == 3 || key == 5 || key == 8 || key == 10; }

        public static bool Has(int[] a, int v)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] == v) return true;
            return false;
        }

        public static int[] AddNote(int[] a, int n)
        {
            List<int> l = new List<int>(a);
            if (n >= 0 && n <= 127 && !l.Contains(n)) l.Add(n);
            l.Sort();
            return l.ToArray();
        }

        public static string Roman(int q, int i)
        {
            string s = Num[i];
            if (q == 1) return s.ToLower();
            if (q == 2) return s.ToLower() + "\u00B0";
            if (q == 3) return s + "+";
            return s;
        }

        // ---------------------------------------------------------------- diatonic base chord for button 0..6
        public static ChordInfo Base(HiState h, int deg)
        {
            int[] S = Scales[h.Scale];
            int n = S.Length;
            int tonic = BaseMidi + 12 * h.Oct + h.Key;
            int r = tonic + S[deg % n] + 12 * (deg / n);
            int t = (tonic + S[(deg + 2) % n] + 12 * ((deg + 2) / n)) - r;
            int f = (tonic + S[(deg + 4) % n] + 12 * ((deg + 4) / n)) - r;
            ChordInfo c = new ChordInfo();
            c.Root = r;
            c.Iv = new int[] { 0, t, f };
            if (t == 4 && f == 7) { c.Q = 0; c.Suf = "maj"; }
            else if (t == 3 && f == 7) { c.Q = 1; c.Suf = "min"; }
            else if (t == 3 && f == 6) { c.Q = 2; c.Suf = "dim"; }
            else if (t == 4 && f == 8) { c.Q = 3; c.Suf = "aug"; }
            else if (t == 5 && f == 7) { c.Q = 4; c.Suf = "sus4"; }
            else if (t == 2 && f == 7) { c.Q = 4; c.Suf = "sus2"; }
            else { c.Q = 4; c.Suf = "(" + t + "," + f + ")"; }
            return c;
        }

        static ChordInfo Mk(int root, string suf, params int[] iv)
        {
            ChordInfo c = new ChordInfo();
            c.Root = root; c.Suf = suf; c.Iv = iv; c.Q = 4;
            return c;
        }

        static ChordInfo Shift(ChordInfo b, int s)
        {
            ChordInfo c = new ChordInfo();
            c.Root = b.Root + s; c.Iv = b.Iv; c.Suf = b.Suf; c.Q = b.Q;
            return c;
        }

        // ---------------------------------------------------------------- the joystick: what a direction does to a chord
        public static ChordInfo Modify(ChordInfo b, int mode, int dir)
        {
            if (dir <= 0) return b;
            bool mi = b.Q == 1 || (b.Q == 4 && b.Iv[1] <= 3);   // minor-flavoured third
            switch (mode)
            {
                case 1: return Extended(b, dir, mi);
                case 2: return Chromatic(b, dir);
                case 3: return Borrow(b, dir, mi);
                default: return Standard(b, dir, mi, mode == 4);
            }
        }

        static ChordInfo Flip(ChordInfo b, bool mi)
        {
            if (b.Q == 2 || mi) return Mk(b.Root, "maj", 0, 4, 7);
            return Mk(b.Root, "min", 0, 3, 7);
        }

        static ChordInfo Standard(ChordInfo b, int dir, bool mi, bool og)
        {
            int r = b.Root;
            int q = b.Q;
            switch (dir)
            {
                case 3: return Flip(b, mi);
                case 2: return Mk(r, "7", 0, 4, 7, 10);
                case 1:
                    if (q == 2) return Mk(r, "m7b5", 0, 3, 6, 10);
                    if (q == 3) return Mk(r, "maj7#5", 0, 4, 8, 11);
                    return mi ? Mk(r, "min7", 0, 3, 7, 10) : Mk(r, "maj7", 0, 4, 7, 11);
                case 8:
                    if (q == 2) return Mk(r, "m7b5", 0, 3, 6, 10);
                    if (q == 3) return Mk(r, "maj9#5", 0, 4, 8, 11, 14);
                    if (mi) return og ? Mk(r, "min9", 0, 3, 7, 10, 15) : Mk(r, "min9", 0, 3, 7, 10, 14);
                    return og ? Mk(r, "maj9", 0, 4, 7, 11, 16) : Mk(r, "maj9", 0, 4, 7, 11, 14);
                case 7: return Mk(r, "sus4", 0, 5, 7);
                case 6:
                    if (mi || q == 2) return Mk(r, "sus2", 0, 2, 7);
                    return Mk(r, "6", 0, 4, 7, 9);
                case 5:
                    if (q == 2) return Mk(r, "min", 0, 3, 7);
                    return Mk(r, "dim", 0, 3, 6);
                case 4: return Mk(r, "aug", 0, 4, 8);
            }
            return b;
        }

        static ChordInfo Extended(ChordInfo b, int dir, bool mi)
        {
            int r = b.Root;
            switch (dir)
            {
                case 3: return Flip(b, mi);
                case 7: return Mk(r, "7#9", 0, 4, 7, 10, 15);
                case 5: return Mk(r, "7sus4", 0, 5, 7, 10);
                case 1: return Mk(r, "add11", 0, 4, 7, 17);
                case 4: return Mk(r, "m7b5", 0, 3, 6, 10);
                case 2: return Mk(r, "9", 0, 4, 7, 10, 14);
                case 6: return Mk(r, "add9", 0, 4, 7, 14);
                case 8: return Mk(r, "min11", 0, 3, 7, 10, 17);
            }
            return b;
        }

        static ChordInfo Chromatic(ChordInfo b, int dir)
        {
            int r = b.Root;
            switch (dir)
            {
                case 3: return Mk(r, "min(maj7)", 0, 3, 7, 11);
                case 7: return Mk(r, "maj13", 0, 4, 7, 11, 21);
                case 5: return Shift(b, -1);
                case 1: return Shift(b, 1);
                case 4: return Mk(r, "7b9", 0, 4, 7, 10, 13);
                case 2: return Mk(r, "7alt", 0, 4, 8, 10, 13);
                case 6: return Mk(r, "6/9", 0, 4, 7, 9, 14);
                case 8: return Mk(r, "m7b5", 0, 3, 6, 10);
            }
            return b;
        }

        static ChordInfo Borrow(ChordInfo b, int dir, bool mi)
        {
            int r = b.Root;
            bool minorFam = mi || b.Q == 2;
            switch (dir)
            {
                case 3: return Mk(r, "7", 0, 4, 7, 10);                       // secondary dominant
                case 7: return minorFam ? Mk(r, "maj", 0, 4, 7) : Mk(r, "min", 0, 3, 7);   // parallel maj <-> min
                case 5: return Mk(r - 1, "maj", 0, 4, 7);                     // flat-side major (bVII, bII...)
                case 1: return Mk(r + 1, "dim7", 0, 3, 6, 9);                 // passing diminished
                case 4: return Mk(r + 6, "7", 0, 4, 7, 10);                   // tritone sub
                case 2: return Mk(r - 2, "7", 0, 4, 7, 10);                   // backdoor bVII7
                case 6: return minorFam ? Mk(r, "maj7", 0, 4, 7, 11) : Mk(r, "min7", 0, 3, 7, 10);   // borrowed 7ths
                case 8: return Mk(r, "m7b5", 0, 3, 6, 10);                    // into a minor ii-V
            }
            return b;
        }

        // ---------------------------------------------------------------- turn a chord into actual MIDI notes
        // inv: 0 root, 1 first, 2 second inversion.  bassMode: 0 off, 1 root, 2 slash (chord's 5th / 3rd / sus note), all 2 octaves down.
        public static int[] Voice(ChordInfo c, int inv, int bassMode)
        {
            List<int> l = new List<int>();
            for (int i = 0; i < c.Iv.Length; i++) l.Add(c.Root + c.Iv[i]);
            l.Sort();
            for (int k = 0; k < inv && k < l.Count - 1; k++)
            {
                int lo = l[0];
                l.RemoveAt(0);
                l.Add(lo + 12);
            }
            if (bassMode > 0)
            {
                int off = 0;
                if (bassMode == 2)
                {
                    bool third = Has(c.Iv, 3) || Has(c.Iv, 4);
                    if (!third && Has(c.Iv, 5)) off = 5;
                    else if (!third && Has(c.Iv, 2)) off = 2;
                    else if (Has(c.Iv, 3) && !Has(c.Iv, 4)) off = 3;
                    else off = 7;
                }
                l.Insert(0, c.Root + off - 24);
            }
            List<int> o = new List<int>();
            for (int i = 0; i < l.Count; i++)
            {
                int n = l[i];
                if (n >= 0 && n <= 127 && !o.Contains(n)) o.Add(n);
            }
            o.Sort();
            if (o.Count == 0) o.Add(Math.Max(0, Math.Min(127, c.Root)));
            return o.ToArray();
        }

        // "1 b3 5 b7 9" style formula for the info card
        static readonly string[] L1 = { "1", "b2", "2", "b3", "3", "4", "b5", "5", "#5", "6", "b7", "7" };
        static readonly string[] L2 = { "1", "b9", "9", "#9", "3", "11", "#11", "5", "b13", "13", "b7", "7" };

        public static string Formula(int[] iv)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < iv.Length; i++)
            {
                int pc = iv[i] % 12;
                if (i > 0) sb.Append(' ');
                sb.Append(iv[i] >= 12 ? L2[pc] : L1[pc]);
            }
            return sb.ToString();
        }

        public static string NoteList(int[] notes)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < notes.Length; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append(Music.Name(notes[i]));
            }
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------------ plain-English blurbs
    static class Lore
    {
        static readonly Dictionary<string, string> D = Build();

        static Dictionary<string, string> Build()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            d["maj"] = "Major triad. Bright and stable: the sound of home.";
            d["min"] = "Minor triad. Darker and more emotional.";
            d["dim"] = "Diminished triad. Tense and suspenseful; it wants to resolve.";
            d["aug"] = "Augmented triad. Floating, dreamy, unresolved.";
            d["maj7"] = "Major 7th. Smooth, jazzy, neo-soul.";
            d["min7"] = "Minor 7th. Mellow and warm.";
            d["7"] = "Dominant 7th. Bluesy tension that wants to resolve.";
            d["maj9"] = "Major 9th. Lush and modern.";
            d["min9"] = "Minor 9th. Rich and moody.";
            d["9"] = "Dominant 9th. Smooth funk colour.";
            d["sus4"] = "Suspended 4th. The 3rd is replaced by the 4th: open, like a question.";
            d["sus2"] = "Suspended 2nd. Airy and open.";
            d["6"] = "Major 6th. Sweet and nostalgic.";
            d["m7b5"] = "Half-diminished 7th. Bittersweet; the classic minor ii chord.";
            d["7#9"] = "The Hendrix chord: major and minor 3rds clash on purpose.";
            d["7sus4"] = "Dominant sus. Suspended tension with a bluesy 7th.";
            d["add11"] = "Add 11. Spacious and modern.";
            d["add9"] = "Add 9. Warm sparkle without a 7th.";
            d["min11"] = "Minor 11th. Modal, wide and open.";
            d["min(maj7)"] = "Minor with a major 7th. Film-noir drama (the 'spy' chord).";
            d["maj13"] = "Major 13th. Full, warm, orchestral.";
            d["7b9"] = "Dominant b9. Dark, Spanish-flavoured tension.";
            d["7alt"] = "Altered dominant. Maximum jazz tension.";
            d["6/9"] = "Major 6/9. A classic jazz ending chord.";
            d["maj7#5"] = "Major 7th with a raised 5th. Dreamy and shimmering.";
            d["maj9#5"] = "Major 9th with a raised 5th. Dreamy and very lush.";
            d["dim7"] = "Diminished 7th. Symmetrical and tense: a chromatic passing chord.";
            return d;
        }

        public static string Blurb(string suf)
        {
            string s;
            if (suf != null && D.TryGetValue(suf, out s)) return s;
            return "Stacked from the notes of your scale.";
        }
    }
}
