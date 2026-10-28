"""Check current-map selection, source integrity and preserved EC index identities."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import prepare_unity_map14_interactions as migration
from prepare_unity_map11_runtime_data import build_witch_results


class CurrentDragonStatueSources(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.templates, cls.tables, cls.entities, _ = migration.load_inputs()

    def test_summon_uses_current_map_and_legacy_default_is_preserved(self):
        for map_id, expected_stage in ((11, 20001), (14, 20004)):
            kwargs = {} if map_id == 11 else {"map_id": map_id}
            effects, _ = build_witch_results(self.tables, self.entities, self.tables["reward_rule"], **kwargs)
            summon = next(row for row in effects if row["ComponentType"] == 245)
            self.assertEqual((summon["ComponentIndex"], summon["GroupId"], summon["SpawnEntityClassId"], summon["SpawnStageId"]),
                             (0, 2000, 5100, expected_stage))

    def test_every_template_component_retains_original_order_and_fields(self):
        for row in self.entities:
            original = self.templates[str(row["EntityClassId"])]
            self.assertEqual([value["Type"] for value in row["Components"]], list(original))
            self.assertEqual({value["Type"]: json.loads(value["Json"]) for value in row["Components"]}, original)
        runtime = json.loads((migration.ROOT / "unity/Assets/Game/Resources/Map14/stories.json").read_bytes())
        self.assertEqual(runtime["entityTemplates"], self.entities)

    def test_hero_weights_status_identity_and_region_source_order(self):
        payload = migration.interaction_payload(self.templates, self.tables)
        self.assertEqual([row["EntityClassId"] for row in payload["heroes"]], [6570, 6571, 6572, 6573])
        for hero in payload["heroes"]:
            self.assertEqual([row["Weight"] for row in hero["PeriodicAwards"]], [5, 25, 70])
            self.assertEqual([row["PeriodicityId"] for row in hero["PeriodicAwards"]], [100, 101, 102])
            effects = [row for row in hero["Effects"] if row["ComponentType"] == 235]
            self.assertEqual([row["ComponentIndex"] for row in effects], [0, 1, 2])
        # The source intentionally lists region 76 before 75. Membership checks
        # must not silently sort the original table into another order.
        self.assertEqual([row["Region"] for row in payload["regions"]][74:78], [74, 76, 75, 77])
        self.assertEqual(len(payload["regions"]), 111)
        self.assertEqual(len(payload["areas"]), 115)

    def test_unresolved_original_result_index_is_rejected(self):
        changed = copy.deepcopy(self.tables)
        changed["witch"][0]["StageResultComps"]["235"] = 999
        with self.assertRaisesRegex(ValueError, "component/index is unresolved"):
            migration.witch_payload(changed, self.entities)

    def test_modified_source_cannot_be_used_as_official_input(self):
        with tempfile.TemporaryDirectory(prefix="map14-interaction-source-") as temp:
            target = Path(temp)
            for name in ("tables.json", "templates.json"):
                (target / name).write_bytes((migration.SOURCE / name).read_bytes())
            (target / "templates.json").write_bytes(b"{}")
            # Keep relative source identity inside the workspace for the guard.
            with patch.object(migration, "ROOT", target.parent), patch.object(migration, "SOURCE", target):
                receipt = json.loads(migration.SOURCE_RECEIPT.read_bytes())
                for row in receipt["outputs"]:
                    name = Path(row["path"]).name
                    if name in ("tables.json", "templates.json"):
                        row["path"] = target.name + "/" + name
                fake = target / "receipt.json"
                fake.write_text(json.dumps(receipt), encoding="utf-8")
                with patch.object(migration, "SOURCE_RECEIPT", fake), self.assertRaisesRegex(ValueError, "input changed"):
                    migration.load_inputs()


if __name__ == "__main__":
    unittest.main()
