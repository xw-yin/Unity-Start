"""Virtual high-heel foot/toe pose, stored as a reversible source-mesh corrective."""
import json,math
from pathlib import Path
import numpy as np
import bpy
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Homecoming/Resources/Homecoming/Poses'
data=json.loads(Path('F:/Unity/Project/StartMotionValidation/Artifacts/BlenderOptimization/Exchange/YenneferDLC.json').read_text(encoding='utf-8'))
rest=[Matrix(np.asarray(b['matrix']).reshape(4,4).tolist()) for b in data['bones']]
def below(index,parent):
    while index>=0:
        if index==parent:return True
        index=data['bones'][index]['parent']
    return False
for side in range(2):
    prefix='J_Bip_'+('L_' if side==0 else 'R_');sign=1 if side==0 else -1
    foot=next(i for i,b in enumerate(data['bones']) if b['name']==prefix+'Foot')
    toe=next(i for i,b in enumerate(data['bones']) if b['name']==prefix+'ToeBase')
    pivot=rest[foot].translation
    rotation=Matrix.Rotation(math.radians(-35),4,'X')
    foot_motion=Matrix.Translation(Vector((-sign*.004,.05,-.02)))@Matrix.Translation(pivot)@rotation@Matrix.Translation(-pivot)
    new_toe=(foot_motion@rest[toe]).translation
    toe_motion=Matrix.Translation(new_toe)@Matrix.Rotation(math.radians(35),4,'X')@Matrix.Translation(-new_toe)@foot_motion
    transforms=[(toe_motion if below(i,toe) else foot_motion if below(i,foot) else Matrix.Identity(4)) for i in range(len(rest))]
    boot=next(s for s in data['surfaces'] if s['role']=='shoe' and (('左' in s['name'] or 'Left' in s['name'])==(side==0)))
    boot_matrix=Matrix(np.asarray(boot['matrix']).reshape(4,4).tolist())
    boot_points=np.asarray([list(boot_matrix@Vector(p)) for p in np.asarray(boot['vertices']).reshape(-1,3)])
    boot_triangles=np.asarray(boot['triangles']).reshape(-1,3)
    boot_ids=np.asarray(boot['boneIndices']).reshape(-1,4);boot_weights=np.asarray(boot['weights']).reshape(-1,4)
    tree=BVHTree.FromPolygons([Vector(p) for p in boot_points],np.asarray(boot['triangles']).reshape(-1,3).tolist(),all_triangles=True)
    knee=next(i for i,b in enumerate(data['bones']) if b['name']==prefix+'LowerLeg')
    def cavity_fit(point):
        p=np.array(point,dtype=float,copy=True)
        # Calf fit follows the native boot surface at this height; no global
        # leg scaling, topology change, or invented replacement anatomy.
        if .16<p[1]<.64:
            amount=np.clip((p[1]-pivot.y)/(rest[knee].translation.y-pivot.y),0,1)
            center=np.array(pivot.lerp(rest[knee].translation,float(amount)),copy=True);center[1]=p[1]
            radial=Vector(p-center);length=radial.length
            if length>.001:
                hit,normal,index,distance=tree.ray_cast(Vector(center),radial.normalized(),.3)
                if hit is not None and length>distance-.0025:p=center+np.asarray(radial.normalized())*max(.005,distance-.0025)
        # In the toe box use its actual local cross section, leaving a small
        # fabric allowance. The original flat-foot shape is restored at zero.
        if p[1]<.18 and p[2]<.10:
            section=boot_points[(np.abs(boot_points[:,2]-p[2])<.014)&(boot_points[:,1]<(.18 if p[2]<-.015 else .24))&(boot_points[:,1]>(0 if p[2]<-.015 else .06))]
            if len(section)>=6:
                low,high=section[:,:2].min(0),section[:,:2].max(0)
                center=(low+high)*.5;radius=np.maximum((high-low)*.5-.0025,.003)
                offset=p[:2]-center;ratio=np.linalg.norm(offset/radius)
                if ratio>1:p[:2]=center+offset/ratio
        return Vector(p)
    for surface in data['surfaces']:
        if surface['role']!='hosiery' or (('左' in surface['name'] or 'Left' in surface['name']) != (side==0)):continue
        positions=np.asarray(surface['vertices']).reshape(-1,3)
        normals=np.asarray(surface['normals']).reshape(-1,3)
        ids=np.asarray(surface['boneIndices']).reshape(-1,4);weights=np.asarray(surface['weights']).reshape(-1,4)
        to_world=Matrix(np.asarray(surface['matrix']).reshape(4,4).tolist());to_local=to_world.inverted()
        fitted_ids=ids.copy();fitted_weights=weights.copy();source_joints={b:i for i,b in enumerate(surface['joints'])}
        delta=[];normal_delta=[]
        for i,p in enumerate(positions):
            matrix=Matrix(np.zeros((4,4)).tolist())
            for j in range(4):
                index=surface['joints'][ids[i,j]]
                matrix+=to_local@transforms[index]@to_world*float(weights[i,j])
            original=Vector(p);target=to_local@cavity_fit(to_world@(matrix@original));delta.extend(target-original)
            original_n=Vector(normals[i]);target_n=(matrix.to_3x3().inverted().transposed()@original_n).normalized();normal_delta.extend(target_n-original_n)
            original_world=to_world@original
            if .20<original_world.y<.64:
                hit,normal,triangle,distance=tree.find_nearest(original_world)
                if hit is not None:
                    vertices=boot_triangles[triangle];a,b,c=boot_points[vertices]
                    v0=b-a;v1=c-a;v2=np.asarray(hit)-a
                    d00=np.dot(v0,v0);d01=np.dot(v0,v1);d11=np.dot(v1,v1);d20=np.dot(v2,v0);d21=np.dot(v2,v1)
                    denominator=d00*d11-d01*d01
                    if abs(denominator)>1e-12:
                        v=(d11*d20-d01*d21)/denominator;w=(d00*d21-d01*d20)/denominator
                        bary=np.maximum(np.array([1-v-w,v,w]),0);bary/=bary.sum();combined={}
                        for corner,vertex in enumerate(vertices):
                            for slot in range(4):
                                joint=source_joints[boot['joints'][boot_ids[vertex,slot]]]
                                combined[joint]=combined.get(joint,0)+float(bary[corner]*boot_weights[vertex,slot])
                        strongest=sorted(combined.items(),key=lambda x:-x[1])[:4];total=sum(w for _,w in strongest)
                        fitted_ids[i]=0;fitted_weights[i]=0
                        for slot,(joint,weight) in enumerate(strongest):fitted_ids[i,slot]=joint;fitted_weights[i,slot]=weight/total
        name='Continuous'+('Left' if side==0 else 'Right')+'Hosiery'
        result={'vertexCount':len(positions),'delta':delta,'normalDelta':normal_delta,'boneNames':[data['bones'][j]['name'] for j in surface['joints']],'boneIndices':fitted_ids.flatten().tolist(),'weights':fitted_weights.flatten().tolist(),'source':'Original complete foot; virtual heel raise, native boot cavity and calf-weight fit; zero weight restores source exactly'}
        (OUT/('FootLast_'+name+'.json')).write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
        print('AUTHORED WORN FOOT',side,len(positions),'maximum offset',float(np.linalg.norm(np.asarray(delta).reshape(-1,3),axis=1).max()))

