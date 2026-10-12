import bpy,json,numpy as np,sys
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'Artifacts/AnimationSkillStudy/Blender';OUT.mkdir(parents=True,exist_ok=True)
data=json.loads(Path('F:/Unity/Project/StartMotionValidation/Artifacts/BlenderOptimization/Exchange/YenneferDLC.json').read_text(encoding='utf-8'))
fitted='--fitted' in sys.argv
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
def material(name,color):
    m=bpy.data.materials.new(name);m.use_nodes=True
    shader=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');shader.inputs['Base Color'].default_value=color;shader.inputs['Roughness'].default_value=.7;return m
for surface in data['surfaces']:
    if surface['role'] not in ['shoe','hosiery'] or not ('Left' in surface['name'] or '左' in surface['name']):continue
    p=np.asarray(surface['vertices']).reshape(-1,3)
    if fitted and surface['role']=='hosiery':
        shape=json.loads((ROOT/'Assets/Homecoming/Resources/Homecoming/Poses/FootLast_ContinuousLeftHosiery.json').read_text(encoding='utf-8'))
        p=p+np.asarray(shape['delta']).reshape(-1,3)
    m=np.asarray(surface['matrix']).reshape(4,4);p=(np.column_stack((p,np.ones(len(p))))@m.T)[:,:3]
    faces=np.asarray(surface['triangles']).reshape(-1,3);faces=faces[(p[faces,:,][...,1].mean(1)<.23)]
    mesh=bpy.data.meshes.new(surface['role']);mesh.from_pydata(p[:,[0,2,1]].tolist(),[],faces[:,[0,2,1]].tolist());mesh.update()
    obj=bpy.data.objects.new(surface['role'],mesh);bpy.context.scene.collection.objects.link(obj);obj.data.materials.append(material(surface['role'],(.04,.12,.20,1) if surface['role']=='shoe' else (.85,.36,.15,1)))
    print('REST FOOT',surface['role'],'bounds',p[p[:,1]<.23].min(0).tolist(),p[p[:,1]<.23].max(0).tolist())
scene=bpy.context.scene
try:scene.render.engine='BLENDER_EEVEE'
except TypeError:pass
scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100
bpy.ops.object.camera_add(location=(.75,-.55,.27));camera=bpy.context.object;scene.camera=camera
camera.rotation_euler=(Vector((.10,-.04,.10))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=65
bpy.ops.object.light_add(type='AREA',location=(0,-1,1));bpy.context.object.data.energy=250;bpy.context.object.data.size=2
scene.world.color=(.2,.2,.2);scene.render.filepath=str(OUT/('fitted-foot-vs-boot-rest.png' if fitted else 'original-foot-vs-boot-rest.png'));bpy.ops.render.render(write_still=True)
