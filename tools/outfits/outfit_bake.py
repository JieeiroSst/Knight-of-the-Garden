# Chuyen (retarget) luoi skinned tu model nguon (vd King - Quaternius, CC0) sang bo xuong dich
# (Farmer cho nhan vat nam, Woman_Casual cho nu) - tao file .glb moi chi chua bo xuong dich + luoi
# da chuyen, de Godot gan thang vao Skeleton3D cua nguoi choi (cung ten xuong, cung bind pose).
# Dung: python3 outfit_bake.py src.glb target.gltf out.glb male|female Mesh1,Mesh2,...
import json, struct, sys, math
import numpy as np
sys.path.insert(0, __file__.rsplit('/', 1)[0])
from glbview import load, acc, world_mats

src_path, tgt_path, out_path, mode, mesh_names = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5].split(',')
helmet_spec = sys.argv[6] if len(sys.argv) > 6 and sys.argv[6] != '-' else None   # file:scale:ox,oy,oz:yaw
drop_mats = sys.argv[7].split(',') if len(sys.argv) > 7 and sys.argv[7] else []
PERP_F = float(sys.argv[8]) if len(sys.argv) > 8 else 1.2

sd, sb = load(src_path)
td, tb = load(tgt_path)
SW = world_mats(sd, {})
TW = world_mats(td, {})

def skin_names(d, si):
    return [d['nodes'][j]['name'] for j in d['skins'][si]['joints']]

# Dung TU THE BIND (tu inverseBindMatrices), khong phai tu the nghi cua node (co the khac nhau).
def mesh_world(d, W, skin_i):
    for i, n in enumerate(d['nodes']):
        if 'mesh' in n and n.get('skin') == skin_i: return W[i]
    return np.eye(4)
