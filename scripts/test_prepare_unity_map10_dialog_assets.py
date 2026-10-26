import hashlib
import json
import struct
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST_PATH = ROOT / "assets/reference/official-map/map10-dialogue-source-manifest.json"


class Map10DialogueAssetTests(unittest.TestCase):
    def test_generated_dialogue_assets_match_the_pinned_source_manifest(self):
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        self.assertEqual(manifest["manifest"], "854_156383")
        self.assertEqual(manifest["mapClassId"], 10)
        adventure = ROOT / manifest["inputs"]["adventure"]["path"]
        self.assertEqual(hashlib.sha256(adventure.read_bytes()).hexdigest(),
                         manifest["inputs"]["adventure"]["sha256"])
        for role_id, source in manifest["inputs"]["portraits"].items():
            self.assertEqual(source["manifest"], "854_156383")
            self.assertIn("portraitFaceComponent", source)
            for sprite in source["sprites"].values():
                self.assertEqual(sprite["manifest"], "854_156383")
                self.assertTrue(sprite["raw_sha256"])
        self.assertEqual(set(manifest["inputs"]["dialogueIcons"]),
                         {"Iconarrow", "IconExit", "Iconarrow_1"})
        for source in manifest["inputs"]["dialogueIcons"].values():
            self.assertEqual(source["manifest"], "854_156383")
            self.assertTrue(source["raw_sha256"])
        for record in manifest["outputs"]:
            output = ROOT / record["path"]
            payload = output.read_bytes()
            self.assertEqual(hashlib.sha256(payload).hexdigest(), record["sha256"])
            if output.suffix == ".png":
                self.assertEqual(struct.unpack(">II", payload[16:24]),
                                 (record["width"], record["height"]))
                self.assertIn("spriteMode: 1", Path(str(output) + ".meta").read_text(encoding="utf-8"))

    def test_native_portrait_bounds_expressions_and_talkdialog_style_are_preserved(self):
        manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
        data_path = ROOT / next(record["path"] for record in manifest["outputs"]
                                if record["path"].endswith("dialogue-portraits.json"))
        data = json.loads(data_path.read_text(encoding="utf-8"))
        self.assertEqual(data["manifest"], "854_156383")
        portraits = {row["portraitPath"]: row for row in data["portraits"]}
        self.assertEqual(set(portraits), {
            "UI/Portrait/Npc_Portrait_8402", "UI/Portrait/Npc_Portrait_8404",
            "UI/Portrait/Npc_Portrait_8417"})
        chaos = portraits["UI/Portrait/Npc_Portrait_8402"]
        self.assertEqual(chaos["main"]["nativeSize"], [608, 724])
        self.assertEqual(chaos["main"]["position"], [0.0, -3.5999999046325684])
        self.assertEqual(chaos["face"]["nodeName"], "Face")
        self.assertEqual({face["type"] for face in chaos["face"]["expressions"]},
                         {"angry", "happy", "shock"})
        bird = portraits["UI/Portrait/Npc_Portrait_8417"]
        self.assertEqual(bird["main"]["nativeSize"], [684, 524])
        self.assertEqual(bird["face"]["nodeName"], "Brow")
        self.assertEqual({face["type"] for face in bird["face"]["expressions"]}, {"cry"})
        self.assertTrue(chaos["needFlipOnRight"])
        self.assertEqual([icon["uri"] for icon in data["icons"]], [
            "UI/Dialogue/Iconarrow", "UI/Dialogue/IconExit", "UI/Dialogue/Iconarrow_1"])
        self.assertTrue(bird["needFlipOnRight"])
        for side in ("left", "right"):
            style = data["speakerStyle"][side]
            self.assertEqual(style["light"], [1.0, 1.0, 1.0, 1.0])
            self.assertAlmostEqual(style["dark"][0], 0.6)
            self.assertAlmostEqual(style["colorDuration"], 0.117)


if __name__ == "__main__":
    unittest.main()
