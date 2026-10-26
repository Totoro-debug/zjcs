"""Copy selected-version official native water components without rule rewrites."""
from pathlib import Path
import json

from official_apk import ROOT
from official_source import OfficialSource
from prepare_unity_map13_sources import encode, sha, source_path, write


DESTINATION = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeWater"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-water-code-source-manifest.json"
COMPONENTS = ("WaterController", "PartialSubmersion", "SeaRendererReplacement", "FordingSpriteRenderer")


def import_meta(path, importer, folder=False):
    identity = source_path(path)
    import hashlib
    guid = hashlib.md5(("zjcs-map13-native:" + identity).encode()).hexdigest()
    value = "fileFormatVersion: 2\nguid: " + guid + "\n"
    if folder: value += "folderAsset: yes\n"
    value += importer + ":\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    target = Path(str(path) + ".meta")
    if target.exists() and target.read_text().splitlines() != value.splitlines():
        raise ValueError("Refusing unrelated Unity metadata: " + str(target))
    write(target, value.encode())


def build():
    source = OfficialSource()
    code = ROOT / ".tools/source-packages/current" / source.version / "official-code"
    manifest_path = code.parent / "code-manifest.json"
    manifest = json.loads(manifest_path.read_text())
    assembly = code / "managed/decoded/CustomWater.dll"
    if manifest["manifest"] != source.version or sha(assembly.read_bytes()) != manifest["assemblies"]["CustomWater.dll"]["decoded_sha256"]:
        raise ValueError("CustomWater.dll does not match the selected official code receipt")
    tool_receipt = ROOT / ".tools/map13-ilspy/source.json"
    tool = json.loads(tool_receipt.read_text())
    package = ROOT / ".tools/map13-ilspy/ilspycmd.11.1.0.9782.nupkg"
    if sha(package.read_bytes()) != tool["sha256"]:
        raise ValueError("ILSpy converter package hash differs")
    outputs, inputs = [], []
    for name in COMPONENTS:
        original = code / "customwater-source" / (name + ".cs")
        text = original.read_text(encoding="utf8")
        # Only the namespace changes. Original serialized names, statements,
        # constants, callbacks and ordering are retained verbatim.
        imported = ("// Migrated from selected official CustomWater.dll; see Map13 source receipt.\n"
                    "namespace Zjcs.Official.World.NativeWater\n{\n" + text + "\n}\n").encode("utf8")
        path = DESTINATION / original.name
        write(path, imported)
        import_meta(path, "MonoImporter")
        inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes())))
        outputs.append(dict(path=source_path(path), sha256=sha(imported), original_class=name))
    asmdef = DESTINATION / "Zjcs.Official.World.NativeWater.asmdef"
    write(asmdef, encode(dict(name="Zjcs.Official.World.NativeWater", rootNamespace="Zjcs.Official.World.NativeWater",
        references=[], includePlatforms=[], excludePlatforms=[], allowUnsafeCode=False,
        overrideReferences=False, precompiledReferences=[], autoReferenced=True,
        defineConstraints=[], versionDefines=[], noEngineReferences=False)))
    import_meta(asmdef, "AssemblyDefinitionImporter")
    import_meta(DESTINATION, "DefaultImporter", folder=True)
    write(RECEIPT, encode(dict(format=1, mapClassId=13, manifest=source.version,
        assembly=dict(path=source_path(assembly), sha256=sha(assembly.read_bytes())),
        code_manifest=dict(path=source_path(manifest_path), sha256=sha(manifest_path.read_bytes())),
        decompiler=dict(tool, entry="ilspycmd.dll", arguments=["-p", "CustomWater.dll", "-r", "managed/decoded", "-r", "UnityEngine"]),
        inputs=inputs, outputs=outputs + [dict(path=source_path(asmdef), sha256=sha(asmdef.read_bytes()))],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        adaptation="Only enclose original decompiled source in the Zjcs.Official.World.NativeWater namespace; no numerical, state, callback or execution-order changes.",
        limitations=["WaterMaskRenderer requires original rendering pipeline/shader restoration and is not included yet.",
                      "Native prefab script and texture bindings, complete scene rendering and Play Mode verification remain required."])))
    print("Imported official Map13 native water components:", ", ".join(COMPONENTS))


if __name__ == "__main__":
    build()
