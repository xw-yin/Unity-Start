"""Bake the installed game's ordinary female gait against the exact exported bind.

No avatar reorientation, shortened ankle stride, or lateral pelvis suppression.
The A-to-T bind conversion matches export_dlc_yennefer_boots.py.
"""
import sys,json,math
from pathlib import Path
import bpy
from mathutils import Matrix,Vector,Quaternion
sys.path[:0]=['F:/Unity/Tools/WitcherConversion','F:/Unity/Tools/WitcherConversion/site-packages']
import witcher3_tools as w3
prefs=w3._get_prefs();prefs.witcher_game_path='F:/SteamLibrary/steamapps/common/The Witcher 3'
prefs.uncook_path='F:/Unity/ExtractedModels/Characters';prefs.fbx_uncook_path=prefs.uncook_path
from witcher3_tools.CR2W.common_blender import repo_file
from witcher3_tools.CR2W.dc_anims import load_bin_anims_single,load_base_skeleton
project=Path('F:/Unity/Project/Start')
source=repo_file(r'animations\woman\movement\woman_movement.w2anims')
rig='F:/Unity/ExtractedModels/Characters/characters/base_entities/woman_base/woman_base.w2rig'
anim=load_bin_anims_single(source,anim_name='locomotion_walk_cycle_slow_forward',rigPath=rig).animations[0].animation
base_skeleton=load_base_skeleton(rig)
raw_rest={}
for b in base_skeleton.bones:
    q=b.ro_quat
    m=Matrix.Translation(Vector(b.co))@Quaternion((q.W,q.X,q.Y,q.Z)).normalized().to_matrix().to_4x4()
    raw_rest[b.name]=raw_rest[base_skeleton.bones[b.parentId].name]@m if b.parentId>=0 else m
bpy.ops.wm.open_mainfile(filepath='F:/Unity/ExtractedModels/Yennefer/Yennefer-dlc-dress.blend')
arm=bpy.data.objects['yennefer_dlc:CMovingPhysicalAgentComponent3_ARM']
for bone in arm.pose.bones:bone.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
source_rest={b.name:arm.matrix_world@b.matrix.copy() for b in arm.pose.bones}
for side,sign in [('l',-1),('r',1)]:
    for name,child in [('bicep','forearm'),('forearm','hand')]:
        bpy.context.view_layer.update();b=arm.pose.bones[side+'_'+name];c=arm.pose.bones[side+'_'+child]
        q=(c.head-b.head).rotation_difference(Vector((sign,0,0)))
        b.matrix=Matrix.Translation(b.head)@q.to_matrix().to_4x4()@Matrix.Translation(-b.head)@b.matrix
bpy.context.view_layer.update()
bind={b.name:arm.matrix_world@b.matrix.copy() for b in arm.pose.bones}
names={'pelvis':'C_Hips','torso':'C_Spine','torso2':'C_Chest','torso3':'C_UpperChest','neck':'C_Neck','head':'C_Head'}
for side,label in [('l','L'),('r','R')]:
    for src,dst in [('shoulder','Shoulder'),('bicep','UpperArm'),('forearm','LowerArm'),('hand','Hand'),('thigh','UpperLeg'),('shin','LowerLeg'),('foot','Foot'),('toe','ToeBase')]:names[side+'_'+src]=label+'_'+dst
    for src,dst in [('index','Index'),('middle','Middle'),('ring','Ring'),('pinky','Little'),('thumb','Thumb')]:
        for i in range(1,4):names[f'{side}_{src}{i}']=f'{label}_{dst}{i}'
mapping={n:'J_Bip_'+v for n,v in names.items() if n in bind}
tracks={b.BoneName:b for b in anim.animBuffer.bones}
ordered=[b.name for b in arm.pose.bones if b.name in mapping]
C=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
scale=.92
manifest=json.loads((project/'Assets/ThirdParty/WitcherYenneferDLC/Prepared/manifest.json').read_text())
pos={}
for node in manifest['nodes']:
    p=Vector(node['position'])
    if node['parent']>=0:p+=pos[manifest['nodes'][node['parent']]['name']]
    pos[node['name']]=p