# Add the same corrective to the separate editable work file. Mesh identity,
# basis vertices, original rig and existing pose/boot shapes remain unchanged.
study=ROOT/'DCC/Blender/InteractionStudy.blend'
if study.exists():
    bpy.ops.wm.open_mainfile(filepath=str(study))
    conversion=np.array(((1,0,0),(0,0,1),(0,1,0)))
    for surface in data['surfaces']:
        if surface['role']!='hosiery':continue
        obj=bpy.data.objects.get(surface['name'])
        if obj is None:continue
        side='Left' if ('Left' in surface['name'] or '左' in surface['name']) else 'Right'
        shape=json.loads((OUT/('FootLast_Continuous'+side+'Hosiery.json')).read_text(encoding='utf-8'))
        if not obj.data.shape_keys:obj.shape_key_add(name='Basis')
        keys=obj.data.shape_keys.key_blocks
        key=keys.get('WornHighHeel') or obj.shape_key_add(name='WornHighHeel')
        basis=np.asarray([v.co[:] for v in keys[0].data])
        matrix=np.asarray(surface['matrix']).reshape(4,4)[:3,:3]
        offsets=np.asarray(shape['delta']).reshape(-1,3)@(conversion@matrix).T
        key.data.foreach_set('co',(basis+offsets).astype(np.float32).flatten());key.value=0
        obj['foot_last_note']='WornHighHeel = 1 for shoe fit; 0 restores complete original barefoot mesh'
        preview_name=surface['name']+'_WornFitPreview'
        previous=bpy.data.objects.get(preview_name)
        if previous is not None:bpy.data.objects.remove(previous,do_unlink=True)
        preview=obj.copy();preview.data=obj.data.copy();preview.name=preview_name
        obj.users_collection[0].objects.link(preview)
        assert preview.data.shape_keys.as_pointer()!=obj.data.shape_keys.as_pointer(),'Fitting preview must own its shape keys'
        preview.vertex_groups.clear()
        groups=[preview.vertex_groups.new(name=name) for name in shape['boneNames']]
        fitted_ids=np.asarray(shape['boneIndices']).reshape(-1,4);fitted_weights=np.asarray(shape['weights']).reshape(-1,4)
        for vertex in range(len(basis)):
            for slot in range(4):
                if fitted_weights[vertex,slot]>0:groups[fitted_ids[vertex,slot]].add([vertex],float(fitted_weights[vertex,slot]),'REPLACE')
        preview.data.shape_keys.key_blocks['WornHighHeel'].value=1
        preview['purpose']='Optional worn native-calf-weight preview; original object and weights are retained'
        preview.hide_render=True;preview.hide_set(True)
    bpy.ops.wm.save_as_mainfile(filepath=str(study))
