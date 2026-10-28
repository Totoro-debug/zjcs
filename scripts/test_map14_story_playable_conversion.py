"""Check lexical float fidelity and the original Map14 performance selection."""
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from official_native_yaml_verification import documents
import verify_unity_map14_story_playable_hierarchy as verification
from prepare_unity_map14_story_playables import selections, selection_keys

class DragonPerformanceConversion(unittest.TestCase):
    def scalar(self, value):
        return documents("--- !u!4 &1\nTransform:\n  value: " + value + "\n")[1][1]["value"]

    def test_negative_zero_retains_original_float_sign(self):
        self.assertTrue(verification.same_float_bits(-0.0, self.scalar("-0"), {"type": "float"}))
        self.assertFalse(verification.same_float_bits(-0.0, self.scalar("0"), {"type": "float"}))
        self.assertTrue(verification.same_float_bits(-0.0, self.scalar("-0.0"), {"type": "float"}))

    def test_original_float_width_controls_decimal_rounding(self):
        self.assertTrue(verification.same_float_bits(0.10000000149011612, self.scalar("0.1"), {"type": "float"}))
        self.assertFalse(verification.same_float_bits(0.10000000149011612, self.scalar("0.1"), {"type": "double"}))
        self.assertFalse(verification.same_float_bits(1.0, self.scalar("1.0000002"), {"type": "float"}))

    def test_every_original_guid_and_alias_is_selected(self):
        root = verification.ROOT / "unity/Assets/Official/World/Map/14/Source"
        graphs = json.loads((root / "stories.json").read_bytes())
        graphs.update(json.loads((root / "story-dependencies.json").read_bytes())["graphs"])
        source = json.loads(verification.SOURCE.read_bytes())
        required = set(selections(graphs))
        self.assertEqual(set(selection_keys(graphs)), set(source["aliases"]))
        required.update(source["aliases"].values())
        self.assertEqual(required, {row["guid"] for row in source["prefabs"]})
        self.assertEqual(len(required), 16)
        self.assertEqual(len([row for row in source["prefabs"] if "/Timeline/Map14/" in row["path"]]), 4)

    def test_modified_original_graph_is_rejected_before_verification(self):
        data = json.loads(verification.SOURCE.read_bytes())
        data["aliases"]["npc_run"] = "0" * 32
        with tempfile.TemporaryDirectory(prefix="map14-playable-source-") as temp:
            path = Path(temp) / "changed.json"
            path.write_text(json.dumps(data), encoding="utf8")
            with patch.object(verification, "SOURCE", path), self.assertRaisesRegex(ValueError, "source changed"):
                verification.build()

if __name__ == "__main__":
    unittest.main()
