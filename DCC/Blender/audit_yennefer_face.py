"""Read original face data and source rig without modifying the source blend."""
import sys,json,hashlib
from pathlib import Path
import bpy
sys.path[:0]=['F:/Unity/Tools/WitcherConversion','F:/Unity/Tools/WitcherConversion/site-packages']
from witcher3_tools.importers.import_rig import loadFaceFile
source=Path('F:/Unity/ExtractedModels/Yennefer/characters/models/main_npc/yennefer/h_01_wa__yennefer/h_01_wa__yennefer.w3fac')
face=loadFaceFile(str(source))
out=Path('F:/Unity/Project/Start/Artifacts/FaceReview-20261021');out.mkdir(parents=True,exist_ok=True)
def vec(v):return list(v) if isinstance(v,(list,tuple)) else [v.x,v.y,v.z] if hasattr(v,'x') else [v.X,v.Y,v.Z]
def quat(v):return list(v) if isinstance(v,(list,tuple)) else [v.X,v.Y,v.Z,v.W] if hasattr(v,'X') else [v.x,v.y,v.z,v.w]
data={'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'bones':face.mimicSkeleton.names,'parents':face.mimicSkeleton.parentIdx,'positions':[quat(v) for v in face.mimicSkeleton.positions],'rotations':[quat(v) for v in face.mimicSkeleton.rotations],'poses':[]}
for pose in face.mimicPoses:
 data['poses'].append({'name':pose.name,'bones':[{'name':b.BoneName,'position':vec(b.positionFrames[0]),'rotation':quat(b.rotationFramesQuat[0]),'scale':vec(b.scaleFrames[0])} for b in pose.animBuffer.bones]})
(out/'source-face.json').write_text(json.dumps(data,indent=2))
print('SOURCE FACE',len(data['bones']),'bones',len(data['poses']),'poses',flush=True)
print([p['name'] for p in data['poses']],flush=True)
bpy.ops.wm.open_mainfile(filepath='F:/Unity/ExtractedModels/Yennefer/Yennefer-dlc-dress.blend')
rigs=[]
for obj in bpy.data.objects:
 if obj.type!='ARMATURE':continue
 rigs.append({'name':obj.name,'matrix':[list(r) for r in obj.matrix_world],'bones':[{'name':b.name,'parent':b.parent.name if b.parent else None,'matrix':[list(r) for r in b.matrix_local]} for b in obj.data.bones]})
(out/'source-rigs.json').write_text(json.dumps(rigs,indent=2))
print('SOURCE RIGS',[(r['name'],len(r['bones'])) for r in rigs])
