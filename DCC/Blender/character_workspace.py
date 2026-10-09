"""Import the exact Unity surfaces and deform rig, retaining vertex identities.

Run inside Blender. Unity coordinates (X,Y,Z) map to Blender (X,Z,Y).
The exchange includes real Unity action poses, not an invented replacement rig.
"""
import json
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix, Vector

PROJECT = Path(r'F:\Unity\Project\Start')
EXCHANGE = Path(r'F:\Unity\Project\StartMotionValidation\Artifacts\BlenderOptimization\Exchange')
C = Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))

def matrix(values):
    return Matrix(np.asarray(values).reshape(4,4).tolist())

def texture_path(path):
    marker='Assets/'
    normalized=path.replace('\\','/')
    if marker in normalized:
        candidate=PROJECT/normalized[normalized.index(marker):]
        if candidate.is_file():return str(candidate)
    return path

def sheer_material(material,item):
    nodes=material.node_tree.nodes;links=material.node_tree.links
    shader=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    shader.inputs['Roughness'].default_value=.73
    skin=nodes.new('ShaderNodeTexImage');skin.image=bpy.data.images.load(texture_path(item['skinTexture']),check_existing=True)
    uv=nodes.new('ShaderNodeUVMap');uv.uv_map='SkinUV' if item.get('skinUV2',0)>.5 else 'OriginalUV';links.new(uv.outputs['UV'],skin.inputs['Vector'])
    geometry=nodes.new('ShaderNodeNewGeometry');dot=nodes.new('ShaderNodeVectorMath');dot.operation='DOT_PRODUCT'
    links.new(geometry.outputs['Normal'],dot.inputs[0]);links.new(geometry.outputs['Incoming'],dot.inputs[1])
    def math_node(operation,a=None,b=None):
        n=nodes.new('ShaderNodeMath');n.operation=operation
        for i,v in enumerate((a,b)):
            if v is None:continue
            if isinstance(v,(int,float)):n.inputs[i].default_value=v
            else:links.new(v,n.inputs[i])
        return n.outputs[0]
    cosine=math_node('MAXIMUM',math_node('ABSOLUTE',dot.outputs['Value']),item.get('grazing',.32) or .32)
    exponent=math_node('DIVIDE',-(item.get('density',.07) or .07)*item.get('denier',20),cosine)
    coverage=math_node('SUBTRACT',1,math_node('POWER',2,exponent))
    fabric=nodes.new('ShaderNodeRGB');fabric.outputs[0].default_value=item['color']
    if item.get('lace',0)>.5:
        tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(texture_path(item['texture']),check_existing=True)
        uv_lace=nodes.new('ShaderNodeUVMap');uv_lace.uv_map='OriginalUV';links.new(uv_lace.outputs['UV'],tex.inputs['Vector'])
        repeat=item.get('laceBodyRepeat',0)
        if repeat>0:
            separate=nodes.new('ShaderNodeSeparateXYZ');links.new(uv_lace.outputs['UV'],separate.inputs[0])
            mirrored=math_node('MULTIPLY',repeat,math_node('SUBTRACT',1,math_node('ABSOLUTE',math_node('SUBTRACT',math_node('MULTIPLY',2,math_node('FRACT',math_node('DIVIDE',separate.outputs['Y'],2*repeat))),1))))
            combined=nodes.new('ShaderNodeCombineXYZ');links.new(separate.outputs['X'],combined.inputs['X']);links.new(mirrored,combined.inputs['Y']);links.new(combined.outputs[0],tex.inputs['Vector'])
        coverage=tex.outputs['Alpha']
        dye=nodes.new('ShaderNodeMixRGB');dye.blend_type='MULTIPLY';dye.inputs[0].default_value=1
        links.new(fabric.outputs[0],dye.inputs[1]);links.new(tex.outputs['Color'],dye.inputs[2]);fabric_output=dye.outputs[0]
    else:fabric_output=fabric.outputs[0]
    if item.get('stockingBoundary',0)>.5:
        boundary=nodes.new('ShaderNodeAttribute');boundary.attribute_name='StockingBoundary'
        region=math_node('MINIMUM',1,math_node('MAXIMUM',0,math_node('DIVIDE',boundary.outputs['Fac'],.003)))
        coverage=math_node('MULTIPLY',coverage,region)
    mixed=nodes.new('ShaderNodeMixRGB');mixed.blend_type='MIX'
    links.new(coverage,mixed.inputs[0]);links.new(skin.outputs['Color'],mixed.inputs[1]);links.new(fabric_output,mixed.inputs[2]);links.new(mixed.outputs[0],shader.inputs['Base Color'])

