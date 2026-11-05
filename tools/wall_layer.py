"""Cut the city's north wall, gatehouse and tower platforms out of the restored painting as their own foreground layer,
so enemies standing outside the wall are drawn behind it (the wall is nearer the camera than they are).
Grass is clear, everything built is kept; the cut ends below the wall's foot. Writes art/masters/city-wall.png (1536 px
space) and assets/city/city-wall.webp (the game's 720 px crop at half scale)."""
import os, numpy as np, cv2
from PIL import Image
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
R = np.asarray(Image.open(os.path.join(ROOT, 'art/masters/city-north-gate-restored.png')).convert('RGB'))
hsv = cv2.cvtColor(R, cv2.COLOR_RGB2HSV)
grass = (hsv[..., 0] >= 30) & (hsv[..., 0] <= 58) & (hsv[..., 1] >= 80) & (hsv[..., 2] >= 80)
built = (~grass).astype(np.uint8)
built[230:] = 0                                   # only the wall band
built[:, :] = cv2.morphologyEx(built, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
built = cv2.morphologyEx(built, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
# keep only the shapes that touch the wall line (y 90..170), dropping stray rocks and grass tufts elsewhere
n, lab, stats, _ = cv2.connectedComponentsWithStats(built)
keep = np.zeros_like(built)
for i in range(1, n):
    x, y, w, h, a = stats[i]
    if a > 400 and y < 170 and y + h > 90: keep[lab == i] = 1
# solid from the wall's top edge down: per column, everything below the first built pixel belongs to the wall
top = np.full(1536, 10_000)
for x in range(1536):
    ys = np.nonzero(keep[:170, x])[0]
    if len(ys): top[x] = ys[0]
top = np.minimum(top, 10_000)
from scipy.ndimage import median_filter, minimum_filter
top = median_filter(np.where(top > 1000, 999, top), size=9)
xs = np.arange(1536); top = np.where((xs < 650) | (xs > 885), np.maximum(top, 52), top)   # only the gatehouse rises above the wall line
solid = (np.arange(1024)[:, None] >= top[None, :]).astype(np.float32)
cap = keep.astype(np.float32); cap[:40] = 0; cap[:, :650] *= (np.arange(1024)[:, None] >= 38); cap[:, 885:] *= (np.arange(1024)[:, None] >= 38)
solid = np.maximum(solid, cap)                                  # pillar caps above the line stay
alpha = cv2.GaussianBlur(solid, (0, 0), 1.0)
foot = np.clip((215 - np.arange(1024)) / 25, 0, 1)[:, None]
alpha = (alpha * foot * 255).astype(np.uint8)
rgba = np.dstack([R, alpha])
Image.fromarray(rgba).save(os.path.join(ROOT, 'art/masters/city-wall.png'))
Image.fromarray(rgba).crop((40, 0, 1480, 960)).resize((720, 480), Image.LANCZOS).save(os.path.join(ROOT, 'assets/city/city-wall.webp'), quality=90, method=6)
print('wall layer', int((alpha > 0).sum()), 'px')
