"""Tests for the pinned Map10 minimap resource preparation."""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import prepare_unity_map10_minimap as prepare


class Map10MinimapPreparationTests(unittest.TestCase):
    def test_prepares_official_geometry_and_textures_with_hashes(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            manifest = prepare.build(output)
            self.assertEqual(manifest["manifest"], "854_156383")
            self.assertEqual(manifest["geometryBatchCount"], 9)
            self.assertEqual(len(manifest["outputs"]), 7)
            for item in manifest["outputs"]:
                path = output / item["path"]
                self.assertEqual(prepare.sha256(path.read_bytes()), item["sha256"])
            catalog = json.loads((output / prepare.CATALOG_RESOURCE).read_text(encoding="utf-8"))
            self.assertEqual(catalog["geometryVersion"], 9)
            self.assertEqual(len(catalog["textures"]), 5)

    def test_changed_existing_output_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            prepare.build(output)
            target = output / prepare.GEOMETRY_RESOURCE
            target.write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "differs from its manifest|Refusing to overwrite changed"):
                prepare.build(output)


if __name__ == "__main__":
    unittest.main()
