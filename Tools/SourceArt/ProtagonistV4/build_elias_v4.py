# Elias v4/v5: an AI-generated protagonist (source/*.glb: one mesh of about a million triangles in a T- or A-pose,
# 4K colour texture, no skeleton) rebuilt on the ProtagonistRig bone convention so EliasNativeInstall and
# MixamoInstall can bake every gameplay and cinematic clip for it. ELIAS_SOURCE picks the file (default: v5).
#   - an A-pose is raised to a T-pose with the palms down (what the rest of this script and the game's hand grips
#     expect), using temporary bone-heat weights from a closed copy
#   - joints located on the mesh itself (T-pose): spine, shoulders, elbows, wrists, five three-phalanx fingers,
#     hips, knees, ankles, toes; skin weights from Blender's bone heat, unweighted leftovers to the nearest bone
#   - head and hands brought towards the cast's proportions, body scaled to HEIGHT with feet on the ground
#   - reduced to about TRIS triangles, keeping hands and face (seen up close in first person and cinematics)
#   - rebuilt as one closed surface (voxel remesh) before reducing: the generated fingers are open half-shells that
#     crack open when bent; tiny pieces left by the reduction are removed (they make bone heat fail)
#   - the painted colours are baked from the full-resolution model onto new UVs (eyes, brows and beard live
#     there); a bluish smudge at the sides of the beard is recoloured from the beard itself
#   - hand weights follow the finger chains; the beard and chin follow the head
#   - head split from the body (hidden in first person) and both sides of the cut closed
# Run:  blender -b --factory-startup --python build_elias_v4.py               (previews in generated/)
#       ELIAS_EXPORT=1 blender -b --factory-startup --python build_elias_v4.py  (also writes the FBX and texture)
import bpy, bmesh, os, math
import numpy as np
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(ROOT, '..', '..', '..'))
SOURCE = os.path.join(ROOT, 'source', os.environ.get('ELIAS_SOURCE', 'Elias_v5_source.glb'))
OUT_DIR = os.path.join(PROJECT, 'Assets', 'ChickenHeistGenerated', 'Characters', 'EliasNative')
GEN = os.path.join(ROOT, 'generated'); os.makedirs(GEN, exist_ok=True)
EXPORT = os.environ.get('ELIAS_EXPORT') == '1'
# Head and arm length are brought to the cast's proportions unless given: head 15.6% of the height measured from
# the jaw, arm 0.574 m from shoulder to wrist (what the carry, wheel and padlock grips need). v4 used 0.74 / 1.10 / 0.92.
HEAD_SCALE = os.environ.get('ELIAS_HEAD_SCALE'); ARM_STRETCH = os.environ.get('ELIAS_ARM_STRETCH')
HEAD_FRACTION, ARM_LENGTH = .156, .574
HAND_SCALE = float(os.environ.get('ELIAS_HAND_SCALE', '1.0'))
HEIGHT = float(os.environ.get('ELIAS_HEIGHT', '1.74'))
TRIS = int(os.environ.get('ELIAS_TRIS', '22000'))
TEX = int(os.environ.get('ELIAS_TEX', '2048'))
VOXEL = float(os.environ.get('ELIAS_VOXEL', '0.0015'))
report = []
def log(*a): report.append(' '.join(str(x) for x in a)); print('ELIAS V4', *a)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SOURCE)
ob = next(o for o in bpy.data.objects if o.type == 'MESH')
for o in [o for o in bpy.data.objects if o.type != 'MESH']: bpy.data.objects.remove(o)
mw = ob.matrix_world.copy(); ob.parent = None; ob.data.transform(mw); ob.matrix_world = Matrix.Identity(4)
me = ob.data
bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=float(os.environ.get("ELIAS_WELD", "5e-5"))); bm.to_mesh(me); bm.free()

def remove_scraps(mesh, smallest=60):
    # tiny closed pieces (two triangles folded onto each other) left by a reduction see no bone and make the
    # bone-heat system unsolvable for the whole body
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

