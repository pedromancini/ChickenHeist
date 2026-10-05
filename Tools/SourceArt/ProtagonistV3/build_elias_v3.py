# Elias v3: the AI-generated protagonist (source/Elias_AI_source.fbx) converted to the project's
# low-poly cast style and rebuilt on the ProtagonistRig bone convention, so EliasNativeInstall can
# re-bake every gameplay and cinematic clip for it.
#   - painted dirt and rips replaced by a flat colour palette (one swatch per face, like the NPC atlas)
#   - head reduced to the cast's proportions, whole body scaled to 1.78 m
#   - bones renamed (Hips, Spine, Spine2, Chest, Neck, Head, ShoulderL, UpperArmL, ...), Root added
#   - five fingers with three phalanges per hand (Thumb/Index/Middle/Ring/Little 1-3 L/R)
#   - head split from the body (hidden in first person)
# Run:  blender -b --factory-startup --python build_elias_v3.py              (previews only)
#       ELIAS_EXPORT=1 blender -b --factory-startup --python build_elias_v3.py (also writes the FBX)
import bpy, bmesh, os, math, random
from mathutils import Vector, Matrix
from collections import Counter

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(ROOT, '..', '..', '..'))
SOURCE = os.path.join(ROOT, 'source', 'Elias_AI_source.fbx')
OUT_DIR = os.path.join(PROJECT, 'Assets', 'ChickenHeistGenerated', 'Characters', 'EliasNative')
GEN = os.path.join(ROOT, 'generated'); os.makedirs(GEN, exist_ok=True)
EXPORT = os.environ.get('ELIAS_EXPORT') == '1'
HEAD_SCALE = float(os.environ.get('ELIAS_HEAD_SCALE', '0.68'))
HEIGHT = float(os.environ.get('ELIAS_HEIGHT', '1.74'))   # hair included; the cast stands 1.67-1.72 m
HAND_SCALE = float(os.environ.get('ELIAS_HAND_SCALE', '0.85'))
TRIS = int(os.environ.get('ELIAS_TRIS', '13000'))
HEAD_KEEP = float(os.environ.get('ELIAS_HEAD_KEEP', '0.35'))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
src_arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
ob = next(o for o in bpy.data.objects if o.type == 'MESH')
J = {b.name: (src_arm.matrix_world @ b.head_local, src_arm.matrix_world @ b.tail_local) for b in src_arm.data.bones}

# ---------------------------------------------------------------- mesh to world space, seams welded
mw = ob.matrix_world.copy()
ob.parent = None; ob.modifiers.clear()
ob.data.transform(mw); ob.matrix_world = Matrix.Identity(4)
bpy.data.objects.remove(src_arm)
me = ob.data
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bm.to_mesh(me); bm.free()

# ---------------------------------------------------------------- weights renamed to ProtagonistRig
RN = {'Hips': 'Hips', 'Spine02': 'Spine', 'Spine01': 'Spine2', 'Spine': 'Chest', 'neck': 'Neck',
      'Head': 'Head', 'head_end': 'Head', 'headfront': 'Head'}
for s, side in (('Left', 'L'), ('Right', 'R')):
    RN.update({s + 'Shoulder': 'Shoulder' + side, s + 'Arm': 'UpperArm' + side, s + 'ForeArm': 'Forearm' + side,
               s + 'Hand': 'Hand' + side, s + 'UpLeg': 'Thigh' + side, s + 'Leg': 'Shin' + side,
               s + 'Foot': 'Foot' + side, s + 'ToeBase': 'Toe' + side})
gname = {g.index: RN[g.name] for g in ob.vertex_groups}
W = []
for v in me.vertices:
    d = {}
    for g in v.groups:
        if g.weight > 0: d[gname[g.group]] = d.get(gname[g.group], 0) + g.weight
    W.append(d)
P = [v.co.copy() for v in me.vertices]

