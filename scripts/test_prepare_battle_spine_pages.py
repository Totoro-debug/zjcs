import json
import io
import sys
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))

import prepare_battle_spine_pages as pages


class PrepareBattleSpinePagesTests(unittest.TestCase):
    def test_all_generic_models_resolve_to_indexed_battle_skeletons(self):
        models = json.loads((ROOT / "assets/official/battle-presentation/models.json")
                            .read_text(encoding="utf-8"))
        catalog = json.loads(pages.SPINE_CATALOG.read_text(encoding="utf-8"))["skeletons"]

        selected, failures = pages.model_stems(catalog, list(models))

        resolved_keys = [key for keys in selected.values() for key in keys]
        self.assertEqual(len(models), 409)
        self.assertEqual(len(resolved_keys), 407)
        self.assertEqual(len(failures), 2)
        self.assertEqual(len(set(resolved_keys)), len(resolved_keys))
        self.assertTrue(all(Path(catalog[key]["file"]).stem == stem
                            for stem, keys in selected.items() for key in keys))

    def test_wrong_sized_resource_falls_back_to_the_verified_page_object(self):
        from PIL import Image

        def png(size):
            stream = io.BytesIO()
            Image.new("RGBA", size).save(stream, format="PNG")
            return stream.getvalue()

        source = {"bundle": "sprite-bundle", "path_id": 1, "sha256": ""}
        fallback_wrong = {"bundle": "appearance-bundle", "path_id": 2, "sha256": ""}
        fallback = {"bundle": "appearance-bundle", "path_id": 3, "sha256": ""}
        with patch.object(pages, "find_page_texture", return_value=[fallback_wrong, fallback]), \
                patch.object(pages, "decode_texture", side_effect=lambda _apk, bundle, *_args:
                             png((24, 24) if bundle == "sprite-bundle" or _args[0] == 2 else (2, 1))):
            payload, selected = pages.decode_page(None, {"page": source}, "page", "page",
                                                   (2, 1), {})

        self.assertIs(selected, fallback)
        self.assertEqual(len(payload), len(png((2, 1))))

    def test_same_sized_ambiguous_atlas_pages_with_different_hashes_are_rejected(self):
        from PIL import Image

        def png(color):
            stream = io.BytesIO()
            Image.new("RGBA", (2, 1), color).save(stream, format="PNG")
            return stream.getvalue()

        candidates = [
            {"bundle": "first", "path_id": 1, "sha256": ""},
            {"bundle": "second", "path_id": 2, "sha256": ""},
        ]
        with patch.object(pages, "find_page_texture", return_value=candidates), \
                patch.object(pages, "decode_texture", side_effect=lambda _apk, bundle, *_args:
                             png("red" if bundle == "first" else "blue")):
            with self.assertRaisesRegex(ValueError, "different data"):
                pages.decode_page(None, {}, "page", "page", (2, 1), {})

    def test_verified_resource_page_wins_over_same_sized_fallback(self):
        from PIL import Image

        def png(color):
            stream = io.BytesIO()
            Image.new("RGBA", (2, 1), color).save(stream, format="PNG")
            return stream.getvalue()

        source = {"bundle": "source", "path_id": 1, "sha256": "verified"}
        with patch.object(pages, "find_page_texture", side_effect=AssertionError("fallback scan")), \
                patch.object(pages, "decode_texture", side_effect=lambda _apk, bundle, *_args:
                             png("red")):
            payload, selected = pages.decode_page(None, {"page": source}, "page", "page",
                                                   (2, 1), {})

        self.assertIs(selected, source)
        self.assertEqual(payload, png("red"))

    def test_page_with_no_matching_source_reports_each_candidate_size(self):
        from PIL import Image

        stream = io.BytesIO()
        Image.new("RGBA", (24, 24)).save(stream, format="PNG")
        source = {"bundle": "sprite-bundle", "path_id": 1, "sha256": ""}
        with patch.object(pages, "find_page_texture", return_value=source), \
                patch.object(pages, "decode_texture", return_value=stream.getvalue()):
            with self.assertRaisesRegex(ValueError, "24x24, atlas requires 2x1"):
                pages.decode_page(None, {"page": source}, "page", "page", (2, 1), {})

    def test_existing_material_is_kept_only_for_the_identical_page_content(self):
        with tempfile.TemporaryDirectory() as temporary:
            page = Path(temporary) / "page.png"
            material = Path(temporary) / "page.mat"
            page.write_bytes(b"official-page")
            page.with_suffix(".png.meta").write_text("guid: " + "a" * 32 + "\n", encoding="utf-8")
            material.write_text("m_Texture: {fileID: 2800000, guid: " + "a" * 32 + ", type: 3}\n",
                                encoding="utf-8")

            self.assertTrue(pages.material_uses_identical_page(
                material, page, pages.sha256(page.read_bytes())))
            self.assertFalse(pages.material_uses_identical_page(material, page, "0" * 64))

    def test_material_rebind_only_replaces_the_expected_stale_texture(self):
        with tempfile.TemporaryDirectory() as temporary:
            material = Path(temporary) / "page.mat"
            old_guid = "a" * 32
            new_guid = "b" * 32
            material.write_text(
                "    - _MainTex:\n        m_Texture: {fileID: 2800000, guid: "
                + old_guid + ", type: 3}\n    m_Ints:\n", encoding="utf-8")

            rebound = pages.bind_material(material, new_guid, old_guid)
            self.assertIn("guid: " + new_guid, rebound)
            with self.assertRaisesRegex(ValueError, "different texture"):
                pages.bind_material(material, new_guid)


if __name__ == "__main__":
    unittest.main()
