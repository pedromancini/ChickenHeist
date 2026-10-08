# Elias v4: the AI-generated protagonist (source/Elias_v4_source.glb: one 987k-triangle mesh in T-pose, 4K
# colour texture, no skeleton) rebuilt on the ProtagonistRig bone convention so EliasNativeInstall and
# MixamoInstall can bake every gameplay and cinematic clip for it.
#   - joints located on the mesh itself (T-pose): spine, shoulders, elbows, wrists, five three-phalanx fingers,
#     hips, knees, ankles, toes; skin weights from Blender's bone heat, unweighted leftovers to the nearest bone
#   - head and hands brought towards the cast's proportions, body scaled to HEIGHT with feet on the ground
#   - reduced to about TRIS triangles, keeping hands and face (seen up close in first person and cinematics)
#   - the painted texture is kept (eyes, brows and beard live there), downsized to TEX pixels; a bluish smudge
#     at the sides of the beard is recoloured from the beard itself
#   - head split from the body (hidden in first person) and the neck closed
# Run:  blender -b --factory-startup --python build_elias_v4.py               (previews in generated/)
#       ELIAS_EXPORT=1 blender -b --factory-startup --python build_elias_v4.py  (also writes the FBX and texture)
import bpy, bmesh, os, math
import numpy as np
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(ROOT, '..', '..', '..'))
SOURCE = os.path.join(ROOT, 'source', 'Elias_v4_source.glb')
OUT_DIR = os.path.join(PROJECT, 'Assets', 'ChickenHeistGenerated', 'Characters', 'EliasNative')
GEN = os.path.join(ROOT, 'generated'); os.makedirs(GEN, exist_ok=True)
EXPORT = os.environ.get('ELIAS_EXPORT') == '1'
HEAD_SCALE = float(os.environ.get('ELIAS_HEAD_SCALE', '0.74'))
ARM_STRETCH = float(os.environ.get('ELIAS_ARM_STRETCH', '1.10'))   # the generated arms are cartoon-short
HAND_SCALE = float(os.environ.get('ELIAS_HAND_SCALE', '0.92'))
HEIGHT = float(os.environ.get('ELIAS_HEIGHT', '1.74'))
TRIS = int(os.environ.get('ELIAS_TRIS', '22000'))
TEX = int(os.environ.get('ELIAS_TEX', '2048'))
report = []
def log(*a): report.append(' '.join(str(x) for x in a)); print('ELIAS V4', *a)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SOURCE)
ob = next(o for o in bpy.data.objects if o.type == 'MESH')
for o in [o for o in bpy.data.objects if o.type != 'MESH']: bpy.data.objects.remove(o)
mw = ob.matrix_world.copy(); ob.parent = None; ob.data.transform(mw); ob.matrix_world = Matrix.Identity(4)
me = ob.data
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=float(os.environ.get("ELIAS_WELD", "5e-5"))); bm.to_mesh(me); bm.free()

def coords():
    a = np.empty(len(me.vertices) * 3, dtype=np.float64); me.vertices.foreach_get('co', a); return a.reshape(-1, 3)
def store(P): me.vertices.foreach_set('co', P.astype(np.float32).ravel()); me.update()
P = coords()
X, Y, Z = P[:, 0], P[:, 1], P[:, 2]
z0, z1 = Z.min(), Z.max(); H0 = z1 - z0; half = np.abs(X).max()
log('source verts', len(P), 'height', round(H0, 3), 'half span', round(half, 3))

# ---------------------------------------------------------------- landmarks on the T-pose (+x = character's left, front = -y)
def band(mask): return P[mask]
outer = np.abs(X) > .6 * half
arm_z = float(np.median(Z[outer])); arm_band = np.abs(Z - arm_z) < .07 * H0
def arm_centre(side, x):
    m = arm_band & (np.abs(X - side * x) < .012 * H0)
    q = P[m]; return Vector((side * x, (q[:, 1].min() + q[:, 1].max()) / 2, (q[:, 2].min() + q[:, 2].max()) / 2))
def y_extent(side, x):
    m = arm_band & (np.abs(X - side * x) < .006 * H0)
    return (P[m][:, 1].max() - P[m][:, 1].min()) if m.sum() > 8 else 1e9
