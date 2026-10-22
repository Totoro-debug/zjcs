"""Focused tests for extracting Map10 stage and formation references."""
import json
from pathlib import Path
import tempfile
import unittest

from prepare_unity_map10_battle_data import (
    VERSION,
    collect_battle_data,
    collect_monster_rosters,
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

    def test_expands_official_groups_and_builds_monster_battle_data(self):
        stage = {
            "Id": 1033, "GameId": 1000, "Level": 1.0, "SubRank": 1,
            "MonsterGroupNo": 0, "MonsterLevelAdd": 0, "MaxRound": 15,
            "WaveArrayIds": [1205], "WaveMonsterIds": [[3281, 3281]],
        }
        arrays = {1205: {"Monsters": [
            {"GroupId": 2, "Pos": {"X": 0, "Y": 4}, "LevelAdd": 0.0,
             "MonsterType": 1, "Faction": 127, "Rot": 1, "FlipX": False, "ViewGroup": 0},
            {"GroupId": 1, "Pos": {"X": -1, "Y": 4}, "LevelAdd": 0.0,
             "MonsterType": 1, "Faction": 127, "Rot": 1, "FlipX": False, "ViewGroup": 0},
            {"GroupId": 1, "Pos": {"X": 1, "Y": 3}, "LevelAdd": 0.0,
             "MonsterType": 1, "Faction": 127, "Rot": 1, "FlipX": False, "ViewGroup": 0},
        ]}}
        scenes = {"1000": {"FightRoleMonsterNormalComponent": {
            "WaveOriPos": [{"X": 11, "Y": 9}]}}}
        battle_rules = {
            "tables": {
                "role_prop_group": [{"ID": 10, "SubRank": 1, "LevelPropID": 500, "ExtraLevelPropID": 0}],
                "monster_group": [{
                    "MonsterId": 3281, "GroupNo": 0,
                    "PropCfg": {"PropGroupId": 10, "PropFactors": {
                        "1": 10000, "2": 9500, "3": 11000, "4": 9500, "6": 0, "100": 50000}},
                    "ExtraProps": {"999": 7}, "ActiveSkills": [14235, 14236],
                    "PassiveSkills": [], "DeactivePassiveSkills": [], "HpPliesId": 0,
                    "ResilienceInfo": {"ResetRound": 0},
                }],
            },
            "props": {"500": {"rows": {"1": {
                "1": 50, "2": 71, "3": 39, "4": 51, "6": 17, "100": 1,
            }}}},
        }
        general_rules = {"item": [
            {"ClassId": 14235, "Rank": 1}, {"ClassId": 14236, "Rank": 1},
        ]}
        skill_rows = [
            {"ClassId": 14235, "EcEntityId": 18140},
            {"ClassId": 14236, "EcEntityId": 18150},
        ]
        skill_entities = {"18140": {}, "18150": {}}
        property_ids = {"Attack": 1, "Defence": 2, "MaxHp": 3, "Speed": 4, "MoveDist": 100}

        rosters = collect_monster_rosters([stage], arrays, scenes, battle_rules,
                                          general_rules, skill_rows, skill_entities, property_ids)

        units = rosters[0]["Waves"][0]["Units"]
        self.assertEqual([unit["MonsterId"] for unit in units], [3281, 3281, 3281])
        self.assertEqual([unit["FormationGroupId"] for unit in units], [1, 1, 2])
        self.assertEqual([unit["FormationGroupIndex"] for unit in units], [0, 0, 1])
        self.assertEqual([unit["Position"] for unit in units], [
            {"X": 10, "Y": 13}, {"X": 12, "Y": 12}, {"X": 11, "Y": 13},
        ])
        properties = {item["Id"]: item["Value"] for item in units[0]["Properties"]}
        self.assertEqual(properties, {1: 50, 2: 67, 3: 42, 4: 48, 6: 0, 100: 5, 999: 7})
        self.assertEqual(units[0]["ActiveSkills"], [
            {"Slot": 0, "ItemId": 14235, "EntityId": 18140, "Rank": 1},
            {"Slot": 1, "ItemId": 14236, "EntityId": 18150, "Rank": 1},
        ])

    def test_rejects_monster_skill_without_official_entity_mapping(self):
        stage = {"Id": 1033, "GameId": 1000, "Level": 1.0, "SubRank": 1,
                 "MonsterGroupNo": 0, "MonsterLevelAdd": 0, "MaxRound": 15,
                 "WaveArrayIds": [1205], "WaveMonsterIds": [[3281]]}
        arrays = {1205: {"Monsters": [{"GroupId": 1, "Pos": {"X": 0, "Y": 0},
                                         "LevelAdd": 0.0, "MonsterType": 1, "Faction": 127,
                                         "Rot": 1, "FlipX": False, "ViewGroup": 0}]}}
        scenes = {"1000": {"FightRoleMonsterNormalComponent": {"WaveOriPos": [{"X": 0, "Y": 0}]}}}
        battle_rules = {
            "tables": {"role_prop_group": [{"ID": 10, "SubRank": 1, "LevelPropID": 500,
                                              "ExtraLevelPropID": 0}],
                       "monster_group": [{"MonsterId": 3281, "GroupNo": 0,
                                          "PropCfg": {"PropGroupId": 10, "PropFactors": {}},
                                          "ExtraProps": {}, "ActiveSkills": [14235]}]},
            "props": {"500": {"rows": {"1": {}}}},
        }
        with self.assertRaisesRegex(ValueError, "skill entity is missing"):
            collect_monster_rosters([stage], arrays, scenes, battle_rules,
                                    {"item": [{"ClassId": 14235, "Rank": 1}]},
                                    [{"ClassId": 14235, "EcEntityId": 18140}], {}, {})


if __name__ == "__main__":
    unittest.main()
