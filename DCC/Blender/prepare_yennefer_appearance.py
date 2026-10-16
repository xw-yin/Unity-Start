"""Reconvert audited source DDS maps without resampling or editing their pixels.

Usage: python prepare_yennefer_appearance.py --source F:/Unity/ExtractedModels/Yennefer
Requires Pillow. Source hashes must match the checked-in provenance manifest.
"""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', type=Path, required=True)
parser.add_argument('--project', type=Path, default=Path(__file__).resolve().parents[2])
args = parser.parse_args()
output = args.project / 'Assets/ThirdParty/WitcherYenneferDLC/Prepared/Appearance'
manifest = json.loads((output / 'source.json').read_text(encoding='utf-8'))
# Validate all inputs before replacing any prepared images.
for entry in manifest:
    source = args.source / entry['source']
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if digest != entry['sourceSHA256']:
        raise ValueError(f'Source DDS differs from audited input: {source}')
for entry in manifest:
    with Image.open(args.source / entry['source']) as image:
        if list(image.size) != entry['size']:
            raise ValueError(f'Unexpected map dimensions: {entry["name"]}')
        image.convert('RGBA').save(output / (entry['name'] + '.png'))
        print(entry['name'], image.size)
