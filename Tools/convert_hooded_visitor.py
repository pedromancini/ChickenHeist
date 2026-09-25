"""Reproduce the OBJ from the supplied GLB, preserving texture coordinates."""
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Assets/c12cb2ea-013a-4d05-b832-3f5e1cf0ad28.glb'
DEST = ROOT / 'Assets/ChickenHeistGenerated/Characters/HoodedVisitor'

def main():
    data = SOURCE.read_bytes()
    magic, version, length = struct.unpack_from('<4sII', data)
    assert magic == b'glTF' and version == 2 and length == len(data)
    chunks = {}
    offset = 12
    while offset < length:
        size, kind = struct.unpack_from('<II', data, offset)
        offset += 8
        chunks[kind] = data[offset:offset + size]
        offset += size
    document = json.loads(chunks[0x4E4F534A])
    binary = chunks[0x004E4942]

    def read(index):
        accessor = document['accessors'][index]
        view = document['bufferViews'][accessor['bufferView']]
        count = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3}[accessor['type']]
        fmt = '<' + {5125: 'I', 5126: 'f'}[accessor['componentType']] * count
        stride = view.get('byteStride', struct.calcsize(fmt))
        start = view.get('byteOffset', 0) + accessor.get('byteOffset', 0)
        return [struct.unpack_from(fmt, binary, start + i * stride)
                for i in range(accessor['count'])]

    primitive = document['meshes'][0]['primitives'][0]
    positions = read(primitive['attributes']['POSITION'])
    normals = read(primitive['attributes']['NORMAL'])
    uv = read(primitive['attributes']['TEXCOORD_0'])
    indices = read(primitive['indices'])
    DEST.mkdir(parents=True, exist_ok=True)
    with (DEST / 'HoodedVisitor.obj').open('w', newline='\n') as output:
        output.write('mtllib HoodedVisitor.mtl\nusemtl HoodedVisitor\n')
        for vertex in positions:
            output.write('v %.8f %.8f %.8f\n' % vertex)
        # GLB images use a top-left origin; OBJ uses bottom-left UVs.
        for u, v in uv:
            output.write('vt %.8f %.8f\n' % (u, 1 - v))
        for normal in normals:
            output.write('vn %.8f %.8f %.8f\n' % normal)
        for offset in range(0, len(indices), 3):
            face = [indices[offset + j][0] + 1 for j in range(3)]
            output.write('f ' + ' '.join(f'{i}/{i}/{i}' for i in face) + '\n')
    (DEST / 'HoodedVisitor.mtl').write_text(
        'newmtl HoodedVisitor\nKd 1 1 1\nmap_Kd hooded_visitor_0.png\n')
    print(f'Converted {len(positions)} vertices, {len(indices) // 3} triangles')

if __name__ == '__main__':
    main()