# ---------------------------------------------------------------- A-pose to T-pose
def tpose():
    global P
    z0_ = P[:, 2].min(); H_ = P[:, 2].max() - z0_
    def clusters(f):
        m = np.abs(P[:, 2] - (z0_ + f * H_)) < .004 * H_
        idx = np.where(m)[0]; idx = idx[np.argsort(P[idx, 0])]
        if len(idx) < 10: return []
        return np.split(idx, np.where(np.diff(P[idx, 0]) > .01 * H_)[0] + 1)
    # torso half-width: the cluster around x = 0, between the hips and the chest
    widths = [np.abs(P[c, 0]).max() for f in np.arange(.46, .62, .02) for c in clusters(f) if P[c, 0].min() < 0 < P[c, 0].max()]
    torso = float(np.median(widths))
    # arm: the outermost cluster on each side, from the armpit down (slices where it is separate from the torso)
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
        rows = rows[1:13]                                   # below the armpit, above the hand
        if len(rows) < 4: return 'no separate arm found: kept as is'
        zs, xs, ys = (np.array(r) for r in zip(*rows))
        b, a = np.polyfit(zs, xs, 1)                        # x = a + b z
        theta = math.degrees(math.atan2(1, abs(b)))         # angle below the horizontal
        fit[side] = (a, b, theta, float(ys.mean()))
    if max(t for _, _, t, _ in fit.values()) < 12: return 'already a T-pose (arms %.1f / %.1f degrees down)' % (fit[1][2], fit[-1][2])
    # temporary rig: body, head, legs and one straight bone per arm from the shoulder joint to the fingertips
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
    proxy = closed_copy(ob, .004 * H_, 40000, 'Elias_pose_proxy')
    ad_ = bpy.data.armatures.new('PoseRig'); rig_ = bpy.data.objects.new('PoseRig', ad_); bpy.context.scene.collection.objects.link(rig_)
    bpy.context.view_layer.objects.active = rig_; bpy.ops.object.mode_set(mode='EDIT')
    for n, (h, t) in bones.items(): eb = ad_.edit_bones.new(n); eb.head, eb.tail = h, t
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT'); proxy.select_set(True); rig_.select_set(True); bpy.context.view_layer.objects.active = rig_
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    unweighted = sum(1 for v in proxy.data.vertices if sum(g.weight for g in v.groups) < 1e-4)
    for n in bones:
        if ob.vertex_groups.get(n) is None: ob.vertex_groups.new(name=n)
    dt = ob.modifiers.new('pose weights', 'DATA_TRANSFER'); dt.object = proxy; dt.use_vert_data = True
    dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
    dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier='pose weights')
    # raise each arm about its shoulder to the horizontal
    for side, nm in ((1, 'L'), (-1, 'R')):
        pb = rig_.pose.bones['Arm' + nm]; pivot = bones['Arm' + nm][0]
        pb.matrix = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(-side * fit[side][2]), 4, 'Y') @ Matrix.Translation(-pivot) @ pb.bone.matrix_local
    bpy.context.view_layer.update()
    mod = ob.modifiers.new('pose', 'ARMATURE'); mod.object = rig_
    bpy.ops.object.modifier_apply(modifier='pose')
    for g in list(ob.vertex_groups): ob.vertex_groups.remove(g)
    bpy.data.objects.remove(proxy); bpy.data.objects.remove(rig_)
    P = coords()
    return 'raised arms %.1f / %.1f degrees (torso half-width %.3f, proxy vertices without weight %d)' % (fit[1][2], fit[-1][2], torso, unweighted)
log('pose:', tpose())
if os.environ.get('ELIAS_DEBUG_POSE') == '1':
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'
    zc = (P[:, 2].min() + P[:, 2].max()) / 2; span = float(np.abs(P[:, 0]).max())
    for nm, loc, ortho, res in (('front', Vector((0, -6, zc)), 2.3 * span, (1200, 800)), ('top', Vector((0, -.3, 6)), 2.3 * span, (1200, 700))):
        cam.location = loc; cam.rotation_euler = (Vector((0, 0, zc)) - loc).to_track_quat('-Z', 'Y').to_euler(); cam.data.ortho_scale = ortho
        sc.render.resolution_x, sc.render.resolution_y = res; sc.render.filepath = os.path.join(GEN, 'dbg_pose_%s.png' % nm); bpy.ops.render.render(write_still=True)
    for sd, side in (('L', 1), ('R', -1)):
        tip = P[np.argmax(side * P[:, 0])]; c = Vector(tip) - Vector((side * .06, 0, 0)); cam.data.ortho_scale = .2
        for nm, off in (('top', Vector((0, -.05, 1))), ('front', Vector((0, -1, 0)))):
            cam.location = c + off * 3; cam.rotation_euler = (-off).to_track_quat('-Z', 'Y').to_euler()
            sc.render.resolution_x, sc.render.resolution_y = 700, 500; sc.render.filepath = os.path.join(GEN, 'dbg_pose_hand%s_%s.png' % (sd, nm)); bpy.ops.render.render(write_still=True)
    raise SystemExit
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
if HEAD_SCALE is None:
    hh = z1 - head_pivot.z; HEAD_SCALE = HEAD_FRACTION * (head_pivot.z - z0) / ((1 - HEAD_FRACTION) * hh)
