#!/usr/bin/env python3
"""Verify Cooking runtime payloads against the pinned official source data."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
sys.path.insert(0, str(ROOT / "scripts"))

import prepare_cooking_module as prepare


class CookingModuleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.converter = prepare.CookingConverter()
        cls.expected_catalog, _ = cls.converter.cooking_data()
        cls.catalog = json.loads((prepare.PAYLOAD / "cooking.json").read_text(encoding="utf-8"))
        cls.ui = json.loads((prepare.PAYLOAD / "ui.json").read_text(encoding="utf-8"))
        cls.manifest = json.loads((prepare.REFERENCE / "source-manifest.json").read_text(encoding="utf-8"))

    def test_runtime_catalog_matches_every_pinned_cooking_and_item_row(self):
        self.assertEqual(self.expected_catalog, self.catalog)
        self.assertEqual(110, len(self.catalog["recipes"]))
        self.assertEqual(305, len(self.catalog["items"]))
        self.assertEqual(9999, self.catalog["maxCookingCountAtATime"])
        self.assertEqual(10, self.catalog["cookingRecoveryCountThreshold"])

    def test_conditions_and_formula_items_match_official_item_types(self):
        items = {item["id"]: item for item in self.catalog["items"]}
        used_item_recipes = [recipe for recipe in self.catalog["recipes"]
                             if recipe["conditionType"] == "UsedItemCondition"]
        self.assertEqual(96, len(used_item_recipes))
        self.assertTrue(all(recipe["useSourceOperate"] == 89 for recipe in used_item_recipes))
        for recipe in self.catalog["recipes"]:
            formula_id = recipe["formulaItemId"]
            if formula_id > 0:
                self.assertEqual(322, items[formula_id]["itemType"], formula_id)

    def test_prefab_tree_and_original_item_row_are_preserved(self):
        panel = self.ui["panel"]
        row = self.ui["itemRow"]
        self.assertEqual("Assets/Prefabs/UI/Cooking/CookingPanel.prefab", panel["source_path"])
        self.assertEqual("Assets/Prefabs/UI/Cooking/CookingPanelItem.prefab", row["source_path"])
        self.assertEqual(245, len(panel["nodes"]))
        self.assertEqual(4, len(row["nodes"]))
        for prefab in (panel, row):
            for index, node in enumerate(prefab["nodes"]):
                self.assertTrue(node["parentIndex"] < index)
        self.assertEqual([100.0, 100.0], row["nodes"][0]["size_delta"])
        self.assertTrue(any(node.get("button") for node in panel["nodes"]))
        self.assertTrue(any(node["name"] == "FoodScrollList" and node.get("has_scroll")
                            for node in panel["nodes"]))

    def test_source_receipts_and_all_extracted_sprite_hashes_are_valid(self):
        self.assertEqual(prepare.MANIFEST_ID, self.manifest["manifest"])
        self.assertEqual(prepare.APK_SHA256, self.manifest["apk"]["sha256"])
        self.assertEqual(prepare.BUNDLE_SHA256, self.manifest["bundle"]["sha256"])
        for relative, expected in self.manifest["outputs"].items():
            path = ROOT / relative
            self.assertTrue(path.is_file(), relative)
            self.assertEqual(expected, hashlib.sha256(path.read_bytes()).hexdigest(), relative)
        for sprite in self.manifest["sprites"]:
            source = sprite["source"]
            self.assertTrue(source["bundle_hash"])
            self.assertIn(source["origin"], ("apk_zip", "official_cdn"))
            self.assertTrue(source.get("bundle_sha256") or source.get("raw_sha256"))
            if source["origin"] == "apk_zip":
                self.assertEqual(prepare.APK_SHA256, source["apk_sha256"])
                self.assertTrue(source["apk_entry"])
            else:
                self.assertEqual(prepare.MANIFEST_ID, source["manifest"])
                self.assertTrue(source["url"])
                self.assertTrue(source["bundle_sha256"])
            self.assertTrue((prepare.SPRITES / (sprite["file"] + ".meta")).is_file(), sprite["file"])

    def test_runtime_exposes_tutorial_callbacks_and_max_hp_provider(self):
        runtime = (ROOT / "unity/Assets/Game/Runtime/Presentation/Cooking/CookingFeatureRuntime.cs").read_text(
            encoding="utf-8")
        service = (ROOT / "unity/Assets/Game/Runtime/Offline/Cooking/OfflineCookingService.cs").read_text(
            encoding="utf-8")
        self.assertIn("Func<float> maxHpProvider", runtime)
        self.assertIn("ItemSelectedForTutorial", runtime)
        self.assertIn("CookingCompletedForTutorial", runtime)
        self.assertIn("Input.GetKeyDown(KeyCode.Escape)", runtime)
        self.assertIn("GetBestUnlockedRecoveryCookingId(Func<float> maxHpProvider)", service)
        self.assertIn("profile.ApplyInventoryTransaction(eventId, save =>", service)


if __name__ == "__main__":
    unittest.main()
