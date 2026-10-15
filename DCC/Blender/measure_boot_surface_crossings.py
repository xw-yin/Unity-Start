"""Actual source-surface triangle crossings, plus a necessary cuff-exit check.

No rendering mask is applied. Alpha openings and edge contact can still produce
triangle pairs; this is a diagnostic, not a verdict on visible penetration.
"""
import sys,json
from pathlib import Path
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
project=Path('F:/Unity/Project/Start')
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
stage=args[0] if args else 'seat-boot-candidate'
root=Path('F:/Unity/Project/StartMotionValidation/Artifacts/SeatBootReview')/stage
results=[]
for actor in [0,2]:
    for path in sorted((root/str(actor)/'Geometry').glob('*.json'),key=lambda p:int(p.stem)):
        data=json.loads(path.read_text());frame=int(path.stem)
        for side in range(2):
            if not (389<=frame<=450 and side==0 or 570<=frame<=630 and side==1):continue
            sample=data['sides'][side]
            def tree(surface):
                p=np.asarray(surface['vertices']).reshape(-1,3)
                f=np.asarray(surface['triangles']).reshape(-1,3)
                return BVHTree.FromPolygons([Vector(v) for v in p],f.tolist(),all_triangles=True),p,f
            bt,bp,bf=tree(sample['boot']);st,sp,sf=tree(sample['sock'])
            pairs=bt.overlap(st)
            entry={'actor':actor,'storyFrame':frame,'side':side,'trianglePairs':len(pairs),'bootTriangles':len(set(p[0] for p in pairs)),'skinTriangles':len(set(p[1] for p in pairs))}
            if sample.get('footVertices') and sample.get('cuff'):
                foot=sp[sample['footVertices']];axis=np.array(sample['axis']);axis/=np.linalg.norm(axis)
                clearance=(foot-np.array(sample.get('lip',sample['cuff'])))@axis
                entry.update(elapsed=data['elapsed'],action=data['action'],clearAction=sample['clearAction'],minimumFootBeyondCuffM=float(clearance.min()),maximumFootBeyondCuffM=float(clearance.max()))
                ids=set(sample['footVertices'])
                entry['footTrianglePairs']=sum(any(int(i) in ids for i in sf[p[1]]) for p in pairs)
                if pairs:
                    centres=sp[sf[list(set(p[1] for p in pairs))]].mean(axis=1)
                    along=(centres-np.array(sample['cuff']))@axis
                    entry['crossingRangeFromCuffM']=[float(along.min()),float(along.max())]
            results.append(entry)
out=project/'Artifacts/AnimationSkillStudy'/f'{stage}-surface-crossings.json'
out.write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
