"""Validate Map10 audio bindings against their official story and asset sources."""
from __future__ import annotations

import hashlib
import json
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

import prepare_unity_map10_audio as audio


class Map10AudioTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.mapping_path = audio.OUTPUT_MAPPING
        cls.manifest_path = audio.OUTPUT_MANIFEST
        cls.mapping = json.loads(cls.mapping_path.read_text(encoding="utf-8"))
        cls.manifest = json.loads(cls.manifest_path.read_text(encoding="utf-8"))
        cls.events = {entry["eventKey"]: entry for entry in cls.mapping["events"]}
        cls.event_records = {entry["event"]: entry for entry in cls.manifest["events"]}
        cls.package_index = json.loads(audio.PACKAGE_INDEX.read_text(encoding="utf-8"))
        cls.package_bytes = {}
        cls.package_banks = {}

    def test_all_story_voice_and_playable_sound_events_are_bound(self):
        expected = set(audio._story_voice_events())
        playables = audio._playable_sounds()
        map_bgm = audio._map_bgm()
        expected.add(map_bgm["playEventKey"])
        expected.update(sound["eventKey"] for item in playables for sound in item["sounds"])
        self.assertEqual(expected, set(self.events))
        self.assertEqual(43, len(self.events))
        self.assertEqual(playables, self.mapping["playables"])
        self.assertEqual(map_bgm, {key: value for key, value in self.mapping["mapBgm"].items()
                                   if key != "file"})
        self.assertEqual(self.events[map_bgm["playEventKey"]]["files"][0],
                         self.mapping["mapBgm"]["file"])
        self.assertEqual(expected, set(self.event_records))
        self.assertTrue(all(len(entry["files"]) == 1 for entry in self.events.values()))

    def test_outputs_and_source_slices_match_the_manifest_hashes(self):
        output_records = {entry["path"]: entry for entry in self.manifest["outputs"]}
        for entry in self.events.values():
            for media in entry["files"]:
                relative = media["path"]
                output_path = ROOT / "unity/Assets/StreamingAssets" / Path(relative)
                data = output_path.read_bytes()
                output_record = output_records["unity/Assets/StreamingAssets/" + relative]
                self.assertEqual(output_record["sha256"], hashlib.sha256(data).hexdigest())

        source_manifest = json.loads(audio.EXISTING_AUDIO_MANIFEST.read_text(encoding="utf-8"))
        for record in self.event_records.values():
            if "sourcePath" in record:
                source = source_manifest["resources"][record["sourcePath"]]
                self.assertEqual(source["sha256"], record["sourceSha256"])
                continue

            package = record["mediaPackage"]
            if package not in self.package_bytes:
                raw = (audio.WWISE_ROOT / package).read_bytes()
                self.package_bytes[package] = raw
                self.assertEqual(self.package_index[package], hashlib.md5(raw).hexdigest())
            raw = self.package_bytes[package]
            offset, length = record["mediaOffset"], record["mediaBytes"]
            source_bytes = raw[offset:offset + length]
            self.assertEqual(length, len(source_bytes))
            self.assertEqual(record["wemSha256"], hashlib.sha256(source_bytes).hexdigest())
            if record["mediaBankId"] is not None:
                if package not in self.package_banks:
                    self.package_banks[package] = audio.parse_pck(raw)[0]
                bank = next(item for item in self.package_banks[package]
                            if item[0] == record["mediaBankId"])
                self.assertGreaterEqual(offset, bank[3])
                self.assertLessEqual(offset + length, bank[3] + bank[2])


if __name__ == "__main__":
    unittest.main()
