# Render xem truoc file GLB/GLTF (tu the nghi/rest hoac khung dau cua 1 animation) ra PNG.
# Dung: python3 glbview.py out.png model.glb [--anim Idle] [--attach piece.glb:Bone:ox,oy,oz:rx,ry,rz:scale]...
import json, struct, sys, os, math
import numpy as np
from PIL import Image, ImageDraw

def load(path):
    b = open(path, 'rb').read()
    if b[:4] == b'glTF':
        l = struct.unpack('<I', b[12:16])[0]
        d = json.loads(b[20:20 + l])
        off = 20 + l
        bins = [b[off + 8: off + 8 + struct.unpack('<I', b[off:off + 4])[0]]] if off < len(b) else [b'']
    else:
        d = json.loads(b)
        base = os.path.dirname(path)
        import base64
        bins = [base64.b64decode(buf['uri'].split(',', 1)[1]) if buf['uri'].startswith('data:')
                else open(os.path.join(base, buf['uri']), 'rb').read() for buf in d['buffers']]
    return d, bins

CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}

def acc(d, bins, i):
    a = d['accessors'][i]
    bv = d['bufferViews'][a['bufferView']]
    buf = bins[bv.get('buffer', 0)]
    dt = np.dtype(CT[a['componentType']])
    n = NC[a['type']]
    start = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = bv.get('byteStride', 0) or dt.itemsize * n
    cnt = a['count']
    raw = np.frombuffer(buf, dtype=np.uint8, count=stride * (cnt - 1) + dt.itemsize * n, offset=start)
    out = np.lib.stride_tricks.as_strided(raw, shape=(cnt, dt.itemsize * n), strides=(stride, 1)).copy()
    arr = out.view(dt).reshape(cnt, n).astype(np.float64)
    if a.get('normalized'):
        arr /= np.iinfo(dt).max
    return arr

def trs(t=None, r=None, s=None):
    m = np.eye(4)
    if s is not None: m = np.diag([s[0], s[1], s[2], 1.0]) @ m
    if r is not None:
        x, y, z, w = r
        R = np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                      [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                      [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])
        M = np.eye(4); M[:3, :3] = R; m = M @ m
    if t is not None:
        T = np.eye(4); T[:3, 3] = t; m = T @ m
    return m

def node_local(n, pose=None, idx=None):
    if 'matrix' in n: return np.array(n['matrix']).reshape(4, 4).T
    t, r, s = n.get('translation'), n.get('rotation'), n.get('scale')
    if pose and idx in pose:
        p = pose[idx]; t = p.get('t', t); r = p.get('r', r); s = p.get('s', s)
    return trs(t, r, s)

def sample_pose(d, bins, anim_name, time=0.0):
    pose = {}
    if not anim_name: return pose
    for a in d.get('animations', []):
        if a.get('name', '').split('|')[-1] != anim_name: continue
        for ch in a['channels']:
            smp = a['samplers'][ch['sampler']]
            ts = acc(d, bins, smp['input'])[:, 0]
            vs = acc(d, bins, smp['output'])
            k = int(np.searchsorted(ts, time, side='right') - 1); k = max(0, min(k, len(ts) - 1))
            path = {'translation': 't', 'rotation': 'r', 'scale': 's'}.get(ch['target']['path'])
            if path: pose.setdefault(ch['target']['node'], {})[path] = list(vs[k])
        break
    return pose

def world_mats(d, pose):
    W = {}
    parent = {}
    for i, n in enumerate(d['nodes']):
        for c in n.get('children', []): parent[c] = i
    def get(i):
        if i in W: return W[i]
        m = node_local(d['nodes'][i], pose, i)
        if i in parent: m = get(parent[i]) @ m
        W[i] = m; return m
    for i in range(len(d['nodes'])): get(i)
    return W

def color_of(d, prim, recolor):
    mi = prim.get('material')
    if mi is None: return (0.7, 0.7, 0.7)
    m = d['materials'][mi]
    name = m.get('name', '')
    if recolor:
        for k, v in recolor.items():
            if k == name: return v
    pbr = m.get('pbrMetallicRoughness', {})
    c = pbr.get('baseColorFactor', [0.8, 0.8, 0.8, 1])
    return tuple(c[:3])

