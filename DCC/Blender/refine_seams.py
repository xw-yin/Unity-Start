"""Repair verified skinning seams in Blender without replacing game anatomy.

Only vertices sharing a geometric edge are paired. UV splits, hard normals,
vertex ordering, triangles, bind poses, shape keys and separate shoe/sock meshes
are preserved. Changes are written as reviewed, sparse Unity overrides.
"""
import json
import struct
import hashlib
from collections import defaultdict
from pathlib import Path
import bpy
import numpy as np

PROJECT=Path(r'F:\Unity\Project\Start')
EXCHANGE=Path(r'F:\Unity\Project\StartMotionValidation\Artifacts\BlenderOptimization\Exchange')

def seam_groups(surface):
    vertices=np.asarray(surface['vertices'],dtype=np.float64).reshape(-1,3)
    keys=[tuple(np.round(v,6)) for v in vertices]
    neighbors=[set() for _ in vertices]
    for a,b,c in np.asarray(surface['triangles']).reshape(-1,3):
        neighbors[a].update((keys[b],keys[c]))
        neighbors[b].update((keys[a],keys[c]))
        neighbors[c].update((keys[a],keys[b]))
    coincident=defaultdict(list)
    for i,key in enumerate(keys):coincident[key].append(i)
    groups=[]
    for group in coincident.values():
        if len(group)<2:continue
        pending=set(group)
        while pending:
            connected=[pending.pop()]
            for a in connected:
                for b in list(pending):
                    if neighbors[a]&neighbors[b]:connected.append(b);pending.remove(b)
            if len(connected)>1:groups.append(sorted(connected))
    return groups

def weights_as_maps(surface):
    ids=np.asarray(surface['boneIndices']).reshape(-1,4)
    weights=np.asarray(surface['weights']).reshape(-1,4)
    return [{int(b):float(w) for b,w in zip(ids[i],weights[i]) if w>0} for i in range(len(ids))]

def write_groups(obj,surface,weights):
    joint_names=[surface['_bone_names'][j] for j in surface['joints']]
    for i,item in enumerate(weights):
        for group in obj.vertex_groups:group.remove([i])
        for bone,amount in item.items():obj.vertex_groups[joint_names[bone]].add([i],float(amount),'REPLACE')

def measured_gap(package,surface,groups,weights):
    if not groups:return 0.0
    points=np.asarray(surface['vertices']).reshape(-1,3)
    transform=np.asarray(surface['matrix']).reshape(4,4)
    points=np.column_stack((points,np.ones(len(points))))@transform.T
    inverses=[np.linalg.inv(np.asarray(package['bones'][j]['matrix']).reshape(4,4)) for j in surface['joints']]
    maximum=0.0
    used=sorted({v for g in groups for v in g})
    for pose in package['poses']:
        matrices=np.asarray(pose['bones']).reshape(-1,4,4)
        skin=[matrices[j]@inverses[k] for k,j in enumerate(surface['joints'])]
        deformed={i:sum((skin[b]@points[i])*w for b,w in weights[i].items())[:3] for i in used}
        for group in groups:
            for i in group[1:]:maximum=max(maximum,float(np.linalg.norm(deformed[i]-deformed[group[0]])))
    return maximum

