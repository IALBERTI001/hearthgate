# The build chain on the owner's PC (run with Blender's bundled python): kit test, model export, then the Unity WebGL
# build (which runs LayoutCheck first). The kit and its test run as N Blenders side by side (HG_PART=k/N).
import subprocess, os, sys
B = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
U = B + r"\unity\Hearthgate3D"; BL = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
N = int(os.environ.get('HG_PARTS', '4')); SKIP_TEST = '--fast' in sys.argv; UNITY_ONLY = '--unity' in sys.argv   # --unity: code changes only
def blend_all(args, log):
    ps = []
    for k in range(N):
        env = dict(os.environ); env['HG_PART'] = f'{k}/{N}'
        ps.append((subprocess.Popen([BL, "-b", "--factory-startup", "--python-exit-code", "1"] + args, stdout=open(f'{log}-{k}.txt', 'w'), stderr=subprocess.STDOUT, env=env, creationflags=0x08000000), f'{log}-{k}.txt'))
    ok = True
    for p, lg in ps:
        ok = (p.wait() == 0) and ok
        ok = ok and "Traceback" not in open(lg, encoding="utf-8", errors="replace").read()
    with open(log + '.txt', 'w', encoding='utf-8') as out:
        for _, lg in ps: out.write(open(lg, encoding='utf-8', errors='replace').read())
    return ok
# the 2D game's sound, shared: effects go into Resources (they load with the game, about 150 KB), music and ambience
# into StreamingAssets (fetched when first played, so they stay out of the first download)
import shutil
for src, dst in ((B + r"\assets\sfx", U + r"\Assets\Resources\Sfx"), (B + r"\assets\music", U + r"\Assets\StreamingAssets\Music"), (B + r"\assets\ambience", U + r"\Assets\StreamingAssets\Music"), (B + r"\assets\voice", U + r"\Assets\StreamingAssets\Voice")):   # the owner's recorded guide lines
    os.makedirs(dst, exist_ok=True)
    for f in os.listdir(src):
        if f.endswith('.mp3') and (not os.path.exists(os.path.join(dst, f)) or os.path.getsize(os.path.join(dst, f)) != os.path.getsize(os.path.join(src, f))):
            shutil.copyfile(os.path.join(src, f), os.path.join(dst, f))
ok = True
if UNITY_ONLY: pass
elif not SKIP_TEST: ok = blend_all(["-P", B + r"\tools\blender\hg_kit_test.py"], B + r"\log-kittest")
ok = ok and (UNITY_ONLY or blend_all(["-P", B + r"\tools\blender\hg_kit.py", "--", U + r"\Assets\Art\Models"], B + r"\log-kit"))
if ok and not UNITY_ONLY:   # the HUD icons, rendered from the models just exported
    r = subprocess.run([BL, "-b", "--factory-startup", "--python-exit-code", "1", "-P", B + r"\tools\blender\hg_icons.py", "--", U + r"\Assets\Art\Models", U + r"\Assets\Resources\Icons"],
                       stdout=open(B + r"\log-icons.txt", "w"), stderr=subprocess.STDOUT, creationflags=0x08000000)
    ok = r.returncode == 0
if ok:
    subprocess.run([r"C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe", "-batchmode", "-quit", "-projectPath", U, "-buildTarget", "WebGL",
                    "-executeMethod", "Hearthgate.EditorTools.HearthgateSetup.BuildWebGL", "-logFile", B + r"\log-build.txt"], creationflags=0x08000000)
open(B + r"\chain-done.txt", "w").write("ok" if ok else "blender failed")
