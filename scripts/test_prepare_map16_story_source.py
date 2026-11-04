import importlib.util
from pathlib import Path
import unittest


SPEC = importlib.util.spec_from_file_location(
    "prepare_map16_story_source", Path(__file__).with_name("prepare_map16_story_source.py"))
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class Map16StorySourceTests(unittest.TestCase):
    def setUp(self):
        self.rows = [
            {"StoryName": "LookForWhaleIsland", "ResPath": "Map13Story/WhaleIsland/LookForWhaleIsland"},
            {"StoryName": "WhaleIslandLeave", "ResPath": "Map13Story/WhaleIsland/WhaleIslandLeave"},
            {"StoryName": "OtherMapStory", "ResPath": "Map14Story/OtherMapStory"},
        ]

    def test_selects_only_the_complete_whale_island_story_closure(self):
        rows = [dict(row, StoryName=f"WhaleStory{index}", ResPath=f"Map13Story/WhaleIsland/{index}")
                for index, row in enumerate(self.rows[:1] * 7)]
        selected = MODULE.story_table_rows(rows + self.rows[2:])
        graphs = {row["StoryName"]: {"nodes": []} for row in selected}
        self.assertEqual(set(MODULE.select_story_graphs(graphs, selected)),
                         {row["StoryName"] for row in selected})

    def test_rejects_incomplete_or_duplicate_original_story_rows(self):
        with self.assertRaisesRegex(ValueError, "closure changed"):
            MODULE.story_table_rows(self.rows)
        duplicate = [{"StoryName": "Same", "ResPath": "Map13Story/WhaleIsland/one"}] * 7
        with self.assertRaisesRegex(ValueError, "closure changed"):
            MODULE.story_table_rows(duplicate)

    def test_rejects_graph_names_that_do_not_match_the_story_table(self):
        rows = [{"StoryName": f"WhaleStory{index}", "ResPath": f"Map13Story/WhaleIsland/{index}"}
                for index in range(7)]
        graphs = {row["StoryName"]: {"nodes": []} for row in rows[:-1]}
        with self.assertRaisesRegex(ValueError, "graph names differ"):
            MODULE.select_story_graphs(graphs, rows)

    def test_pinned_story_source_closes_dialogue_roles_tags_and_hand_in_rules(self):
        import hashlib
        from pathlib import Path
        import json

        root = Path(__file__).resolve().parents[1]
        source_path = root / "unity/Assets/Official/World/Map/16/Source/story-source.json"
        receipt = json.loads((root / "assets/reference/official-map/map16-story-source-manifest.json").read_bytes())
        source_bytes = source_path.read_bytes()
        source = json.loads(source_bytes)
        self.assertEqual("884_156692", receipt["manifest"])
        self.assertEqual(hashlib.sha256(source_bytes).hexdigest(), receipt["outputs"][0]["sha256"])
        table_sources = {row["table"]: row for row in receipt["tables"] if row.get("table")}
        self.assertEqual("defaultpackage_assets_config_luban_dialog.bundle",
                         table_sources["Dialog/dialog_role"]["bundle"])
        self.assertEqual("defaultpackage_assets_config_luban_map.bundle",
                         table_sources["Map/map_logic_entity_tag"]["bundle"])
        self.assertTrue(all(row["manifest"] == "884_156692" and row["origin"] == "apk_zip"
                            for row in table_sources.values()))
        roles = {row["RoleId"] for row in source["dialogRoles"]}
        dialogue_roles = {row["RoleId"] for groups in source["dialogues"].values()
                          for rows in groups.values() for row in rows if row["RoleId"] > 0}
        self.assertEqual(dialogue_roles, roles)
        self.assertTrue(source["mapLogicTags"])
        self.assertTrue(all(row["MapClass"] == 16 for row in source["mapLogicTags"]))
        from prepare_unity_map13_story_runtime import parse_inline_object
        condition = next(node for graph in source["graphs"].values() for node in graph["nodes"].values()
                         if node["_type"] == "PlayerConditionNode")
        self.assertEqual("MapLogicEntityEndByTag", condition["Type"])
        condition_tag = parse_inline_object(condition["Param"])["Tag"]
        self.assertIn(condition_tag, {row["Tag"] for row in source["mapLogicTags"]})
        hand_in_ids = {int(node.get("HandInItemId", node.get("HandleItemId", 0)))
                       for graph in source["graphs"].values() for node in graph["nodes"].values()
                       if node["_type"] in ("CanHandInItemNode", "HandInItemNode")}
        hand_in_ids.discard(0)
        self.assertEqual(hand_in_ids, {row["Id"] for row in source["handInItems"]})
        fixed_reward_ids = {int(node.get("FixedRewardId", node.get("AwardId", 0)))
                            for graph in source["graphs"].values() for node in graph["nodes"].values()
                            if node["_type"] in ("GetFixAwardNode", "CanGetFixedRewardNode")}
        fixed_reward_ids.discard(0)
        self.assertEqual(fixed_reward_ids, {row["Id"] for row in source["fixedRewards"]})


if __name__ == "__main__":
    unittest.main()