HEAD_SCALE = float(HEAD_SCALE); log('head scale', round(HEAD_SCALE, 3))
f = 1 - (1 - HEAD_SCALE) * wh
pv = np.array(head_pivot)
P = pv + (P - pv) * f[:, None]
for sd, side in (('L', 1), ('R', -1)):
    wr = np.array(joint['Hand' + sd][0])
    wgt = smooth((side * P[:, 0] - (wrist_x[sd] - .01 * H0)) / (.02 * H0)) * (np.abs(P[:, 2] - arm_z) < .12 * H0)
    P = P + (wr + (P - wr) * HAND_SCALE - P) * wgt[:, None]
    a, b = joint['Hand' + sd]; joint['Hand' + sd] = (a, a + (b - a) * HAND_SCALE)
# arms: stretch everything beyond the shoulder outward along x (T-pose), ramping in over the shoulder
if ARM_STRETCH is None:
    s_est = HEIGHT / (P[:, 2].max() - z0)
    arm = sum((joint['Hand' + sd][0] - joint['UpperArm' + sd][0]).length for sd in 'LR') / 2 * s_est
    ARM_STRETCH = ARM_LENGTH / arm
ARM_STRETCH = float(ARM_STRETCH); log('arm stretch', round(ARM_STRETCH, 3))
# (shoulder to wrist only: the hand moves out with the wrist and keeps its shape)
for sd, side in (('L', 1), ('R', -1)):
    sx = side * joint['UpperArm' + sd][0].x; wx = wrist_x[sd]
    wgt = smooth((side * P[:, 0] - sx) / .03) * (np.abs(P[:, 2] - arm_z) < .16 * H0)
    off = (np.clip(side * P[:, 0], sx, wx) - sx) * (ARM_STRETCH - 1) * wgt
    P[:, 0] += side * off
    for k in [k for k in joint if k.endswith(sd) and k.startswith(('UpperArm', 'Forearm', 'Hand'))]:
        a_, b_ = joint[k]
        joint[k] = tuple(Vector((v.x + side * (min(max(side * v.x, sx), wx) - sx) * (ARM_STRETCH - 1), v.y, v.z)) for v in (a_, b_))
    wrist_x[sd] = wx + (wx - sx) * (ARM_STRETCH - 1)
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
    def overlap(g1, g2):
        a0, a1 = P[g1][:, 1].min(), P[g1][:, 1].max(); b0, b1 = P[g2][:, 1].min(), P[g2][:, 1].max()
        return max(0.0, min(a1, b1) - max(a0, b0)) / max(1e-6, min(a1 - a0, b1 - b0))
    merged = []
    for g, c, m in sorted(leaves, key=lambda l: l[2][1]):
        if merged and overlap(g, merged[-1][0]) > .6 and abs(m[2] - merged[-1][2][2]) < .02:
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
    CHAINS[sd] = dict(chains)
    DEBUG_PIECES.append((H, [d['idx'] for d in [thumb] + fingers]))
DEBUG_PIECES = []; CHAINS = {}
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

