import json
from pathlib import Path
import tempfile
import unittest

from audit_mainline_entity_script_bindings import build, script_file_id


class MainlineEntityScriptBindingAuditTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.roots_path = self.root / "roots.json"
        self.export_path = self.root / "native-export-source-manifest.json"
        self.source_root = self.root / "official-code"
        self.runtime_root = self.root / "Runtime/Official"
        self.prefab_path = self.root / "native-export/ExportedProject/Assets/Prefabs/Map/Test.prefab"
        self.prefab_path.parent.mkdir(parents=True)
        self.prefab_path.write_text(
            "--- !u!114 &1\nMonoBehaviour:\n  m_Script: {fileID: "
            + str(script_file_id("Demo", "MapEntity"))
            + ", guid: 06dd3d5416bf43ea368cc39ff96e6d5a, type: 3}\n"
            + "  serializedConfig: 4\n"
            + "  m_Script: {fileID: 5, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 3}\n",
            encoding="utf-8",
        )
        self.source_file = self.source_root / "Demo/MapEntity.cs"
        self.source_file.parent.mkdir(parents=True)
        self.source_file.write_text(
            "namespace Demo\n{\n    public class MapEntity\n    {\n        public int serializedConfig;\n    }\n}\n",
            encoding="utf-8",
        )
        self.runtime_file = self.runtime_root / "Demo/MapEntity.cs"
        self.runtime_file.parent.mkdir(parents=True)
        self.runtime_file.write_text(
            "namespace Demo\n{\n    public class MapEntity\n    {\n        public int serializedConfig;\n    }\n}\n",
            encoding="utf-8",
        )
        Path(str(self.runtime_file) + ".meta").write_text(
            "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nMonoImporter:\n",
            encoding="utf-8",
        )
        self.prefab = "Assets/Prefabs/Map/Test.prefab"
        self.roots_path.write_text(json.dumps({
            "manifest": "884_156692", "mapClassId": 16,
            "prefabs": [{"path": self.prefab}],
        }), encoding="utf-8")
        raw = self.prefab_path.read_bytes()
        self.export_path.parent.mkdir(parents=True, exist_ok=True)
        self.export_path.write_text(json.dumps({
            "manifest": "884_156692", "completed": True, "export_directory": "native-export",
            "outputs": [{"path": "ExportedProject/" + self.prefab,
                         "sha256": self.sha(raw), "bytes": len(raw)}],
        }), encoding="utf-8")

    def tearDown(self):
        self.temp.cleanup()

    @staticmethod
    def sha(raw):
        import hashlib
        return hashlib.sha256(raw).hexdigest()

    def test_resolves_source_type_and_exact_runtime_script_guid(self):
        result = build(16, self.roots_path, self.export_path,
                       self.source_root, self.runtime_root, self.root)
        self.assertEqual(result["counts"], {
            "client_script_ids": 1, "source_resolved": 1,
            "runtime_type_matches": 1, "runtime_type_missing": 0,
            "runtime_name_candidates": 1,
            "candidate_field_name_matches": 1,
            "candidate_field_type_differences": 0,
            "candidate_field_types_match": 1,
        })
        script = result["scripts"][0]
        self.assertEqual((script["namespace"], script["type"]), ("Demo", "MapEntity"))
        self.assertEqual(script["runtime"][0]["guid"], "0123456789abcdef0123456789abcdef")
        self.assertEqual(script["runtime_name_candidates"], script["runtime"])
        self.assertEqual(script["serialized_fields"], ["serializedConfig"])
        self.assertEqual(script["runtime_name_candidates"][0]["serialized_field_names_missing"], [])
        self.assertEqual(script["runtime_name_candidates"][0]["serialized_field_types_unresolved"], [])

    def test_rejects_prefab_that_does_not_match_export_receipt(self):
        self.prefab_path.write_text("changed", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "differs from its export receipt"):
            build(16, self.roots_path, self.export_path,
                  self.source_root, self.runtime_root, self.root)

    def test_reports_renamed_namespace_as_candidate_without_direct_binding(self):
        self.runtime_file.write_text(
            "namespace MigratedDemo\n{\n    public class MapEntity\n    {\n        public int serializedConfig;\n    }\n}\n",
            encoding="utf-8")
        result = build(16, self.roots_path, self.export_path,
                       self.source_root, self.runtime_root, self.root)
        self.assertEqual(result["counts"]["runtime_type_matches"], 0)
        self.assertEqual(result["counts"]["runtime_name_candidates"], 1)
        self.assertEqual(result["scripts"][0]["runtime_name_candidates"][0]["guid"],
                         "0123456789abcdef0123456789abcdef")

    def test_reports_serialized_field_names_missing_from_candidate(self):
        self.runtime_file.write_text("namespace Demo\n{\n    public class MapEntity {}\n}\n", encoding="utf-8")
        result = build(16, self.roots_path, self.export_path,
                       self.source_root, self.runtime_root, self.root)
        self.assertEqual(result["scripts"][0]["runtime_name_candidates"][0]["serialized_field_names_missing"],
                         ["serializedConfig"])
        self.assertEqual(result["counts"]["candidate_field_name_matches"], 0)

    def test_reports_serialized_field_type_differences(self):
        self.runtime_file.write_text(
            "namespace Demo\n{\n    public class MapEntity\n    {\n        public long serializedConfig;\n    }\n}\n",
            encoding="utf-8",
        )
        result = build(16, self.roots_path, self.export_path,
                       self.source_root, self.runtime_root, self.root)
        self.assertEqual(result["scripts"][0]["runtime_name_candidates"][0]["serialized_field_type_differences"], [{
            "field": "serializedConfig", "source_types": ["int"], "runtime_types": ["long"],
        }])
        self.assertEqual(result["counts"]["candidate_field_type_differences"], 1)
        self.assertEqual(result["counts"]["candidate_field_types_match"], 0)

    def test_preserves_unresolved_client_script_ids(self):
        unknown = script_file_id("Demo", "MissingEntity")
        self.prefab_path.write_text(
            "--- !u!114 &1\nMonoBehaviour:\n  m_Script: {fileID: " + str(unknown)
            + ", guid: 06dd3d5416bf43ea368cc39ff96e6d5a, type: 3}\n",
            encoding="utf-8",
        )
        raw = self.prefab_path.read_bytes()
        export = json.loads(self.export_path.read_bytes())
        export["outputs"][0].update(sha256=self.sha(raw), bytes=len(raw))
        self.export_path.write_text(json.dumps(export), encoding="utf-8")
        result = build(16, self.roots_path, self.export_path,
                       self.source_root, self.runtime_root, self.root)
        self.assertIsNone(result["scripts"][0]["type"])
        self.assertEqual(result["counts"]["source_resolved"], 0)
        self.assertEqual(result["counts"]["runtime_type_missing"], 1)


if __name__ == "__main__":
    unittest.main()