chest_z = arm_z - .11 * H0
chest = P[(np.abs(Z - chest_z) < .006 * H0) & (np.abs(X) < .5 * half)]
torso_half = np.abs(chest[:, 0]).max(); torso_y = (chest[:, 1].min() + chest[:, 1].max()) / 2
# neck: narrowest slice between the shoulders and the top of the head
best = None
for z in np.linspace(arm_z + .02 * H0, arm_z + .14 * H0, 40):
    s = P[(np.abs(Z - z) < .004 * H0) & (np.abs(X) < .3 * half)]
    if len(s) < 10: continue
    w = s[:, 0].max() - s[:, 0].min()
    if best is None or w < best[0]: best = (w, z, (s[:, 1].min() + s[:, 1].max()) / 2)
neck_w, neck_z, neck_y = best
# crotch: lowest body point between the legs
mid = P[(np.abs(X) < .012 * H0) & (Z > z0 + .25 * H0) & (Z < arm_z)]
crotch_z = mid[:, 2].min()
log('arm z', round(arm_z - z0, 3), 'torso half', round(torso_half, 3), 'neck z', round(neck_z - z0, 3), 'crotch z', round(crotch_z - z0, 3))

def leg_centre(side, z):
    m = (np.abs(Z - z) < .006 * H0) & (side * X > .004 * H0) & (np.abs(X) < .35 * half)
    q = P[m]; return Vector(((q[:, 0].min() + q[:, 0].max()) / 2, (q[:, 1].min() + q[:, 1].max()) / 2, z))

joint = {}
hip_z = crotch_z + .045 * H0
neck_base = arm_z + .03 * H0
spine = lambda f: Vector((0, torso_y, hip_z + (neck_base - hip_z) * f))
joint['Hips'] = (spine(0), spine(.25)); joint['Spine'] = (spine(.25), spine(.5)); joint['Spine2'] = (spine(.5), spine(.75))
joint['Chest'] = (spine(.75), Vector((0, neck_y, neck_base)))
head_pivot = Vector((0, neck_y, neck_z + .015 * H0))
joint['Neck'] = (Vector((0, neck_y, neck_base)), head_pivot)
wrist_x = {}
for side, sd in ((1, 'L'), (-1, 'R')):
    xs = np.linspace(.62 * half, .9 * half, 60)
    wx = float(xs[int(np.argmin([y_extent(side, x) for x in xs]))]); wrist_x[sd] = wx
    sx = torso_half * .9; ex = sx + (wx - sx) * .5
    sh, el, wr = arm_centre(side, sx), arm_centre(side, ex), arm_centre(side, wx)
    joint['Shoulder' + sd] = (Vector((side * .03 * H0, neck_y, neck_base - .01 * H0)), sh)
    joint['UpperArm' + sd] = (sh, el); joint['Forearm' + sd] = (el, wr)
    joint['Hand' + sd] = (wr, Vector((side * half, wr.y, wr.z)))
    th = leg_centre(side, crotch_z - .03 * H0); th.z = crotch_z + .02 * H0
    ankle_z = z0 + .055 * H0; an = leg_centre(side, ankle_z)
    kn = leg_centre(side, ankle_z + (th.z - ankle_z) * .47)
    foot = P[(Z < z0 + .03 * H0) & (side * X > 0) & (np.abs(X) < .35 * half)]
    tip_y = foot[:, 1].min()
    toe = Vector((an.x, an.y + (tip_y - an.y) * .65, z0 + .02 * H0)); tip = Vector((an.x, tip_y, z0 + .02 * H0))
    joint['Thigh' + sd] = (th, kn); joint['Shin' + sd] = (kn, an); joint['Foot' + sd] = (an, toe); joint['Toe' + sd] = (toe, tip)
    log(sd, 'wrist x', round(wx, 3), 'shoulder', tuple(round(v, 3) for v in sh), 'hip', tuple(round(v, 3) for v in th))