def refine_character(name,collection):
    package=json.loads((EXCHANGE/(name+'.json')).read_text(encoding='utf-8'))
    changes=[];summaries=[];sparse=[]
    for surface in package['surfaces']:
        surface['_bone_names']=[b['name'] for b in package['bones']]
        # Facial cards and hair intentionally have overlapping independent layers.
        if surface['name'].startswith(('h_','he_','h_wa__','i_')):continue
        groups=seam_groups(surface)
        original=weights_as_maps(surface);refined=[dict(v) for v in original]
        repaired=[]
        for group in groups:
            keys={b for i in group for b in original[i]}
            difference=max(sum(abs(original[i].get(b,0)-original[group[0]].get(b,0)) for b in keys) for i in group)
            if difference<.001:continue
            avg={b:sum(original[i].get(b,0) for i in group)/len(group) for b in keys}
            strongest=sorted(avg,key=lambda b:(-avg[b],b))[:4]
            total=sum(avg[b] for b in strongest)
            value={b:avg[b]/total for b in strongest}
            for i in group:refined[i]=dict(value)
            repaired.append(group)
        # Preserve hard edges, but make smooth UV seam normals identical.
        normals=np.asarray(surface['normals'],dtype=np.float64).reshape(-1,3)
        old_normals=normals.copy()
        smooth_normal_groups=0
        for group in groups:
            n=normals[group]
            n=n/np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-12)
            if np.min(n@n.T)>.985 and np.max(np.linalg.norm(n-n[0],axis=1))>.001:
                mean=np.mean(n,axis=0);mean/=np.linalg.norm(mean)
                normals[group]=mean;smooth_normal_groups+=1
        # Restored covered arms were reshaped under the sleeve. Recalculate
        # their actual surface normals, leaving original hands/forearms intact.
        covered_normals=0
        if name=='Shani' and surface['name'].startswith('ta_01_wa__body_shani'):
            raw=PROJECT/'Assets/ThirdParty/WitcherShani/Prepared/Part_21.bin'
            original_count=struct.unpack('<i',raw.read_bytes()[:4])[0]
            positions=np.asarray(surface['vertices']).reshape(-1,3)
            triangles=np.asarray(surface['triangles']).reshape(-1,3)
            accumulated=np.zeros_like(positions)
            cross=np.cross(positions[triangles[:,1]]-positions[triangles[:,0]],positions[triangles[:,2]]-positions[triangles[:,0]])
            for corner in range(3):np.add.at(accumulated,triangles[:,corner],cross)
            valid=np.linalg.norm(accumulated,axis=1)>1e-12
            indices=np.where(valid & (np.arange(len(positions))>=original_count))[0]
            new=accumulated[indices]/np.linalg.norm(accumulated[indices],axis=1)[:,None]
            # The imported game's winding convention is verified against its normals.
            if np.mean(np.sum(new*old_normals[indices],axis=1))<0:new=-new
            normals[indices]=new;covered_normals=len(indices)
        if not repaired and not smooth_normal_groups and not covered_normals:continue
        obj=next(o for o in collection.objects if o.get('unity_mesh_path')==surface['path'])
        before=measured_gap(package,surface,repaired,original)
        after=measured_gap(package,surface,repaired,refined)
        assert after<.000005, (surface['name'],after)
        write_groups(obj,surface,refined)
        normal_matrix=np.linalg.inv(np.array(((1,0,0),(0,0,1),(0,1,0)))@np.asarray(surface['matrix']).reshape(4,4)[:3,:3]).T
        converted=normals@normal_matrix.T
        converted/=np.maximum(np.linalg.norm(converted,axis=1)[:,None],1e-12)
        obj.data.normals_split_custom_set_from_vertices(converted.tolist())
        ids=[];amounts=[]
        for item in refined:
            pairs=list(item.items())+[(0,0)]*(4-len(item))
            ids.extend(b for b,w in pairs);amounts.extend(w for b,w in pairs)
        changes.append({'path':surface['path'],'vertices':surface['vertices'],'normals':normals.flatten().tolist(),'boneIndices':ids,'weights':amounts})
        entries=[]
        for i in range(len(refined)):
            weight_changed=refined[i]!=original[i]
            normal_changed=np.linalg.norm(normals[i]-old_normals[i])>1e-6
            if weight_changed or normal_changed:
                pairs=list(refined[i].items())+[(0,0)]*(4-len(refined[i]))
                entries.append({'index':i,'normal':normals[i].tolist(),'bones':[b for b,w in pairs],'weights':[w for b,w in pairs]})
        fingerprint=hashlib.sha256(struct.pack('<i',len(refined))+np.asarray(surface['vertices'],dtype='<f4').tobytes()+struct.pack('<i',len(surface['triangles']))+np.asarray(surface['triangles'],dtype='<i4').tobytes()+struct.pack('<i',len(surface['joints']))).hexdigest()
        sparse.append({'path':surface['path'],'fingerprint':fingerprint,'vertexCount':len(refined),'vertices':entries})
        summaries.append({'surface':surface['name'],'path':surface['path'],'weight_seam_groups':len(repaired),
                          'smooth_normal_groups':smooth_normal_groups,'covered_arm_normals':covered_normals,
                          'before_max_seam_gap_mm':round(before*1000,6),'after_max_seam_gap_mm':round(after*1000,6),
                          'changed_vertices':len(entries),'geometry_changed':False})
    print(json.dumps({'character':name,'edited_surfaces':len(summaries),'details':summaries},ensure_ascii=False))
    return changes,sparse,summaries

def refine_all():
    exec(compile((PROJECT/'DCC/Blender/character_workspace.py').read_text(encoding='utf-8'),'character_workspace.py','exec'),globals())
    all_edits=[];all_sparse=[];report={}
    for file in sorted(EXCHANGE.glob('*.json')):
        if file.name=='edits.json':continue
        name=file.stem
        collection=bpy.data.collections.get('Unity_'+name)
        if collection is None:_,collection=import_character(name)
        changes,sparse,summaries=refine_character(name,collection)
        all_edits.extend(changes);all_sparse.extend(sparse);report[name]=summaries
        for obj in collection.objects:obj.hide_set(True)
    (EXCHANGE/'edits.json').write_text(json.dumps({'surfaces':all_edits}),encoding='utf-8')
    (PROJECT/'DCC/Blender/refinements.json').write_text(json.dumps({'surfaces':all_sparse},indent=2),encoding='utf-8')
    (PROJECT/'Artifacts/BlenderOptimization/model-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    for obj in bpy.data.collections['Unity_Shani'].objects:
        obj.hide_set(obj.type!='MESH' or not obj.get('unity_enabled',False))
    bpy.context.scene.frame_set(570)
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(PROJECT/'DCC/Blender/Characters.blend'),compress=True)
    print('BLENDER CHARACTER REFINEMENTS COMPLETE')
