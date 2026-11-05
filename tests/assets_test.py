"""One asset, one sheet (user rule, Nov 5 2026): every sprite the game draws lives in its own file, and every frame in it
holds that subject only, with clear space all round. Nothing from another asset can bleed into a frame.
Run: python3 tests/assets_test.py   (needs Pillow, numpy, scipy)

Checks, for every image under assets/towers and assets/enemies:
1. It is listed in assets/manifest.json with exactly one subject, its frame count and its cell size.
2. The file is exactly frames x cell wide and one cell tall (one row, one subject).
3. Every frame has a transparent margin (no pixel above alpha 16 within MARGIN px of the cell edge), so nothing is cut
   off and nothing from a neighbour reaches in.
4. Every frame is one subject: one main shape, and every loose bit (a drip, a spark) sits within NEAR px of it and is
   smaller than STRAY of it. A fragment of another sprite sits apart from the subject and fails the test.
"""
import json, os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MARGIN, ALPHA, STRAY, NEAR = 2, 16, 0.08, 14
fails = []

def fail(msg):
    fails.append(msg)

def one_subject(cell):
    lab, k = ndimage.label(cell)
    if k <= 1:
        return []
    sizes = ndimage.sum(cell, lab, range(1, k + 1)); main = int(np.argmax(sizes)) + 1
    gap = ndimage.distance_transform_edt(lab != main)
    out = []
    for j in range(1, k + 1):
        if j == main:
            continue
        d = gap[lab == j].min()
        if d > NEAR or sizes[j - 1] > STRAY * sizes[main - 1]:
            out.append(f'a separate {int(sizes[j - 1])} px shape {d:.0f} px from the subject')
    return out

man_path = os.path.join(ROOT, 'assets', 'manifest.json')
manifest = json.load(open(man_path)) if os.path.exists(man_path) else {}
if not manifest:
    fail('assets/manifest.json is missing or empty')

for folder in ('towers', 'enemies', 'kit'):
    d = os.path.join(ROOT, 'assets', folder)
    for f in sorted(os.listdir(d)):
        rel = f'{folder}/{f}'
        if not f.lower().endswith(('.png', '.webp')):
            continue
        m = manifest.get(rel)
        if not m:
            fail(f'{rel}: not in the manifest (every sheet must name its one subject)'); continue
        if not isinstance(m.get('subject'), str) or not m['subject']:
            fail(f'{rel}: manifest entry needs one subject')
        im = Image.open(os.path.join(d, f)).convert('RGBA')
        n, cw, ch = m.get('frames', 1), m['cell'][0], m['cell'][1]
        if im.size != (n * cw, ch):
            fail(f'{rel}: size {im.size} is not {n} frames of {cw}x{ch} in one row'); continue
        a = np.array(im)[:, :, 3] > ALPHA
        for i in range(n):
            cell = a[:, i * cw:(i + 1) * cw]
            if not cell.any():
                fail(f'{rel} frame {i}: empty'); continue
            edge = cell.copy(); edge[MARGIN:-MARGIN, MARGIN:-MARGIN] = False
            if edge.any():
                fail(f'{rel} frame {i}: {int(edge.sum())} px touch the cell edge (cut off or bleeding)')
            for msg in one_subject(cell):
                fail(f'{rel} frame {i}: {msg}')

if fails:
    print('ASSET RULE FAILED'); [print(' -', f) for f in fails[:40]]
    if len(fails) > 40: print(f' ... {len(fails) - 40} more')
    sys.exit(1)
print(f'asset rule passed: {len(manifest)} sheets, one subject each, no bleed')
