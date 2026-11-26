import copy
import struct
import hashlib
import unittest

from unity_compressed_mesh import static_mesh_proof, unpack_ints, unpack_floats


def vector(values, bits, scale=None, start=0):
    packed = sum(value << (index * bits) for index, value in enumerate(values))
    row = dict(m_NumItems=len(values), m_BitSize=bits,
               m_Data=list(packed.to_bytes((len(values) * bits + 7) // 8, 'little')))
    if scale is not None:
        row.update(m_Range=scale, m_Start=start)
    return row


class PackedMeshTests(unittest.TestCase):
    def source(self):
        packed = dict(m_Vertices=vector([0, 1, 2, 3, 2, 1, 1, 0, 3], 2, 3, -1),
                      m_UV=vector([0, 3, 1, 2, 3, 0], 2, 1), m_Triangles=vector([0, 2, 1], 2),
                      m_UVInfo=5, m_Normals=vector([], 0, 0))
        return dict(m_CompressedMesh=packed, m_VertexData=dict(m_VertexCount=0, m_DataSize=b''),
                    m_BindPose=[], m_BoneNameHashes=[], m_IndexFormat=0,
                    m_SubMeshes=[dict(firstByte=0, indexCount=3, baseVertex=0, topology=0)])

    def test_bit_samples_cross_byte_boundaries(self):
        values = [0, 31, 1, 17, 8, 29, 5]
        self.assertEqual(unpack_ints(vector(values, 5)), values)

    def test_original_geometry_samples_have_a_stable_proof(self):
        source = self.source()
        original = copy.deepcopy(source)
        proof = static_mesh_proof(source)
        self.assertEqual(source, original)
        positions = unpack_floats(source['m_CompressedMesh']['m_Vertices'])
        uv = unpack_floats(source['m_CompressedMesh']['m_UV'])
        data = b''.join(struct.pack('<5f', *positions[i*3:i*3+3], *uv[i*2:i*2+2]) for i in range(3))
        self.assertEqual(struct.unpack_from('<5f', data), (-1, 0, 1, 0, 1))
        self.assertEqual(proof['vertex_stream_sha256'], hashlib.sha256(data).hexdigest())
        self.assertEqual(proof['index_stream_sha256'], hashlib.sha256(struct.pack('<3H', 0, 2, 1)).hexdigest())
        self.assertEqual((proof['vertices'], proof['indices']), (3, 3))

    def test_unsupported_channels_and_invalid_indices_are_rejected(self):
        source = self.source()
        source['m_CompressedMesh']['m_Normals'] = vector([1], 1, 1)
        with self.assertRaisesRegex(ValueError, 'channel'):
            static_mesh_proof(source)
        source = self.source()
        source['m_CompressedMesh']['m_Triangles'] = vector([0, 3, 1], 2)
        with self.assertRaisesRegex(ValueError, 'index outside'):
            static_mesh_proof(source)


if __name__ == '__main__':
    unittest.main()
