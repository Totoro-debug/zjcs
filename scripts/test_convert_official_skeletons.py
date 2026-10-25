"""Regression cases for image bindings lost by the upstream binary format reader."""
import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from convert_official_skeletons import reader_type


class AttachmentImageBindingTests(unittest.TestCase):
    def decode(self, key, raw, strings):
        reader_class, binary_input = reader_type()
        reader = reader_class()
        reader.inp = binary_input(raw)
        reader.inp.strings = strings
        reader.nonessential = False
        value = reader._read_attachment(key)
        self.assertEqual(reader.inp.index, len(raw))
        return value

    def test_region_keeps_name_when_binary_path_is_implicit(self):
        # Official layout: flags, image-name ref, x/y, scales, width/height.
        raw = bytes([8, 1]) + struct.pack('>6f', 0, 0, 1, 1, 139, 154)
        value = self.decode('eye_yandi', raw, ['face/eye_01_yandi'])
        self.assertEqual(value.get('path', value.get('name', 'eye_yandi')), 'face/eye_01_yandi')

    def test_mesh_keeps_name_when_image_and_attachment_keys_differ(self):
        raw = bytes([10, 1, 3, 3]) + struct.pack('>12f',
            0, 0, 10, 0, 0, 10, 0, 0, 1, 0, 0, 1) + bytes([0, 1, 2])
        value = self.decode('hair_A_toufaA_B02', raw, ['hair_M_01/hair_01A_toufaA_B04'])
        self.assertEqual(value.get('path', value.get('name', 'hair_A_toufaA_B02')),
                         'hair_M_01/hair_01A_toufaA_B04')
        self.assertEqual(value['triangles'], [0, 1, 2])

    def test_explicit_path_takes_precedence_over_image_name(self):
        raw = bytes([24, 1, 2]) + struct.pack('>6f', 0, 0, 1, 1, 139, 154)
        value = self.decode('eye_yandi', raw, ['image-alias', 'face/eye_01_yandi'])
        self.assertEqual(value['name'], 'image-alias')
        self.assertEqual(value['path'], 'face/eye_01_yandi')


if __name__ == '__main__':
    unittest.main()