def import_character(name):
    package = json.loads((EXCHANGE / (name+'.json')).read_text(encoding='utf-8'))
    collection = bpy.data.collections.new('Unity_'+name)
    bpy.context.scene.collection.children.link(collection)
    data = bpy.data.armatures.new(name+'_OriginalRig')
    armature = bpy.data.objects.new(name+'_Rig',data)
    collection.objects.link(armature)
    bpy.context.view_layer.objects.active=armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in package['bones']:
        edit=data.edit_bones.new(bone['name'])
        rest=C @ matrix(bone['matrix']) @ C
        edit.head=rest.translation
        edit.tail=edit.head+Vector((0,.04,0))
        edit.matrix=rest
        edit.length=.04
    for i,bone in enumerate(package['bones']):
        if bone['parent']>=0:
            data.edit_bones[bone['name']].parent=data.edit_bones[package['bones'][bone['parent']]['name']]
    bpy.ops.object.mode_set(mode='OBJECT')
    armature.show_in_front=True
    armature['unity_character']=name
    material_cache={}
    objects={}
    for surface in package['surfaces']:
        positions=np.asarray(surface['vertices']).reshape(-1,3)
        transform=matrix(surface['matrix'])
        to_blender=np.array(C @ transform)
        converted=(np.column_stack((positions,np.ones(len(positions)))) @ to_blender.T)[:,:3]
        triangles=np.asarray(surface['triangles']).reshape(-1,3)[:,[0,2,1]]
        mesh=bpy.data.meshes.new(surface['name'])
        mesh.from_pydata(converted.tolist(),[],triangles.tolist())
        mesh.update()
        obj=bpy.data.objects.new(surface['name'],mesh)
        collection.objects.link(obj)
        objects[surface['name']]=obj
        obj['unity_mesh_path']=surface['path']
        obj['unity_role']=surface['role']
        obj['unity_character']=name
        obj['unity_enabled']=surface['enabled']
        switchable=package['character']=='YenneferDLC' and surface['role'] in ['hosiery','retained_lace','dressed_leg','dressed_lace']
        obj.hide_set(False if switchable else not surface['enabled'])
        obj.hide_render=not surface['enabled']
        identity=mesh.attributes.new('unity_vertex_id','INT','POINT')
        identity.data.foreach_set('value',np.arange(len(positions),dtype=np.int32))
        if len(surface['uv'])==len(positions)*2:
            uv=np.asarray(surface['uv']).reshape(-1,2)
            layer=mesh.uv_layers.new(name='OriginalUV')
            indices=np.empty(len(mesh.loops),dtype=np.int32)
            mesh.loops.foreach_get('vertex_index',indices)
            layer.data.foreach_set('uv',uv[indices].astype(np.float32).flatten())
        if len(surface.get('uv2',[]))==len(positions)*2:
            skin_uv=np.asarray(surface['uv2']).reshape(-1,2)
            layer=mesh.uv_layers.new(name='SkinUV')
            indices=np.empty(len(mesh.loops),dtype=np.int32);mesh.loops.foreach_get('vertex_index',indices)
            layer.data.foreach_set('uv',skin_uv[indices].astype(np.float32).flatten())
        if len(surface.get('uv3',[]))==len(positions)*2:
            boundary=np.asarray(surface['uv3']).reshape(-1,2)
            attribute=mesh.attributes.new('StockingBoundary','FLOAT','POINT')
            attribute.data.foreach_set('value',(boundary[:,1]-boundary[:,0]).astype(np.float32))
        boot_boundary=surface.get('uv4',[])
        masked_boot=name=='YenneferDLC' and len(boot_boundary)==len(positions)*2
        if masked_boot:
            boundary=np.asarray(boot_boundary).reshape(-1,2)
            attribute=mesh.attributes.new('OriginalBootBoundary','FLOAT','POINT')
            attribute.data.foreach_set('value',(boundary[:,1]-boundary[:,0]).astype(np.float32))
        for item in surface['materials']:
            key=(item['name'],item['texture'],surface['name'] if masked_boot else '')
            if key not in material_cache:
                material=bpy.data.materials.new(item['name'])
                material.use_nodes=True
                shader=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
                shader.inputs['Base Color'].default_value=item['color']
                shader.inputs['Roughness'].default_value=.65
                if item['texture'] and Path(item['texture']).is_file():
                    tex=material.node_tree.nodes.new('ShaderNodeTexImage')
                    tex.image=bpy.data.images.load(texture_path(item['texture']),check_existing=True)
                    material.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color'])
                    if surface['role']=='dressed_lace' or any(word in item['name'].lower() for word in ('hair','lash')):
                        material.node_tree.links.new(tex.outputs['Alpha'],shader.inputs['Alpha'])
                if item.get('sheer',0)>.5 and item.get('skinTexture'):sheer_material(material,item)
                if item.get('normal') and Path(texture_path(item['normal'])).is_file():
                    normal_tex=material.node_tree.nodes.new('ShaderNodeTexImage');normal_tex.image=bpy.data.images.load(texture_path(item['normal']),check_existing=True);normal_tex.image.colorspace_settings.name='Non-Color'
                    normal_map=material.node_tree.nodes.new('ShaderNodeNormalMap');normal_map.inputs['Strength'].default_value=item.get('bumpScale',1);material.node_tree.links.new(normal_tex.outputs['Color'],normal_map.inputs['Color']);material.node_tree.links.new(normal_map.outputs['Normal'],shader.inputs['Normal'])
                if item.get('specular') and item.get('workflow',1)<.5 and Path(texture_path(item['specular'])).is_file():
                    spec_tex=material.node_tree.nodes.new('ShaderNodeTexImage');spec_tex.image=bpy.data.images.load(texture_path(item['specular']),check_existing=True)
                    rough=material.node_tree.nodes.new('ShaderNodeMath');rough.operation='SUBTRACT';rough.inputs[0].default_value=1;material.node_tree.links.new(spec_tex.outputs['Alpha'],rough.inputs[1]);material.node_tree.links.new(rough.outputs[0],shader.inputs['Roughness'])
                    if 'Specular Tint' in shader.inputs:material.node_tree.links.new(spec_tex.outputs['Color'],shader.inputs['Specular Tint'])
                if item.get('uvScale',[1,1])!=[1,1]:
                    uv_node=material.node_tree.nodes.new('ShaderNodeUVMap');uv_node.uv_map='OriginalUV';tiling=material.node_tree.nodes.new('ShaderNodeVectorMath');tiling.operation='MULTIPLY';tiling.inputs[1].default_value=(*item['uvScale'],1);material.node_tree.links.new(uv_node.outputs['UV'],tiling.inputs[0])
                    for tex_node in material.node_tree.nodes:
                        if tex_node.type=='TEX_IMAGE':material.node_tree.links.new(tiling.outputs[0],tex_node.inputs['Vector'])
                if masked_boot:
                    material.surface_render_method='DITHERED'
                    # Per-side masks preserve the complete sock/foot mesh while
                    # suppressing the portion enclosed by the original boot.
                    nodes=material.node_tree.nodes;links=material.node_tree.links
                    boundary=nodes.new('ShaderNodeAttribute');boundary.attribute_name='OriginalBootBoundary'
                    inside=nodes.new('ShaderNodeMath');inside.operation='GREATER_THAN';inside.inputs[1].default_value=0;links.new(boundary.outputs['Fac'],inside.inputs[0])
                    worn=nodes.new('ShaderNodeValue');worn.label='Original boot worn'
                    side='Left' if 'Left' in surface['name'] or '左' in surface['name'] else 'Right'
                    detach=390 if side=='Left' else 570
                    for frame,value in [(0,1),(detach-1,1),(detach,0),(1170,0)]:
                        worn.outputs[0].default_value=value;worn.outputs[0].keyframe_insert('default_value',frame=frame)
                    multiply=nodes.new('ShaderNodeMath');multiply.operation='MULTIPLY';links.new(inside.outputs[0],multiply.inputs[0]);links.new(worn.outputs[0],multiply.inputs[1])
                    alpha=nodes.new('ShaderNodeMath');alpha.operation='SUBTRACT';alpha.inputs[0].default_value=1;links.new(multiply.outputs[0],alpha.inputs[1])
                    if surface['role']=='dressed_lace':
                        cutout=nodes.new('ShaderNodeMath');cutout.operation='GREATER_THAN';cutout.inputs[1].default_value=.18;links.new(tex.outputs['Alpha'],cutout.inputs[0])
                        combined=nodes.new('ShaderNodeMath');combined.operation='MULTIPLY';links.new(alpha.outputs[0],combined.inputs[0]);links.new(cutout.outputs[0],combined.inputs[1]);links.new(combined.outputs[0],shader.inputs['Alpha'])
                    else:links.new(alpha.outputs[0],shader.inputs['Alpha'])
                material_cache[key]=material
            mesh.materials.append(material_cache[key])
        offset=0
        for index,count in enumerate(surface['submeshSizes']):
            for polygon in mesh.polygons[offset:offset+count//3]:polygon.material_index=index
            offset+=count//3
        for polygon in mesh.polygons:polygon.use_smooth=True
        normals=np.asarray(surface['normals']).reshape(-1,3)
        normal_matrix=np.linalg.inv(to_blender[:3,:3]).T
        normals=normals @ normal_matrix.T
        normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-12)
        mesh.normals_split_custom_set_from_vertices(normals.tolist())
        joints=surface['joints']
        groups=[obj.vertex_groups.new(name=package['bones'][joint]['name']) for joint in joints]
        indices=np.asarray(surface['boneIndices']).reshape(-1,4)
        weights=np.asarray(surface['weights']).reshape(-1,4)
        for v in range(len(positions)):
            for slot in range(4):
                if weights[v,slot]>0:groups[indices[v,slot]].add([v],float(weights[v,slot]),'REPLACE')
        modifier=obj.modifiers.new('OriginalGameSkin','ARMATURE')
        modifier.object=armature
        modifier.use_deform_preserve_volume=False
        if surface['shapes']:
            obj.shape_key_add(name='Basis')
            for shape in surface['shapes']:
                key=obj.shape_key_add(name=shape['name'])
                delta=np.asarray(shape['delta']).reshape(-1,3) @ to_blender[:3,:3].T
                key.data.foreach_set('co',(converted+delta).astype(np.float32).flatten())
    for pose in package['poses']:
        values=np.asarray(pose['bones']).reshape(-1,16)
        desired=[]
        for i,bone in enumerate(package['bones']):
            rest=C @ matrix(bone['matrix']) @ C
            posed=C @ matrix(values[i]) @ C
            desired.append(posed @ rest.inverted() @ data.bones[bone['name']].matrix_local)
        # Assign local bases explicitly; setting global matrices against stale
        # parent evaluation produces incorrect child transforms during import.
        for i,bone in enumerate(package['bones']):
            child_rest=data.bones[bone['name']].matrix_local
            parent=bone['parent']
            basis=child_rest.inverted() @ desired[i]
            if parent>=0:
                parent_rest=data.bones[package['bones'][parent]['name']].matrix_local
                basis=child_rest.inverted() @ parent_rest @ desired[parent].inverted() @ desired[i]
            armature.pose.bones[bone['name']].matrix_basis=basis
        bpy.context.view_layer.update()
        for bone in armature.pose.bones:
            bone.keyframe_insert('location',frame=pose['frame'])
            bone.keyframe_insert('rotation_quaternion',frame=pose['frame'])
            bone.keyframe_insert('scale',frame=pose['frame'])
        offset=0
        for surface in package['surfaces']:
            obj=objects[surface['name']]
            for shape in surface['shapes']:
                key=obj.data.shape_keys.key_blocks[shape['name']]
                key.value=pose['shapes'][offset]
                key.keyframe_insert('value',frame=pose['frame'])
                offset+=1
    scene=bpy.context.scene
    scene.frame_start=0
    scene.frame_end=max(p['frame'] for p in package['poses'])
    scene.render.fps=30
    scene.frame_set(90)
    armature.hide_set(True)
    for obj in bpy.context.selected_objects:obj.select_set(False)
    for obj in collection.objects:
        if obj.type=='MESH' and not obj.hide_get():obj.select_set(True)
    print(json.dumps({'character':name,'surfaces':len(package['surfaces']),
                      'vertices':sum(len(s['vertices'])//3 for s in package['surfaces']),
                      'bones':len(package['bones']),'pose_frames':[p['frame'] for p in package['poses']]}))
    return package,collection
