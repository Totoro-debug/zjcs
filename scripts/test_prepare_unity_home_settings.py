"""Checks the official SettingsDialog staging payload and its asset closure."""
from __future__ import annotations

import hashlib
import json
import unittest
from pathlib import Path

import prepare_unity_home_settings as prepare


class HomeSettingsPayloadTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads(prepare.SOURCE_MANIFEST.read_text(encoding="utf-8"))
        cls.runtime = cls.manifest["runtime"]
        cls.extension = json.loads(
            (prepare.OUTPUT / "settings-ui.json").read_text(encoding="utf-8"))
        cls.root = cls.extension["panels"][0]["root"]
        cls.nodes = cls.extension["panels"][0]["nodes"]

    def test_official_panel_paths_and_parent_graph_are_preserved(self):
        self.assertEqual("settings", self.extension["panels"][0]["key"])
        self.assertEqual("/SettingsDialog", self.root["path"])
        paths = {self.root["path"]}
        paths.update(node["path"] for node in self.nodes)
        self.assertEqual(len(paths), len(self.nodes) + 1)
        for node in self.nodes:
            self.assertIn(node["parent_path"], paths)
        self.assertEqual(self.runtime["node_count"], len(paths))

    def test_official_runtime_asset_hashes_match_the_source_manifest(self):
        outputs = dict(self.runtime["outputs"])
        outputs.update(self.runtime["sprites"])
        self.assertEqual(30, len(self.runtime["sprites"]))
        self.assertTrue(any(path.endswith("/click.wav") for path in outputs))
        for relative, expected in outputs.items():
            content = (prepare.ROOT / relative).read_bytes()
            self.assertEqual(expected, hashlib.sha256(content).hexdigest(), relative)

    def test_required_offline_tabs_and_control_bindings_are_present(self):
        paths = {self.root["path"]}
        paths.update(node["path"] for node in self.nodes)
        for name in ("GameGeneralTab", "QualityTab", "SoundTab", "OtherTab",
                     "MessagePushTab", "SettingsQualityGroup_Frame",
                     "SettingsLongNumDisplayGroup", "SoundSettings1"):
            self.assertTrue(any(path.endswith("/" + name) for path in paths), name)
        bindings = json.loads(prepare.SOURCE.read_text(encoding="utf-8"))["bindings"]
        self.assertEqual(
            ["SettingsOption4", "SettingsOption1", "SettingsOption2", "SettingsOption3"],
            [row["object"] for row in bindings["SettingsQualityGroup_Frame"]])


if __name__ == "__main__":
    unittest.main()
