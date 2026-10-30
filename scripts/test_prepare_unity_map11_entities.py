import json
from pathlib import Path
import tempfile
import unittest

from prepare_unity_map10_entities import ROOT, build, sha256


class Map11EntityStateTests(unittest.TestCase):
    def test_keeps_goddess_states_and_building_state_dependencies(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            manifest = build(output, 11)
            catalog = json.loads((output / "map11-entities.json").read_text(encoding="utf-8"))
            self.assertEqual(len(catalog["entities"]), 1803)
            self.assertEqual(sum(bool(model.get("states")) for model in catalog["models"]), 24)
            self.assertEqual(sum(len(model.get("states", [])) for model in catalog["models"]), 64)
            self.assertEqual(len(catalog["geometryFiles"]), 171)
            self.assertEqual(len(catalog["textureUris"]), 219)
            models = {model["modelPath"]: model for model in catalog["models"]}
            for entity_class_id in (6010, 6011):
                placement = next(row for row in catalog["entities"] if row["entityClassId"] == entity_class_id)
                states = {row["state"]: row for row in models[placement["modelPath"]]["states"]}
                self.assertEqual(set(states), {-1, 0, 1})
                self.assertIsNone(states[-1]["geometryResource"])
                self.assertEqual(states[-1]["geometryBatchCount"], 0)
                self.assertEqual(states[0]["geometryBatchCount"], 1)
                self.assertGreater(states[1]["geometryBatchCount"], 0)
                self.assertNotEqual(states[0]["geometryResource"], states[1]["geometryResource"])
            for record in manifest["geometry"].values():
                copied = (output / record["path"]).read_bytes()
                self.assertEqual(copied, (ROOT / record["source"]).read_bytes())
                self.assertEqual(sha256(copied), record["sha256"])
            additional = manifest["additionalStateTextureSources"]
            self.assertEqual(len(additional), 2)
            active_source = json.loads((ROOT / "official-source.json").read_text(encoding="utf-8"))["active"]
            for record in additional.values():
                self.assertEqual(record["source"]["manifest"], active_source)
                self.assertEqual(record["source"]["bundle_hash"], "d5637b1b83d26edacbe343c3c1603600")


if __name__ == "__main__":
    unittest.main()
