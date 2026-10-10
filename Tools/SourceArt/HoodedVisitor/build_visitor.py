# Hooded visitor (opening cinematic): the supplied AI model (Assets/c12cb2ea-...glb: one closed mesh of 98 thousand
# triangles in an A-pose, 2K colour texture, no skeleton) rigged on Elias' bone convention (ProtagonistRig names:
# Hips, Spine, Spine2, Chest, Neck, Head, Shoulder/UpperArm/Forearm/Hand, five fingers of three phalanges,
# Thigh/Shin/Foot/Toe) so the cinematic poses both actors with the same code and HoodedVisitorInstall bakes the
# visitor's clips from the same captured takes. Before this the static mesh was glued to a villager skeleton at run
# time with weights cut by height and distance (no fingers, joints off the mesh's own elbows and wrists).
#   - the A-pose is raised to a T-pose with the palms down (temporary weights from a closed proxy), as for Elias
#   - joints located on the mesh: spine, shoulders, elbows, wrists, five fingers, hips, knees, ankles, toes
#   - the original surface, UVs and texture are kept (the model is closed apart from a few pinholes); loose specks
#     are removed and the flat (faceted) shading of the source is kept
#   - skin weights from Blender's bone heat, then: the wrist blends over a short band (hand past it, forearm before
#     it), the hand follows the finger chains, the face mask follows the head
# Run:  blender -b --factory-startup --python build_visitor.py                 (previews in generated/)
#       VISITOR_EXPORT=1 blender -b --factory-startup --python build_visitor.py  (also writes the FBX files)
import bpy, bmesh, os, math
import numpy as np
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(ROOT, '..', '..', '..'))
SOURCE = os.path.join(PROJECT, 'Assets', 'c12cb2ea-013a-4d05-b832-3f5e1cf0ad28.glb')
OUT_DIR = os.path.join(PROJECT, 'Assets', 'ChickenHeistGenerated', 'Characters', 'HoodedVisitor')
GEN = os.path.join(ROOT, 'generated'); os.makedirs(GEN, exist_ok=True)
EXPORT = os.environ.get('VISITOR_EXPORT') == '1'
HEIGHT = float(os.environ.get('VISITOR_HEIGHT', '1.70'))
# wrist transition (metres along the forearm from the wrist joint): forearm only before START, hand only after END
WRIST_START, WRIST_END = (float(x) for x in os.environ.get('VISITOR_WRIST', '-0.035,0.015').split(','))
report = []
def log(*a): report.append(' '.join(str(x) for x in a)); print('VISITOR', *a)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SOURCE)
ob = next(o for o in bpy.data.objects if o.type == 'MESH')
for o in [o for o in bpy.data.objects if o.type != 'MESH']: bpy.data.objects.remove(o)
mw = ob.matrix_world.copy(); ob.parent = None; ob.data.transform(mw); ob.matrix_world = Matrix.Identity(4)
ob.name = ob.data.name = 'VisitorBody'
me = ob.data
# the source is flat shaded with every face split apart: weld it into one surface (the faceted look comes back from
# flat shading), and drop the loose specks (a few vertices each) that no bone could reach
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=5e-5); bm.to_mesh(me); bm.free()
if me.has_custom_normals: bpy.context.view_layer.objects.active = ob; bpy.ops.mesh.customdata_custom_splitnormals_clear()

def remove_scraps(mesh, smallest=60):
    bm_ = bmesh.new(); bm_.from_mesh(mesh); seen = set(); scraps = []
    for v in bm_.verts:
        if v in seen: continue
        stack = [v]; comp = []
        while stack:
            x = stack.pop()
            if x in seen: continue
            seen.add(x); comp.append(x); stack.extend(e.other_vert(x) for e in x.link_edges if e.other_vert(x) not in seen)
        if len(comp) < smallest: scraps += comp
    bmesh.ops.delete(bm_, geom=scraps, context='VERTS'); bm_.to_mesh(mesh); bm_.free(); mesh.update()
    return len(scraps)
log('loose specks removed:', remove_scraps(me), 'vertices; vertices', len(me.vertices))
def closed_copy(src_ob, voxel, tris, name):
    c = src_ob.copy(); c.data = src_ob.data.copy(); c.name = name; bpy.context.scene.collection.objects.link(c)
    c.data.remesh_voxel_size = voxel; c.data.remesh_voxel_adaptivity = 0
    bpy.ops.object.select_all(action='DESELECT'); c.select_set(True); bpy.context.view_layer.objects.active = c
    bpy.ops.object.voxel_remesh()
    m = c.modifiers.new('d', 'DECIMATE'); m.ratio = min(1, tris / len(c.data.polygons)); m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier='d')
    remove_scraps(c.data)
    return c
