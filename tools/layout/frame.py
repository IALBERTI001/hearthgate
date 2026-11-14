# Hearthgate 3D layout search (Nov 13 2026). Finds where the camera, the plaza and the ten building plots go so that:
# every plot's roof line and the plaza are seen over the wall, the gatehouse and the towers; nothing overlaps; all of it
# is inside the 16:9 frame and clear of the HUD. The winning numbers are copied into Assets/Scripts/Game/Field.cs and
# Editor/LayoutCheck.cs checks the same rules on the real models. Run: python3 tools/layout/frame.py
import math, numpy as np, itertools
import math, numpy as np, itertools
W,H=1280,720
def cam(pos,look,fov,aspect=16/9):
    pos=np.array(pos,float); f=np.array(look,float)-pos; f/=np.linalg.norm(f)
    r=np.cross([0,1,0],f); r/=np.linalg.norm(r); u=np.cross(f,r)
    t=math.tan(math.radians(fov/2))
    def P(p):
        d=np.array(p,float)-pos; z=d@f; x=d@r; y=d@u   # right = cross(up, forward), as Unity
        return (0.5+x/(z*t*aspect)/2, 0.5+y/(z*t)/2)
    return P

ROADZ=5.4; SPAWN=27
TOWERS=[-7.5,-13,-18.5,-24]
def boxes(plots,plaza):
    B=[('gate',(-4.2,4.2,-2.3,2.3,0,6.2)),('gate_keep',(-3.1,3.1,-2.0,2.0,0,9.4))]   # gatehouse; keep with its roof eaves
    B+= [('wall',(-31,16,-0.9,0.9,0,4.0))]
    B+= [(f'tower{x}',(x-2.0,x+2.0,-2.0,2.0,0,8.8)) for x in TOWERS]   # 5 m tower + the tallest element cap
    B+= [(f'plot{i}',(x-2.3,x+2.3,z-2.3,z+2.3,0,6.0)) for i,(x,z) in enumerate(plots)]
    B+= [('fountain',(plaza[0]-3.0,plaza[0]+3.0,plaza[1]-3.0,plaza[1]+3.0,0,3.8))]
    return B
def ray_hits(o,d,b,tmax):
    lo=np.array([b[0],b[4],b[2]]); hi=np.array([b[1],b[5],b[3]])
    t0,t1=0,tmax
    for k in range(3):
        if abs(d[k])<1e-9:
            if o[k]<lo[k] or o[k]>hi[k]: return False
        else:
            a=(lo[k]-o[k])/d[k]; c=(hi[k]-o[k])/d[k]
            a,c=min(a,c),max(a,c); t0=max(t0,a); t1=min(t1,c)
            if t0>t1: return False
    return t1>0 and t0<tmax*0.999
def check(camp,look,fov,plots,plaza,verbose=False):
    P=cam(camp,look,fov); probs=[]
    B=boxes(plots,plaza)
    HUD=[(0,0.235,0.8,1,'labels'),(0.747,0.981,0.76,0.967,'buttons'),(0.36,0.64,0.043,0.068,'gatebar')]
    for n,b in B:
        if n=='wall': continue
        cs=[P((x,y,z)) for x in b[:2] for z in b[2:4] for y in b[4:]]
        xs=[c[0] for c in cs]; ys=[c[1] for c in cs]
        if min(xs)<0.005 or max(xs)>0.995 or min(ys)<0.005 or max(ys)>0.995: probs.append(f'{n} off {min(xs):.2f}-{max(xs):.2f} {min(ys):.2f}-{max(ys):.2f}')
        for a,b2,c,d,h in HUD:
            if max(xs)>a and min(xs)<b2 and max(ys)>c and min(ys)<d: probs.append(f'{n} under {h}')
    for (n1,b1),(n2,b2) in itertools.combinations(B,2):
        if 'wall' in (n1,n2) and not (n1.startswith('plot') or n2.startswith('plot') or 'fountain' in (n1,n2)): continue
        if {n1,n2}=={'gate','gate_keep'}: continue
        if b1[0]+.05<b2[1] and b2[0]+.05<b1[1] and b1[2]+.05<b2[3] and b2[2]+.05<b1[3]: probs.append(f'overlap {n1} {n2}')
    # every plot: its roof centre must be seen (sight line clear of gate, wall, towers, other plots)
    o=np.array(camp,float)
    for i,(x,z) in enumerate(plots+[plaza]):
        tgt=np.array([x,3.0 if i<len(plots) else 1.2,z]); d=tgt-o; L=np.linalg.norm(d); d/=L
        for n,b in B:
            if n in (f'plot{i}',) or (i==len(plots) and n=='fountain'): continue
            if ray_hits(o,d,b,L-0.3): probs.append(f'{"plot%d"%i if i<len(plots) else "plaza"} hidden by {n}'); break
    # road: enemies along it in view
    for p in (1,8,16,SPAWN):
        for lat in (-2.3,2.3):
            x,y=P((-p,0.9,ROADZ+lat))
            if not(0.01<x<0.99 and 0.01<y<0.99): probs.append(f'road off at {p},{lat}: {x:.2f},{y:.2f}')
    ppm=(P((-10,1.8,ROADZ))[1]-P((-10,0,ROADZ))[1])*720/1.8
    return probs,ppm
