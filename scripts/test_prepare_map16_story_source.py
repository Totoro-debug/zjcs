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


if __name__ == "__main__":
    unittest.main()
