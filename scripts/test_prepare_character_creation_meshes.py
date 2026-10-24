import json
from pathlib import Path
import unittest
import zipfile

import prepare_character_creation_meshes as prepare

ROOT = Path(__file__).resolve().parents[1]


class CompatibleSkinTests(unittest.TestCase):
    def test_selects_only_exact_pose_attachments_and_weapon_fallbacks(self):
        rules = {"skins": {"1": {}, "2": {}, "3": {}, "4": {}}}
        catalog = {"parts": {"male": {"1": "hair", "2": "missing"}}}
        pose = {"parts": {"hair": "hair.json", "profession_weapon_3": "weapon.json"}}

        self.assertEqual({1, 3}, prepare.compatible_skin_ids("male", pose, rules, catalog))


class NativeMeshPackageTests(unittest.TestCase):
    def test_all_packaged_skins_have_per_skin_idle_and_run_entries(self):
        manifest_path = ROOT / "assets/reference/character-creation/native-mesh-source-manifest.json"
        package_path = ROOT / "unity/Assets/StreamingAssets/CharacterCreation/creation-meshes.zip"
        rules_path = ROOT / "unity/Assets/Official/CharacterCreation/wardrobe-rules.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        rules = json.loads(rules_path.read_text(encoding="utf-8"))

        with zipfile.ZipFile(package_path) as archive:
            for sex in ("male", "female"):
                for pose in ("home/idle_01", "map/run_01"):
                    ids = manifest["pose_skin_ids"][sex][pose]
                    expected = {rules["skins"][str(skin_id)]["SkinName"] for skin_id in ids}
                    index_name = f"{sex}-{pose.replace('/', '-')}-index.json"
                    index = json.loads(archive.read(index_name))
                    entries = {skin["name"]: skin["entries"] for skin in index["skins"]}

                    self.assertTrue(expected.issubset(entries), f"{sex}/{pose}")
                    for entry in entries.values():
                        for path in entry:
                            self.assertIn(path, manifest["files"])
                            self.assertIsNotNone(archive.getinfo(path))


if __name__ == "__main__":
    unittest.main()