th=math.radians(40); lx=-7.5; lz=-3.5; dist=46
camp=(lx,dist*math.sin(th),lz+dist*math.cos(th)); look=(lx,0,lz); fov=38
P=cam(camp,look,fov); o=np.array(camp)
fixed=boxes([], (100,100))[:-1]   # gate, keep, wall, towers
HUD=[(0,0.235,0.8,1),(0.747,0.981,0.76,0.967),(0.36,0.64,0.043,0.068)]
def seen(x,z,h=3.6,r=2.3):
    # the roof line: 3 points across the front at 60% of the height (as Editor/LayoutCheck tests them); at least 2 of
    # them seen past the gatehouse, the keep, the wall and the towers
    vis=0
    for dx in (-r*0.6,0,r*0.6):
        tgt=np.array([x+dx,h,z+r+0.3]); d=tgt-o; L=np.linalg.norm(d); d/=L
        if not any(ray_hits(o,d,b,L-0.2) for n,b in fixed): vis+=1
    return vis>=2
def framed(x,z,r=2.3,h=6):
    cs=[P((x+a,y,z+b)) for a in (-r,r) for b in (-r,r) for y in (0,h)]
    xs=[c[0] for c in cs]; ys=[c[1] for c in cs]
    if min(xs)<0.01 or max(xs)>0.99 or min(ys)<0.01 or max(ys)>0.99: return False
    return not any(max(xs)>a and min(xs)<b and max(ys)>c and min(ys)<d for a,b,c,d in HUD)
cands=[]
for z in np.arange(-5.0,-22,-0.5):
    for x in np.arange(17,-25.5,-0.5):   # the town stays a cluster inside the walls, not strung out to the frame's edges
        if seen(x,z) and framed(x,z): cands.append((x,z))
print(len(cands),'candidate spots')
# plaza first: as close behind the gate as possible and seen (low object, 1.2 m)
pl=[(x,z) for z in np.arange(-5,-16,-0.5) for x in np.arange(-6,14,0.5) if seen(x,z,1.2,3.0) and framed(x,z,3.0,3.8)]
pl.sort(key=lambda p:(abs(p[0])+abs(p[1])))
plaza=pl[0]; print('plaza',plaza)
# greedy: rank spots by screen size (nearer = bigger), keep 5.6 m apart from each other and 5.8 from the plaza
cands.sort(key=lambda p:(-p[1]*0.0 + abs(p[1]+6)*1.0 + abs(p[0]+5)*0.15))
chosen=[]
def pbox(x,z,h=6.0): return (x-2.3,x+2.3,z-2.3,z+2.3,0,h)
def seen_by(x,z,others):   # roof line clear of the fixed structures and of the plots already chosen
    if not seen(x,z): return False
    vis=0
    for dx in (-1.38,0,1.38):
        tgt=np.array([x+dx,3.6,z+2.6]); d=tgt-o; L=np.linalg.norm(d); d/=L
        if not any(ray_hits(o,d,pbox(*q[:2],q[2] if len(q)>2 else 6.0),L-0.2) for q in others): vis+=1
    return vis>=2
for c in cands:
    if math.dist(c,plaza)<6.6: continue
    if not all(max(abs(c[0]-q[0]),abs(c[1]-q[1]))>=5.0 for q in chosen): continue   # square footprints 0.4 m apart
    if not seen_by(*c,chosen): continue
    if not all(seen_by(*q,[c]+[r for r in chosen if r!=q]) for q in chosen): continue
    chosen.append(c)
    if len(chosen)==10: break
chosen.sort(key=lambda p:(-p[1],p[0]))
print('plots',[(float(a),float(b)) for a,b in chosen])
print('frame/HUD/overlap check:',[p for p in check(camp,look,fov,chosen,plaza)[0] if 'hidden' not in p] or 'clean')

# the three tall buildings (frost spire 7.4 m, storm bell 8.1 m, watchtower 7.6 m) take the spots nothing stands behind
TALL=('frostSpire','stormBell','watchtower')
IDS=['forge','frostSpire','stormBell','bakery','market','tavern','granary','library','cottages','watchtower']
def hides(a,b,h):   # does a building of height h at spot a hide spot b's roof line?
    return not seen_by(b[0],b[1],[(a[0],a[1],h)])
order=sorted(chosen,key=lambda c:sum(hides(c,q,8.1) for q in chosen if q!=c))   # spots that hide the fewest go first
tall_spots=order[:3]; rest=[c for c in chosen if c not in tall_spots]
assign={}
for bid,spot in zip(TALL,tall_spots): assign[bid]=spot
for bid in IDS:
    if bid not in assign: assign[bid]=rest.pop(0)
H={'frostSpire':7.4,'stormBell':8.1,'watchtower':7.6}
final=[(assign[b][0],assign[b][1],H.get(b,6.0)) for b in IDS]
problems=[f'{IDS[i]} hidden' for i,q in enumerate(final) if not seen_by(q[0],q[1],[r for r in final if r is not q])]
print('PLOTS in HG.BUILDINGS order:',[(float(a),float(b)) for a,b,_ in final])
print('tall-building check:',problems or 'all roof lines seen')
