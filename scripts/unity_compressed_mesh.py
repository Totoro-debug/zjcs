"""Verify original packed static-mesh geometry without rewriting its native YAML."""
import hashlib
import math
import struct


def unpack_ints(vector):
    count, bits = vector['m_NumItems'], vector['m_BitSize']
    data = bytes(vector['m_Data'])
    if count < 0 or not 0 <= bits <= 32 or count * bits > len(data) * 8:
        raise ValueError('Invalid original packed mesh vector')
    packed = int.from_bytes(data, 'little')
    mask = (1 << bits) - 1
    return [(packed >> (index * bits)) & mask for index in range(count)]


def unpack_floats(vector):
    integers = unpack_ints(vector)
    scale = vector['m_Range'] / ((1 << vector['m_BitSize']) - 1) if vector['m_BitSize'] else 0
    values = [value * scale + vector['m_Start'] for value in integers]
    if not all(math.isfinite(value) for value in values):
        raise ValueError('Nonfinite original packed mesh sample')
    return values


def static_mesh_proof(original):
    packed = original['m_CompressedMesh']
    if not packed['m_Vertices']['m_NumItems']:
        raise ValueError('Expected original packed mesh positions')
    if original['m_VertexData']['m_VertexCount'] or original['m_VertexData']['m_DataSize']:
        raise ValueError('Review mesh containing both packed and vertex-stream geometry')
    allowed = {'m_Vertices', 'm_UV', 'm_Triangles', 'm_UVInfo'}
    for key, value in packed.items():
        if key not in allowed and isinstance(value, dict) and value['m_NumItems']:
            raise ValueError('Migrate original packed mesh channel: ' + key)
    if original['m_BindPose'] or original['m_BoneNameHashes']:
        raise ValueError('Review original skinned packed mesh')
    vertices = unpack_floats(packed['m_Vertices'])
    if len(vertices) % 3:
        raise ValueError('Incomplete original packed position')
    count = len(vertices) // 3
    uvs = unpack_floats(packed['m_UV'])
    if packed['m_UVInfo'] != 5 or len(uvs) != count * 2:
        raise ValueError('Migrate original packed UV channel layout')
    indices = unpack_ints(packed['m_Triangles'])
    if any(index >= count for index in indices) or not indices:
        raise ValueError('Original packed triangle index outside the vertex range')
    if original['m_IndexFormat'] != 0 or any(row['topology'] != 0 for row in original['m_SubMeshes']):
        raise ValueError('Migrate original packed mesh index layout')
    if any(row['firstByte'] % 2 or row['firstByte'] // 2 + row['indexCount'] > len(indices)
           or row['baseVertex'] != 0 for row in original['m_SubMeshes']):
        raise ValueError('Original packed mesh submesh range differs')
    vertex_bytes = b''.join(struct.pack('<5f', *vertices[index * 3:index * 3 + 3], *uvs[index * 2:index * 2 + 2])
                            for index in range(count))
    index_bytes = struct.pack('<' + 'H' * len(indices), *indices)
    proof = dict(vertices=count, indices=len(indices), stride=20,
                 vertex_stream_sha256=hashlib.sha256(vertex_bytes).hexdigest(),
                 index_stream_sha256=hashlib.sha256(index_bytes).hexdigest(),
                 serialization='Exact verified native mesh YAML retained; generic flow-map rewriting corrupts packed mesh loading.')
    return proof
