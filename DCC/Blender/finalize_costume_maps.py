"""Keep special REDengine clothing inputs in the URP material adaptation."""
from pathlib import Path
import json,math,sys
from PIL import Image
ROOT=Path('F:/Unity/Project/StartMotionValidation/Assets/ThirdParty')
for name in (sys.argv[1:] or ['YenneferDLC','CiriDLC']):
 folder=ROOT/('Witcher'+name)/'Prepared';file=folder/'costume-materials.json';data=json.loads(file.read_text());sources=json.loads((folder/'costume-source-inputs.json').read_text())
 for record in data['materials']:
  if name=='YenneferDLC' and not record['name'].endswith(('Material2','Material4')):
   p=record['parameters'];values=[float(x.strip())/255 for x in p.get('SpecularColor','51;51;51;255').split(';')[:3]];peak=max(max(values),.001);scale=float(p.get('RSpecScale','1'))
   def srgb(v):return 12.92*v if v<=.0031308 else 1.055*v**(1/2.4)-.055
   rgb=tuple(round(max(0,min(1,srgb(.04*x/peak*scale)))*255) for x in values)
   image=Image.open(folder/record['specular']).convert('RGBA');alpha=image.getchannel('A');result=Image.new('RGB',image.size,rgb).convert('RGBA');result.putalpha(alpha);result.save(folder/record['specular'])
   record['reflectanceAdaptation']='Source specular hue and scale applied to 0.04 dielectric F0; metal trim keeps original colored reflection.'
  if name=='CiriDLC' and 'chainmail' in record['sourceShader']:
   source=next(m for m in sources['materials'] if m['name']==record['name'])
   for key,label in [('chain_d','Diffuse'),('chain_n','Normal')]:
    image=Image.open(source['textures'][key]).convert('RGBA');image.thumbnail((2048,2048));filename='Costume_Chain_'+label+'.png';image.save(folder/filename);record['albedo' if key=='chain_d' else 'normal']=filename
   record['tileX'],record['tileY']=[float(v.strip()) for v in source['parameters']['chain_tile'].split(';')[:2]]
   record['specialAdaptation']='Original chain_d / chain_n pair and chain_tile; no body-atlas color used for chainmail.'
 file.write_text(json.dumps(data,indent=2))
 print('FINALIZED ORIGINAL COSTUME INPUTS',name)
