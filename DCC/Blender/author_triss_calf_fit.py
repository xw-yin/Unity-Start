"""Fit Triss's original calf to native DLC boot weights; restore after exit.

No high-heel conversion, replacement foot, or hiding mask is added here.
"""
import json
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2]
data=json.loads(Path('F:/Unity/Project/StartMotionValidation/Artifacts/BlenderOptimization/Exchange/Triss.json').read_text(encoding='utf-8-sig'))
def mat(values):return Matrix(np.asarray(values).reshape(4,4).tolist())
for side in ['Left','Right']:
    boot=next(s for s in data['surfaces'] if s['role']=='shoe' and side in s['name'])
    to_world=mat(boot['matrix']);bp=np.asarray([list(to_world@Vector(p)) for p in np.asarray(boot['vertices']).reshape(-1,3)])
    triangles=np.asarray(boot['triangles']).reshape(-1,3)
    bi=np.asarray(boot['boneIndices']).reshape(-1,4);bw=np.asarray(boot['weights']).reshape(-1,4)
    normal_matrix=to_world.to_3x3().inverted().transposed()
    boot_normals=np.asarray([list((normal_matrix@Vector(n)).normalized()) for n in np.asarray(boot['normals']).reshape(-1,3)])
    tree=BVHTree.FromPolygons([Vector(p) for p in bp],triangles.tolist(),all_triangles=True)
    skin=next(s for s in data['surfaces'] if s['role']=='hosiery' and ('Continuous'+side+'Hosiery.asset') in s['path'])
    positions=np.asarray(skin['vertices']).reshape(-1,3)
    transform=mat(skin['matrix']);inverse=transform.inverted()
    ids=np.asarray(skin['boneIndices']).reshape(-1,4);weights=np.asarray(skin['weights']).reshape(-1,4)
    source_joint={j:i for i,j in enumerate(skin['joints'])}
    fitted_ids=ids.copy();fitted_weights=weights.copy();delta=np.zeros_like(positions)
    changed=0
    for i,p in enumerate(positions):
        world=transform@Vector(p)
        if not .20<world.y<.64:continue
        hit,normal,triangle,distance=tree.find_nearest(world)
        if hit is None or distance>.04:continue
        a,b,c=bp[triangles[triangle]];v0=b-a;v1=c-a;v2=np.asarray(hit)-a
        d00=v0@v0;d01=v0@v1;d11=v1@v1;denominator=d00*d11-d01*d01
        if abs(denominator)<1e-12:continue
        v=(d11*(v2@v0)-d01*(v2@v1))/denominator
        w=(d00*(v2@v1)-d01*(v2@v0))/denominator
        bary=np.maximum([1-v-w,v,w],0);bary/=bary.sum();combined={}
        normal=Vector(bary@boot_normals[triangles[triangle]]).normalized()
        # Correct only calf vertices beyond the actual nearby native boot
        # surface. The original foot, knee and topology stay untouched.
        if (world-hit).dot(normal)>0:
            target=inverse@(hit-normal*.0025)
            delta[i]=np.asarray(target)-p
        for corner,vertex in enumerate(triangles[triangle]):
            for slot in range(4):
                amount=float(bary[corner]*bw[vertex,slot])
                if amount<=0:continue
                joint=source_joint[boot['joints'][bi[vertex,slot]]]
                combined[joint]=combined.get(joint,0)+amount
        strongest=sorted(combined.items(),key=lambda x:-x[1])[:4];total=sum(w for _,w in strongest)
        fitted_ids[i]=0;fitted_weights[i]=0
        for slot,(joint,amount) in enumerate(strongest):fitted_ids[i,slot]=joint;fitted_weights[i,slot]=amount/total
        changed+=1
    name='Continuous'+side+'Hosiery'
    result={'character':'Triss','vertexCount':len(positions),'baseVertices':positions.astype(np.float32).ravel().tolist(),'delta':delta.ravel().tolist(),
            'normalDelta':np.zeros_like(delta).ravel().tolist(),'boneNames':[data['bones'][j]['name'] for j in skin['joints']],
            'boneIndices':fitted_ids.ravel().tolist(),'weights':fitted_weights.ravel().tolist(),
            'source':'Original Triss foot unchanged; local calf cavity fit and native DLC boot weights while worn, restored after exit'}
    (ROOT/'Assets/Homecoming/Resources/Homecoming/Poses'/('TrissFootLast_'+name+'.json')).write_text(json.dumps(result,separators=(',',':')),encoding='utf-8')
    print('TRISS NATIVE CALF FIT',side,changed,'of',len(positions),'maximum local offset',float(np.linalg.norm(delta,axis=1).max()))

study=ROOT/'DCC/Blender/TrissGather.blend'
if study.exists():
    bpy.ops.wm.open_mainfile(filepath=str(study))
    for side in ['Left','Right']:
        surface=next(s for s in data['surfaces'] if s['role']=='hosiery' and ('Continuous'+side+'Hosiery.asset') in s['path'])
        original=bpy.data.objects[surface['name']]
        name='Triss_'+side+'_NativeCalfPreview'
        previous=bpy.data.objects.get(name)
        if previous:bpy.data.objects.remove(previous,do_unlink=True)
        preview=original.copy();preview.data=original.data.copy();preview.name=name
        original.users_collection[0].objects.link(preview)
        shape=json.loads((ROOT/'Assets/Homecoming/Resources/Homecoming/Poses'/('TrissFootLast_Continuous'+side+'Hosiery.json')).read_text(encoding='utf-8'))
        preview.vertex_groups.clear();groups=[preview.vertex_groups.new(name=n) for n in shape['boneNames']]
        transform=np.asarray(surface['matrix']).reshape(4,4)[:3,:3]
        conversion=np.array(((1,0,0),(0,0,1),(0,1,0)))
        positions=np.asarray([v.co[:] for v in preview.data.vertices])
        offsets=np.asarray(shape['delta']).reshape(-1,3)@(conversion@transform).T
        # An independent preview basis, never the retained original object's data.
        if preview.data.shape_keys:
            for key in preview.data.shape_keys.key_blocks:
                coords=np.asarray([v.co[:] for v in key.data]);key.data.foreach_set('co',(coords+offsets).astype(np.float32).ravel())
        else:preview.data.vertices.foreach_set('co',(positions+offsets).astype(np.float32).ravel())
        indices=np.asarray(shape['boneIndices']).reshape(-1,4);weights=np.asarray(shape['weights']).reshape(-1,4)
        for i in range(len(indices)):
            for slot in range(4):
                if weights[i,slot]>0:groups[indices[i,slot]].add([i],float(weights[i,slot]),'REPLACE')
        preview['purpose']='Optional native DLC calf-weight preview; original anatomy and weights retained separately'
        preview.hide_set(True);preview.hide_render=True
    bpy.ops.wm.save_as_mainfile(filepath=str(study))
