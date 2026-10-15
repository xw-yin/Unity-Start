import json
from pathlib import Path
import numpy as np
root=Path('F:/Unity/Project/StartMotionValidation/Artifacts/SeatBootReview/contact-source')
results=[]
for actor in [0,2]:
    s=json.loads((root/str(actor)/'Geometry/405.json').read_text())['sides'][0]
    p=np.asarray(s['relaxed']).reshape(-1,3);f=np.asarray(s['boot']['triangles']).reshape(-1,3)
    weld={};ids=[];points=[]
    for v in p:
        key=tuple(np.round(v*1e5).astype(int))
        if key not in weld:weld[key]=len(points);points.append(v)
        ids.append(weld[key])
    points=np.asarray(points);counts={}
    for tri in f:
        a,b,c=[ids[i] for i in tri]
        for edge in [(a,b),(b,c),(c,a)]:
            key=tuple(sorted(edge));counts[key]=counts.get(key,0)+1
    adj={}
    for (a,b),count in counts.items():
        if count!=1:continue
        adj.setdefault(a,set()).add(b);adj.setdefault(b,set()).add(a)
    remaining=set(adj);loops=[]
    while remaining:
        queue=[remaining.pop()];part=set(queue)
        while queue:
            for nxt in adj[queue.pop()]:
                if nxt in remaining:remaining.remove(nxt);part.add(nxt);queue.append(nxt)
        q=points[list(part)];span=q.max(0)-q.min(0)
        if len(part)>=8:loops.append({'vertices':len(part),'mean':q.mean(0).tolist(),'minimum':q.min(0).tolist(),'maximum':q.max(0).tolist(),'span':span.tolist()})
    m=np.asarray(s['worldToBoot']).reshape(4,4);c=(m@np.r_[s['cuff'],1])[:3]
    result={'actor':actor,'mesh':s['mesh'],'cuff':c.tolist(),'loops':loops};results.append(result)
print(json.dumps(results,indent=2))
Path('F:/Unity/Project/Start/Artifacts/AnimationSkillStudy/boot-mouth-inspection.json').write_text(json.dumps(results,indent=2))
