// GamepadMIDI v2 - Core: Windows API, data model, controller reading, MIDI output, engine (pad mode + HiChord mode).
// All files are built together by BUILD.bat with the C# compiler that ships with Windows (C# 5 syntax only).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace GamepadMidi
{
    // ------------------------------------------------------------------ Windows API (winmm only)
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct JOYINFOEX
        {
            public uint dwSize, dwFlags, dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos, dwButtons, dwButtonNumber, dwPOV, dwReserved1, dwReserved2;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct JOYCAPS
        {
            public ushort wMid;
            public ushort wPid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint wXmin, wXmax, wYmin, wYmax, wZmin, wZmax, wNumButtons, wPeriodMin, wPeriodMax;
            public uint wRmin, wRmax, wUmin, wUmax, wVmin, wVmax, wCaps, wMaxAxes, wNumAxes, wMaxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szRegKey;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szOEMVxD;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MIDIOUTCAPS
        {
            public ushort wMid;
            public ushort wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public ushort wTechnology, wVoices, wNotes, wChannelMask;
            public uint dwSupport;
        }

        [DllImport("winmm.dll")] public static extern int joyGetPosEx(int id, ref JOYINFOEX info);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] public static extern int joyGetDevCapsW(IntPtr id, ref JOYCAPS caps, int size);
        [DllImport("winmm.dll")] public static extern int midiOutGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] public static extern int midiOutGetDevCapsW(IntPtr id, ref MIDIOUTCAPS caps, int size);
        [DllImport("winmm.dll")] public static extern int midiOutOpen(out IntPtr h, int id, IntPtr cb, IntPtr inst, int flags);
        [DllImport("winmm.dll")] public static extern int midiOutClose(IntPtr h);
        [DllImport("winmm.dll")] public static extern int midiOutReset(IntPtr h);
        [DllImport("winmm.dll")] public static extern int midiOutShortMsg(IntPtr h, uint msg);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX, sThumbLY, sThumbRX, sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] public static extern int XInputGetState14(int idx, out XINPUT_STATE s);
        [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")] public static extern int XInputGetState13(int idx, out XINPUT_STATE s);
    }

    // ------------------------------------------------------------------ note names for pad mode
    static class Music
    {
        public static bool FL = true; // FL Studio calls MIDI note 60 "C5"
        static readonly string[] Pc = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        public static readonly string[] ChordNames = { "Major 7 (maj7)", "Minor 7 (m7)", "Dominant 7 (7)", "Major 9 (maj9)", "Dominant 9 (9)", "Minor 9 (m9)", "Major (triad)", "Minor (triad)" };
        public static readonly string[] ChordShort = { "maj7", "m7", "7", "maj9", "9", "m9", "", "m" };
        public static readonly string[] ChordCanon = { "maj7", "min7", "7", "maj9", "9", "min9", "maj", "min" };
        public static readonly int[][] ChordIv =
        {
            new int[] { 0, 4, 7, 11 },
            new int[] { 0, 3, 7, 10 },
            new int[] { 0, 4, 7, 10 },
            new int[] { 0, 4, 7, 11, 14 },
            new int[] { 0, 4, 7, 10, 14 },
            new int[] { 0, 3, 7, 10, 14 },
            new int[] { 0, 4, 7 },
            new int[] { 0, 3, 7 }
        };
        public static string PcName(int n) { return Pc[((n % 12) + 12) % 12]; }
        public static string Name(int n) { return Pc[((n % 12) + 12) % 12] + ((n / 12) - 1 + (FL ? 1 : 0)); }
        public static bool IsBlack(int n) { int p = n % 12; return p == 1 || p == 3 || p == 6 || p == 8 || p == 10; }
    }

    // ------------------------------------------------------------------ data model
    enum PadStyle { Classic = 0, PS4 = 1, PS5 = 2, Xbox = 3, Generic = 4 }
    enum SrcType { None, Button, Pov, Axis }

    class Source
    {
        public SrcType T;
        public int Idx;
        public int Sign;
        public Source() { T = SrcType.None; }
        public Source(SrcType t, int idx, int sign) { T = t; Idx = idx; Sign = sign; }

        public string Ser()
        {
            switch (T)
            {
                case SrcType.Button: return "B:" + Idx;
                case SrcType.Pov: return "P:" + Idx;
                case SrcType.Axis: return "A:" + Idx + ":" + Sign;
            }
            return "-";
        }

        public static Source Parse(string s)
        {
            try
            {
                string[] p = s.Split(':');
                if (p[0] == "B") return new Source(SrcType.Button, int.Parse(p[1]), 0);
                if (p[0] == "P") return new Source(SrcType.Pov, int.Parse(p[1]), 0);
                if (p[0] == "A") return new Source(SrcType.Axis, int.Parse(p[1]), int.Parse(p[2]));
            }
            catch { }
            return new Source();
        }
    }

    class Mapping
    {
        public int Kind;   // 0 nothing, 1 note, 2 chord, 3 octave down, 4 octave up
        public int Root = 60;
        public int Chord;
        public int Vel = 100;
        public int[] Held = new int[0];
    }

    class Pad
    {
        public string Id, Label, Name, Verb = "Press";
        public bool Notes;      // can play notes / chords in pad mode
        public bool KnobInput;  // is a direction used by a knob
        public bool Visible = true;
        public int Shape;       // 0 circle, 1 rounded rectangle, 2 rounded square
        public Rectangle R;
        public Color Col;
        public Source Src = new Source();
        public Mapping Map = new Mapping();
        public float Value;
        public bool Pressed;
        public Pad ShowAs;                  // which visible pad to highlight in the teach wizard
        public Pad DxP, DxN, DyP, DyN;      // stick caps show a live dot
        public string HiTag = "", HiSub = "";   // HiChord-mode labels
        public Color HiCol;                     // HiChord-mode colour (A == 0: use Col)
    }

    class Knob
    {
        public string Id, Label;
        public Pad Inc, Dec;
        public bool Spring, PitchBend, Reverse;
        public bool HiBlocked;    // in HiChord mode this knob's stick/d-pad is the chord-flavour joystick
        public int CC;
        public float Speed = 1f;
        public int RestMid;       // 1 = rests in the middle (CC mode)
        public float Pos;         // 0..1
        public int Sent = -1;
        public bool Primed;
        public Point Center;
        public int Radius;

        public float RestPos() { return PitchBend ? 0.5f : (RestMid == 1 ? 0.5f : 0f); }

        public void ResetPos() { Pos = Spring ? RestPos() : 0f; Sent = -1; Primed = false; }

        // returns true when the value to send changed
        public bool Step(float dt)
        {
            float inp = Inc.Value - Dec.Value;
            if (Reverse) inp = -inp;
            bool analog = Inc.Src.T == SrcType.Axis || Dec.Src.T == SrcType.Axis;
            float rest = RestPos();
            if (Spring)
            {
                if (analog) Pos = rest + (inp >= 0f ? inp * (1f - rest) : inp * rest);
                else if (Math.Abs(inp) > 0.01f) Pos += inp * Speed * dt * 0.5f;
                else Pos = rest;
            }
            else
            {
                Pos += inp * Speed * dt * 0.5f;
            }
            if (Pos < 0f) Pos = 0f;
            if (Pos > 1f) Pos = 1f;
            int v = PitchBend ? (int)(Pos * 16383f + 0.5f) : (int)(Pos * 127f + 0.5f);
            if (!Primed) { Primed = true; Sent = v; return false; }
            if (v != Sent) { Sent = v; return true; }
            return false;
        }
    }

    class RawState
    {
        public uint Buttons;
        public bool[] Dirs = new bool[4];
        public float[] Axes = new float[6];
        public float[] Trig = new float[2];     // analog L2/R2 (0..1) where the controller has them
        public void CopyFrom(RawState o)
        {
            Buttons = o.Buttons;
            Array.Copy(o.Dirs, Dirs, 4);
            Array.Copy(o.Axes, Axes, 6);
            Array.Copy(o.Trig, Trig, 2);
        }
    }

    class DevInfo
    {
        public int Id;
        public string Name;
        public ushort Mid, Pid;
        public bool[] Has = new bool[6];
        public uint[] Min = new uint[6];
        public uint[] Max = new uint[6];
        public bool XInput;
        public int XIndex;
        public PadStyle Style;
    }

    // ------------------------------------------------------------------ controller reading
    static class Joy
    {
        const float Dead = 0.15f;
        static int xstate;   // 0 unknown, 1 xinput1_4.dll, 2 xinput1_3.dll, -1 not available

        static int XGet(int idx, out Native.XINPUT_STATE s)
        {
            s = new Native.XINPUT_STATE();
            if (xstate == 0 || xstate == 1)
            {
                try { int r = Native.XInputGetState14(idx, out s); xstate = 1; return r; }
                catch (Exception) { xstate = 2; }
            }
            if (xstate == 2)
            {
                try { return Native.XInputGetState13(idx, out s); }
                catch (Exception) { xstate = -1; }
            }
            return 1167;   // ERROR_DEVICE_NOT_CONNECTED
        }

        public static List<DevInfo> Scan()
        {
            List<DevInfo> list = new List<DevInfo>();
            bool anyX = false;
            for (int u = 0; u < 4; u++)
            {
                Native.XINPUT_STATE xs;
                if (XGet(u, out xs) == 0)
                {
                    DevInfo x = new DevInfo();
                    x.XInput = true; x.XIndex = u; x.Id = 100 + u;
                    x.Mid = 0x045E; x.Pid = 0x028E;
                    x.Name = "Xbox controller " + (u + 1);
                    x.Style = PadStyle.Xbox;
                    list.Add(x);
                    anyX = true;
                }
            }
            for (int id = 0; id < 16; id++)
            {
                Native.JOYCAPS caps = new Native.JOYCAPS();
                if (Native.joyGetDevCapsW((IntPtr)id, ref caps, Marshal.SizeOf(typeof(Native.JOYCAPS))) != 0) continue;
                Native.JOYINFOEX ji = new Native.JOYINFOEX();
                ji.dwSize = (uint)Marshal.SizeOf(typeof(Native.JOYINFOEX));
                ji.dwFlags = 0xFF;
                if (Native.joyGetPosEx(id, ref ji) != 0) continue;
                if (anyX && caps.wMid == 0x045E) continue;     // already listed through XInput
                DevInfo d = new DevInfo();
                d.Id = id;
                d.Mid = caps.wMid;
                d.Pid = caps.wPid;
                d.Name = string.IsNullOrEmpty(caps.szPname) ? "Game controller" : caps.szPname;
                d.Has[0] = true; d.Has[1] = true;
                d.Has[2] = (caps.wCaps & 1) != 0;
                d.Has[3] = (caps.wCaps & 2) != 0;
                d.Has[4] = (caps.wCaps & 4) != 0;
                d.Has[5] = (caps.wCaps & 8) != 0;
                d.Min[0] = caps.wXmin; d.Max[0] = caps.wXmax;
                d.Min[1] = caps.wYmin; d.Max[1] = caps.wYmax;
                d.Min[2] = caps.wZmin; d.Max[2] = caps.wZmax;
                d.Min[3] = caps.wRmin; d.Max[3] = caps.wRmax;
                d.Min[4] = caps.wUmin; d.Max[4] = caps.wUmax;
                d.Min[5] = caps.wVmin; d.Max[5] = caps.wVmax;
                d.Style = Detect(d);
                list.Add(d);
            }
            return list;
        }

        public static PadStyle Detect(DevInfo d)
        {
            if (d.XInput) return PadStyle.Xbox;
            if (d.Mid == 0x054C)
            {
                if (d.Pid == 0x0CDA) return PadStyle.Classic;
                if (d.Pid == 0x05C4 || d.Pid == 0x09CC || d.Pid == 0x0BA0) return PadStyle.PS4;
                if (d.Pid == 0x0CE6 || d.Pid == 0x0DF2) return PadStyle.PS5;
            }
            if (d.Mid == 0x045E) return PadStyle.Xbox;
            string n = d.Name == null ? "" : d.Name;
            if (n.IndexOf("DualSense", StringComparison.OrdinalIgnoreCase) >= 0) return PadStyle.PS5;
            if (n.IndexOf("Wireless Controller", StringComparison.OrdinalIgnoreCase) >= 0) return PadStyle.PS4;
            if (n.IndexOf("DUALSHOCK", StringComparison.OrdinalIgnoreCase) >= 0) return PadStyle.PS4;
            if (n.IndexOf("Xbox", StringComparison.OrdinalIgnoreCase) >= 0) return PadStyle.Xbox;
            return PadStyle.Generic;
        }

        static float Norm(DevInfo d, int i, uint val)
        {
            if (!d.Has[i]) return 0f;
            float min = d.Min[i];
            float range = (float)d.Max[i] - min;
            if (range <= 0f) { min = 0f; range = 65535f; }
            float v = ((float)val - min) / range * 2f - 1f;
            if (v < -1f) v = -1f;
            if (v > 1f) v = 1f;
            return v;
        }

        static float Nz(short v)
        {
            float f = v / 32767f;
            return f < -1f ? -1f : (f > 1f ? 1f : f);
        }

        static bool ReadX(DevInfo d, RawState s)
        {
            Native.XINPUT_STATE xs;
            if (XGet(d.XIndex, out xs) != 0) return false;
            int w = xs.Gamepad.wButtons;
            uint b = 0;
            if ((w & 0x1000) != 0) b |= 1u << 0;   // A
            if ((w & 0x2000) != 0) b |= 1u << 1;   // B
            if ((w & 0x4000) != 0) b |= 1u << 2;   // X
            if ((w & 0x8000) != 0) b |= 1u << 3;   // Y
            if ((w & 0x0100) != 0) b |= 1u << 4;   // LB
            if ((w & 0x0200) != 0) b |= 1u << 5;   // RB
            if ((w & 0x0020) != 0) b |= 1u << 6;   // Back
            if ((w & 0x0010) != 0) b |= 1u << 7;   // Start
            if ((w & 0x0040) != 0) b |= 1u << 8;   // L3
            if ((w & 0x0080) != 0) b |= 1u << 9;   // R3
            s.Buttons = b;
            s.Dirs[0] = (w & 0x0001) != 0;
            s.Dirs[1] = (w & 0x0008) != 0;
            s.Dirs[2] = (w & 0x0002) != 0;
            s.Dirs[3] = (w & 0x0004) != 0;
            s.Axes[0] = Nz(xs.Gamepad.sThumbLX);
            s.Axes[1] = -Nz(xs.Gamepad.sThumbLY);
            s.Axes[2] = Nz(xs.Gamepad.sThumbRX);
            s.Axes[3] = -Nz(xs.Gamepad.sThumbRY);
            float lt = xs.Gamepad.bLeftTrigger / 255f;
            float rt = xs.Gamepad.bRightTrigger / 255f;
            s.Axes[4] = lt; s.Axes[5] = rt;
            s.Trig[0] = lt; s.Trig[1] = rt;
            return true;
        }

        public static bool Read(DevInfo d, RawState s)
        {
            if (d.XInput) return ReadX(d, s);
            Native.JOYINFOEX ji = new Native.JOYINFOEX();
            ji.dwSize = (uint)Marshal.SizeOf(typeof(Native.JOYINFOEX));
            ji.dwFlags = 0xFF;
            if (Native.joyGetPosEx(d.Id, ref ji) != 0) return false;
            s.Buttons = ji.dwButtons;
            uint pov = ji.dwPOV & 0xFFFF;
            s.Dirs[0] = false; s.Dirs[1] = false; s.Dirs[2] = false; s.Dirs[3] = false;
            if (pov <= 35999)
            {
                s.Dirs[0] = pov >= 31500 || pov <= 4500;
                s.Dirs[1] = pov >= 4500 && pov <= 13500;
                s.Dirs[2] = pov >= 13500 && pov <= 22500;
                s.Dirs[3] = pov >= 22500 && pov <= 31500;
            }
            s.Axes[0] = Norm(d, 0, ji.dwXpos);
            s.Axes[1] = Norm(d, 1, ji.dwYpos);
            s.Axes[2] = Norm(d, 2, ji.dwZpos);
            s.Axes[3] = Norm(d, 3, ji.dwRpos);
            s.Axes[4] = Norm(d, 4, ji.dwUpos);
            s.Axes[5] = Norm(d, 5, ji.dwVpos);
            s.Trig[0] = 0f; s.Trig[1] = 0f;
            if ((d.Style == PadStyle.PS4 || d.Style == PadStyle.PS5) && d.Has[4] && d.Has[5])
            {
                s.Trig[0] = (s.Axes[4] + 1f) * 0.5f;
                s.Trig[1] = (s.Axes[5] + 1f) * 0.5f;
            }
            return true;
        }

        // 0..1 : how far this physical input is pressed
        public static float Value(Source src, RawState s)
        {
            switch (src.T)
            {
                case SrcType.Button:
                    return (src.Idx >= 0 && src.Idx < 32 && ((s.Buttons >> src.Idx) & 1u) != 0) ? 1f : 0f;
                case SrcType.Pov:
                    return (src.Idx >= 0 && src.Idx < 4 && s.Dirs[src.Idx]) ? 1f : 0f;
                case SrcType.Axis:
                    {
                        if (src.Idx < 0 || src.Idx > 5) return 0f;
                        float a = s.Axes[src.Idx] * src.Sign;
                        if (a <= Dead) return 0f;
                        a = (a - Dead) / (1f - Dead);
                        return a > 1f ? 1f : a;
                    }
            }
            return 0f;
        }
    }

    // ------------------------------------------------------------------ MIDI output
    class MidiOut
    {
        IntPtr h = IntPtr.Zero;
        public string Name = "";
        public bool IsOpen { get { return h != IntPtr.Zero; } }

        public static List<string> List()
        {
            List<string> r = new List<string>();
            int n = Native.midiOutGetNumDevs();
            for (int i = 0; i < n; i++)
            {
                Native.MIDIOUTCAPS c = new Native.MIDIOUTCAPS();
                if (Native.midiOutGetDevCapsW((IntPtr)i, ref c, Marshal.SizeOf(typeof(Native.MIDIOUTCAPS))) == 0) r.Add(c.szPname);
                else r.Add("MIDI port " + i);
            }
            return r;
        }

        public bool Open(int index)
        {
            Close();
            IntPtr hh;
            if (Native.midiOutOpen(out hh, index, IntPtr.Zero, IntPtr.Zero, 0) != 0) return false;
            h = hh;
            return true;
        }

        public void Close()
        {
            if (h != IntPtr.Zero)
            {
                Native.midiOutReset(h);
                Native.midiOutClose(h);
                h = IntPtr.Zero;
            }
        }

        void Send(int status, int d1, int d2)
        {
            IntPtr hh = h;
            if (hh != IntPtr.Zero) Native.midiOutShortMsg(hh, (uint)(status | (d1 << 8) | (d2 << 16)));
        }

        public void NoteOn(int ch, int note, int vel) { Send(0x90 | ch, note, vel); }
        public void NoteOff(int ch, int note) { Send(0x80 | ch, note, 0); }
        public void CC(int ch, int cc, int val) { Send(0xB0 | ch, cc, val); }
        public void Bend(int ch, int v14) { Send(0xE0 | ch, v14 & 0x7F, (v14 >> 7) & 0x7F); }
    }

    // ------------------------------------------------------------------ HiChord mode: state
    class ChordInfo
    {
        public int Root;        // absolute MIDI note of the chord root
        public int[] Iv;        // intervals above the root
        public string Suf;      // "maj", "min7", ...
        public int Q;           // 0 major, 1 minor, 2 dim, 3 aug, 4 other (only for diatonic base chords)
        public string Name(bool flats) { return Theory.PcName(Root, flats) + Suf; }
    }

    // which pads play which HiChord role
    class HiBind
    {
        public Pad[] Chord = new Pad[7];
        public Pad Inv, Lock, Gray, ModeP, ScaleP, Panic, Touch;
        public Pad JL, JR, JU, JD;           // the 8-way "joystick": left stick (PS4) or D-pad (PS Classic)
        public Pad KeyL, KeyR, OctU, OctD;   // PS4: D-pad changes key / octave directly
    }

    class HiState
    {
        // ---- settings (saved in profiles)
        public bool On;
        public int Key;        // 0..11 (C..B)
        public int Oct;        // -2..2
        public int Scale;      // index into Theory.Scales
        public int JoyMode;    // 0 Default, 1 Extended, 2 Chromatic, 3 Borrow, 4 OG
        public int Bass;       // 0 off, 1 root, 2 slash (auto)
        public bool VLead;
        public int Vel = 100;

        // ---- runtime (written by the engine thread, read by the window)
        public int Dir;                  // 0 neutral, 1 right, 2 up-right, 3 up, 4 up-left, 5 left, 6 down-left, 7 down, 8 down-right
        public float Jx, Jy;
        public bool Gray;
        public bool[] Held = new bool[7];
        public bool[] Suppressed = new bool[7];
        public bool[] Dirty = new bool[7];
        public int[] Inv = new int[7];
        public int[] Slash = new int[7];
        public int[] Stamp = new int[7];
        public int[] Sig = new int[7];
        public ChordInfo[] Lock = new ChordInfo[7];
        public string[] Cur = new string[7];
        public int[][] Notes = new int[7][];
        public int Counter;
        public int Ref;
        public int[] Last, Prev;
        public int[] CVel = new int[7];     // velocity each held chord was struck with
        public int LastRoot;                // absolute root of the last voiced chord
        public string LastName = "", LastSuf = "";
        public int[] LastIv;
        public int LastDeg = -1;
        public int[] Trail = new int[8];
        public string[] TrailName = new string[8];
        public int TrailPos, TrailCount;
        public string Msg = "";
        public int MsgAt;

        public HiState() { for (int i = 0; i < 7; i++) Slash[i] = -1; }

        public void Say(string s) { Msg = s; MsgAt = Environment.TickCount; }

        public void Push(int deg, string name)
        {
            Trail[TrailPos] = deg;
            TrailName[TrailPos] = name;
            TrailPos = (TrailPos + 1) % Trail.Length;
            if (TrailCount < Trail.Length) TrailCount++;
        }

        public void SetNewestTrailName(string name)
        {
            if (TrailCount > 0) TrailName[(TrailPos + Trail.Length - 1) % Trail.Length] = name;
        }

        public int[] TrailDegs()
        {
            int n = TrailCount;
            int[] r = new int[n];
            int start = (TrailPos - n + Trail.Length) % Trail.Length;
            for (int i = 0; i < n; i++) r[i] = Trail[(start + i) % Trail.Length];
            return r;
        }

        public string[] TrailNames()
        {
            int n = TrailCount;
            string[] r = new string[n];
            int start = (TrailPos - n + Trail.Length) % Trail.Length;
            for (int i = 0; i < n; i++) r[i] = TrailName[(start + i) % Trail.Length];
            return r;
        }
    }

    // ------------------------------------------------------------------ the engine (runs on its own fast thread)
    class Engine
    {
        public int ArpCurve = 0; // 0=Linear, 1=Log, 2=Inverted Log, 3=Sine
        public float ArpCurveIntensity = 50f; // 0 to 100%
        public int ArpBpm = 120;
        public float ArpSpeedMultiplier = 1.0f; // <--- NEW: Controls the slider
        public float ArpSwing = 0f;
        public bool ArpActive = false;
        public int ArpStep = 0;
        public float ArpTimer = 0f;
        public int[] ArpNotes = new int[0];

        private float invHoldTime = 0f;
        private bool invHoldTriggered = false;

        // --- HUMANIZE MODE ---
        public bool HumanizeOn = false;
        public float HumanizeAmount = 0f; // 0.0 to 1.0 (0 = robotic, 1 = max human)
        public float RightStickHumanize = 0f; // Live modifier from right stick
        public MidiOut Out = new MidiOut();
        public object Lock = new object();
        public List<Pad> Pads = new List<Pad>();
        public List<Knob> Knobs = new List<Knob>();
        public HiBind Bind;
        public HiState Hi = new HiState();
        public DevInfo Dev;
        public volatile bool Connected;
        public volatile bool Paused;
        public int Channel;
        public int Octave;
        public bool[] Sounding = new bool[128];
        // last thing played in pad mode (for the info card)
        public int LastKind, LastRoot, LastChordIdx;
        public int[] LastNotesPad = new int[0];

        int[] refc = new int[128];
        bool[] prev = new bool[24];
        RawState raw = new RawState();
        RawState snap = new RawState();
        Thread th;
        volatile bool run;

        public void Start()
        {
            run = true;
            th = new Thread(Loop);
            th.IsBackground = true;
            th.Priority = ThreadPriority.AboveNormal;
            th.Start();
        }

        public void Stop()
        {
            run = false;
            if (th != null) th.Join(500);
        }

        public void SetDevice(DevInfo d) { lock (Lock) { ReleaseAllLocked(); Dev = d; } }
        public void SetLayout(List<Pad> p, List<Knob> k, HiBind b) { lock (Lock) { ReleaseAllLocked(); Pads = p; Knobs = k; Bind = b; } }
        public void SetPaused(bool p) { lock (Lock) { Paused = p; if (p) ReleaseAllLocked(); } }
        public void SetHi(bool on) { lock (Lock)
                                            if (ArpActive)
                                            {
                                                for (int j = 0; j < ArpNotes.Length; j++) NoteOffRef(ArpNotes[j]);
                                                ArpNotes = new int[0];
                                                ArpActive = false;
                                            }
                                        { ReleaseAllLocked(); Hi.On = on; 
                                        }
                                    }
        public void Panic() { lock (Lock) { ReleaseAllLocked(); PanicLocked(); } }

        public RawState Snapshot()
        {
            RawState r = new RawState();
            lock (Lock) { r.CopyFrom(snap); }
            return r;
        }

        void Loop()
        {
            Native.timeBeginPeriod(1);
            Stopwatch sw = Stopwatch.StartNew();
            long last = sw.ElapsedTicks;
            while (run)
            {
                try
                {

                    long now = sw.ElapsedTicks;
                    float dt = (float)(now - last) / Stopwatch.Frequency;
                    last = now;
                    if (dt > 0.1f) dt = 0.1f;
                    int sleep = 1;
                    lock (Lock)
                    {
                        DevInfo d = Dev;
                        if (d != null && Joy.Read(d, raw))
                        {
                            Connected = true;
                            snap.CopyFrom(raw);
                            if (!Paused) Process(dt);
                        }
                        else
                        {
                            Connected = false;
                            ReleaseAllLocked();
                            sleep = 20;
                        }
                    }
                    Thread.Sleep(sleep);

                }
                catch (Exception ex)
                {
                    // Log the crash so we can see it next time
                    System.IO.File.WriteAllText(@"C:\NexaKord_engine_crash.txt", ex.ToString());

                    Thread.Sleep(1000);
                }
                
            }
            lock (Lock) { ReleaseAllLocked(); }
            Native.timeEndPeriod(1);
        }

        void Process(float dt)
        {
            // --- ARPEGGIATOR CLOCK ---
            if (ArpActive)
            {
                // Get the full chord
                int newestChord = Newest();
                if (newestChord >= 0)
                {
                    ChordInfo bc = Theory.Base(Hi, newestChord);
                    ChordInfo c = Hi.Lock[newestChord] != null ? Hi.Lock[newestChord] : Theory.Modify(bc, Hi.JoyMode, Hi.Dir);
                    ArpNotes = Theory.Voice(c, Hi.Inv[newestChord], Hi.Bass);
                }
                else if (Hi.Last != null && Hi.Last.Length > 0) ArpNotes = Hi.Last;

                if (ArpNotes != null && ArpNotes.Length > 0)
                {
                    // Calculate target time in milliseconds based on BPM and Slider Multiplier
                    float baseMs = 60000f / (ArpBpm * ArpSpeedMultiplier);

                    // Apply Speed Curve
                    float stepRatio = (float)ArpStep / ArpNotes.Length;
                    float curveMult = 1f;
                    if (ArpCurve == 1) curveMult = (float)Math.Pow(2, stepRatio * (ArpCurveIntensity / 100f) * 2);
                    else if (ArpCurve == 2) curveMult = (float)Math.Pow(2, (1 - stepRatio) * (ArpCurveIntensity / 100f) * 2);
                    else if (ArpCurve == 3) curveMult = 1f + (float)Math.Sin(stepRatio * Math.PI * 0.5) * (ArpCurveIntensity / 100f);

                    float targetMs = baseMs * curveMult;

                    // Apply Swing (swing only applies to off-beats)
                    if (ArpStep % 2 != 0) targetMs += (targetMs * (ArpSwing / 100f));

                    ArpTimer += dt * 1000f;

                    // While loop prevents timing lag at high speeds
                    while (ArpTimer >= targetMs)
                    {
                        ArpTimer -= targetMs;

                        int prevStep = (ArpStep - 1 + ArpNotes.Length) % ArpNotes.Length;
                        if (prevStep >= 0 && prevStep < ArpNotes.Length) NoteOffRef(ArpNotes[prevStep]);

                        if (ArpStep >= 0 && ArpStep < ArpNotes.Length) NoteOnRef(ArpNotes[ArpStep], Hi.Vel);

                        ArpStep = (ArpStep + 1) % ArpNotes.Length;
                    }
                }
            }

            bool hi = Hi.On;
            for (int i = 0; i < Pads.Count; i++)
            {
                Pad p = Pads[i];
                float v = Joy.Value(p.Src, raw);
                p.Value = v;
                if (!p.Pressed && v >= 0.5f)
                {
                    p.Pressed = true;
                    if (p.Notes && !hi) On(p);
                }
                else if (p.Pressed && v < 0.35f)
                {
                    p.Pressed = false;
                    if (p.Notes && !hi) Off(p);
                }
            }
            // touchpad click: HiChord on/off, or chord lock while a chord button is held
            if (Bind != null && Bind.Touch != null)
            {
                if (Rise(20, Bind.Touch.Pressed))
                {
                    if (!hi) { Hi.On = true; hi = true; Hi.Say("HiChord ON"); }
                    else if (Newest() >= 0) ToggleLock(Newest());
                    else { HiSilenceAllLocked(); Hi.On = false; hi = false; Hi.Say("HiChord OFF"); }
                }
            }
            if (hi) HiProcess(dt);
            for (int i = 0; i < Knobs.Count; i++)
            {
                Knob k = Knobs[i];

                // Block right stick knobs in HiChord mode so they don't fight Humanize
                if (hi && (k.Id == "K3" || k.Id == "K4")) continue;
                if (hi && k.HiBlocked) continue;

                if (k.Inc != null && k.Dec != null && k.Step(dt))
                {
                    if (k.PitchBend) Out.Bend(Channel, k.Sent);
                    else Out.CC(Channel, k.CC, k.Sent);
                }
            }
        }

        // ---------------- pad mode
        void On(Pad p)
        {
            Mapping m = p.Map;
            if (m.Kind == 3) { if (Octave > -4) Octave--; return; }
            if (m.Kind == 4) { if (Octave < 4) Octave++; return; }
            if (m.Kind != 1 && m.Kind != 2) return;
            int ci = (m.Chord < 0 || m.Chord >= Music.ChordIv.Length) ? 0 : m.Chord;
            int[] iv = m.Kind == 1 ? new int[] { 0 } : Music.ChordIv[ci];
            int baseNote = m.Root + 12 * Octave;
            int[] tmp = new int[iv.Length];
            int cnt = 0;
            for (int j = 0; j < iv.Length; j++)
            {
                int n = baseNote + iv[j];
                if (n >= 0 && n <= 127) tmp[cnt++] = n;
            }
            int[] held = new int[cnt];
            Array.Copy(tmp, held, cnt);
            m.Held = held;
            LastKind = m.Kind; LastRoot = m.Root; LastChordIdx = ci; LastNotesPad = held;
            int vel = m.Vel;
            for (int j = 0; j < held.Length; j++) NoteOnRef(held[j], vel);
        }

        void Off(Pad p)
        {
            int[] held = p.Map.Held;
            p.Map.Held = new int[0];
            for (int j = 0; j < held.Length; j++) NoteOffRef(held[j]);
        }

        void NoteOnRef(int n, int vel)
        {
            if (refc[n]++ == 0) Out.NoteOn(Channel, n, vel);
            Sounding[n] = true;
        }

        void NoteOffRef(int n)
        {
            if (refc[n] > 0 && --refc[n] == 0)
            {
                Out.NoteOff(Channel, n);
                Sounding[n] = false;
            }
        }

        void ReleaseAllLocked()
        {
            for (int i = 0; i < Pads.Count; i++)
            {
                Pad p = Pads[i];
                if (p.Map.Held.Length > 0) Off(p);
                p.Pressed = false;
            }
            HiResetLocked();
        }

        void PanicLocked()
        {
            for (int n = 0; n < 128; n++)
            {
                if (refc[n] > 0) { refc[n] = 0; Sounding[n] = false; Out.NoteOff(Channel, n); }
            }
            Out.CC(Channel, 123, 0);
        }

        // ---------------- HiChord mode
        bool Rise(int slot, bool now)
        {
            bool r = now && !prev[slot];
            prev[slot] = now;
            return r;
        }

        bool RiseP(int slot, Pad p) { return Rise(slot, p != null && p.Pressed); }

        int Newest()
        {
            HiState h = Hi;
            int best = -1;
            for (int i = 0; i < 7; i++)
                if (h.Held[i] && !h.Suppressed[i] && (best < 0 || h.Stamp[i] > h.Stamp[best])) best = i;
            return best;
        }

        void HiResetLocked()
        {
            HiState h = Hi;
            for (int i = 0; i < 7; i++)
            {
                if (h.Notes[i] != null) HiSilence(i);
                h.Held[i] = false;
                h.Suppressed[i] = false;
                h.Slash[i] = -1;
                h.Cur[i] = null;
            }
            for (int i = 0; i < prev.Length; i++) prev[i] = false;
        }

        /* analog trigger (L2/R2) pull = MIDI velocity; no pull = the default velocity
        int SampleVel(int def)
        {
            float t = Math.Max(raw.Trig[0], raw.Trig[1]);
            if (t < 0.05f) return def;
            int v = (int)(t * 126f) + 1;
            return v < 1 ? 1 : (v > 127 ? 127 : v);
        }*/

        void HiSilenceAllLocked()
        {
            HiState h = Hi;
            for (int i = 0; i < 7; i++)
            {
                if (h.Notes[i] != null) HiSilence(i);
                h.Held[i] = false;
                h.Suppressed[i] = false;
                h.Slash[i] = -1;
                h.Cur[i] = null;
            }
        }

        void HiSilence(int i)
        {
            int[] n = Hi.Notes[i];
            Hi.Notes[i] = null;
            if (n != null) for (int j = 0; j < n.Length; j++) NoteOffRef(n[j]);
        }

        void CycleScale()
        {
            HiState h = Hi;
            h.Scale = (h.Scale + 1) % Theory.Scales.Length;
            for (int i = 0; i < 7; i++) { h.Lock[i] = null; h.Dirty[i] = true; }
            h.Say("Scale: " + Theory.ScaleNames[h.Scale] + "  (locks cleared)");
        }

        void HiProcess(float dt)
        {
            HiBind b = Bind;
            HiState h = Hi;
            if (b == null || b.JL == null || b.JR == null || b.JU == null || b.JD == null) return;

            bool gray = b.Gray != null && b.Gray.Pressed;
            h.Gray = gray;
            int newest = Newest();

            // ---- the 8-way joystick (Refined Sticky Snap)
            float x = b.JR.Value - b.JL.Value;
            float y = b.JU.Value - b.JD.Value;
            h.Jx = x; h.Jy = y;
            float mag = (float)Math.Sqrt(x * x + y * y);

            int dir = h.Dir;
            if (mag > 0.55f) // Stick is pushed out
            {
                double ang = Math.Atan2(y, x) * 180.0 / Math.PI;
                if (ang < 0) ang += 360.0;
                int rawDir = 1 + ((int)((ang + 22.5) / 45.0) % 8);

                // Hysteresis: If we are already in a direction, we need to move 15 degrees past the midpoint to switch.
                if (dir == 0 || dir == rawDir)
                {
                    dir = rawDir;
                }
                else
                {
                    // Calculate angular distance between current held direction and new raw direction
                    double currentAngle = (dir - 1) * 45.0;
                    double diff = Math.Abs(ang - currentAngle);
                    if (diff > 180) diff = 360 - diff;

                    // Only switch if the user has pushed significantly into the new zone
                    if (diff > 35.0) dir = rawDir;
                }
            }
            else if (mag < 0.35f) // Snap back to center (deadzone)
            {
                dir = 0;
            }

            // ---- Right Stick Humanize Control
            // Read raw Right Stick Y (Axis 3 is typical for right stick vertical)
            float rightY = raw.Axes[3];

            if (Math.Abs(rightY) > 0.15f) // Deadzone
            {
                // Push Up (negative on most sticks) = More humanize
                // Push Down (positive) = Less humanize
                RightStickHumanize = -rightY * 0.5f; // Scale it so it can add up to 50% extra
            }
            else
            {
                // Snap back to 0 when released
                RightStickHumanize = 0f;
            }

            if (gray) dir = 0; // Key menu open
            h.Dir = dir;

            // ---- edges
            bool rKL = RiseP(12, b.KeyL), rKR = RiseP(13, b.KeyR), rOU = RiseP(14, b.OctU), rOD = RiseP(15, b.OctD);
            bool rJL = Rise(16, b.JL.Pressed), rJR = Rise(17, b.JR.Pressed), rJU = Rise(18, b.JU.Pressed), rJD = Rise(19, b.JD.Pressed);
            bool rInv = RiseP(7, b.Inv), rLock = RiseP(8, b.Lock), rMode = RiseP(9, b.ModeP), rScale = RiseP(10, b.ScaleP), rPanic = RiseP(11, b.Panic);

            // ---- INV / Arpeggiator Logic
            if (b.Inv != null)
            {
                if (b.Inv.Pressed)
                {
                    invHoldTime += dt;
                    if (invHoldTime > 0.2f && !invHoldTriggered) // Reduced to 200ms for snappier feel
                    {
                        invHoldTriggered = true;
                        ArpActive = true;
                        ArpStep = 0;
                        ArpTimer = 0f;
                        h.Say("ARPEGGIATOR ON");
                    }
                }
                else
                {
                    // It's a TAP (released before 200ms)
                    if (invHoldTime > 0f && invHoldTime <= 0.2f && !invHoldTriggered)
                    {
                        if (newest >= 0)
                        {
                            if (h.VLead) h.Say("VLEAD AUTO");
                            else { h.Inv[newest] = (h.Inv[newest] + 1) % 3; h.Dirty[newest] = true; h.Say("Inversion " + h.Inv[newest]); }
                        }
                    }

                    // It's a RELEASE from a HOLD
                    if (invHoldTriggered)
                    {
                        invHoldTriggered = false;
                        ArpActive = false;
                        h.Say("ARPEGGIATOR OFF");
                        for (int i = 0; i < ArpNotes.Length; i++) NoteOffRef(ArpNotes[i]);
                        ArpNotes = new int[0];
                    }
                    invHoldTime = 0f;
                }
            }

            // ---- chord buttons
            for (int i = 0; i < 7; i++)
            {
                bool now = b.Chord[i] != null && b.Chord[i].Pressed;
                bool was = h.Held[i];

                if (now && !was) HiPress(i);
                // Only release if NOT locked
                else if (!now && was && h.Lock[i] == null) HiRelease(i);

                // Force Held to stay true if it's locked, so flavors can be changed
                if (h.Lock[i] != null) h.Held[i] = true;
            }
            if (gray && newest < 0)
            {
                // KEY/OCT held with no chord down: Start = stick mode, L2 = scale (PS Classic shortcuts)
                if (rLock) { h.JoyMode = (h.JoyMode + 1) % 5; h.Say("Stick: " + Theory.JoyNames[h.JoyMode]); }
                if (rInv) CycleScale();
                rLock = false; rInv = false;
            }
            if (rInv && newest >= 0)
            {
                if (h.VLead) h.Say("VLEAD AUTO");
                else { h.Inv[newest] = (h.Inv[newest] + 1) % 3; h.Dirty[newest] = true; h.Say("Inversion " + h.Inv[newest]); }
            }
            // Lock / Unlock Logic
            if (rLock)
            {
                if (newest >= 0)
                {
                    ToggleLock(newest);
                }
                else
                {
                    // If no new chord is pressed, clear ALL locks
                    for (int i = 0; i < 7; i++)
                    {
                        if (h.Lock[i] != null)
                        {
                            h.Lock[i] = null;
                            h.Dirty[i] = true;
                            HiRelease(i);
                        }
                    }
                    h.Say("All chords unlocked");
                }
            }

            // ---- re-voice any held chord whose ingredients changed (this is the live "flavor" bending)
            int sig = h.Dir | (h.Key << 4) | ((h.Oct + 2) << 8) | (h.Scale << 12) | (h.JoyMode << 16) | (h.Bass << 20);
            for (int i = 0; i < 7; i++)
            {
                if (!h.Held[i] || h.Suppressed[i]) continue;
                int s2 = sig ^ (h.Inv[i] << 23) ^ (h.Lock[i] != null ? (1 << 26) : 0) ^ ((h.Slash[i] + 1) << 27);
                if (s2 != h.Sig[i] || h.Dirty[i])
                {
                    h.Sig[i] = s2;
                    h.Dirty[i] = false;
                    HiVoice(i);
                }
            }
        }

        void HiPress(int i)
        {
            HiState h = Hi;
            // --- KILL HANGING NOTES ON CHORD CHANGE ---
            if (ArpActive)
            {
                for (int j = 0; j < ArpNotes.Length; j++) NoteOffRef(ArpNotes[j]);
                ArpNotes = new int[0];
                ArpStep = 0;
                ArpTimer = 0f;
            }
            h.Held[i] = true;
            h.Stamp[i] = ++h.Counter;
            h.Inv[i] = 0;
            h.Suppressed[i] = false;
            h.Slash[i] = -1;
            h.Dirty[i] = true;
            // slash chord: the button that was already held supplies the bass note
            int first = -1;
            for (int j = 0; j < 7; j++)
            {
                // ONLY use it as a slash chord if it's held AND NOT LOCKED
                if (j != i && h.Held[j] && !h.Suppressed[j] && h.Lock[j] == null && (first < 0 || h.Stamp[j] < h.Stamp[first]))
                    first = j;
            }
            if (first >= 0)
            {
                h.Slash[i] = first;
                h.Suppressed[first] = true;
                HiSilence(first);
            }

            h.Prev = h.Last;
            if (h.VLead) h.Inv[i] = BestInversion(i);
            h.Ref = i;
            h.CVel[i] = h.Vel;
            ChordInfo bc = Theory.Base(h, i);
            ChordInfo c = h.Lock[i] != null ? h.Lock[i] : Theory.Modify(bc, h.JoyMode, h.Dir);
            h.Push(i, c.Name(Theory.UseFlats(h.Key)));
        }
    

        void HiRelease(int i)
        {
            HiState h = Hi;
            h.Held[i] = false;
            h.Suppressed[i] = false;
            h.Cur[i] = null;
            HiSilence(i);
        }

        int BestInversion(int i)
        {
            HiState h = Hi;
            int[] pv = h.Prev;
            if (pv == null || pv.Length == 0) return 0;
            ChordInfo c = h.Lock[i] != null ? h.Lock[i] : Theory.Modify(Theory.Base(h, i), h.JoyMode, h.Dir);
            int best = 0, bestCost = int.MaxValue;
            for (int inv = 0; inv < 3; inv++)
            {
                int[] nn = Theory.Voice(c, inv, 0);
                int cost = 0;
                for (int a = 0; a < nn.Length; a++)
                {
                    int m = 999;
                    for (int q = 0; q < pv.Length; q++) m = Math.Min(m, Math.Abs(nn[a] - pv[q]));
                    cost += m;
                }
                if (cost < bestCost) { bestCost = cost; best = inv; }
            }
            return best;
        }

        void ToggleLock(int i)
        {
            HiState h = Hi;
            bool fl = Theory.UseFlats(h.Key);
            if (h.Lock[i] != null)
            {
                h.Lock[i] = null;
                h.Say("Button " + (i + 1) + " unlocked");

                // If the physical button isn't held, release it now
                if (Bind != null && Bind.Chord[i] != null && !Bind.Chord[i].Pressed)
                {
                    HiRelease(i);
                }
            }
            else
            {
                ChordInfo c = Theory.Modify(Theory.Base(h, i), h.JoyMode, h.Dir);
                h.Lock[i] = c;
                h.Say("Button " + (i + 1) + " locked to " + c.Name(fl));
                h.Held[i] = true; // Force it to stay held
            }
            h.Dirty[i] = true;
        }

        void HiVoice(int i)
        {
            HiState h = Hi;
            bool fl = Theory.UseFlats(h.Key);
            ChordInfo bc = Theory.Base(h, i);
            ChordInfo c = h.Lock[i] != null ? h.Lock[i] : Theory.Modify(bc, h.JoyMode, h.Dir);
            int[] notes = Theory.Voice(c, h.Inv[i], h.Slash[i] >= 0 ? 0 : h.Bass);
            string name = c.Name(fl);

            if (h.Slash[i] >= 0)
            {
                ChordInfo bb = Theory.Base(h, h.Slash[i]);
                int low = notes[0];
                int pc = ((bb.Root % 12) + 12) % 12;
                int d = (((low - pc) % 12) + 12) % 12;
                if (d == 0) d = 12;
                int bass = low - d;
                if (low - bass < 7) bass -= 12;
                notes = Theory.AddNote(notes, bass);
                name = name + "/" + Theory.PcName(pc, fl);
            }

            int[] old = h.Notes[i];
            if (old != null)
            {
                for (int j = 0; j < old.Length; j++)
                {
                    if (!Theory.Has(notes, old[j]))
                    {
                    NoteOffRef(old[j]); // <--- Remove the lock check here
                }
                }
            }

            // Kill old notes if not arpeggiating
            if (!ArpActive)
            {
               
                if (old != null) for (int j = 0; j < old.Length; j++) if (!Theory.Has(notes, old[j])) NoteOffRef(old[j]);
            }

            if (ArpActive && i == h.Ref)
            {
                ArpNotes = notes;
            }
            else if (!ArpActive)
            {
                // --- HUMANIZE LOGIC ---
                float totalHuman = Math.Max(0f, Math.Min(1f, HumanizeAmount + RightStickHumanize));
                if (HumanizeOn && totalHuman > 0.01f && notes.Length > 1)
                {
                    float maxSpreadMs = totalHuman * 80f;
                    float stepMs = maxSpreadMs / (notes.Length - 1);
                    for (int j = 0; j < notes.Length; j++)
                    {
                        int note = notes[j];
                        if (old == null || !Theory.Has(old, note))
                        {
                            int delay = (int)(j * stepMs);
                            int vel = h.Vel;
                            if (HumanizeOn) vel += new Random().Next(-5, 6);
                            if (vel < 1) vel = 1; if (vel > 127) vel = 127;

                            if (delay == 0) NoteOnRef(note, vel);
                            else
                            {
                                int capturedNote = note; int capturedVel = vel; int chordIndex = i;
                                Task.Delay(delay).ContinueWith(t => {
                                    lock (Lock) { if (Hi.Held[chordIndex] && Hi.Cur[chordIndex] == name) NoteOnRef(capturedNote, capturedVel); }
                                });
                            }
                        }
                    }
                }
                else
                {
                    // Standard instant play
                    for (int j = 0; j < notes.Length; j++)
                    {
                        if (old == null || !Theory.Has(old, notes[j]))
                            NoteOnRef(notes[j], h.Vel);
                    }
                }
            }

            h.Notes[i] = notes;
            h.Cur[i] = name;
            if (i == h.Ref)
            {
                h.Last = notes; h.LastName = name; h.LastSuf = c.Suf; h.LastIv = c.Iv; h.LastDeg = i; h.LastRoot = c.Root;
                h.SetNewestTrailName(name);
            }
        }
    }
}
