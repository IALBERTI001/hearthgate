"""Store covers, concept A: the same city ruined on the left and restored on the right, the gatehouse and four tier-3
towers holding against enemies coming over the meadow, the title in Lilita One. Built only from shipped art.
Usage: python3 tools/covers.py <outdir>   ->  cover-A-1920x1080.jpg, cover-A-800x1200.jpg, cover-A-800x800.jpg, cover-A-1600x1000.jpg
"""
import os, sys, json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
A = lambda *p: os.path.join(ROOT, *p)
FONT = A('assets', 'fonts', 'LilitaOne-Regular.ttf')
CITY = json.loads(open(A('js', 'city.js')).read().split('root.HG_CITY = ')[1].split('};')[0].replace("'", '"')
                  .replace('band', '"band"').replace('crop', '"crop"') + '}') if False else None
PLAT = [(220, 133), (460, 133), (1073, 133), (1313, 133)]


def scene(W, H, city_scale, meadow_h, split_x):
    """Meadow on top, the city below it (ruined left of split_x, restored right, soft diagonal), towers, enemies, bolts."""
    rest = Image.open(A('art', 'masters', 'city-north-gate-restored.png')).convert('RGB')
    ruin = Image.open(A('art', 'masters', 'city-north-gate-ruined.png')).convert('RGB')
    cw, ch = round(1536 * city_scale), round(1024 * city_scale)
    rest, ruin = rest.resize((cw, ch), Image.LANCZOS), ruin.resize((cw, ch), Image.LANCZOS)
    ox = (W - cw) // 2
    # ruined on the left, restored on the right, blended over a slanted band
    yy, xx = np.mgrid[0:ch, 0:cw]
    t = np.clip(((xx + ox) - split_x + (yy - ch / 2) * 0.25) / 90 + 0.5, 0, 1)[..., None]
    city = Image.fromarray((np.asarray(ruin) * (1 - t) + np.asarray(rest) * t).astype(np.uint8))
    out = Image.new('RGB', (W, H))
    meadow = Image.open(A('assets', 'ground', 'meadow.webp')).convert('RGB')
    m = meadow.resize((W, round(meadow.height * W / meadow.width)), Image.LANCZOS)
    for y in range(0, meadow_h + 40, m.height): out.paste(m, (0, y))
    fade = Image.linear_gradient('L').resize((cw, 60))
    mask = Image.new('L', (cw, ch), 255); mask.paste(fade, (0, 0))
    out.paste(city, (ox, meadow_h), mask)
    s = city_scale
    def place(img, x, base_y, h):
        im = Image.open(img).convert('RGBA'); pad = 8
        k = h / (im.height - 2 * pad); im = im.resize((round(im.width * k), round(im.height * k)), Image.LANCZOS)
        out.paste(im, (round(x - im.width / 2), round(base_y - im.height + pad * k)), im)
    # enemies crossing the meadow toward the gate
    u = W / 1920
    wall = meadow_h + 70 * s          # just in front of the wall, where the crowd presses
    # every enemy stands in open grass between the towers, clear of the gatehouse and of each other
    # every enemy stands in open grass between the subtitle and the wall, between the towers, clear of each other
    # at the game's own scale: every enemy shorter than a tower (owner's correction, Nov 5 2026), so a crowd, not giants
    FOES = [('golem', 0.50, -22, 132, 1), ('slime', 0.40, -14, 92, 0), ('beetle', 0.60, -20, 84, 2), ('slime', 0.33, -70, 80, 3),
            ('beetle', 0.68, -78, 76, 4), ('slime', 0.56, -120, 72, 5), ('slime', 0.31, -135, 70, 2), ('beetle', 0.44, -150, 66, 1),
            ('slime', 0.69, -160, 68, 4)]
    for kind, x, dy, h, frame in FOES:
        sheet = Image.open(A('assets', 'enemies', f'{kind}-walk.webp')).convert('RGBA'); fw = sheet.width // 6
        fr = sheet.crop((frame * fw, 0, (frame + 1) * fw, sheet.height)); k = h * u / (fr.height - 16)
        fr = fr.resize((round(fr.width * k), round(fr.height * k)), Image.LANCZOS)
        if x > 0.5: fr = fr.transpose(Image.FLIP_LEFT_RIGHT)   # everyone faces the gate
        bx, by = round(W * x - fr.width / 2), round(wall + dy * u - fr.height)
        sh = Image.new('RGBA', fr.size, (0, 0, 0, 0)); d = ImageDraw.Draw(sh)
        d.ellipse([fr.width * 0.15, fr.height * 0.86, fr.width * 0.85, fr.height * 1.0], fill=(20, 24, 10, 110))
        sh = sh.filter(ImageFilter.GaussianBlur(5)); out.paste(sh, (bx + 6, by), sh)
        out.paste(fr, (bx, by), fr)
    # the wall is nearer the camera than the enemies outside it: lay the wall layer back over them
    wl = Image.open(A('art', 'masters', 'city-wall.png')).convert('RGBA').resize((cw, ch), Image.LANCZOS)
    out.paste(wl, (ox, meadow_h), wl)
    # tier-3 towers on the four painted platforms, a contact shadow under each
    for (px, py), el in zip(PLAT, ['fire', 'frost', 'storm', 'fire']):
        x, y = ox + px * s, meadow_h + py * s
        sh = Image.new('RGBA', (int(70 * s * 2), int(30 * s * 2)), (0, 0, 0, 0)); ImageDraw.Draw(sh).ellipse([0, 0, sh.width - 1, sh.height - 1], fill=(20, 24, 10, 120))
        sh = sh.filter(ImageFilter.GaussianBlur(5)); out.paste(sh, (round(x - sh.width / 2 + 8), round(y - sh.height / 2 + 6)), sh)
        place(A('assets', 'towers', f'{el}-3.webp'), x, y + 24 * s, 196 * s)
    # glowing bolts from the towers toward the enemies, and the SHATTER burst on the golem
    glow = Image.new('RGBA', (W, H), (0, 0, 0, 0)); g = ImageDraw.Draw(glow)
    col = {'fire': (255, 140, 60), 'frost': (150, 230, 255), 'storm': (210, 150, 255)}
    # only the two inner towers fire: the outer towers fall outside the portrait and square crops, and a bolt cut by the edge reads as a defect
    for (px, py), el, (tx, dty) in list(zip(PLAT, ['fire', 'frost', 'storm', 'fire'], [(0.31, -150), (0.385, -72), (0.615, -76), (0.69, -170)]))[1:3]:
        x0, y0 = ox + px * s, meadow_h + py * s - 150 * s
        x1, y1 = W * tx, wall + dty * u
        for i in range(9):
            f = 0.35 + i * 0.07; x, y = x0 + (x1 - x0) * f, y0 + (y1 - y0) * f; r = (3 + i * 1.2) * u
            g.ellipse([x - r * 2.6, y - r * 2.6, x + r * 2.6, y + r * 2.6], fill=col[el] + (50 + i * 16,))
            g.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, 110 + i * 16))
    glow = glow.filter(ImageFilter.GaussianBlur(3)); out.paste(glow, (0, 0), glow)
    return out


