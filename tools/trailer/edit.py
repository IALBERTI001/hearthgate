"""Trailer editor (Nov 12 2026). Builds finished frames for one output size from an edit list of shots, with short crossfades,
then encodes with ffmpeg. Gameplay frames (900x1600 portrait captures) sit centred on a blurred, darkened copy of themselves;
still images (covers) fill the frame. Real-time footage only: shots are cut, never sped up (CrazyGames: no fast-forwarding).
Usage: python3 edit.py <edit.json> <W> <H> <out.mp4> [--audio audio.wav]
edit.json: {"fade": 6, "shots": [{"still": "cover.jpg", "frames": 24} | {"dir": "...", "from": 60, "to": 170}]}"""
import json, os, subprocess, sys, tempfile
from PIL import Image, ImageFilter, ImageEnhance, ImageDraw
ed, W, H, out = json.load(open(sys.argv[1])), int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]
audio = sys.argv[sys.argv.index('--audio') + 1] if '--audio' in sys.argv else None
FADE = ed.get('fade', 6)

BG = None
if ed.get('bg'):   # a fixed backdrop (the key art, softened) instead of a blurred copy of each frame: wide formats waste less space
    b = Image.open(ed['bg']).convert('RGB'); k = max(W / b.width, H / b.height); b = b.resize((round(b.width * k), round(b.height * k)), Image.LANCZOS)
    x, y = (b.width - W) // 2, (b.height - H) // 2; BG = ImageEnhance.Brightness(b.crop((x, y, x + W, y + H)).filter(ImageFilter.GaussianBlur(ed.get('bgBlur', 5)))).enhance(ed.get('bgDim', 0.6))
def place(im):
    if im.size == (W, H): return im
    if BG is not None:
        f = min(W / im.width, H / im.height); fg = im.resize((round(im.width * f), round(im.height * f)), Image.LANCZOS); bg = BG.copy()
        sh = Image.new('L', (fg.width + 60, fg.height + 60), 0); ImageDraw.Draw(sh).rectangle((30, 30, fg.width + 30, fg.height + 30), fill=150); sh = sh.filter(ImageFilter.GaussianBlur(14))
        bg.paste((0, 0, 0), ((W - fg.width) // 2 - 30, (H - fg.height) // 2 - 30), sh); bg.paste(fg, ((W - fg.width) // 2, (H - fg.height) // 2)); return bg
    k = max(W / im.width, H / im.height); bg = im.resize((max(1, round(im.width * k / 8)), max(1, round(im.height * k / 8))), Image.BILINEAR)
    bg = bg.filter(ImageFilter.GaussianBlur(3)).resize((round(im.width * k), round(im.height * k)), Image.BILINEAR)
    bg = ImageEnhance.Brightness(bg).enhance(0.62); x, y = (bg.width - W) // 2, (bg.height - H) // 2; bg = bg.crop((x, y, x + W, y + H))
    f = min(W / im.width, H / im.height); fg = im.resize((round(im.width * f), round(im.height * f)), Image.LANCZOS)
    bg.paste(fg, ((W - fg.width) // 2, (H - fg.height) // 2)); return bg

def frames(shot):
    if 'still' in shot:
        im = Image.open(shot['still']).convert('RGB'); im = im.resize((W, H), Image.LANCZOS) if im.size != (W, H) and shot.get('fill') else place(im)
        for _ in range(shot['frames']): yield im
    else:
        for i in range(shot['from'], shot['to']): yield place(Image.open(os.path.join(shot['dir'], 'frame-%05d.jpg' % i)).convert('RGB'))

tmp = tempfile.mkdtemp(); n = 0; tail = []; starts = []
for si, shot in enumerate(ed['shots']):
    starts.append(n)
    seq = frames(shot); fade = shot.get('fade', FADE) if si else 0
    for k, im in enumerate(seq):
        if k < fade and tail: im = Image.blend(tail[k], im, (k + 1) / (fade + 1))
        im.save(os.path.join(tmp, 'f%05d.jpg' % n), quality=94); n += 1
    # keep the last `fade` frames of this shot to blend into the next one: rewind n so they are overwritten by the blend
    nxt = ed['shots'][si + 1].get('fade', FADE) if si + 1 < len(ed['shots']) else 0
    if nxt:
        n -= nxt; tail = [Image.open(os.path.join(tmp, 'f%05d.jpg' % (n + j))) for j in range(nxt)]; [t.load() for t in tail]
    else: tail = []
if ed.get('music') and not audio:   # a sound mix: music bed under, the Magister's recorded lines placed where his lines start on screen
    T = n / 30; ins = ['-i', ed['music']]; parts = [f"[0:a]atrim=0:{T},volume={ed.get('musicVol', 0.5)},afade=t=in:d=0.4,afade=t=out:st={T - 1.6}:d=1.5[m]"]; mix = ['[m]']
    for k, v in enumerate(ed.get('voice', [])):
        shot = ed['shots'][v['shot']]; t = (starts[v['shot']] + v['frame'] - shot['from']) / 30
        ins += ['-i', v['file']]; parts.append(f"[{k + 1}:a]volume={ed.get('voiceVol', 1.6)},adelay={round(t * 1000)}|{round(t * 1000)}[v{k}]"); mix.append(f'[v{k}]')
    audio = os.path.join(tmp, 'mix.wav')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', *ins, '-filter_complex', ';'.join(parts) + ';' + ''.join(mix) + f'amix=inputs={len(mix)}:normalize=0:duration=first,loudnorm=I=-16:TP=-1.5:LRA=11[a]', '-map', '[a]', '-ac', '2', '-ar', '44100', audio], check=True)
cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-framerate', '30', '-i', os.path.join(tmp, 'f%05d.jpg')]
if audio: cmd += ['-i', audio, '-c:a', 'aac', '-b:a', '160k', '-shortest']
else: cmd += ['-an']
cmd += ['-c:v', 'libx264', '-profile:v', 'high', '-pix_fmt', 'yuv420p', '-crf', '19', '-preset', 'slow', '-movflags', '+faststart', out]
subprocess.run(cmd, check=True); print(out, n, 'frames', round(n / 30, 2), 's')
