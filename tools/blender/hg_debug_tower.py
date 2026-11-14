# debug: build the tower without exporting and report where the masonry stones on the camera-facing face are
import bpy, sys, os, math
sys.argv = [sys.argv[0], '--', os.environ.get('TEMP', '.')]
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hg_kit as K
sys.modules['hg_kit'] = K
import hg_front as F
out = []
K.export = lambda name: out.append(name) or 0
F.K.export = K.export
K.clear(); F.tower_base()
blks = [o for o in bpy.data.objects if o.name.startswith('blk')]
front = [o for o in blks if o.location.y > 1.5 and abs(o.location.x) < 0.6 and 1.0 < o.location.z < 4.4]
print('[dbg] blocks', len(blks), 'front', len(front))
for o in front[:6]:
    print('[dbg]', tuple(round(v, 2) for v in o.location), 'rotZ', round(math.degrees(o.rotation_euler.z), 1), 'dims', tuple(round(v, 2) for v in o.dimensions))
sample = sorted(blks, key=lambda o: -o.location.y)[:4]
for o in sample: print('[dbg] max-y', tuple(round(v, 2) for v in o.location), 'rotZ', round(math.degrees(o.rotation_euler.z), 1), 'dims', tuple(round(v, 2) for v in o.dimensions))
