"""Restore original mountain bundle schemas for native Windows asset conversion."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_native import hydrate
from prepare_unity_map12_sources import encode, meta, sha, source_path, write


ROOTS = ["defaultpackage_assets_partition_mapground_12_" + suffix + ".bundle"
         for suffix in ("mapground_12_bare", "prefabs", "chunks", "extensions", "prefabs_n")]
OUTPUT = ROOT / "unity/Assets/Official/World/Map/12/Source/native-components.json"
RECEIPT = ROOT / "assets/reference/official-map/map12-native-input-source-manifest.json"


def build(destination):
    destination = destination.resolve()
    if not destination.is_relative_to(ROOT / "output") or destination == ROOT / "output":
        raise ValueError("Converter inputs must stay in the repository output directory")
    if destination.exists():
        raise FileExistsError("Refusing to overwrite existing converter inputs")
    apk = Apk.current()
    assets, missing = collect(apk, ROOTS)
    if missing:
        raise ValueError("Mountain CAB dependency closure is incomplete: " + str(missing))
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    code_manifest_path = code.parent / "code-manifest.json"
    code_manifest = json.loads(code_manifest_path.read_bytes())
    if code_manifest["manifest"] != apk.version or code_manifest["apk_sha256"] != apk.source.apk_sha256:
        raise ValueError("Native schemas do not belong to the selected official version")
    assemblies = []
    for name, record in code_manifest["assemblies"].items():
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != record["decoded_sha256"]:
            raise ValueError("Official schema assembly differs from its receipt: " + name)
        assemblies.append(dict(path=source_path(path), sha256=record["decoded_sha256"]))
    schemas = NativeSchemas(code)
    destination.mkdir(parents=True)
    inputs, components, trees, object_count = [], [], {}, 0
    for name in sorted(apk.loaded):
        decoded, record = read_source(apk, name)
        if name.startswith("assets/bin/Data/"):
            content, fields, descriptions = decoded, [], []
            count = sum(len(file.objects) for file in serialized_files(apk.loaded[name]))
        else:
            content, fields, descriptions, count = hydrate(apk.loaded[name], schemas)
        components.extend(fields)
        object_count += count
        for description in descriptions:
            trees[encode(description)] = description
        record.update(output=Path(name).name, converter_input_sha256=sha(content), converter_input_bytes=len(content))
        write(destination / record["output"], content)
        inputs.append(record)
        print("Restored", name, len(fields), "components,", count, "unchanged objects", flush=True)
    builtin = "assets/bin/Data/unity default resources"
    builtin_raw = apk.archive.read(builtin)
    builtin_input = next((record for record in inputs if record["name"] == builtin), None)
    if builtin_input and builtin_input["converter_input_sha256"] != sha(builtin_raw):
        raise ValueError("Built-in resources were modified")
    if not builtin_input:
        write(destination / Path(builtin).name, builtin_raw)
    write(OUTPUT, encode(dict(manifest=apk.version, mapClassId=12, components=components)))
    meta(OUTPUT)
    tools = [Path(__file__), ROOT / "scripts/prepare_unity_map13_native.py", ROOT / "scripts/map13_native_schemas.py",
             ROOT / "scripts/official_unity_schema.py", ROOT / "scripts/unity_schema_types.cs"]
    metadata = ROOT / ".tools/source-packages/official-code/global-metadata.dat"
    result = dict(format=1, manifest=apk.version, mapClassId=12, roots=ROOTS, inputs=inputs,
        builtins=[dict(apk_entry=builtin, apk_sha256=apk.source.apk_sha256, sha256=sha(builtin_raw))],
        serialized_files=list(assets.values()), schemas=list(trees.values()),
        metadata=dict(path=source_path(metadata), sha256=sha(metadata.read_bytes())),
        code_manifest=dict(path=source_path(code_manifest_path), sha256=sha(code_manifest_path.read_bytes())),
        assemblies=assemblies,
        reference_assemblies=[dict(path=path.as_posix(), sha256=sha(path.read_bytes())) for path in schemas.reference_inputs],
        tools=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in tools],
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(OUTPUT.read_bytes()))],
        counts=dict(bundles=len(inputs), restored_components=len(components), unchanged_objects=object_count),
        operation="Restore stripped schemas from selected official assemblies and pinned APK metadata throughout the CAB closure. Reuse the verified native schema hydrator; preserve every original object payload byte, Unity version, Android target, CAB identity and pointer; independently reread all restored component fields. These are converter inputs, not Windows runtime bundles.",
        limitations=["Native C# bindings, shader translation, Windows import and rendered gameplay remain required."])
    write(RECEIPT, encode(result))
    write(destination / "source-manifest.json", encode(result))
    apk.archive.close()
    print("Mountain native converter inputs:", result["counts"], flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", type=Path, required=True)
    build(parser.parse_args().inputs)
