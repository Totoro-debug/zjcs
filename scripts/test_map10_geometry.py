import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from map10_geometry import Map10GeometryError, decode


class Map10GeometryTests(unittest.TestCase):
    def test_decodes_pinned_official_geometry_and_texture_sources(self):
        raw = (ROOT / "assets/official/regions/10.geom").read_bytes()
        batches = decode(raw)

        self.assertEqual(len(batches), 1404)
        self.assertEqual(sum(len(batch["positions"]) // 2 for batch in batches), 17856)
        self.assertEqual(sum(len(batch["indices"]) for batch in batches), 26784)
        self.assertEqual(len({batch["metadata"]["texture"] for batch in batches}), 166)
        self.assertEqual(sum(batch["metadata"].get("effect", {}).get("kind") == "submersion"
                             for batch in batches), 26)
        self.assertTrue(all((ROOT / batch["metadata"]["texture"].removeprefix("res://")).is_file()
                            for batch in batches))
        self.assertEqual(batches[-1]["identifier"], None)

    def test_rejects_truncated_or_unindexed_batch(self):
        raw = (ROOT / "assets/official/regions/10.geom").read_bytes()
        with self.assertRaises(Map10GeometryError):
            decode(raw[:-1])


if __name__ == "__main__":
    unittest.main()
