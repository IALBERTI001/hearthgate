"""Cut Warden's shared sprite sheets into one sheet per asset (user rule, Nov 5 2026: every asset has its own sheet).

Never cut a shared sheet on an even grid: Warden's sheets are not grid-true (slime undersides and golem heads cross into
the beetle row), so a grid cut drags pieces of neighbours into a frame and chops parts off the subject. Instead:
1. Find every separate shape on the whole sheet (parts within JOIN px join into one shape: drips, legs, sparks).
2. Big shapes are subjects; each goes to the grid slot its centre falls in. Small shapes go to the nearest subject.
3. Each subject is copied out through its own mask only, so nothing outside its outline can come along.
4. Frames of one subject go side by side in its own sheet, all the same cell size, with a clear margin, keeping the
   subject's height above the row's ground line (so a hopping slime still hops).
Writes assets/<kind>/<name>.webp and assets/manifest.json. Run: python3 tools/split_sheets.py <warden game/assets dir>
"""
import json, os, sys
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
JOIN, ALPHA, PAD = 6, 16, 10

def subjects(path, cols, rows):
    im = Image.open(path).convert('RGBA'); rgba = np.array(im)
    a = rgba[:, :, 3] > ALPHA
    lab, n = ndimage.label(a)  # raw outlines: separate sprites never merge
    cw, ch = im.width / cols, im.height / rows
    idx = range(1, n + 1)
    area = ndimage.sum(a, lab, idx); com = ndimage.center_of_mass(a, lab, idx)
    big_min = 0.04 * cw * ch
    slots = {}
    for i, (ar, (cy, cx)) in enumerate(zip(area, com), start=1):
        if ar >= big_min:
            slots.setdefault((int(cy // ch), int(cx // cw)), []).append(i)
    # Two subjects joined by a glow or a spark: split the joined shape at its thinnest line between them, never the grid.
    for _ in range(cols * rows):
        missing = [(r, c) for r in range(rows) for c in range(cols) if (r, c) not in slots]
        if not missing:
            break
        r, c = missing[0]
        mates = [k for k in slots if (abs(k[0] - r) + abs(k[1] - c)) == 1 and len(slots[k]) == 1 and area[slots[k][0] - 1] > 1.6 * np.median(area[area >= big_min])]
        assert mates, f'{path}: slot {(r, c)} is empty and no joined neighbour to split'
        k = mates[0]; i = slots[k][0]; m = lab == i
        if k[1] == c:  # stacked: cut along the emptiest row between the two slot centres
            lo, hi = int(min(r, k[0]) * ch + ch / 2), int(max(r, k[0]) * ch + ch / 2)
            cut = lo + int(np.argmin(m[lo:hi].sum(axis=1)))
            part = np.zeros_like(m); part[cut:] = True
        else:          # side by side: cut along the emptiest column
            lo, hi = int(min(c, k[1]) * cw + cw / 2), int(max(c, k[1]) * cw + cw / 2)
            cut = lo + int(np.argmin(m[:, lo:hi].sum(axis=0)))
            part = np.zeros_like(m); part[:, cut:] = True
        far = part if (r > k[0] or c > k[1]) else ~part
        n += 1; lab[m & far] = n
        area = np.append(area, (m & far).sum()); area[i - 1] -= (m & far).sum()
        com = list(com) + [ndimage.center_of_mass(m & far)]
        slots[(r, c)] = [n]
    idx = range(1, n + 1)
    assert len(slots) == cols * rows, f'{path}: found {len(slots)} subjects for {cols}x{rows} slots'
    # loose bits (drips, sparks, leg tips) join the subject whose outline is nearest; too far from any = dropped
    dist = {k: ndimage.distance_transform_edt(~np.isin(lab, v)) for k, v in slots.items()}
    for i, ar in enumerate(area, start=1):
        if 0 < ar < big_min:
            pts = lab == i
            k, d = min(((k, dist[k][pts].min()) for k in slots), key=lambda t: t[1])
            if d <= 3 * JOIN:
                slots[k].append(i)
    out = {}
    for k, ids in slots.items():
        mask = np.isin(lab, ids)
        ys, xs = np.where(mask)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        piece = rgba[y0:y1, x0:x1].copy(); piece[~mask[y0:y1, x0:x1]] = 0
        out[k] = (Image.fromarray(piece), y1)  # y1 = the subject's bottom on the source sheet
    return out

def sheet(frames, scale):
    """frames: list of (image, bottom_y). Same cell for all; each keeps its lift above the lowest bottom in the set."""
    ground = max(b for _, b in frames)
    imgs = []
    for im, b in frames:
        w, h = max(1, round(im.width * scale)), max(1, round(im.height * scale))
        imgs.append((im.resize((w, h), Image.LANCZOS), round((ground - b) * scale)))
    cw = max(i.width for i, _ in imgs) + 2 * PAD
    chh = max(i.height + lift for i, lift in imgs) + 2 * PAD
    out = Image.new('RGBA', (cw * len(imgs), chh), (0, 0, 0, 0))
    for n, (im, lift) in enumerate(imgs):
        out.alpha_composite(im, (n * cw + (cw - im.width) // 2, chh - PAD - lift - im.height))
    arr = np.array(out); arr[arr[:, :, 3] <= ALPHA] = 0  # resampling haze at the edge becomes fully clear
    return Image.fromarray(arr), (cw, chh)

def main(src):
    manifest = {}
    def save(rel, img, cell, frames, subject, source):
        p = os.path.join(ROOT, 'assets', rel); os.makedirs(os.path.dirname(p), exist_ok=True)
        img.save(p, quality=88, method=6)
        manifest[rel] = {'subject': subject, 'frames': frames, 'cell': list(cell), 'anchor': 'bottom-center', 'source': source}
    walk = subjects(os.path.join(src, 'enemies/enemy-walk.png'), 6, 3)
    for r, name in enumerate(['slime', 'beetle', 'golem']):
        img, cell = sheet([walk[(r, c)] for c in range(6)], 0.5)
        save(f'enemies/{name}-walk.webp', img, cell, 6, name, 'warden enemies/enemy-walk.png row %d' % r)
    tiers = subjects(os.path.join(src, 'towers/element-tiers.png'), 3, 3)
    for r, name in enumerate(['fire', 'frost', 'storm']):
        for c in range(3):
            im, b = tiers[(r, c)]
            img, cell = sheet([(im, b)], 180 / im.height)
            save(f'towers/{name}-{c + 1}.webp', img, cell, 1, f'{name} tower tier {c + 1}', 'warden towers/element-tiers.png r%d c%d' % (r, c))
    json.dump(manifest, open(os.path.join(ROOT, 'assets', 'manifest.json'), 'w'), indent=1)
    print(f'{len(manifest)} sheets written')

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else '/home/claude/ialberti001/warden-of-power/game/assets')