# ---------------------------------------------------------------- proportions: head and hands
def smooth(t): t = np.clip(t, 0, 1); return t * t * (3 - 2 * t)
wh = smooth((Z - (neck_z - .01 * H0)) / (.035 * H0))
f = 1 - (1 - HEAD_SCALE) * wh
pv = np.array(head_pivot)
P = pv + (P - pv) * f[:, None]
for sd, side in (('L', 1), ('R', -1)):
    wr = np.array(joint['Hand' + sd][0])
    wgt = smooth((side * P[:, 0] - (wrist_x[sd] - .01 * H0)) / (.02 * H0)) * (np.abs(P[:, 2] - arm_z) < .12 * H0)
    P = P + (wr + (P - wr) * HAND_SCALE - P) * wgt[:, None]
    a, b = joint['Hand' + sd]; joint['Hand' + sd] = (a, a + (b - a) * HAND_SCALE)
# arms: stretch everything beyond the shoulder outward along x (T-pose), ramping in over the shoulder
for sd, side in (('L', 1), ('R', -1)):
    sx = side * joint['UpperArm' + sd][0].x
    wgt = smooth((side * P[:, 0] - sx) / .03) * (np.abs(P[:, 2] - arm_z) < .16 * H0)
    off = np.maximum(side * P[:, 0] - sx, 0) * (ARM_STRETCH - 1) * wgt
    P[:, 0] += side * off
    for k in [k for k in joint if k.endswith(sd) and k.startswith(('UpperArm', 'Forearm', 'Hand'))]:
        a_, b_ = joint[k]
        joint[k] = tuple(Vector((v.x + side * max(side * v.x - sx, 0) * (ARM_STRETCH - 1), v.y, v.z)) for v in (a_, b_))
    wrist_x[sd] = wrist_x[sd] + (wrist_x[sd] - sx) * (ARM_STRETCH - 1)
head_top = P[:, 2].max()
joint['Head'] = (head_pivot, Vector((0, neck_y, head_top - (head_top - head_pivot.z) * .25)))
# feet on the ground, HEIGHT overall
s = HEIGHT / (P[:, 2].max() - z0)
def S(v): return Vector((v[0] * s, v[1] * s, (v[2] - z0) * s))
P = np.stack([P[:, 0] * s, P[:, 1] * s, (P[:, 2] - z0) * s], axis=1)
joint = {k: (S(a), S(b)) for k, (a, b) in joint.items()}
joint['Root'] = (Vector((0, 0, 0)), joint['Hips'][0].copy())
store(P)
log('scale', round(s, 4))


# ---------------------------------------------------------------- fingers (three phalanges each)
FINGERS = ['Thumb', 'Index', 'Middle', 'Ring', 'Little']
def edges():
    e = np.empty(len(me.edges) * 2, dtype=np.int64); me.edges.foreach_get('vertices', e); return e.reshape(-1, 2)
EV = edges()
def components(idx):
    # connected pieces of the mesh restricted to the vertex set idx (union-find over the edges inside it)
    inside = np.zeros(len(P), bool); inside[idx] = True
    e = EV[inside[EV[:, 0]] & inside[EV[:, 1]]]
    parent = {int(i): int(i) for i in idx}
    def find(x):
        while parent[x] != x: parent[x] = parent[parent[x]]; x = parent[x]
        return x
    for a, b in e:
        ra, rb = find(int(a)), find(int(b))
        if ra != rb: parent[ra] = rb
    groups = {}
    for i in idx: groups.setdefault(find(int(i)), []).append(int(i))
    return sorted((np.array(g) for g in groups.values()), key=len, reverse=True)

