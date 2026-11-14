# Kit test (Nov 13 2026): builds every model without exporting and fails when a part's origin is far from the part
# (the transform_apply bug that sent tower stones to the wrong faces and roof battens across the gatehouse), or when the
# tower's camera-facing face has no stones. Run: blender -b --factory-startup --python-exit-code 1 -P hg_kit_test.py
# Also runs one real export and fails if it moves the model. (exit 1 on failure; without --python-exit-code Blender exits 0 even when the script raises)
import bpy, sys, os, math
sys.argv = [sys.argv[0], '--', os.environ.get('TEMP', '/tmp')]
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hg_kit as K
sys.modules['hg_kit'] = K
import hg_front as F, hg_city as C
from mathutils import Vector

bad = []
def check(name):
    for o in bpy.data.objects:
        if o.type != 'MESH' or not o.data.vertices: continue
        if all(abs(a) < 1e-6 for a in o.rotation_euler): continue   # only rotated parts can swing round a far origin
        c = sum((v.co for v in o.data.vertices), Vector()) / len(o.data.vertices)   # mesh centre in local space
        size = max(o.dimensions) or 1
        if c.length > max(1.0, size):   # a rotated part whose geometry sits far from its own origin
            bad.append(f'{name}: {o.name} is rotated about a far origin (mesh centre {tuple(round(v, 2) for v in c)})'); break
# the real export must keep every model where it was built (only turned 180 degrees): a rotated, offset first part caught
# the join keeping its rotation (Nov 13 2026, the ring wall came out mirrored); export raises if the model moves
real_export = K.export
K.clear(); F.block((6.0, -9.0, 1.0), (2.0, 1.0, 1.0), F.M('stone'), 0.0, 2.6); F.block((-3.0, -14.0, 0.5), (1.0, 1.0, 1.0), F.M('stone'), 0.0)
try: real_export('_export_test')
except Exception as ex: bad.append(f'export: {ex}')
K.export = lambda name: 0
F.K.export = K.export; C.K.export = K.export
def tower_front():
    blks = [o for o in bpy.data.objects if o.name.startswith('blk')]
    front = [o for o in blks if o.matrix_world.translation.y > 1.5 and abs(o.matrix_world.translation.x) < 0.6 and 1.0 < o.matrix_world.translation.z < 4.4]
    if len(front) < 6: bad.append(f'tower_base: only {len(front)} stones on the face toward the camera')
for fn, name, extra in ((F.gate, 'gate', None), (F.wall, 'wall', None), (F.tower_base, 'tower_base', tower_front), (F.road, 'road', None),
                        (F.ground, 'ground', None), (F.fountain, 'fountain', None), (F.slime, 'slime', None), (F.beetle, 'beetle', None),
                        (F.golem, 'golem', None), (C.ruin, 'ruin', None), (F.wizard, 'wizard', None)):
    K.clear(); fn(); check(name)
    if extra: extra()
for k in ('stone', 'fire', 'frost', 'storm'): K.clear(); F.top(k); check('top_' + k)
for bid in C.BUILDERS: K.clear(); C.BUILDERS[bid](); check('building_' + bid)
for bid in C.SHELLS: K.clear(); C.ruin_of(bid); check('ruin_' + bid)
K.clear(); F.marker(); check('marker')
for b in bad: print('[kit-test] FAIL', b)
print('[kit-test]', 'passed' if not bad else f'FAILED ({len(bad)})')
sys.exit(1 if bad else 0)
