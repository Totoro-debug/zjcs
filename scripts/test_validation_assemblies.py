"""Observable assembly staging behaviour on an isolated filesystem."""
import json
from pathlib import Path
import unittest

from validation_assemblies import stage_runtime_modules
from validation_workspace import temporary_validation_directory


class AssemblyStagingTests(unittest.TestCase):
    def setUp(self):
        self.work = temporary_validation_directory("assemblies-")
        self.addCleanup(self.work.cleanup)
        self.root = Path(self.work.name)
        self.runtime = self.root / "source/Assets/Game/Runtime"
        self.project = self.root / "project"

    def write(self, relative, data):
        path = self.runtime / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(data, encoding="utf8")
        return path

    def assembly(self, folder, name, references=(), guid="a" * 32):
        path = self.write(folder + "/code.asmdef", json.dumps(dict(name=name, references=list(references))))
        Path(str(path) + ".meta").write_text("guid: " + guid, encoding="utf8")
        self.write(folder + "/Code.cs", "// " + name)
        self.write(folder + "/Code.cs.meta", "code identity " + guid)

    def test_cycles_nested_ownership_and_both_asmref_forms(self):
        self.assembly("Root", "Root", ["Dependency", "Package"])
        self.assembly("Dependency", "Dependency", ["Root"], guid="b" * 32)
        self.assembly("Root/Nested", "Unselected", guid="c" * 32)
        for folder, reference in (("ByName", "Root"), ("ByGuid", "GUID:" + "a" * 32)):
            self.write(folder + "/link.asmref", json.dumps(dict(reference=reference)))
            self.write(folder + "/link.asmref.meta", "reference identity")
            self.write(folder + "/Linked.cs", "// linked")
        self.write("Root/texture.png", "payload stays with the fixture")
        self.write("Root/texture.png.meta", "asset identity")
        roots = ["Root"]
        selected = stage_runtime_modules(self.project, self.runtime, roots, allowed_external={"Package"})
        self.assertEqual(selected, {"Root", "Dependency"})
        self.assertEqual(roots, ["Root"])
        target = self.project / "Assets/Game/Runtime"
        expected = {"Root/code.asmdef", "Root/code.asmdef.meta", "Root/Code.cs", "Root/Code.cs.meta",
                    "Dependency/code.asmdef", "Dependency/code.asmdef.meta", "Dependency/Code.cs", "Dependency/Code.cs.meta",
                    "ByName/link.asmref", "ByName/link.asmref.meta", "ByName/Linked.cs",
                    "ByGuid/link.asmref", "ByGuid/link.asmref.meta", "ByGuid/Linked.cs"}
        self.assertEqual({p.relative_to(target).as_posix() for p in target.rglob("*") if p.is_file()}, expected)
        for relative in expected:
            self.assertEqual((target / relative).read_bytes(), (self.runtime / relative).read_bytes())

    def test_external_policy_fails_before_copy_and_exclusions_take_precedence(self):
        self.assembly("Root", "Root", ["Package", "Missing"])
        self.assembly("Package", "Package", guid="b" * 32)
        with self.assertRaisesRegex(ValueError, "Missing"):
            stage_runtime_modules(self.project, self.runtime, ["Root"], allowed_external=set())
        self.assertFalse(self.project.exists())
        self.assertEqual(stage_runtime_modules(self.project, self.runtime, ["Root"],
                         excluded={"Package"}), {"Root"})
        self.assertFalse((self.project / "Assets/Game/Runtime/Package").exists())

    def test_implicit_inputs_keep_source_bytes_and_meta(self):
        self.assembly("Serialization", "Zjcs.Official.SharedSerialization")
        self.assembly("Progression", "Zjcs.Offline.Progression", guid="b" * 32)
        inputs = ["Assets/Official/World/Map/14/SharedSerialization/MessagePack.dll",
                  "Assets/Game/Resources/Map14/world-alone-preparation.json",
                  "Assets/Game/Resources/Map16/world-alone-configuration.json"]
        for relative in inputs:
            for suffix in ("", ".meta"):
                path = self.runtime.parents[2] / (relative + suffix)
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes((relative + suffix).encode())
        stage_runtime_modules(self.project, self.runtime,
                              ["Zjcs.Official.SharedSerialization", "Zjcs.Offline.Progression"])
        for relative in inputs:
            for suffix in ("", ".meta"):
                self.assertEqual((self.project / (relative + suffix)).read_bytes(),
                                 (self.runtime.parents[2] / (relative + suffix)).read_bytes())


if __name__ == "__main__":
    unittest.main()
