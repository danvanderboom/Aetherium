// Aphelion audio generator — synthesizes the game's music stems and a few SFX as WAV files.
// Deterministic (fixed seed): the committed audio is exactly reproducible from this source,
// so the repo's music carries no third-party license at all.
//
// Usage: dotnet run [outputDir]   (default: ../../Assets/Audio relative to this project)

const int SR = 24000;           // lo-fi synth aesthetic; halves file size vs 48k
const double BPM = 90.0;
const double Beat = 60.0 / BPM; // 0.6667 s
const double Bar = 4 * Beat;

string outDir = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "Audio"));
string musicDir = Path.Combine(outDir, "Music");
string sfxDir = Path.Combine(outDir, "SFX");
Directory.CreateDirectory(musicDir);
Directory.CreateDirectory(sfxDir);

// ---- musical material: D minor, i–VI–III–VII (Dm, Bb, F, C), two bars per chord ----
double Hz(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
int[][] chords =
{
    new[] { 50, 53, 57 }, // D3 F3 A3   (Dm)
    new[] { 46, 50, 53 }, // Bb2 D3 F3  (Bb)
    new[] { 53, 57, 60 }, // F3 A3 C4   (F)
    new[] { 48, 52, 55 }, // C3 E3 G3   (C)
};

Write(Path.Combine(musicDir, "pad-explore.wav"), RenderPad(loopBars: 8), stereo: true);
Write(Path.Combine(musicDir, "arp-tension.wav"), RenderArp(loopBars: 8), stereo: true);
Write(Path.Combine(musicDir, "pulse-combat.wav"), RenderPulse(loopBars: 8), stereo: true);
Write(Path.Combine(musicDir, "aphelion-theme.wav"), RenderTheme(), stereo: true);
Write(Path.Combine(sfxDir, "heartbeat.wav"), RenderHeartbeat(), stereo: false);
Write(Path.Combine(sfxDir, "pickup-chime.wav"), RenderChime(), stereo: false);
Write(Path.Combine(sfxDir, "ui-blip.wav"), RenderBlip(), stereo: false);
Console.WriteLine($"done -> {outDir}");

// ================= renderers =================

double[,] RenderPad(int loopBars)
{
    // Detuned saw pair + sine sub per chord, slow attack, gentle LFO on the lowpass.
    // Rendered with a wrapped tail so the loop point is seamless.
    int loopLen = (int)(loopBars * Bar * SR);
    int tail = (int)(2.0 * SR);
    var buf = new double[2, loopLen + tail];
    for (int c = 0; c < 4; c++)
    {
        double t0 = c * 2 * Bar;
        foreach (int m in chords[c])
        {
            AddVoice(buf, t0, 2 * Bar + 1.5, Hz(m), amp: 0.11, attack: 0.9, release: 1.2, detune: 0.004, kind: Wave.Saw);
            AddVoice(buf, t0, 2 * Bar + 1.5, Hz(m) * 2, amp: 0.03, attack: 1.4, release: 1.2, detune: 0.006, kind: Wave.Saw);
        }
        AddVoice(buf, t0, 2 * Bar + 1.0, Hz(chords[c][0] - 12), amp: 0.16, attack: 0.6, release: 1.0, detune: 0, kind: Wave.Sine);
    }
    WrapTail(buf, loopLen);
    LowpassSweep(buf, loopLen, baseHz: 900, depthHz: 500, lfoHz: 1.0 / (loopBars * Bar)); // one sweep per loop -> seamless
    return Trim(buf, loopLen);
}

double[,] RenderArp(int loopBars)
{
    // 16th-note rising arpeggio over the chord tones, plucked, with a dotted-eighth echo.
    int loopLen = (int)(loopBars * Bar * SR);
    int tail = (int)(1.5 * SR);
    var buf = new double[2, loopLen + tail];
    var rng = new Random(4157);
    int steps = loopBars * 16;
    for (int s = 0; s < steps; s++)
    {
        double t0 = s * Beat / 4;
        int chord = (s / 32) % 4;                    // 2 bars per chord
        int[] tones = chords[chord];
        int m = tones[s % 3] + 12 * (1 + ((s / 3) % 2)); // climb through two octaves
        double vel = 0.05 + 0.03 * rng.NextDouble();
        AddVoice(buf, t0, 0.22, Hz(m), amp: vel, attack: 0.004, release: 0.18, detune: 0.002, kind: Wave.Tri);
    }
    WrapTail(buf, loopLen);
    Echo(buf, loopLen, delaySec: Beat * 0.75, feedback: 0.35, mix: 0.4, pingPong: true);
    return Trim(buf, loopLen);
}

double[,] RenderPulse(int loopBars)
{
    // Combat layer: pitch-drop kick on quarters, noise snare on 2 & 4, offbeat hats,
    // driving eighth-note bass following the progression.
    int loopLen = (int)(loopBars * Bar * SR);
    var buf = new double[2, loopLen + (int)(0.5 * SR)];
    var rng = new Random(90210);
    for (int bar = 0; bar < loopBars; bar++)
    {
        double bt = bar * Bar;
        int chord = (bar / 2) % 4;
        for (int q = 0; q < 4; q++) AddKick(buf, bt + q * Beat);
        AddSnare(buf, bt + 1 * Beat, rng); AddSnare(buf, bt + 3 * Beat, rng);
        for (int e = 0; e < 8; e++)
        {
            if (e % 2 == 1) AddHat(buf, bt + e * Beat / 2, rng);
            AddVoice(buf, bt + e * Beat / 2, 0.26, Hz(chords[chord][0] - 12), amp: 0.13, attack: 0.005, release: 0.1, detune: 0.001, kind: Wave.Saw);
        }
    }
    WrapTail(buf, loopLen);
    return Trim(buf, loopLen);
}

double[,] RenderTheme()
{
    // The extraction theme: pads underneath, one lonely lead line above (vibrato + echo).
    // 8-bar progression + 2-bar resolution; not a loop — it's the song of going home.
    int totalBars = 10;
    int len = (int)(totalBars * Bar * SR) + (int)(3.0 * SR);
    var buf = new double[2, len];
    for (int c = 0; c < 5; c++) // 5 chords: Dm Bb F C + final Dm
    {
        int[] tones = c < 4 ? chords[c] : chords[0];
        double t0 = c * 2 * Bar;
        foreach (int m in tones)
            AddVoice(buf, t0, 2 * Bar + 1.2, Hz(m), amp: 0.09, attack: 0.8, release: 1.5, detune: 0.004, kind: Wave.Saw);
        AddVoice(buf, t0, 2 * Bar + 1.0, Hz(tones[0] - 12), amp: 0.13, attack: 0.5, release: 1.4, detune: 0, kind: Wave.Sine);
    }
    // melody: (midi, startBeat, lengthBeats) — rising far, falling home
    (int m, double b, double d)[] line =
    {
        (62, 0, 2), (65, 2, 1), (69, 3, 1),      // D4  F4  A4
        (72, 4, 2), (69, 6, 2),                  // C5  A4
        (70, 8, 2), (69, 10, 1), (65, 11, 1),    // Bb4 A4  F4
        (67, 12, 3), (65, 15, 1),                // G4  F4
        (69, 16, 2), (72, 18, 1), (74, 19, 1),   // A4  C5  D5
        (72, 20, 2), (69, 22, 2),                // C5  A4
        (67, 24, 2), (64, 26, 1), (67, 27, 1),   // G4  E4  G4
        (69, 28, 4),                             // A4 (held — the ache)
        (74, 32, 2), (69, 34, 2),                // D5  A4
        (62, 36, 4),                             // D4 (home)
    };
    foreach (var (m, b, d) in line)
        AddLead(buf, b * Beat, d * Beat, Hz(m), amp: 0.16);
    Echo(buf, len - (int)(0.1 * SR), delaySec: Beat * 1.5, feedback: 0.3, mix: 0.35, pingPong: true);
    return buf;
}

double[,] RenderHeartbeat()
{
    var buf = new double[1, (int)(1.1 * SR)];
    AddThump(buf, 0.00, 52, 0.9); AddThump(buf, 0.18, 44, 0.7);
    return buf;
}

double[,] RenderChime()
{
    var buf = new double[1, (int)(0.5 * SR)];
    AddVoice(buf, 0, 0.45, Hz(86), amp: 0.25, attack: 0.002, release: 0.4, detune: 0.001, kind: Wave.Sine, mono: true);
    AddVoice(buf, 0.05, 0.4, Hz(93), amp: 0.15, attack: 0.002, release: 0.35, detune: 0.001, kind: Wave.Sine, mono: true);
    return buf;
}

double[,] RenderBlip()
{
    var buf = new double[1, (int)(0.14 * SR)];
    AddVoice(buf, 0, 0.12, Hz(81), amp: 0.22, attack: 0.001, release: 0.1, detune: 0, kind: Wave.Tri, mono: true);
    return buf;
}

// ================= synthesis =================

void AddVoice(double[,] buf, double t0, double dur, double freq, double amp, double attack, double release, double detune, Wave kind, bool mono = false)
{
    int start = (int)(t0 * SR), n = (int)(dur * SR), len = buf.GetLength(1);
    double phL = 0, phR = 0.25;
    double fL = freq * (1 - detune), fR = freq * (1 + detune);
    for (int i = 0; i < n && start + i < len; i++)
    {
        double t = i / (double)SR;
        double env = Env(t, dur, attack, release);
        double l = Osc(kind, ref phL, fL) * amp * env;
        double r = mono ? l : Osc(kind, ref phR, fR) * amp * env;
        buf[0, start + i] += l;
        if (buf.GetLength(0) > 1) buf[1, start + i] += r;
    }
}

void AddLead(double[,] buf, double t0, double dur, double freq, double amp)
{
    int start = (int)(t0 * SR), n = (int)((dur + 0.35) * SR), len = buf.GetLength(1);
    double ph = 0;
    for (int i = 0; i < n && start + i < len; i++)
    {
        double t = i / (double)SR;
        double vib = 1.0 + 0.006 * Math.Sin(2 * Math.PI * 5.2 * t) * Math.Min(1, t / 0.4); // vibrato fades in
        double env = Env(t, dur + 0.3, 0.06, 0.32);
        double s = (0.7 * Osc(Wave.Tri, ref ph, freq * vib) + 0.3 * Math.Sin(2 * Math.PI * 2 * freq * vib * t)) * amp * env;
        buf[0, start + i] += s * 0.95;
        if (buf.GetLength(0) > 1) buf[1, start + i] += s;
    }
}

void AddKick(double[,] buf, double t0)
{
    int start = (int)(t0 * SR), n = (int)(0.25 * SR), len = buf.GetLength(1);
    for (int i = 0; i < n && start + i < len; i++)
    {
        double t = i / (double)SR;
        double f = 40 + 60 * Math.Exp(-t * 22);             // pitch drop
        double s = Math.Sin(2 * Math.PI * f * t) * 0.5 * Math.Exp(-t * 14);
        buf[0, start + i] += s; if (buf.GetLength(0) > 1) buf[1, start + i] += s;
    }
}

void AddSnare(double[,] buf, double t0, Random rng)
{
    int start = (int)(t0 * SR), n = (int)(0.18 * SR), len = buf.GetLength(1);
    for (int i = 0; i < n && start + i < len; i++)
    {
        double t = i / (double)SR;
        double s = ((rng.NextDouble() * 2 - 1) * 0.28 + Math.Sin(2 * Math.PI * 185 * t) * 0.12) * Math.Exp(-t * 24);
        buf[0, start + i] += s; if (buf.GetLength(0) > 1) buf[1, start + i] += s;
    }
}

void AddHat(double[,] buf, double t0, Random rng)
{
    int start = (int)(t0 * SR), n = (int)(0.05 * SR), len = buf.GetLength(1);
    double last = 0;
    for (int i = 0; i < n && start + i < len; i++)
    {
        double w = rng.NextDouble() * 2 - 1;
        double hp = w - last; last = w;                      // crude highpass
        double s = hp * 0.12 * Math.Exp(-i / (double)SR * 90);
        buf[0, start + i] += s; if (buf.GetLength(0) > 1) buf[1, start + i] += s;
    }
}

void AddThump(double[,] buf, double t0, double f0, double amp)
{
    int start = (int)(t0 * SR), n = (int)(0.16 * SR), len = buf.GetLength(1);
    for (int i = 0; i < n && start + i < len; i++)
    {
        double t = i / (double)SR;
        double s = Math.Sin(2 * Math.PI * (f0 - 12 * t * 6) * t) * amp * 0.4 * Math.Exp(-t * 20);
        buf[0, start + i] += s;
    }
}

double Env(double t, double dur, double attack, double release)
{
    double a = Math.Min(1, t / Math.Max(1e-4, attack));
    double r = t > dur - release ? Math.Max(0, (dur - t) / Math.Max(1e-4, release)) : 1;
    return a * r;
}

double Osc(Wave kind, ref double phase, double freq)
{
    phase += freq / SR; if (phase >= 1) phase -= 1;
    return kind switch
    {
        Wave.Sine => Math.Sin(2 * Math.PI * phase),
        Wave.Tri => 4 * Math.Abs(phase - 0.5) - 1,
        _ => SoftSaw(phase), // 8-harmonic saw: cheap and band-limited enough at these registers
    };
}

double SoftSaw(double phase)
{
    double s = 0;
    for (int h = 1; h <= 8; h++) s += Math.Sin(2 * Math.PI * h * phase) / h;
    return s * (2.0 / Math.PI) * 0.5;
}

// fold anything rendered past the loop point back onto the start -> click-free loops
void WrapTail(double[,] buf, int loopLen)
{
    int ch = buf.GetLength(0), len = buf.GetLength(1);
    for (int c = 0; c < ch; c++)
        for (int i = loopLen; i < len; i++)
            buf[c, i - loopLen] += buf[c, i];
}

void LowpassSweep(double[,] buf, int len, double baseHz, double depthHz, double lfoHz)
{
    int ch = buf.GetLength(0);
    for (int c = 0; c < ch; c++)
    {
        double state = 0;
        for (int i = 0; i < len; i++)
        {
            double cutoff = baseHz + depthHz * Math.Sin(2 * Math.PI * lfoHz * (i / (double)SR));
            double a = 1 - Math.Exp(-2 * Math.PI * cutoff / SR);
            state += a * (buf[c, i] - state);
            buf[c, i] = state;
        }
    }
}

void Echo(double[,] buf, int len, double delaySec, double feedback, double mix, bool pingPong)
{
    int d = (int)(delaySec * SR), ch = buf.GetLength(0);
    for (int i = d; i < len; i++)
        for (int c = 0; c < ch; c++)
        {
            int src = pingPong && ch > 1 ? 1 - c : c;
            buf[c, i] += buf[src, i - d] * feedback * mix / Math.Max(mix, 0.0001) * mix;
        }
}

double[,] Trim(double[,] buf, int len)
{
    int ch = buf.GetLength(0);
    var o = new double[ch, len];
    for (int c = 0; c < ch; c++) for (int i = 0; i < len; i++) o[c, i] = buf[c, i];
    return o;
}

void Write(string path, double[,] data, bool stereo)
{
    int chIn = data.GetLength(0), n = data.GetLength(1), chOut = stereo ? 2 : 1;
    // normalize to -1.5 dBFS
    double peak = 1e-9;
    for (int c = 0; c < chIn; c++) for (int i = 0; i < n; i++) peak = Math.Max(peak, Math.Abs(data[c, i]));
    double g = 0.84 / peak;
    using var fs = new FileStream(path, FileMode.Create);
    using var w = new BinaryWriter(fs);
    int dataBytes = n * chOut * 2;
    w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVE"u8);
    w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)chOut);
    w.Write(SR); w.Write(SR * chOut * 2); w.Write((short)(chOut * 2)); w.Write((short)16);
    w.Write("data"u8); w.Write(dataBytes);
    for (int i = 0; i < n; i++)
        for (int c = 0; c < chOut; c++)
        {
            double v = data[Math.Min(c, chIn - 1), i] * g;
            v = Math.Tanh(v * 1.2) / 1.2; // gentle soft clip
            w.Write((short)Math.Clamp(v * 32767, short.MinValue, short.MaxValue));
        }
    Console.WriteLine($"{Path.GetFileName(path),-22} {n / (double)SR,6:0.0}s  {(36 + dataBytes) / 1024.0 / 1024.0,5:0.00} MB");
}

enum Wave { Sine, Saw, Tri }
