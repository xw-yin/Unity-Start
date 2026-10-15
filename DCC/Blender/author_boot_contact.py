"""Bake local upper-boot pressure correctives from actual Unity motion samples.

Source topology/UVs, soles and the complete original foot are preserved.
Nearest-surface pressure is followed by triangle-crossing checks; residual
crossings are reported, never replaced by a rendering-mask result.
"""
import json
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
ROOT=Path('F:/Unity/Project/Start')
INPUT=Path('F:/Unity/Project/StartMotionValidation/Artifacts/SeatBootReview/contact-source')
OUT=ROOT/'Artifacts/AnimationSkillStudy/BootContactPending'
OUT.mkdir(parents=True,exist_ok=True)
report=[];study=[]
for actor in [0,2]:
    samples=[json.loads(p.read_text()) for p in sorted((INPUT/str(actor)/'Geometry').glob('*.json'),key=lambda p:int(p.stem))]
    for side in [0,1]:
        start=12+side*6
        frames=[];basis=None;name=None;opening=None
        for sample in samples:
            if not start<=sample['action']<sample['sides'][side]['clearAction']:continue
            s=sample['sides'][side]
            if 'basis' not in s:raise ValueError('Re-export source geometry with bind identity')
            name=s['mesh'];basis=np.asarray(s['basis']).reshape(-1,3)
            bp=np.asarray(s['boot']['vertices']).reshape(-1,3);bf=np.asarray(s['boot']['triangles']).reshape(-1,3)
            sp=np.asarray(s['sock']['vertices']).reshape(-1,3);sf=np.asarray(s['sock']['triangles']).reshape(-1,3)
            normals=np.asarray(s['sock']['normals']).reshape(-1,3)
            face_normals=np.cross(sp[sf[:,1]]-sp[sf[:,0]],sp[sf[:,2]]-sp[sf[:,0]])
            face_normals/=np.maximum(np.linalg.norm(face_normals,axis=1)[:,None],1e-12)
            sign=-1 if np.mean(np.sum(face_normals*normals[sf].mean(axis=1),axis=1))<0 else 1
            face_normals*=sign
            to_local=np.asarray(s['worldToBoot']).reshape(4,4)
            local=(np.column_stack((bp,np.ones(len(bp))))@to_local.T)[:,:3]
            relaxed=np.asarray(s['relaxed']).reshape(-1,3)
            soft=np.clip((relaxed[:,1]-.065)/.045,0,1)
            local_cuff=(to_local@np.r_[np.asarray(s['cuff']),1])[:3]
            grip=int(np.argmin(np.linalg.norm(local-local_cuff,axis=1)));soft[grip]=0
            body=BVHTree.FromPolygons([Vector(p) for p in sp],sf.tolist(),all_triangles=True)
            proposed=bp.copy();before=None
            for iteration in range(12):
                boot=BVHTree.FromPolygons([Vector(p) for p in proposed],bf.tolist(),all_triangles=True)
                pairs=boot.overlap(body)
                if before is None:before=len(pairs)
                if not pairs:break
                active=set(int(v) for p in pairs for v in bf[p[0]])
                changed=False
                for i in active:
                    if soft[i]<=0:continue
                    hit,normal,tri,distance=body.find_nearest(Vector(proposed[i]))
                    if hit is None or distance>.07:continue
                    normal=face_normals[tri]
                    signed=float((proposed[i]-np.asarray(hit))@normal)
                    push=max(0,.006-signed)
                    # Intersecting broad faces can have vertices outside the skin.
                    # Small additional pressure iterations clear their chords.
                    if iteration>0:push=max(push,.002)
                    candidate=proposed[i]+normal*push*soft[i]
                    delta=candidate-bp[i];length=np.linalg.norm(delta)
                    if length>.045:candidate=bp[i]+delta*.045/length
                    changed|=np.linalg.norm(candidate-proposed[i])>1e-7;proposed[i]=candidate
                if not changed:break
            after=len(BVHTree.FromPolygons([Vector(p) for p in proposed],bf.tolist(),all_triangles=True).overlap(body))
            delta=(proposed-bp)@to_local[:3,:3].T
            phase=(sample['action']-start)/(s['clearAction']-start)
            frames.append((phase,delta))
            if opening is None:
                bone_ids=np.asarray(s['boneIndices']).reshape(-1,4);weights=np.asarray(s['weights']).reshape(-1,4)
                skin_matrices=np.asarray(s['skinMatrices']).reshape(-1,4,4)[:,:3,:3]
                deform=(skin_matrices[bone_ids]*weights[:,:,None,None]).sum(axis=1)
                opening=np.linalg.solve(deform,(proposed-bp)[:,:,None]).squeeze(-1)
            report.append({'actor':actor,'side':side,'phase':phase,'beforePairs':before,'afterPairs':after,'maximumOffsetM':float(np.linalg.norm(delta,axis=1).max())})
            if sample['frame']==405+side*180:study.append((name,bp,proposed,bf,sp,sf))
        if not frames:raise ValueError(f'Missing extraction data: {actor}/{side}')
        active=np.flatnonzero(np.max(np.stack([np.linalg.norm(d,axis=1) for _,d in frames]),axis=0)>1e-7)
        # Pre-loosen the worn upper before transfer, so the first contact
        # correction is already present instead of popping on when socks appear.
        keys=[{'phase':0,'delta':frames[0][1][active].flatten().tolist()}]
        keys += [{'phase':float(p),'delta':d[active].flatten().tolist()} for p,d in frames]
        keys.append({'phase':1,'delta':[0.0]*(len(active)*3)})
        payload={'vertexCount':len(basis),'baseVertices':basis.flatten().tolist(),'vertices':active.tolist(),'opening':opening[active].flatten().tolist(),'keys':keys,'source':'Contact-source Unity samples; Blender nearest-skin pressure and triangle checks; hard soles and source foot unchanged'}
        character='YenneferDLC' if actor==0 else 'Triss'
        (OUT/f'BootContact_{character}_{name}.json').write_text(json.dumps(payload,separators=(',',':')))
        print('BOOT CONTACT CLIP',name,'vertices',len(active),'keys',len(keys),'remaining max',max(r['afterPairs'] for r in report if r['actor']==actor and r['side']==side))
(ROOT/'Artifacts/AnimationSkillStudy/boot-contact-authoring.json').write_text(json.dumps(report,indent=2))
# Separate diagnostic work file, retaining both actual source and proposed surfaces.
bpy.ops.wm.read_factory_settings(use_empty=True)
for case,(name,original,fitted,faces,body,body_faces) in enumerate(study):
    collection=bpy.data.collections.new(name);bpy.context.scene.collection.children.link(collection)
    for label,points,tris,color in [('Original',original,faces,(.4,.18,.05,1)),('Pressure',fitted,faces,(.15,.45,.15,1)),('OriginalFoot',body,body_faces,(.6,.35,.25,1))]:
        mesh=bpy.data.meshes.new(name+'_'+label);mesh.from_pydata(points[:,[0,2,1]].tolist(),[],tris[:,[0,2,1]].tolist());mesh.update()
        obj=bpy.data.objects.new(mesh.name,mesh);collection.objects.link(obj);obj.location.x+=case*2
        mat=bpy.data.materials.new(label);mat.diffuse_color=color;mesh.materials.append(mat)
        obj['source']='Actual Unity source-pose geometry; diagnostic material';obj.hide_viewport=label=='Original';obj.hide_render=label=='Original'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'DCC/Blender/BootOpeningStudy.blend'))