ANIM_FROM = None
def gather(path, base_mat=np.eye(4), anim=None, recolor=None, only=None, mats=None):
    d, bins = load(path)
    pose = sample_pose(d, bins, anim)
    if anim and not pose and ANIM_FROM:
        ad, ab = load(ANIM_FROM)
        ap = sample_pose(ad, ab, anim)
        byname = {ad['nodes'][i].get('name'): v for i, v in ap.items()}
        pose = {i: byname[n.get('name')] for i, n in enumerate(d['nodes']) if n.get('name') in byname}
    W = world_mats(d, pose)
    tris = []
    for ni, n in enumerate(d['nodes']):
        if 'mesh' not in n: continue
        if only and n.get('name') not in only: continue
        mesh = d['meshes'][n['mesh']]
        skin = d['skins'][n['skin']] if 'skin' in n else None
        if skin:
            ibm = acc(d, bins, skin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
            jm = np.array([W[j] @ ibm[k] for k, j in enumerate(skin['joints'])])
        for prim in mesh['primitives']:
            if prim.get('mode', 4) != 4: continue
            if mats and d['materials'][prim['material']].get('name') not in mats: continue
            P = acc(d, bins, prim['attributes']['POSITION'])
            P4 = np.c_[P, np.ones(len(P))]
            if skin and 'JOINTS_0' in prim['attributes'] and anim:
                J = acc(d, bins, prim['attributes']['JOINTS_0']).astype(int)
                Wt = acc(d, bins, prim['attributes']['WEIGHTS_0'])
                out = np.zeros((len(P), 4))
                for c in range(4):
                    out += Wt[:, c:c+1] * np.einsum('nij,nj->ni', jm[J[:, c]], P4)
                P4 = out
            else:
                P4 = (W[ni] @ P4.T).T
            P4 = (base_mat @ P4.T).T
            idx = acc(d, bins, prim['indices'])[:, 0].astype(int) if 'indices' in prim else np.arange(len(P))
            col = color_of(d, prim, recolor)
            V = P4[:, :3]
            for k in range(0, len(idx) - 2, 3):
                tris.append((V[idx[k]], V[idx[k+1]], V[idx[k+2]], col))
    return tris, d, W

def bone_world(d, W, name):
    for i, n in enumerate(d['nodes']):
        if n.get('name') == name: return W[i]
    return None

def euler(rx, ry, rz):
    rx, ry, rz = map(math.radians, (rx, ry, rz))
    cx, sx, cy, sy, cz, sz = math.cos(rx), math.sin(rx), math.cos(ry), math.sin(ry), math.cos(rz), math.sin(rz)
    X = np.array([[1,0,0,0],[0,cx,-sx,0],[0,sx,cx,0],[0,0,0,1]])
    Y = np.array([[cy,0,sy,0],[0,1,0,0],[-sy,0,cy,0],[0,0,0,1]])
    Z = np.array([[cz,-sz,0,0],[sz,cz,0,0],[0,0,1,0],[0,0,0,1]])
    return Y @ X @ Z

def render(tris, out, size=420, views=((0, 'front'), (90, 'side'))):
    imgs = []
    allv = np.array([v for t in tris for v in t[:3]])
    lo, hi = allv.min(0), allv.max(0)
    ctr = (lo + hi) / 2; ext = (hi - lo).max() * 0.55
    light = np.array([0.4, 0.7, 0.6]); light /= np.linalg.norm(light)
    for yaw, label in views:
        a = math.radians(yaw); R = np.array([[math.cos(a), 0, math.sin(a)], [0, 1, 0], [-math.sin(a), 0, math.cos(a)]])
        im = Image.new('RGB', (size, size), (235, 238, 242)); dr = ImageDraw.Draw(im)
        items = []
        for v0, v1, v2, col in tris:
            p = [R @ (v - ctr) for v in (v0, v1, v2)]
            nrm = np.cross(p[1] - p[0], p[2] - p[0]); ln = np.linalg.norm(nrm)
            if ln < 1e-12: continue
            nrm /= ln
            sh = 0.35 + 0.65 * abs(float(nrm @ light))
            z = (p[0][2] + p[1][2] + p[2][2]) / 3
            pts = [(size/2 + q[0] / ext * size/2, size/2 - q[1] / ext * size/2) for q in p]
            items.append((z, pts, tuple(int(255 * min(1, (c * sh) ** (1 / 2.2))) for c in col)))
        items.sort(key=lambda x: x[0])
        for _, pts, c in items: dr.polygon(pts, fill=c)
        dr.text((6, 6), label, fill=(0, 0, 0))
        imgs.append(im)
    W = Image.new('RGB', (size * len(imgs), size))
    for i, im in enumerate(imgs): W.paste(im, (i * size, 0))
    W.save(out)

if __name__ == '__main__':
    global_anim = None
    out, model = sys.argv[1], sys.argv[2]
    anim = None; attaches = []; recolor = {}; also = []; only = None
    args = sys.argv[3:]
    i = 0
    while i < len(args):
        if args[i] == '--anim': anim = args[i+1]; i += 2
        elif args[i] == '--attach': attaches.append(args[i+1]); i += 2
        elif args[i] == '--also': also.append(args[i+1]); i += 2
        elif args[i] == "--animfrom": globals()["ANIM_FROM"] = args[i+1]; i += 2
        elif args[i] == '--only': only = args[i+1].split(','); i += 2
        elif args[i] == '--color':
            k, v = args[i+1].split('='); recolor[k] = tuple(float(x) for x in v.split(',')); i += 2
        else: i += 1
    tris, d, W = gather(model, anim=anim, recolor=recolor, only=only)
    for a in also:
        parts = a.split(':')
        f, names = parts[0], parts[1]
        t2, _, _ = gather(f, anim=anim, recolor=recolor, only=names.split(','), mats=parts[2].split(',') if len(parts) > 2 else None)
        tris += t2
    for a in attaches:
        p, bone, off, rot, sc = a.split(':')
        bw = bone_world(d, W, bone)
        if bw is None: print('no bone', bone); continue
        # Bo scale cua xuong (giu huong + vi tri), giong BoneAttachment3D trong Godot (xet scale rieng).
        Rb = bw[:3, :3]; s = np.linalg.norm(Rb, axis=0); Rn = Rb / s
        M = np.eye(4); M[:3, :3] = Rn; M[:3, 3] = bw[:3, 3]
        o = [float(x) for x in off.split(',')]; r = [float(x) for x in rot.split(',')]
        T = np.eye(4); T[:3, 3] = o
        S = np.diag([float(sc)] * 3 + [1])
        t2, _, _ = gather(p, base_mat=M @ T @ euler(*r) @ S)
        tris += t2
    render(tris, out)
    allv = np.array([v for t in tris for v in t[:3]])
    print('bounds', allv.min(0).round(3), allv.max(0).round(3), 'tris', len(tris))
