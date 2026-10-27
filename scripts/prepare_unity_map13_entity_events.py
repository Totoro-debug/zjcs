"""Migrate the original world-entity event dispatch implementation.

Preserve receiver snapshots, duplicate receiver registration, thread-local list
pools and the original delegate dictionary lifetime. Only diagnostics and the
namespace are adapted; this does not instantiate world entities.
"""
import json
from pathlib import Path
import sys

_python = Path(__file__).resolve().parents[1] / ".tools/map13-entity-native-cache-871_156598/python"
if (_python / "UnityPy/__init__.py").is_file():
    sys.path.insert(0, str(_python))

from official_apk import ROOT
from official_source import OfficialSource
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_water_components import import_meta


NAMESPACE = "Zjcs.Official.World.Map13EntityEvents"
DESTINATION = ROOT / "unity/Assets/Game/Runtime/Official/World/Map13EntityEvents"
RECEIPT = ROOT / "assets/reference/official-map/map13-entity-events-source-manifest.json"
TYPES = ("IEventReceiver", "IEventDelegate", "EventDelegateBasicBase", "EventDelegateBase")


def adapt(original):
    lines, result, diagnostics = original.splitlines(), [], 0
    index = 0
    while index < len(lines):
        line = lines[index]
        if line.startswith("using Framework.Logging"):
            index += 1
            continue
        if "GLoggerErrorInterpolatedStringHandler message = " in line:
            indent = line[:len(line) - len(line.lstrip())]
            block = []
            while index < len(lines):
                block.append(lines[index])
                index += 1
                if "Log.Error(" in block[-1]:
                    break
            else:
                raise ValueError("Official diagnostic block is not complete")
            if "Log.Error(exception," in block[-1]:
                result.append(indent + "UnityEngine.Debug.LogException(exception);")
            else:
                literal = next((part.split('AppendLiteral("', 1)[1].split('"', 1)[0]
                                for part in block if 'AppendLiteral("' in part), None)
                if literal is None:
                    raise ValueError("Official error diagnostic has no literal")
                result.append(indent + "UnityEngine.Debug.LogError(" + json.dumps(literal) + ");")
            diagnostics += 1
            continue
        if line == "namespace Common;":
            result.extend(["namespace " + NAMESPACE, "{"])
        else:
            result.append(line)
        index += 1
    result.append("}")
    return ("// Migrated original entity event dispatch; see Map13 source receipt.\n" +
            "\n".join(result) + "\n").encode(), diagnostics


def build():
    source = OfficialSource()
    code = ROOT / ".tools/source-packages/current" / source.version / "official-code"
    manifest_path = code.parent / "code-manifest.json"
    manifest = json.loads(manifest_path.read_bytes())
    assembly = code / "managed/decoded/Console.dll"
    if manifest["manifest"] != source.version or sha(assembly.read_bytes()) != manifest["assemblies"]["Console.dll"]["decoded_sha256"]:
        raise ValueError("Official entity-event assembly differs from the selected version")
    inputs, outputs, diagnostics = [], [], 0
    for name in TYPES:
        path = code / "common-source/Common" / (name + ".cs")
        data, count = adapt(path.read_text(encoding="utf8"))
        expected = {"EventDelegateBasicBase": 2, "EventDelegateBase": 15}.get(name, 0)
        if count != expected:
            raise ValueError("Official event diagnostic scope differs: " + name)
        diagnostics += count
        target = DESTINATION / path.name
        write(target, data)
        import_meta(target, "MonoImporter")
        inputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
        outputs.append(dict(path=source_path(target), sha256=sha(data)))
    asmdef = DESTINATION / (NAMESPACE + ".asmdef")
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, references=[],
        includePlatforms=[], excludePlatforms=[], allowUnsafeCode=False,
        overrideReferences=False, precompiledReferences=[], autoReferenced=True,
        defineConstraints=[], versionDefines=[], noEngineReferences=False)))
    import_meta(asmdef, "AssemblyDefinitionImporter")
    import_meta(DESTINATION, "DefaultImporter", folder=True)
    outputs.append(dict(path=source_path(asmdef), sha256=sha(asmdef.read_bytes())))
    write(RECEIPT, encode(dict(format=1, manifest=source.version, mapClassId=13,
        assembly=dict(path=source_path(assembly), sha256=sha(assembly.read_bytes())),
        code_manifest=dict(path=source_path(manifest_path), sha256=sha(manifest_path.read_bytes())),
        inputs=inputs, outputs=outputs,
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        adaptation="Namespace and 17 diagnostic blocks only. Original zero-to-four argument generic dispatch, receiver snapshot, nested dispatch, thread-local list pool, duplicate receiver behavior, Merge/Clear and delegate dictionary retention are unchanged.",
        limitations=["This is event dispatch only. Entity/container lifecycle, view binding, map interaction and saves still require migration."])))
    print("Original entity-event dispatch migrated:", len(TYPES), "source files;", diagnostics, "diagnostic adaptations")


if __name__ == "__main__":
    build()
