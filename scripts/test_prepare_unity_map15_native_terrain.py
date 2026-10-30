"""Verify the installed 871 Map15 terrain closure and its conversion receipt."""
import hashlib
import json
import unittest

from prepare_unity_map15_native_terrain import RECEIPT, ROOT


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class Map15NativeTerrainTests(unittest.TestCase):
    def test_installed_closure_and_bindings_match_source_receipt(self):
        receipt = json.loads(RECEIPT.read_bytes())
        generator = ROOT / receipt["generator"]["path"]
        self.assertEqual(receipt["manifest"], "871_156598")
        self.assertEqual(receipt["root_count"], 14)
        self.assertEqual(receipt["closure_files"], 794)
        self.assertEqual(receipt["unresolved_guids"], [])
        self.assertEqual(sha256(generator), receipt["generator"]["sha256"])
        self.assertEqual(len(receipt["outputs"]), 753)
        self.assertEqual(len(receipt["scripts"]), 31)
        self.assertEqual(len(receipt["shaders"]), 10)
        self.assertEqual({row["name"] for row in receipt["converted_shaders"]}, {
            "Universal Render Pipeline/2D/Sprite-WithTransparentMask",
            "Unlit/TransparentMask",
        })

        output_paths = set()
        for record in receipt["outputs"]:
            output = ROOT / record["output"]
            meta = ROOT / (record["output"] + ".meta")
            self.assertNotIn(record["output"], output_paths)
            output_paths.add(record["output"])
            self.assertEqual(sha256(output), record["output_sha256"], record["output"])
            self.assertEqual(sha256(meta), record["output_meta_sha256"], str(meta))
            self.assertRegex(record["proposed_output_meta_sha256"], r"^[a-f0-9]{64}$")

        for record in receipt["converted_shaders"]:
            output = ROOT / record["output"]
            meta = ROOT / (record["output"] + ".meta")
            self.assertEqual(sha256(output), record["output_sha256"])
            self.assertEqual(sha256(meta), record["output_meta_sha256"])
            self.assertRegex(record["proposed_output_meta_sha256"], r"^[a-f0-9]{64}$")
            self.assertTrue(all(program["source"] and program["sha256"]
                                for program in record["programs"]))


if __name__ == "__main__":
    unittest.main()
