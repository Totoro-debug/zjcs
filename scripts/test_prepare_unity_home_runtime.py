import hashlib
import json
import unittest

import prepare_unity_home_runtime as prepare


class FlattenHomeUiTests(unittest.TestCase):
    def test_fixed_official_panel_set_has_complete_flat_parent_graphs(self):
        ui = prepare.load_ui()

        self.assertEqual(81, len(ui["panels"]))
        panel_keys = {panel["key"] for panel in ui["panels"]}
        self.assertTrue({
            "astrological_entry", "astrological_props", "astrological_rule",
            "astrological_contract_tag", "astrological_rank",
            "character_prop_row",
            *("astrological_season_" + str(season) for season in range(1, 7)),
        }.issubset(panel_keys))
        astrological_art = ui["astrological_tag_art"]
        self.assertEqual(13, len(astrological_art))
        for setting in astrological_art:
            icon = setting["icon"]["file"]
            self.assertTrue((prepare.OFFICIAL / "sprites" / icon).is_file())
            self.assertTrue((prepare.SPRITE_DIR / icon).is_file())
            self.assertTrue((prepare.SPRITE_DIR / (icon + ".meta")).is_file())
        repair = next(panel for panel in ui["panels"]
                      if panel["key"] == "wish_FurnitureRepairDialog")
        self.assertEqual("/FurnitureRepairDialog", repair["root"]["path"])
        self.assertEqual(24, len(repair["nodes"]))

        for panel in ui["panels"]:
            paths = {panel["root"]["path"]}
            paths.update(node["path"] for node in panel["nodes"])
            self.assertEqual(1 + len(panel["nodes"]), len(paths), panel["key"])
            for node in panel["nodes"]:
                self.assertIn(node["parent_path"], paths, panel["key"])

    def test_direct_official_prefab_root_is_preserved(self):
        source = {
            "name": "FurnitureRepairDialog",
            "path": "/FurnitureRepairDialog",
            "active": True,
            "children": [{
                "name": "RepairButton",
                "path": "/FurnitureRepairDialog/RepairButton",
                "button": True,
                "children": [],
            }],
        }

        trimmed = prepare.trim_prefab(source)
        flattened = prepare.flatten_panel({"key": "wish_FurnitureRepairDialog", **trimmed})

        self.assertEqual(flattened["root"]["path"], "/FurnitureRepairDialog")
        self.assertEqual(flattened["nodes"][0]["path"], "/FurnitureRepairDialog/RepairButton")

    def test_flat_panel_retains_every_node_and_parent_edge(self):
        source = {
            "key": "home",
            "root": {
                "name": "HomeUI",
                "path": "/HomeUI",
                "children": [{
                    "name": "Content",
                    "path": "/HomeUI/Content",
                    "children": [{
                        "name": "Button",
                        "path": "/HomeUI/Content/Button",
                        "button": True,
                        "children": [{
                            "name": "Label",
                            "path": "/HomeUI/Content/Button/Label",
                            "text": {"content": "Home"},
                            "children": [],
                        }],
                    }],
                }],
            },
        }

        flattened = prepare.flatten_panel(source)

        self.assertEqual(flattened["root"]["path"], "/HomeUI")
        self.assertNotIn("children", flattened["root"])
        self.assertEqual(
            [(node["path"], node["parent_path"]) for node in flattened["nodes"]],
            [
                ("/HomeUI/Content", "/HomeUI"),
                ("/HomeUI/Content/Button", "/HomeUI/Content"),
                ("/HomeUI/Content/Button/Label", "/HomeUI/Content/Button"),
            ],
        )
        self.assertTrue(all("children" not in node for node in flattened["nodes"]))


class HomeTutorialRewardAssetTests(unittest.TestCase):
    def test_fixed_reward_items_have_required_official_home_icons(self):
        reward_item_ids = prepare.home_tutorial_fixed_reward_item_ids()
        self.assertEqual({1, 8050, 8101}, reward_item_ids)

        rules = prepare.load_rules()
        staged_ids = prepare.icon_item_ids(rules, reward_item_ids)
        items = {item["id"]: item for item in rules["items"]}
        self.assertTrue(reward_item_ids.issubset(staged_ids))
        for item_id in reward_item_ids:
            icon = items[item_id]["icon"]
            self.assertTrue(icon, f"item {item_id} has no official icon name")
            self.assertTrue((prepare.OFFICIAL / "sprites" / icon).is_file(),
                            f"item {item_id} icon is missing: {icon}")


