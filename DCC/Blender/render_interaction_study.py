"""Render authored pose/shape study from the separate Blender workspace."""
import bpy
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Artifacts/AnimationSkillStudy/Blender';OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene
try:scene.render.engine='BLENDER_EEVEE'
except TypeError:pass
scene.render.resolution_x=768;scene.render.resolution_y=768;scene.render.resolution_percentage=100
bpy.ops.object.camera_add(location=(.2,-3.4,1.0));camera=bpy.context.object;scene.camera=camera
camera.rotation_euler=(Vector((0,0,.90))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=52
for location,energy,size in [((1,-2,3),650,3),((-2,-1,2),420,3),((0,2,2.5),550,2)]:
    bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object;light.data.energy=energy;light.data.shape='DISK';light.data.size=size;light.rotation_euler=(Vector((0,0,1))-light.location).to_track_quat('-Z','Y').to_euler()
scene.world.color=(.15,.15,.15)
for actor in ['YenneferDLC','Triss']:
    for collection in bpy.data.collections:
        if collection.name.startswith('Unity_'):collection.hide_render=collection.name!='Unity_'+actor
    for obj in bpy.data.objects:
        if obj.type!='MESH':continue
        if obj.name.startswith('SoftBoot_'):obj.hide_render=True
        if obj.get('unity_character')==actor:obj.hide_render=obj.get('unity_role') in ['shoe','leg','dressed_leg','dressed_lace']
    scene.render.filepath=str(OUT/(actor+'-authored-standing.png'));bpy.ops.render.render(write_still=True)
for collection in bpy.data.collections:
    if collection.name.startswith('Unity_'):collection.hide_render=True
for obj in bpy.data.objects:
    if obj.name.startswith('SoftBoot_'):obj.hide_render=False
camera.location=(.85,-1.45,.6);camera.rotation_euler=(Vector((.6,0,.28))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=48
scene.render.filepath=str(OUT/'soft-boot-key-shape.png');bpy.ops.render.render(write_still=True)
