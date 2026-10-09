import bpy,json,struct,pathlib,math,sys
from mathutils import Vector,Matrix
bpy.ops.wm.open_mainfile(filepath='F:/Unity/ExtractedModels/Characters/TrissDLC.blend')
for o in bpy.data.objects:
 o.hide_viewport=False;o.hide_render=False;o.hide_set(False)
for c in bpy.data.collections:c.hide_viewport=False;c.hide_render=False
def reveal(c):
 c.exclude=False;c.hide_viewport=False
 for child in c.children:reveal(child)
reveal(bpy.context.view_layer.layer_collection)
bpy.context.view_layer.update()
KEEP_BODY='--full-body' in sys.argv
OUT=pathlib.Path('F:/Unity/Project/Start/Artifacts/TrissCorrectedBinding/'+('FullBody' if KEEP_BODY else 'Prepared'));OUT.mkdir(parents=True,exist_ok=True)
a=next(o for o in bpy.data.objects if o.type=='ARMATURE' and 'CMovingPhysicalAgent' in o.name and 'pelvis' in o.pose.bones); scale=.92
initial={b.name:a.matrix_world@b.matrix.copy() for b in a.pose.bones}
# APX cache rigs encode a different local basis for the same named helpers.
# Use the actual costume/body constraint for those helpers, and move cached
# world-space cloth by pose deltas instead of rebinding it as a body mesh.
body_helpers={}
for obj in bpy.data.objects:
 if obj.type=='ARMATURE' and obj.name=='triss_dlc:CAnimDangleConstraint_Breast16_ARM':
  for name in ('l_boob','r_boob'):
   body_helpers[name]=(obj,obj.pose.bones[name])
   initial[name]=obj.matrix_world@obj.pose.bones[name].matrix.copy()
if len(body_helpers)!=2:raise RuntimeError('The original Triss breast constraint is missing')
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
objects=[o for o in bpy.data.objects if o.type=='MESH' and 'shadowmesh' not in o.name and '_lod0' in o.name and (any(c.name=='_WitcherRedclothCache' for c in o.users_collection) if 'GMesh' in o.name else not any(c.name=='_WitcherRedclothCache' for c in o.users_collection))]
for o in objects:
 for mod in o.modifiers:
  if mod.type=='NODES':mod.show_viewport=False;mod.show_render=False
  if mod.type=='ARMATURE' and mod.object:
   for b in mod.object.pose.bones:
    if b.name not in bones:bones[b.name]=(mod.object,b)
bpy.context.view_layer.update()
bones.update(body_helpers)
keys=list(bones);idx={k:i+1 for i,k in enumerate(keys)};positions={k:cv(o.matrix_world@b.head) for k,(o,b) in bones.items()}
nodes=[dict(name='YenneferSkeleton',parent=-1,position=[0,0,0],rotation=[0,0,0,1],scale=[1,1,1])];hum=[]
for k,(o,b) in bones.items():
 p=b.parent.name if b.parent else None
 if k in ('l_boob','r_boob'):p='torso3'
 elif o!=a and (p not in idx or p=='Root'):p='neck' if ('pendant' in k or k=='thyroid') else 'head'
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
 alpha='MASK' if any(s in m.name.lower() for s in ['c_01','c_05','fur','lash', 'he_01_wa__'+ 'triss' +'_material4']) else 'OPAQUE'
 if 'eyelash' in str(m.get('witcher3_mat_source_path','')).lower() or 'eyelash' in str(m.get('witcher3_mat_base','')).lower():alpha='MASK'
 color=[.28,.24,.28,1] if m.name.startswith(('c_01','c_05')) else ([.12,.12,.13,1] if 'fur_01' in m.name else [1,1,1,1])
 matindex[m.name]=len(materials);materials.append(dict(name=m.name,alpha=alpha,texture=ti('Diffuse'),normal=ti('Normal'),color=color,cutoff=.25,doubleSided=True));return len(materials)-1
