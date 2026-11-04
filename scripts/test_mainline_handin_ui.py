"""Check installed shared UI provenance and its complete native reference closure."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[1]
REFERENCE = ROOT / "assets/reference/official-map"
RUNTIME = ROOT / "unity/Assets/Game/Resources/World/Mainline/HandIn"


class MainlineHandInUiTests(unittest.TestCase):
    def test_source_and_native_receipts_match_installed_files(self):
        for name in ("mainline-handin-ui-source-manifest.json", "mainline-handin-ui-bindings-source-manifest.json"):
            receipt = json.loads((REFERENCE / name).read_bytes())
            self.assertEqual(receipt["manifest"], "884_156692")
            for row in receipt["inputs"] + receipt["outputs"]:
                with self.subTest(receipt=name, path=row["path"]):
                    raw = (ROOT / row["path"]).read_bytes()
                    if row.get("hashLineEndings") == "LF":
                        raw = raw.replace(b"\r\n", b"\n")
                    self.assertEqual(hashlib.sha256(raw).hexdigest(), row["sha256"])

    def test_current_original_hierarchy_and_audio(self):
        raw = (RUNTIME / "catalog.json").read_text(encoding="utf8")
        self.assertNotIn("output/validation", raw)
        data = json.loads(raw)
        self.assertEqual(data["manifest"], "884_156692")
        self.assertEqual(data["mapClassId"], 0)
        self.assertEqual([len(panel["nodes"]) for panel in data["panels"]], [43, 19])
        self.assertEqual(len(data["components"]), 101)
        nodes = data["panels"][0]["nodes"]
        probability = next(node for node in nodes if node["name"] == "RandomProbabilityContent")
        self.assertFalse(probability["active"])
        for sprite in data["sprites"]:
            path = ROOT / "unity" / sprite["unityAssetPath"]
            self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), sprite["sha256"])
        self.assertEqual({event["id"] for event in data["audioProof"]["events"]}, {3772241779, 3686182067})
        for event in data["audioProof"]["events"]:
            self.assertNotIn("master volume: auto", event["graph"])
            self.assertEqual(hashlib.sha256((ROOT / event["reusedAsset"]).read_bytes()).hexdigest(), event["renderedSha256"])

    def test_native_references_resolve(self):
        # The common prefabs, materials and animations must have actual local bindings.
        references = {}
        for path in RUNTIME.iterdir():
            if path.suffix not in (".prefab", ".mat", ".anim", ".controller"):
                continue
            references[path] = {guid for guid in re.findall(r"guid: ([a-f0-9]{32})", path.read_text(encoding="utf8"))
                                if not guid.startswith("0000000000000000")}
        needed = set().union(*references.values())
        result = subprocess.run(["rg", "--no-ignore", "--no-heading", "--with-filename", "--glob", "*.meta",
            "^guid: (" + "|".join(sorted(needed)) + r")\r?$",
            *[str(ROOT / "unity" / folder) for folder in ("Assets", "Library/PackageCache", "Packages")]],
            check=True, stdout=subprocess.PIPE, text=True, encoding="utf8")
        assets = {}
        for line in result.stdout.splitlines():
            filename, guid = line.rsplit(":guid: ", 1)
            assets[guid] = Path(filename).with_suffix("")
        for path, guids in references.items():
            for guid in guids:
                with self.subTest(asset=path.name, guid=guid):
                    self.assertTrue(guid in assets, "Unresolved native reference: " + guid)
                    self.assertTrue(assets[guid].exists())


if __name__ == "__main__":
    unittest.main()
