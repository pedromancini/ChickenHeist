# Weights around the left wrist of generated/Elias_v4.blend: vertices past the wrist joint that still follow bones
# outside the hand (forearm, upper arm...), and forearm vertices that follow hand or finger bones.
# Run: blender -b generated/Elias_v4.blend --python inspect_wrist_weights.py
import bpy
from collections import Counter
rig = bpy.data.objects['ProtagonistRig']; body = bpy.data.objects['ProtagonistBody']; me = body.data
for side in 'LR':
    hand = rig.data.bones['Hand' + side]; inside = {b.name for b in hand.children_recursive} | {hand.name}
    el = rig.data.bones['Forearm' + side].head_local; wr = hand.head_local; axis = (wr - el).normalized()
    past_out = Counter(); fore_in = Counter(); worst = []
    for v in me.vertices:
        t = (v.co - wr).dot(axis); r = (v.co - (wr + axis * t)).length
        if r > .15 or t < -.15: continue
        ws = {body.vertex_groups[g.group].name: g.weight for g in v.groups if g.weight > 1e-3}
        out_w = sum(w for n, w in ws.items() if n not in inside); in_w = sum(w for n, w in ws.items() if n in inside)
        bucket = round(t * 100)
        if t > .02 and out_w > .02:
            past_out['t=%+3dcm' % bucket] += 1; worst.append((out_w, t, r, ws))
        if t < -.06 and in_w > .02: fore_in['t=%+3dcm' % bucket] += 1
    print('WRIST', side, 'past the wrist with weight outside the hand:', sum(past_out.values()), dict(sorted(past_out.items())))
    print('WRIST', side, 'forearm with hand/finger weight:', sum(fore_in.values()), dict(sorted(fore_in.items())))
    for out_w, t, r, ws in sorted(worst, key=lambda x: -x[0])[:8]:
        print('WRIST   out %.2f at t %+.3f r %.3f: %s' % (out_w, t, r, {k: round(w, 2) for k, w in ws.items()}))