def coords():
    a = np.empty(len(me.vertices) * 3, dtype=np.float64); me.vertices.foreach_get('co', a); return a.reshape(-1, 3)
def store(P): me.vertices.foreach_set('co', P.astype(np.float32).ravel()); me.update()
P = coords()

# ---------------------------------------------------------------- A-pose to T-pose (same method as build_elias_v4.py)
def tpose():
    global P
    z0_ = P[:, 2].min(); H_ = P[:, 2].max() - z0_
    def clusters(f):
        m = np.abs(P[:, 2] - (z0_ + f * H_)) < .004 * H_
        idx = np.where(m)[0]; idx = idx[np.argsort(P[idx, 0])]
        if len(idx) < 10: return []
        return np.split(idx, np.where(np.diff(P[idx, 0]) > .01 * H_)[0] + 1)
    widths = [np.abs(P[c, 0]).max() for f in np.arange(.46, .62, .02) for c in clusters(f) if P[c, 0].min() < 0 < P[c, 0].max()]
    torso = float(np.median(widths))
    fit = {}
    for side in (1, -1):
        rows = []
        for f in np.arange(.78, .40, -.01):
            out = [c for c in clusters(f) if side * P[c, 0].mean() > torso * 1.05]
            if not out:
                if rows: break
                continue
            c = max(out, key=lambda c: side * P[c, 0].mean()) if len(out) == 1 else min(out, key=lambda c: side * P[c, 0].mean())
            rows.append((z0_ + f * H_, P[c, 0].mean(), P[c, 1].mean()))
        rows = rows[1:13]
        if len(rows) < 4: return 'no separate arm found: kept as is'
        zs, xs, ys = (np.array(r) for r in zip(*rows))
        b, a = np.polyfit(zs, xs, 1)
        theta = math.degrees(math.atan2(1, abs(b)))
        fit[side] = (a, b, theta, float(ys.mean()))
    if max(t for _, _, t, _ in fit.values()) < 12: return 'already a T-pose (arms %.1f / %.1f degrees down)' % (fit[1][2], fit[-1][2])
    yc = float(np.median(P[:, 1])); crotch = z0_ + .42 * H_; neck = z0_ + .80 * H_
    bones = {'Body': (Vector((0, yc, crotch)), Vector((0, yc, neck))), 'Head': (Vector((0, yc, neck)), Vector((0, yc, P[:, 2].max())))}
    for side, nm in ((1, 'L'), (-1, 'R')):
        leg = [c for c in clusters(.30) if side * P[c, 0].mean() > 0]
        lx = float(P[leg[0], 0].mean()) if leg else side * .06 * H_
        bones['Leg' + nm] = (Vector((lx, yc, crotch)), Vector((lx, yc, z0_ + .03 * H_)))
        a, b, theta, ay = fit[side]
        px = side * torso * 1.1; pivot = Vector((px, ay, (px - a) / b))
        d = Vector((-b, 0, -1)).normalized()
        arm_side = side * P[:, 0] > torso * 1.05
        reach = max(0.0, float(((P[arm_side] - np.array(pivot)) @ np.array(d)).max()))
        bones['Arm' + nm] = (pivot, pivot + d * reach)
    proxy = closed_copy(ob, .004 * H_, 40000, 'Visitor_pose_proxy')
    ad_ = bpy.data.armatures.new('PoseRig'); rig_ = bpy.data.objects.new('PoseRig', ad_); bpy.context.scene.collection.objects.link(rig_)
    bpy.context.view_layer.objects.active = rig_; bpy.ops.object.mode_set(mode='EDIT')
    for n, (h, t) in bones.items(): eb = ad_.edit_bones.new(n); eb.head, eb.tail = h, t
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); proxy.select_set(True); rig_.select_set(True); bpy.context.view_layer.objects.active = rig_
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    for n in bones:
        if ob.vertex_groups.get(n) is None: ob.vertex_groups.new(name=n)
    dt = ob.modifiers.new('pose weights', 'DATA_TRANSFER'); dt.object = proxy; dt.use_vert_data = True
    dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
    dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier='pose weights')
    for side, nm in ((1, 'L'), (-1, 'R')):
        pb = rig_.pose.bones['Arm' + nm]; pivot = bones['Arm' + nm][0]
        pb.matrix = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(-side * fit[side][2]), 4, 'Y') @ Matrix.Translation(-pivot) @ pb.bone.matrix_local
    bpy.context.view_layer.update()
    mod = ob.modifiers.new('pose', 'ARMATURE'); mod.object = rig_
    bpy.ops.object.modifier_apply(modifier='pose')
    for g in list(ob.vertex_groups): ob.vertex_groups.remove(g)
    bpy.data.objects.remove(proxy); bpy.data.objects.remove(rig_)
    P = coords()
    return 'raised arms %.1f / %.1f degrees (torso half-width %.3f)' % (fit[1][2], fit[-1][2], torso)
