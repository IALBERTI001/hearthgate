"""Hearthgate's calm 8-bit playlist and the ambience between songs, made in code (Nov 6 2026).

Rules (game-audio-assets, long-session music, owner-approved Nov 6 2026):
- calm songs at a steady 70 to 90 BPM (slow, constant tempo lowers heart rate and stress; tempo matters more than style),
  gentle soft-square and triangle voices in the low and middle range, major keys, phrases with room to breathe;
- songs rotate, never the same one twice in a row, with 30 to 60 s of quiet ambience between them;
- boss waves switch to the energetic battle theme, so the player hears that a test is coming.
Usage: python3 tools/make_music.py   ->  assets/music/{hearthfire,lantern-walk,starlit-wall,rebuilding}.mp3, assets/ambience/town.mp3
Masters (WAV) go to art/masters/audio/.
"""
import os, subprocess, wave
import numpy as np

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NOTE = {n: i for i, n in enumerate(['C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B'])}
MAJOR = [0, 2, 4, 5, 7, 9, 11]


def hz(midi): return 440.0 * 2 ** ((midi - 69) / 12)


def osc(kind, f, n, duty=0.5, vib=0.0, t0=0.0):
    t = (np.arange(n) / SR) + t0
    ph = f * t + (vib * np.sin(2 * np.pi * 5.2 * t) / (2 * np.pi * 5.2) * f if vib else 0)
    ph = ph % 1.0
    if kind == 'tri': return 4 * np.abs(ph - 0.5) - 1
    if kind == 'sq':  return np.where(ph < duty, 1.0, -1.0)
    return np.sin(2 * np.pi * ph)


def adsr(n, a=0.01, d=0.08, s=0.6, r=0.08):
    e = np.ones(n) * s; A, D, R = int(a * SR), int(d * SR), int(r * SR)
    A = min(A, n); e[:A] = np.linspace(0, 1, A, endpoint=False)
    D2 = min(D, n - A); e[A:A + D2] = np.linspace(1, s, D2, endpoint=False)
    R = min(R, n); e[n - R:] *= np.linspace(1, 0, R)
    return e


def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR); y = np.empty_like(x); acc = 0.0
    for i in range(len(x)): acc = (1 - a) * x[i] + a * acc; y[i] = acc
    return y


def lp_fast(x, cutoff, passes=2):
    # one-pole low-pass in blocks via cumulative filtering (scipy-free, vectorised enough for minutes of audio)
    a = np.exp(-2 * np.pi * cutoff / SR)
    try:
        from scipy.signal import lfilter
        for _ in range(passes): x = lfilter([1 - a], [1, -a], x)
        return x
    except ImportError:
        for _ in range(passes): x = lowpass(x, cutoff)
        return x


def echo(x, ms, fb, mix, side=0):
    d = int(SR * ms / 1000); y = x.copy()
    for k in range(1, 5):
        g = mix * fb ** (k - 1)
        if d * k >= len(x): break
        y[d * k:] += x[:-d * k] * g
    return y


