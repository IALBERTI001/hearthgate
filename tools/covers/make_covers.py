"""Store covers from one key art painting (Nov 12 2026), so all sizes share one look (CrazyGames asks for consistent covers). CrazyGames rules (checked Nov 12 2026): the game's title is the only text, no
borders, icons or logos, not blurry. The title is set in the game's own display font (Lilita One) in the HUD's gold with a
plum outline and a soft shadow, sized to each cover. Each size is a crop box of the painting (never stretched), scaled to size. Usage: python3 tools/covers/make_covers.py <keyart.png> <outdir>"""
import sys
from PIL import Image, ImageDraw, ImageFont, ImageFilter
art, out = sys.argv[1:3]
FONT = 'assets/fonts/LilitaOne-Regular.ttf'
GOLD, PLUM = (255, 226, 140), (42, 20, 34)

def title(im, cx, top, size):
    f = ImageFont.truetype(FONT, size); t = 'HEARTHGATE'
    w = f.getbbox(t, stroke_width=round(size * 0.07))[2]; x = cx - w / 2
    sh = Image.new('RGBA', im.size, (0, 0, 0, 0)); d = ImageDraw.Draw(sh)
    d.text((x + size * 0.03, top + size * 0.06), t, font=f, fill=(20, 8, 16, 170), stroke_width=round(size * 0.09), stroke_fill=(20, 8, 16, 170))
    sh = sh.filter(ImageFilter.GaussianBlur(size * 0.05)); im.alpha_composite(sh)
    d = ImageDraw.Draw(im); d.text((x, top), t, font=f, fill=GOLD, stroke_width=round(size * 0.07), stroke_fill=PLUM)
    return w

A = Image.open(art).convert('RGBA')
# name, w, h, crop box in the 1536x1024 painting, title width (share of w), title top (share of h)
specs = [('cover-1920x1080', 1920, 1080, (0, 57, 1536, 921), 0.55, 0.035),   # whole scene; keeps both villagers' feet in
         ('cover-1600x1000', 1600, 1000, (0, 10, 1536, 970), 0.55, 0.035),
         ('cover-1080x1620', 1080, 1620, (24, 0, 707, 1024), 0.86, 0.03),    # the Magister close up: first frame of the portrait video
         ('cover-800x1200', 800, 1200, (24, 0, 707, 1024), 0.86, 0.03),
         ('cover-800x800', 800, 800, (21, 66, 885, 930), 0.80, 0.03)]   # the Magister with the fire and frost towers
for name, w, h, box, tw, tt in specs:
    im = A.crop(box).resize((w, h), Image.LANCZOS); f = ImageFont.truetype(FONT, 100); bw = f.getbbox('HEARTHGATE', stroke_width=7)[2]
    title(im, w / 2, round(h * tt), round(100 * w * tw / bw))
    im.convert('RGB').save(f'{out}/{name}.jpg', quality=93); print(name, 'scale', round(w / (box[2] - box[0]), 3), 'aspect err', round((box[2] - box[0]) / (box[3] - box[1]) - w / h, 4))
