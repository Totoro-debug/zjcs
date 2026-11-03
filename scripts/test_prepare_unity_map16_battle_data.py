"""Validate the pinned Map16 battle-source closure without loading Unity."""
from __future__ import annotations

import hashlib
import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/16/Source"
OUTPUT = ROOT / "unity/Assets/Official/World/Map/16/map16-battles-source.json"
RECEIPT = ROOT / "assets/reference/official-map/map16-battles-source-manifest.json"


def read_json(path: Path):
    return json.loads(path.read_bytes())


class Map16BattleSourceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.output_bytes = OUTPUT.read_bytes()
        cls.receipt = read_json(RECEIPT)
        cls.output = json.loads(cls.output_bytes)

    def test_receipts_pin_current_output_and_generator(self):
        self.assertEqual("884_156692", self.receipt["manifest"])
        self.assertEqual(16, self.receipt["mapClassId"])
        self.assertEqual(self.receipt["output"]["sha256"],
                         hashlib.sha256(self.output_bytes).hexdigest())
        generator = ROOT / self.receipt["generator"]["path"]
        normalized = generator.read_bytes().replace(b"\r\n", b"\n")
        self.assertEqual(self.receipt["generator"]["sha256"], hashlib.sha256(normalized).hexdigest())

    def test_official_stage_scene_and_formation_references_are_closed(self):
        stages = {int(row["Id"]): row for row in self.output["stages"]}
        self.assertEqual(70, len(stages))
        self.assertEqual(46, len(self.output["monsterArrays"]))
        self.assertEqual(9, len(self.output["battleScenes"]))
        self.assertEqual(set(str(row["GameId"]) for row in stages.values()),
                         set(self.output["battleScenes"]))
        for stage in stages.values():
            self.assertEqual(len(stage.get("WaveArrayIds") or []),
                             len(stage.get("WaveMonsterIds") or []))
            for array_id in stage.get("WaveArrayIds") or []:
                self.assertIn(str(array_id), self.output["monsterArrays"])
        component_stage_ids = {
            int(stage_id)
            for binding in self.output["entityStageBindings"]
            for component in binding["StageComponents"]
            for stage_id in component["StageIds"]
        }
        self.assertEqual(set(stages), component_stage_ids)
        self.assertEqual([], self.output["storyStageBindings"])

    def test_original_whale_island_stories_have_no_stage_start_nodes(self):
        source_receipt = read_json(ROOT / "assets/reference/official-map/map16-story-source-manifest.json")
        story_path = SOURCE / "story-source.json"
        source_output = next(row for row in source_receipt["outputs"]
                             if row["path"] == story_path.relative_to(ROOT).as_posix())
        self.assertEqual(source_output["sha256"], hashlib.sha256(story_path.read_bytes()).hexdigest())
        stories = read_json(story_path)
        self.assertEqual(7, len(stories["graphs"]))
        self.assertTrue(all(node["_type"] != "StoryStartStageNode"
                            for graph in stories["graphs"].values()
                            for node in graph["nodes"].values()))


if __name__ == "__main__":
    unittest.main()
