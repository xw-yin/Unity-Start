"""Editable Triss gather correctives, preserving the exact imported topology.

This is a pose sculpture, not a cloth simulation. The earlier lift put the
bottom edge above the grasp and collapsed several panels into a pointed fan.
"""
import json, sys
from pathlib import Path
import bpy
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
from character_workspace import import_character, EXCHANGE
for obj in list(bpy.data.objects): bpy.data.objects.remove(obj,do_unlink=True)
import_character('Triss')
package=json.loads((EXCHANGE/'Triss.json').read_text(encoding='utf-8-sig'))
surface=next(s for s in package['surfaces'] if s['role']=='garment')
v=np.asarray(surface['vertices']).reshape(-1,3)
shapes={s['name']:np.asarray(s['delta']).reshape(-1,3) for s in surface['shapes']}
seated=v+shapes['Sitting']
def smooth(a,b,x):
    u=np.clip((x-a)/(b-a),0,1);return u*u*(3-2*u)
obj=bpy.data.objects[surface['name']]
transform=np.asarray(surface['matrix']).reshape(4,4)
obj.data.shape_keys.animation_data_clear()
result=[]
for side,name in enumerate(['BootLeft','BootRight']):
    front=smooth(-.22,.02,v[:,2])
    lateral=smooth(.20 if side==0 else -.20,-.12 if side==0 else .12,v[:,0])
    free=smooth(.94,.82,v[:,1])
    length=np.clip((.94-v[:,1])/.94,0,1)
    hem=length**2
    fold=np.sin(np.pi*length)*free
    grip_x=-.06 if side==0 else .06
    # Retain a broad, hanging panel below the grasp; no periodic creases or
    # convergence of every lower vertex onto the same narrow spike.
    delta=np.column_stack(((grip_x-v[:,0])*.38*hem,
                           .10*fold+.48*hem,
                           .015*fold-.27*hem))*(front*lateral)[:,None]
    target=seated+delta
    # A hem cannot be pulled higher than the hand-supported fold. Taper the
    # correction smoothly rather than imposing a hard horizontal clipping plane.
    excess=np.maximum(0,target[:,1]-.91)
    delta[:,1]-=excess*smooth(.80,.94,target[:,1])*front*lateral*free
    key=obj.data.shape_keys.key_blocks[name]
    # Unity Y-up -> Blender Z-up; keep the original IDs, triangles and UVs.
    unity_world=(np.column_stack((v+delta,np.ones(len(v))))@transform.T)[:,:3]
    key.data.foreach_set('co',unity_world[:,[0,2,1]].astype(np.float32).ravel())
    result.append({'name':name,'delta':delta.astype(np.float32).ravel().tolist()})
    print(name,'hem maximum',float(target[v[:,1]<.15,1].max()),'max offset',float(np.linalg.norm(delta,axis=1).max()))
obj.data.shape_keys.key_blocks['Sitting'].value=1
obj.data.shape_keys.key_blocks['BootLeft'].value=1
obj.data.shape_keys.key_blocks['BootRight'].value=0
obj['gather_method']='Broad hand-supported fold; hanging hem retained. No cloth simulation claim.'
for frame,sit,left,right in [(0,0,0,0),(270,1,0,0),(285,1,0,0),(300,1,1,0),
                              (390,1,1,0),(420,1,0,0),(480,1,0,1),
                              (570,1,0,1),(600,1,0,0),(780,0,0,0)]:
    for name,value in [('Sitting',sit),('BootLeft',left),('BootRight',right)]:
        key=obj.data.shape_keys.key_blocks[name];key.value=value;key.keyframe_insert('value',frame=frame)
action=obj.data.shape_keys.animation_data.action
for layer in action.layers:
    for strip in layer.strips:
        for bag in strip.channelbags:
            for curve in bag.fcurves:
                for point in curve.keyframe_points:point.interpolation='CONSTANT'
out=ROOT/'Assets/Homecoming/Resources/Homecoming/Poses/TrissDress.json'
out.write_text(json.dumps({'character':'Triss','mesh':surface['name'],'vertexCount':len(v),
                           'baseVertices':v.astype(np.float32).ravel().tolist(),'shapes':result}),encoding='utf-8')
bpy.context.scene.frame_set(390)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'DCC/Blender/TrissGather.blend'))