tskin = td['skins'][0]
tnames = skin_names(td, 0)
tibm = acc(td, tb, tskin['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
M = mesh_world(td, TW, 0)
tpos = {n: (M @ np.linalg.inv(tibm[k]))[:3, 3] for k, n in enumerate(tnames)}
spos = {}
for si, s in enumerate(sd['skins']):
    sibm = acc(sd, sb, s['inverseBindMatrices']).reshape(-1, 4, 4).transpose(0, 2, 1)
    Ms = mesh_world(sd, SW, si)
    for k, n in enumerate(skin_names(sd, si)):
        spos.setdefault(n, (Ms @ np.linalg.inv(sibm[k]))[:3, 3])

# Anh xa xuong nguon -> xuong dich.
FINGERS = ['Index', 'Middle', 'Ring', 'Pinky']
def map_bone(n):
    if mode == 'male':
        return n if n in tnames else None
    side = n[-2:] if n.endswith(('.L', '.R')) else ''
    base = n[:-2] if side else n
    m = {'Root': 'Bone', 'Body': 'Body', 'Hips': 'Hips', 'Abdomen': 'Abdomen', 'Torso': 'Torso', 'Chest': 'Torso',
         'Neck': 'Neck', 'Head': 'Head', 'Shoulder': 'Shoulder', 'UpperArm': 'UpperArm', 'LowerArm': 'LowerArm',
         'Wrist': 'Palm', 'UpperLeg': 'UpperLeg', 'LowerLeg': 'LowerLeg', 'Foot': 'Foot', 'PT': 'PoleTarget',
         'Thumb1': 'Thumb1', 'Thumb2': 'Thumb2', 'Thumb3': 'Thumb2'}
    if base in m: return m[base] + side
    for f in FINGERS:
        if base.startswith(f): return ('MiddleHand' if base.endswith('1') else 'Fingers') + side
    return None

# Doan xuong (dau -> con) de tinh huong + do dai cho moi xuong nguon/dich.
def seg_src(n):
    side = n[-2:] if n.endswith(('.L', '.R')) else ''
    base = n[:-2] if side else n
    nxt = {'Hips': 'Abdomen', 'Abdomen': 'Torso', 'Torso': 'Chest', 'Chest': 'Neck', 'Neck': 'Head',
           'Shoulder': 'UpperArm', 'UpperArm': 'LowerArm', 'LowerArm': 'Wrist', 'Wrist': 'Middle1',
           'UpperLeg': 'LowerLeg', 'LowerLeg': 'Foot'}.get(base)
    return (nxt + side) if nxt else None
def seg_tgt(n):
    side = n[-2:] if n.endswith(('.L', '.R')) else ''
    base = n[:-2] if side else n
    if mode == 'male': return seg_src(n)
    nxt = {'Hips': 'Abdomen', 'Abdomen': 'Torso', 'Torso': 'Neck', 'Neck': 'Head', 'Shoulder': 'UpperArm',
           'UpperArm': 'LowerArm', 'LowerArm': 'Palm', 'Palm': 'Fingers', 'UpperLeg': 'LowerLeg', 'LowerLeg': 'Foot'}.get(base)
    return (nxt + side) if nxt else None

def height(pos): return pos['Head'][1] - min(pos['Foot.L'][1], pos['Foot.R'][1])
s_global = height(tpos) / height(spos)

def rot_between(a, b):
    a = a / np.linalg.norm(a); b = b / np.linalg.norm(b)
    v = np.cross(a, b); c = float(a @ b)
    if c > 0.99999: return np.eye(3)
    if c < -0.99999: return -np.eye(3)
    vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + vx + vx @ vx * (1 / (1 + c))

# Khung chuyen cho tung xuong nguon: v' = pT + R * S * (v - pS).
xf_cache = {}
def bone_xf(n):
    if n in xf_cache: return xf_cache[n]
    tn = map_bone(n)
    if tn is None or n not in spos:
        xf_cache[n] = None; return None
    # Ngon tay dung chung khung voi co tay (giu hinh ban tay).
    side = n[-2:] if n.endswith(('.L', '.R')) else ''
    base = n[:-2] if side else n
    anchor = n
    if any(base.startswith(f) for f in FINGERS) or base.startswith('Thumb'):
        anchor = 'Wrist' + side
    ta = map_bone(anchor)
    if anchor != n or base == 'Wrist':
        # Ban tay: xoay theo huong cang tay, ti le deu (doan co tay -> ngon qua ngan de do ti le).
        la_s, la_t = spos['LowerArm' + side], tpos[map_bone('LowerArm' + side)]
        R = rot_between(spos[anchor] - la_s, tpos[ta] - la_t)
        xf = (spos[anchor], tpos[ta], R @ (np.eye(3) * s_global * (0.9 if mode == 'female' else 1.0)), R)
        xf_cache[n] = xf
        return xf
    cs, ct = seg_src(anchor), seg_tgt(ta)
    R = np.eye(3); S = np.eye(3) * s_global
    if cs and ct and cs in spos and ct in tpos:
        ds = spos[cs] - spos[anchor]; dt = tpos[ct] - tpos[ta]
        ls, lt = np.linalg.norm(ds), np.linalg.norm(dt)
        if ls > 1e-6 and lt > 1e-6:
            R = rot_between(ds, dt)
            u = ds / ls
            along = lt / ls
            # Doc theo xuong: theo ti le do dai; vuong goc: theo ti le chieu cao tong the.
            # Nu: tay/than/chan cua rig nu day hon ti le chieu cao -> noi rong giap theo chieu ngang.
            perp = s_global * (PERP_F if mode == 'female' else 1.0)
            S = perp * (np.eye(3) - np.outer(u, u)) + along * np.outer(u, u)
    xf = (spos[anchor], tpos[ta], R @ S, R)
    xf_cache[n] = xf
    return xf

# Ma tran M (W_j * IBM_j cua dich o tu the nghi) -> doi toa do the gioi ve khong gian luoi dich.
Minv = np.linalg.inv(M)

out_meshes = []
materials = []
mat_index = {}
for ni, n in enumerate(sd['nodes']):
    if n.get('name') not in mesh_names or 'mesh' not in n: continue
    skin = sd['skins'][n['skin']]
    snames = skin_names(sd, n['skin'])
    Ms = SW[ni]
    prims = []
    for prim in sd['meshes'][n['mesh']]['primitives']:
        a = prim['attributes']
        P = acc(sd, sb, a['POSITION']); Nn = acc(sd, sb, a['NORMAL']) if 'NORMAL' in a else None
        J = acc(sd, sb, a['JOINTS_0']).astype(int); Wt = acc(sd, sb, a['WEIGHTS_0'])
        world = (Ms @ np.c_[P, np.ones(len(P))].T).T
        nworld = None
        if Nn is not None:
            nworld = (np.linalg.inv(Ms[:3, :3]).T @ Nn.T).T
            nworld /= np.maximum(np.linalg.norm(nworld, axis=1, keepdims=True), 1e-9)
        newP = np.zeros((len(P), 3)); newN = np.zeros((len(P), 3))
        newJ = np.zeros((len(P), 4), dtype=np.uint16); newW = np.zeros((len(P), 4), dtype=np.float32)
        for vi in range(len(P)):
            acc_p = np.zeros(3); acc_n = np.zeros(3); tw = 0.0
            wmap = {}
            for c in range(4):
                w = Wt[vi, c]
                if w <= 0: continue
                bn = snames[J[vi, c]]
                xf = bone_xf(bn)
                tbn = map_bone(bn)
                if xf is None or tbn is None: continue
                pS, pT, A, Rr = xf
                acc_p += w * (pT + A @ (world[vi, :3] - pS))
                if nworld is not None: acc_n += w * (Rr @ nworld[vi])
                tw += w
                wmap[tbn] = wmap.get(tbn, 0) + w
            if tw <= 0: acc_p, tw = world[vi, :3] * s_global, 1.0; wmap = {'Hips': 1.0}
            newP[vi] = acc_p / tw
            if nworld is not None:
                nn = acc_n / max(np.linalg.norm(acc_n), 1e-9); newN[vi] = nn
            items = sorted(wmap.items(), key=lambda x: -x[1])[:4]
            s = sum(w for _, w in items)
            for c, (bn, w) in enumerate(items):
                newJ[vi, c] = tnames.index(bn); newW[vi, c] = w / s
        local = (Minv @ np.c_[newP, np.ones(len(newP))].T).T[:, :3]
        nlocal = (np.linalg.inv(M[:3, :3]).T @ newN.T).T if Nn is not None else None
        if nlocal is not None: nlocal /= np.maximum(np.linalg.norm(nlocal, axis=1, keepdims=True), 1e-9)
        idx = acc(sd, sb, prim['indices'])[:, 0].astype(np.uint32)
        mi = prim.get('material')
        if mi is not None and sd['materials'][mi].get('name') in drop_mats: continue
        if mi is not None and mi not in mat_index:
            mat_index[mi] = len(materials); materials.append(sd['materials'][mi])
        prims.append((local.astype(np.float32), None if nlocal is None else nlocal.astype(np.float32), newJ, newW, idx, mat_index.get(mi)))
    out_meshes.append((n['name'], prims))

# ---- Mu tru: luoi cung buoc 100% vao xuong Head (khong can BoneAttachment trong Godot) ----
if helmet_spec:
    hf, hs, ho, hy = helmet_spec.split(':')
    hs = float(hs); ho = np.array([float(x) for x in ho.split(',')]); hy = math.radians(float(hy))
    hd, hb = load(hf); HW = world_mats(hd, {})
    Ry = np.array([[math.cos(hy), 0, math.sin(hy)], [0, 1, 0], [-math.sin(hy), 0, math.cos(hy)]])
    hprims = []
    head_i = tnames.index('Head')
    for ni, n in enumerate(hd['nodes']):
        if 'mesh' not in n: continue
        for prim in hd['meshes'][n['mesh']]['primitives']:
            P = acc(hd, hb, prim['attributes']['POSITION'])
            Pw = (HW[ni] @ np.c_[P, np.ones(len(P))].T).T[:, :3]
            Pw = (Ry @ (Pw * hs).T).T + ho
            Nn = acc(hd, hb, prim['attributes']['NORMAL']) if 'NORMAL' in prim['attributes'] else None
            if Nn is not None:
                Nn = (Ry @ (np.linalg.inv(HW[ni][:3, :3]).T @ Nn.T)).T
                Nn /= np.maximum(np.linalg.norm(Nn, axis=1, keepdims=True), 1e-9)
            local = (Minv @ np.c_[Pw, np.ones(len(Pw))].T).T[:, :3]
            nl = None
            if Nn is not None:
                nl = (np.linalg.inv(M[:3, :3]).T @ Nn.T).T; nl /= np.maximum(np.linalg.norm(nl, axis=1, keepdims=True), 1e-9)
            J = np.zeros((len(P), 4), dtype=np.uint16); J[:, 0] = head_i
            Wt = np.zeros((len(P), 4), dtype=np.float32); Wt[:, 0] = 1
            idx = acc(hd, hb, prim['indices'])[:, 0].astype(np.uint32)
            m = dict(hd['materials'][prim['material']]); m['name'] = 'Helmet_' + m.get('name', 'mat')
            materials.append(m)
            hprims.append((local.astype(np.float32), None if nl is None else nl.astype(np.float32), J, Wt, idx, len(materials) - 1))
    out_meshes.append(('Helmet', hprims))

# ---- Ghi GLB: bo xuong dich (moi node khong phai luoi) + luoi moi ----
binbuf = bytearray(); views = []; accessors = []
def add(arr, target=None, comp=5126, typ='VEC3', minmax=False):
    while len(binbuf) % 4: binbuf.append(0)
    off = len(binbuf); data = arr.tobytes(); binbuf.extend(data)
    v = {'buffer': 0, 'byteOffset': off, 'byteLength': len(data)}
    if target: v['target'] = target
    views.append(v)
    a = {'bufferView': len(views) - 1, 'componentType': comp, 'count': int(arr.shape[0]), 'type': typ}
    if minmax: a['min'] = arr.min(0).tolist(); a['max'] = arr.max(0).tolist()
    accessors.append(a)
    return len(accessors) - 1

keep = [i for i, n in enumerate(td['nodes']) if 'mesh' not in n]
remap = {old: new for new, old in enumerate(keep)}
nodes = []
for old in keep:
    n = dict(td['nodes'][old])
    n.pop('skin', None)
    if 'children' in n:
        ch = [remap[c] for c in n['children'] if c in remap]
        if ch: n['children'] = ch
        else: n.pop('children')
    nodes.append(n)
ibm_acc = add(tibm.transpose(0, 2, 1).reshape(-1, 16).astype(np.float32), typ='MAT4')
skins = [{'joints': [remap[j] for j in tskin['joints']], 'inverseBindMatrices': ibm_acc}]
if 'skeleton' in tskin and tskin['skeleton'] in remap: skins[0]['skeleton'] = remap[tskin['skeleton']]
meshes = []
scene_roots = [remap[r] for r in td['scenes'][td.get('scene', 0)]['nodes'] if r in remap]
for name, prims in out_meshes:
    gp = []
    for P, Nn, J, Wt, idx, mi in prims:
        attrs = {'POSITION': add(P, 34962, minmax=True), 'JOINTS_0': add(J, 34962, comp=5123, typ='VEC4'), 'WEIGHTS_0': add(Wt, 34962, typ='VEC4')}
        if Nn is not None: attrs['NORMAL'] = add(Nn, 34962)
        p = {'attributes': attrs, 'indices': add(idx.reshape(-1, 1), 34963, comp=5125, typ='SCALAR')}
        accessors[p['indices']]['type'] = 'SCALAR'
        if mi is not None: p['material'] = mi
        gp.append(p)
    meshes.append({'name': name, 'primitives': gp})
    nodes.append({'name': name, 'mesh': len(meshes) - 1, 'skin': 0, 'matrix': M.T.reshape(-1).tolist()})
    scene_roots.append(len(nodes) - 1)
mats = []
for m in materials:
    m = {k: v for k, v in m.items() if k in ('name', 'pbrMetallicRoughness', 'doubleSided', 'alphaMode')}
    pbr = m.get('pbrMetallicRoughness', {})
    pbr.pop('baseColorTexture', None); pbr.pop('metallicRoughnessTexture', None)
    mats.append(m)
gl = {'asset': {'version': '2.0', 'generator': 'hsvv-outfit-bake'}, 'scene': 0, 'scenes': [{'nodes': scene_roots}],
      'nodes': nodes, 'meshes': meshes, 'skins': skins, 'materials': mats, 'accessors': accessors, 'bufferViews': views,
      'buffers': [{'byteLength': len(binbuf)}]}
js = json.dumps(gl, separators=(',', ':')).encode()
while len(js) % 4: js += b' '
while len(binbuf) % 4: binbuf.append(0)
total = 12 + 8 + len(js) + 8 + len(binbuf)
with open(out_path, 'wb') as f:
    f.write(struct.pack('<III', 0x46546C67, 2, total))
    f.write(struct.pack('<II', len(js), 0x4E4F534A)); f.write(js)
    f.write(struct.pack('<II', len(binbuf), 0x004E4942)); f.write(bytes(binbuf))
print('ok', out_path, 's_global', round(s_global, 3), 'verts', sum(len(p[0]) for _, ps in out_meshes for p in ps))