# ---------------------------------------------------------------- one closed surface
# The generated mesh has open half-shells (each finger is two halves a hair apart), holes and overlapping folds;
# reduced as is, they open, fold over and show their insides. Rebuild it as one closed surface first; the colours
# come back from the untouched full-resolution copy in the bake below.
hi = ob.copy(); hi.data = ob.data.copy(); hi.name = 'EliasV4_high'; bpy.context.scene.collection.objects.link(hi)
ob.data.remesh_voxel_size = VOXEL; ob.data.remesh_voxel_adaptivity = 0
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.object.voxel_remesh()
me = ob.data; P = coords()
log('voxel remesh', VOXEL, 'verts', len(P))
EV = edges()
for sd, side in (('L', 1), ('R', -1)):
    ch = CHAINS[sd]; wrist = joint['Hand' + sd][0]; ax = Vector((side, 0, 0))
    s_root = sum((ch[f][0] - wrist).dot(ax) for f in FINGERS[1:]) / 4
    idx = np.where((side * (P[:, 0] - wrist.x) > s_root + .012) & (np.abs(P[:, 2] - wrist.z) < .15))[0]
    comps = [c for c in components(idx) if len(c) > 20]
    log(sd, 'pieces past the knuckles after closing the surface:', len(comps), sorted((len(c) for c in comps), reverse=True))
if os.environ.get('ELIAS_DEBUG_REMESH') == '1':
    cols = [(1, .2, .2, 1), (.2, 1, .2, 1), (.2, .4, 1, 1), (1, 1, .2, 1), (1, .3, 1, 1), (.2, 1, 1, 1), (1, .6, .2, 1), (.6, .3, 1, 1)]
    attr = me.color_attributes.new('dbg', 'FLOAT_COLOR', 'POINT'); data = np.tile([.5, .5, .5, 1], len(P)).astype(np.float32).reshape(-1, 4)
    for sd, side in (('L', 1), ('R', -1)):
        ch = CHAINS[sd]; wrist = joint['Hand' + sd][0]; ax = Vector((side, 0, 0))
        s_root = sum((ch[f][0] - wrist).dot(ax) for f in FINGERS[1:]) / 4
        idx = np.where((side * (P[:, 0] - wrist.x) > s_root + .012) & (np.abs(P[:, 2] - wrist.z) < .15))[0]
        comps = [c for c in components(idx) if len(c) > 20]
        for i, c in enumerate(sorted(comps, key=len, reverse=True)): data[c] = cols[i % len(cols)]
    attr.data.foreach_set('color', data.ravel())
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.color_type = 'VERTEX'; sc.display.shading.light = 'STUDIO'
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    for o in bpy.data.objects:
        if o.type == 'MESH' and o is not ob: o.hide_render = True
    hw = joint['HandL'][0]; c = hw + Vector((.12, 0, 0))
    for nm, off in (('top', Vector((0, -.02, .45))), ('front', Vector((0, -.45, 0))), ('end', Vector((.45, -.05, .02)))):
        cam.location = c + off; cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler(); cam.data.lens = 60
        sc.render.resolution_x, sc.render.resolution_y = 800, 600; sc.render.filepath = os.path.join(GEN, 'dbg_remesh_L_%s.png' % nm); bpy.ops.render.render(write_still=True)
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
# The generated shoe soles are open (hundreds of small holes in the tread), so the voxel remesh wraps every surface
# in a thin double wall (about 1.5 mm): an outer skin and an inner one facing into the body. The inner wall pokes through when joints bend and shows as dark
# triangles; it also takes half of the triangle budget. Faces that cannot see the outside along any of a fan of
# rays around their normal are inside: remove them.
from mathutils.bvhtree import BVHTree
import random
me = ob.data; bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table(); bm.normal_update()
tree = BVHTree.FromBMesh(bm)
random.seed(3); fan = []
while len(fan) < 24:
    v = Vector((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(-1, 1)))
    if .05 < v.length <= 1: fan.append(v.normalized())
def signed_volume(b): return sum(f.verts[0].co.dot(f.verts[1].co.cross(f.verts[2].co)) for f in b.faces if len(f.verts) == 3) / 6
ratio = signed_volume(bm) / HEIGHT ** 3
log('remeshed volume / height^3 %.4f (solid body ~0.010, hollow wall ~0.003)' % ratio)
inner = []
for f in (bm.faces if ratio < .006 else []):
    n = f.normal; c = f.calc_center_median() + n * .0004
    dirs = [n] + [d if d.dot(n) > 0 else -d for d in fan]
    if all(tree.ray_cast(c, d, 4.0)[0] is not None for d in dirs): inner.append(f)
