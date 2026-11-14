# Hearthgate 3D layout, the owner's sketch (Nov 13 2026 18:44): "Make it so that monsters are walking up to the towers on a
# route in front of each tower. Close off the loop around town to make a protected town." The town sits inside an
# elliptical ring wall; the front arc (toward the camera) carries the gatehouse in the middle and two towers each side;
# four lanes run straight up the screen from its bottom edge to the foot of each tower. This searches the camera, the
# ring size and the plot spots so that: the lanes, the towers, the gate and the whole ring are in the frame and clear of
# the HUD; every plot's roof line and the plaza are seen over the front wall, the keep and the towers; nothing overlaps.
# Coordinates: Unity x (screen left is +x, the camera looks south), z north toward the camera, y up.
import math, numpy as np, itertools, sys
W,H=1280,720
def cam(pos,look,fov,aspect=16/9):
    pos=np.array(pos,float); f=np.array(look,float)-pos; f/=np.linalg.norm(f)
    r=np.cross([0,1,0],f); r/=np.linalg.norm(r); u=np.cross(f,r)
    t=math.tan(math.radians(fov/2))
    def P(p):
        d=np.array(p,float)-pos; z=d@f; x=d@r; y=d@u
        return (0.5+x/(z*t*aspect)/2, 0.5+y/(z*t)/2)
    return P
def ray_hits(o,d,b,tmax):
    lo=np.array([b[0],b[4],b[2]]); hi=np.array([b[1],b[5],b[3]]); t0,t1=0,tmax
    for k in range(3):
        if abs(d[k])<1e-9:
            if o[k]<lo[k] or o[k]>hi[k]: return False
        else:
            a=(lo[k]-o[k])/d[k]; c=(hi[k]-o[k])/d[k]; a,c=min(a,c),max(a,c); t0=max(t0,a); t1=min(t1,c)
            if t0>t1: return False
    return t1>0 and t0<tmax*0.999

RX,RZ=25.0,16.0                     # ring half axes; the ring's centre is (0, -RZ) so its front point is the gate at (0, 0)
WALL_H=4.0                          # wall with merlons
TX=[17.0,8.5,-8.5,-17.0]            # towers along the front arc, screen left to right
def ring_z(x): return -RZ+RZ*math.sqrt(max(0.0,1-(x/RX)**2))
TOWERS=[(x,ring_z(x)) for x in TX]
def HUDFOR(w,h):
    sc=math.sqrt(w/1280*h/720)
    return [(0,250*sc/w,1-118*sc/h,1,'labels'),(1-246*sc/w,1-16*sc/w,1-134*sc/h,1-16*sc/h,'buttons'),(0.5-180*sc/w,0.5+180*sc/w,27*sc/h,53*sc/h,'gate bar')]
HUD=HUDFOR(1280,720)
SHAPES=[(1280,720),(1600,727),(1080,900)]
def fov_for(fov,aspect):
    if aspect>=16/9: return fov
    return 2*math.degrees(math.atan(math.tan(math.radians(fov/2))*(16/9)/aspect))

def inside(x,z,m=0.0): return (x/(RX-m))**2+((z+RZ)/(RZ-m))**2<1
def wall_ok(o,tgt):   # the sight line clears the ring wall where it enters the town
    d=tgt-o
    # solve ((ox+t dx)/RX)^2 + ((oz+RZ+t dz)/RZ)^2 = 1 for the crossing between camera and target
    a=(d[0]/RX)**2+(d[2]/RZ)**2; b=2*(o[0]*d[0]/RX**2+(o[2]+RZ)*d[2]/RZ**2); c=(o[0]/RX)**2+((o[2]+RZ)/RZ)**2-1
    disc=b*b-4*a*c
    if disc<0: return True
    for t in sorted([(-b-math.sqrt(disc))/(2*a),(-b+math.sqrt(disc))/(2*a)]):
        if 0<t<1:
            y=o[1]+t*d[1]
            return y>WALL_H+0.2
    return True
def blockers():
    B=[('gate',(-4.3,4.3,-2.4,2.4,0,6.2)),('keep',(-3.2,3.2,-2.1,2.1,0,10.0))]
    B+=[(f't{x:.0f}',(x-2.2,x+2.2,z-2.2,z+2.2,0,10.5)) for x,z in TOWERS]
    return B

def setup(pitch,fov,dist,lookz):
    th=math.radians(pitch); look=(0,0,lookz)
    camp=(0,dist*math.sin(th),lookz+dist*math.cos(th))
    return camp,look,cam(camp,look,fov)
def frame_ok(P,hud=None):
    hud=hud or HUD
    pts=[(x,10.5,z) for x,z in TOWERS]+[(x,0,z) for x,z in TOWERS]
    pts+=[(RX,WALL_H,-RZ),(-RX,WALL_H,-RZ),(0,WALL_H,-2*RZ),(RX*0.7,WALL_H,-RZ-RZ*0.71),(-RX*0.7,WALL_H,-RZ-RZ*0.71),(0,9.4,0)]
    for p in pts:
        x,y=P(p)
        if not(0.015<x<0.985 and 0.02<y<0.97): return False
    for x,z in TOWERS:   # whole towers with their tallest cap clear of the HUD
        for p in [(x+dx,y,z+dz) for dx in (-2.2,0,2.2) for dz in (-2.2,2.2) for y in (0,5,8,10.5)]:
            sx,sy=P(p)
            if any(a<sx<b and c<sy<d for a,b,c,d,n in hud): return False
    return True
def all_shapes_ok(pitch,fov,dist,lookz):
    th=math.radians(pitch); look=(0,0,lookz); camp=(0,dist*math.sin(th),lookz+dist*math.cos(th))
    for w,h in SHAPES:
        a=w/h
        if not frame_ok(cam(camp,look,fov_for(fov,a),a),HUDFOR(w,h)): return False
    return True
