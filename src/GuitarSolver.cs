// NexaKord v3 - GuitarSolver: turns MIDI notes into fret positions on a 6-string, 22-fret guitar (standard tuning).
// If a playable chord shape exists it returns that shape ("constellation"); otherwise every position of every note.
using System;
using System.Collections.Generic;

namespace GamepadMidi
{
    class FretPos
    {
        public int Str;
        public int Fret;
        public bool Root;
    }

    class FretResult
    {
        public bool Constellation;
        public int[] Frets = new int[6];
        public List<FretPos> Dots = new List<FretPos>();
        public FretResult() { for (int i = 0; i < 6; i++) Frets[i] = -1; }
    }

    static class GuitarSolver
    {
        public static readonly int[] Tuning = { 40, 45, 50, 55, 59, 64 }; // E2 A2 D3 G3 B3 E4
        public const int MaxFret = 22;

        // Dictionary format: "RootPC_Suffix" -> { LowE, A, D, G, B, HighE }
        // -1 = Muted (x), 0 = Open (0)
        static Dictionary<string, int[]> Shapes = new Dictionary<string, int[]>()
        {
            // MAJOR
            { "0_maj", new int[] { -1, 3, 2, 0, 1, 0 } }, // C
            { "2_maj", new int[] { -1, -1, 0, 2, 3, 2 } }, // D
            { "4_maj", new int[] { 0, 2, 2, 1, 0, 0 } }, // E
            { "5_maj", new int[] { 1, 3, 3, 2, 1, 1 } }, // F (Barre)
            { "7_maj", new int[] { 3, 2, 0, 0, 0, 3 } }, // G
            { "9_maj", new int[] { -1, 0, 2, 2, 2, 0 } }, // A
            { "11_maj", new int[] { -1, 2, 4, 4, 4, 2 } }, // B (Barre)
            
            // MINOR
            { "0_min", new int[] { -1, 3, 5, 5, 4, 3 } }, // Cm
            { "2_min", new int[] { -1, -1, 0, 2, 3, 1 } }, // Dm
            { "4_min", new int[] { 0, 2, 2, 0, 0, 0 } }, // Em
            { "5_min", new int[] { 1, 3, 3, 1, 1, 1 } }, // Fm (Barre)
            { "7_min", new int[] { 3, 5, 5, 3, 3, 3 } }, // Gm (Barre)
            { "9_min", new int[] { -1, 0, 2, 2, 1, 0 } }, // Am
            { "11_min", new int[] { -1, 2, 4, 4, 3, 2 } }, // Bm (Barre)

            // DOMINANT 7
            { "0_7", new int[] { -1, 3, 2, 3, 1, 0 } }, // C7
            { "2_7", new int[] { -1, -1, 0, 2, 1, 2 } }, // D7
            { "4_7", new int[] { 0, 2, 0, 1, 0, 0 } }, // E7
            { "7_7", new int[] { 3, 2, 0, 0, 0, 1 } }, // G7
            { "9_7", new int[] { -1, 0, 2, 0, 2, 0 } }, // A7
            { "11_7", new int[] { -1, 2, 1, 2, 0, 2 } }, // B7

            // MAJOR 7
            { "0_maj7", new int[] { -1, 3, 2, 0, 0, 0 } }, // Cmaj7
            { "2_maj7", new int[] { -1, -1, 0, 2, 2, 2 } }, // Dmaj7
            { "4_maj7", new int[] { 0, 2, 1, 1, 0, 0 } }, // Emaj7
            { "7_maj7", new int[] { 3, 2, 0, 0, 0, 2 } }, // Gmaj7
            { "9_maj7", new int[] { -1, 0, 2, 1, 2, 0 } }, // Amaj7
            { "11_maj7", new int[] { -1, 2, 4, 3, 4, 2 } }, // Bmaj7

            // MINOR 7
            { "0_min7", new int[] { -1, 3, 5, 3, 4, 3 } }, // Cm7
            { "2_min7", new int[] { -1, -1, 0, 2, 1, 1 } }, // Dm7
            { "4_min7", new int[] { 0, 2, 0, 0, 0, 0 } }, // Em7
            { "7_min7", new int[] { 3, 5, 3, 3, 3, 3 } }, // Gm7
            { "9_min7", new int[] { -1, 0, 2, 0, 1, 0 } }, // Am7
            { "11_min7", new int[] { -1, 2, 4, 2, 3, 2 } }  // Bm7
        };

