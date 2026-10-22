"""Focused tests for extracting Map10 stage and formation references."""
import json
from pathlib import Path
import tempfile
import unittest

from prepare_unity_map10_battle_data import (
    VERSION,
    collect_battle_data,
    existing_output_hashes,
    map10_stage_ids,
    sha256,
)


class Map10StageIdTests(unittest.TestCase):
    def test_collects_map_transition_and_entity_battle_stages(self):
        placements = {
            "id": 10,
            "entities": [{"stages": {"1": [[1033], 3281, [3281]]}}],
        }
        map_stages = [{"MapClassId": 10, "StageId": 9910}]
        self.assertEqual(map10_stage_ids(placements, map_stages, expected_entity_count=1), {9910, 1033})

    def test_rejects_malformed_entity_stage_payload(self):
        placements = {"id": 10, "entities": [{"stages": {"1": [1033, 3281]}}]}
        with self.assertRaisesRegex(ValueError, "stage schema"):
            map10_stage_ids(placements, [], expected_entity_count=1)


class Map10BattleDataTests(unittest.TestCase):
    def test_existing_output_must_match_its_manifest_before_replacement(self):
        with tempfile.TemporaryDirectory() as directory:
            payload_path = Path(directory) / "map10-battles.json"
            manifest_path = Path(directory) / "battle-source-manifest.json"
            payload = b"old payload"
            manifest = {
                "format": 1,
                "manifest": VERSION,
                "mapClassId": 10,
                "output": {
                    "path": "Assets/Official/World/Map/10/map10-battles.json",
                    "sha256": sha256(payload),
                },
            }
            payload_path.write_bytes(payload)
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

            self.assertEqual(existing_output_hashes(payload_path, manifest_path),
                             (sha256(payload), sha256(manifest_path.read_bytes())))
            payload_path.write_bytes(b"changed payload")
            with self.assertRaisesRegex(ValueError, "does not match its source manifest"):
                existing_output_hashes(payload_path, manifest_path)

    def test_sorts_array_groups_and_selects_only_referenced_scenes(self):
        stages = [{
            "Id": 1033,
            "GameId": 1000,
            "WaveArrayIds": [1205],
            "WaveMonsterIds": [[3281, 3281]],
            "WaveArrayCatalogs": None,
        }]
        arrays = {
            1205: {
                "Catalog": 120,
                "ArrayId": 1205,
                "Monsters": [{"GroupId": 2, "Pos": {"X": 0, "Y": 4}},
                             {"GroupId": 1, "Pos": {"X": 1, "Y": 3}},
                             {"GroupId": 1, "Pos": {"X": -1, "Y": 4}}],
            },
            9: {"Catalog": 0, "ArrayId": 9, "Monsters": []},
        }
        scenes = {"1000": {"FightComponent": {"SceneName": "game_1000"},
                            "FightRoleMonsterNormalComponent": {"WaveOriPos": [{"X": 11, "Y": 9}]}},
                  "1004": {"FightComponent": {"SceneName": "game_1004"}}}

        selected_arrays, selected_scenes = collect_battle_data(stages, arrays, scenes)

        self.assertEqual(list(selected_arrays), ["1205"])
        self.assertEqual([item["GroupId"] for item in selected_arrays["1205"]["Monsters"]], [1, 1, 2])
        self.assertEqual(list(selected_scenes), ["1000"])

    def test_rejects_group_count_that_differs_from_stage_roster(self):
        stages = [{"Id": 1033, "GameId": 1000, "WaveArrayIds": [1205],
                   "WaveMonsterIds": [[3281, 3282]], "WaveArrayCatalogs": None}]
        arrays = {1205: {"Catalog": 120, "ArrayId": 1205,
                         "Monsters": [{"GroupId": 1}, {"GroupId": 2}, {"GroupId": 3}]}}
        with self.assertRaisesRegex(ValueError, "group count"):
            collect_battle_data(stages, arrays, {"1000": {}})

    def test_rejects_unresolved_official_arena(self):
        stages = [{"Id": 1033, "GameId": 1000, "WaveArrayIds": [],
                   "WaveMonsterIds": [], "WaveArrayCatalogs": None}]
        with self.assertRaisesRegex(ValueError, "no official battle scene"):
            collect_battle_data(stages, {}, {})


if __name__ == "__main__":
    unittest.main()
