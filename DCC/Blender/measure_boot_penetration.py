"""Nearest outer-surface diagnostic, not a closed-volume or visibility proof."""
import json,numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path('F:/Unity/Project/StartMotionValidation/Artifacts/FootwearMotionReview')
results=[]
for actor in [0,2]:
    for frame,side in [(390,0),(396,0),(405,0),(420,0),(570,1),(585,1),(600,1)]:
        sample=json.loads((ROOT/str(actor)/'Geometry'/f'{frame}.json').read_text())['sides'][side]
        boot=sample['boot'];v=np.asarray(boot['vertices']).reshape(-1,3);f=np.asarray(boot['triangles']).reshape(-1,3);n=np.asarray(boot['normals']).reshape(-1,3)
        tree=BVHTree.FromPolygons([Vector(p) for p in v],f.tolist(),all_triangles=True)
        body=np.asarray(sample['sock']['vertices']).reshape(-1,3);matrix=np.asarray(sample['worldToBoot']).reshape(4,4)
        local=(np.column_stack((body,np.ones(len(body))))@matrix.T)[:,:3]
        distances=[]
        for p,q in zip(body,local):
            if not .20<q[1]<.60:continue
            hit,normal,triangle,distance=tree.find_nearest(Vector(p))
            if hit is None:continue
            outer=n[f[triangle]].mean(0);outer/=max(np.linalg.norm(outer),1e-9)
            signed=float(np.dot(p-np.asarray(hit),outer))
            if signed>0:distances.append(signed)
        results.append(dict(character=actor,frame=frame,side=side,outwardVertices=len(distances),maxOutwardM=max(distances,default=0),p95OutwardM=float(np.percentile(distances,95)) if distances else 0))
print(json.dumps(results,indent=2))
(ROOT/'penetration-diagnostic.json').write_text(json.dumps(results,indent=2))