f_before = len(bm.faces)
bmesh.ops.delete(bm, geom=inner, context='FACES')
bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
bm.to_mesh(me); bm.free(); me.update()
log('inner wall removed:', len(inner), 'of', f_before, 'faces')
tri1 = sum(len(p.vertices) - 2 for p in me.polygons)
dec = ob.modifiers.new('dec', 'DECIMATE'); dec.ratio = TRIS / tri1; dec.use_collapse_triangulate = True
dec.vertex_group = '_keep'; dec.vertex_group_factor = float(os.environ.get('ELIAS_KEEP', '1.0')); dec.invert_vertex_group = True
bpy.context.view_layer.objects.active = ob; bpy.ops.object.modifier_apply(modifier='dec')
ob.vertex_groups.remove(ob.vertex_groups['_keep']); me = ob.data
log('triangles', tri0, '->', sum(len(p.vertices) - 2 for p in me.polygons))
# zero-area slivers from the collapse make the bone-heat solver fail: dissolve them
bm = bmesh.new(); bm.from_mesh(me)
nv0 = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-4)
bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 3])
loose = [v for v in bm.verts if not v.link_faces]; bmesh.ops.delete(bm, geom=loose, context='VERTS')

small = [f for f in bm.faces if f.calc_area() < 1e-9]
bm.to_mesh(me); bm.free(); me.update()
scraps = remove_scraps(me)
log('cleanup: verts', nv0, '->', len(me.vertices), 'loose scrap vertices removed', scraps)

# ---------------------------------------------------------------- texture: baked from the full-resolution mesh
# Collapsing a million triangles leaves faces spanning the source's many small UV islands, which then sample hair
# or beard colour on the skin. The reduced mesh gets its own UVs and the colour is baked from the full mesh.
src = bpy.data.images['Image_0'] if 'Image_0' in bpy.data.images else next(i for i in bpy.data.images if i.size[0] > 0)
IW, IH = src.size; pix = np.empty(IW * IH * 4, dtype=np.float32); src.pixels.foreach_get(pix); pix = pix.reshape(IH, IW, 4)
def sample(uv): x = min(IW - 1, max(0, int((uv[0] % 1) * IW))); y = min(IH - 1, max(0, int((uv[1] % 1) * IH))); return pix[y, x, :3]
# bluish smudge at the sides of the beard: on the full mesh, point those faces at the nearest beard colour
from mathutils import kdtree
hme = hi.data; chin_z = joint['Head'][0].z
bm = bmesh.new(); bm.from_mesh(hme); bm.faces.ensure_lookup_table(); uvl = bm.loops.layers.uv.active
def centroid_uv(f): return sum((l[uvl].uv for l in f.loops), Vector((0, 0))) / len(f.loops)
def bluish(c): return c[2] > c[0] * 1.08 and c[2] > c[1] * 1.02 and c[2] > .12
head_top_z = max(v.co.z for v in hme.vertices)
head_faces = [f for f in bm.faces if chin_z - .04 < f.verts[0].co.z < chin_z + .45 * (head_top_z - chin_z)]
beard = [f for f in head_faces if f.verts[0].co.y < joint['Head'][0].y and (lambda c: c[0] < .35 and c[0] > c[2] and not bluish(c))(sample(centroid_uv(f)))]
fixed = 0
if beard:
    kd = kdtree.KDTree(len(beard))
    for i, f in enumerate(beard): kd.insert(f.calc_center_median(), i)
    kd.balance()
    for f in head_faces:
        if bluish(sample(centroid_uv(f))):
            uv = centroid_uv(beard[kd.find(f.calc_center_median())[1]])
            for l in f.loops: l[uvl].uv = uv
            fixed += 1
