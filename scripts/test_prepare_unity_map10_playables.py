import json
from pathlib import Path
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from prepare_unity_map10_playables import build


class PrepareUnityMap10PlayablesTests(unittest.TestCase):
    def test_exports_pinned_story_particle_playables_and_dependencies(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "Map10"
            result = build(output, output / "map10-playables-source-manifest.json")

            self.assertEqual([item["particleSystemCount"] for item in result["playables"]], [10, 11, 8])
            self.assertEqual(len(result["textures"]), 26)
            self.assertEqual(len(result["meshes"]), 2)
            self.assertEqual(sum(len(item["materialIds"]) for item in result["playables"]), 26)

            counterpart_ids = set()
            for index, item in enumerate(result["playables"]):
                payload_path = output / item["payloadPath"]
                payload = json.loads(payload_path.read_text(encoding="utf-8"))
                self.assertEqual(payload["assetGuid"], item["assetGuid"])
                self.assertEqual(len(payload["systems"]), item["particleSystemCount"])
                self.assertTrue(payload["meshes"])
                self.assertTrue(Path(str(payload_path) + ".meta").is_file())
                mesh_particles = [particle for particle in payload["systems"] if particle["render_mode"] == 4]
                self.assertEqual(len(mesh_particles), [2, 2, 1][index])
                self.assertTrue(all(particle["meshResources"] for particle in mesh_particles))
                for particle in payload["systems"]:
                    self.assertEqual(len(particle["matrix"]), 16)
                    self.assertFalse(particle["unconverted_modules"])
                for mesh in payload["meshes"]:
                    self.assertTrue(mesh["vertices"])
                    self.assertTrue(mesh["triangles"])
                for material in payload["materials"]:
                    self.assertTrue(material["textures"])
                    if material["source"].get("origin") == "official_cdn_counterpart":
                        counterpart_ids.add(material["id"])
                    for texture in material["textures"]:
                        texture_path = output / "Resources/Map10/Playables/Textures" \
                            / (Path(texture["resource"]).name + ".png")
                        self.assertTrue(texture_path.is_file())
                        self.assertTrue(Path(str(texture_path) + ".meta").is_file())

            self.assertEqual(len(counterpart_ids), 1)
            self.assertTrue(all(digest for digest in result["outputs"].values()))


if __name__ == "__main__":
    unittest.main()
