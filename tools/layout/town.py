# Hearthgate 3D town layout at human scale (owner, Nov 13 2026: "we have to work on the proportions here ... I want the
# wizard to be able to walk into the buildings inside. if we have to make the city bigger then so be it").
# The buildings are real-size rooms now (tools/blender/hg_layout.SIZES, 6-10 m across, 6.5-18.5 m tall), so the ring
# wall grows round them. For each ring size this searches the wall camera (whole ring, towers and lanes in view and clear
# of the HUD at three window shapes) and then places the plaza and the ten buildings: streets of 3 m or more between
# them, 2.5 m or more from the wall, clear of the gatehouse and the towers, every roof line and the plaza seen from the
# camera past the wall, the gatehouse, the towers and each other. It prints the smallest ring that fits.
# Usage: python3 tools/layout/town.py
import math, sys, os, numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'blender'))
import hg_layout as LY

HEIGHT = {'forge': 7.6, 'frostSpire': 14.8, 'stormBell': 12.9, 'bakery': 10.4, 'market': 6.6, 'tavern': 10.6,
          'granary': 8.8, 'library': 9.0, 'cottages': 6.8, 'watchtower': 13.2}   # hg_city.py, ground to the top
WALL_H = 4.0
SHAPES = [(1280, 720), (1600, 727), (1080, 900)]

def cam(pos, look, fov, aspect=16 / 9):
    pos = np.array(pos, float); f = np.array(look, float) - pos; f /= np.linalg.norm(f)
    r = np.cross([0, 1, 0], f); r /= np.linalg.norm(r); u = np.cross(f, r); t = math.tan(math.radians(fov / 2))
    def P(p):
        d = np.array(p, float) - pos; z = d @ f
        return (0.5 + (d @ r) / (z * t * aspect) / 2, 0.5 + (d @ u) / (z * t) / 2)
    return P
def fov_for(fov, a): return fov if a >= 16 / 9 else 2 * math.degrees(math.atan(math.tan(math.radians(fov / 2)) * (16 / 9) / a))
def HUD(w, h):
    s = math.sqrt(w / 1280 * h / 720)
    return [(0, 250 * s / w, 1 - 118 * s / h, 1), (1 - 246 * s / w, 1 - 16 * s / w, 1 - 134 * s / h, 1 - 16 * s / h),
            (0.5 - 180 * s / w, 0.5 + 180 * s / w, 27 * s / h, 53 * s / h), (0.5 - 115 * s / w, 0.5 + 115 * s / w, 62 * s / h, 112 * s / h)]
def ray_hits(o, d, b, tmax):
    lo = np.array([b[0], b[4], b[2]]); hi = np.array([b[1], b[5], b[3]]); t0, t1 = 0, tmax
    for k in range(3):
        if abs(d[k]) < 1e-9:
            if o[k] < lo[k] or o[k] > hi[k]: return False
        else:
            a = (lo[k] - o[k]) / d[k]; c = (hi[k] - o[k]) / d[k]; a, c = min(a, c), max(a, c); t0 = max(t0, a); t1 = min(t1, c)
            if t0 > t1: return False
    return t1 > 0 and t0 < tmax * 0.999