parts=[];dg=bpy.context.evaluated_depsgraph_get()
for o in objects:
 me=o.data;me.calc_loop_triangles()
 arm=next((m.object for m in o.modifiers if m.type=='ARMATURE' and m.object),None)
 deform={}
 if arm:
  for g in o.vertex_groups:
   if g.name in bones and g.name in arm.data.bones:
    target,targetbone=bones[g.name]
    deform[g.index]=(target.matrix_world@targetbone.matrix)@(initial[g.name].inverted() if 'GMesh' in o.name and g.name in initial else (arm.matrix_world@arm.data.bones[g.name].matrix_local).inverted())
 def posed(v):
  p=o.matrix_world@v.co;n=o.matrix_world.to_3x3()@v.normal
  ws=[(deform[g.group],g.weight) for g in v.groups if g.group in deform and g.weight>0]
  if ws:
   total=sum(w for m,w in ws);p=sum((m@p*w for m,w in ws),Vector())/total;n=sum((m.to_3x3()@n*w for m,w in ws),Vector()).normalized()
  return cv(p),Vector((n.x,n.z,n.y)).normalized()
 groupnames={g.index:g.name for g in o.vertex_groups}; uv=me.uv_layers.active
 batches={}
 neighbours={v.index:set() for v in me.vertices}
 for face in me.polygons:
  for vi in face.vertices:neighbours[vi].update(face.vertices)
 components={};todo=set(neighbours)
 while todo:
  seed=todo.pop();pending=[seed];members={seed}
  while pending:
   for vi in neighbours[pending.pop()]&todo:todo.remove(vi);members.add(vi);pending.append(vi)
  top=max(posed(me.vertices[vi])[0].y for vi in members)
  for vi in members:components[vi]=top
 for tri in me.loop_triangles:
  if o.name.startswith('he_01') and tri.material_index==1:continue
  pts=[posed(me.vertices[v])[0] for v in tri.vertices]
  if o.name.startswith('twals_01_wa__body_triss') and not KEEP_BODY and sum(p.y for p in pts)/3<.68:continue
  role='Cloth' if 'GMesh' in o.name else 'Body'
  if o.name.startswith('body_01_wa__triss_dlc') and tri.material_index==2 and max(components[vi] for vi in tri.vertices)<.54:role='ShoeLeft' if sum(p.x for p in pts)<0 else 'ShoeRight'
  mat=surf(o.data.materials[tri.material_index]);batches.setdefault((mat,role),[]).append(tri)
 for (mat,role),tris in batches.items():
  verts=[];inds=[];lookup={};cloth=[]
  for tri in tris:
   face=[]
   for vi,li in zip(tri.vertices,tri.loops):
    t=tuple(uv.data[li].uv) if uv else (0,0);key=(vi,t)
    if key not in lookup:
     v=me.vertices[vi];p,n=posed(v)
     weights=[(idx[groupnames[g.group]],g.weight) for g in v.groups if groupnames.get(g.group) in idx and g.weight>.00001]
     weights=sorted(weights,key=lambda x:-x[1])[:4]
     # Keep the original calf, ankle and foot skinning on the boot shaft.
     if not weights:weights=[(idx['pelvis'],1)]
     total=sum(w for i,w in weights);weights=[(i,w/total) for i,w in weights]+[(0,0)]*(4-len(weights))
     lookup[key]=len(verts);verts.append((p,n,t,weights))
     group=o.vertex_groups.get('PhysXMaximumDistance'); free=next((g.weight for g in v.groups if group and g.group==group.index),0);cloth.append(free*float(o.get('maximumMaxDistance',1))*scale if role=='Cloth' else 0)
    face.append(lookup[key])
   inds.extend(reversed(face))
  filename=f'Part_{len(parts):02}.bin'
  with (OUT/filename).open('wb') as f:
   f.write(struct.pack('<4i',len(verts),len(inds),len(nodes),0))
   for p,n,t,w in verts:f.write(struct.pack('<8f4i4f',*p,*n,*t,*[i for i,v in w],*[v for i,v in w]))
   f.write(struct.pack('<%di'%len(inds),*inds))
   for i in range(len(nodes)):
    p=positions[keys[i-1]] if i else Vector();m=Matrix.Translation(-p);f.write(struct.pack('<16f',*[m[r][c] for c in range(4) for r in range(4)]))
  parts.append(dict(name=o.name+'_'+str(mat)+'_'+role,file=filename,role=role,node=0,material=mat,joints=list(range(len(nodes))),expressions=[],cloth=cloth if role=='Cloth' else []))
(OUT/'manifest.json').write_text(json.dumps(dict(nodes=nodes,materials=materials,parts=parts,humanBones=hum),indent=2))
(OUT/'texture-sources.json').write_text(json.dumps(textures,indent=2))
print('EXPORTED',len(parts),len(nodes),len(textures),flush=True)


