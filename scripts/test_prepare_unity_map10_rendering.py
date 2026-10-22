import json
from pathlib import Path
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from prepare_unity_map10_rendering import build


class PrepareUnityMap10RenderingTests(unittest.TestCase):
    def test_prepares_all_geometry_referenced_official_textures(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "Map10"
            result = build(output)

            self.assertEqual(result["geometry"]["batches"], 1404)
            self.assertEqual(result["geometry"]["vertices"], 17856)
            self.assertEqual(result["geometry"]["indices"], 26784)
            self.assertEqual(len(result["textures"]), 166)
            self.assertEqual([item["monsterId"] for item in result["monsters"]],
                             [3204, 3206, 3207, 3254, 3255, 3281, 3282, 3283, 3401, 3411, 3603])
            for item in result["textures"]:
                copied = output / item["path"]
                self.assertEqual(copied.read_bytes(), (ROOT / item["uri"].removeprefix("res://")).read_bytes())
                self.assertTrue(Path(str(copied) + ".meta").is_file())

            for item in result["monsters"]:
                copied = output / item["payloadPath"]
                payload = json.loads(copied.read_text(encoding="utf-8"))
                self.assertEqual(payload["monsterId"], item["monsterId"])
                self.assertEqual(payload["animation"], "idle_01")
                self.assertEqual(payload["fps"], 12)
                self.assertEqual(len(payload["frames"]), item["frameCount"])
                self.assertEqual(payload["scaleX"], payload["scaleY"])
                self.assertTrue(Path(str(copied) + ".meta").is_file())
                self.assertTrue((output / (item["texturePath"])).is_file())

            manifest = json.loads((output / "rendering-source-manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(manifest["geometry"]["sha256"], result["geometry"]["sha256"])
            self.assertEqual(manifest["monsters"], result["monsters"])

    def test_refuses_to_replace_modified_geometry_payload(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "Map10"
            output.mkdir()
            (output / "map10.geom.bytes").write_bytes(b"user data")
            with self.assertRaisesRegex(ValueError, "Refusing to overwrite"):
                build(output)


if __name__ == "__main__":
    unittest.main()
