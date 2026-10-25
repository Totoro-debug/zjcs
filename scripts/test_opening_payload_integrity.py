"""Verify the exact byte contracts used by opening playback on Windows."""
import hashlib
import gzip
import json
import re
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
OPENING = ROOT / "unity/Assets/StreamingAssets/Opening"


class OpeningPayloadIntegrityTests(unittest.TestCase):
    def test_runtime_ui_shaders_are_explicitly_packaged_for_windows(self):
        folder = ROOT / "unity/Assets/Game/Runtime/Presentation/Opening"
        runtime = (folder / "OpeningUiMeshGraphic.cs").read_text(encoding="utf-8")
        required = set(re.findall(r'return "(Zjcs/Opening/[^"]+)"', runtime))
        self.assertEqual(4, len(required))
        packaged = (ROOT / "unity/Assets/Game/Resources/Opening/OpeningShaderVariants.shadervariants")
        variants = packaged.read_text(encoding="utf-8")
        for shader in folder.glob("*.shader"):
            name = re.search(r'Shader "([^"]+)"', shader.read_text(encoding="utf-8")).group(1)
            if name not in required:
                continue
            guid = re.search(r"guid: (\w+)", shader.with_suffix(".shader.meta")
                             .read_text(encoding="utf-8")).group(1)
            self.assertIn(guid, variants, "Shader.Find alone does not preserve a Player shader.")
            required.remove(name)
        self.assertFalse(required)
        self.assertIn("_STRAIGHT_ALPHA_INPUT", variants)

    def test_fight_prefab_transforms_cover_record_entities(self):
        payload = OPENING / "model-transforms.json"
        transforms = json.loads(payload.read_bytes())
        record = json.loads(gzip.decompress((OPENING / "opening-record.bytes").read_bytes()))
        fighters = set()

        def visit(value):
            if isinstance(value, dict):
                if any("FightRoleCharacterComponent" in component.get("$type", "")
                       for component in value.get("ComponentList", [])):
                    fighters.add(value["ClassId"])
                for child in value.values():
                    visit(child)
            elif isinstance(value, list):
                for child in value:
                    visit(child)

        visit(record)
        self.assertEqual(fighters, {row["class_id"] for row in transforms["units"]})
        for row in transforms["units"]:
            self.assertTrue(all(source["origin"] == "apk_zip" and len(source["sha256"]) == 64
                                for source in row["sources"]))
        receipt = json.loads((ROOT / "assets/reference/character-creation/opening-source-manifest.json")
                             .read_bytes())
        declared = next(row for row in receipt["outputs"] if row["path"].endswith("/model-transforms.json"))
        self.assertEqual(declared["sha256"], hashlib.sha256(payload.read_bytes()).hexdigest())

    def test_every_declared_particle_mesh_retains_its_pinned_bytes(self):
        effects = json.loads((OPENING / "skill-effects.json").read_text(encoding="utf-8"))
        self.assertTrue(effects["meshes"])
        for name, source in effects["meshes"].items():
            with self.subTest(mesh=name):
                payload = (OPENING / "Meshes" / name).read_bytes()
                self.assertEqual(hashlib.sha256(payload).hexdigest(), source["sha256"],
                                 "Restore the verified source bytes; do not change the expected hash.")

    def test_dialogue_layout_is_bound_to_verified_apk_components(self):
        receipt = json.loads((ROOT / "assets/reference/character-creation/opening-source-manifest.json")
                             .read_text(encoding="utf-8"))
        path = "unity/Assets/StreamingAssets/Opening/ui/opening-ui.json"
        output = next(row for row in receipt["outputs"] if row["path"] == path)
        self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), output["sha256"])
        sources = receipt["ui"]["layout_sources"]
        self.assertTrue(any(source["object_path"].endswith("/NpcName/NpcNameText")
                            and source["script"] == "ContentSizeFitter" for source in sources))
        self.assertTrue(all(source["origin"] == "apk_zip" and source["manifest"] == "854_156383"
                            and len(source["component_sha256"]) == 64 for source in sources))


if __name__ == "__main__":
    unittest.main()
