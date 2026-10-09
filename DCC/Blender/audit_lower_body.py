"""Inspect welded topology on disposable copies of original lower-body meshes."""
import json
from pathlib import Path
import bpy
import bmesh

PROJECT=Path(r'F:\Unity\Project\Start')

def audit_lower_body():
    report=[]
    for obj in bpy.data.objects:
        if obj.type!='MESH' or not obj.get('unity_mesh_path'):continue
        role=obj.get('unity_role','')
        if role not in ('leg','hosiery','shoe') and not obj.name.startswith('l_02_wa__yennefer_lod0_21_Shoe'):continue
        mesh=obj.data
        bm=bmesh.new();bm.from_mesh(mesh)
        original=len(bm.verts)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
        boundaries=[e for e in bm.edges if e.is_boundary]
        foot_edges=[e for e in boundaries if max(v.co.z for v in e.verts)<.15]
        foot_faces=[f for f in bm.faces if min(v.co.z for v in f.verts)<.15]
        min_height=min(v.co.z for v in bm.verts)
        max_height=max(v.co.z for v in bm.verts)
        item={'character':obj['unity_character'],'surface':obj.name,'path':obj['unity_mesh_path'],
              'role':role,'original_vertices':original,'welded_vertices':len(bm.verts),
              'boundary_edges':len(boundaries),'foot_boundary_edges_below_15cm':len(foot_edges),
              'foot_faces':len(foot_faces),'min_height':float(min_height),'max_height':float(max_height),
              'nonmanifold_nonboundary_edges':sum(not e.is_manifold and not e.is_boundary for e in bm.edges),
              'materials':[m.name for m in mesh.materials]}
        if foot_edges:
            item['foot_boundary_positions']=[[(float(v.co.x),float(v.co.y),float(v.co.z)) for v in e.verts] for e in foot_edges[:24]]
        report.append(item)
        bm.free()
    destination=PROJECT/'Artifacts/CuteMotion/Blender'
    destination.mkdir(parents=True,exist_ok=True)
    (destination/'lower-body-topology.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps([{k:v for k,v in item.items() if k not in ('foot_boundary_positions','materials','path')} for item in report],ensure_ascii=False))
    return report