assert max((Vector((bind[n].translation.x,bind[n].translation.z,bind[n].translation.y))*scale-pos[mapping[n]]).length for n in ordered)<1e-5,'Source bind no longer matches project'
def value(values,f):return values[min(f,len(values)-1)]
def local_track(n,f):
    if n not in tracks:
        b=arm.data.bones[n];return b.parent.matrix_local.inverted()@b.matrix_local if b.parent else b.matrix_local.copy()
    t=tracks[n];q=value(t.rotationFramesQuat,f)
    return Matrix.Translation(Vector(value(t.positionFrames,f)))@Quaternion((q.W,q.X,q.Y,q.Z)).normalized().to_matrix().to_4x4()
frames=anim.animBuffer.numFrames-1
rotations=[];hips=[];native=[];previous={}
for frame in range(frames+1):
    world={}
    for b in arm.pose.bones:
        m=local_track(b.name,frame)
        world[b.name]=world[b.parent.name]@m if b.parent else arm.matrix_world@m
    native.append(world)
    posed={n:world[n]@(arm.matrix_world@raw_rest[n]).inverted()@source_rest[n] for n in ordered}
    desired={n:(C@posed[n]@bind[n].inverted()@C).to_quaternion().normalized() for n in ordered}
    if frame==frames:continue
    for n in ordered:
        parent=arm.pose.bones[n].parent
        pq=desired[parent.name] if parent and parent.name in desired else Quaternion()
        q=pq.inverted()@desired[n];q.normalize()
        if n in previous and previous[n].dot(q)<0:q=-q
        previous[n]=q.copy();rotations.append(q)
    # Remove only trajectory translation. Preserve native lateral weight transfer,
    # vertical arc, and pelvis/torso phase as one authored motion.
    p=world['pelvis'].translation-world['Trajectory'].translation
    hips.append(Vector((p.x,p.z,p.y))*scale)
trajectory=native[-1]['Trajectory'].translation-native[0]['Trajectory'].translation
stride=Vector((trajectory.x,trajectory.y,0)).length*scale
base=pos['J_Bip_C_Hips'];mean=sum(hips,Vector())/len(hips)
# Match bind pelvis height, keeping the cycle's original vertical amplitude.
hips=[p+base-mean for p in hips]
header=(project/'Assets/Homecoming/Resources/Homecoming/WalkCycle.asset').read_text().split('  source:')[0]
lines=[header.rstrip(),'  source: animations/woman/movement/woman_movement.w2anims::locomotion_walk_cycle_slow_forward',f'  duration: {anim.duration}',f'  strideDistance: {stride}','  forward: {x: 0, y: 0, z: -1}','  bones:']
lines+=['  - '+mapping[n] for n in ordered];lines+=['  rotations:']
lines += [f'  - {{x: {q.x:.9g}, y: {q.y:.9g}, z: {q.z:.9g}, w: {q.w:.9g}}}' for q in rotations]
lines+=['  hipPositions:']+[f'  - {{x: {p.x:.9g}, y: {p.y:.9g}, z: {p.z:.9g}}}' for p in hips]+[f'  frames: {frames}']
(project/'Assets/Homecoming/Resources/Homecoming/NativeWalkCycle.asset').write_text('\n'.join(line.rstrip() for line in lines)+'\n')
print('NATIVE WALK BAKED',json.dumps({'frames':frames,'duration':anim.duration,'stride':stride,'bones':len(ordered),'hipRange':[[min(p[i] for p in hips),max(p[i] for p in hips)] for i in range(3)]}))
# Preserve a source-rig study with the decoded action, separate from original assets.
for b in arm.pose.bones:b.matrix_basis=Matrix.Identity(4)
for frame,world in enumerate(native):
    bpy.context.scene.frame_set(frame)
    for b in arm.pose.bones:
        if b.name in raw_rest:b.matrix=arm.matrix_world.inverted()@world[b.name]@(arm.matrix_world@raw_rest[b.name]).inverted()@source_rest[b.name]
    bpy.context.view_layer.update()
    for b in arm.pose.bones:
        b.rotation_mode='QUATERNION';b.keyframe_insert('rotation_quaternion',frame=frame);b.keyframe_insert('location',frame=frame)
bpy.context.scene.frame_start=0;bpy.context.scene.frame_end=frames;bpy.context.scene.render.fps=30
bpy.ops.wm.save_as_mainfile(filepath=str(project/'DCC/Blender/NativeWalkStudy.blend'))
