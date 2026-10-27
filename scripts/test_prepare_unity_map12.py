"""Source-fidelity regressions for the mountain conversion."""
import copy
import json
from pathlib import Path
import unittest

from prepare_unity_map10_battle_data import map11_component_stage_bindings
from prepare_unity_map11_runtime_data import build_story_node
from prepare_unity_map12_runtime_data import parse_terminal
from prepare_unity_map12_sources import verify_legacy_equivalence
from prepare_unity_map11_story_spatial import foreign_map_subgraphs


ROOT = Path(__file__).resolve().parents[1]


def load(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8"))


class MountainSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        base = "unity/Assets/Official/World/Map/12/Source/"
        cls.source = {name: load(base + name + ".json") for name in
                      ("navigation", "placement", "entities", "templates", "tables", "stories")}

    def test_reuse_requires_selected_source_equivalence(self):
        values = self.source
        verified = verify_legacy_equivalence(values["navigation"], values["placement"],
            values["entities"], values["templates"], values["tables"], values["stories"])
        self.assertTrue(all(verified["checks"].values()))
        changed = copy.deepcopy(values["stories"])
        graph = next(iter(changed.values()))
        node = next(n for n in graph["nodes"].values()
                    if any(p["connections"] for p in n["ports"].values()))
        port = next(p for p in node["ports"].values() if p["connections"])
        port["connections"][0]["node"] = "999999"
        with self.assertRaisesRegex(ValueError, "storyNodesAndPorts"):
            verify_legacy_equivalence(values["navigation"], values["placement"],
                values["entities"], values["templates"], values["tables"], changed)

    def test_story_created_npcs_have_packaged_display_models(self):
        from prepare_unity_map10_entities import select_display_path
        stories = load("unity/Assets/Official/World/Map/12/Resources/Map12/stories.json")
        source_models = load("assets/official/map-models/catalog.json")["models"]
        packaged = load("unity/Assets/Official/World/Map/12/Resources/Map12/Entities/catalog.json")
        bound = {row["modelPath"] for row in packaged["models"]}
        created = {node["entityClassId"] for story in stories["stories"] + stories["subgraphs"]
                   for node in story["nodes"] if node["type"] in
                   ("AddMapExclusiveNpcAroundPlayerNode", "AddMapExclusiveNpcAtPosNode")
                   and node["entityClassId"] > 0}
        self.assertIn(8402, created)
        for identity in created:
            with self.subTest(entityClassId=identity):
                self.assertIn(select_display_path(identity, list(source_models), 12), bound)

    def test_node_countdown_stops_indefinite_npc_idle(self):
        graph = self.source["stories"]["FirstMeetMete"]["nodes"]
        node = graph["20"]
        self.assertEqual(node["PlayableDura"], 1)
        playables = load("assets/official/map-playables.json")["scenes"]
        guid = node["PlayablesProxy"][0]["mAssetGuid"]
        self.assertEqual(playables[guid]["duration"], -1)
        exported = build_story_node("FirstMeetMete", "20", node, playables, set())
        self.assertTrue(exported["waitPlayableStop"])
        self.assertEqual(exported["playableDuration"], 1)
        self.assertEqual(json.loads(exported["sourceJson"]), node)

    def test_unused_lo_battle_data_does_not_create_a_battle(self):
        definition = load("unity/Assets/Official/World/Map/12/map12.json")
        bindings = map11_component_stage_bindings(definition, 12)
        self.assertNotIn(202, {r["PlacementId"] for r in bindings})
        original = self.source["entities"]["202"]["LoStageBehStageComponent"]
        self.assertEqual(original["StageIds"], [2000])
        raw = next(r for r in definition["EntityComponents"] if r["EntityId"] == 202)
        self.assertEqual(json.loads(raw["Json"])["LoStageBehStageComponent"], original)
        stages = load("unity/Assets/Official/World/Map/12/map12-battles.json")["stages"]
        self.assertFalse(any(r["Id"] == 2000 for r in stages))
        self.assertTrue(any(r["Id"] == 20002 for r in stages))

    def test_huarong_reset_and_auto_lists_are_distinct(self):
        original = self.source["entities"]["746"]["LoEntityPuzzleTerminalComponent"]
        parsed = parse_terminal(original["Params"])
        self.assertIn(722, parsed["NeedRemoveLoEntityIds"])
        self.assertNotIn(722, parsed["BoxLoEntityIds"])
        self.assertIn(721, parsed["BoxLoEntityIds"])
        self.assertNotIn(721, parsed["NeedRemoveLoEntityIds"])
        runtime = load("unity/Assets/Official/World/Map/12/Resources/Map12/stories.json")
        rule = next(r for r in runtime["puzzleRules"] if r["PlacementId"] == 746)
        self.assertEqual(rule["TerminalRemoveIds"], parsed["NeedRemoveLoEntityIds"])
        self.assertEqual(rule["TerminalBoxIds"], parsed["BoxLoEntityIds"])
        self.assertEqual(json.loads(rule["TerminalJson"]), original)

    def test_native_spine_mesh_is_not_drawn_twice_and_uv_is_restored(self):
        base = 'unity/Assets/Official/World/Map/12/'
        catalog = load(base + 'map12-native-entities.json')
        model = next(m for m in catalog['models'] if m['modelPath'].endswith('/6024_view'))
        self.assertEqual(model['geometryBatchCount'], 0)
        self.assertIsNone(model['geometryResource'])
        self.assertEqual(len(model['spines']), 1)
        source = load(base + 'Resources/Map12/Spines/6024-0.json')
        native = load(base + 'Resources/Map12/NativeSpines/6024-0.json')
        for old_frame, new_frame in zip(source['frames'], native['frames']):
            for old, new in zip(old_frame['attachments'], new_frame['attachments']):
                self.assertEqual(new['vertices'], old['vertices'])
                for i, (old_uv, new_uv) in enumerate(zip(old['uv'], new['uv'])):
                    self.assertAlmostEqual(new_uv, old_uv if i % 2 == 0 else 1-old_uv)
        proof = load('assets/reference/official-map/map12-coordinate-source-manifest.json')
        checks = [r for r in proof['nativeMeshChecks'] if r['model'].endswith('/6024_view')]
        self.assertTrue(checks)
        self.assertTrue(all(r['mesh']['path_id'] and r['component']['path_id'] for r in checks))

    def test_push_rules_preserve_current_masks_rects_and_designated_boxes(self):
        runtime = load("unity/Assets/Official/World/Map/12/Resources/Map12/stories.json")
        rules = {r["PlacementId"]: r for r in runtime["puzzleRules"]}
        self.assertEqual({r["PlacementId"] for r in rules.values() if r["LockType"] == 6},
                         {1401, 4602, 6305, 8702})
        for rule in rules.values():
            if rule["HasPushResult"]:
                self.assertEqual(rule["MoveMask"], 1)
                local = self.source["entities"][str(rule["PlacementId"])]["LoStageResultEntityPushedComponent"]
                self.assertEqual(rule["MovableRect"], local["MovableRect"])
                self.assertEqual(rule["Body"], self.source["templates"][str(rule["EntityClassId"])]["GridTransformComponent"]["BodyRangeInfos"])
            if rule["LockType"] == 6:
                component = self.source["entities"][str(rule["PlacementId"])]["LoEntityLockComponent"]
                params = parse_terminal(component["Params"])
                self.assertEqual(rule["PushBoxDetectorIds"], params["LoOverlapDetectorEntityIds"])
                self.assertEqual(rule["PushBoxIds"], params["LoBoxEntityIds"])

    def test_deferred_reward_spawn_preserves_original_default_spawn_block(self):
        runtime = load("unity/Assets/Official/World/Map/12/Resources/Map12/stories.json")
        rules = {row["PlacementId"]: row for row in runtime["puzzleRules"]}
        for lock_id in (1401, 4602, 6305, 8702):
            for target in rules[lock_id]["SpawnIds"]:
                support = self.source["entities"][str(target)]["LoEntityComponent"]["SpanwInfo"]["PrivateMapSupportData"]
                self.assertTrue(support["SupportSpawnInMap"])
                self.assertTrue(support["BlockDefaultSpawn"])
                self.assertFalse(rules[target]["InitiallySpawned"], str(target))

    def test_story_spatial_uses_native_tags_and_proven_foreign_branches(self):
        base = "unity/Assets/Official/World/Map/12/Resources/Map12/"
        runtime = load(base + "stories.json")
        spatial = load(base + "story-spatial.json")
        self.assertEqual(spatial["manifest"], "871_156598")
        self.assertEqual(len(spatial["Entities"]), 138)
        foreign = foreign_map_subgraphs(runtime, 12)
        self.assertIn("WorldTreeDungeonActive", foreign)
        self.assertNotIn("IronMountDungeonActive", foreign)
        keys = {node["registerKey"] for story in runtime["stories"] + runtime["subgraphs"]
                if story["key"] not in foreign for node in story["nodes"]
                if node["type"] == "GetPosAroundStoryObjectNode"}
        self.assertEqual(keys, {"MountLift", "IronMount"})
        self.assertTrue(keys <= {key for row in spatial["Entities"] for key in row["RegisterKeys"]})
        for player in (3001, 3002):
            self.assertTrue(next(row for row in spatial["Entities"] if row["EntityClassId"] == player)["TagType"] & 1)
        changed = copy.deepcopy(runtime)
        graph = next(row for row in changed["stories"] + changed["subgraphs"] if row["key"] == "DungeonActive")
        node = next(row for row in graph["nodes"] if row["type"] == "IsInTargetMapNode"
                    and json.loads(row["sourceJson"])["MapClassId"] == 11)
        data = json.loads(node["sourceJson"]); data["MapClassId"] = 12; node["sourceJson"] = json.dumps(data)
        self.assertNotIn("WorldTreeDungeonActive", foreign_map_subgraphs(changed, 12))

    def test_water_runtime_keeps_all_original_mesh_uv_material_and_mask_bindings(self):
        source = load("assets/official/map12-rendering.json")["water"]
        runtime = load("unity/Assets/Game/Resources/Map12/Water/map12-water-runtime.json")
        receipt = load("assets/reference/official-map/map12-water-runtime-source-manifest.json")
        self.assertEqual(runtime["mapClassId"], 12)
        self.assertEqual(len(source["renderers"]), 57)
        self.assertEqual(len(runtime["renderers"]), 57)
        self.assertEqual(runtime["maskRect"], source["native_materials"]["mask"]["rect"])
        self.assertEqual(runtime["maskResource"], "Map12/Water/map12-water-mask")
        self.assertEqual(receipt["renderer_count"], 57)
        self.assertEqual(len(receipt["texture_bindings"]), 4)
        self.assertEqual(sum(not row["uv"] for row in source["renderers"]), 34)
        water_view = (ROOT / "unity/Assets/Game/Runtime/Presentation/World/NativeMap12/OfficialMap12WaterView.cs").read_text()
        native_acceptance = (ROOT / "unity/Assets/Game/Editor/Map12/OfficialMap12NativeWorldAcceptance.cs").read_text()
        for code in (water_view, native_acceptance):
            self.assertIn("GetIndexCount(subMesh)", code)
            self.assertNotIn(".triangles", code)
        self.assertNotIn("mesh.vertices", native_acceptance)
        self.assertNotIn("mesh.uv", native_acceptance)
        for original, packaged in zip(source["renderers"], runtime["renderers"]):
            with self.subTest(renderer=original["name"]):
                self.assertEqual(packaged["name"], original["name"])
                self.assertEqual(packaged["material"], original["material"])
                self.assertEqual(packaged["vertices"], [v for point in original["vertices"] for v in point])
                self.assertEqual(packaged["uv"], [v for point in original["uv"] for v in point])
                self.assertEqual(packaged["indices"], original["indices"])
                self.assertEqual({v["property"] for v in packaged["textures"]}, set(original["textures"]))
                self.assertEqual({v["property"] for v in packaged["colors"]}, set(original["colors"]))
                self.assertEqual({v["property"] for v in packaged["parameters"]}, set(original["parameters"]))
        self.assertTrue((ROOT / "unity/Assets/Game/Resources/World/Map12Water.shader").is_file())
        self.assertTrue((ROOT / "unity/Assets/Game/Resources/Map12/Water/map12-water-mask.png").is_file())

    def test_interaction_range_state_overrides_are_exported_without_inventing_priority(self):
        templates = load("unity/Assets/Official/World/Map/12/Source/templates.json")
        overrides = {int(class_id): row["StageResultOverrideInteractiveRangeComponent"]
                     for class_id, row in templates.items()
                     if "StageResultOverrideInteractiveRangeComponent" in row}
        self.assertEqual(set(overrides), {5013, 5014, 5015, 6660})
        self.assertTrue(all(not row["WorldStageComponent"]["InteractionFirst"] for row in templates.values()
                            if "WorldStageComponent" in row))
        rules = load("unity/Assets/Official/World/Map/12/Resources/Map12/stories.json")["puzzleRules"]
        for class_id, component in overrides.items():
            with self.subTest(class_id=class_id):
                rule = next(value for value in rules if value["EntityClassId"] == class_id)
                self.assertEqual(rule["InteractiveRangeInfos"], component["InteractionRangeInfos"])
                self.assertEqual(rule["InteractiveRangeOperations"], component["StateOperationInfos"])

    def test_map12_mainline_1215_tag_resolves_to_mount_lift(self):
        runtime = load("unity/Assets/Official/World/Map/12/Resources/Map12/stories.json")
        task_catalog = load("unity/Assets/Game/Resources/Official/Progression/mainline-tasks.json")
        task = next(value for value in task_catalog["tasks"] if value["classId"] == 1215)
        tag = task["finishCondition"]["tag"]
        logic = next(value for value in runtime["mapLogicTags"] if value["Tag"] == tag)
        self.assertEqual(task["finishCondition"]["type"], "MapLogicEntityEndByTagCondition")
        self.assertEqual((logic["MapClass"], logic["LogicId"], logic["EntityClass"]), (12, 3501, 6302))
        self.assertIn(1215, {value["ClassId"] for value in runtime["mainlineTasks"]})


if __name__ == "__main__":
    unittest.main()