bm.to_mesh(hme); bm.free()
log('bluish head faces recoloured', fixed)
for l in list(me.uv_layers): me.uv_layers.remove(l)
uvb = me.uv_layers.new(name='UVMap'); me.uv_layers.active = uvb; uvb.active_render = True
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.003)
bpy.ops.object.mode_set(mode='OBJECT')
tex = bpy.data.images.new('EliasV4_Albedo', TEX, TEX, alpha=False)
mat = bpy.data.materials.new('EliasV4'); mat.use_nodes = True
tn = mat.node_tree.nodes.new('ShaderNodeTexImage'); tn.image = tex; mat.node_tree.nodes.active = tn
bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'); bsdf.inputs['Roughness'].default_value = .85
mat.node_tree.links.new(tn.outputs[0], bsdf.inputs[0])
me.materials.clear(); me.materials.append(mat)
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 1
sc.render.bake.use_selected_to_active = True; sc.render.bake.cage_extrusion = float(os.environ.get('ELIAS_CAGE', '.006')); sc.render.bake.max_ray_distance = .03; sc.render.bake.margin = 6
bpy.ops.object.select_all(action='DESELECT'); hi.select_set(True); ob.select_set(True); bpy.context.view_layer.objects.active = ob
bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_selected_to_active=True)
log('texture baked', TEX)
# Hands and forearms are bare skin: the source paints shadows into the creases between the fingers, and the bake
# spreads them over the closed surface as dark specks. Texels of the hand faces much darker than the hand's own
# skin tone take that tone back (the face, beard and clothes are not touched).
tw_, th_ = tex.size; tp = np.empty(tw_ * th_ * 4, dtype=np.float32); tex.pixels.foreach_get(tp); tp = tp.reshape(th_, tw_, 4)
reach_x = min(abs(joint['HandL'][0].x), abs(joint['HandR'][0].x)) - .10
uvd_ = me.uv_layers.active.data; mask = np.zeros((th_, tw_), bool)
for poly in me.polygons:
    if abs(poly.center.x) < reach_x: continue
    uv = np.array([uvd_[li].uv[:] for li in poly.loop_indices]) * [tw_, th_]
    x0, y0 = np.floor(uv.min(0)).astype(int); x1, y1 = np.ceil(uv.max(0)).astype(int)
    x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, tw_ - 1), min(y1, th_ - 1)
    if x1 < x0 or y1 < y0: continue
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
    a_, b_, c_ = uv[0], uv[1], uv[2]
    d = (b_[1] - c_[1]) * (a_[0] - c_[0]) + (c_[0] - b_[0]) * (a_[1] - c_[1])
    if abs(d) < 1e-9: continue
    l1 = ((b_[1] - c_[1]) * (gx - c_[0]) + (c_[0] - b_[0]) * (gy - c_[1])) / d
    l2 = ((c_[1] - a_[1]) * (gx - c_[0]) + (a_[0] - c_[0]) * (gy - c_[1])) / d
    inside = (l1 >= -.02) & (l2 >= -.02) & (1 - l1 - l2 >= -.02)
    mask[y0:y1 + 1, x0:x1 + 1] |= inside
lum = tp[..., 0] * .3 + tp[..., 1] * .59 + tp[..., 2] * .11
if mask.any():
    skin = np.median(tp[mask][:, :3], axis=0); skin_l = float(np.median(lum[mask]))
    dark = mask & (lum < .78 * skin_l)
    ratio = np.clip(lum[dark] / skin_l, .88, 1.0)[:, None]
    tp[dark, :3] = skin * ratio
    tex.pixels.foreach_set(tp.ravel()); tex.update()
    log('hand texels', int(mask.sum()), 'dark specks lifted to the skin tone', int(dark.sum()))
