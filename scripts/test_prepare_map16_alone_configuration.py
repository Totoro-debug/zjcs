import importlib.util
import unittest
from pathlib import Path

SCRIPT = Path(__file__).with_name("prepare_map16_alone_configuration.py")
SPEC = importlib.util.spec_from_file_location("prepare_map16_alone_configuration", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class Map16AloneConfigurationTests(unittest.TestCase):
    def setUp(self):
        self.map_row = {
            "AloneMapEntryId": 16,
            "PreparingType": 1,
            "PreparingDataParam": "{Rank:'Silver',DayOffset:14,MinDurationS:300}",
            "PrepareEndConditionId": 116,
            "PrepareEndStoryId": "DiscoverWhaleIsland",
            "PrepareEndActiveMapClassIds": [16],
            "FinishStoryId": "WhaleIslandLeave",
            "FinishRewardRuleIds": [770, 771],
            "UiPrefabPath": "UI/AloneWorldMap/AloneWorldMapEntryItemContentMap16",
        }
        self.condition = {"ClassId": 116, "Condition": {"_type": "MapUnlockCondition", "MapClassId": 16}}
        self.stories = [{"StoryName": name, "ResPath": "Map13Story/WhaleIsland/" + name}
                        for name in MODULE.STORY_NAMES]

    def test_extracts_official_whale_island_configuration_in_declared_story_order(self):
        result = MODULE.extract_configuration([self.map_row], [self.condition], self.stories)
        self.assertEqual(result["manifest"], "884_156692")
        self.assertEqual(result["aloneWorld"]["PreparingDataParam"],
                         "{Rank:'Silver',DayOffset:14,MinDurationS:300}")
        self.assertEqual(result["prepareEndCondition"], self.condition)
        self.assertEqual([row["StoryName"] for row in result["stories"]], list(MODULE.STORY_NAMES))

    def test_rejects_changed_preparation_or_map_unlock_condition(self):
        changed = dict(self.map_row, PreparingDataParam="{Rank:'Silver',DayOffset:15,MinDurationS:300}")
        with self.assertRaises(ValueError):
            MODULE.extract_configuration([changed], [self.condition], self.stories)
        wrong_condition = {"ClassId": 116, "Condition": {"_type": "MapUnlockCondition", "MapClassId": 15}}
        with self.assertRaises(ValueError):
            MODULE.extract_configuration([self.map_row], [wrong_condition], self.stories)

    def test_rejects_missing_whale_island_story(self):
        with self.assertRaises(ValueError):
            MODULE.extract_configuration([self.map_row], [self.condition], self.stories[:-1])


if __name__ == "__main__":
    unittest.main()
