"""Store cover B, step 2: a real late-game frame (tools/cover_b_capture.cjs) cropped to 800x1200 with the title in Lilita One.
Usage: python3 tools/cover_b.py <frame.png> <out.jpg> [tagline] [badge]   (cover C: the Shop-looks frame from tools/cover_c_capture.cjs)
"""
import os, sys
from PIL import Image, ImageDraw, ImageFont
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT = os.path.join(ROOT, 'assets', 'fonts', 'LilitaOne-Regular.ttf')

def main(src, out, tag='Every wave feeds the city.', badge='WAVE 1  ->  WAVE 41'):
    fr = Image.open(src).convert('RGB'); s = fr.width / 720            # frame px per stage px
    top, bot = 0, 1100                                                  # stage rows: treeline to the bottom of the city
    h = bot - top; w = h * 2 / 3; x0 = (720 - w) / 2
    img = fr.crop((round(x0 * s), round(top * s), round((x0 + w) * s), round(bot * s))).resize((800, 1200), Image.LANCZOS)
    band = Image.new('RGBA', img.size, (0, 0, 0, 0)); bd = ImageDraw.Draw(band)
    for i in range(40): bd.rectangle([0, 0, 800, 300 - i * 6], fill=(30, 14, 26, 6))
    img.paste(band, (0, 0), band); d = ImageDraw.Draw(img)
    def centre(text, size, y, fill, stroke):
        f = ImageFont.truetype(FONT, size); tw = d.textlength(text, font=f)
        d.text(((800 - tw) / 2, y), text, font=f, fill=fill, stroke_width=stroke, stroke_fill=(42, 20, 34)); return tw
    centre('HEARTHGATE', 116, 26, (255, 226, 140), 11)
    centre(tag, 40, 156, (255, 236, 250), 5)
    f = ImageFont.truetype(FONT, 44); t = badge; tw = d.textlength(t, font=f)
    d.rounded_rectangle([(800 - tw) / 2 - 24, 212, (800 + tw) / 2 + 24, 274], radius=30, fill=(42, 26, 34, 235), outline=(201, 162, 74), width=4)
    d.text(((800 - tw) / 2, 218), t, font=f, fill=(255, 213, 107))
    img.save(out, quality=92); print('wrote', out)

if __name__ == '__main__':
    main(*sys.argv[1:5])