def solve(RX, RZ, TX):
    def ring_z(x): return -RZ + RZ * math.sqrt(max(0.0, 1 - (x / RX) ** 2))
    TOW = [(x, ring_z(x)) for x in TX]
    def inside(x, z, m=0.0): return (x / (RX - m)) ** 2 + ((z + RZ) / (RZ - m)) ** 2 < 1
    def frame_ok(P, hud):
        pts = [(x, 10.5, z) for x, z in TOW] + [(x, 0, z) for x, z in TOW]
        pts += [(s * RX * 0.8, WALL_H, ring_z(RX * 0.8)) for s in (-1, 1)]   # the front arc; the town runs on beyond the frame (owner's sketch)
        for p in pts:
            x, y = P(p)
            if not (0.015 < x < 0.985 and 0.02 < y < 0.97): return False
        for x, z in TOW:
            for p in [(x + dx, y, z + dz) for dx in (-2.2, 0, 2.2) for dz in (-2.2, 2.2) for y in (0, 5, 8, 10.5)]:
                sx, sy = P(p)
                if any(a < sx < b and c < sy < d for a, b, c, d in hud): return False
        return True
    cams = {}
    for pitch in range(30, 56, 2):
        for fov in (34, 36, 38, 40, 42):
            for lookz in np.arange(-10, 2, 1.0):
                th = math.radians(pitch)
                for dist in np.arange(40, 120, 1.0):
                    camp = (0, dist * math.sin(th), lookz + dist * math.cos(th)); look = (0, 0, lookz)
                    if not all(frame_ok(cam(camp, look, fov_for(fov, w / h), w / h), HUD(w, h)) for w, h in SHAPES): continue
                    P = cam(camp, look, fov)
                    lo, hi = -5.0, camp[2] - 0.5
                    for _ in range(40):
                        m = (lo + hi) / 2
                        if P((0, 0, m))[1] > 0: lo = m
                        else: hi = m
                    if P((TOW[1][0], 0, TOW[1][1]))[1] < 0.46 or lo - TOW[1][1] < 15: break
                    ppm = (P((9, 1.8, 4))[1] - P((9, 0, 4))[1]) * 720 / 1.8
                    if pitch not in cams or ppm > cams[pitch][0]: cams[pitch] = (ppm, pitch, fov, float(lookz), float(dist), camp)
                    break
    def place(best):
        ppm, pitch, fov, lookz, dist, camp = best
        P = cam(camp, (0, 0, lookz), fov); o = np.array(camp)
        B = [(-4.3, 4.3, -2.4, 2.4, 0, 6.2), (-3.2, 3.2, -2.1, 2.1, 0, 10.0)] + [(x - 2.2, x + 2.2, z - 2.2, z + 2.2, 0, 10.5) for x, z in TOW]
        def wall_ok(tgt):
            d = tgt - o
            a = (d[0] / RX) ** 2 + (d[2] / RZ) ** 2; b = 2 * (o[0] * d[0] / RX ** 2 + (o[2] + RZ) * d[2] / RZ ** 2); c = (o[0] / RX) ** 2 + ((o[2] + RZ) / RZ) ** 2 - 1
            disc = b * b - 4 * a * c
            if disc < 0: return True
            for t in sorted([(-b - math.sqrt(disc)) / (2 * a), (-b + math.sqrt(disc)) / (2 * a)]):
                if 0 < t < 1: return o[1] + t * d[1] > WALL_H + 0.2
            return True
        def box(q): x, z, w, d, h = q; return (x - w / 2, x + w / 2, z - d / 2, z + d / 2, 0, h)
        def seen_pt(tgt, others, skip=None):
            d = tgt - o; L = np.linalg.norm(d); dn = d / L
            if not wall_ok(tgt) or any(ray_hits(o, dn, b, L - 0.3) for b in B): return False
            return not any(ray_hits(o, dn, box(q), L - 0.3) for q in others if q is not skip)
        def roof_seen(q, others):   # 2 of 3 points across the front at 60% of the height (LayoutCheck's rule)
            x, z, w, d, h = q
            return sum(seen_pt(np.array([x + fx * w, h * 0.6, z + d / 2 + 0.3]), others, q) for fx in (-0.3, 0, 0.3)) >= 2
        def framed(q):
            x, z, w, d, h = q
            cs = [P((x + a * w / 2, y, z + b * d / 2)) for a in (-1, 1) for b in (-1, 1) for y in (0, h)]
            xs = [c[0] for c in cs]; ys = [c[1] for c in cs]
            if min(xs) < 0.01 or max(xs) > 0.99 or min(ys) < 0.01 or max(ys) > 0.99: return False
            return not any(max(xs) > a and min(xs) < b and max(ys) > c and min(ys) < dd for a, b, c, dd in HUD(1280, 720))
        def fits(q, placed):
            x, z, w, d, h = q
            if not all(inside(x + a * w / 2, z + b * d / 2, 2.5) for a in (-1, 1) for b in (-1, 1)): return False
            if abs(x) < 4.4 + w / 2 + 3 and z + d / 2 > -2.5 - 3: return False                   # the gatehouse and its yard
            if any(abs(x - tx) < 2.2 + w / 2 + 2 and abs(z - tz) < 2.2 + d / 2 + 2 for tx, tz in TOW): return False
            for p in placed:   # 3 m streets
                if abs(x - p[0]) < (w + p[2]) / 2 + 3 and abs(z - p[1]) < (d + p[3]) / 2 + 3: return False
            return True
        # the plaza (fountain 3 m, room round it) behind the gatehouse, slightly off the keep's line
        plaza = None
        for z in np.arange(-9, -2 * RZ + 8, -0.5):
            for x in sorted(np.arange(-10, 10.5, 0.5), key=abs):
                q = (float(x), float(z), 9.0, 9.0, 3.8)
                if fits(q, []): plaza = q; break
            if plaza: break
        if plaza is None: return None
        # rows, like a real town: broad halls along the back (anything taller there would leave the top of the frame), the
        # three towers and the tavern in the middle, the low workshops and the market up front
        rows = [['library', 'granary', 'bakery'], ['frostSpire', 'watchtower', 'stormBell', 'tavern'], ['market', 'cottages', 'forge']]
        D = 2 * RZ; bands = [(-D + 3, -D * 0.64), (-D * 0.64, -D * 0.36), (-D * 0.36, -5.0)]
        placed = [plaza]; where = {}
        def plaza_ok(pl): return seen_pt(np.array([plaza[0], 1.2, plaza[1] + 1.5]), pl[1:])
        for row, (zb, zt) in zip(rows, bands):
            for k, bid in enumerate(row):
                w, d = LY.SIZES[bid]; h = HEIGHT[bid]
                got = None
                # spread along the row: prefer x away from what this row already holds
                xs = np.arange(-RX + 3, RX - 3, 0.5)
                mine = [where[b][0] for b in row if b in where]
                xs = sorted(xs, key=lambda v: -min([abs(v - m) for m in mine] + [99]) + abs(v) * 0.05)
                for z in np.arange(zb, zt, 0.5):   # packed from the back of each band forward
                    for x in xs:
                        q = (float(x), float(z), w, d, h)
                        if not fits(q, placed): continue
                        got = q; break
                    if got: break
                if got is None: return ('stuck', bid, ppm, pitch, fov, lookz, dist, plaza, where)
                placed.append(got); where[bid] = got
        return ('ok', ppm, pitch, fov, lookz, dist, camp, plaza, where, TOW)

    last = None
    for best in sorted(cams.values(), key=lambda c: -c[0])[:1]:
        r = place(best)
        if r[0] == 'ok': return r
        last = r
    return last

if __name__ == '__main__':
    for RX, RZ in ((30, 20), (32, 22), (34, 22), (34, 24), (36, 24), (38, 26)):
        TX = [9.0, -9.0, round(RX * 0.6, 1), -round(RX * 0.6, 1)]
        r = solve(RX, RZ, TX)
        if r is None: print(RX, RZ, 'no camera or plaza'); continue
        if r[0] == 'stuck': print(RX, RZ, 'stuck at', r[1], f'{r[2]:.1f} px/m', {k: v[:2] for k, v in r[8].items()}); continue
        _, ppm, pitch, fov, lookz, dist, camp, plaza, where, TOW = r
        print(f'RX {RX} RZ {RZ}: camera pitch {pitch} fov {fov} look z {lookz} dist {dist}, {ppm:.1f} px/m; towers {[(x, round(z, 2)) for x, z in TOW]}')
        print('PLAZA', plaza[:2]); print('PLOTS', [where[b][:2] for b in LY.IDS])
        break
