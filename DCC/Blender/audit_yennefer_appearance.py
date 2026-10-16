import bpy,json
from pathlib import Path
bpy.ops.wm.open_mainfile(filepath='F:/Unity/ExtractedModels/Yennefer/Yennefer-dlc-dress.blend')
def plain(v):
 if hasattr(v,'items'):return {k:plain(x) for k,x in v.items()}
 if isinstance(v,(str,int,float,bool)) or v is None:return v
 try:return [plain(x) for x in v]
 except:return str(v)
result=[]
for m in bpy.data.materials:
 if not any(t in m.name for t in ['h_01_wa__yennefer','he_01_wa__yennefer','c_01_wa__yennefer']):continue
 r={'name':m.name,'properties':{k:plain(v) for k,v in m.items()},'nodes':[]}
 if m.use_nodes:
  for n in m.node_tree.nodes:
   d={'type':n.type,'label':n.label,'name':n.name}
   if n.type=='TEX_IMAGE' and n.image:d['image']=bpy.path.abspath(n.image.filepath)
   if n.type=='GROUP':d['group']=n.node_tree.name
   d['inputs']={s.name:str(s.default_value[:]) if hasattr(getattr(s,'default_value',None),'__len__') else str(getattr(s,'default_value','')) for s in n.inputs if not s.is_linked}
   r['nodes'].append(d)
 result.append(r)
out=Path('F:/Unity/Project/Start/Artifacts/AppearanceReview-20261017/source-materials.json');out.write_text(json.dumps(result,indent=2),encoding='utf-8')
print('SOURCE APPEARANCE MATERIALS',len(result))

group=bpy.data.node_groups.get('Witcher3_Skin')
if group:
 edges=[]
 for link in group.links:
  if any(k in (link.from_node.name+' '+link.to_node.name+' '+link.from_node.label+' '+link.to_node.label+' '+link.to_socket.name) for k in ['Ambient','Separate','Roughness','Subsurface']):
   edges.append([link.from_node.name,link.from_socket.name,link.to_node.name,link.to_socket.name])
 Path('F:/Unity/Project/Start/Artifacts/AppearanceReview-20261017/skin-group-links.json').write_text(json.dumps(edges,indent=2))