        public static FretResult Solve(int[] notes, int rootPc, string suffix)
        {
            FretResult r = new FretResult();
            if (notes == null || notes.Length == 0) return r;

            if (notes.Length == 1)
            {
                AddExact(r, notes[0], notes[0] % 12, true);
                return r;
            }

            // 1. Check the Dictionary First
            if (suffix != null)
            {
                // Clean up suffix to match dictionary keys (e.g. "maj" stays "maj", "min7" stays "min7")
                string key = ((rootPc % 12) + 12) % 12 + "_" + suffix;
                int[] shape;
                if (Shapes.TryGetValue(key, out shape))
                {
                    r.Constellation = true;
                    for (int s = 0; s < 6; s++)
                    {
                        r.Frets[s] = shape[s];
                        if (shape[s] >= 0)
                        {
                            FretPos p = new FretPos();
                            p.Str = s; p.Fret = shape[s];
                            p.Root = (Tuning[s] + shape[s]) % 12 == rootPc;
                            r.Dots.Add(p);
                        }
                    }
                    return r;
                }
            }

            // 2. Fallback to Math Solver for complex chords (max 4 fret stretch)
            bool[] pcs = new bool[12];
            int pcCount = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                int pc = notes[i] % 12;
                if (!pcs[pc]) { pcs[pc] = true; pcCount++; }
            }

            int bestScore = int.MinValue;
            int[] best = null;
            int[] cur = new int[6];

            for (int start = 0; start <= MaxFret - 4; start++)
            {
                Dfs(0, cur, start, start + 4, pcs, rootPc, pcCount, ref bestScore, ref best);
            }

            if (best != null)
            {
                r.Constellation = true;
                for (int s = 0; s < 6; s++)
                {
                    r.Frets[s] = best[s];
                    if (best[s] >= 0)
                    {
                        FretPos p = new FretPos();
                        p.Str = s; p.Fret = best[s];
                        p.Root = rootPc >= 0 && (Tuning[s] + best[s]) % 12 == rootPc;
                        r.Dots.Add(p);
                    }
                }
            }
            else
            {
                for (int i = 0; i < notes.Length; i++) AddExact(r, notes[i], rootPc, false);
            }
            return r;
        }

        static void AddExact(FretResult r, int note, int rootPc, bool single)
        {
            for (int s = 0; s < 6; s++)
            {
                int f = note - Tuning[s];
                if (f < 0 || f > MaxFret) continue;
                FretPos p = new FretPos();
                p.Str = s; p.Fret = f;
                p.Root = single || (rootPc >= 0 && note % 12 == rootPc);
                r.Dots.Add(p);
            }
        }

        static void Dfs(int s, int[] cur, int lo, int hi, bool[] pcs, int rootPc, int pcCount, ref int bestScore, ref int[] best)
        {
            if (s == 6)
            {
                int sc = Score(cur, rootPc, pcCount);
                if (sc > bestScore) { bestScore = sc; best = (int[])cur.Clone(); }
                return;
            }
            cur[s] = -1;
            Dfs(s + 1, cur, lo, hi, pcs, rootPc, pcCount, ref bestScore, ref best);
            if (pcs[Tuning[s] % 12])
            {
                cur[s] = 0;
                Dfs(s + 1, cur, lo, hi, pcs, rootPc, pcCount, ref bestScore, ref best);
            }
            for (int f = lo; f <= hi; f++)
            {
                if (pcs[(Tuning[s] + f) % 12])
                {
                    cur[s] = f;
                    Dfs(s + 1, cur, lo, hi, pcs, rootPc, pcCount, ref bestScore, ref best);
                }
            }
            cur[s] = -1;
        }

        static int Score(int[] f, int rootPc, int pcCount)
        {
            bool[] seen = new bool[12];
            int cnt = 0, strings = 0, maxF = 0, minF = 99, rootFound = 0;
            int firstPlayed = -1, lastPlayed = -1;

            for (int s = 0; s < 6; s++)
            {
                if (f[s] < 0) continue;
                strings++;
                if (firstPlayed < 0) firstPlayed = s;
                lastPlayed = s;

                int pc = (Tuning[s] + f[s]) % 12;
                if (!seen[pc]) { seen[pc] = true; cnt++; }
                if (pc == rootPc) rootFound = 1;

                if (f[s] > 0)
                {
                    if (f[s] > maxF) maxF = f[s];
                    if (f[s] < minF) minF = f[s];
                }
            }

            if (strings < 3 || cnt < pcCount) return int.MinValue;
            if (rootPc >= 0 && rootFound == 0) return int.MinValue;

            int innerMutes = 0;
            for (int s = firstPlayed; s <= lastPlayed; s++) if (f[s] < 0) innerMutes++;
            if (innerMutes > 0) return int.MinValue;

            int span = maxF > 0 ? maxF - minF : 0;
            return strings * 20 - span * 10 - maxF;
        }
    }
}