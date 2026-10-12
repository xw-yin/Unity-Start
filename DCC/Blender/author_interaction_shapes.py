"""Author an editable rest pose and soft-boot shapes on original Unity meshes.

Run with Blender --factory-startup --background --python this_file.
Outputs a separate .blend and explicit Unity pose/shape data; never overwrites sources.
"""
import json
import math
import sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix, Vector, Quaternion

PROJECT=Path(__file__).resolve().parents[2]
EXCHANGE=Path('F:/Unity/Project/StartMotionValidation/Artifacts/BlenderOptimization/Exchange')
OUT=PROJECT/'Assets/Homecoming/Resources/Homecoming/Poses'
PROOF=PROJECT/'Artifacts/AnimationSkillStudy/Blender'
OUT.mkdir(parents=True,exist_ok=True);PROOF.mkdir(parents=True,exist_ok=True)
C=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
def mat(a):return Matrix(np.asarray(a).reshape(4,4).tolist())
def turn(bone,axis,degrees):
    p=bone.matrix.translation.copy()
    bone.matrix=Matrix.Translation(p)@Quaternion(Vector(axis),math.radians(degrees)).to_matrix().to_4x4()@Matrix.Translation(-p)@bone.matrix
    bpy.context.view_layer.update()
def solve(arm,names,target,hint):
    a,b,c=[arm.pose.bones[n] for n in names]
    origin=a.matrix.translation.copy();old_a=b.matrix.translation-origin;old_b=c.matrix.translation-b.matrix.translation
    la,lb=old_a.length,old_b.length;direction=(target-origin).normalized()
    distance=min(la+lb-.004,max(abs(la-lb)+.004,(target-origin).length))
    bend=hint-direction*hint.dot(direction);bend.normalize()
    along=(la*la-lb*lb+distance*distance)/(2*distance)
    joint=origin+direction*along+bend*math.sqrt(max(0,la*la-along*along))
    turn_a=old_a.rotation_difference(joint-origin)
    p=a.matrix.translation.copy();a.matrix=Matrix.Translation(p)@turn_a.to_matrix().to_4x4()@Matrix.Translation(-p)@a.matrix
    bpy.context.view_layer.update()
    old=b.matrix.translation.copy();vector=c.matrix.translation-old;turn_b=vector.rotation_difference(target-old)
    b.matrix=Matrix.Translation(old)@turn_b.to_matrix().to_4x4()@Matrix.Translation(-old)@b.matrix
    bpy.context.view_layer.update()

if 'YenneferDLC_Rig' not in bpy.data.objects:
    sys.path.insert(0,str(Path(__file__).parent))
    from character_workspace import import_character
    for default in list(bpy.data.objects):bpy.data.objects.remove(default,do_unlink=True)
    import_character('YenneferDLC');import_character('Triss')
