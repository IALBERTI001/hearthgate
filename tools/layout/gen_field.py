# Writes the layout numbers from tools/blender/hg_layout.py into Assets/Scripts/Game/Field.cs (the ring, towers, lanes,
# plaza, plots, sizes, heights and the wall camera), so the game and the Blender kit never disagree. Run after any change
# to hg_layout.py: python3 tools/layout/gen_field.py
import os, re, sys, math
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
sys.path.insert(0, os.path.join(ROOT, 'tools', 'blender'))
import hg_layout as L
p = os.path.join(ROOT, 'unity', 'Hearthgate3D', 'Assets', 'Scripts', 'Game', 'Field.cs'); s = open(p).read()
f = lambda v: f'{v:.3f}f'
V = lambda a, b: f'new Vector2({f(a)}, {f(b)})'
def sub(pat, new):
    global s
    s2 = re.sub(pat, lambda m: new, s, count=1, flags=re.S)
    assert s2 != s or new in s, pat; s = s2
sub(r'public const float RX = [^;]*;', f'public const float RX = {L.RX:g}f, RZ = {L.RZ:g}f;')
sub(r'static readonly Vector2\[\] TOWERS = \{.*?\};', 'static readonly Vector2[] TOWERS = { ' + ', '.join(V(x, z) for x, z in L.TOWERS) + ' };')
sub(r'static readonly Vector2\[\] FEET = \{.*?\};', 'static readonly Vector2[] FEET = { ' + ', '.join(V(*a) for a, b in L.LANES) + ' };')
sub(r'static readonly Vector2\[\] STARTS = \{.*?\};', 'static readonly Vector2[] STARTS = { ' + ', '.join(V(*b) for a, b in L.LANES) + ' };')
sub(r'public static readonly Vector3 PLAZA = new Vector3\([^)]*\);', f'public static readonly Vector3 PLAZA = new Vector3({f(L.PLAZA[0])}, 0, {f(L.PLAZA[1])});')
pl = [V(x, z) for x, z in L.PLOTS]
sub(r'static readonly Vector2\[\] PLOTS = \{.*?\};', 'static readonly Vector2[] PLOTS = {\n            ' + ', '.join(pl[:5]) + ',\n            ' + ', '.join(pl[5:]) + ' };')
sub(r'static readonly Vector2\[\] SIZES = \{.*?\};', 'static readonly Vector2[] SIZES = { ' + ', '.join(V(*L.SIZES[b]) for b in L.IDS) + ' };')
sub(r'static readonly float\[\] HEIGHTS = \{.*?\};', 'static readonly float[] HEIGHTS = { ' + ', '.join(f(L.HEIGHT[b]) for b in L.IDS) + ' };')
sub(r'public static readonly Vector3 CAM_LOOK = new Vector3\([^)]*\);', f'public static readonly Vector3 CAM_LOOK = new Vector3({f(L.CAM_LOOK[0])}, 0, {f(L.CAM_LOOK[2])});')
th = math.radians(L.CAM_PITCH)
sub(r'public static readonly Vector3 CAM_POS = [^;]*;[^\n]*', f'public static readonly Vector3 CAM_POS = CAM_LOOK + new Vector3(0, {f(L.CAM_DIST * math.sin(th))}, {f(L.CAM_DIST * math.cos(th))});   // {L.CAM_DIST:g} m away, {L.CAM_PITCH:g} degrees down')
sub(r'public const float CAM_FOV = [^,]*,', f'public const float CAM_FOV = {L.CAM_FOV:g}f,')
open(p, 'w').write(s); print('Field.cs written from hg_layout.py')