def digits(sd, side):
    # Digits are separate geometry past the webbing, but each gap starts at its own depth: split recursively,
    # raising the cut only inside a piece that still holds several digits. The generated fingers are open
    # shells (upper and lower halves apart), so leaves come in pairs: merge leaves lying side by side.
    wrist = joint['Hand' + sd][0]; a = np.array((side, 0, 0)); w = np.array(wrist)
    H = np.where((side * P[:, 0] > side * wrist.x) & (np.abs(P[:, 2] - wrist.z) < .12))[0]
    sA = (P[H] - w) @ a; smax = sA.max(); along = dict(zip(H.tolist(), sA.tolist()))
    minsize = max(40, int(.004 * len(H)))
    def split(S, c0):
        for c in np.arange(c0 + .02, .97, .02):
            sub = np.array([i for i in S if along[int(i)] > c * smax])
            comps = [x for x in components(sub) if len(x) >= minsize] if len(sub) else []
            if len(comps) >= 2: return [leaf for x in comps for leaf in split(x, c)]
            if not comps: break
        return [(S, c0)]
    leaves = []
    for comp in components(H[sA > .3 * smax]):
        if len(comp) >= minsize: leaves += split(comp, .3)
    leaves = [(g, c, P[g].mean(axis=0)) for g, c in leaves]
    merged = []
    for g, c, m in sorted(leaves, key=lambda l: l[2][1]):
        if merged and abs(m[1] - merged[-1][2][1]) < .012 and abs(m[2] - merged[-1][2][2]) < .02:
            G, C, _ = merged[-1]; G = np.concatenate([G, g]); merged[-1] = (G, min(C, c), P[G].mean(axis=0))
        else: merged.append((g, c, m))
    out = [dict(idx=g, sep=c, centre=m, reach=max(along[int(i)] for i in g) / smax) for g, c, m in merged]
    for d in out: log(sd, 'digit', len(d['idx']), 'sep', round(float(d['sep']), 2), 'reach', round(d['reach'], 2), 'y', round(float(d['centre'][1] - w[1]), 3))
    return out, H, sA, smax

def remove_extra_finger(sd, side, ds, smax):
    # Six digits (an AI generation slip): drop the middle finger and slide the neighbours together.
    global P, EV
    thumb = min(ds, key=lambda d: d['reach']); fingers = sorted([d for d in ds if d is not thumb], key=lambda d: d['centre'][1])
    gone = fingers[2]; front, back = fingers[:2], fingers[3:]
    spacing = np.median([fingers[i + 1]['centre'][1] - fingers[i]['centre'][1] for i in range(len(fingers) - 1) if i not in (1, 2)])
    close = (back[0]['centre'][1] - front[-1]['centre'][1]) - spacing
    base = gone['centre'].copy()
    wrist = np.array(joint['Hand' + sd][0]); a = np.array((side, 0, 0))
    sep = min(d['sep'] for d in fingers) * smax
    # slide: front fingers back by half the gap, back fingers forward, fading out towards the knuckles
    sA = (P - wrist) @ a; ramp = np.clip((sA - (sep - .12 * smax)) / (.1 * smax), 0, 1) * (np.abs(P[:, 2] - wrist[2]) < .12) * (side * P[:, 0] > side * wrist[0])
    ramp[thumb['idx']] = 0
    mid = gone['centre'][1]
    shift = np.where(P[:, 1] < mid, close / 2, -close / 2) * ramp
    P = P.copy(); P[:, 1] += shift
    store(P)
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    doomed = set(int(i) for i in gone['idx'])
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in doomed], context='VERTS')
    # close the stump where the finger met the palm (only boundary edges near its base)
    stump = Vector(base); stump.x = wrist[0] + side * sep
    ring = [e for e in bm.edges if e.is_boundary and all((v.co - stump).length < .035 for v in e.verts)]
    # the shells are open, so close the stump with a fan from its centre
    caps = []
    if ring:
        rv = {v for e in ring for v in e.verts}; c = bm.verts.new(sum((v.co for v in rv), Vector()) / len(rv))
        for e in ring:
            try: caps.append(bm.faces.new((e.verts[0], e.verts[1], c)))
            except ValueError: pass
        bmesh.ops.recalc_face_normals(bm, faces=caps)
        for f in caps:
            if f.normal.dot(f.calc_center_median() - stump) < 0: f.normal_flip()
    uvl = bm.loops.layers.uv.active
    for f in caps:
        srcf = next((g for e in f.edges for g in e.link_faces if g not in caps), None)
        if srcf is not None and uvl is not None:
            for lp in f.loops: lp[uvl].uv = srcf.loops[0][uvl].uv
    bm.to_mesh(me); bm.free(); me.update()
    P = coords(); EV = edges()
    log(sd, 'extra finger removed:', len(doomed), 'verts; gap closed by', round(float(close), 3), 'm; stump edges', len(ring), 'cap faces', len(caps))

def fix_hand(sd, side):
    ds, H, sA, smax = digits(sd, side)
    if len(ds) == 6: remove_extra_finger(sd, side, ds, smax)