def lane_start(P,camp,look,fov):   # the ground z at the bottom edge of the frame (+1.5 m, so they walk in)
    lo,hi=-5.0,camp[2]-0.5
    for _ in range(50):
        m=(lo+hi)/2
        if P((0,0,m))[1]>0.0: lo=m
        else: hi=m
    return lo+1.5

best=None
for pitch in range(30,48,2):
    for fov in (32,34,36,38,40):
        for lookz in (-4,-3,-2,-1,0,1,2):
            for dist in np.arange(30,80,1.0):
                camp,look,P=setup(pitch,fov,dist,lookz)
                if not all_shapes_ok(pitch,fov,dist,lookz): continue
                ppm=(P((8.5,1.8,4))[1]-P((8.5,0,4))[1])*720/1.8
                zs=lane_start(P,camp,look,fov)
                # the owner's sketch: the wall sits in the upper part of the screen and the lanes fill the lower half
                if P((8.5,0,TOWERS[1][1]))[1]<0.46 or zs-TOWERS[1][1]<15.5: continue
                if best is None or ppm>best[0]: best=(ppm,pitch,fov,lookz,dist,camp,zs)
                break
ppm,pitch,fov,lookz,dist,camp,zs=best
print(f'camera: pitch {pitch} fov {fov} look (0,0,{lookz}) dist {dist} pos {tuple(round(v,2) for v in camp)}  {ppm:.1f} px/m at a lane; lanes start at z {zs:.1f}')
camp,look,P=setup(pitch,fov,dist,lookz); o=np.array(camp)
B=blockers()
def seen(x,z,h=3.6,r=2.3,others=()):
    vis=0
    for dx in (-r*0.6,0,r*0.6):
        tgt=np.array([x+dx,h,z+r+0.3]); d=tgt-o; L=np.linalg.norm(d); dn=d/L
        if wall_ok(o,tgt) and not any(ray_hits(o,dn,b,L-0.2) for n,b in B) and not any(ray_hits(o,dn,(q[0]-2.3,q[0]+2.3,q[1]-2.3,q[1]+2.3,0,q[2] if len(q)>2 else 6.0),L-0.2) for q in others): vis+=1
    return vis>=2
def framed(x,z,r=2.3,h=6):
    cs=[P((x+a,y,z+b)) for a in (-r,r) for b in (-r,r) for y in (0,h)]
    xs=[c[0] for c in cs]; ys=[c[1] for c in cs]
    if min(xs)<0.01 or max(xs)>0.99 or min(ys)<0.01 or max(ys)>0.99: return False
    return not any(max(xs)>a and min(xs)<b and max(ys)>c and min(ys)<d for a,b,c,d,n in HUD)
def clear_of_structures(x,z,r=2.4):
    if any(max(abs(x-tx),abs(z-tz))<2.3+r+0.4 for tx,tz in TOWERS): return False
    return not (abs(x)<4.4+r+0.4 and z>-2.5-r-0.4)
cands=[(x,z) for z in np.arange(-4.5,-2*RZ+3,-0.5) for x in np.arange(RX-3,-RX+3,-0.5) if inside(x,z,3.2) and clear_of_structures(x,z) and seen(x,z) and framed(x,z)]
print(len(cands),'candidate spots')
pl=[(x,z) for z in np.arange(-6,-20,-0.5) for x in np.arange(-8,8.5,0.5) if inside(x,z,4) and clear_of_structures(x,z,3.0) and seen(x,z,1.2,3.0) and framed(x,z,3.0,3.8)]
pl.sort(key=lambda p:(abs(p[0])*0.5+abs(p[1]+9)))
plaza=pl[0]; print('plaza',plaza)
cands.sort(key=lambda p:abs(p[1]+8)+abs(p[0])*0.12)
chosen=[]
for c in cands:
    if max(abs(c[0]-plaza[0]),abs(c[1]-plaza[1]))<3.0+2.4+0.5: continue
    if not all(max(abs(c[0]-q[0]),abs(c[1]-q[1]))>=5.0 for q in chosen): continue
    if not seen(*c,others=chosen): continue
    if not all(seen(*q,others=[c]+[r for r in chosen if r!=q]) for q in chosen): continue
    chosen.append(c)
    if len(chosen)==10: break
print('chosen',len(chosen))
IDS=['forge','frostSpire','stormBell','bakery','market','tavern','granary','library','cottages','watchtower']
TALL={'frostSpire':7.4,'stormBell':8.1,'watchtower':7.6}
order=sorted(chosen,key=lambda c:sum(not seen(q[0],q[1],others=[(c[0],c[1],8.1)]) for q in chosen if q!=c))
assign=dict(zip(TALL,order[:3])); rest=[c for c in chosen if c not in order[:3]]
for b in IDS:
    if b not in assign: assign[b]=rest.pop(0)
final=[(assign[b][0],assign[b][1],TALL.get(b,6.0)) for b in IDS]
hidden=[IDS[i] for i,q in enumerate(final) if not seen(q[0],q[1],others=[r for r in final if r is not q])]
print('PLOTS',[(float(a),float(b)) for a,b,_ in final]); print('hidden after assignment:',hidden or 'none')
print('TOWERS',[(x,round(z,2)) for x,z in TOWERS])
print('tower screen x', [round(P((x,0,z))[0],3) for x,z in TOWERS], 'y', [round(P((x,0,z))[1],3) for x,z in TOWERS])
print('ring back y', round(P((0,WALL_H,-2*RZ))[1],3), 'sides x', round(P((RX,0,-RZ))[0],3), round(P((-RX,0,-RZ))[0],3))
