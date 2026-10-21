import bpy,json,argparse,sys
from pathlib import Path
parser=argparse.ArgumentParser()
parser.add_argument('--all',action='store_true',help='Include costume materials and their source shader links')
parser.add_argument('--output',type=Path,default=Path('F:/Unity/Project/Start/Artifacts/AppearanceReview-20261017'))
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
args.output.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath='F:/Unity/ExtractedModels/Yennefer/Yennefer-dlc-dress.blend')
def plain(v):
 if hasattr(v,'items'):return {k:plain(x) for k,x in v.items()}
 if isinstance(v,(str,int,float,bool)) or v is None:return v
 try:return [plain(x) for x in v]
 except:return str(v)
result=[]
for m in bpy.data.materials:
 if not args.all and not any(t in m.name for t in ['h_01_wa__yennefer','he_01_wa__yennefer','c_01_wa__yennefer']):continue
 r={'name':m.name,'properties':{k:plain(v) for k,v in m.items()},'nodes':[]}
 if m.use_nodes:
  for n in m.node_tree.nodes:
   d={'type':n.type,'label':n.label,'name':n.name}
   if n.type=='TEX_IMAGE' and n.image:d['image']=bpy.path.abspath(n.image.filepath)
   if n.type=='GROUP':d['group']=n.node_tree.name
   d['inputs']={s.name:str(s.default_value[:]) if hasattr(getattr(s,'default_value',None),'__len__') else str(getattr(s,'default_value','')) for s in n.inputs if not s.is_linked}
   r['nodes'].append(d)
 result.append(r)
out=args.output/'source-materials.json';out.write_text(json.dumps(result,indent=2),encoding='utf-8')
print('SOURCE APPEARANCE MATERIALS',len(result))
if args.all:
 graphs=[]
 for group in bpy.data.node_groups:
  if 'witcher' not in group.name.lower():continue
  graphs.append({'name':group.name,'nodes':[{'name':n.name,'label':n.label,'type':n.type} for n in group.nodes],'links':[[l.from_node.name,l.from_socket.name,l.to_node.name,l.to_socket.name] for l in group.links]})
 (args.output/'shader-graphs.json').write_text(json.dumps(graphs,indent=2),encoding='utf-8')
 attributes=[]
 for obj in bpy.data.objects:
  if obj.type!='MESH':continue
  rows=[]
  for attr in obj.data.color_attributes:
   colors=[tuple(d.color) for d in attr.data]
   if colors:rows.append({'name':attr.name,'domain':attr.domain,'count':len(colors),'first':colors[0],'min':[min(c[i] for c in colors) for i in range(4)],'max':[max(c[i] for c in colors) for i in range(4)]})
  attributes.append({'name':obj.name,'vertices':len(obj.data.vertices),'colors':rows})
 (args.output/'mesh-color-attributes.json').write_text(json.dumps(attributes,indent=2),encoding='utf-8')

group=bpy.data.node_groups.get('Witcher3_Skin')
if group:
 edges=[]
 for link in group.links:
  if any(k in (link.from_node.name+' '+link.to_node.name+' '+link.from_node.label+' '+link.to_node.label+' '+link.to_socket.name) for k in ['Ambient','Separate','Roughness','Subsurface']):
   edges.append([link.from_node.name,link.from_socket.name,link.to_node.name,link.to_socket.name])
 (args.output/'skin-group-links.json').write_text(json.dumps(edges,indent=2))