for p_ in me.polygons: p_.use_smooth = False

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
bpy.data.objects.remove(hi)
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
# Wrist. Bone heat leaves the palm base and the root of the thumb (2-9 cm past the wrist joint) weighted to the
# forearm: they stay behind when the hand bends and stretch into spikes. Past WRIST_END everything follows the hand
# and its fingers, before WRIST_START everything follows the forearm, and in between the two blend smoothly.
WRIST_START, WRIST_END = (float(x) for x in os.environ.get('ELIAS_WRIST', '-0.045,0.02').split(','))
moved = [0, 0, 0]
for sd in 'LR':
    hand_bone = rig.data.bones['Hand' + sd]; inside = {b.name for b in hand_bone.children_recursive} | {hand_bone.name}
    el, wr = joint['Forearm' + sd]; axis = (wr - el).normalized()
    fa, hd = ob.vertex_groups['Forearm' + sd], ob.vertex_groups['Hand' + sd]
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
# Head: everything above the jaw, the beard in front of the neck down to the collar and the throat under it follow
# the head bone (bone heat gave them to the neck: the head bone starts at the jaw, the beard hangs 6 cm lower).
# The green shirt collar stays on the body.
tw, th = tex.size; tpx = np.empty(tw * th * 4, dtype=np.float32); tex.pixels.foreach_get(tpx); tpx = tpx.reshape(th, tw, 4)
uvd = me.uv_layers.active.data
hz = joint['Head'][0].z; ny = joint['Neck'][0].y
head_verts = set(); head_faces = 0
for poly in me.polygons:
    c = poly.center
    if abs(c.x) > .14 or c.z < hz - .09: continue
    u = sum(uvd[li].uv[0] for li in poly.loop_indices) / poly.loop_total; v = sum(uvd[li].uv[1] for li in poly.loop_indices) / poly.loop_total
    r, g, b = tpx[min(th - 1, int(v * th)), min(tw - 1, int(u * tw)), :3]
    shirt = g > r * 1.02 and g > b
    cut = hz - .065 if c.y < ny - .02 else (hz if c.y > ny + .04 else hz - .045)
    if c.z > hz + .03 or (c.z > cut and not shirt):
        head_verts.update(poly.vertices); head_faces += 1
hgi = ob.vertex_groups['Head'].index
for vi in head_verts:
    for g in [g.group for g in me.vertices[vi].groups if g.group != hgi]: ob.vertex_groups[g].remove([vi])
    ob.vertex_groups['Head'].add([vi], 1.0, 'REPLACE')
log('head faces (beard and chin included)', head_faces)

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
# Close the cut on both sides with a fan to the centre of each opening (the cut follows the beard down in front and
# the hairline up behind, so it is far from flat: a flat fill pokes out). The body cap takes the neck's skin colour
# (seen from above in first person), the head cap the beard's.
def lum(c): return .3 * c[0] + .59 * c[1] + .11 * c[2]
def face_colour(f, uvl):
    u = sum(l[uvl].uv[0] for l in f.loops) / len(f.loops); v = sum(l[uvl].uv[1] for l in f.loops) / len(f.loops)
    return (u, v), tpx[min(th - 1, int(v * th)), min(tw - 1, int(u * tw)), :3]
def cap(obj, outward, pick, label):
    bm = bmesh.new(); bm.from_mesh(obj.data); uvl = bm.loops.layers.uv.active; dl = bm.verts.layers.deform.verify()
    ring = {e for e in bm.edges if e.is_boundary}; loops = []; seen = set()
    for e in ring:
        if e in seen: continue
        stack = [e]; comp = []
        while stack:
            x = stack.pop()
            if x in seen: continue
            seen.add(x); comp.append(x); stack.extend(y for v in x.verts for y in v.link_edges if y in ring and y not in seen)
        loops.append(comp)
    made = 0
    for loop in loops:
        verts = list({v for e in loop for v in e.verts})
        near = [f for v in verts for f in v.link_faces]
        uv, _ = pick([face_colour(f, uvl) for f in near])
        c = bm.verts.new(sum((v.co for v in verts), Vector()) / len(verts))
        c[dl].clear(); [c[dl].__setitem__(k, w) for k, w in verts[0][dl].items()]
        for e in loop:
            try: f = bm.faces.new((e.verts[0], e.verts[1], c))
            except ValueError: continue
            f.normal_update()
            if f.normal.dot(outward) < 0: f.normal_flip()
            for lp in f.loops: lp[uvl].uv = uv
            f.smooth = False; made += 1
    # one consistent outward winding for the whole surface (the patches under the shoes face down, the neck's up)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data); bm.free()
    log(label, len(loops), 'openings', made, 'faces')
def skin(cands):
    sk = [c for c in cands if c[1][0] > c[1][1] > c[1][2] and c[1][0] > .35]
    return max(sk or cands, key=lambda c: c[1][0])
def beard(cands): return min(cands, key=lambda c: lum(c[1]))
cap(body, Vector((0, 0, 1)), skin, 'neck cap')
cap(head, Vector((0, 0, -1)), beard, 'head cap')
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