class HomePlayerRuntimeTests(unittest.TestCase):
    def test_runtime_uses_official_transform_idle_and_sorting_preset(self):
        runtime = prepare.load_actors()["player"]["runtime"]

        self.assertEqual([4.5, -5.440000057220459, 0.0], runtime["position"])
        self.assertAlmostEqual(0.4, runtime["scale"][0])
        self.assertAlmostEqual(0.4, runtime["scale"][1])
        self.assertAlmostEqual(1.0, runtime["scale"][2])
        self.assertEqual("Mansion_Front01", runtime["sorting_layer_name"])
        self.assertEqual(14, runtime["sorting_layer"])
        self.assertEqual(10, runtime["sorting_order"])
        self.assertEqual("home/idle_01", runtime["animation"])

    def test_runtime_payload_records_exact_transform_and_sorting_sources(self):
        runtime_path = prepare.OUTPUT / "player-runtime.json"
        manifest_path = prepare.OUTPUT / "source-manifest.json"
        runtime = json.loads(runtime_path.read_text(encoding="utf-8"))
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        self.assertEqual(prepare.load_actors()["player"]["runtime"], runtime)

        sources = manifest["inputs"]["homePlayerRuntime"]
        for path_key, hash_key in (("transformSource", "transformSourceSha256"),
                                   ("sortingLayerSource", "sortingLayerSourceSha256")):
            source = prepare.ROOT / sources[path_key]
            self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(), sources[hash_key])


class HomeMeteorRuntimeTests(unittest.TestCase):
    def test_official_templates_keep_particle_data_materials_and_transforms(self):
        environment = prepare.load_environment()
        runtime = environment["meteorRuntime"]

        self.assertEqual(1, runtime["format"])
        self.assertEqual("854_156383", runtime["sourceManifest"])
        self.assertEqual(1, len(runtime["templates"]))
        template = runtime["templates"][0]
        self.assertEqual("d997ed1d45b207446aac55e6a6c4a86f", template["assetGuid"])
        self.assertEqual(1, len(template["systems"]))
        particle = template["systems"][0]
        self.assertEqual("/Meteor/Eff_Ui_Main_Star03/liuxinyu (1)", particle["path"])
        self.assertEqual(16, len(particle["matrix"]))
        self.assertAlmostEqual(3.45, particle["matrix"][3], places=5)
        self.assertAlmostEqual(6.34, particle["matrix"][7], places=5)
        self.assertAlmostEqual(-1.0, particle["matrix"][11], places=5)
        self.assertEqual(10.0, particle["duration"])
        self.assertEqual(1, particle["max_particles"])
        self.assertEqual([], particle.get("unconverted_modules", []))
        self.assertTrue(particle["emission_enabled"])
        self.assertEqual(1, particle["emitter_velocity_mode"])
        self.assertFalse(particle["use_unscaled_time"])

        self.assertEqual("Fx_GuangQuan_0006", particle["material"]["name"])
        self.assertEqual("Fx_TuoWei_16 5", particle["modules"]["TrailModule"]["material"]["name"])
        staged_files = {binding["file"] for binding in particle["material"]["textures"]}
        staged_files.update(binding["file"] for binding in particle["modules"]["TrailModule"]["material"]["textures"])
        self.assertEqual({
            "res://assets/official/home-player/sprites/Fx_GuangQuan_0006-99d1c1cb82.png",
            "res://assets/official/home-player/sprites/Fx_RaoDon_0009-85ff8dffb0.png",
            "res://assets/official/home-player/sprites/Fx_TuoWei_16_2F-29efafea3d.png",
            "res://assets/official/home-player/sprites/Fx_WenLi_118-7bec945cc1.png",
        }, staged_files)
        self.assertTrue(particle["modules"]["SizeModule"]["enabled"])
        self.assertTrue(particle["modules"]["VelocityModule"]["enabled"])
        self.assertTrue(particle["modules"]["ClampVelocityModule"]["enabled"])
        self.assertTrue(particle["modules"]["ColorModule"]["enabled"])
        self.assertTrue(particle["modules"]["TrailModule"]["enabled"])
        self.assertFalse(particle["modules"]["VelocityModule"]["inWorldSpace"])
        clamp = particle["modules"]["ClampVelocityModule"]
        self.assertTrue(clamp["multiplyDragByParticleSize"])
        self.assertTrue(clamp["multiplyDragByParticleVelocity"])

    def test_environment_source_manifest_records_particle_and_counterpart_inputs(self):
        path = prepare.OUTPUT / "source-manifest.json"
        manifest = json.loads(path.read_text(encoding="utf-8"))
        source = manifest["inputs"]["homeMeteorRuntime"]

        source_json = prepare.ROOT / source["source"]
        source_manifest = prepare.ROOT / source["assetManifest"]
        self.assertEqual(hashlib.sha256(source_json.read_bytes()).hexdigest(), source["sourceSha256"])
        self.assertEqual(hashlib.sha256(source_manifest.read_bytes()).hexdigest(), source["assetManifestSha256"])
        self.assertEqual("defaultpackage_assets_prefabs_home_dynamic_nature.bundle",
                         source["particleSystems"][0]["bundle"])
        self.assertEqual("a276e6bb01f9c444408233f254cc1a57c1824971cfa7908998a6a1cfe1f2c73f",
                         source["particleSystems"][0]["sha256"])
        counterpart = source["counterpartDependencies"]["bundles"][0]
        self.assertEqual("864_156496", counterpart["counterpart_manifest"])
        self.assertEqual("a07b3ea87359773c6a550bd3550c1424a03efee270abb2a3080dba3e0a18baaa",
                         counterpart["counterpart_sha256"])


if __name__ == "__main__":
    unittest.main()
