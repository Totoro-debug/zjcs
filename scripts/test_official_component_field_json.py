"""Check lossless transport of official Timeline nonfinite floating point fields."""
import json
import struct
import unittest

from recover_current_component_fields import FLOAT_TAG, encode_fields, decode_fields


class ComponentFieldJsonTests(unittest.TestCase):
    def test_standard_json_round_trip_keeps_float_bits_and_regular_fields(self):
        bits = ['7ff0000000000000', 'fff0000000000000', '7ff8000000012345', '8000000000000000']
        scalars = [struct.unpack('>d', bytes.fromhex(value))[0] for value in bits]
        fields = {'m_ClipIn': scalars[0], 'nested': scalars[1:], 'flag': True, 'id': 17, 'text': '原字段'}
        payload = json.dumps(encode_fields(fields), allow_nan=False)
        def reject(token):
            raise AssertionError('Nonstandard JSON constant: ' + token)
        restored = decode_fields(json.loads(payload, parse_constant=reject))
        actual = [restored['m_ClipIn'], *restored['nested']]
        self.assertEqual([struct.pack('>d', value).hex() for value in actual], bits)
        self.assertIs(restored['flag'], True)
        self.assertEqual((restored['id'], restored['text']), (17, '原字段'))

    def test_reserved_field_and_invalid_tags_are_rejected(self):
        with self.assertRaises(ValueError):
            encode_fields({FLOAT_TAG: 'source value'})
        for value in ('0000000000000000', 'not float bits', 0, None):
            with self.assertRaises(ValueError):
                decode_fields({FLOAT_TAG: value})


if __name__ == '__main__':
    unittest.main()