log('pose:', tpose())

# feet on the ground, HEIGHT overall (before the landmarks, so every threshold below is in metres)
z0 = P[:, 2].min(); s = HEIGHT / (P[:, 2].max() - z0)
P = np.stack([P[:, 0] * s, P[:, 1] * s, (P[:, 2] - z0) * s], axis=1); store(P)
log('scale', round(s, 4))
X, Y, Z = P[:, 0], P[:, 1], P[:, 2]
z0, z1 = Z.min(), Z.max(); H0 = z1 - z0; half = np.abs(X).max()

# ---------------------------------------------------------------- landmarks on the T-pose (+x = character's left, front = -y)
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
# The hood hides the neck, so its narrowest slice is not the neck: the neck sits at the cast's proportion, above
# the shoulders, centred front to back on the hood's opening.
neck_base = arm_z + .03 * H0
mid = P[(np.abs(X) < .012 * H0) & (Z > z0 + .25 * H0) & (Z < arm_z)]
crotch_z = mid[:, 2].min()
head_pivot_z = z0 + .868 * H0
ring = P[(np.abs(Z - head_pivot_z) < .01 * H0) & (np.abs(X) < .04)]
neck_y = float((ring[:, 1].min() + ring[:, 1].max()) / 2) if len(ring) > 8 else torso_y
log('arm z', round(arm_z - z0, 3), 'torso half', round(torso_half, 3), 'neck y', round(neck_y, 3), 'crotch z', round(crotch_z - z0, 3))
def leg_centre(side, z):
    m = (np.abs(Z - z) < .006 * H0) & (side * X > .004 * H0) & (np.abs(X) < .35 * half)
    q = P[m]; return Vector(((q[:, 0].min() + q[:, 0].max()) / 2, (q[:, 1].min() + q[:, 1].max()) / 2, z))
joint = {}
hip_z = crotch_z + .045 * H0
spine = lambda f: Vector((0, torso_y, hip_z + (neck_base - hip_z) * f))
joint['Hips'] = (spine(0), spine(.25)); joint['Spine'] = (spine(.25), spine(.5)); joint['Spine2'] = (spine(.5), spine(.75))
joint['Chest'] = (spine(.75), Vector((0, neck_y, neck_base)))
head_pivot = Vector((0, neck_y, head_pivot_z))
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
    log(sd, 'wrist x', round(wx, 3), 'shoulder', tuple(round(v, 3) for v in sh), 'elbow', tuple(round(v, 3) for v in el), 'hip', tuple(round(v, 3) for v in th))
head_top = z1
joint['Head'] = (head_pivot, Vector((0, neck_y, head_top - (head_top - head_pivot.z) * .25)))
joint['Root'] = (Vector((0, 0, 0)), joint['Hips'][0].copy())

# ---------------------------------------------------------------- fingers (three phalanges each)
FINGERS = ['Thumb', 'Index', 'Middle', 'Ring', 'Little']
def edges():
    e = np.empty(len(me.edges) * 2, dtype=np.int64); me.edges.foreach_get('vertices', e); return e.reshape(-1, 2)