def title(img, size, y, sub=None, sub_size=None):
    W = img.width; d = ImageDraw.Draw(img); f = ImageFont.truetype(FONT, size)
    tw = d.textlength('HEARTHGATE', font=f); x = (W - tw) / 2
    band = Image.new('RGBA', img.size, (0, 0, 0, 0)); bd = ImageDraw.Draw(band)
    for i in range(40): bd.rectangle([0, 0, W, y + size * 1.9 - i * size * 0.05], fill=(30, 14, 26, 6))
    img.paste(band, (0, 0), band)
    d = ImageDraw.Draw(img)
    d.text((x, y), 'HEARTHGATE', font=f, fill=(255, 226, 140), stroke_width=max(6, size // 10), stroke_fill=(42, 20, 34))
    if sub:
        fs = ImageFont.truetype(FONT, sub_size); sw = d.textlength(sub, font=fs)
        d.text(((W - sw) / 2, y + size * 1.08), sub, font=fs, fill=(255, 244, 220), stroke_width=max(4, sub_size // 9), stroke_fill=(42, 20, 34))
    return img


def labels(img, left, right, y, size):
    d = ImageDraw.Draw(img); f = ImageFont.truetype(FONT, size)
    for text, cx, colr in [(left, img.width * 0.25, (220, 210, 190)), (right, img.width * 0.75, (255, 226, 140))]:
        w = d.textlength(text, font=f); pad = size * 0.4
        d.rounded_rectangle([cx - w / 2 - pad, y - pad * 0.6, cx + w / 2 + pad, y + size + pad * 0.5], radius=size * 0.5, fill=(42, 26, 34, 230), outline=(201, 162, 74), width=max(3, size // 14))
        d.text((cx - w / 2, y), text, font=f, fill=colr)
    return img


def main(out):
    os.makedirs(out, exist_ok=True)
    land = scene(1920, 1080, 1.25, 380, 960)
    title(land, 150, 26, 'Hold the gate. Rebuild the city.', 54)
    land.save(os.path.join(out, 'cover-A-1920x1080.jpg'), quality=92)
    fp = scene(1600, 1000, 1.04, 330, 800); title(fp, 128, 22, 'Hold the gate. Rebuild the city.', 46)
    fp.save(os.path.join(out, 'cover-A-1600x1000.jpg'), quality=92)
    big = scene(1920, 1440, 1.25, 560, 960)
    sq = big.crop((420, 120, 1500, 1200)).resize((800, 800), Image.LANCZOS)
    title(sq, 96, 18, 'Hold the gate. Rebuild the city.', 34)
    sq.save(os.path.join(out, 'cover-A-800x800.jpg'), quality=92)
    tall = scene(1920, 1560, 1.25, 600, 960)   # the inner towers sit 383 px either side of the gate: keep both whole
    pt = tall.crop((445, 0, 1473, 1542)).resize((800, 1200), Image.LANCZOS)
    title(pt, 104, 30, 'Hold the gate. Rebuild the city.', 34)
    pt.save(os.path.join(out, 'cover-A-800x1200.jpg'), quality=92)
    print('covers written to', out)


if __name__ == '__main__':
    main(sys.argv[1] if len(sys.argv) > 1 else A('store'))
