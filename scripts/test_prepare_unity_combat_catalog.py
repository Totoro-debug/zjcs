import hashlib
import json
import tempfile
import unittest
from pathlib import Path

import prepare_unity_combat_catalog as catalog


class UnityCombatCatalogRestoreTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.unity = self.root / "unity"
        self.sources = (
            ("assets/official/skill-rules.json", "assets/reference/skill-rules/source-manifest.json",
             "Assets/Official/Combat/skill-rules.json", "Assets/Official/Combat/skill-rules-source-manifest.json",
             "output_sha256"),
            ("assets/official/battle-rules.json", "assets/reference/battle-rules/source-manifest.json",
             "Assets/Official/Combat/battle-rules.json", "Assets/Official/Combat/battle-rules-source-manifest.json",
             "output_sha256"),
            ("assets/official/rules.json", "assets/reference/official-city/source-manifest.json",
             "Assets/Official/Combat/rules.json", "Assets/Official/Combat/rules-source-manifest.json",
             "data_sha256.rules.json"),
        )
        self.original_sources = catalog.SOURCES
        catalog.SOURCES = self.sources
        for index, (payload_name, manifest_name, _, _, manifest_field) in enumerate(self.sources):
            payload = self.root / payload_name
            manifest = self.root / manifest_name
            payload.parent.mkdir(parents=True, exist_ok=True)
            manifest.parent.mkdir(parents=True, exist_ok=True)
            payload.write_bytes(("payload-" + str(index)).encode("ascii"))
            digest = hashlib.sha256(payload.read_bytes()).hexdigest()
            if manifest_field == "output_sha256":
                record = {"manifest": catalog.EXPECTED_VERSION, "output_sha256": digest}
            else:
                record = {"manifest": catalog.EXPECTED_VERSION, "data_sha256": {"rules.json": digest}}
            manifest.write_text(json.dumps(record), encoding="utf-8")

    def tearDown(self):
        catalog.SOURCES = self.original_sources
        self.temporary.cleanup()

    def test_restore_is_verified_and_idempotent(self):
        first = catalog.restore(self.root, self.unity)
        second = catalog.restore(self.root, self.unity)

        self.assertEqual(len(first), 7)
        self.assertTrue(all(entry["restored"] for entry in first))
        self.assertTrue(all(not entry["restored"] for entry in second))
        module_manifest = json.loads((self.unity / catalog.MODULE_MANIFEST).read_text(encoding="utf-8"))
        self.assertEqual(module_manifest["manifest"], catalog.EXPECTED_VERSION)
        self.assertEqual(len(module_manifest["assets"]), 3)
        rules_manifest = json.loads((self.unity / self.sources[2][3]).read_text(encoding="utf-8"))
        self.assertEqual(rules_manifest["manifest"], catalog.EXPECTED_VERSION)
        self.assertEqual(rules_manifest["source_field"], "data_sha256.rules.json")
        for payload_name, _, target_name, _, _ in self.sources:
            self.assertEqual((self.unity / target_name).read_bytes(), (self.root / payload_name).read_bytes())

    def test_rejects_source_output_hash_mismatch(self):
        payload_name, manifest_name, _, _, _ = self.sources[0]
        manifest = json.loads((self.root / manifest_name).read_text(encoding="utf-8"))
        manifest["output_sha256"] = "0" * 64
        (self.root / manifest_name).write_text(json.dumps(manifest), encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "output hash differs"):
            catalog.restore(self.root, self.unity)

    def test_refuses_changed_payload_destination(self):
        target = self.unity / self.sources[0][2]
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(b"local change")

        with self.assertRaisesRegex(ValueError, "Refusing to overwrite"):
            catalog.restore(self.root, self.unity)

    def test_refreshes_only_payloads_verified_by_previous_module_manifest(self):
        catalog.restore(self.root, self.unity)
        payload_name, manifest_name, target_name, sidecar_name, _ = self.sources[2]
        payload = self.root / payload_name
        payload.write_bytes(b"new official rules payload")
        source_manifest = self.root / manifest_name
        source_data = json.loads(source_manifest.read_text(encoding="utf-8"))
        source_data["data_sha256"]["rules.json"] = hashlib.sha256(payload.read_bytes()).hexdigest()
        source_manifest.write_text(json.dumps(source_data), encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "Refusing to overwrite"):
            catalog.restore(self.root, self.unity)

        refreshed = catalog.restore(self.root, self.unity, replace_verified_stale=True)
        self.assertEqual((self.unity / target_name).read_bytes(), payload.read_bytes())
        self.assertTrue(next(item for item in refreshed if item["path"] == target_name)["restored"])
        self.assertTrue(next(item for item in refreshed if item["path"] == sidecar_name)["restored"])
        self.assertTrue(next(item for item in refreshed if item["path"] == catalog.MODULE_MANIFEST)["restored"])
        self.assertTrue(all(not item["restored"] for item in catalog.restore(self.root, self.unity)))

    def test_refresh_rejects_payload_not_matching_previous_manifest(self):
        catalog.restore(self.root, self.unity)
        target = self.unity / self.sources[2][2]
        target.write_bytes(b"local change")
        payload_name, manifest_name, _, _, _ = self.sources[2]
        payload = self.root / payload_name
        payload.write_bytes(b"new official rules payload")
        source_manifest = self.root / manifest_name
        source_data = json.loads(source_manifest.read_text(encoding="utf-8"))
        source_data["data_sha256"]["rules.json"] = hashlib.sha256(payload.read_bytes()).hexdigest()
        source_manifest.write_text(json.dumps(source_data), encoding="utf-8")

        with self.assertRaisesRegex(ValueError, "differs from its module manifest"):
            catalog.restore(self.root, self.unity, replace_verified_stale=True)


if __name__ == "__main__":
    unittest.main()