EV = edges()
def components(idx):
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
    # Digits are separate geometry past the webbing, but each gap starts at its own depth: split recursively, raising
    # the cut only inside a piece that still holds several digits.
    wrist = joint['Hand' + sd][0]; a = np.array((side, 0, 0)); w = np.array(wrist)
    H = np.where((side * P[:, 0] > side * wrist.x) & (np.abs(P[:, 2] - wrist.z) < .12))[0]
    sA = (P[H] - w) @ a; smax = sA.max(); along = dict(zip(H.tolist(), sA.tolist()))
    minsize = max(12, int(.004 * len(H)))
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
    # each finger of the source is two shells (back and palm side) meeting along its sides: merge pieces lying one
    # over the other (overlapping side to side, a few millimetres apart in height)
    leaves = [(g, c, P[g].mean(axis=0)) for g, c in leaves]
    def overlap(g1, g2):
        a0, a1 = P[g1][:, 1].min(), P[g1][:, 1].max(); b0, b1 = P[g2][:, 1].min(), P[g2][:, 1].max()
        return max(0.0, min(a1, b1) - max(a0, b0)) / max(1e-6, min(a1 - a0, b1 - b0))
    merged = []
    for g, c, m in sorted(leaves, key=lambda l: l[2][1]):
        if merged and overlap(g, merged[-1][0]) > .6 and abs(m[2] - merged[-1][2][2]) < .02:
            G, C, _ = merged[-1]; G = np.concatenate([G, g]); merged[-1] = (G, min(C, c), P[G].mean(axis=0))
        else: merged.append((g, c, m))
    out = [dict(idx=g, sep=c, centre=m, reach=max(along[int(i)] for i in g) / smax) for g, c, m in merged]
    for d in out: log(sd, 'digit', len(d['idx']), 'sep', round(float(d['sep']), 2), 'reach', round(d['reach'], 2), 'y', round(float(d['centre'][1] - w[1]), 3), 'z', round(float(d['centre'][2] - w[2]), 3))
    return out, H, sA, smax
CHAINS = {}; DEBUG_PIECES = []
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
    CHAINS[sd] = dict(chains)
    DEBUG_PIECES.append((H, [d['idx'] for d in [thumb] + fingers]))
build_hand('L', 1); build_hand('R', -1)
if os.environ.get('VISITOR_DEBUG_HANDS') == '1':
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

# ---------------------------------------------------------------- armature and skin
PARENT = {'Root': None, 'Hips': 'Root', 'Spine': 'Hips', 'Spine2': 'Spine', 'Chest': 'Spine2', 'Neck': 'Chest', 'Head': 'Neck'}
for sd in 'LR':
    PARENT.update({'Shoulder' + sd: 'Chest', 'UpperArm' + sd: 'Shoulder' + sd, 'Forearm' + sd: 'UpperArm' + sd,
                   'Hand' + sd: 'Forearm' + sd, 'Thigh' + sd: 'Hips', 'Shin' + sd: 'Thigh' + sd,
                   'Foot' + sd: 'Shin' + sd, 'Toe' + sd: 'Foot' + sd})
    for fn in FINGERS:
        PARENT['%s1%s' % (fn, sd)] = 'Hand' + sd; PARENT['%s2%s' % (fn, sd)] = '%s1%s' % (fn, sd); PARENT['%s3%s' % (fn, sd)] = '%s2%s' % (fn, sd)
names = list(PARENT.keys())
ad = bpy.data.armatures.new('VisitorRig'); rig = bpy.data.objects.new('VisitorRig', ad)
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
gi = {g.index: g.name for g in ob.vertex_groups}
def seg_dist(p, a, b):
    ab = b - a; t = max(0, min(1, (p - a).dot(ab) / max(1e-9, ab.length_squared))); return (p - (a + ab * t)).length
orphans = 0
for v in me.vertices:
    if sum(g.weight for g in v.groups) < 1e-4:
        nb = min((n for n in names if n != 'Root'), key=lambda n: seg_dist(v.co, *joint[n]))
        ob.vertex_groups[nb].add([v.index], 1.0, 'REPLACE'); orphans += 1
log('vertices weighted by nearest bone (bone heat did not reach them)', orphans)
# The face mask is a separate piece in front of the face (from the bridge of the nose down over the throat): all
# of it follows the head, as a mask tied on would.
pieces = components(np.arange(len(me.vertices)))
mask = next((c for c in pieces[1:] if len(c) > 100 and P[c][:, 2].min() > head_pivot.z - .2 and np.abs(P[c][:, 0]).max() < .1), np.array([], int))
for vi in mask.tolist():
    for g in [g.group for g in me.vertices[vi].groups]: ob.vertex_groups[g].remove([vi])
    ob.vertex_groups['Head'].add([vi], 1.0, 'REPLACE')
