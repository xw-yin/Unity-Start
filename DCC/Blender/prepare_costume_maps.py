"""Pack original game specular/roughness and cloth normals for Unity URP.

All image inputs come from the locally extracted character materials. No
generated textures or geometry are used. Source parameters remain in JSON.
"""
import bpy,json,math,hashlib,sys
import numpy as np
from pathlib import Path
ROOT=Path('F:/Unity/Project/StartMotionValidation/Assets/ThirdParty')
SIZE=2048
grid=(np.arange(SIZE,dtype=np.float32)+.5)/SIZE
U,V=np.meshgrid(grid,grid)
def image(path):
    im=bpy.data.images.load(path,check_existing=True);im.colorspace_settings.name='Non-Color'
    pixels=np.empty(len(im.pixels),dtype=np.float32);im.pixels.foreach_get(pixels)
    return pixels.reshape(im.size[1],im.size[0],4)
def sample(pixels,u=U,v=V):
    h,w=pixels.shape[:2];x=u*w-.5;y=v*h-.5;ix=np.floor(x).astype(int);iy=np.floor(y).astype(int);fx=(x-ix)[...,None];fy=(y-iy)[...,None]
    return (pixels[iy%h,ix%w]*(1-fx)+pixels[iy%h,(ix+1)%w]*fx)*(1-fy)+(pixels[(iy+1)%h,ix%w]*(1-fx)+pixels[(iy+1)%h,(ix+1)%w]*fx)*fy
def normalize(n):return n/np.maximum(np.linalg.norm(n,axis=2,keepdims=True),1e-6)
def normal(rgb):
    n=rgb[...,:3]*2-1;n[...,2]=np.sqrt(np.maximum(0,1-n[...,0]**2-n[...,1]**2));return normalize(n)
def save(path,pixels):
    im=bpy.data.images.new(path.stem,SIZE,SIZE,alpha=True);im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(np.clip(pixels,0,1).astype(np.float32).ravel());im.filepath_raw=str(path);im.file_format='PNG';im.save();bpy.data.images.remove(im)
def vec(text,default):
    try:return [float(v.strip()) for v in text.split(';')]
    except:return default
for name in (sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['Triss','YenneferDLC','CiriDLC']):
    folder=ROOT/('Witcher'+name)/'Prepared';data=json.loads((folder/'costume-source-inputs.json').read_text());records=[]
    for index,m in enumerate(data['materials']):
        p=m['parameters'];tex=m['textures'];source=tex.get('Normal')
        if not source or not Path(source).is_file():continue
        if 'skin' in m['shader'] or 'hair' in m['shader'] or 'lash' in m['name'].lower():continue
        base=sample(image(source));n=normal(base)
        for key,tile_key,rotation_key in [('DetailNormal','DetailTile','DetailRotation'),('DetailNormal1','DetailTile1','DetailRotation1'),('DetailNormal2','DetailTile2','DetailRotation2'),('Detail2Normal','Detail2Tile','Detail2Rotation')]:
            if key not in tex or not Path(tex[key]).is_file():continue
            tile=vec(p.get(tile_key,''),[1,1]);rotation=float(p.get(rotation_key,'0'))*math.tau;cos=math.cos(rotation);sin=math.sin(rotation)
            x=U*tile[0];y=V*tile[1];d=normal(sample(image(tex[key]),x*cos-y*sin,x*sin+y*cos));dx=d[...,0].copy();dy=d[...,1].copy();d[...,0]=dx*cos+dy*sin;d[...,1]=-dx*sin+dy*cos
            power=min(1,float(p.get('DetailPower','1')));d[...,:2]*=power;d=normalize(d)
            t=n.copy();t[...,2]+=1;z=d.copy();z[...,:2]*=-1;n=normalize(t*np.sum(t*z,axis=2,keepdims=True)/np.maximum(t[...,2:3],1e-5)-z)
        packed=np.concatenate((n*.5+.5,np.ones((*n.shape[:2],1))),axis=2)
        normal_file=f'Costume_{index:02}_Normal.png';save(folder/normal_file,packed)
        spec=tex.get('SpecularTexture');rgb=sample(image(spec))[...,:3] if spec and Path(spec).is_file() else np.broadcast_to(np.array(vec(p.get('SpecularColor','51;51;51;255'),[51,51,51,255])[:3])/255,(*n.shape[:2],3))
        if name=='YenneferDLC' and not spec and not m['name'].endswith(('Material2','Material4')):
            # REDengine's standard material tint is not an absolute metallic F0.
            # Retain the hue and source scale on a dielectric reflectance.
            tint=np.array(vec(p.get('SpecularColor','51;51;51;255'),[51,51,51,255])[:3])/255
            f0=.04*tint/max(tint.max(),.001)*float(p.get('RSpecScale','1'))
            encoded=np.where(f0<=.0031308,f0*12.92,1.055*np.power(f0,1/2.4)-.055)
            rgb=np.broadcast_to(encoded,(*n.shape[:2],3))
        rmin=max(0,min(1,float(p.get('Roughness_min','0'))));rmax=max(rmin,min(1,float(p.get('Roughness_max','1'))));rough=rmin+(rmax-rmin)*base[...,3:4]
        spec_file=f'Costume_{index:02}_SpecGloss.png';save(folder/spec_file,np.concatenate((rgb,1-rough),axis=2))
        records.append(dict(name=m['name'],normal=normal_file,specular=spec_file,sourceShader=m['shader'],sourceNormal=source,sourceSpecular=spec or p.get('SpecularColor','default'),parameters=p))
        print('COSTUME MAP',name,m['name'],flush=True)
    (folder/'costume-materials.json').write_text(json.dumps(dict(materials=records,description='URP adaptation of source specular RGB, normal-alpha roughness and original tiled cloth normal inputs.'),indent=2))
