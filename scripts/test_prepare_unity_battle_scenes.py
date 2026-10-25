import json
import tempfile
import unittest
from pathlib import Path, PurePosixPath

import prepare_unity_battle_scenes as battle_scenes


class PrepareUnityBattleScenesTests(unittest.TestCase):
    def test_all_official_stages_and_day_night_geometry_are_available(self):
        inventory = battle_scenes.load_source_inventory(battle_scenes.ROOT)
        self.assertEqual(len(inventory["game_scene_names"]), 256)
        self.assertEqual(len(set(inventory["game_scene_names"].values())), 229)
        self.assertEqual(inventory["game_scene_names"][905], "game_900")

        textures = set()
        base_count = 0
        environment_paths = set()
        empty_environment_paths = set()
        for scene_name, scene in inventory["scene_catalog"].items():
            base_path = battle_scenes.ROOT / "assets/official/battle-scenes" / f"{scene_name}-base.geom"
            batch_count, uris = battle_scenes.geometry_textures(base_path.read_bytes())
            self.assertGreater(batch_count, 0)
            base_count += 1
            textures.update(uris)
            for period in ("day", "night"):
                for metadata_uri in scene[period]:
                    metadata_path = battle_scenes.ROOT / metadata_uri.removeprefix("res://")
                    metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
                    geometry_path = battle_scenes.ROOT / metadata["geometry"].removeprefix("res://")
                    geometry = geometry_path.read_bytes()
                    if len(geometry) == 12 and geometry[:8] == b"ZJMAP001":
                        self.assertEqual(int.from_bytes(geometry[8:12], "little"), 0)
                        empty_environment_paths.add(geometry_path)
                        continue
                    _, uris = battle_scenes.geometry_textures(geometry)
                    textures.update(uris)
                    environment_paths.add(geometry_path)

        self.assertEqual((base_count, len(environment_paths), len(empty_environment_paths)), (229, 50, 398))
        self.assertEqual(len(textures), 6376)
        self.assertTrue(all((battle_scenes.ROOT / uri.removeprefix("res://")).is_file()
                            for uri in textures))

    def test_restore_copies_hashed_geometry_textures_and_resource_bindings(self):
        with tempfile.TemporaryDirectory() as temporary:
            target_root = Path(temporary) / "unity"
            result = battle_scenes.restore(battle_scenes.ROOT, target_root, game_ids=(1000, 1004, 1008))
            manifest_path = target_root / battle_scenes.MANIFEST_PATH
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            catalog_path = target_root / battle_scenes.CATALOG_PATH
            catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
            self.assertEqual([(scene["game_id"], scene["scene_name"], scene["batch_count"], scene["texture_count"])
                              for scene in result["scenes"]],
                             [(1000, "game_1000", 228, 29),
                              (1004, "game_1004", 296, 49),
                              (1008, "game_1008", 228, 29)])
            self.assertEqual(result["unique_scenes"], 3)
            self.assertEqual([scene["game_ids"] for scene in manifest["scenes"]], [[1000], [1004], [1008]])
            self.assertEqual([scene["gameId"] for scene in catalog["scenes"]], [1000, 1004, 1008])
            expected_texture_counts = {1000: 29, 1004: 49, 1008: 29}
            expected_scene_names = {1000: "game_1000", 1004: "game_1004", 1008: "game_1008"}
            for scene in catalog["scenes"]:
                self.assertEqual(len(scene["textures"]), expected_texture_counts[scene["gameId"]])
                self.assertEqual(scene["sceneName"], expected_scene_names[scene["gameId"]])
                self.assertEqual(scene["rootOffset"], [5000.0, 0.0, 0.0])
                self.assertEqual(scene["cameraOrthographicSize"], 5.0)
                self.assertEqual(scene["sourceParticleCount"], 0)
                self.assertEqual(scene["dayLayers"], [])
                self.assertEqual(len(scene["nightLayers"]), 1)

            expected_uris = set()
            for game_id in (1000, 1004, 1008):
                expected_uris.update(battle_scenes.scene_metadata(battle_scenes.ROOT, game_id)["texture_uris"])
            geometry_paths = {
                f"{battle_scenes.RESOURCE_ROOT.as_posix()}/{Path(scene['scene'] + '.bytes').name}"
                for scene in manifest["scenes"]
            }
            geometry_paths.update(
                f"{battle_scenes.RESOURCE_ROOT.as_posix()}/{Path(layer['geometryResource']).name}.bytes"
                for scene in catalog["scenes"]
                for layer in scene["dayLayers"] + scene["nightLayers"]
            )
            texture_paths = {
                f"{battle_scenes.RESOURCE_ROOT.as_posix()}/Textures/"
                f"{PurePosixPath(uri.removeprefix('res://')).name}"
                for uri in expected_uris
            }
            asset_paths = {entry["path"] for entry in manifest["assets"]}
            self.assertEqual(asset_paths, geometry_paths | texture_paths | {battle_scenes.CATALOG_PATH.as_posix()})
            for entry in manifest["assets"]:
                asset = target_root / Path(*Path(entry["path"]).parts)
                self.assertTrue(asset.is_file())
                self.assertEqual(battle_scenes.sha256(asset), entry["sha256"])
                self.assertTrue(asset.with_name(asset.name + ".meta").is_file())
            for directory in (battle_scenes.UNITY_SCENE_ROOT, battle_scenes.UNITY_SCENE_ROOT / "Resources",
                              battle_scenes.RESOURCE_ROOT, battle_scenes.RESOURCE_ROOT / "Textures"):
                folder = target_root.joinpath(*directory.parts)
                self.assertTrue(folder.with_name(folder.name + ".meta").is_file())
                self.assertIn(battle_scenes.stable_guid(directory),
                              folder.with_name(folder.name + ".meta").read_text(encoding="utf-8"))

    def test_restore_refuses_to_replace_changed_native_asset(self):
        with tempfile.TemporaryDirectory() as temporary:
            target_root = Path(temporary) / "unity"
            battle_scenes.restore(battle_scenes.ROOT, target_root, game_ids=(1000,))
            geometry = target_root.joinpath(*battle_scenes.RESOURCE_ROOT.parts, "game_1000-base.bytes")
            geometry.write_bytes(b"modified")
            with self.assertRaisesRegex(ValueError, "differs from its manifest"):
                battle_scenes.restore(battle_scenes.ROOT, target_root, game_ids=(1000,))


if __name__ == "__main__":
    unittest.main()