scene=bpy.context.scene
scene.frame_set(930)
for name in ['YenneferDLC','Triss']:
    package=json.loads((EXCHANGE/(name+'.json')).read_text(encoding='utf-8'))
    arm=bpy.data.objects[name+'_Rig']
    arm.animation_data_clear()
    # A small shoulder/chest opposition, rather than continuous full-body swaying.
    turn(arm.pose.bones['J_Bip_C_Chest'],(0,1,0),-2.2 if name=='YenneferDLC' else 1.8)
    turn(arm.pose.bones['J_Bip_C_Head'],(0,1,0),2 if name=='YenneferDLC' else -2)
    targets=[]
    for side in range(2):
        prefix='J_Bip_'+('L_' if side==0 else 'R_');sign=1 if side==0 else -1
        shoulder=arm.pose.bones[prefix+'Shoulder']
        turn(shoulder,(0,1,0),sign*(1.6 if side==0 else .5))
        names=[prefix+'UpperArm',prefix+'LowerArm',prefix+'Hand']
        a,b,c=[arm.pose.bones[n] for n in names]
        length=(a.matrix.translation-b.matrix.translation).length+(b.matrix.translation-c.matrix.translation).length
        # Asymmetric, slightly flexed, close-to-body wrists with clear finger space.
        offset=Vector((sign*(.068 if side==0 else .050),-.035 if side==0 else -.018,0))
        drop=math.sqrt((length*(.965 if side==0 else .983))**2-offset.x**2-offset.y**2)
        target=a.matrix.translation+offset+Vector((0,0,-drop))
        solve(arm,names,target,Vector((sign*.025,.17,-.01)))
        hand=arm.pose.bones[prefix+'Hand'];old=hand.matrix.translation.copy()
        index=arm.pose.bones[prefix+'Index1'].matrix.translation;little=arm.pose.bones[prefix+'Little1'].matrix.translation
        middle=arm.pose.bones[prefix+'Middle1'].matrix.translation
        finger_dir=(middle-old).normalized();forearm=(old-arm.pose.bones[prefix+'LowerArm'].matrix.translation).normalized()
        hand.matrix=Matrix.Translation(old)@finger_dir.rotation_difference(forearm).to_matrix().to_4x4()@Matrix.Translation(-old)@hand.matrix
        bpy.context.view_layer.update()
        targets.append(list(C@target))
    desired=[]
    for bone in package['bones']:
        b=arm.pose.bones[bone['name']]
        unity=C@(b.matrix@arm.data.bones[bone['name']].matrix_local.inverted()@C@mat(bone['matrix'])@C)@C
        desired.append(unity)
    records=[]
    for i,bone in enumerate(package['bones']):
        if not any(k in bone['name'] for k in ['Spine','Chest','Head','Shoulder','Arm','Hand','Index','Middle','Ring','Little','Thumb']):continue
        local=desired[bone['parent']].inverted()@desired[i] if bone['parent']>=0 else desired[i]
        q=local.to_quaternion();records.append({'name':bone['name'],'rotation':[q.x,q.y,q.z,q.w]})
    (OUT/(name+'.json')).write_text(json.dumps({'character':name,'intent':'Release the task, lower the shoulders and settle; no automatic greeting','bones':records},indent=2),encoding='utf-8')
    arm['authored_pose']='Calm after taking boots off';arm['wrist_targets_unity']=str(targets)
    for bone in arm.pose.bones:bone.keyframe_insert('rotation_quaternion',frame=930)
    print('AUTHORED REST POSE',name,len(records),targets)
    if name!='YenneferDLC':continue
    for surface in [s for s in package['surfaces'] if s['role']=='shoe']:
        positions=np.asarray(surface['vertices'],dtype=float).reshape(-1,3)
        matrix=np.array(mat(surface['matrix']))
        positions=(np.column_stack((positions,np.ones(len(positions))))@matrix.T)[:,:3]
        center=(positions.min(axis=0)+positions.max(axis=0))*.5;center[1]=positions[:,1].min()
        positions-=center
        side='Left' if 'Left' in surface['name'] else 'Right';sign=1 if side=='Left' else -1
        # Section controls establish bend and oval collapse; no regular wave field.
        top=positions[:,1].max()
        section_y=np.array([0,.14,.27,.40,.53,top])
        dx=np.array([0,0,.008,.023,.042,.066])*sign
        dy=np.array([0,0,-.030,-.090,-.170,-.26])
        dz=np.array([0,0,.012,.034,.070,.12])
        sx=np.array([1,1,.98,.95,.89,.82]);sz=np.array([1,1,.95,.84,.60,.35])
        shaped=positions.copy()
        for i,p in enumerate(positions):
            y=p[1];shaped[i]=[p[0]*np.interp(y,section_y,sx)+np.interp(y,section_y,dx),y+np.interp(y,section_y,dy),p[2]*np.interp(y,section_y,sz)+np.interp(y,section_y,dz)]
        delta=shaped-positions
        mesh=bpy.data.meshes.new('SoftBoot_'+side);mesh.from_pydata((positions[:,[0,2,1]]).tolist(),[],np.asarray(surface['triangles']).reshape(-1,3)[:,[0,2,1]].tolist());mesh.update()
        obj=bpy.data.objects.new('SoftBoot_'+side,mesh);scene.collection.objects.link(obj)
        obj.location=Vector((sign*.6,0,0));obj.shape_key_add(name='Basis');key=obj.shape_key_add(name='Empty shaft - oval compression and sag')
        key.data.foreach_set('co',shaped[:,[0,2,1]].astype(np.float32).ravel());key.value=1
        for polygon in mesh.polygons:polygon.use_smooth=True
        obj.data.materials.append(bpy.data.materials.new('Boot Study '+side))
        obj.data.materials[0].diffuse_color=(.13,.095,.075,1)
        (OUT/('BootShape_OriginalYenneferBootDLC'+side+'.json')).write_text(json.dumps({'vertexCount':len(positions),'delta':delta.astype(float).ravel().tolist(),'source':'Original game boot; additive authored key shape; sole and heel unchanged'},separators=(',',':')),encoding='utf-8')
        print('AUTHORED BOOT SHAPE',side,len(positions),float(np.linalg.norm(delta,axis=1).max()))

# Keep models and authored source rig in a new file; original Characters.blend stays intact.
for collection in bpy.data.collections:
    if collection.name.startswith('Unity_'):collection.hide_render=collection.name!='Unity_YenneferDLC';collection.hide_viewport=collection.name!='Unity_YenneferDLC'
for obj in bpy.data.objects:
    if obj.type=='MESH' and obj.get('unity_character')=='YenneferDLC':
        role=obj.get('unity_role');obj.hide_render=role in ['shoe','leg','dressed_leg','dressed_lace'];obj.hide_viewport=obj.hide_render
scene.frame_set(930);bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/'DCC/Blender/InteractionStudy.blend'))