def build_hand(sd, side):
    ds, H, sA, smax = digits(sd, side)
    if len(ds) != 5: raise RuntimeError('%s hand: %d digits' % (sd, len(ds)))
    wrist = joint['Hand' + sd][0]; a = Vector((side, 0, 0))
    thumb = min(ds, key=lambda d: d['reach']); fingers = sorted([d for d in ds if d is not thumb], key=lambda d: d['centre'][1])
    chains = {}
    for name, d in zip(FINGERS, [thumb] + fingers):
        pts = P[d['idx']]; cen = pts.mean(axis=0); _, _, vt = np.linalg.svd(pts - cen); dv = Vector(vt[0])
        if dv.dot(Vector(cen) - wrist) < 0: dv = -dv
        al = (pts - cen) @ np.array(dv)
        tip = Vector(cen) + dv * float(al.max()); base = Vector(cen) + dv * float(al.min())
        root = base.lerp(wrist + a * .22 * smax, .45) if name == 'Thumb' else base - dv * .06 * smax
        chains[name] = (root, tip)
        fr = (0, .4, .72, 1.0) if name == 'Thumb' else (0, .45, .75, 1.0)
        q = [root.lerp(tip, t) for t in fr]
        for k in range(3): joint['%s%d%s' % (name, k + 1, sd)] = (q[k], q[k + 1])
    joint['Hand' + sd] = (wrist, chains['Middle'][0])
    DEBUG_PIECES.append((H, [d['idx'] for d in [thumb] + fingers]))
DEBUG_PIECES = []
fix_hand('L', 1); fix_hand('R', -1)
build_hand('L', 1); build_hand('R', -1)
if os.environ.get('ELIAS_DEBUG_HANDS') == '1':
    cols = [(1, .2, .2, 1), (.2, 1, .2, 1), (.2, .4, 1, 1), (1, 1, .2, 1), (1, .3, 1, 1), (.2, 1, 1, 1)]
    attr = me.color_attributes.new('dbg', 'FLOAT_COLOR', 'POINT'); data = np.tile([.5, .5, .5, 1], len(P)).astype(np.float32).reshape(-1, 4)
    for H_, pcs in DEBUG_PIECES:
        data[H_] = (.9, .8, .7, 1)
        for i, pc in enumerate(pcs): data[pc] = cols[i % len(cols)]
    attr.data.foreach_set('color', data.ravel())
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'VERTEX'; sc.display.shading.light = 'FLAT'
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    for sd in 'LR':
        hw = joint['Hand' + sd][0]; c = hw + Vector(((1 if sd == 'L' else -1) * .1, 0, 0))
        for nm, off in (('top', Vector((0, -.02, .5))), ('front', Vector((0, -.5, 0)))):
            cam.location = c + off; cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler(); cam.data.lens = 60
            sc.render.resolution_x, sc.render.resolution_y = 800, 600; sc.render.filepath = os.path.join(GEN, 'dbg_hand_%s_%s.png' % (sd, nm)); bpy.ops.render.render(write_still=True)
    raise SystemExit

# ---------------------------------------------------------------- reduce, keeping hands and face
keep = ob.vertex_groups.new(name='_keep')
k = np.zeros(len(P))
for sd, side in (('L', 1), ('R', -1)): k = np.maximum(k, (side * P[:, 0] > side * joint['Hand' + sd][0].x - .02) * .7)
# the forearm next to the wrist twists with the hand: keep enough rings there
for sd, side in (('L', 1), ('R', -1)):
    wx = joint['Hand' + sd][0].x
    k = np.maximum(k, ((side * P[:, 0] > side * wx - .14) & (side * P[:, 0] <= side * wx - .02) & (np.abs(P[:, 2] - joint['Hand' + sd][0].z) < .1)) * .6)
k = np.maximum(k, (P[:, 2] > joint['Neck'][1].z) * .2)
for val in np.unique(k):
    if val > 0: keep.add(np.where(k == val)[0].tolist(), float(val), 'REPLACE')