def song(key, bpm, prog, form, seed, lead_duty, lead_oct, mood):
    """prog: list of scale-degree chords (0-based), one per bar; form: list of section letters; 4/4."""
    rng = np.random.default_rng(seed)
    root = NOTE[key] + 48                       # C3 region
    beat = 60.0 / bpm; bar = 4 * beat
    def deg(d, octv=0): return root + 12 * (octv + d // 7) + MAJOR[d % 7]
    # a motif per section: rhythms in eighths (1 = note start, 0 = hold/rest) and scale steps around chord tones
    RHY = [[1,0,1,0,1,0,0,0], [1,0,0,1,0,0,1,0], [1,0,1,1,1,0,0,0], [1,0,0,0,1,0,1,0], [0,0,1,0,1,0,1,0], [1,1,1,0,0,0,0,0]]
    motifs = {}
    for sec in set(form):
        r = [RHY[rng.integers(len(RHY))] for _ in range(4)]
        r[3] = [1, 0, 0, 0, 0, 0, 0, 0]          # each phrase ends on a long note, then breathes
        steps = [[int(v) for v in rng.choice([0, 2, 4, 1, -1, 2, 4, 5], 8)] for _ in range(4)]
        motifs[sec] = (r, steps)
    bars = []
    for sec in form:
        for b in range(len(prog)): bars.append((sec, b))
    total = int(len(bars) * bar * SR) + SR * 3
    L = np.zeros(total); R = np.zeros(total)
    def add(sig, start, pan=0.0):
        i = int(start * SR); j = min(total, i + len(sig)); sig = sig[:j - i]
        L[i:j] += sig * (1 - max(0, pan)); R[i:j] += sig * (1 + min(0, pan))
    for bi, (sec, b) in enumerate(bars):
        t0 = bi * bar; ch = prog[b % len(prog)]
        tones = [deg(ch), deg(ch + 2), deg(ch + 4)]
        # bass: triangle, root on 1 and 3, fifth lead-in on 4 and-a
        for k, (beat_i, d) in enumerate([(0, 0), (2, 0), (3.5, 4)]):
            m = deg(ch + d, -1) if d else deg(ch, -1)
            n = int(beat * (1.6 if beat_i < 3 else 0.45) * SR)
            add(osc('tri', hz(m), n) * adsr(n, 0.005, 0.1, 0.7, 0.06) * 0.30, t0 + beat_i * beat)
        # arpeggio: quiet 12.5% square, eighths up and down the chord an octave up, panned
        pat = [0, 1, 2, 1, 0, 1, 2, 1] if mood != 'dream' else [0, 2, 1, 2, 0, 2, 1, 2]
        for e8 in range(8):
            m = tones[pat[e8]] + 12; n = int(beat * 0.42 * SR)
            add(osc('sq', hz(m), n, duty=0.125) * adsr(n, 0.003, 0.05, 0.35, 0.08) * 0.055, t0 + e8 * beat / 2, pan=0.35 if e8 % 2 else -0.35)
        # pad: soft triangle chord, slow attack, under everything
        n = int(bar * SR)
        pad = sum(osc('tri', hz(m + 12), n) for m in tones) / 3
        add(pad * adsr(n, 0.35, 0.3, 0.8, 0.4) * 0.07, t0)
        # lead: the section's motif on this chord; rests in the B section every other bar so phrases breathe
        r, steps = motifs[sec]; rb, sb = r[b % 4], steps[b % 4]
        if sec == 'C' and b % 2: continue
        starts = [i for i, v in enumerate(rb) if v]
        for si, i in enumerate(starts):
            nxt = starts[si + 1] if si + 1 < len(starts) else 8
            dur = (nxt - i) * beat / 2 * 0.92
            base_deg = ch + (0 if i % 4 == 0 else sb[i] % 5)   # strong beats on chord tones
            if i % 4 == 0: base_deg = ch + [0, 2, 4][int(rng.integers(3))]
            m = deg(base_deg, lead_oct); n = int(dur * SR)
            add(osc('sq', hz(m), n, duty=lead_duty, vib=0.004) * adsr(n, 0.01, 0.12, 0.55, min(0.2, dur * 0.4)) * 0.11, t0 + i * beat / 2)
        # percussion: a soft noise tick on the offbeats, a gentle low thump on 1 (no snare: calm)
        for e8 in (1, 3, 5, 7):
            n = int(0.04 * SR); nz = rng.standard_normal(n) * np.exp(-np.arange(n) / (0.008 * SR))
            add(np.diff(nz, prepend=0) * 0.03, t0 + e8 * beat / 2, pan=0.2)
        n = int(0.18 * SR); tt = np.arange(n) / SR
        add(np.sin(2 * np.pi * (55 + 60 * np.exp(-tt / 0.03)) * tt) * np.exp(-tt / 0.07) * 0.22, t0)
    # warmth and space: low-pass, a quarter-note stereo echo, fade the ends
    for ch_ in (L, R): ch_[:] = lp_fast(ch_, 5200, 1)
    L2 = echo(L, beat * 1000 * 0.75, 0.35, 0.22); R2 = echo(R, beat * 1000 * 0.5, 0.35, 0.22)
    end = int(len(bars) * bar * SR + 2.5 * SR); L2, R2 = L2[:end], R2[:end]
    fade = np.minimum(1, np.arange(end) / (0.5 * SR)) * np.minimum(1, (end - np.arange(end)) / (2.5 * SR))
    return np.stack([L2 * fade, R2 * fade], 1)


def ambience(seconds=45, seed=9):
    """Soft wind, a distant fountain and a few birds: what the city sounds like between songs."""
    rng = np.random.default_rng(seed); n = int(seconds * SR); t = np.arange(n) / SR
    wind = lp_fast(rng.standard_normal(n), 350, 2) * (0.55 + 0.45 * np.sin(2 * np.pi * t / 11.0 + 1.3)) * 4.0
    water = (lp_fast(rng.standard_normal(n), 2600, 1) - lp_fast(rng.standard_normal(n), 900, 1)) * 0.35
    birds = np.zeros(n)
    for _ in range(int(seconds / 4)):
        st = rng.uniform(0.5, seconds - 1.5); f0 = rng.uniform(2600, 3800)
        for c in range(int(rng.integers(2, 5))):
            m = int(0.09 * SR); tt = np.arange(m) / SR; i = int((st + c * 0.13) * SR)
            chirp = np.sin(2 * np.pi * (f0 + 1400 * tt / 0.09) * tt) * np.sin(np.pi * tt / 0.09) ** 2 * 0.12
            birds[i:i + m] += chirp[:max(0, min(m, n - i))]
    mono = wind + water + birds
    fade = np.minimum(1, t / 3) * np.minimum(1, (seconds - t) / 3)
    return np.stack([mono * fade, np.roll(mono, int(0.012 * SR)) * fade], 1)


def write(x, name, folder, lufs):
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.8
    os.makedirs(os.path.join(ROOT, 'art', 'masters', 'audio'), exist_ok=True)
    wav = os.path.join(ROOT, 'art', 'masters', 'audio', name + '.wav')
    with wave.open(wav, 'wb') as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR); w.writeframes((x * 32767).astype(np.int16).tobytes())
    out = os.path.join(ROOT, 'assets', folder, name + '.mp3'); os.makedirs(os.path.dirname(out), exist_ok=True)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', wav, '-af', f'loudnorm=I={lufs}:TP=-1.5:LRA=11', '-ar', '44100', '-ac', '2', '-b:a', '128k', out], check=True)
    print('wrote', out)


SONGS = [  # name, key, BPM, chords per bar (scale degrees), form, seed, lead duty, lead octave, mood
    ('hearthfire', 'F', 84, [0, 4, 5, 3], list('AABA'), 11, 0.5, 1, 'warm'),
    ('lantern-walk', 'D', 78, [0, 5, 3, 4], list('ABAB'), 23, 0.25, 1, 'warm'),
    ('starlit-wall', 'A#', 72, [0, 3, 5, 4], list('ACAC'), 37, 0.5, 1, 'dream'),
    ('rebuilding', 'G', 88, [0, 4, 3, 4], list('AABB'), 51, 0.25, 1, 'warm'),
]

if __name__ == '__main__':
    for name, key, bpm, prog, form, seed, duty, octv, mood in SONGS:
        x = song(key, bpm, prog * 2, form + form[2:], seed, duty, octv, mood)   # 48 bars: about 2.2 to 2.7 minutes
        write(x, name, 'music', -23)
    write(ambience(), 'town', 'ambience', -31)
