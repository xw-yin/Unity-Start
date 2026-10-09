import json,numpy as np
from pathlib import Path
ROOT=Path('F:/Unity/Project/StartMotionValidation/Artifacts/FootwearMotionReview')
OUT=ROOT/'intersections.json'
def unpack(surface):
 v=np.asarray(surface['vertices'],float).reshape(-1,3);t=np.asarray(surface['triangles'],int).reshape(-1,3)
 boundary=np.asarray(surface.get('boundary',[]),float)
 if len(boundary)!=len(v):boundary=np.zeros(len(v))
 return v[t],boundary,t

def intersections(surface,boot,mask):
 a,boundary,indices=unpack(surface);b,_,_=unpack(boot)
 lo,hi=a.min(1),a.max(1);lb,hb=b.min(1),b.max(1)
 ia,ib=np.where(((lo[:,None]<=hb)&(hi[:,None]>=lb)).all(2))
 if not len(ia):return dict(total=0,visible=0,points=[])
 aa,bb=a[ia],b[ib];allpoints=[];allbounds=[];alluv=[];allnormals=[]
 for reverse in [False,True]:
  first,second=(bb,aa) if reverse else (aa,bb)
  e1=second[:,1]-second[:,0];e2=second[:,2]-second[:,0]
  for edge in range(3):
   origin=first[:,edge];direction=first[:,(edge+1)%3]-origin
   h=np.cross(direction,e2);det=np.sum(e1*h,1);valid=np.abs(det)>1e-10
   inv=np.divide(1,det,out=np.zeros_like(det),where=valid)
   s=origin-second[:,0];u=np.sum(s*h,1)*inv;q=np.cross(s,e1)
   v=np.sum(direction*q,1)*inv;amount=np.sum(e2*q,1)*inv
   hit=valid&(u>1e-5)&(v>1e-5)&(u+v<1-1e-5)&(amount>1e-5)&(amount<1-1e-5)
   points=origin[hit]+direction[hit]*amount[hit,None];ids=indices[ia[hit]]
   if reverse:dist=(1-u[hit]-v[hit])*boundary[ids[:,0]]+u[hit]*boundary[ids[:,1]]+v[hit]*boundary[ids[:,2]]
   else:dist=boundary[ids[:,edge]]*(1-amount[hit])+boundary[ids[:,(edge+1)%3]]*amount[hit]
   uv=np.asarray(surface.get('uv',[]),float).reshape(-1,2)
   if reverse:coords=(1-u[hit]-v[hit])[:,None]*uv[ids[:,0]]+u[hit,None]*uv[ids[:,1]]+v[hit,None]*uv[ids[:,2]]
   else:coords=uv[ids[:,edge]]*(1-amount[hit,None])+uv[ids[:,(edge+1)%3]]*amount[hit,None]
   normals=np.asarray(surface.get('normals',[]),float).reshape(-1,3)
   if reverse:ns=(1-u[hit]-v[hit])[:,None]*normals[ids[:,0]]+u[hit,None]*normals[ids[:,1]]+v[hit,None]*normals[ids[:,2]]
   else:ns=normals[ids[:,edge]]*(1-amount[hit,None])+normals[ids[:,(edge+1)%3]]*amount[hit,None]
   allnormals.extend(ns.tolist())
   allpoints.extend(points.tolist());allbounds.extend(dist.tolist());alluv.extend(coords.tolist())
 if not allpoints:return dict(total=0,visible=0,points=[])
 points=np.asarray(allpoints);distance=np.asarray(allbounds)
 if mask['restMasked']:visible=distance<=0
 elif mask['masked']:
  matrix=np.asarray(mask['worldToBoot']).reshape(4,4);local=(np.column_stack((points,np.ones(len(points))))@matrix.T)[:,:3]
  visible=~((local>np.asarray(mask['minimum']))&(local<np.asarray(mask['maximum']))).all(1)
 else:visible=np.ones(len(points),bool)
 if mask.get('openFoot') and mask['masked']:
  matrix=np.asarray(mask['worldToBoot']).reshape(4,4);local=(np.column_stack((points,np.ones(len(points))))@matrix.T)[:,:3]
  ns=np.asarray(allnormals)@matrix[:3,:3].T;ns/=np.maximum(np.linalg.norm(ns,axis=1,keepdims=True),1e-6)
  low=np.asarray(mask['minimum']);high=np.asarray(mask['maximum']);xz=((local[:,[0,2]]>low[[0,2]])&(local[:,[0,2]]<high[[0,2]])).all(1)
  visible&=~((local[:,1]<mask['soleMax'])&(ns[:,1]<.7)&xz)
 if surface.get('alphaTexture'):
  import bpy
  path=surface['alphaTexture'];image=bpy.data.images.load(path,check_existing=True);w,h=image.size;pixels=np.asarray(image.pixels[:],float).reshape(h,w,4)
  uv=np.asarray(alluv);x=np.floor((uv[:,0]%1)*w).astype(int)%w;y=np.floor((uv[:,1]%1)*h).astype(int)%h
  visible&=pixels[y,x,3]>=surface.get('cutoff',.18)
 if '--probe' in __import__('sys').argv and visible.any():print('VISIBLE_LOCAL',local[visible].tolist(),'NORMALS',ns[visible].tolist(),'SOLE',mask['soleMax'])
 return dict(total=len(points),visible=int(visible.sum()),points=points[visible].round(5).tolist()[:20])
results=[]
import sys
for character in [0,2]:
 for file in sorted((ROOT/str(character)/'Geometry').glob('*.json'),key=lambda p:int(p.stem)):
  if '--probe' in sys.argv and not (character==2 and file.stem=='389'):continue
  data=json.loads(file.read_text())
  for side,sample in enumerate(data['sides']):
   for name in ['sock','lace']:
    if not sample.get(name) or not sample[name].get('triangles'):continue
    r=intersections(sample[name],sample['boot'],sample);results.append(dict(character=character,frame=data['frame'],side=side,surface=name,**r))
OUT.write_text(json.dumps(results,indent=2))
problems=[r for r in results if r['visible']]
print('BOOT SURFACE AUDIT',len(results),'samples; visible intersections',sum(r['visible'] for r in problems),'problem states',len(problems))
print(json.dumps([{k:v for k,v in r.items() if k!='points'} for r in problems],indent=2))




