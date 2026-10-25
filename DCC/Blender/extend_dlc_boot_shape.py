"""Transfer the existing empty-boot fold to the restored original knee panels.

Run with Blender --background --python this.py -- --project <validation project>.
The first 2057 authored vertex deltas remain unchanged. No source geometry changes.
"""
import argparse, json, struct, sys
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

parser = argparse.ArgumentParser()
parser.add_argument('--project', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
root = Path(args.project)
source = root / 'Assets/ThirdParty/WitcherYenneferDLC/DLCBoots'
poses = root / 'Assets/Homecoming/Resources/Homecoming/Poses'

def read(path):
    data = path.read_bytes()
    nv, ni, nb, ns = struct.unpack_from('<4i', data)
    vertices = [Vector(struct.unpack_from('<3f', data, 16 + i * 64)) for i in range(nv)]
    indices = struct.unpack_from(f'<{ni}i', data, 16 + nv * 64)
    return vertices, [tuple(indices[i:i+3]) for i in range(0, ni, 3)]

for side in ('Left', 'Right'):
    vertices, triangles = [], []
    for part in (0, 1):
        v, t = read(source / f'OriginalBoot{side}_{part}.bin')
        triangles.extend(tuple(i + len(vertices) for i in tri) for tri in t)
        vertices.extend(v)
    panels, _ = read(source / f'OriginalBoot{side}_2.bin')
    path = poses / f'BootShape_OriginalYenneferBootDLC{side}.json'
    shape = json.loads(path.read_text())
    if shape['vertexCount'] == len(vertices) + len(panels):
        print('Already extended:', side)
        continue
    assert shape['vertexCount'] == len(vertices) == 2057
    assert len(panels) == 110 and len(shape['delta']) == 3 * len(vertices)
    old = shape['delta'][:]
    deltas = [Vector(old[i:i+3]) for i in range(0, len(old), 3)]
    tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
    distances = []
    for p in panels:
        hit, normal, index, distance = tree.find_nearest(p)
        a, b, c = triangles[index]
        delta = barycentric_transform(hit, vertices[a], vertices[b], vertices[c], deltas[a], deltas[b], deltas[c])
        shape['delta'].extend(delta)
        distances.append(distance)
    assert shape['delta'][:len(old)] == old
    shape['vertexCount'] = len(vertices) + len(panels)
    shape['source'] += '; original knee panels: barycentric transfer from existing boot fold'
    path.write_text(json.dumps(shape, separators=(',', ':')))
    print('Extended', side, 'vertices', shape['vertexCount'], 'maximum transfer distance', max(distances))