tri0 = sum(len(p.vertices) - 2 for p in me.polygons)
pre = ob.modifiers.new('pre', 'DECIMATE'); pre.ratio = min(1, TRIS * 4 / tri0); pre.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier='pre')
tri1 = sum(len(p.vertices) - 2 for p in me.polygons)
dec = ob.modifiers.new('dec', 'DECIMATE'); dec.ratio = TRIS / tri1; dec.use_collapse_triangulate = True
dec.vertex_group = '_keep'; dec.vertex_group_factor = float(os.environ.get('ELIAS_KEEP', '1.0')); dec.invert_vertex_group = True
bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier='dec')
ob.vertex_groups.remove(ob.vertex_groups['_keep']); me = ob.data
log('triangles', tri0, '->', sum(len(p.vertices) - 2 for p in me.polygons))

# ---------------------------------------------------------------- texture: downsized, beard-side smudge recoloured
src = bpy.data.images['Image_0'] if 'Image_0' in bpy.data.images else next(i for i in bpy.data.images if i.size[0] > 0)
IW, IH = src.size; pix = np.empty(IW * IH * 4, dtype=np.float32); src.pixels.foreach_get(pix); pix = pix.reshape(IH, IW, 4)
def sample(uv): x = min(IW - 1, max(0, int((uv[0] % 1) * IW))); y = min(IH - 1, max(0, int((uv[1] % 1) * IH))); return pix[y, x, :3]
bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table(); uvl = bm.loops.layers.uv.active
chin_z = joint['Head'][0].z
def centroid_uv(f): return sum((l[uvl].uv for l in f.loops), Vector((0, 0))) / len(f.loops)
head_faces = [f for f in bm.faces if f.calc_center_median().z > chin_z - .04]
def bluish(c): return c[2] > c[0] * 1.08 and c[2] > c[1] * 1.02 and c[2] > .12
beard = [f for f in head_faces if f.calc_center_median().y < joint['Head'][0].y and (lambda c: c[0] < .35 and c[0] > c[2] and not bluish(c))(sample(centroid_uv(f)))]
fixed = 0
if beard:
    for f in head_faces:
        if bluish(sample(centroid_uv(f))):
            ref = min(beard, key=lambda g: (g.calc_center_median() - f.calc_center_median()).length)
            uv = centroid_uv(ref)
            for l in f.loops: l[uvl].uv = uv
            fixed += 1
bm.to_mesh(me); bm.free()
log('bluish head faces recoloured', fixed)
tex = bpy.data.images.new('EliasV4_Albedo', IW, IH, alpha=False); tex.pixels.foreach_set(pix.ravel()); tex.scale(TEX, TEX)
mat = bpy.data.materials.new('EliasV4'); mat.use_nodes = True
tn = mat.node_tree.nodes.new('ShaderNodeTexImage'); tn.image = tex
bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'); bsdf.inputs['Roughness'].default_value = .85
mat.node_tree.links.new(tn.outputs[0], bsdf.inputs[0])
me.materials.clear(); me.materials.append(mat)
for p in me.polygons: p.use_smooth = False

# ---------------------------------------------------------------- armature and skin
PARENT = {'Root': None, 'Hips': 'Root', 'Spine': 'Hips', 'Spine2': 'Spine', 'Chest': 'Spine2', 'Neck': 'Chest', 'Head': 'Neck'}
for sd in 'LR':
    PARENT.update({'Shoulder' + sd: 'Chest', 'UpperArm' + sd: 'Shoulder' + sd, 'Forearm' + sd: 'UpperArm' + sd,
                   'Hand' + sd: 'Forearm' + sd, 'Thigh' + sd: 'Hips', 'Shin' + sd: 'Thigh' + sd,
                   'Foot' + sd: 'Shin' + sd, 'Toe' + sd: 'Foot' + sd})
    for fn in FINGERS:
        PARENT['%s1%s' % (fn, sd)] = 'Hand' + sd; PARENT['%s2%s' % (fn, sd)] = '%s1%s' % (fn, sd); PARENT['%s3%s' % (fn, sd)] = '%s2%s' % (fn, sd)
names = list(PARENT.keys())
ad = bpy.data.armatures.new('ProtagonistRig'); rig = bpy.data.objects.new('ProtagonistRig', ad)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode='EDIT')
for n in names:
    eb = ad.edit_bones.new(n); eb.head, eb.tail = joint[n]
    if (eb.tail - eb.head).length < .005: eb.tail = eb.head + Vector((0, 0, .02))
