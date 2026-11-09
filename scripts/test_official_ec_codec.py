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


    # Original 884 FightSkill_part2533, entity 60680: the component
    # inherits its configuration through FightStatusHpDecreaseUnitComponent.
    # Its untagged float cannot be decoded as an ordinary MessagePack array.
    def test_original_healing_component_uses_inherited_configuration(self):
        payload = bytes.fromhex('9990023e19999aff9196cded0a3f800000c296c290c200c2c2029090c3c2ff')
        actual = self.formats.component('Common.FightStatusHpIncreaseUnitComponent', payload)
        self.assertAlmostEqual(actual['UnitHpPer'], 0.15)
        self.assertEqual(actual['TargetType'], 2)
        self.assertEqual(actual['MaxCount'], -1)
        self.assertEqual(actual['InvokeMaxCountOnHpChange'], -1)
        self.assertEqual(len(actual['StatusList']), 1)
        self.assertEqual(actual['Skills'], [])
        self.assertTrue(actual['TryAtStart'])
        self.assertFalse(actual['TriggerWhenHpLessThan0'])
        self.assertEqual(actual, self.formats.component(
            'Common.FightStatusHpDecreaseUnitComponent', payload))
        self.assertIn('component:FightStatusHpIncreaseUnitComponent', self.formats.sources)
        self.assertIn('component:FightStatusHpDecreaseUnitComponent', self.formats.sources)

if __name__ == '__main__':
    unittest.main()
