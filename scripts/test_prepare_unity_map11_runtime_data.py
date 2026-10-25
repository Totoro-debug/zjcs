"""Regression checks for pinned Map11 runtime data conversion."""
import json
import tempfile
import unittest
from pathlib import Path

import prepare_unity_map10_battle_data as battle_converter
import prepare_unity_map11_runtime_data as converter


class Map11RuntimeDataTests(unittest.TestCase):
    def test_pinned_map11_runtime_payload_is_complete(self):
        with tempfile.TemporaryDirectory(prefix="zjcs-map11-runtime-") as output:
            root = Path(output)
            battle_root = root / "battle-source"
            battle_root.mkdir()
            battle_converter.build(output=battle_root, map_id=11)
            previous_battle_path = converter.BATTLE_SOURCE_PATH
            previous_manifest_path = converter.BATTLE_SOURCE_MANIFEST_PATH
            converter.BATTLE_SOURCE_PATH = battle_root / "map11-battles.json"
            converter.BATTLE_SOURCE_MANIFEST_PATH = battle_root / "battle-source-manifest.json"
            try:
                runtime_root = root / "runtime"
                converter.build(runtime_root)
            finally:
                converter.BATTLE_SOURCE_PATH = previous_battle_path
                converter.BATTLE_SOURCE_MANIFEST_PATH = previous_manifest_path
            stories = json.loads((runtime_root / "stories.json").read_text(encoding="utf-8"))
            battles = json.loads((runtime_root / "battle-runtime.json").read_text(encoding="utf-8"))
            manifest = json.loads((runtime_root / "map11-runtime-source-manifest.json").read_text(encoding="utf-8"))

        self.assertEqual((stories["manifest"], stories["mapClassId"]), ("854_156383", 11))
        self.assertEqual((len(stories["stories"]), sum(len(story["nodes"]) for story in stories["stories"])),
                         (44, 666))
        self.assertEqual((len(stories["mapLogicTags"]), len(stories["mapTalkEntries"])), (35, 50))
        self.assertEqual(len(stories["storyOptions"]), 61)
        self.assertEqual([reward["Id"] for reward in stories["fixedRewards"]], [23, 29, 30, 31, 41, 42, 43, 45])
        self.assertTrue(all(reward["CanGetTimes"] == 1 for reward in stories["fixedRewards"]))
        self.assertEqual(len(stories["dialogRoles"]), 17)
        self.assertEqual(len(stories["playables"]), 10)
        templates = {row["EntityClassId"]: {component["Type"]: json.loads(component["Json"])
                                           for component in row["Components"]}
                     for row in stories["entityTemplates"]}
        self.assertEqual(len(templates), 110)
        puzzle_rules = {row["PlacementId"]: row for row in stories["puzzleRules"]}
        self.assertEqual(sum(bool(row["TerminalLockIds"]) for row in puzzle_rules.values()), 7)
        self.assertEqual(sum(row["LockType"] in (2, 7) and row["EntityClassId"] in (6530, 6533)
                             for row in puzzle_rules.values()), 9)
        self.assertNotIn(423, puzzle_rules)
        # Preserve the dangling official update, and declare only the reviewed
        # offline exception; never fabricate a placement or discard the source.
        self.assertEqual(puzzle_rules[413]["OtherStates"][0]["StageDatas"],
                         [{"LoEntId": 423, "State": 1, "UseLoEntDefaultState": False}])
        self.assertEqual([(r["PlacementId"], r["OfflineIgnoredMissingStateTargets"])
                          for r in puzzle_rules.values() if r["OfflineIgnoredMissingStateTargets"]], [(413, [423])])
        # Goddess activation is an EC touch transition, not a combat StageId and
        # not an inventory grant based on map_region.DropItems (the treasure preview).
        for class_id, active_type in ((6010, 2), (6011, 3)):
            template = templates[class_id]
            self.assertEqual(template["WorldActiveableEntityComponent"]["ActiveableEntityType"], active_type)
            transition = template["WorldStageComponent"]["StateInfos"][0]["SubStateList"][0]
            self.assertEqual(transition["Conditions"], [{"CompType": 101, "CompIndex": 0}])
            self.assertEqual(transition["Behaviour"], {"CompType": 2, "CompIndex": 0})
            self.assertEqual(transition["Results"], [{"CompType": 204, "CompIndex": 0},
                                                    {"CompType": 209, "CompIndex": 0}])
            self.assertNotIn("StageResultAwardComponent", template)
        self.assertEqual([task["ClassId"] for task in stories["mainlineTasks"]], list(converter.MAINLINE_TASK_IDS))
        all_graphs = stories["stories"] + stories["subgraphs"]
        self.assertEqual([(story["key"], len(story["nodes"])) for story in stories["subgraphs"]],
                         [("CombatAssistance", 33), ("FangjianbeiActive", 11), ("GriffonTeamUp", 5), ("HolyMonsterActive", 15)])
        self.assertTrue(all(json.loads(node["sourceJson"])["_type"] == node["type"]
                            for story in all_graphs for node in story["nodes"]))
        self.assertEqual((len(battles["stages"]), len(battles["monsterRosters"]), len(battles["battleScenes"])),
                         (129, 129, 17))
        self.assertEqual(len(battles["entityStageBindings"]), 591)
        self.assertEqual(manifest["outputs"]["battle-runtime.json"]["stageCount"], 129)
        self.assertEqual(manifest["outputs"]["stories.json"]["subgraphCount"], 4)
        self.assertEqual(manifest["outputs"]["stories.json"]["subgraphNodeCount"], 64)
        self.assertEqual(manifest["outputs"]["stories.json"]["entityTemplateCount"], 110)
        self.assertEqual(set(manifest["outputs"]), {"battle-runtime.json", "stories.json"})


if __name__ == "__main__":
    unittest.main()
