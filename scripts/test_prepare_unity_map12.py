"""Source-fidelity regressions for the mountain conversion."""
import copy
import json
from pathlib import Path
import unittest

from prepare_unity_map10_battle_data import map11_component_stage_bindings
from prepare_unity_map11_runtime_data import build_story_node
from prepare_unity_map12_runtime_data import parse_terminal
from prepare_unity_map12_sources import verify_legacy_equivalence


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


if __name__ == "__main__":
    unittest.main()
