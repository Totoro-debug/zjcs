"""Recover the five original entity Timeline CAB closures without rebuilding the map.

Every converter input must reproduce the previously verified full entity input
hash. This is a selection of original assets, not a rewrite of their tracks.
"""
import argparse
import json
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map14_entity_native_schemas import install
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map13_sources import encode, meta, sha, source_path, write

CACHE = ROOT / ".tools/source-packages/conversion-cache/871_156598/map14-entity-timelines"
SOURCE = ROOT / "unity/Assets/Official/World/Map/14/Source/entity-timelines.json"
COMPONENTS = ROOT / "unity/Assets/Official/World/Map/14/Source/native-entity-timeline-components.json"
RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-timeline-input-source-manifest.json"
EXPORT_RECEIPT = ROOT / "assets/reference/official-map/map14-native-entity-timeline-export-source-manifest.json"


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def build(unity_project):
    prior_path = ROOT / "assets/reference/official-map/map14-native-entity-input-source-manifest.json"
    source_proof = ROOT / "assets/reference/official-map/map14-entity-timelines-source-manifest.json"
    prior = json.loads(prior_path.read_bytes())
    proof = json.loads(source_proof.read_bytes())
    if prior["manifest"] != "871_156598" or proof["manifest"] != prior["manifest"]:
        raise ValueError("Entity Timeline source version differs")
    expected_source = next(r for r in proof["outputs"] if r["path"] == source_path(SOURCE))
    if sha(SOURCE.read_bytes()) != expected_source["sha256"]:
        raise ValueError("Verified entity Timeline source changed")
    source = json.loads(SOURCE.read_bytes())
    files = {r["serialized_file"].casefold(): r for r in prior["serialized_files"]}
    roots = sorted({files[t["asset"]["cab"].casefold()]["source"] for t in source["timelines"]})
    if len(roots) != 5:
        raise ValueError("Original entity Timeline root coverage differs")
    apk = Apk.current()
    if apk.version != prior["manifest"]:
        raise ValueError("Selected package version differs")
    assets, missing = collect(apk, roots)
    if missing:
        raise ValueError("Unresolved original entity Timeline CAB closure: " + repr(missing))
    print("Original entity Timeline closure:", len(apk.loaded), "inputs", flush=True)
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    for entry in prior["assemblies"]:
        if sha((ROOT / entry["path"]).read_bytes()) != entry["sha256"]:
            raise ValueError("Original schema assembly changed")
    schemas = NativeSchemas(code, unity_project=unity_project)
    schema_proof = install(schemas, code)
    old_inputs = {r["name"]: r for r in prior["inputs"]}
    tools = [record(Path(__file__))] + [record(ROOT / "scripts" / name) for name in (
        "prepare_unity_map13_water_entity_inputs.py", "official_managed_reference_conversion.py",
        "map13_native_schemas.py", "map13_entity_native_schemas.py", "map14_entity_native_schemas.py",
        "official_manifest.py", "official_unity_schema.py", "unity_schema_types.cs",
        "official_metadata.py", "read_official_native.py", "prepare_unity_bundle_inputs.py")]
    inputs, components, changes, unions, errors = [], [], [], [], []
    count = 0
    for name in sorted(apk.loaded):
        original, entry = read_source(apk, name)
        if name.startswith("assets/bin/Data/"):
            converted, fields, changed, typed = original, [], [], []
            checked = sum(len(f.objects) for f in serialized_files(apk.loaded[name]))
        else:
            try:
                converted, fields, changed, typed, checked = hydrate(
                    apk.loaded[name], schemas, include_source_identity=True)
            except Exception as error:
                errors.append(dict(bundle=name, error=str(error), cause=str(error.__cause__)))
                print("Unresolved original Timeline schema:", errors[-1], flush=True)
                continue
        digest = sha(converted)
        if digest != old_inputs[name]["converter_input_sha256"]:
            raise ValueError("Rebuilt input differs from verified full entity closure: " + name)
        entry.update(output=Path(name).name, converter_input_sha256=digest,
                     converter_input_bytes=len(converted), restored_components=len(fields),
                     changed_payloads=len(changed), checked_objects=checked)
        write(CACHE / "inputs" / entry["output"], converted)
        inputs.append(entry)
        components.extend(fields); changes.extend(changed); unions.extend(typed); count += checked
        print("Verified Timeline input:", name, len(fields), "components", flush=True)
    if errors:
        raise ValueError("Resolve original Timeline schemas before conversion: " + repr(errors))
    for entry in tools:
        if sha((ROOT / entry["path"]).read_bytes()) != entry["sha256"]:
            raise ValueError("Conversion tool changed during recovery")
    write(COMPONENTS, encode(dict(manifest=apk.version, mapClassId=14, components=components)))
    meta(COMPONENTS)
    result = dict(format=1, manifest=apk.version, mapClassId=14, roots=roots,
        source=record(SOURCE), source_proof=record(source_proof), full_entity_input=record(prior_path),
        inputs=inputs, serialized_files=list(assets.values()), assemblies=prior["assemblies"],
        native_schema=schema_proof, tools=tools, managed_reference_unions=unions,
        converted_objects=changes, outputs=[record(COMPONENTS)],
        counts=dict(timelines=5, bundles=len(inputs), components=len(components),
                    checked_objects=count, converted_registries=len(changes)),
        operation="Select the five original entity Timeline root CABs and close all original external CAB dependencies. Restore every original component using the verified native schemas; require every rebuilt converter input to reproduce the recorded full entity input SHA256. Preserve original objects and reversible reference identities. No runtime replacement or omitted track is introduced.",
        limitations=["Conversion inputs and native export only; migrated runtime, actors, native material/script bindings and graphical playback remain required."])
    write(RECEIPT, encode(result)); write(CACHE / "inputs/source-manifest.json", encode(result))
    apk.archive.close()
    print("Original entity Timeline inputs verified:", result["counts"], flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-project", type=Path, default=ROOT / "unity")
    parser.add_argument("--convert", action="store_true")
    parser.add_argument("--export-only", action="store_true")
    args = parser.parse_args()
    result = json.loads(RECEIPT.read_bytes()) if args.export_only else build(args.unity_project)
    if args.convert:
        from prepare_unity_map13_water_entity_export import convert, build as export
        convert(CACHE)
        export(receipt_path=RECEIPT, component_path=COMPONENTS, roots=result["roots"],
               cache_root=CACHE, proof_path=EXPORT_RECEIPT, expected_counts=None,
               entry_path=Path(__file__).resolve(), map_class_id=14, subject="entity Timeline")