# ---------------------------------------------------------------- joints (world)
def h(n): return J[n][0].copy()
joint = {
    'Root': (Vector((0, 0, 0)), h('Hips')),
    'Hips': (h('Hips'), h('Spine02')), 'Spine': (h('Spine02'), h('Spine01')), 'Spine2': (h('Spine01'), h('Spine')),
    'Chest': (h('Spine'), h('neck')), 'Neck': (h('neck'), h('Head')), 'Head': (h('Head'), h('Head') + Vector((0, 0, .3)))}
for s, side in (('Left', 'L'), ('Right', 'R')):
    joint['Shoulder' + side] = (h(s + 'Shoulder'), h(s + 'Arm'))
    joint['UpperArm' + side] = (h(s + 'Arm'), h(s + 'ForeArm'))
    joint['Forearm' + side] = (h(s + 'ForeArm'), h(s + 'Hand'))
    joint['Hand' + side] = (h(s + 'Hand'), J[s + 'Hand'][1].copy())
    joint['Thigh' + side] = (h(s + 'UpLeg'), h(s + 'Leg'))
    joint['Shin' + side] = (h(s + 'Leg'), h(s + 'Foot'))
    joint['Foot' + side] = (h(s + 'Foot'), h(s + 'ToeBase'))
    joint['Toe' + side] = (h(s + 'ToeBase'), J[s + 'ToeBase'][1].copy())

# ---------------------------------------------------------------- head to cast proportions
pivot = joint['Head'][0].copy()
for i, p in enumerate(P):
    wh = W[i].get('Head', 0)
    if wh > 0: P[i] = pivot + (p - pivot) * (1 - (1 - HEAD_SCALE) * wh)
head_top = max(P[i].z for i in range(len(P)) if W[i].get('Head', 0) > .5)
joint['Head'] = (pivot, Vector((pivot.x, pivot.y, pivot.z + (head_top - pivot.z) * .55)))

# ---------------------------------------------------------------- 1.78 m, feet on the ground
z0 = min(p.z for p in P); scale = HEIGHT / (max(p.z for p in P) - z0)
def S(p): return Vector((p.x * scale, p.y * scale, (p.z - z0) * scale))
P = [S(p) for p in P]
joint = {k: (S(a), S(b)) for k, (a, b) in joint.items()}
joint['Root'] = (Vector((0, 0, 0)), joint['Hips'][0].copy())

