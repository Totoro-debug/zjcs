import json
from pathlib import Path
import tempfile
import unittest

from verify_mainline_entity_native_exports import build


class MainlineEntityNativeExportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.roots_path = self.root / "roots.json"
        self.export_path = self.root / "native-conversion/native-export-all-source-manifest.json"
        self.asset_root = self.export_path.parent / "native-export-all/ExportedProject/Assets"
        self.prefab = "Assets/Prefabs/Map/Test/100.prefab"
        self.prefab_path = self.asset_root / "Prefabs/Map/Test/100.prefab"
        self.meta_path = Path(str(self.prefab_path) + ".meta")
        self.prefab_path.parent.mkdir(parents=True)
        self.prefab_bytes = b"prefab yaml"
        self.meta_bytes = b"guid: 0123456789abcdef0123456789abcdef\n"
        self.prefab_path.write_bytes(self.prefab_bytes)
        self.meta_path.write_bytes(self.meta_bytes)
        roots = {"manifest": "884_156692", "mapClassId": 16,
                 "prefabs": [{"path": self.prefab, "source": {"bundle": "test.bundle"},
                              "object_sha256": "source-object"}]}
        self.roots_path.write_text(json.dumps(roots), encoding="utf-8")
        outputs = []
        for suffix, raw in (("", self.prefab_bytes), (".meta", self.meta_bytes)):
            path = "ExportedProject/" + self.prefab + suffix
            outputs.append({"path": path, "sha256": self.sha(raw), "bytes": len(raw)})
        export = {"manifest": "884_156692", "completed": True,
                  "export_directory": "native-export-all", "outputs": outputs}
        self.export_path.parent.mkdir(parents=True, exist_ok=True)
        self.export_path.write_text(json.dumps(export), encoding="utf-8")

    def tearDown(self):
        self.temp.cleanup()

    @staticmethod
    def sha(raw):
        import hashlib
        return hashlib.sha256(raw).hexdigest()

    def test_build_records_verified_prefab_and_meta(self):
        result = build(self.roots_path, self.export_path, self.root)
        self.assertEqual(result["mapClassId"], 16)
        self.assertEqual(result["counts"]["prefabs"], 1)
        self.assertEqual(result["outputs"][0]["prefab"]["sha256"], self.sha(self.prefab_bytes))
        self.assertEqual(result["outputs"][0]["meta"]["sha256"], self.sha(self.meta_bytes))
        self.assertIn("Windows script bindings", result["limitations"][0])

    def test_rejects_mismatched_source_version(self):
        export = json.loads(self.export_path.read_bytes())
        export["manifest"] = "871_156598"
        self.export_path.write_text(json.dumps(export), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "same source version"):
            build(self.roots_path, self.export_path, self.root)

    def test_rejects_changed_prefab_bytes(self):
        self.prefab_path.write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "differs from its export receipt"):
            build(self.roots_path, self.export_path, self.root)

    def test_rejects_missing_metadata(self):
        self.meta_path.unlink()
        with self.assertRaisesRegex(FileNotFoundError, "output is missing"):
            build(self.roots_path, self.export_path, self.root)

    def test_rejects_parent_traversal(self):
        roots = json.loads(self.roots_path.read_bytes())
        roots["prefabs"][0]["path"] = "Assets/Prefabs/../outside.prefab"
        self.roots_path.write_text(json.dumps(roots), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "outside the expected export root"):
            build(self.roots_path, self.export_path, self.root)

    def test_rejects_windows_separator_in_export_path(self):
        roots = json.loads(self.roots_path.read_bytes())
        roots["prefabs"][0]["path"] = "Assets/Prefabs\\..\\outside.prefab"
        self.roots_path.write_text(json.dumps(roots), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "outside the expected export root"):
            build(self.roots_path, self.export_path, self.root)

    def test_rejects_windows_separator_in_export_directory(self):
        export = json.loads(self.export_path.read_bytes())
        export["export_directory"] = "native-export-all\\..\\outside"
        self.export_path.write_text(json.dumps(export), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "directory is invalid"):
            build(self.roots_path, self.export_path, self.root)


if __name__ == "__main__":
    unittest.main()
