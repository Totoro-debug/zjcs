import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import prepare_battle_native_mesh_pack as mesh_pack


class PrepareBattleNativeMeshPackTests(unittest.TestCase):
    def test_normalizes_source_mesh_frames_for_unity_json_arrays(self):
        source = {
            "animation": "idle_01",
            "duration": 0.5,
            "fps": 12,
            "scale": 0.01,
            "frames": [[{
                "order": 2,
                "slot": "body",
                "page": "monster.png",
                "vertices": [0, 0, 1, 0, 0, 1],
                "uv": [0, 0, 1, 0, 0, 1],
                "triangles": [0, 1, 2],
                "color": [1, 1, 1, 1],
                "blend": 0,
            }]],
        }

        encoded, pages = mesh_pack.normalize_animation(source)
        normalized = json.loads(encoded.decode("utf-8"))

        self.assertEqual(pages, ["monster.png"])
        self.assertEqual(normalized["frames"][0]["batches"][0]["slot"], "body")
        self.assertEqual(normalized["frames"][0]["batches"][0]["triangles"], [0, 1, 2])

    def test_rejects_draw_indices_that_exceed_the_vertex_count(self):
        source = {
            "duration": 1,
            "fps": 12,
            "scale": 0.01,
            "frames": [[{
                "page": "monster.png",
                "vertices": [0, 0, 1, 0, 0, 1],
                "uv": [0, 0, 1, 0, 0, 1],
                "triangles": [0, 1, 3],
                "color": [1, 1, 1, 1],
                "blend": 0,
            }]],
        }

        with self.assertRaisesRegex(ValueError, "outside its vertex range"):
            mesh_pack.normalize_animation(source)

    def test_binds_source_atlas_pages_to_verified_hashed_sprite_files(self):
        source = {
            "animation": "idle_01",
            "duration": 0.5,
            "fps": 12,
            "scale": 0.01,
            "frames": [[{
                "page": "monster.png",
                "vertices": [0, 0, 1, 0, 0, 1],
                "uv": [0, 0, 1, 0, 0, 1],
                "triangles": [0, 1, 2],
                "color": [1, 1, 1, 1],
                "blend": 0,
            }]],
        }
        encoded, pages = mesh_pack.normalize_animation(source)
        page_record = {
            "archive_name": "monster-abc123.png",
            "sha256": "verified-hash",
        }

        bound, missing = mesh_pack.bind_animation_pages(
            encoded,
            pages,
            {"monster.png": "res://assets/official/sprites/monster-abc123.png"},
            {"res://assets/official/sprites/monster-abc123.png": page_record},
        )

        self.assertEqual(missing, [])
        self.assertEqual(json.loads(bound.decode("utf-8"))["frames"][0]["batches"][0]["page"],
                         "monster-abc123.png")


if __name__ == "__main__":
    unittest.main()