for n in names:
    if PARENT[n]: ad.edit_bones[n].parent = ad.edit_bones[PARENT[n]]
ad.edit_bones['Root'].use_deform = False
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
# vertices bone heat could not reach go to the nearest bone segment
gi = {g.index: g.name for g in ob.vertex_groups}
def seg_dist(p, a, b):
    ab = b - a; t = max(0, min(1, (p - a).dot(ab) / max(1e-9, ab.length_squared))); return (p - (a + ab * t)).length
orphans = 0
for v in me.vertices:
    if sum(g.weight for g in v.groups) < 1e-4:
        nb = min((n for n in names if n != 'Root'), key=lambda n: seg_dist(v.co, *joint[n]))
        ob.vertex_groups[nb].add([v.index], 1.0, 'REPLACE'); orphans += 1
# at most four influences, normalised
for v in me.vertices:
    gs = sorted(((g.group, g.weight) for g in v.groups), key=lambda x: -x[1])
    keepg = gs[:4]; t = sum(w for _, w in keepg) or 1
    for g, _ in gs[4:]: ob.vertex_groups[g].remove([v.index])
    for g, w in keepg: ob.vertex_groups[g].add([v.index], w / t, 'REPLACE')
log('vertices weighted by nearest bone', orphans)
# wrist: spread the forearm-to-hand blend over ~10 cm so a turned hand twists the skin gradually instead of
# folding it in one ring (the carry, steering and lockpick IK turn the hands a lot)
blended = 0
for sd in 'LR':
    el, wr = joint['Forearm' + sd]; axis = (wr - el).normalized()
    fa, hd = ob.vertex_groups['Forearm' + sd], ob.vertex_groups['Hand' + sd]
    for v in me.vertices:
        ws = {ob.vertex_groups[g.group].name: g.weight for g in v.groups if g.weight > 0}
        if not ws or set(ws) - {'Forearm' + sd, 'Hand' + sd}: continue
        t = (v.co - wr).dot(axis)
        if not -.12 < t < .04: continue
        if (v.co - (wr + axis * t)).length > .08: continue
        u = max(0.0, min(1.0, (t + .10) / .13)); u = u * u * (3 - 2 * u)
        fa.add([v.index], 1 - u, 'REPLACE'); hd.add([v.index], u, 'REPLACE'); blended += 1
log('wrist vertices re-blended', blended)

# ---------------------------------------------------------------- split head, close the neck
hg = ob.vertex_groups['Head'].index
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT'); bpy.ops.object.mode_set(mode='OBJECT')
for p in me.polygons:
    p.select = all(sum(g.weight for g in me.vertices[v].groups if g.group == hg) > .5 for v in p.vertices)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.separate(type='SELECTED'); bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.data.objects if o.type == 'MESH']
head = max(parts, key=lambda o: min(v.co.z for v in o.data.vertices)); body = next(o for o in parts if o != head)
head.name = head.data.name = 'ProtagonistHead'; body.name = body.data.name = 'ProtagonistBody'
bm = bmesh.new(); bm.from_mesh(body.data); uvl = bm.loops.layers.uv.active
neck_top = joint['Neck'][0].z
ring = [e for e in bm.edges if e.is_boundary and all(v.co.z > neck_top - .03 and abs(v.co.x) < .2 for v in e.verts)]
caps = bmesh.ops.holes_fill(bm, edges=ring, sides=0)['faces']
if caps:
    caps = bmesh.ops.triangulate(bm, faces=caps)['faces']
    for f in caps:
        srcf = next((g for e in f.edges for g in e.link_faces if g not in caps), None)
        if srcf is not None:
            for lp in f.loops: lp[uvl].uv = srcf.loops[0][uvl].uv
    bmesh.ops.recalc_face_normals(bm, faces=caps)
bm.to_mesh(body.data); bm.free()
log('neck cap', len(ring), 'edges', len(caps), 'faces')
for o in (head, body):
    used = {x.group for v in o.data.vertices for x in v.groups}
    for g in [g for g in o.vertex_groups if g.index not in used]: o.vertex_groups.remove(g)
tris = [sum(len(p.vertices) - 2 for p in o.data.polygons) for o in (body, head)]
log('triangles body', tris[0], 'head', tris[1])

