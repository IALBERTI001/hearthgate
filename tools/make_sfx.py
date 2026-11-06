"""Hearthgate's made-in-code sounds for the systems added Nov 5-6 2026 (game-audio-assets: synthesised, logged, levels matched).
Writes assets/sfx/{buy,feat,surge,claim,boss_horn,level_up}.mp3 (mono 64 kbps), then measure with ffmpeg ebur128.
Usage: python3 tools/make_sfx.py
"""
import os, subprocess, wave
import numpy as np
SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def env(n, a=0.005, r=0.3):
    t = np.arange(n) / SR; e = np.minimum(1, t / a) * np.exp(-t / r); return e
def bell(f, dur, r=0.35, amp=1.0):
    n = int(SR * dur); t = np.arange(n) / SR
    s = np.sin(2 * np.pi * f * t) + 0.45 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t / (r * 0.4)) + 0.2 * np.sin(2 * np.pi * f * 5.4 * t) * np.exp(-t / (r * 0.2))
    return amp * s * env(n, 0.003, r)
def place(out, s, at):
    i = int(at * SR); out[i:i + len(s)] += s[:len(out) - i]; return out
def blank(d): return np.zeros(int(SR * d))
def write(name, x, peak_db=-10):
    x = x / (np.max(np.abs(x)) + 1e-9) * 10 ** (peak_db / 20)
    tmp = f'/tmp/{name}.wav'
    with wave.open(tmp, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((x * 32767).astype(np.int16).tobytes())
    out = os.path.join(ROOT, 'assets', 'sfx', name + '.mp3')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', tmp, '-ac', '1', '-b:a', '64k', out], check=True); os.remove(tmp)
    print('wrote', out)

# a purchase: two bright coin bells, a fifth apart
x = blank(0.7); place(x, bell(1318.5, 0.6, 0.18), 0); place(x, bell(1975.5, 0.6, 0.22, 0.8), 0.07); write('buy', x)
# a Feat: a four-note arpeggio up (C E G C) with a soft pad under it
x = blank(1.3)
for i, f in enumerate([523.25, 659.25, 783.99, 1046.5]): place(x, bell(f, 0.9, 0.3, 0.9 if i < 3 else 1.1), i * 0.09)
t = np.arange(len(x)) / SR; x += 0.18 * (np.sin(2 * np.pi * 261.6 * t) + np.sin(2 * np.pi * 392 * t)) * np.minimum(1, t / 0.1) * np.exp(-t / 0.5)
write('feat', x)
# the Surge: a rising filtered-noise whoosh with a shimmer on top
n = int(SR * 1.1); t = np.arange(n) / SR; rng = np.random.default_rng(3)
noise = rng.standard_normal(n); k = np.exp(-np.linspace(4, 0.3, n)); sm = np.convolve(noise, np.ones(30) / 30, 'same') * k
f = 300 + 1500 * (t / t[-1]) ** 2; sh = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.5 * np.minimum(1, t / 0.6) * np.exp(-np.maximum(0, t - 0.8) / 0.12)
x = (sm * 1.3 + sh) * np.minimum(1, (t[-1] - t) / 0.15); write('surge', x, -14)
# a quest claim: a quick sparkle of three high pings
x = blank(0.6)
for i, f in enumerate([1567.98, 2093.0, 2637.0]): place(x, bell(f, 0.4, 0.12, 0.8), i * 0.05)
write('claim', x)
# a boss arrives: a low horn with a slow swell and a drum hit
n = int(SR * 1.6); t = np.arange(n) / SR
horn = sum(a * np.sin(2 * np.pi * 98 * h * t) for h, a in [(1, 1), (2, .6), (3, .35), (4, .2), (5, .1)]) * np.minimum(1, t / 0.25) * np.exp(-np.maximum(0, t - 0.9) / 0.25)
drum = np.sin(2 * np.pi * (60 + 80 * np.exp(-t / 0.05)) * t) * np.exp(-t / 0.25)
write('boss_horn', horn * 0.7 + drum * 1.0, -12.5)
# a building levels up: a warm low-high chime
x = blank(0.6); place(x, bell(587.33, 0.5, 0.2), 0); place(x, bell(880, 0.5, 0.25, 0.9), 0.06); write('level_up', x)