log('face mask vertices on the head bone', len(mask), '(pieces:', [len(c) for c in pieces[:6]], ')')
# Wrist: past WRIST_END everything follows the hand and its fingers, before WRIST_START everything follows the
# forearm, and in between the two blend smoothly (bone heat leaves the base of the palm and the thumb on the forearm,
# which stays behind and stretches into spikes when the hand bends).
moved = [0, 0, 0]
for sd in 'LR':
    hand_bone = rig.data.bones['Hand' + sd]; inside = {b.name for b in hand_bone.children_recursive} | {hand_bone.name}
    el, wr = joint['Forearm' + sd]; axis = (wr - el).normalized()
    for v in me.vertices:
        t = (v.co - wr).dot(axis)
        if t < WRIST_START - .2 or (v.co - (wr + axis * t)).length > .15: continue
        ws = {ob.vertex_groups[g.group].name: g.weight for g in v.groups if g.weight > 0}
        if not ws: continue
        if t >= WRIST_END:
            out = sum(w for n, w in ws.items() if n not in inside)
            if out <= 0: continue
            keep = {n: w for n, w in ws.items() if n in inside}; keep['Hand' + sd] = keep.get('Hand' + sd, 0) + out; moved[0] += 1
        elif t <= WRIST_START:
            inn = sum(w for n, w in ws.items() if n in inside)
            if inn <= 0: continue
            keep = {n: w for n, w in ws.items() if n not in inside}; keep['Forearm' + sd] = keep.get('Forearm' + sd, 0) + inn; moved[1] += 1
        else:
            u = (t - WRIST_START) / (WRIST_END - WRIST_START); u = u * u * (3 - 2 * u)
            keep = {'Forearm' + sd: 1 - u, 'Hand' + sd: u}; moved[2] += 1
        for g in [g.group for g in v.groups]: ob.vertex_groups[g].remove([v.index])
        tot = sum(keep.values())
        for n, w in keep.items():
            if w > 1e-4: ob.vertex_groups[n].add([v.index], w / tot, 'REPLACE')
log('wrist: hand vertices freed from the forearm', moved[0], 'forearm vertices freed from the hand', moved[1], 'blended', moved[2])
# Hand: the fingers follow their own chains (bone heat mixes neighbouring fingers where they nearly touch)
def seg_param(root, tip, p):
    seg = tip - root; t = (p - root).dot(seg) / max(1e-9, seg.length_squared)
    return t, (p - root.lerp(tip, min(1, max(0, t)))).length
handset = 0
for sd, side in (('L', 1), ('R', -1)):
    ch = CHAINS[sd]; wrist = joint['Hand' + sd][0]; a = Vector((side, 0, 0))
    s_root = sum((ch[f][0] - wrist).dot(a) for f in FINGERS[1:]) / 4
    own = {ob.vertex_groups[n].index for n in ['Forearm' + sd, 'Hand' + sd]}
    for v in me.vertices:
        p = v.co
        if side * (p.x - wrist.x) < -.005 or abs(p.z - wrist.z) > .15 or abs(p.y - wrist.y) > .15: continue
        si = (p - wrist).dot(a)
        tt, td = seg_param(*ch['Thumb'], p)
        name = None
        if td < .016 and tt > .05:
            name, t = 'Thumb', tt; f = min(1, max(0, (tt - .05) / .2))
        elif si > s_root - .015:
            best = min(FINGERS[1:], key=lambda n: seg_param(*ch[n], p)[1]); name = best; t = seg_param(*ch[best], p)[0]
            f = min(1, max(0, (si - (s_root - .015)) / .025))
        base = {ob.vertex_groups[g.group].name: g.weight for g in v.groups if g.group in own and g.weight > 0}
        tot = sum(base.values())
        base = {k: w / tot for k, w in base.items()} if tot > 0 else {'Hand' + sd: 1.0}
        new = {k: w * (1 - (f if name else 0)) for k, w in base.items()}
        if name and f > 0:
            centres = (.2, .58, .87); ph = [0, 0, 0]
            if t <= centres[0]: ph[0] = 1
            elif t >= centres[2]: ph[2] = 1
            else:
                k = 0 if t < centres[1] else 1; u = (t - centres[k]) / (centres[k + 1] - centres[k]); ph[k] = 1 - u; ph[k + 1] = u
            for k in range(3):
                if ph[k] > 0: new['%s%d%s' % (name, k + 1, sd)] = new.get('%s%d%s' % (name, k + 1, sd), 0) + f * ph[k]
        for g in [g.group for g in v.groups]: ob.vertex_groups[g].remove([v.index])
        for k, w in new.items():
            if w > 1e-4: ob.vertex_groups[k].add([v.index], w, 'REPLACE')
        handset += 1
