import hashlib
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

            environment = result["environment"]
            self.assertEqual(environment["water"]["rendererCount"], 4)
            self.assertEqual(environment["butterflies"]["instanceCount"], 15)
            self.assertEqual(environment["butterflies"]["animationCount"], 2)
            water_payload = json.loads((output / environment["water"]["payloadPath"]).read_text(encoding="utf-8"))
            butterfly_payload = json.loads((output / environment["butterflies"]["payloadPath"]).read_text(encoding="utf-8"))
            self.assertEqual(len(water_payload["renderers"]), 4)
            self.assertEqual(len(butterfly_payload["instances"]), 15)
            self.assertTrue(Path(str(output / environment["water"]["payloadPath"]) + ".meta").is_file())
            self.assertTrue(Path(str(output / environment["butterflies"]["payloadPath"]) + ".meta").is_file())

            water_runtime = json.loads((output / environment["water"]["runtimePayloadPath"])
                                       .read_text(encoding="utf-8"))
            self.assertEqual(len(water_runtime["renderers"]), 4)
            self.assertEqual(len(water_runtime["renderers"][0]["vertices"]), 84)
            self.assertEqual(water_runtime["renderers"][0]["indices"], water_payload["renderers"][0]["indices"])
            self.assertEqual(water_runtime["maskResource"], "Map10/Environment/map10-water-mask")
            self.assertEqual(water_runtime["maskRect"], [-24.0, -29.0, 50.0, 53.0])
            noise = next(item for item in water_runtime["renderers"][0]["textures"]
                         if item["property"] == "_NoiseTex")
            self.assertEqual(noise["scale"], [0.20000000298023224, 1.0])
            self.assertEqual(noise["offset"], [0.0, 0.0])
            self.assertTrue(Path(str(output / environment["water"]["runtimePayloadPath"]) + ".meta").is_file())

            butterfly_runtime = json.loads((output / environment["butterflies"]["runtimePayloadPath"])
                                            .read_text(encoding="utf-8"))
            self.assertEqual(len(butterfly_runtime["instances"]), 15)
            self.assertEqual(sum(item["animationResource"].endswith("loop1")
                                 for item in butterfly_runtime["instances"]), 7)
            self.assertEqual(sum(item["animationResource"].endswith("loop2")
                                 for item in butterfly_runtime["instances"]), 8)
            self.assertEqual(len(butterfly_runtime["instances"][0]["motion"]["tracks"]), 6)
            first_curve = butterfly_runtime["instances"][0]["motion"]["tracks"][0]
            self.assertEqual(first_curve["property"], "position.x")
            self.assertAlmostEqual(first_curve["keys"][0]["outTangent"], -1.0327868461608887)
            self.assertEqual(butterfly_runtime["instances"][0]["sortingOrder"], 18003)
            self.assertTrue(Path(str(output / environment["butterflies"]["runtimePayloadPath"]) + ".meta").is_file())
            for item in environment["butterflies"]["animations"]:
                animation = json.loads((output / item["payloadPath"]).read_text(encoding="utf-8"))
                self.assertEqual(animation["animation"], item["animation"])
                self.assertEqual(len(animation["frames"]), item["frameCount"])
                self.assertEqual(animation["sortingLayer"], 18)
                self.assertEqual(animation["sortingOrder"], 3)
                self.assertTrue(Path(str(output / item["payloadPath"]) + ".meta").is_file())

            for item in environment["textures"]:
                copied = output / item["path"]
                source = ROOT / item["uri"].removeprefix("res://")
                self.assertEqual(copied.read_bytes(), source.read_bytes())
                self.assertEqual(hashlib.sha256(copied.read_bytes()).hexdigest(), item["sha256"])
                self.assertTrue(Path(str(copied) + ".meta").is_file())
            self.assertTrue(any(item["textureType"] == 1 for item in environment["textures"]))
            mask = environment["water"]["mask"]
            copied_mask = output / mask["path"]
            source_mask = ROOT / mask["source"].removeprefix("res://")
            self.assertEqual(copied_mask.read_bytes(), source_mask.read_bytes())
            self.assertEqual(hashlib.sha256(copied_mask.read_bytes()).hexdigest(), mask["sha256"])
            self.assertIn("sRGBTexture: 0", Path(str(copied_mask) + ".meta").read_text(encoding="utf-8"))

            manifest = json.loads((output / "rendering-source-manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(manifest["geometry"]["sha256"], result["geometry"]["sha256"])
            self.assertEqual(manifest["monsters"], result["monsters"])
            self.assertEqual(manifest["environment"], result["environment"])
            water_material = environment["water"]["material"]
            butterfly_material = environment["butterflies"]["material"]
            self.assertEqual(water_material["shader"], "Universal Render Pipeline/2D/Sprite-Lit-CustomWater2")
            self.assertEqual(butterfly_material["shader"], "Effect/Noise_Mask_Pro")
            self.assertEqual(butterfly_material["keywords"], ["USE_MAINTEX"])
            for material in (water_material, butterfly_material):
                reconstructed = ROOT / material["reconstructionPath"]
                self.assertEqual(hashlib.sha256(reconstructed.read_bytes()).hexdigest(),
                                 material["reconstructionSha256"])

    def test_refuses_to_replace_modified_geometry_payload(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "Map10"
            output.mkdir()
            (output / "map10.geom.bytes").write_bytes(b"user data")
            with self.assertRaisesRegex(ValueError, "Refusing to overwrite"):
                build(output)


if __name__ == "__main__":
    unittest.main()
