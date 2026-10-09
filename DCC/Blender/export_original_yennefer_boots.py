import bpy,json,struct,pathlib,math
from mathutils import Vector,Matrix
bpy.ops.wm.open_mainfile(filepath='F:/Unity/ExtractedModels/Yennefer/Yennefer-dlc-dress.blend')
for o in bpy.data.objects:
 o.hide_viewport=False;o.hide_render=False;o.hide_set(False)
for c in bpy.data.collections:c.hide_viewport=False;c.hide_render=False
def reveal(c):
 c.exclude=False;c.hide_viewport=False
 for child in c.children:reveal(child)
reveal(bpy.context.view_layer.layer_collection)
bpy.context.view_layer.update()
OUT=pathlib.Path('F:/Unity/Project/Start/Assets/ThirdParty/WitcherYenneferDLC/OriginalBoots');OUT.mkdir(parents=True,exist_ok=True)
a=bpy.data.objects['yennefer_dlc:CMovingPhysicalAgentComponent3_ARM']; scale=.92
# Rotate each arm chain from the source A pose into a horizontal bind pose.
for side,sign in [('l',-1),('r',1)]:
 for bn,child in [('bicep','forearm'),('forearm','hand')]:
  bpy.context.view_layer.update();b=a.pose.bones[side+'_'+bn];c=a.pose.bones[side+'_'+child]
  q=(c.head-b.head).rotation_difference(Vector((sign,0,0)));m=b.matrix.copy();m=Matrix.Translation(b.head)@q.to_matrix().to_4x4()@Matrix.Translation(-b.head)@m;b.matrix=m
bpy.context.view_layer.update()
mapnames={'pelvis':('C_Hips','hips'),'torso':('C_Spine','spine'),'torso2':('C_Chest','chest'),'torso3':('C_UpperChest','upperChest'),'neck':('C_Neck','neck'),'head':('C_Head','head')}
for side,label,h in [('l','L','left'),('r','R','right')]:
 for src,dest,human in [('shoulder','Shoulder','Shoulder'),('bicep','UpperArm','UpperArm'),('forearm','LowerArm','LowerArm'),('hand','Hand','Hand'),('thigh','UpperLeg','UpperLeg'),('shin','LowerLeg','LowerLeg'),('foot','Foot','Foot'),('toe','ToeBase','Toes')]:mapnames[side+'_'+src]=(label+'_'+dest,h+human)
 for finger,human in [('index','Index'),('middle','Middle'),('ring','Ring'),('pinky','Little'),('thumb','Thumb')]:
  for i,ph in enumerate(['Proximal','Intermediate','Distal'],1):mapnames[f'{side}_{finger}{i}']=(label+'_'+human+str(i),h+human+ph)
def cv(v):return Vector((v.x,v.z,v.y))*scale
bones={b.name:(a,b) for b in a.pose.bones}
objects=[o for o in bpy.data.objects if o.type=='MESH' and 'shadowmesh' not in o.name and not any(c.name=='_WitcherRedclothCache' for c in o.users_collection) and '_lod0' in o.name and 'GMesh' not in o.name]
for o in objects:
 for mod in o.modifiers:
  if mod.type=='NODES':mod.show_viewport=False;mod.show_render=False
  if mod.type=='ARMATURE' and mod.object:
   for b in mod.object.pose.bones:
    if b.name not in bones:bones[b.name]=(mod.object,b)
bpy.context.view_layer.update()
keys=list(bones);idx={k:i+1 for i,k in enumerate(keys)};positions={k:cv(o.matrix_world@b.head) for k,(o,b) in bones.items()}
nodes=[dict(name='YenneferSkeleton',parent=-1,position=[0,0,0],rotation=[0,0,0,1],scale=[1,1,1])];hum=[]
for k,(o,b) in bones.items():
 p=b.parent.name if b.parent else None
 if o!=a and (p not in idx or p=='Root'):
  p='torso3' if ('boob' in k.lower() or 'breast' in k.lower()) else ('pelvis' if 'feather' in k.lower() else ('neck' if ('pendant' in k or 'scarf' in k or k=='thyroid') else 'head'))
 elif p not in idx and k not in ['Root']:p='pelvis'
 pos=positions[k]-(positions[p] if p in idx else Vector())
 nodes.append(dict(name='J_Bip_'+mapnames[k][0] if k in mapnames else 'W3_'+k,parent=idx.get(p,0),position=list(pos),rotation=[0,0,0,1],scale=[1,1,1]))
 if k in mapnames:hum.append(dict(bone=mapnames[k][1],node=idx[k]))
