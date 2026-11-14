# Hearthgate 3D: the game's view rendered in Blender, for fast art reviews between Unity builds (Nov 13 2026).
# Imports the exported models (Assets/Art/Models/*.fbx) and places them where the game does (numbers from
# Assets/Scripts/Game/Field.cs), then renders from the game camera at 1280x720. The QA loop reviews these frames, then
# the real WebGL build. Usage: blender -b -P hg_scene.py -- <models dir> <out.png> [hud] [state]
#   hud: 1 draws the HUD's boxes over a second copy (<out>-hud.png) for the layout pass; never in review frames.
#   state: 'mid' (some buildings rebuilt, three elements, a wave on the road) or 'fresh' (all ruins, stone tops).
import bpy, sys, os, math, glob
from mathutils import Vector, Euler

a = sys.argv[sys.argv.index('--') + 1:]
MODELS, OUT = a[0], a[1]
HUD = len(a) > 2 and a[2] == '1'
STATE = a[3] if len(a) > 3 else 'mid'

# ---- the layout (hg_layout.py, mirrored by Field.cs); Unity (x, z) -> Blender (x, y)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import hg_layout as L
TOWER_TOP = L.TOWER_TOP; PLAZA = L.PLAZA; PLOTS = L.PLOTS; IDS = L.IDS
CAM_LOOK = Vector((L.CAM_LOOK[0], L.CAM_LOOK[2], L.CAM_LOOK[1])); CAM_POS = Vector((L.CAM_POS[0], L.CAM_POS[2], L.CAM_POS[1])); CAM_FOV = L.CAM_FOV
def lane_at(k, prog, lat):   # Field.Lane
    (fx, fz), (sx, sz) = L.LANES[k]; d = Vector((sx - fx, sz - fz)).normalized()
    return (fx + d.x * prog - d.y * lat, fz + d.y * prog + d.x * lat)
def facing(k):   # Field.Facing, as a Unity yaw in degrees
    (fx, fz), (sx, sz) = L.LANES[k]; return math.degrees(math.atan2(sx - fx, sz - fz))

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

def place(name, loc, yaw_deg=0.0, scale=1.0):
    path = os.path.join(MODELS, name + '.fbx')
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.parent is None:
            # the export turned models 180 degrees for Unity; the importer undoes the axes, so turn them back here
            o.rotation_euler.rotate(Euler((0, 0, math.pi + math.radians(-yaw_deg))))
            o.location = Vector(loc); o.scale = o.scale * scale   # keep whatever unit scale the importer set
    return new

place('ground', (0, 0, 0)); place('road', (0, 0, 0)); place('wall', (0, 0, 0)); place('gate', (0, 0, 0))
place('fountain', (PLAZA[0], PLAZA[1], 0))
els = ['fire', 'frost', 'storm', 'fire'] if STATE == 'mid' else ['stone'] * 4
for (x, z), el in zip(L.TOWERS, els):
    place('tower_base', (x, z, 0)); place('top_' + el, (x, z, TOWER_TOP))
built = {'forge', 'frostSpire', 'stormBell', 'bakery', 'cottages'} if STATE == 'mid' else set()
for (x, y), bid in zip(PLOTS, IDS):
    place('building_' + bid if bid in built else 'ruin_' + bid, (x, y, 0), 180)   # Field.PLOT_FACING: fronts to the camera
if os.path.exists(os.path.join(MODELS, 'wizard.fbx')):   # the wizard in the square, facing the walls
    place('wizard', (-6.0, -8.0, 0), 180, 0.66)   # Wizard.HOME, Wizard.SCALE
if STATE == 'mid':
    # a wave on the lanes: slimes walking up to the towers, a few gathered at a tower's foot, a beetle and a golem
    import random as _r; rr = _r.Random(5)
    for k in range(4):
        for prog in (4.0 + rr.uniform(0, 2), 9.0 + rr.uniform(0, 3), 14.0 + rr.uniform(0, 2)):
            x, y = lane_at(k, prog, rr.uniform(-1.3, 1.3)); place('slime', (x, y, 0), facing(k) + rr.choice((-1, 1)) * rr.uniform(14, 30), 1.47)
    for lat in (-1.6, 0.4, 2.0):
        x, y = lane_at(0, 1.4 + abs(lat) * 0.4, lat); place('slime', (x, y, 0), facing(0), 1.47)
    x, y = lane_at(1, 7.0, 0.3); place('beetle', (x, y, 0), facing(1), 1.1)
    x, y = lane_at(3, 17.0, -0.4); place('golem', (x, y, 0), facing(3), 1.2)

# ---- light like the game: a warm sun from the camera's front right (world -x, +y, high), a soft blue sky
sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 3.2; sun.angle = math.radians(8); sun.color = (1.0, 0.93, 0.82)
so = bpy.data.objects.new('sun', sun); sc.collection.objects.link(so)
d = Vector((-0.45, -0.4, -0.8)).normalized()                   # the direction the light travels (Blender axes): from the upper left of the screen
so.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
w = bpy.data.worlds.new('sky'); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.55, 0.7, 0.92, 1); bg.inputs['Strength'].default_value = 0.9

cam = bpy.data.cameras.new('cam'); cam.sensor_fit = 'VERTICAL'; cam.angle_y = math.radians(CAM_FOV); cam.clip_end = 300
co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
co.location = CAM_POS; co.rotation_euler = (CAM_LOOK - CAM_POS).to_track_quat('-Z', 'Y').to_euler()
if os.environ.get('HG_CAM'):   # a close-up for inspection: "x,y,z,lookx,looky,lookz,fov" in Blender axes
    v = [float(q) for q in os.environ['HG_CAM'].split(',')]
    co.location = Vector(v[:3]); co.rotation_euler = (Vector(v[3:6]) - Vector(v[:3])).to_track_quat('-Z', 'Y').to_euler(); cam.angle_y = math.radians(v[6])
if os.environ.get('HG_TOP'):   # a plan view of the whole layout, for finding stray geometry (north up)
    cam.type = 'ORTHO'; cam.ortho_scale = 110; co.location = (0, -16, 90); co.rotation_euler = (0, 0, math.pi)

eng = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in eng else 'BLENDER_EEVEE_NEXT'
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
try:
    sc.view_settings.view_transform = 'AgX'; sc.view_settings.look = 'AgX - Punchy'
except Exception:
    sc.view_settings.view_transform = 'Filmic'
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print('[hg_scene] wrote', OUT)

if HUD:   # the HUD's fixed boxes (Hud.cs at 1280x720), drawn over a copy for the layout pass only
    import numpy as np
    img = bpy.data.images.load(OUT); W, H = img.size
    px = np.array(img.pixels[:]).reshape(H, W, 4)
    def box(x0, y0, x1, y1, col):   # pixel box from the top-left
        r0, r1 = H - y1, H - y0
        px[r0:r1, x0:x1, :3] = px[r0:r1, x0:x1, :3] * 0.5 + np.array(col) * 0.5
    box(0, 0, 300, 140, (1, 0, 0)); box(W - 324, 24, W - 24, 172, (1, 0, 0)); box(W // 2 - 180, H - 49, W // 2 + 180, H - 31, (1, 0, 0))
    img.pixels[:] = px.ravel(); img.filepath_raw = OUT.replace('.png', '-hud.png'); img.file_format = 'PNG'; img.save()
    print('[hg_scene] wrote', img.filepath_raw)
