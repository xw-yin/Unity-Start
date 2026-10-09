"""Refresh Blender inspection poses from the current Unity motion controller."""
import json
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix

EXCHANGE=Path(r'F:\Unity\Project\StartMotionValidation\Artifacts\BlenderOptimization\Exchange')
C=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))

def update_pose_samples():
    for file in sorted(EXCHANGE.glob('*.json')):
        if file.name=='edits.json':continue
        package=json.loads(file.read_text(encoding='utf-8'))
        armature=bpy.data.objects[package['character']+'_Rig']
        armature.animation_data_clear()
        rest=[C@Matrix(np.asarray(b['matrix']).reshape(4,4).tolist())@C for b in package['bones']]
        basis=[armature.data.bones[b['name']].matrix_local.copy() for b in package['bones']]
        for pose in package['poses']:
            matrices=np.asarray(pose['bones']).reshape(-1,4,4)
            desired=[C@Matrix(matrices[i].tolist())@C@rest[i].inverted()@basis[i] for i in range(len(basis))]
            for i,bone in enumerate(package['bones']):
                parent=bone['parent']
                local=basis[i].inverted()@desired[i]
                if parent>=0:local=basis[i].inverted()@basis[parent]@desired[parent].inverted()@desired[i]
                pb=armature.pose.bones[bone['name']]
                pb.matrix_basis=local
                pb.keyframe_insert('location',frame=pose['frame'])
                pb.keyframe_insert('rotation_quaternion',frame=pose['frame'])
                pb.keyframe_insert('scale',frame=pose['frame'])
        for surface in package['surfaces']:
            objects=[o for o in bpy.data.objects if o.type=='MESH' and o.get('unity_character')==package['character'] and o.get('unity_mesh_path')==surface['path']]
            for obj in objects:
                obj['unity_enabled']=surface['enabled']
                switchable=package['character']=='YenneferDLC' and surface['role'] in ['hosiery','retained_lace','dressed_leg','dressed_lace']
                if surface['role']!='shoe' and not switchable:continue
                left=surface['name'].startswith('Left') or 'Left' in surface['name'] or '左' in surface['name']
                detach=390 if left else 570
                retained=surface['role'] in ['hosiery','retained_lace']
                for frame,hidden in [(0,retained),(detach-1,retained),(detach,not retained)]:
                    obj.hide_viewport=hidden;obj.hide_render=hidden
                    obj.keyframe_insert('hide_viewport',frame=frame);obj.keyframe_insert('hide_render',frame=frame)
        print('Refreshed poses:',package['character'])
    bpy.context.scene.render.fps=30
    bpy.context.scene.frame_start=0;bpy.context.scene.frame_end=1170
    bpy.context.scene.frame_set(810)
    bpy.context.view_layer.update()
