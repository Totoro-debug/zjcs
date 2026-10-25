import hashlib
import json
import unittest
from pathlib import Path

from prepare_unity_battle_assets import build_unit_bars_runtime


ROOT = Path(__file__).resolve().parents[1]
UNITY = ROOT / "unity"


class BattleUnitBarRuntimeTests(unittest.TestCase):
    def test_fixed_unit_bar_layouts_keep_pinned_entities_and_official_sprites(self):
        source_path = ROOT / "assets/official/battle-hud/unit-bars.json"
        source = json.loads(source_path.read_text(encoding="utf-8"))
        source_manifest = json.loads(
            (ROOT / "assets/reference/battle-hud/source-manifest.json").read_text(encoding="utf-8")
        )
        source_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()
        self.assertEqual(source_manifest["manifest"], "854_156383")
        self.assertEqual(source_manifest["output_sha256"]["unit-bars.json"], source_hash)

        runtime = build_unit_bars_runtime(source)
        self.assertEqual(runtime["manifest"], "854_156383")
        self.assertEqual(len(runtime["entities"]), 503)
        self.assertEqual(sum(entry["statusLayout"] is not None for entry in runtime["entities"]), 424)
        self.assertEqual(sum(any(part["role"] == "Resilience" for part in entry["parts"])
                             for entry in runtime["entities"]), 402)

        sprite_files = {part["spriteFile"] for entry in runtime["entities"] for part in entry["parts"]}
        self.assertEqual(sprite_files, {
            "ImgBg_HpBar-8bcbf4b323.png",
            "ImgLoad_HpBar-f915a5bb0c.png",
        })
        status_layouts = {tuple(entry["statusLayout"]["cellSize"])
                          for entry in runtime["entities"] if entry["statusLayout"] is not None}
        self.assertEqual(status_layouts, {(0.18000000715255737, 0.18000000715255737)})

        sample = next(entry for entry in runtime["entities"] if entry["entityId"] == 3100)
        hp_bar = next(part for part in sample["parts"] if part["role"] == "HpSlider")
        self.assertEqual(hp_bar["spriteFile"], "ImgLoad_HpBar-f915a5bb0c.png")
        self.assertAlmostEqual(hp_bar["position"][1], -0.560400004273653)
        self.assertEqual((sample["statusLayout"]["columns"], sample["statusLayout"]["rows"]), (5, 1))

        runtime["sourceSha256"] = source_hash
        runtime_path = UNITY / "Assets/Game/Resources/Combat/unit-bars-runtime.json"
        runtime_manifest_path = UNITY / "Assets/Game/Resources/Combat/unit-bars-runtime-source-manifest.json"
        runtime_manifest = json.loads(runtime_manifest_path.read_text(encoding="utf-8"))
        runtime_bytes = runtime_path.read_bytes()
        self.assertEqual(hashlib.sha256(runtime_bytes).hexdigest(), runtime_manifest["output_sha256"])
        self.assertEqual(runtime_manifest["manifest"], "854_156383")
        self.assertEqual(runtime_manifest["source_path"], "Assets/Official/Combat/HUD/unit-bars.json")
        self.assertEqual(runtime_manifest["source_sha256"], source_hash)
        self.assertEqual(runtime_manifest["output_path"], "Assets/Game/Resources/Combat/unit-bars-runtime.json")
        self.assertEqual(json.loads(runtime_bytes.decode("utf-8")), runtime)

        official_input = UNITY / "Assets/Official/Combat/HUD/unit-bars.json"
        self.assertEqual(hashlib.sha256(official_input.read_bytes()).hexdigest(), source_hash)
        catalog = (UNITY / "Assets/Game/Resources/Combat/OfficialBattleHudCatalog.asset").read_text(encoding="utf-8")
        for sprite_file in sprite_files:
            self.assertTrue((UNITY / "Assets/Official/Combat/HUD/Sprites" / sprite_file).is_file())
            self.assertIn(sprite_file, catalog)


if __name__ == "__main__":
    unittest.main()
