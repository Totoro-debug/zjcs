"""Regress original EC polymorphic configuration and strict byte consumption."""
import unittest
from pathlib import Path

from official_ec_codec import Formats

ROOT = Path(__file__).resolve().parents[1]


class OfficialEcConfigurationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.formats = Formats(ROOT / '.tools/source-packages/current/884_156692/official-code')

    # Original 884 FightSkill_part101, entity 12030, component
    # FightSkillBehAttachOperateComponent. The fragment and original bundle
    # hashes are retained in active-entity-configuration-source-manifest.json.
    payload = bytes.fromhex('91912a789500cd33723f800000c2c2')

    def decode(self, payload):
        return self.formats.component('Common.FightSkillBehAttachOperateComponent', payload)

    def test_original_nonabstract_registered_base_and_registered_leaf(self):
        self.assertEqual(self.decode(self.payload), {'AttachOperateSettings': [{
            'AttachOperateType': 0, 'SkillEntityClassId': 13170, 'Rate': 1.0,
            'IsUseMainSkillTarget': False, 'IsUseAI': False,
            '_type': 'SkillAttachChildSkillSetting'}]})

    def test_original_null_polymorphic_entry(self):
        self.assertEqual(self.decode(bytes.fromhex('91910000')),
                         {'AttachOperateSettings': [None]})

    def test_unknown_polymorphic_opcode_is_rejected(self):
        with self.assertRaisesRegex(ValueError, 'Unknown official .* opcode 65535'):
            self.decode(self.payload[:2] + b'\xff\xff' + self.payload[4:])

    def test_truncated_component_is_rejected(self):
        with self.assertRaises(ValueError):
            self.decode(self.payload[:-1])

    def test_trailing_component_bytes_are_rejected(self):
        with self.assertRaisesRegex(ValueError, 'consumed 15/16'):
            self.decode(self.payload + b'\x00')


if __name__ == '__main__':
    unittest.main()
