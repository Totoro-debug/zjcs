import tempfile
import unittest
from pathlib import Path

from prepare_unity_map15_native_assets import build_index, close, interaction_roots


class Map15NativeAssetClosureTests(unittest.TestCase):
    def test_collects_map15_world_map_and_interaction_roots(self):
        with tempfile.TemporaryDirectory() as temporary:
            assets = Path(temporary) / "Assets"
            relative = (
                "Prefabs/UI/WorldMap/WorldRuinDialog.prefab",
                "Prefabs/UI/ConfirmDialog/ConfirmDialog.prefab",
                "Prefabs/UI/MiniGame/MiniGamePanel.prefab",
                "Prefabs/Playables/EC/ActionWorld/puzzle.prefab",
                "Scenes/map_world.unity",
            )
            for name in relative:
                path = assets / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("%YAML 1.1\n", encoding="utf-8")

            self.assertEqual({assets / name for name in relative}, set(interaction_roots(assets)))

    def test_follows_exported_guid_references_and_reports_missing_scripts(self):
        with tempfile.TemporaryDirectory() as temporary:
            assets = Path(temporary) / "Assets"
            root = assets / "Prefabs/Map/Map_Tower15_Test/Root.prefab"
            material = assets / "Materials/Tower15.mat"
            root.parent.mkdir(parents=True)
            material.parent.mkdir(parents=True)
            root.write_text(
                "m_Material: {fileID: 2100000, guid: " + "1" * 32 + ", type: 2}\n"
                "m_Script: {fileID: 11500000, guid: " + "2" * 32 + ", type: 3}\n",
                encoding="utf-8",
            )
            material.write_text("Material: {}\n", encoding="utf-8")
            Path(str(root) + ".meta").write_text("guid: " + "3" * 32 + "\n", encoding="utf-8")
            Path(str(material) + ".meta").write_text("guid: " + "1" * 32 + "\n", encoding="utf-8")

            index = build_index(assets)
            files, unresolved = close(assets, [root], index)

            self.assertEqual({root, material}, set(files))
            self.assertEqual([{"guid": "2" * 32, "referenced_by": ["Prefabs/Map/Map_Tower15_Test/Root.prefab"]}], unresolved)


if __name__ == "__main__":
    unittest.main()
