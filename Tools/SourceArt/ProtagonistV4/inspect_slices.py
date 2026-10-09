# Horizontal slices of a source model: for each height, the clusters of vertices along x (torso, arms, legs),
# to see where the arms leave the torso. Run: blender -b --factory-startup --python inspect_slices.py -- <file>
import bpy, sys, numpy as np
src = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob = next(o for o in bpy.data.objects if o.type == 'MESH'); ob.data.transform(ob.matrix_world)
P = np.empty(len(ob.data.vertices) * 3); ob.data.vertices.foreach_get('co', P); P = P.reshape(-1, 3)
z0, z1 = P[:, 2].min(), P[:, 2].max(); H = z1 - z0
for f in np.arange(.30, .98, .02):
    z = z0 + f * H; s = P[np.abs(P[:, 2] - z) < .004 * H]
    if len(s) == 0: continue
    xs = np.sort(s[:, 0]); gaps = np.where(np.diff(xs) > .01 * H)[0]
    parts = np.split(xs, gaps + 1)
    print('SLICE %.2f  ' % f + '  '.join('[%.3f..%.3f]' % (p[0], p[-1]) for p in parts))
