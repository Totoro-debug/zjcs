"""Exercise both official HCC layouts, including an untouched binary suffix."""
import struct
import unittest

from Crypto.Cipher import AES
from Crypto.Util.Padding import pad
from prepare_official_sources import decode_assembly


class AssemblyDecodeTests(unittest.TestCase):
    def encrypt(self, raw):
        key = bytes(v-7 for v in [40,49,83,108,112,123,112,117,110,44,84,63,63,42,133,40])
        return AES.new(key, AES.MODE_CBC, key[::-1]).encrypt(pad(raw, 16))

    def test_legacy_whole_assembly(self):
        original = b'MZ' + bytes(range(256)) * 17
        self.assertEqual(decode_assembly(self.encrypt(original), 'whole'), original)

    def test_new_partial_encryption_preserves_every_tail_byte(self):
        head = b'MZ' + bytes(range(256)) * 8
        encrypted = self.encrypt(head)
        for tail in (b'', b'\x00\xff\x80', bytes(range(256)) * 23):
            raw = struct.pack('<i', len(encrypted)) + encrypted + tail
            self.assertEqual(decode_assembly(raw, 'length-prefixed'), head + tail)

    def test_plain_bootstrap_stays_identical(self):
        raw = b'MZ' + bytes(range(256))
        self.assertEqual(decode_assembly(raw, 'length-prefixed'), raw)

    def test_invalid_header_ciphertext_and_plaintext_are_rejected(self):
        invalid = [b'\x00', struct.pack('<i', 0), struct.pack('<i', -1),
                   struct.pack('<i', 100) + b'x' * 16,
                   struct.pack('<i', 3) + b'abc',
                   struct.pack('<i', 16) + self.encrypt(b'bad')]
        for raw in invalid:
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                decode_assembly(raw, 'length-prefixed')


if __name__ == '__main__':
    unittest.main()
