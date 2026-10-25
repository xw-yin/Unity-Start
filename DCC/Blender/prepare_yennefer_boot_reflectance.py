"""Pack a URP reflectance adaptation for the original embossed boot panel.

Source diffuse, UVs and normal textures are unchanged. SpecularColor in the
REDengine graph is a tint with additional RSpec operations, not URP absolute F0.
Preserve its hue on a dielectric 0.04 F0 and retain every packed roughness pixel.
This is an engine adaptation, not a reproduction of the original game shader.
"""
import argparse, hashlib, json
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2])
args = parser.parse_args()
prepared = args.project / 'Assets/ThirdParty/WitcherYenneferDLC/Prepared'
data = json.loads((prepared / 'costume-materials.json').read_text())
record = next(m for m in data['materials'] if m['name'] == 'body_05_wa__yennefer_Material4')
tint = [float(v.strip()) / 255 for v in record['parameters']['SpecularColor'].split(';')[:3]]
linear = [v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in tint]
f0 = [.04 * v / max(linear) for v in linear]
def encode(v):
    return round(255 * (12.92 * v if v <= .0031308 else 1.055 * v ** (1 / 2.4) - .055))
source = prepared / record['specular']
with Image.open(source) as original:
    original = original.convert('RGBA')
    result = Image.new('RGBA', original.size, tuple(encode(v) for v in f0) + (255,))
    result.putalpha(original.getchannel('A'))
    target = prepared / 'BootPanel_SpecGloss.png'
    result.save(target)
    assert result.getchannel('A').tobytes() == original.getchannel('A').tobytes()
report = dict(source=record['name'], sourceMap=source.name,
              sourceMapSHA256=hashlib.sha256(source.read_bytes()).hexdigest(),
              sourceParameters=record['parameters'], output=target.name,
              linearF0=f0, encodedRGB=[encode(v) for v in f0],
              roughnessAlpha='unchanged',
              adaptation='Original specular hue on dielectric F0; independent boot material; original diffuse and normals unchanged')
(prepared / 'BootPanel_Reflectance.json').write_text(json.dumps(report, indent=2))
print(json.dumps({k: v for k, v in report.items() if k != 'sourceParameters'}, indent=2))
