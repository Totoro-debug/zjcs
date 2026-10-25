import json
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from prepare_unity_map10_battle_data import map11_component_stage_bindings


class Map11BattleDataTests(unittest.TestCase):
    def test_preserves_official_stage_and_periodicity_components(self):
        entities = [{"Id": index} for index in range(1, 1804)]
        components = [{"EntityId": index, "Json": "{}"} for index in range(1, 1804)]
        components[103] = {
            "EntityId": 104,
            "Json": json.dumps({
                "LoStageBehStageComponent": {"StageIds": [171]},
                "LoStageBehStoneCastleStageComponent": {"StageId": 170, "EnergyCircuitGameId": 0},
            }),
        }
        components[1115] = {
            "EntityId": 1116,
            "Json": json.dumps({
                "LoStageBehBattleSteleStageComponent": {"StageId": 900},
                "LoStageResultPeriodicityInteractionComponent": {"PeriodicityGroupIds": [101]},
                "LoStageConPlayerPeriodicityInteractionComponent": {"PeriodicityGroupIds": [101]},
            }),
        }

        bindings = map11_component_stage_bindings({
            "ClassId": 11,
            "Entities": entities,
            "EntityComponents": components,
        })

        self.assertEqual(bindings, [
            {"PlacementId": 104, "StageComponents": [
                {"Component": "LoStageBehStageComponent", "StageIds": [171]},
                {"Component": "LoStageBehStoneCastleStageComponent", "StageIds": [170]},
            ]},
            {"PlacementId": 1116, "StageComponents": [
                {"Component": "LoStageBehBattleSteleStageComponent", "StageIds": [900]},
            ], "PeriodicityGroupIds": [101], "PlayerPeriodicityGroupIds": [101]},
        ])

    def test_rejects_misaligned_entity_component_rows(self):
        definition = {
            "ClassId": 11,
            "Entities": [{"Id": index} for index in range(1, 1804)],
            "EntityComponents": [{"EntityId": index, "Json": "{}"} for index in range(1, 1804)],
        }
        definition["EntityComponents"][0]["EntityId"] = 2

        with self.assertRaisesRegex(ValueError, "one-to-one"):
            map11_component_stage_bindings(definition)


if __name__ == "__main__":
    unittest.main()
