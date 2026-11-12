"""The Magister's recorded lines (owner's own voice, Nov 12 2026) -> assets/voice/<line>.mp3.
Cuts the record/stop clicks and the quiet before and after (keeps 0.08 s before, 0.15 s after), removes rumble, evens out
peaks gently, levels every line to -19 LUFS with a -4.4 dBFS limiter, mono 44.1 kHz 80 kbps.
Usage: python3 tools/make_voice.py [masters dir, default art/masters/audio/voice]"""
import glob, os, re, subprocess, sys
src = sys.argv[1] if len(sys.argv) > 1 else 'art/masters/audio/voice'
os.makedirs('assets/voice', exist_ok=True)
def run(args): return subprocess.run(args, capture_output=True, text=True)
def loud(path, af): e = run(['ffmpeg', '-hide_banner', '-i', path, '-af', af + ',ebur128=peak=true', '-f', 'null', '-']).stderr; return float(re.findall(r'I:\s+(-?[0-9.]+) LUFS', e)[-1])
for f in sorted(glob.glob(os.path.join(src, '*.m4a'))):
    name = os.path.basename(f)[:-4]
    d = float(run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', f]).stdout)
    e = run(['ffmpeg', '-hide_banner', '-i', f, '-af', 'silencedetect=n=-42dB:d=0.12', '-f', 'null', '-']).stderr
    quiet, t0 = [], None
    for k, v in re.findall(r'silence_(start|end): ([0-9.]+)', e):
        if k == 'start': t0 = float(v)
        else: quiet.append((t0 or 0, float(v))); t0 = None
    if t0 is not None: quiet.append((t0, d))
    quiet.sort(); sound, t = [], 0
    for a, b in quiet:
        if a > t + 0.001: sound.append((t, a))
        t = max(t, b)
    if t < d - 0.01: sound.append((t, d))
    sound = [(a, b) for a, b in sound if b - a >= 0.15]   # clicks and breaths are shorter than any word
    a, b = max(0, sound[0][0] - 0.08), sound[-1][1] + 0.15
    base = f'atrim={a}:{b},asetpts=N/SR/TB,highpass=f=80,acompressor=threshold=0.125:ratio=2.5:attack=5:release=80,afade=t=in:d=0.03,afade=t=out:st={b - a - 0.1}:d=0.1'
    g = -19 - loud(f, base)
    run(['ffmpeg', '-y', '-loglevel', 'error', '-i', f, '-af', base + f',volume={g}dB,alimiter=limit=0.6:level=disabled:attack=1:release=50,aresample=44100', '-ac', '1', '-c:a', 'libmp3lame', '-b:a', '80k', f'assets/voice/{name}.mp3'])
    print(name, round(a, 2), round(b, 2), round(loud(f'assets/voice/{name}.mp3', 'anull'), 1), 'LUFS')
