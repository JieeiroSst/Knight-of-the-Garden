# Mo phong lop tu the cong them (giong ArmPoseModifier ben Godot) de kiem tra dau goc quay.
import sys, math, json
import numpy as np
sys.path.insert(0, '.')
import glbview as g

def axis_rot(axis, deg):
    a = np.array(axis, float); a /= np.linalg.norm(a); t = math.radians(deg)
    K = np.array([[0, -a[2], a[1]], [a[2], 0, -a[0]], [-a[1], a[0], 0]])
    return np.eye(3) + math.sin(t) * K + (1 - math.cos(t)) * K @ K

def mat_to_quat(m):
    m = m / np.linalg.norm(m, axis=0)
    t = np.trace(m)
    if t > 0:
        s = math.sqrt(t + 1) * 2; w = 0.25 * s; x = (m[2,1]-m[1,2])/s; y = (m[0,2]-m[2,0])/s; z = (m[1,0]-m[0,1])/s
    elif m[0,0] > m[1,1] and m[0,0] > m[2,2]:
        s = math.sqrt(1+m[0,0]-m[1,1]-m[2,2])*2; w=(m[2,1]-m[1,2])/s; x=0.25*s; y=(m[0,1]+m[1,0])/s; z=(m[0,2]+m[2,0])/s
    elif m[1,1] > m[2,2]:
        s = math.sqrt(1+m[1,1]-m[0,0]-m[2,2])*2; w=(m[0,2]-m[2,0])/s; x=(m[0,1]+m[1,0])/s; y=0.25*s; z=(m[1,2]+m[2,1])/s
    else:
        s = math.sqrt(1+m[2,2]-m[0,0]-m[1,1])*2; w=(m[1,0]-m[0,1])/s; x=(m[0,2]+m[2,0])/s; y=(m[1,2]+m[2,1])/s; z=0.25*s
    return [x, y, z, w]

UP, FWD, RIGHT = [0, 1, 0], [0, 0, 1], [-1, 0, 0]

def apply(d, pose, mods):
    parent = {}
    for i, n in enumerate(d['nodes']):
        for c in n.get('children', []): parent[c] = i
    idx = {n.get('name'): i for i, n in enumerate(d['nodes'])}
    for bone, (pitch, yaw, roll) in mods:
        if bone not in idx: continue
        b = idx[bone]
        W = g.world_mats(d, pose)
        G = W[b][:3, :3]; P = W[parent[b]][:3, :3] if b in parent else np.eye(3)
        R = axis_rot(FWD, roll) @ axis_rot(RIGHT, pitch) @ axis_rot(UP, yaw)
        local = np.linalg.inv(P) @ (R @ G)
        pose.setdefault(b, {})['r'] = mat_to_quat(local)
    return pose

if __name__ == '__main__':
    model, anim, out = sys.argv[1], sys.argv[2], sys.argv[3]
    frames = json.loads(sys.argv[4])  # list of {label, mods:[[bone,p,y,r],...]}
    from PIL import Image, ImageDraw
    tiles = []
    for fr in frames:
        d, b = g.load(model)
        pose = g.sample_pose(d, b, anim)
        apply(d, pose, [(m[0], (m[1], m[2], m[3])) for m in fr['mods']])
        # gather with given pose
        orig = g.sample_pose
        g.sample_pose = lambda *a, **k: pose
        tris, _, _ = g.gather(model, anim=anim)
        g.sample_pose = orig
        g.render(tris, '_f.png', size=260, views=((0, fr['label']), (90, 'side')))
        tiles.append(Image.open('_f.png').copy())
    W = Image.new('RGB', (520, 260 * len(tiles)))
    for i, t in enumerate(tiles): W.paste(t, (0, 260 * i))
    W.save(out)
