"""Check Map15 water scripts against pinned official 871 source bytes."""
from __future__ import annotations

import hashlib
from pathlib import Path
import unittest

import json

from prepare_unity_map15_water_scripts import ASSETS, DESTINATION, RECEIPT, SOURCE, migrate, script_guid


class Map15WaterScriptTests(unittest.TestCase):
    def test_generated_scripts_match_pinned_sources_and_meta_guids(self):
        receipt = json.loads(RECEIPT.read_text(encoding="utf-8"))
        self.assertEqual("871_156598", receipt["manifest"])
        records = {record["name"]: record for record in receipt["assets"]}
        for name, (relative, expected_hash) in ASSETS.items():
            with self.subTest(name=name):
                source = SOURCE / relative
                raw = source.read_bytes()
                self.assertEqual(expected_hash, hashlib.sha256(raw).hexdigest())
                output = migrate(name, source)
                target = DESTINATION / name
                self.assertTrue(target.is_file())
                self.assertEqual(output, target.read_bytes())
                self.assertEqual(hashlib.sha256(output).hexdigest(), records[name]["output_sha256"])
                self.assertEqual(expected_hash, records[name]["source_sha256"])
                self.assertEqual(script_guid(name), records[name]["script_guid"])
                self.assertIn("guid: " + script_guid(name), Path(str(target) + ".meta").read_text())
        self.assertIn(b"using Zjcs.Official.World.NativeWater;", migrate("Pond.cs", SOURCE / ASSETS["Pond.cs"][0]))
        self.assertIn(b"using Zjcs.Official.World.NativeWater;", migrate("WaterMaskGenerator.cs", SOURCE / ASSETS["WaterMaskGenerator.cs"][0]))
        transparent_mask = migrate("TransparentMask.cs", SOURCE / ASSETS["TransparentMask.cs"][0])
        self.assertIn(b"using Object = UnityEngine.Object;", transparent_mask)
        self.assertIn(b"camera.TryGetCullingParameters(out val5);", transparent_mask)
        self.assertEqual(2, transparent_mask.count(b"new RenderTargetIdentifier(val)"))
        self.assertNotIn(b"RenderTargetIdentifier.op_Implicit", transparent_mask)


if __name__ == "__main__":
    unittest.main()