log('hand vertices weighted from finger chains', handset)
# at most four influences, normalised
for v in me.vertices:
    gs = sorted(((g.group, g.weight) for g in v.groups), key=lambda x: -x[1])
    keepg = gs[:4]; t = sum(w for _, w in keepg) or 1
    for g, _ in gs[4:]: ob.vertex_groups[g].remove([v.index])
    for g, w in keepg: ob.vertex_groups[g].add([v.index], w / t, 'REPLACE')
for p_ in me.polygons: p_.use_smooth = False
used = {x.group for v in me.vertices for x in v.groups}
for g in [g for g in ob.vertex_groups if g.index not in used]: ob.vertex_groups.remove(g)
log('triangles', sum(len(p.vertices) - 2 for p in me.polygons), 'vertices', len(me.vertices))

# the colour texture is the source's own (already in the project as hooded_visitor_0.png): UVs are unchanged
if EXPORT:
    bpy.ops.object.select_all(action='DESELECT')
    for o in (rig, ob): o.select_set(True)
    for path in ('Visitor_Rigged.fbx', 'VisitorHumanoid.fbx'):
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, path), use_selection=True, object_types={'MESH', 'ARMATURE'},
                                 add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', path_mode='STRIP')
    log('exported to', OUT_DIR)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(GEN, 'Visitor.blend'))

# ---------------------------------------------------------------- previews
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
def shot(name, loc, look, lens=50, res=(900, 1100)):
    sc.render.resolution_x, sc.render.resolution_y = res; cam.data.lens = lens; cam.location = Vector(loc)
    cam.rotation_euler = (Vector(look) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(GEN, name); bpy.ops.render.render(write_still=True)
shot('visitor_front.png', (0, -4.6, 1.0), (0, 0, .9))
sc.display.shading.show_xray = True; sc.display.shading.xray_alpha = .35
rig.show_in_front = True; ad.display_type = 'STICK'
shot('visitor_joints.png', (0, -4.6, 1.0), (0, 0, .9))
hl = joint['HandL'][0]
shot('visitor_joints_hand.png', tuple(hl + Vector((.12, -.05, .55))), tuple(hl + Vector((.1, 0, 0))), 85, (900, 700))
shot('visitor_joints_side.png', (4.6, 0, 1.0), (0, 0, .9))
sc.display.shading.show_xray = False
pb = rig.pose.bones
for sd, sgn in (('L', 1), ('R', -1)):
    pb['Forearm' + sd].rotation_mode = 'XYZ'; pb['Forearm' + sd].rotation_euler = (0, 0, math.radians(70) * sgn)
    pb['UpperArm' + sd].rotation_mode = 'XYZ'; pb['UpperArm' + sd].rotation_euler = (math.radians(60), 0, 0)
    for fn in FINGERS:
        for k in (1, 2, 3):
            b = pb['%s%d%s' % (fn, k, sd)]; b.rotation_mode = 'XYZ'; b.rotation_euler = (math.radians(55 if fn != 'Thumb' else 25), 0, 0)
pb['ThighL'].rotation_mode = 'XYZ'; pb['ThighL'].rotation_euler = (math.radians(-35), 0, 0)
pb['ShinL'].rotation_mode = 'XYZ'; pb['ShinL'].rotation_euler = (math.radians(50), 0, 0)
pb['Head'].rotation_mode = 'XYZ'; pb['Head'].rotation_euler = (0, math.radians(30), 0)
bpy.context.view_layer.update()
shot('visitor_posed.png', (2.6, -3.8, 1.2), (0, 0, .92))
hp = rig.matrix_world @ pb['Middle2L'].head
shot('visitor_posed_hand.png', tuple(hp + Vector((.25, -.45, .2))), tuple(hp), 70, (900, 700))
shot('visitor_posed_hand_b.png', tuple(hp + Vector((.35, .1, -.35))), tuple(hp), 70, (900, 700))
for b in rig.pose.bones: b.rotation_mode = 'XYZ'; b.rotation_euler = (0, 0, 0)
pb['HandL'].rotation_euler = (math.radians(40), math.radians(75), 0)
bpy.context.view_layer.update()
wp = rig.matrix_world @ pb['HandL'].head
shot('visitor_wrist_twist.png', tuple(wp + Vector((.05, -.35, .12))), tuple(wp), 70, (900, 700))
open(os.path.join(GEN, 'build_visitor.txt'), 'w').write('\n'.join(report))
