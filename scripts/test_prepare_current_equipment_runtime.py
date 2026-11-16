import unittest

from prepare_current_equipment_runtime import EXTRA_TABLES, MANIFEST, RULE_TABLES, build


class CurrentEquipmentRuntimeTests(unittest.TestCase):
    def test_runtime_payload_uses_complete_current_tables_and_level_properties(self):
        payload, receipt = build()

        self.assertEqual(payload["manifest"], MANIFEST)
        self.assertEqual(MANIFEST, "884_156692")
        self.assertEqual(set(payload["rules"]), set(RULE_TABLES))
        self.assertEqual(set(payload["tables"]), set(EXTRA_TABLES))
        self.assertEqual(len(payload["props"]), 101)
        self.assertEqual(len(payload["rules"]["equip"]), 1223)
        self.assertEqual(len(payload["rules"]["item"]), 12725)
        self.assertEqual(len(payload["tables"]["equip_resonance"]), 240)
        self.assertEqual(receipt["manifest"], MANIFEST)
        self.assertTrue(all(row["path"].startswith(".tools/source-packages/current/884_156692/")
            for row in receipt["inputs"] if row["kind"] not in ("converter", "selection")))


if __name__ == "__main__":
    unittest.main()
