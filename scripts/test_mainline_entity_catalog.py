"""Regression checks against the pinned original placement configuration."""
import copy
import hashlib
import json
from pathlib import Path
import unittest

from mainline_entity_catalog import project_entities, read_inputs

ROOT = Path(__file__).resolve().parents[1]


class MainlineEntityCatalogTests(unittest.TestCase):
    def test_map16_uses_its_selected_receipt_and_keeps_placement_values(self):
        definition, components, _ = read_inputs(
            16, "884_156692", components_receipt="assets/reference/official-map/map16-definition-source-manifest.json")
        before = copy.deepcopy(components)
        rows = project_entities(definition, components, "Map16/NativeEntity/empty")
        self.assertEqual(1021, len(rows))
        installed = json.loads((ROOT / "unity/Assets/Game/Resources/Map16/Entities/catalog.json").read_bytes())
        self.assertEqual(16, installed["mapClassId"])
        self.assertEqual(rows, installed["entities"])
        self.assertEqual({int(key) for key in components}, {row["placementId"] for row in rows})
        for row in rows:
            original = components[str(row["placementId"])]
            self.assertEqual(original.get("LoWorldStageComponent", {}).get("State", 0), row["state"])
            self.assertEqual(original.get("LoWorldActiveableEntityComponent", {}).get("Active", True), row["active"])
            self.assertEqual([original["LoGridTransformComponent"]["Pos"][axis] for axis in ("X", "Y")], row["grid"])
        self.assertEqual(before, components)
        receipt = json.loads((ROOT / "assets/reference/official-map/map16-entity-catalog-source-manifest.json").read_bytes())
        for record in receipt["inputs"] + receipt["outputs"]:
            self.assertEqual(record["sha256"], hashlib.sha256((ROOT / record["path"]).read_bytes()).hexdigest(), record["path"])
        with self.assertRaisesRegex(ValueError, "source receipt"):
            read_inputs(16, "871_156598",
                        components_receipt="assets/reference/official-map/map16-definition-source-manifest.json")

    def test_map14_keeps_original_state_locks_and_interaction_bindings(self):
        definition, components, _ = read_inputs(14, "871_156598")
        before = copy.deepcopy(components)
        rows = project_entities(definition, components, "Map14/NativeEntity/empty")
        entities = {row["placementId"]: row for row in rows}
        self.assertEqual(components, before, "Projection must not rewrite another consumer's EC data")
        self.assertEqual(len(rows), 2961)
        self.assertEqual(entities[101]["state"], 1)
        self.assertFalse(entities[102]["active"])
        self.assertEqual(entities[103]["stageBindings"],
                         [{"Component": "LoStageBehStageComponent", "StageIds": [240]}])
        self.assertEqual(entities[201]["lockType"], 2)
        self.assertEqual(json.loads(entities[201]["lockParamsJson"]), {
            "EntityStateDict": {"208": 1}, "ConditionEntityNeverDestroy": False,
            "ConditionEntityInactiveInUnlock": True, "IsMetEntityIdSet": []})
        self.assertEqual(entities[301]["awardItems"], [{"ItemId": 107, "Count": 1}])
        self.assertEqual(entities[528]["periodicityGroupIds"], [130])
        self.assertEqual(entities[9701]["changeMapClassId"], 15)
        self.assertEqual(sum(row["state"] != 0 for row in rows), 20)
        self.assertEqual(sum(not row["active"] for row in rows), 147)
        self.assertEqual(sum(row["lockType"] != 0 for row in rows), 92)
        self.assertEqual(sum(bool(row["stageBindings"]) for row in rows), 909)

    def test_map13_existing_catalog_bytes_are_unchanged(self):
        definition, components, _ = read_inputs(13, "871_156598")
        rows = project_entities(definition, components, "Map13/NativeEntity/empty")
        payload = {"mapClassId": 13, "entities": rows,
                   "models": [{"modelPath": "Map13/NativeEntity/empty", "modelId": "empty",
                               "geometryResource": "", "geometryBatchCount": 0, "spines": []}],
                   "textureUris": []}
        raw = (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf8")
        self.assertEqual(hashlib.sha256(raw).hexdigest(),
                         "15961c3328a1c7a91bf211e6f927f21ca63c98df8f080168c8df751c2d4ebe27")

    def test_rejects_partial_or_duplicate_original_placements(self):
        definition, components, _ = read_inputs(14, "871_156598")
        del components["101"]
        with self.assertRaisesRegex(ValueError, "closure"):
            project_entities(definition, components, "Map14/NativeEntity/empty")
        definition["Entities"].append(definition["Entities"][0])
        with self.assertRaisesRegex(ValueError, "duplicated"):
            project_entities(definition, components, "Map14/NativeEntity/empty")


if __name__ == "__main__":
    unittest.main()
