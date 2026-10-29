import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import prepare_unity_equipment_catalog as catalog


class UnityEquipmentCatalogRestoreTests(unittest.TestCase):
    def test_restore_verifies_sources_and_is_idempotent(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source"
            unity = root / "unity"
            payloads = {
                "assets/official/rules.json": b'{"item":[],"equip":[]}',
                "assets/official/equipment-rules.json": b'{"tables":{},"props":{}}',
                "assets/official/fidelity-rules.json": b'{"system_level_limit":[],"system_level_gap_limit":[]}',
            }
            for name, payload in payloads.items():
                path = source / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(payload)
            payload = payloads["assets/official/rules.json"]
            source_manifest = source / "assets/reference/official-city/source-manifest.json"
            source_manifest.parent.mkdir(parents=True)
            digest = hashlib.sha256(payload).hexdigest()
            source_manifest.write_text(json.dumps({
                "manifest": catalog.MANIFEST_VERSION,
                "data_sha256": {"rules.json": digest},
            }), encoding="utf-8")
            fidelity = source / "assets/reference/equipment-fidelity/source-manifest.json"
            fidelity.parent.mkdir(parents=True)
            equipment_digest = hashlib.sha256(payloads["assets/official/equipment-rules.json"]).hexdigest()
            fidelity.write_text(json.dumps({
                "manifest": catalog.MANIFEST_VERSION,
                "outputs": {"equipment-rules.json": equipment_digest},
            }), encoding="utf-8")
            completion = source / "assets/reference/equipment-completion/source-manifest.json"
            completion.parent.mkdir(parents=True)
            completion_digest = hashlib.sha256(payloads["assets/official/fidelity-rules.json"]).hexdigest()
            sprite_path = source / "assets/official/sprites/equipment-test.png"
            sprite_path.parent.mkdir(parents=True)
            sprite_content = b"official equipment sprite"
            sprite_path.write_bytes(sprite_content)
            sprite_resource = "res://assets/official/sprites/equipment-test.png"
            panel_source = {
                "bundle": "test-equipment.bundle",
                "bundle_hash": "0123456789abcdef0123456789abcdef",
                "path_id": "10",
                "prefab": "/Equipment.prefab",
            }
            panels = {name: {"source": dict(panel_source)} for name in catalog.UI_PANELS}
            panels[catalog.UI_PANELS[0]] = {"source": dict(panel_source), "root": {"image": {"sprite": {
                "file": sprite_resource, "name": "equipment-test", "width": 8, "height": 8,
                "border": [1, 2, 3, 4], "ppu": 100, "pivot": [4, 4],
            }}}}
            ui_content = json.dumps(panels, ensure_ascii=False).encode("utf-8")
            ui_path = source / catalog.UI_SOURCE
            ui_path.parent.mkdir(parents=True, exist_ok=True)
            ui_path.write_bytes(ui_content)
            completion.write_text(json.dumps({
                "manifest": catalog.MANIFEST_VERSION,
                "output_sha256": {
                    "fidelity-rules.json": completion_digest,
                    "ui.json": hashlib.sha256(ui_content).hexdigest(),
                },
            }), encoding="utf-8")
            feedback = source / "assets/reference/equipment-feedback/source-manifest.json"
            feedback.parent.mkdir(parents=True)
            feedback.write_text(json.dumps({
                "manifest": catalog.MANIFEST_VERSION,
                "inheritance_prefab": dict(panel_source),
            }), encoding="utf-8")
            city_data = json.loads(source_manifest.read_text(encoding="utf-8"))
            city_data["panel_prefabs"] = {name: dict(panel_source) for name in catalog.UI_PANELS}
            city_data["resources"] = {sprite_resource: {
                "sha256": hashlib.sha256(sprite_content).hexdigest(),
            }}
            source_manifest.write_text(json.dumps(city_data), encoding="utf-8")
            restored = catalog.restore(source, unity)
            self.assertEqual(22, len(restored))
            self.assertTrue(all(item["restored"] for item in restored))
            self.assertTrue(all(not item["restored"] for item in catalog.restore(source, unity)))
            self.assertTrue((unity / "Assets/Official/Equipment/Runtime/equipment-runtime-rules.json").is_file())
            self.assertTrue((unity / "Assets/Official/Equipment/UI/equipment-ui.json").is_file())
            self.assertEqual(sprite_content, (unity / "Assets/Official/Equipment/UI/Sprites/equipment-test.png").read_bytes())

    def test_refuses_existing_payload_with_different_hash(self):
        with tempfile.TemporaryDirectory() as temporary:
            target = Path(temporary) / "payload.json"
            target.write_bytes(b"local edit")
            with self.assertRaisesRegex(ValueError, "Refusing to overwrite"):
                catalog.copy_verified(b"official", target, hashlib.sha256(b"official").hexdigest())

    def test_rejects_output_that_disagrees_with_source_manifest(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source = root / "source"
            unity = root / "unity"
            for relative in catalog.SOURCE_MANIFESTS:
                path = source / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(json.dumps({"manifest": catalog.MANIFEST_VERSION}), encoding="utf-8")
            for source_name, manifest_name, field in catalog.SOURCES:
                path = source / source_name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"incorrect")
                manifest_path = source / manifest_name
                data = json.loads(manifest_path.read_text(encoding="utf-8"))
                section, _, key = field.partition(".")
                data[section] = {key: hashlib.sha256(b"expected").hexdigest()}
                manifest_path.write_text(json.dumps(data), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "differs from its source manifest"):
                catalog.restore(source, unity)


if __name__ == "__main__":
    unittest.main()
