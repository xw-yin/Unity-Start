"""Bake source additive mimic poses through the original Blender rigs into Unity's bind.

No source mesh, weights, blend, or original animation file is written.
"""
import bpy,sys,json,hashlib
from pathlib import Path
from mathutils import Matrix,Vector
sys.path[:0]=['F:/Unity/Tools/WitcherConversion','F:/Unity/Tools/WitcherConversion/site-packages']
from witcher3_tools.importers.import_rig import loadFaceFile
from witcher3_tools.importers import import_anims
from witcher3_tools.CR2W.w3_types import CSkeletalAnimationSetEntry
from witcher3_tools.ui import ui_morphs
ui_morphs.register()
project=Path('F:/Unity/Project/Start')
source=Path('F:/Unity/ExtractedModels/Yennefer/characters/models/main_npc/yennefer/h_01_wa__yennefer/h_01_wa__yennefer.w3fac')
face=loadFaceFile(str(source))
bpy.ops.wm.open_mainfile(filepath='F:/Unity/ExtractedModels/Yennefer/Yennefer-dlc-dress.blend')
for obj in bpy.data.objects:
 if obj.type=='ARMATURE':
  obj.animation_data_clear()
  for b in obj.pose.bones:b.matrix_basis=Matrix.Identity(4)
 if obj.type=='MESH':
  for mod in obj.modifiers:
   if mod.type=='NODES':mod.show_viewport=False
bpy.context.view_layer.update()
main=bpy.data.objects['yennefer_dlc:CMovingPhysicalAgentComponent3_ARM']
mimic=bpy.data.objects['yennefer_dlc:CMimicComponent02_ARM']
objects=[o for o in bpy.data.objects if o.type=='MESH' and 'shadowmesh' not in o.name and not any(c.name=='_WitcherRedclothCache' for c in o.users_collection) and '_lod0' in o.name and 'GMesh' not in o.name]
targets={b.name:(main,b) for b in main.pose.bones}
for obj in objects:
 for mod in obj.modifiers:
  if mod.type=='ARMATURE' and mod.object:
   for b in mod.object.pose.bones:
    if b.name not in targets:targets[b.name]=(mod.object,b)
manifest=json.loads((project/'Assets/ThirdParty/WitcherYenneferDLC/Prepared/manifest.json').read_text())
nodes=manifest['nodes'];rest=[]
for n in nodes:
 m=Matrix.Translation(Vector(n['position']))
 rest.append(rest[n['parent']]@m if n['parent']>=0 else m)
names={n['name']:i for i,n in enumerate(nodes)}
pose_bones={b.BoneName for b in face.mimicPoses[0].animBuffer.bones}
selected=[(i,n['name'][3:]) for i,n in enumerate(nodes) if n['name'].startswith('W3_') and n['name'][3:] in pose_bones]
C=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)));S=Matrix.Scale(.92,4)
initial={name:o.matrix_world@b.matrix.copy() for name,(o,b) in targets.items()}
error=max(((S@C@initial[name]).translation-rest[i].translation).length for i,name in selected)
if error>1e-5:raise ValueError('Original face rest does not match current imported bind: '+str(error))
result={'source':source.relative_to(Path('F:/Unity/ExtractedModels/Yennefer')).as_posix(),'sourceSHA256':hashlib.sha256(source.read_bytes()).hexdigest(),'restFitError':error,'bones':[nodes[i]['name'] for i,n in selected],'poses':[],'ignoredSourceBones':sorted(pose_bones-{n for i,n in selected})}
bpy.ops.object.select_all(action='DESELECT');mimic.select_set(True);bpy.context.view_layer.objects.active=mimic
for pose in face.mimicPoses:
 mimic.animation_data_clear()
 for b in mimic.pose.bones:b.matrix_basis=Matrix.Identity(4)
 pose.SkeletalAnimationType='SAT_Additive';entry=CSkeletalAnimationSetEntry();entry.animation=pose
 import_anims.import_anim(bpy.context,'imported',entry,facePose=True,override_select=[mimic],update_scene_settings=False,at_frame=0)
 bpy.context.scene.frame_set(0);bpy.context.view_layer.update()
 desired={}
 for i,name in selected:
  o,b=targets[name];posed=o.matrix_world@b.matrix
  delta=S@C@posed@initial[name].inverted()@C@S.inverted()
  desired[i]=delta@rest[i]
 transforms=[]
 for i,name in selected:
  parent=nodes[i]['parent'];parent_world=desired.get(parent,rest[parent]) if parent>=0 else Matrix.Identity(4)
  local=parent_world.inverted()@desired[i];position=local.translation-Vector(nodes[i]['position']);q=local.to_quaternion().normalized()
  if q.w<0:q=-q
  if position.length>1e-6 or Vector((q.x,q.y,q.z)).length>1e-5:
   transforms.append({'bone':nodes[i]['name'],'position':{'x':position.x,'y':position.y,'z':position.z},'rotation':{'x':q.x,'y':q.y,'z':q.z,'w':q.w}})
 result['poses'].append({'name':pose.name,'transforms':transforms})
 print('FACE POSE',pose.name,len(transforms),flush=True)
folder=project/'Assets/ThirdParty/WitcherYenneferDLC/Prepared/Face';folder.mkdir(exist_ok=True)
(folder/'poses.json').write_text(json.dumps(result,indent=2))
print('SOURCE FACE EXPORTED',len(selected),'bones',len(result['poses']),'poses, rest error',error,flush=True)