tex.filepath_raw = os.path.join(GEN, 'EliasV4_Albedo.png'); tex.file_format = 'PNG'; tex.save()
if EXPORT:
    tex.filepath_raw = os.path.join(OUT_DIR, 'EliasV4_Albedo.png'); tex.save()
    bpy.ops.object.select_all(action='DESELECT')
    for o in (rig, head, body): o.select_set(True)
    for path in ('Elias_Rigged.fbx', 'EliasHumanoid.fbx'):
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, path), use_selection=True, object_types={'MESH', 'ARMATURE'},
                                 add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
    log('exported to', OUT_DIR)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(GEN, 'Elias_v4.blend'))

# ---------------------------------------------------------------- previews
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
def shot(name, loc, look, lens=50, res=(900, 1100)):
    sc.render.resolution_x, sc.render.resolution_y = res; cam.data.lens = lens; cam.location = Vector(loc)
    cam.rotation_euler = (Vector(look) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(GEN, name); bpy.ops.render.render(write_still=True)
shot('v4_front.png', (0, -4.6, 1.0), (0, 0, .92))
shot('v4_threequarter.png', (2.6, -3.8, 1.2), (0, 0, .92))
shot('v4_face.png', (.35, -1.2, 1.55), (0, 0, 1.52), 85, (900, 900))
hl = joint['HandL'][0]
shot('v4_hand.png', tuple(hl + Vector((.12, -.25, .55))), tuple(hl + Vector((.1, 0, 0))), 85, (900, 700))
# joints overlay (x-ray of the skeleton on the body)
sc.display.shading.show_xray = True; sc.display.shading.xray_alpha = .35
rig.show_in_front = True; ad.display_type = 'STICK'
shot('v4_joints.png', (0, -4.6, 1.0), (0, 0, .92))
shot('v4_joints_hand.png', tuple(hl + Vector((.12, -.05, .55))), tuple(hl + Vector((.1, 0, 0))), 85, (900, 700))
sc.display.shading.show_xray = False
# pose test: elbows, fingers curled, a step
pb = rig.pose.bones
for sd, sgn in (('L', 1), ('R', -1)):
    pb['Forearm' + sd].rotation_mode = 'XYZ'; pb['Forearm' + sd].rotation_euler = (0, 0, math.radians(70) * sgn)
    pb['UpperArm' + sd].rotation_mode = 'XYZ'; pb['UpperArm' + sd].rotation_euler = (math.radians(60), 0, 0)
    for fn in FINGERS:
        for k in (1, 2, 3):
            b = pb['%s%d%s' % (fn, k, sd)]; b.rotation_mode = 'XYZ'; b.rotation_euler = (math.radians(55 if fn != 'Thumb' else 25), 0, 0)
pb['ThighL'].rotation_mode = 'XYZ'; pb['ThighL'].rotation_euler = (math.radians(-35), 0, 0)
pb['ShinL'].rotation_mode = 'XYZ'; pb['ShinL'].rotation_euler = (math.radians(50), 0, 0)
bpy.context.view_layer.update()
shot('v4_posed.png', (2.6, -3.8, 1.2), (0, 0, .92))
hp = rig.matrix_world @ pb['Middle2L'].head
shot('v4_posed_hand.png', tuple(hp + Vector((.25, -.45, .2))), tuple(hp), 70, (900, 700))
shot('v4_posed_hand_b.png', tuple(hp + Vector((.35, .1, -.35))), tuple(hp), 70, (900, 700))
open(os.path.join(GEN, 'build_v4.txt'), 'w').write('\n'.join(report))

# wrist test: hand twisted and bent the way the carry and steering IK turn it
for k in list(pb.keys()) if False else []: pass
for b in rig.pose.bones: b.rotation_mode = 'XYZ'; b.rotation_euler = (0, 0, 0)
pb['HandL'].rotation_euler = (math.radians(40), math.radians(75), 0)
bpy.context.view_layer.update()
wp = rig.matrix_world @ pb['HandL'].head
shot('v4_wrist_twist.png', tuple(wp + Vector((.05, -.35, .12))), tuple(wp), 70, (900, 700))
shot('v4_wrist_twist_b.png', tuple(wp + Vector((.05, .3, -.2))), tuple(wp), 70, (900, 700))
