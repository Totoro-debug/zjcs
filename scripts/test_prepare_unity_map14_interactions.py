"""Verify active-source interaction bindings and fast-stage source fields."""
import copy
import json
import unittest

import prepare_mainline_interactions as shared
import prepare_unity_map14_interactions as migration


class CurrentMainlineInteractionSources(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        (cls.version, cls.templates, cls.tables, cls.stage_fast_types,
         cls.template_evidence, cls.input_records) = shared.load_current_interaction_inputs()

    def test_active_source_and_shared_payload(self):
        active = json.loads((shared.ROOT / "official-source.json").read_bytes())["active"]
        output = json.loads((shared.OUT / "interactions.json").read_bytes())
        receipt = json.loads((shared.RECEIPT).read_bytes())
        self.assertEqual(self.version, active)
        self.assertEqual(output["manifest"], active)
        self.assertEqual(receipt["manifest"], active)
        self.assertEqual(receipt["inputs"]["map_region"]["manifest"], active)
        self.assertEqual({row["manifest"] for row in receipt["templates"]["sources"]}, {active})
        expected_regions = [row for row in self.tables["Map/map_region"] if 10 <= row["ClassId"] <= 16]
        self.assertEqual([(row["MapClassId"], row["Region"], row["FastStageCombatRating"], row["GameFastType"])
                          for row in output["regions"]],
                         [(row["ClassId"], row["Region"], row["FastStageCombatRating"], row["GameFastType"])
                          for row in expected_regions])
        self.assertTrue(output["stageFastTemplates"])
        self.assertTrue(receipt["templates"]["stageFastTypesSha256"])

    def test_map14_interactions_match_active_templates_and_tables(self):
        expected = migration.interaction_payload(self.templates, self.tables, version=self.version,
                                                 fast_stage_templates=self.stage_fast_types)
        output = json.loads(migration.OUTPUT.read_bytes())
        receipt = json.loads(migration.RECEIPT.read_bytes())
        self.assertEqual(output, expected)
        self.assertEqual(receipt["manifest"], self.version)
        self.assertEqual([row["EntityClassId"] for row in output["heroes"]], [6570, 6571, 6572, 6573])
        for hero in output["heroes"]:
            self.assertEqual([row["Weight"] for row in hero["PeriodicAwards"]], [5, 25, 70])
            self.assertEqual([row["PeriodicityId"] for row in hero["PeriodicAwards"]], [100, 101, 102])
        actual_fast_types = [(row["EntityClassId"], row["Component"], row["FastForwardType"])
                             for row in output["stageFastTemplates"]]
        expected_fast_types = [(row["EntityClassId"], row["Component"], row["FastForwardType"])
                               for row in self.stage_fast_types]
        self.assertEqual(actual_fast_types, expected_fast_types)
        original_regions = [row for row in self.tables["Map/map_region"] if row["ClassId"] == 14]
        self.assertEqual([row["Region"] for row in output["regions"]],
                         [row["Region"] for row in original_regions])
        self.assertEqual([row["FastStageCombatRating"] for row in output["regions"]],
                         [row["FastStageCombatRating"] for row in original_regions])
        self.assertEqual([row["GameFastType"] for row in output["regions"]],
                         [row["GameFastType"] for row in original_regions])

    def test_scoped_maps_match_active_interaction_payloads(self):
        for map_id, region_count in ((12, 99), (13, 106), (16, 43)):
            with self.subTest(map_id=map_id):
                hero_ids = None
                if map_id == 16:
                    hero_ids = [int(key) for key, components in self.templates.items()
                                if "StageResultPeriodicityRandomAwardComponent" in components]
                expected = migration.interaction_payload(self.templates, self.tables,
                    map_id, region_count, self.version, hero_ids, self.stage_fast_types)
                output = json.loads((shared.ROOT / f"unity/Assets/Game/Resources/Map{map_id}/interactions.json").read_bytes())
                self.assertEqual(output, json.loads(json.dumps(expected)))
                self.assertEqual(output["manifest"], self.version)
        proof = json.loads((shared.ROOT / "assets/reference/official-map/mainline-interaction-maps-source-manifest.json").read_bytes())
        self.assertEqual(proof["manifest"], self.version)
        self.assertEqual({row["mapClassId"] for row in proof["maps"]}, {12, 13, 14, 16})

    def test_all_six_active_hero_variants_are_preserved(self):
        hero_ids = [int(key) for key, components in self.templates.items()
                    if "StageResultPeriodicityRandomAwardComponent" in components]
        payload = migration.interaction_payload(self.templates, self.tables, 16, 43, self.version, hero_ids)
        self.assertEqual(payload["heroTemplateIds"], [6570, 6571, 6572, 6573, 6574, 6575])
        self.assertEqual([row["EntityClassId"] for row in payload["heroes"]], hero_ids)

    def test_invalid_hero_selection_and_unported_periodicity_are_rejected(self):
        for ids in ([], [6570, 6570], [999999]):
            with self.subTest(ids=ids), self.assertRaises(ValueError):
                migration.interaction_payload(self.templates, self.tables, hero_ids=ids)
        changed = copy.deepcopy(self.templates)
        changed["6570"]["StageResultPeriodicityRandomAwardComponent"]["PeriodicityRandomAwardGroupIds"] = [999]
        with self.assertRaisesRegex(ValueError, "unported rule"):
            migration.interaction_payload(changed, self.tables, hero_ids=[6570])

    def test_current_stage_fast_types_are_valid_and_unique(self):
        identities = [(row["EntityClassId"], row["Component"]) for row in self.stage_fast_types]
        self.assertTrue(self.stage_fast_types)
        self.assertEqual(len(identities), len(set(identities)))
        self.assertTrue(all(0 <= row["FastForwardType"] <= 7 for row in self.stage_fast_types))
        self.assertEqual(self.input_records["map_region"]["manifest"], self.version)


if __name__ == "__main__":
    unittest.main()
