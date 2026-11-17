import hashlib
import json
import unittest

from prepare_current_equipment_runtime import EXTRA_TABLES, ROOT, RULE_TABLES, build


class CurrentEquipmentRuntimeTests(unittest.TestCase):
    def test_runtime_payload_uses_complete_current_tables_and_level_properties(self):
        payload, receipt = build()

        selected = json.loads((ROOT / "official-source.json").read_bytes())["active"]
        self.assertNotIn("manifest", payload)
        self.assertEqual(set(payload["rules"]), set(RULE_TABLES))
        self.assertEqual(set(payload["tables"]), set(EXTRA_TABLES))
        self.assertTrue(payload["props"])
        self.assertEqual(receipt["manifest"], selected)
        self.assertTrue(all(row["path"].startswith(f".tools/source-packages/current/{selected}/")
            for row in receipt["inputs"] if row["kind"] not in ("converter", "selection")))
        table_inputs = {row["table"]: row for row in receipt["inputs"] if row["kind"] == "luban-table"}
        self.assertEqual(set(table_inputs), set(RULE_TABLES + EXTRA_TABLES))
        for name, row in table_inputs.items():
            table = payload["rules"].get(name, payload["tables"].get(name))
            self.assertEqual(len(table), row["entries"])
            self.assertEqual(hashlib.sha256((ROOT / row["path"]).read_bytes()).hexdigest(), row["sha256"])
        required_props = {str(value) for equip in payload["rules"]["equip"]
                          for key, value in equip.items() if key.endswith("PropId") and isinstance(value, int) and value > 0}
        self.assertTrue(required_props.issubset(payload["props"]))


if __name__ == "__main__":
    unittest.main()
