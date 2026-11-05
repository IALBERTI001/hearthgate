"""Bring Warden's Outer Seal kit into Hearthgate, one asset per sheet (user rule, Nov 5 2026).
Each kit piece is already its own file in Warden (art/kits/outer); this pads it with a clear margin, scales it to the
Hearthgate scale and lists it in assets/manifest.json with its subject. The meadow ground is the full-screen ground layer.
Run after tools/split_sheets.py:  python3 tools/build_kit.py <warden repo dir>
"""
import json, os, sys
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PAD, ALPHA = 8, 16
# Hearthgate draws the kit at 0.42 of Warden's kit size: a wall section 150 px wide and 65 px tall on the 720 px stage.
KIT_SCALE = 0.42
PIECES = {  # file in art/kits/outer -> (subject, scale)
    'wall_a': ('stone wall section A', KIT_SCALE),
    'wall_b': ('stone wall section B', KIT_SCALE),
    'gatepost': ('gatepost with blue orb', KIT_SCALE),
    'pillar': ('wall pillar with blue orb', KIT_SCALE),
    'forest_edge_top': ('forest edge, top of the map', 720 / 1222 * 1.04),
}

def padded(im, scale):
    im = im.convert('RGBA')
    im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    out = Image.new('RGBA', (im.width + 2 * PAD, im.height + 2 * PAD), (0, 0, 0, 0))
    out.alpha_composite(im, (PAD, PAD))
    a = np.array(out); a[a[:, :, 3] <= ALPHA] = 0
    return Image.fromarray(a)

def main(warden):
    man_path = os.path.join(ROOT, 'assets', 'manifest.json')
    manifest = json.load(open(man_path))
    os.makedirs(os.path.join(ROOT, 'assets', 'kit'), exist_ok=True)
    for name, (subject, scale) in PIECES.items():
        img = padded(Image.open(os.path.join(warden, 'art/kits/outer', name + '.png')), scale)
        rel = f'kit/{name.replace("_", "-")}.webp'
        img.save(os.path.join(ROOT, 'assets', rel), quality=88, method=6)
        manifest[rel] = {'subject': subject, 'frames': 1, 'cell': [img.width, img.height], 'pad': PAD,
                         'anchor': 'bottom-center', 'source': f'warden art/kits/outer/{name}.png'}
    # ground layer: the Outer Seal meadow, scaled so its grass matches the city painting's grass, cropped to the field
    g = Image.open(os.path.join(warden, 'art/ground/outer/meadow_v1.png')).convert('RGB')
    s = 680 / g.height; g = g.resize((round(g.width * s), 680), Image.LANCZOS)
    x0 = (g.width - 720) // 2; g = g.crop((x0, 0, x0 + 720, 680))
    os.makedirs(os.path.join(ROOT, 'assets', 'ground'), exist_ok=True)
    g.save(os.path.join(ROOT, 'assets', 'ground', 'meadow.webp'), quality=82, method=6)
    json.dump(manifest, open(man_path, 'w'), indent=1)
    print('kit pieces:', ', '.join(PIECES))

if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else '/home/claude/ialberti001/warden-of-power')
