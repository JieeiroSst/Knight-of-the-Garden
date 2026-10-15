import sys; sys.path.insert(0,'.')
import numpy as np, glbview as g, heldsim2 as h
from PIL import Image
def zoom(rig, curl, label, item_name='sword', sc=1.8, gp=(0,-0.7,0), mods=None):
    mods = mods or [('UpperArm.R',(8,0,-6)),('LowerArm.R',(28,0,0)),('Wrist.R',(0,-25,25)),('Palm.R',(0,-25,25))]
    t,_=h.scene(rig,mods,item_name,sc,gp,curl=curl)
    # tim tam ban tay
    rg=h.RIGS[rig]; d,b=g.load(h.C+rg['model']); pose=g.sample_pose(d,b,rg['anim'])
    from posesim import apply; apply(d,pose,mods)
    W=g.world_mats(d,pose); idx={n.get('name'):i for i,n in enumerate(d['nodes'])}
    c=W[idx[rg['hand']]][:3,3]; r=0.2 if rig=='male' else 0.55
    keep=[x for x in t if np.linalg.norm((x[0]+x[1]+x[2])/3-c)<r]
    g.render(keep,'_z.png',size=300,views=((0,label),(90,'side'),(200,'back'),(300,'other')))
    return Image.open('_z.png').copy()
if __name__=="__main__":
  tiles=[zoom('male',True,'male sword'),zoom('male',True,'male hoe','hoe',3.6,(0,0.8,0)),zoom('female',True,'fem sword'),zoom('female',True,'fem hoe','hoe',3.6,(0,0.8,0))]
  W=Image.new('RGB',(1200,300*len(tiles)))
  for i,t in enumerate(tiles): W.paste(t,(0,300*i))
  W.save('outfit/hand_zoom.png')
