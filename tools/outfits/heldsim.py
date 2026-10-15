# Mo phong vat cam tay: nhan vat (Idle + lop tu the) + vat pham gan vao xuong ban tay.
import sys, math, json
import numpy as np
sys.path.insert(0, '.')
import glbview as g
from posesim import apply, axis_rot

def obj_tris(path, color):
    V=[]; T=[]
    for l in open(path):
        if l.startswith('v '): V.append([float(x) for x in l.split()[1:4]])
        elif l.startswith('f '):
            idx=[int(p.split('/')[0])-1 for p in l.split()[1:]]
            for k in range(1,len(idx)-1): T.append((idx[0],idx[k],idx[k+1]))
    V=np.array(V); return [(V[a],V[b],V[c],color) for a,b,c in T]

def box(size, pos, color):
    sx,sy,sz=[s/2 for s in size]; p=np.array(pos)
    c=[np.array([x,y,z])+p for x in(-sx,sx) for y in(-sy,sy) for z in(-sz,sz)]
    F=[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]
    out=[]
    for a,b,cc,d in F: out+= [(c[a],c[b],c[cc],color),(c[a],c[cc],c[d],color)]
    return out

def cyl(r, h, pos, color, n=8):
    p=np.array(pos); out=[]
    for i in range(n):
        a0=2*math.pi*i/n; a1=2*math.pi*(i+1)/n
        v=lambda a,y: p+np.array([r*math.cos(a), y, r*math.sin(a)])
        out+=[(v(a0,-h/2),v(a1,-h/2),v(a1,h/2),color),(v(a0,-h/2),v(a1,h/2),v(a0,h/2),color)]
    return out

GT='/Users/quan/Desktop/HiepSiVeVuon/assets3d/garden_tools/'
def item(name):
    wood=(0.42,0.29,0.16); steel=(0.75,0.77,0.8)
    if name=='sword':
        return box((0.55,8.5,0.18),(0,4.6,0),steel)+box((2.1,0.35,0.4),(0,0.2,0),(0.55,0.45,0.2))+cyl(0.32,1.8,(0,-0.7,0),(0.32,0.2,0.12))
    if name in('hoe','xeng','cao_co'):
        return obj_tris(GT+name+'_handle.obj',wood)+obj_tris(GT+name+'_head.obj',steel)
    if name=='can':
        return cyl(1.2,2.6,(0,1.3,0),(0.42,0.55,0.4))+box((1.6,0.18,0.18),(0,3.95,-0.35),(0.42,0.55,0.4))+box((0.25,0.25,2.2),(0,2.3,1.2),(0.42,0.55,0.4))
    raise SystemExit('item?')

def euler_yxz(deg):
    x,y,z=[math.radians(v) for v in deg]
    # Godot RotationDegrees: thu tu YXZ  (R = Ry * Rx * Rz)
    return axis_rot([0,1,0],math.degrees(y)) @ axis_rot([1,0,0],math.degrees(x)) @ axis_rot([0,0,1],math.degrees(z))

def render_case(model, anim, mods, bone, item_name, grip_pos, grip_rot_deg, scale_model, out, label, item_offset=(0,0,0), extra_bone_mods=None):
    d,b=g.load(model)
    pose=g.sample_pose(d,b,anim)
    apply(d,pose,mods)
    W=g.world_mats(d,pose)
    orig=g.sample_pose; g.sample_pose=lambda *a,**k: pose
    tris,_,_=g.gather(model,anim=anim); g.sample_pose=orig
    idx={n.get('name'):i for i,n in enumerate(d['nodes'])}
    B=W[idx[bone]]; s=np.linalg.norm(B[:3,0])
    R=B[:3,:3]/s
    # Vat pham: thiet ke theo don vi "the gioi game" (nhan vat cao ~40) -> doi ve don vi model = /scale_model
    k=1.0/scale_model
    Rg=euler_yxz(grip_rot_deg)
    pos=B[:3,3]+R@(np.array(grip_pos)*k)
    for v0,v1,v2,c in item(item_name):
        tris.append(tuple(pos+R@Rg@((np.array(v)+np.array(item_offset))*k) for v in (v0,v1,v2))+(c,))
    return tris

if __name__=='__main__':
    pass
