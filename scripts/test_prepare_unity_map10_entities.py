import json
from pathlib import Path
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from prepare_unity_map10_entities import build, resolve_display_path, sha256, write_generated


class PrepareUnityMap10EntitiesTests(unittest.TestCase):
    def test_uses_official_generic_display_binding_for_map_bonus_entities(self):
        model_catalog = {"models": {
            "Map/Common/EntityTemplate/0template_singleitem_view": {"id": "single"},
            "Map/Common/EntityTemplate/0template_singleitem_shallow_view": {"id": "shallow"},
        }}
        map_displays = [
            {"EntityClassId": 8211, "GroupId": 0, "MapClassId": 0, "AreaTag": "IgnoreKey",
             "GroundType": 8888, "DisplayPath": "Map/Common/EntityTemplate/0template_singleitem_view"},
            {"EntityClassId": 8211, "GroupId": 0, "MapClassId": 0, "AreaTag": "IgnoreKey",
             "GroundType": 6, "DisplayPath": "Map/Common/EntityTemplate/0template_singleitem_shallow_view"},
        ]
        self.assertEqual(resolve_display_path(8211, model_catalog, map_displays, 11),
                         "Map/Common/EntityTemplate/0template_singleitem_view")

    def test_prepares_official_display_bindings_and_resources(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            manifest = build(output)
            payload = json.loads((output / "map10-entities.json").read_text(encoding="utf-8"))

            self.assertEqual(len(payload["entities"]), 82)
            self.assertEqual(len({row["entityClassId"] for row in payload["entities"]}), 26)
            self.assertEqual(payload["missingDisplayEntityClassIds"], [8210])
            self.assertEqual(len(payload["models"]), 26)
            self.assertEqual(len(payload["geometryFiles"]), 21)
            self.assertEqual(len(payload["spineFiles"]), 8)
            self.assertEqual(len(payload["textureUris"]), 41)
            item_pickup = next(entity for entity in payload["entities"] if entity["placementId"] == 103)
            self.assertEqual(item_pickup["lockedByEntityGroups"], [115])
            self.assertEqual(item_pickup["awardItems"], [{"ItemId": 22000, "Count": 1}])
            exit_entity = next(entity for entity in payload["entities"] if entity["placementId"] == 501)
            self.assertEqual(exit_entity["changeMapClassId"], 11)
            self.assertEqual(exit_entity["lockedByEntityGroups"], [502])
            self.assertTrue(exit_entity["conditionEntityInactiveInUnlock"])
            self.assertEqual(sum(bool(entity["awardItems"]) for entity in payload["entities"]), 5)
            explore_points = [entity for entity in payload["entities"] if entity["typeId"] == 503]
            self.assertEqual(len(explore_points), 12)
            self.assertTrue(all(not entity["stageIds"] and not entity["awardItems"] for entity in explore_points))
            story_model = next(model for model in payload["models"]
                               if model["modelPath"] == "Map/Common/EntityDisplaysWorldNPC/WorldNPC_Common/8402_view")
            self.assertEqual(story_model["modelId"], "dfc13d6b3f1fec3d")
            self.assertEqual(story_model["geometryResource"],
                             "Map10/EntityGeometry/dfc13d6b3f1fec3d.geom")
            self.assertEqual(story_model["spines"][0]["resource"], "Map10/Spines/8402-0")
            self.assertTrue((output / "Resources/Map10/EntityGeometry/dfc13d6b3f1fec3d.geom.bytes").is_file())
            self.assertTrue((output / "Resources/Map10/Spines/8402-0.json").is_file())
            self.assertEqual((output / "Resources/Map10/Entities/catalog.json").read_bytes(),
                             (output / "map10-entities.json").read_bytes())

            for model_id, record in payload["geometryFiles"].items():
                source = ROOT / record["source"]
                copied = output / record["path"]
                self.assertEqual(copied.read_bytes(), source.read_bytes())
                self.assertEqual(sha256(copied.read_bytes()), record["sha256"])

            for record in payload["spineFiles"].values():
                copied = output / record["path"]
                sequence = json.loads(copied.read_text(encoding="utf-8"))
                self.assertEqual(sequence["monsterId"], sequence["entityClassId"])
                self.assertGreater(len(sequence["frames"]), 0)
                self.assertGreater(sequence["fps"], 0)
                self.assertEqual(sha256(copied.read_bytes()), record["sha256"])
                self.assertTrue((output / "Resources/Map10/Textures" / Path(record["textureUri"]).name).is_file())

            self.assertEqual(manifest["output"]["sha256"], sha256((output / "map10-entities.json").read_bytes()))
            recorded = json.loads((output / "entity-source-manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(recorded["output"]["sha256"], manifest["output"]["sha256"])

    def test_refuses_to_replace_changed_entity_data(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "map10-entities.json"
            output.write_text("changed", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Refusing to overwrite changed Map10 entity payload"):
                write_generated(output, b"expected")


if __name__ == "__main__":
    unittest.main()
