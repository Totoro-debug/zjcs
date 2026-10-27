"""Prepare exact original Map13 story performance CAB closure for native conversion."""
import json
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map13_sources import encode, meta, sha, source_path, write

VERSION = "871_156598"
CACHE = ROOT / ".tools/map13-story-playable-native-cache-871_156598"
SOURCE = ROOT / "unity/Assets/Official/World/Map/13/Source/story-playables.json"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map13-story-playables-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Official/World/Map/13/Source/native-story-playable-components.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-story-playable-input-source-manifest.json"


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def build():
    receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    expected = {r["path"]: r["sha256"] for r in receipt["outputs"]}
    if receipt["manifest"] != VERSION or expected.get(source_path(SOURCE)) != sha(SOURCE.read_bytes()):
        raise ValueError("Story performance source does not match the selected source receipt")
    source = json.loads(SOURCE.read_bytes())
    root_bundles = sorted({p["selected_asset"]["bundle"] for p in source["prefabs"]})
    tool_paths = [Path(__file__).resolve()] + [ROOT / "scripts" / name for name in
        ("prepare_unity_map13_water_entity_inputs.py", "official_managed_reference_conversion.py",
         "map13_native_schemas.py", "map13_entity_native_schemas.py", "official_manifest.py",
         "official_unity_schema.py", "unity_schema_types.cs", "official_metadata.py",
         "read_official_native.py", "prepare_unity_bundle_inputs.py")]
    tools = [record(path) for path in tool_paths]
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Wrong selected story performance version")
    assets, missing = collect(apk, root_bundles)
    if missing:
        raise ValueError("Original story performance CAB closure is incomplete: " + repr(missing))
    print("Original story performance CAB closure:", len(apk.loaded), "bundles", flush=True)
    code = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
    code_manifest = code.parent / "code-manifest.json"
    manifest = json.loads(code_manifest.read_bytes())
    if manifest["manifest"] != VERSION:
        raise ValueError("Wrong official assembly version")
    assemblies = []
    for name, entry in manifest["assemblies"].items():
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != entry["decoded_sha256"]:
            raise ValueError("Changed original assembly: " + name)
        assemblies.append(record(path))
    schemas = NativeSchemas(code)
    proof = install(schemas, code)
    inputs, components, changes, unions, errors, count = [], [], [], [], [], 0
    for name in sorted(apk.loaded):
        original, entry = read_source(apk, name)
        if name.startswith("assets/bin/Data/"):
            converted, fields, changed, typed = original, [], [], []
            checked = sum(len(file.objects) for file in serialized_files(apk.loaded[name]))
        else:
            try:
                converted, fields, changed, typed, checked = hydrate(apk.loaded[name], schemas, include_source_identity=True)
            except Exception as error:
                errors.append(dict(bundle=name, error=str(error), cause=str(error.__cause__)))
                print("Unresolved original performance schema:", errors[-1], flush=True)
                continue
        components.extend(fields); changes.extend(changed); unions.extend(typed); count += checked
        entry.update(output=Path(name).name, converter_input_sha256=sha(converted), converter_input_bytes=len(converted),
                     restored_components=len(fields), changed_payloads=len(changed), checked_objects=checked)
        write(CACHE / "inputs" / entry["output"], converted)
        inputs.append(entry)
        if fields:
            print("Original performance input:", name, len(fields), "components", len(changed), "registries", flush=True)
    if errors:
        write(CACHE / "unresolved-schemas.json", encode(errors))
        raise ValueError("Resolve every original performance schema before conversion: " + str(len(errors)))
    for path, entry in zip(tool_paths, tools):
        if sha(path.read_bytes()) != entry["sha256"]:
            raise ValueError("Conversion source changed during extraction; rerun: " + str(path))
    write(OUTPUT, encode(dict(manifest=VERSION, mapClassId=13, components=components)))
    meta(OUTPUT)
    result = dict(format=1, manifest=VERSION, mapClassId=13, roots=root_bundles,
        selection=dict(source=record(SOURCE), prefabs=source["prefabs"]),
        inputs=inputs, serialized_files=list(assets.values()), assemblies=assemblies,
        code_manifest=record(code_manifest), native_schema=proof, tools=tools,
        managed_reference_unions=unions, converted_objects=changes,
        outputs=[record(OUTPUT)], counts=dict(prefabs=len(source["prefabs"]), root_bundles=len(root_bundles),
            bundles=len(inputs), components=len(components), checked_objects=count, converted_registries=len(changes)),
        operation="Close all actual original story performance CAB references. Strictly restore original fields, preserve each reference RID, descriptor, pointer and order through reversible converter schemas, reopen and compare all fields and unmodified object bytes. Temporary identity fields are verified and must be stripped during export restoration. Original APK/cloud bytes are retained unchanged.",
        limitations=["Native conversion inputs only. External runtime-selected appearance/animation/audio resources, restored native prefabs and Timeline references, official runtime scripts, Windows shaders and actual performance playback remain required."])
    write(RECEIPT, encode(result)); write(CACHE / "inputs/source-manifest.json", encode(result))
    (CACHE / "unresolved-schemas.json").unlink(missing_ok=True)
    print("Original performance inputs prepared:", result["counts"], flush=True)
    return result


if __name__ == "__main__":
    build()