# ---------------------------------------------------------------- fingers
FINGERS = ['Thumb', 'Index', 'Middle', 'Ring', 'Little']
report = []
def build_hand(side):
    hand = 'Hand' + side
    wrist, tail = joint[hand]
    a = (tail - wrist).normalized()
    n = Vector((0, 0, -1)); n = (n - a * n.dot(a)).normalized()          # palm normal (palms down in T-pose)
    lat = n.cross(a).normalized()
    H = [i for i in range(len(P)) if W[i].get(hand, 0) >= .5]
    s = {i: (P[i] - wrist).dot(a) for i in H}; l = {i: (P[i] - wrist).dot(lat) for i in H}
    smax = max(s.values())
    F = [i for i in H if s[i] > .62 * smax]
    ls = sorted(F, key=lambda i: l[i])
    gaps = sorted(range(1, len(ls)), key=lambda k: l[ls[k]] - l[ls[k - 1]], reverse=True)[:3]
    cuts = sorted(gaps); clusters = []; start = 0
    for c in cuts + [len(ls)]: clusters.append(ls[start:c]); start = c
    # thumb lies on the -Y (front) side of the hand: order clusters index..little from the front
    clusters.sort(key=lambda c: sum(P[i].y for i in c) / len(c))
    chains = {}
    s_root = .43 * smax
    for name, cl in zip(FINGERS[1:], clusters):
        top = max(s[i] for i in cl)
        tip = sum((P[i] for i in cl if s[i] >= top - .006), Vector()) / len([i for i in cl if s[i] >= top - .006])
        c0 = sum((P[i] for i in cl), Vector()) / len(cl)
        d = (tip - c0).normalized()
        k = (s_root - (c0 - wrist).dot(a)) / max(1e-4, d.dot(a)); root = c0 + d * k
        chains[name] = (root, tip)
    index_front = min(sum(P[i].y for i in clusters[0]) / len(clusters[0]), 0) if False else None
    # thumb: hand vertices in front of the index line, before the knuckles
    iroot, itip = chains['Index']; idir = (itip - iroot).normalized()
    front = Vector((0, -1, 0))
    T = []
    for i in H:
        if not (.12 * smax < s[i] < .62 * smax): continue
        rel = P[i] - iroot; off = rel - idir * rel.dot(idir)
        if off.dot(front) > .012 or (P[i] - wrist).dot(n) > .03: T.append(i)
    far = sorted(T, key=lambda i: (P[i] - wrist).length, reverse=True)
    tip = sum((P[i] for i in far[:max(3, len(far) // 20)]), Vector()) / max(3, len(far) // 20)
    near = sorted(T, key=lambda i: (P[i] - wrist).length)[:max(3, len(T) // 6)]
    base = sum((P[i] for i in near), Vector()) / len(near)
    base = base.lerp(wrist + a * .25 * smax, .35)
    chains['Thumb'] = (base, tip)
    report.append('%s hand: verts %d fingers %s thumb verts %d' % (side, len(H), [len(c) for c in clusters], len(T)))
    # joints for the three phalanges
    for name, (root, tip) in chains.items():
        fr = (0, .4, .72, 1.0) if name == 'Thumb' else (0, .45, .75, 1.0)
        pts = [root.lerp(tip, f) for f in fr]
        for k in range(3): joint['%s%d%s' % (name, k + 1, side)] = (pts[k], pts[k + 1])
    # hand bone ends at the middle knuckle
    joint[hand] = (wrist, chains['Middle'][0])
    # skin: past the knuckle line every hand vertex follows its nearest finger; thumb vertices follow the thumb
    Tset = set(T)
    def seg_param(name, p):
        root, tip = chains[name]; seg = tip - root; L2 = seg.length_squared; t = (p - root).dot(seg) / L2
        return t, (p - root.lerp(tip, min(1, max(0, t)))).length
    for i in range(len(P)):
        wh = W[i].get(hand, 0)
        if wh <= 0: continue
        si = (P[i] - wrist).dot(a)
        tt, td = seg_param('Thumb', P[i])
        if i in Tset or (td < .02 and tt > .15 and si < s_root):
            name, t = 'Thumb', tt; f = min(1, max(0, (t + .05) / .25))
        elif si > s_root - .015:
            name = min(FINGERS[1:], key=lambda nm: seg_param(nm, P[i])[1]); t = seg_param(name, P[i])[0]
            f = min(1, max(0, (si - (s_root - .015)) / .03))
        else: continue
        if f <= 0: continue
        centres = (.2, .58, .87); phal = [0, 0, 0]
        if t <= centres[0]: phal[0] = 1
        elif t >= centres[2]: phal[2] = 1
        else:
            k = 0 if t < centres[1] else 1
            u = (t - centres[k]) / (centres[k + 1] - centres[k]); phal[k] = 1 - u; phal[k + 1] = u
        W[i][hand] = wh * (1 - f)
        for k in range(3):
            if phal[k] > 0:
                bn = '%s%d%s' % (name, k + 1, side); W[i][bn] = W[i].get(bn, 0) + wh * f * phal[k]
build_hand('L'); build_hand('R')

# cartoon hands reduced to the cast's proportions, around the wrist
for side in 'LR':
    wrist = joint['Hand' + side][0]
    for i, d in enumerate(W):
        wh = sum(w for b, w in d.items() if b == 'Hand' + side or (b.startswith(tuple(FINGERS)) and b.endswith(side)))
        if wh > 0: P[i] = wrist + (P[i] - wrist) * (1 - (1 - HAND_SCALE) * wh)
    for k in list(joint):
        if k == 'Hand' + side or (k.startswith(tuple(FINGERS)) and k.endswith(side)):
            a, b = joint[k]; joint[k] = (wrist + (a - wrist) * HAND_SCALE, wrist + (b - wrist) * HAND_SCALE)

# normalise, keep 4 strongest influences
for i, d in enumerate(W):
    top = sorted(d.items(), key=lambda x: -x[1])[:4]; t = sum(w for _, w in top)
    W[i] = {b: w / t for b, w in top} if t > 0 else {'Hips': 1.0}

# ---------------------------------------------------------------- write back mesh + armature
for i, v in enumerate(me.vertices): v.co = P[i]
ob.vertex_groups.clear()
names = list(joint.keys())
for n in names: ob.vertex_groups.new(name=n)
for i, d in enumerate(W):
    for b, w in d.items(): ob.vertex_groups[b].add([i], w, 'REPLACE')

PARENT = {'Root': None, 'Hips': 'Root', 'Spine': 'Hips', 'Spine2': 'Spine', 'Chest': 'Spine2', 'Neck': 'Chest', 'Head': 'Neck'}
for side in 'LR':
    PARENT.update({'Shoulder' + side: 'Chest', 'UpperArm' + side: 'Shoulder' + side, 'Forearm' + side: 'UpperArm' + side,
                   'Hand' + side: 'Forearm' + side, 'Thigh' + side: 'Hips', 'Shin' + side: 'Thigh' + side,
                   'Foot' + side: 'Shin' + side, 'Toe' + side: 'Foot' + side})
    for f in FINGERS:
        PARENT['%s1%s' % (f, side)] = 'Hand' + side
        PARENT['%s2%s' % (f, side)] = '%s1%s' % (f, side); PARENT['%s3%s' % (f, side)] = '%s2%s' % (f, side)
ad = bpy.data.armatures.new('ProtagonistRig'); rig = bpy.data.objects.new('ProtagonistRig', ad)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode='EDIT')
for n in names:
    eb = ad.edit_bones.new(n); eb.head, eb.tail = joint[n]
    if (eb.tail - eb.head).length < .005: eb.tail = eb.head + Vector((0, 0, .02))
for n in names:
    if PARENT[n]: ad.edit_bones[n].parent = ad.edit_bones[PARENT[n]]
bpy.ops.object.mode_set(mode='OBJECT')
ob.parent = rig
mod = ob.modifiers.new('Armature', 'ARMATURE'); mod.object = rig

# ---------------------------------------------------------------- decimate, protecting hands and face
keep = ob.vertex_groups.new(name='_keep')
for i, d in enumerate(W):
    # hands are always in view; neck, chest, shoulders and upper arms sit ~20 cm under the first-person camera
    k = sum(w for b, w in d.items() if b.startswith(('Hand',) + tuple(FINGERS))) + HEAD_KEEP * d.get('Head', 0)         + .5 * sum(w for b, w in d.items() if b in ('Neck', 'Chest') or b.startswith(('Shoulder', 'UpperArm')))
    if k > 0: keep.add([i], min(1, k), 'REPLACE')
tri0 = sum(len(p.vertices) - 2 for p in me.polygons)
dec = ob.modifiers.new('dec', 'DECIMATE'); dec.ratio = TRIS / tri0; dec.use_collapse_triangulate = True
dec.vertex_group = '_keep'; dec.vertex_group_factor = float(os.environ.get('ELIAS_KEEP', '1.5')); dec.invert_vertex_group = True
bpy.context.view_layer.objects.active = ob
while ob.modifiers[0].name != 'dec': bpy.ops.object.modifier_move_up(modifier='dec')
bpy.ops.object.modifier_apply(modifier='dec')
ob.vertex_groups.remove(ob.vertex_groups['_keep'])
me = ob.data

# ---------------------------------------------------------------- flat palette
img = next(i for i in bpy.data.images if i.size[0] > 0)
IW, IH = img.size; px = img.pixels[:]
def sample(u, v):
    x = min(IW - 1, max(0, int((u % 1) * IW))); y = min(IH - 1, max(0, int((v % 1) * IH))); k = (y * IW + x) * 4
    return (px[k], px[k + 1], px[k + 2])
gidx = {g.index: g.name for g in ob.vertex_groups}
def dominant(poly):
    acc = Counter()
    for vi in poly.vertices:
        for g in me.vertices[vi].groups: acc[gidx[g.group]] += g.weight
    return acc.most_common(1)[0][0]
def region(bone):
    if bone == 'Head': return 'head'
    if bone == 'Neck': return 'neck'
    if bone.startswith(tuple(FINGERS)) or bone.startswith('Hand'): return 'hand'
    if bone.startswith('Forearm'): return 'forearm'
    if bone.startswith('UpperArm'): return 'upperarm'
    if bone.startswith(('Thigh', 'Shin')): return 'leg'
    if bone.startswith(('Foot', 'Toe')): return 'foot'
    return 'torso'
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
uvl = bm.loops.layers.uv.active
regions = [region(dominant(me.polygons[f.index])) for f in bm.faces]
def face_col(f):
    uvs = [l[uvl].uv for l in f.loops]; c = sum(uvs, Vector((0, 0))) / len(uvs)
    cols = [sample(p.x, p.y) for p in [c] + [c.lerp(q, .55) for q in uvs]]
    return tuple(sorted(ch)[len(ch) // 2] for ch in zip(*cols))
fcol = [face_col(f) for f in bm.faces]; area = [f.calc_area() for f in bm.faces]
def lum(c): return .3 * c[0] + .59 * c[1] + .11 * c[2]
def kmeans(items, k, iters=30):
    random.seed(7); cent = [c for c, _ in random.sample(items, k)]
    for _ in range(iters):
        acc = [[0, 0, 0, 0] for _ in cent]
        for c, a in items:
            j = min(range(len(cent)), key=lambda j: sum((c[t] - cent[j][t]) ** 2 for t in range(3)))
            for t in range(3): acc[j][t] += c[t] * a
            acc[j][3] += a
        cent = [tuple(s[t] / s[3] for t in range(3)) if s[3] > 0 else cent[j] for j, s in enumerate(acc)]
    return cent
# per-region palettes: each body part keeps only its own dominant materials
REG_K = {'head': 8, 'neck': 2, 'hand': 2, 'forearm': 3, 'upperarm': 2, 'torso': 3, 'leg': 2, 'foot': 2}
REG_MIN = {'head': .002, 'neck': .15, 'hand': .2, 'forearm': .15, 'upperarm': .25, 'torso': .12, 'leg': .2, 'foot': .2}
palette = []; label = [0] * len(fcol)
for reg, k in REG_K.items():
    ids = [i for i, r in enumerate(regions) if r == reg]
    if not ids: continue
    cent = kmeans([(fcol[i], area[i] * (6 if reg == 'head' and (lum(fcol[i]) > .5 or lum(fcol[i]) < .09) else 1)) for i in ids], min(k, len(ids)))
    lab = {i: min(range(len(cent)), key=lambda j: sum((fcol[i][t] - cent[j][t]) ** 2 for t in range(3))) for i in ids}
    tot = sum(area[i] for i in ids); share = Counter()
    for i in ids: share[lab[i]] += area[i] / tot
    allowed = [j for j in range(len(cent)) if share[j] >= REG_MIN[reg]] or [share.most_common(1)[0][0]]
    for i in ids:
        if lab[i] not in allowed:
            lab[i] = min(allowed, key=lambda j: sum((fcol[i][t] - cent[j][t]) ** 2 for t in range(3)))
    # neighbour vote inside the region (stains, rips, stray triangles)
    nb = {i: [g.index for e in bm.faces[i].edges for g in e.link_faces if g.index != i and regions[g.index] == reg] for i in ids}
    for _ in range(0 if reg == 'head' else 3):
        new = dict(lab)
        for i in ids:
            cnt = Counter({lab[i]: area[i] * 1.3})
            for j in nb[i]: cnt[lab[j]] += area[j]
            new[i] = cnt.most_common(1)[0][0]
        lab = new
    base = len(palette); palette += cent
    for i in ids: label[i] = base + lab[i]
# merge near-identical swatches across regions (shared skin, shirt...)
merged = []; remap = {}
for j, c in enumerate(palette):
    for m, d in enumerate(merged):
        if sum((c[t] - d[t]) ** 2 for t in range(3)) < .0025: remap[j] = m; break
    else: remap[j] = len(merged); merged.append(c)
label = [remap[j] for j in label]; palette = merged
used = sorted(set(label)); compact = {j: k for k, j in enumerate(used)}; label = [compact[j] for j in label]; palette = [palette[j] for j in used]
# lift the painted-in shading a little so the swatches read like the cast atlas
palette = [tuple(min(1, c[t] * 1.12 + .02) for t in range(3)) for c in palette]
# the source skin is a saturated orange; settle it on a natural tan with the cast's saturation
TAN = (.80, .60, .49)
def skin_like(c): return c[0] > .45 and c[0] > c[1] * 1.25 and c[1] > c[2] and c[1] > .25
palette = [tuple(c[t] * .45 + TAN[t] * .55 * (sum(c) / sum(TAN)) for t in range(3)) if skin_like(c) else c for c in palette]

CELLS = 8; CS = 8; SIZE = CELLS * CS
pal_img = bpy.data.images.new('EliasV3_Palette', SIZE, SIZE, alpha=False)
pix = [0.0] * (SIZE * SIZE * 4)
for j, c in enumerate(palette):
    cx, cy = j % CELLS, j // CELLS
    for y in range(cy * CS, cy * CS + CS):
        for x in range(cx * CS, cx * CS + CS):
            k = (y * SIZE + x) * 4; pix[k:k + 4] = [c[0], c[1], c[2], 1.0]
pal_img.pixels = pix
for f in bm.faces:
    j = label[f.index]; u = ((j % CELLS) * CS + CS / 2) / SIZE; v = ((j // CELLS) * CS + CS / 2) / SIZE
    for lp in f.loops: lp[uvl].uv = (u, v)
    f.smooth = False
bm.to_mesh(me); bm.free()
while len(me.uv_layers) > 1: me.uv_layers.remove(me.uv_layers[1])
mat = bpy.data.materials.new('EliasV3'); mat.use_nodes = True
tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = pal_img; tex.interpolation = 'Closest'
mat.node_tree.links.new(tex.outputs[0], next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED').inputs[0])
me.materials.clear(); me.materials.append(mat)

# ---------------------------------------------------------------- split head
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
hg = ob.vertex_groups['Head'].index
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT'); bpy.ops.object.mode_set(mode='OBJECT')
for p in me.polygons:
    p.select = all(sum(g.weight for g in me.vertices[v].groups if g.group == hg) > .5 for v in p.vertices)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.separate(type='SELECTED'); bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.data.objects if o.type == 'MESH']
head = max(parts, key=lambda o: min(v.co.z for v in o.data.vertices)); body = next(o for o in parts if o != head)
head.name = head.data.name = 'ProtagonistHead'; body.name = body.data.name = 'ProtagonistBody'
# close the neck where the head was split off: in first person the camera looks down at the collar,
# and the open tube (seen from inside through the two-sided first-person material) reads as broken geometry
bm = bmesh.new(); bm.from_mesh(body.data); uvl = bm.loops.layers.uv.active
neck_z = joint['Neck'][0].z
ring = [e for e in bm.edges if e.is_boundary and all(v.co.z > neck_z - .03 and abs(v.co.x) < .2 for v in e.verts)]
caps = bmesh.ops.holes_fill(bm, edges=ring, sides=0)['faces']
if caps:
    caps = bmesh.ops.triangulate(bm, faces=caps)['faces']
    for f in caps:
        src = next((g for e in f.edges for g in e.link_faces if g not in caps), None)
        if src is not None:
            for lp in f.loops: lp[uvl].uv = src.loops[0][uvl].uv
        f.smooth = False
    bmesh.ops.recalc_face_normals(bm, faces=caps)
report.append('neck cap: %d boundary edges, %d faces' % (len(ring), len(caps)))
bm.to_mesh(body.data); bm.free()
for o in (head, body):
    for g in [g for g in o.vertex_groups]:
        if not any(g.index == x.group for v in o.data.vertices for x in v.groups): o.vertex_groups.remove(g)
tris = sum(len(p.vertices) - 2 for o in (head, body) for p in o.data.polygons)
report.append('triangles %d (body %d, head %d), palette %d colours, scale %.3f' % (
    tris, sum(len(p.vertices) - 2 for p in body.data.polygons), sum(len(p.vertices) - 2 for p in head.data.polygons), len(palette), scale))

pal_img.filepath_raw = os.path.join(GEN, 'EliasV3_Palette.png'); pal_img.file_format = 'PNG'; pal_img.save()
if EXPORT:
    pal_img.filepath_raw = os.path.join(OUT_DIR, 'EliasV3_Palette.png'); pal_img.save()
    bpy.ops.object.select_all(action='DESELECT')
    for o in (rig, head, body): o.select_set(True)
    for path in ('Elias_Rigged.fbx', 'EliasHumanoid.fbx'):
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, path), use_selection=True, object_types={'MESH', 'ARMATURE'},
                                 add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
    report.append('exported to ' + OUT_DIR)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(GEN, 'Elias_v3.blend'))

# ---------------------------------------------------------------- previews
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
def shot(name, loc, look, lens=50, res=(900, 1100)):
    sc.render.resolution_x, sc.render.resolution_y = res; cam.data.lens = lens; cam.location = Vector(loc)
    cam.rotation_euler = (Vector(look) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(GEN, name); bpy.ops.render.render(write_still=True)
shot('v3_front.png', (0, -4.6, 1.0), (0, 0, .92))
shot('v3_threequarter.png', (2.6, -3.8, 1.2), (0, 0, .92))
shot('v3_face.png', (.35, -1.2, 1.62), (0, 0, 1.6), 85, (900, 900))
shot('v3_hand.png', (.78, -.25, 2.0), (.78, -.02, 1.35), 85, (900, 700))
# pose test: bend elbows, curl fingers, step
pb = rig.pose.bones
for side, sgn in (('L', 1), ('R', -1)):
    pb['Forearm' + side].rotation_mode = 'XYZ'; pb['Forearm' + side].rotation_euler = (0, 0, math.radians(70) * sgn)
    pb['UpperArm' + side].rotation_mode = 'XYZ'; pb['UpperArm' + side].rotation_euler = (math.radians(60), 0, 0)
    for f in FINGERS:
        for k in (1, 2, 3):
            b = pb['%s%d%s' % (f, k, side)]; b.rotation_mode = 'XYZ'; b.rotation_euler = (math.radians(55 if f != 'Thumb' else 25), 0, 0)
pb['ThighL'].rotation_mode = 'XYZ'; pb['ThighL'].rotation_euler = (math.radians(-35), 0, 0)
pb['ShinL'].rotation_mode = 'XYZ'; pb['ShinL'].rotation_euler = (math.radians(50), 0, 0)
shot('v3_posed.png', (2.6, -3.8, 1.2), (0, 0, .92))
bpy.context.view_layer.update()
hp = rig.matrix_world @ pb['Middle2L'].head
shot('v3_posed_hand.png', tuple(hp + Vector((.25, -.45, .2))), tuple(hp), 70, (900, 700))
shot('v3_posed_hand_b.png', tuple(hp + Vector((.35, .1, -.35))), tuple(hp), 70, (900, 700))
for line in report: print('ELIAS V3', line)