materials=[];matindex={};textures=[]
def surf(m):
 if m.name in matindex:return matindex[m.name]
 tex={n.label or n.name:bpy.path.abspath(n.image.filepath) for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image} if m.use_nodes else {}
 def ti(k):
  path=tex.get(k,tex.get(k+'map',''))
  if not path:return -1
  if path not in textures:textures.append(path)
  return textures.index(path)
 alpha='MASK' if any(s in m.name.lower() for s in ['c_01','fur','feathers','lash','he_01_wa__yennefer_material4']) else 'OPAQUE'
 color=[.28,.24,.28,1] if 'c_01' in m.name else ([.12,.12,.13,1] if 'fur_01' in m.name else [1,1,1,1])
 matindex[m.name]=len(materials);materials.append(dict(name=m.name,alpha=alpha,texture=ti('Diffuse'),normal=ti('Normal'),color=color,cutoff=.25,doubleSided=True));return len(materials)-1

o=bpy.data.objects['l_02_wa__yennefer_lod0'];me=o.data;me.calc_loop_triangles()
arm=next(m.object for m in o.modifiers if m.type=='ARMATURE' and m.object)
deform={g.index:(bones[g.name][0].matrix_world@bones[g.name][1].matrix)@(arm.matrix_world@arm.data.bones[g.name].matrix_local).inverted() for g in o.vertex_groups if g.name in bones and g.name in arm.data.bones}
def posed(v):
 p=o.matrix_world@v.co;n=o.matrix_world.to_3x3()@v.normal
 ws=[(deform[g.group],g.weight) for g in v.groups if g.group in deform and g.weight>0]
 total=sum(w for m,w in ws)
 if total:p=sum((m@p*w for m,w in ws),Vector())/total;n=sum((m.to_3x3()@n*w for m,w in ws),Vector()).normalized()
 return cv(p),Vector((n.x,n.z,n.y)).normalized()
# Verify the normalized skeleton against the existing import before reusing it.
existing=json.loads(pathlib.Path('F:/Unity/Project/Start/Assets/ThirdParty/WitcherYenneferDLC/Prepared/manifest.json').read_text())
assert len(nodes)==len(existing['nodes'])
assert [n['name'] for n in nodes]==[n['name'] for n in existing['nodes']]
assert max(abs(v-w) for n,e in zip(nodes,existing['nodes']) for v,w in zip(n['position'],e['position']))<.00001
uv=me.uv_layers.active;parts=[]
for side in ['Left','Right']:
 for material in [0,1]:
  tris=[t for t in me.loop_triangles if t.material_index==material and ((sum(posed(me.vertices[v])[0].x for v in t.vertices)<0)==(side=='Left'))]
  lookup={};vertices=[];indices=[]
  for t in tris:
   face=[]
   for vi,li in zip(t.vertices,t.loops):
    tex=tuple(uv.data[li].uv);key=(vi,tex)
    if key not in lookup:
     v=me.vertices[vi];p,n=posed(v)
     ws=sorted([(idx[o.vertex_groups[g.group].name],g.weight) for g in v.groups if o.vertex_groups[g.group].name in idx and g.weight>.00001],key=lambda x:-x[1])[:4]
     total=sum(w for i,w in ws);ws=[(i,w/total) for i,w in ws]+[(0,0)]*(4-len(ws))
     lookup[key]=len(vertices);vertices.append((p,n,tex,ws))
    face.append(lookup[key])
   indices.extend([face[0],face[2],face[1]])
  filename=f'OriginalBoot{side}_{material}.bin'
  with (OUT/filename).open('wb') as f:
   f.write(struct.pack('<4i',len(vertices),len(indices),len(nodes),0))
   for p,n,t,ws in vertices:f.write(struct.pack('<8f4i4f',*p,*n,*t,*[i for i,w in ws],*[w for i,w in ws]))
   f.write(struct.pack('<'+str(len(indices))+'i',*indices))
   for i in range(len(nodes)):
    pos=Vector() if i==0 else positions[keys[i-1]]
    bind=Matrix.Translation(-pos)
    f.write(struct.pack('<16f',*[bind[r][c] for c in range(4) for r in range(4)]))
  parts.append(dict(side=side,material=material,file=filename,vertices=len(vertices),triangles=len(tris),height=max(p.y for p,n,t,w in vertices)-min(p.y for p,n,t,w in vertices)))
report=dict(source='characters/models/main_npc/yennefer/model/l_02_wa__yennefer.w2mesh',blend='F:/Unity/ExtractedModels/Yennefer/Yennefer-dlc-dress.blend',scale=scale,parts=parts)
(OUT/'manifest.json').write_text(json.dumps(report,indent=2))
print('ORIGINAL BOOT EXPORT',json.dumps(report))
