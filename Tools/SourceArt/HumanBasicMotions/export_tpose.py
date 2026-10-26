import bpy,sys
src,dst=sys.argv[sys.argv.index('--')+1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src,automatic_bone_orientation=False)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
arm.animation_data_clear()
for pb in arm.pose.bones:
    pb.location=(0,0,0);pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0);pb.scale=(1,1,1)
for a in list(bpy.data.actions):bpy.data.actions.remove(a)
bpy.ops.export_scene.fbx(filepath=dst,object_types={'ARMATURE','EMPTY'},bake_anim=False,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X')
print('EXPORTED',dst)
