"""Convert original feather/scarf maps and retain their material provenance.

Run audit_yennefer_appearance.py --all first; this script only reads extracted DDS files.
"""
import argparse
import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from PIL import Image, ImageChops

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', type=Path, default=Path('F:/Unity/ExtractedModels/Yennefer'))
parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2])
parser.add_argument('--audit', type=Path, required=True)
args = parser.parse_args()
output = args.project / 'Assets/ThirdParty/WitcherYenneferDLC/Prepared/CostumeAppearance'
output.mkdir(parents=True, exist_ok=True)
audit = json.loads(args.audit.read_text(encoding='utf-8'))
records = []
materials = []

def convert(name, relative, surface=False):
    path = args.source / relative.replace('\\', '/')
    path = path.with_suffix('.dds') if path.suffix == '.xbm' else path
    with Image.open(path) as source:
        image = source.convert('RGBA')
        if surface:
            # Keep roughness separate: normal-map compression does not retain its alpha.
            packed = Image.new('RGBA', image.size, (255, 0, 0, 255))
            packed.putalpha(Image.new('L', image.size, 255))
            red, _, blue, alpha = packed.split()
            image = Image.merge('RGBA', (red, image.getchannel('A'), blue, alpha))
        existing={'FeatherDiffuse':'Texture_12.png','FeatherNormal':'Texture_13.png',
                  'ScarfDiffuse':'Texture_24.png','ScarfNormal':'Texture_25.png'}.get(name)
        if existing:
            with Image.open(output.parent / existing) as prepared:
                if image.size!=prepared.size or ImageChops.difference(image,prepared.convert('RGBA')).getbbox(alpha_only=False):
                    raise ValueError(f'Existing prepared source pixels differ: {existing}')
        else:
            image.save(output / (name + '.png'))
    records.append(dict(name=name, source=path.relative_to(args.source).as_posix(),
                        sourceSHA256=hashlib.sha256(path.read_bytes()).hexdigest(),
                        size=list(image.size), prepared=existing or 'CostumeAppearance/'+name+'.png',
                        channels='R=1 G=normal DDS alpha roughness B=0 A=1' if surface else 'unchanged RGBA'))

for prefix, material_name in [('Feather', 'feathers_05_wa__yennefer_Material0'), ('Scarf', 'scarf_01_wa__yennefer_Material0')]:
    material = next(m for m in audit if m['name'] == material_name)
    props = material['properties']['witcher_props']
    params = {n.get('name'): n.get('value') for n in ET.fromstring(props['xml_text']).findall('param')}
    materials.append(dict(name=material_name, shader=props['base_custom'], parameters=params))
    convert(prefix + 'Diffuse', params['Diffuse'])
    convert(prefix + 'Normal', params['Normal'])
    convert(prefix + 'Surface', params['Normal'], surface=True)
    if prefix == 'Scarf':
        convert('ScarfDetail', params['DetailNormal'])
        index=int(float(params['Pattern_Index']))
        convert('ScarfPattern', params['Pattern_Array'] + f'.texture_{index}.dds')
(output / 'source.json').write_text(json.dumps(dict(textures=records, materials=materials), indent=2), encoding='utf-8')
print('Prepared', len(records), 'original costume inputs')
