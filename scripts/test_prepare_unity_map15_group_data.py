"""Verify packaged Map15 Resources against their generated receipt."""
import hashlib
import json
import unittest

from prepare_unity_map15_group_data import OUT, RECEIPT, ROOT


class Map15GroupDataTests(unittest.TestCase):
    def test_installed_group_is_complete_and_hash_locked(self):
        receipt = json.loads(RECEIPT.read_bytes())
        catalog = json.loads((OUT / "group-catalog.json").read_bytes())
        self.assertEqual(receipt["manifest"], "854_156383")
        self.assertEqual(receipt["counts"]["maps"], 60)
        self.assertEqual(receipt["counts"]["regions"], 118)
        self.assertEqual(receipt["counts"]["placements"], 2899)
        self.assertEqual(receipt["counts"]["templates"], 82)
        self.assertEqual(receipt["counts"]["components"], 23291)
        self.assertEqual(len(catalog["maps"]), 60)

        for record in receipt["outputs"]:
            path = ROOT / record["path"]
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), record["sha256"], record["path"])

        placements = 0
        for row in catalog["maps"]:
            payload = json.loads((OUT / "maps" / (str(row["ClassId"]) + ".json")).read_bytes())
            self.assertEqual(payload["map"]["ClassId"], row["ClassId"])
            self.assertEqual(payload["map"]["NextMapClassId"], row["NextMapClassId"])
            self.assertEqual(len(payload["regions"]), row["RegionCount"])
            placed = payload["regionData"]["placement"]["entities"]
            self.assertEqual(len(placed), row["PlacementCount"])
            self.assertEqual({str(item["id"]) for item in placed}, set(payload["entityComponents"]))
            placements += len(placed)

        self.assertEqual(placements, 2899)
        entrance = json.loads((OUT / "maps/15.json").read_bytes())
        self.assertEqual(entrance["map"]["NextMapClassId"], 42001)
        self.assertEqual(entrance["regions"][0]["LimitServerRank"], 5)
        self.assertEqual(len(json.loads((OUT / "entity-templates.json").read_bytes())["templates"]), 82)


if __name__ == "__main__":
    unittest.main()
