import json
from pathlib import Path
import unittest

import prepare_unity_battle_assets as battle_assets

ROOT = Path(__file__).resolve().parents[1]


class BattleHudAnimationTests(unittest.TestCase):
    def test_node_preserves_animation_autoplay_flag(self):
        node = {"animation_play_automatically": True, "children": []}

        battle_assets.normalize_hud_node(node)

        self.assertTrue(node["animation_play_automatically"])

    def test_animation_keys_are_json_utility_records(self):
        normalized = battle_assets.normalize_hud_animation({
            "name": "OfficialClip",
            "owner": "/Panel/Animator",
            "duration": 0.5,
            "loop": False,
            "source": {"bundle": "source-only"},
            "tracks": [{
                "path": "/Panel/Label",
                "property": "m_Alpha",
                "keys": [[0.0, 0.0, 0.0, 0.0], [0.5, 1.0, None, None]],
            }],
        })

        self.assertEqual(normalized["name"], "OfficialClip")
        self.assertEqual(normalized["duration"], 0.5)
        self.assertNotIn("source", normalized)
        self.assertEqual(normalized["tracks"][0]["keys"], [
            {"time": 0.0, "value": 0.0, "in_tangent": 0.0, "out_tangent": 0.0},
            {"time": 0.5, "value": 1.0, "in_tangent": 0.0, "out_tangent": 0.0},
        ])

    def test_invalid_animation_key_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "invalid key"):
            battle_assets.normalize_hud_animation({
                "tracks": [{"keys": [[0.0]]}],
            })

    def test_damage_float_metadata_preserves_official_bindings(self):
        binding = battle_assets.build_hud_damage_float_binding({
            "root": {"original_bindings": {"CharacterDamageUI": {
                "mType": 8,
                "mShowTime": 1.0,
                "mNumberUi": None,
                "mTipUi": "/Float/Tip",
                "mBreakResilienceGo": "/Float/Break",
                "mTrueDamageText": "/Float/True",
                "mHeight": 3.0,
                "mSceneOffset": {"x": 1.0, "y": 2.0, "z": 3.0},
            }}}
        })

        self.assertEqual(binding, {
            "type": 8,
            "showTime": 1.0,
            "numberPath": "",
            "tipPath": "/Float/Tip",
            "breakResiliencePath": "/Float/Break",
            "trueDamagePath": "/Float/True",
            "height": 3.0,
            "sceneOffset": [1.0, 2.0, 3.0],
        })

    def test_official_float_markers_receive_localized_text(self):
        root = {
            "path": "/Float",
            "children": [
                {"path": "/Float/Break", "children": [
                    {"path": "/Float/Break/Text", "localization_key": "ui_BreakResilience"},
                ]},
                {"path": "/Float/True", "localization_key": "ui_TrueDamage"},
            ],
        }
        battle_assets.localize_hud_node(root, {
            "ui_BreakResilience": "\u7834\u97e7",
            "ui_TrueDamage": "\u7834\u706d\u4f24\u5bb3",
        }.get)

        self.assertEqual(root["children"][0]["children"][0]["text_value"], "\u7834\u97e7")
        self.assertEqual(root["children"][1]["text_value"], "\u7834\u706d\u4f24\u5bb3")

    def test_non_damage_prefab_has_no_damage_binding(self):
        self.assertIsNone(battle_assets.build_hud_damage_float_binding({"root": {}}))

    def test_boss_bar_keeps_official_sprite_variants_and_timing(self):
        binding = battle_assets.build_boss_hp_bar_binding({
            "root": {"original_bindings": {"FightBossHpAndShieldBar": {
                "HpSprites": [{"file": "res://ui/HpA.png"}, {"file": "res://ui/HpB.png"}],
                "ShieldSprite": {"file": "res://ui/Shield.png"},
                "ShieldColors": [
                    {"r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0},
                    {"r": 0.8, "g": 0.8, "b": 0.8, "a": 1.0},
                ],
                "EmptyBarSprite": {"file": "res://ui/Empty.png"},
                "MoveSpeed": 0.1,
            }}}
        })

        self.assertEqual(binding, {
            "hpSpriteFiles": ["HpA.png", "HpB.png"],
            "shieldSpriteFile": "Shield.png",
            "shieldColors": [
                {"rgba": [1.0, 1.0, 1.0, 1.0]},
                {"rgba": [0.8, 0.8, 0.8, 1.0]},
            ],
            "emptyBarSpriteFile": "Empty.png",
            "moveSpeed": 0.1,
        })

    def test_non_boss_prefab_has_no_boss_bar_binding(self):
        self.assertIsNone(battle_assets.build_boss_hp_bar_binding({"root": {}}))

    def test_pinned_boss_prefab_has_five_hp_layers_and_two_shield_colors(self):
        panels = json.loads((ROOT / "assets/official/battle-hud/panels.json").read_text(encoding="utf-8"))
        binding = battle_assets.build_boss_hp_bar_binding(panels["prefabs"]["boss_item"])

        self.assertEqual(binding["hpSpriteFiles"], [
            "ImgBossHp-96e18394dc.png",
            "ImgBossHp2-950fcf6321.png",
            "ImgBossHp3-fd9356da83.png",
            "ImgBossHp4-9ac587e908.png",
            "ImgBossHp5-f16f0b06fd.png",
        ])
        self.assertEqual(binding["shieldSpriteFile"], "ImgBossShield-7221080647.png")
        self.assertEqual(len(binding["shieldColors"]), 2)
        self.assertAlmostEqual(binding["moveSpeed"], 0.1)


if __name__ == "__main__":
    unittest.main()
