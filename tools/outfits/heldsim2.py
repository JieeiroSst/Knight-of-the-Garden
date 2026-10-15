import sys, math, json
import numpy as np
sys.path.insert(0, '.')
import glbview as g
from posesim import apply, axis_rot, mat_to_quat
from heldsim import item

RIGS = {
  'male': dict(model='Farmer.gltf', anim='Idle', hand='Wrist.R',
    curl=[(f+str(i)+'.R',a) for f in ('Index','Middle','Ring','Pinky') for i,a in ((1,10),(2,85),(3,80),(4,60))]+[('Thumb2.R',40),('Thumb3.R',35)]),
  'female': dict(model='Woman_Casual.glb', anim='Female_Idle', hand='Palm.R',
    curl=[('MiddleHand.R',80),('Fingers.R',85),('Thumb1.R',20),('Thumb2.R',45)]),
}
C='/Users/quan/Desktop/HiepSiVeVuon/assets3d/quaternius/characters/'

def frame(rig, d, W):
    idx={n.get('name'):i for i,n in enumerate(d['nodes'])}
    P=lambda n: W[idx[n]][:3,3]
    if rig=='male':
        F=P('Middle4.R')-P('Middle1.R'); A=P('Index2.R')-P('Pinky2.R'); base=P('Middle2.R'); flen=np.linalg.norm(P('Middle4.R')-P('Middle2.R'))
    else:
        F=P('Fingers.R')-P('MiddleHand.R'); A=P('Thumb1.R')-P('Palm.R'); base=P('MiddleHand.R'); flen=np.linalg.norm(P('Fingers.R')-P('MiddleHand.R'))*1.6
    F/=np.linalg.norm(F); A-=A.dot(F)*F; A/=np.linalg.norm(A); N=np.cross(F,A)
    return F,A,N,base,flen

def curl_pose(d, pose, rig, amount=1.0):
    parent={}
    for i,n in enumerate(d['nodes']):
        for c in n.get('children',[]): parent[c]=i
    idx={n.get('name'):i for i,n in enumerate(d['nodes'])}
    W=g.world_mats(d,pose); F,A,N,_,_=frame(rig,d,W)
    for bn,deg in RIGS[rig]['curl']:
        if bn not in idx: continue
        b=idx[bn]; W=g.world_mats(d,pose)
        G=W[b][:3,:3]; Pm=W[parent[b]][:3,:3]
        R=axis_rot(A,deg*amount)
        pose.setdefault(b,{})['r']=mat_to_quat(np.linalg.inv(Pm)@(R@G))
    return pose

# Khung cam (trong khong gian xuong ban tay, da bo scale): truc vat Y = A (phia ngon tro), X = -F, Z = X x Y.
OFFS={'male':(0.18,0.32),'female':(-0.1,0.45)}
def grip_local(rig, d, W):
    idx={n.get('name'):i for i,n in enumerate(d['nodes'])}
    F,A,N,base,flen=frame(rig,d,W)
    B=W[idx[RIGS[rig]['hand']]]; s=np.linalg.norm(B[:3,0]); R=B[:3,:3]/s
    X=-F; Y=A; Z=np.cross(X,Y)
    Rw=np.c_[X,Y,Z]
    fa,na=OFFS[rig]
    center=base + F*flen*fa - N*flen*na
    return R.T@Rw, (R.T@(center-B[:3,3]))/s, s, B, R, center

def scene(rig, mods, item_name, item_scale, grip_pt, curl=True, can=False):
    rg=RIGS[rig]; d,b=g.load(C+rg['model']); pose=g.sample_pose(d,b,rg['anim'])
    # khung cam tinh o tu the goc (truoc khi xoay tay) - hang so trong khong gian xuong
    W0=g.world_mats(d,pose)
    Rl,pl,_,_,_,_=grip_local(rig,d,W0)
    apply(d,pose,mods)
    if curl: curl_pose(d,pose,rig)
    W=g.world_mats(d,pose)
    orig=g.sample_pose; g.sample_pose=lambda *a,**k: pose
    tris,_,_=g.gather(C+rg['model'],anim=rg['anim']); g.sample_pose=orig
    idx={n.get('name'):i for i,n in enumerate(d['nodes'])}
    B=W[idx[rg['hand']]]; s=np.linalg.norm(B[:3,0]); R=B[:3,:3]/s
    Rg=Rl.copy()
    if can:  # binh tuoi: voi (Z vat) theo truc nam tay, mieng binh len (Y vat) ve phia co tay
        Xl,Yl,Zl=Rl[:,0],Rl[:,1],Rl[:,2]
        Rg=np.c_[np.cross(Xl,Yl), Xl, Yl]   # itemY = -F (ve phia co tay = huong len), itemZ = A (voi), itemX = Y x Z
    height=1.8 if rig=='male' else 4.62
    k=height/40.0   # don vi game (nguoi cao ~40) -> don vi model
    pos=B[:3,3]+R@(pl*s)
    for v0,v1,v2,c in item(item_name):
        tris.append(tuple(pos+R@Rg@((np.array(v)-np.array(grip_pt))*item_scale*k) for v in (v0,v1,v2))+(c,))
    return tris, (Rl, pl)
