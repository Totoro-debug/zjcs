"""Recover the four original Map12 stele performances and their CAB closure.

Only verified converter inputs are staged. This does not import or run Unity.
"""
import argparse
import json
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from validation_workspace import validation_directory

VERSION = "871_156598"
BUNDLE = "defaultpackage_share_03ce017337f6b663bd88db2ec0a05b55.bundle"
ROOTS = {
    6650: (("mCanInteractionPlayable", "multistele_loop_playable", -6160978803773559141),
           ("mEndInteractionPlayable", "multistele_dis_playable", 7080318833739824495)),
    6655: (("mCanInteractionPlayable", "singlestele_loop_playable", -5194250875956297176),
           ("mEndInteractionPlayable", "singlestele_dis_playable", -3387268642415751169)),
}
SOURCE = ROOT / "unity/Assets/Official/World/Map/12/Source/stele-operations.json"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map12-steles-source-manifest.json"
OUTPUT = SOURCE.with_name("native-stele-playable-components.json")
RECEIPT = ROOT / "assets/reference/official-map/map12-stele-playable-input-source-manifest.json"


def record(path):
    return dict(path=source_path(path) if path.is_relative_to(ROOT) else path.as_posix(),
                sha256=sha(path.read_bytes()))


def build(stage_dir, unity_project=ROOT / "unity"):
    stage_dir = stage_dir.resolve()
    validation = validation_directory()
    if stage_dir == validation or not stage_dir.is_relative_to(validation):
        raise ValueError("Stage original conversion inputs inside a validation session")
    selection = json.loads(SOURCE.read_bytes())
    source_receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    if selection["manifest"] != VERSION or source_receipt["manifest"] != VERSION:
        raise ValueError("Wrong original stele source version")
    expected = {r["path"]: r["sha256"] for r in source_receipt["outputs"]}
    if expected.get(source_path(SOURCE)) != sha(SOURCE.read_bytes()):
        raise ValueError("Original stele display receipt is stale")
    for display in selection["Displays"]:
        refs = display["fields"]["references"]["RefIds"]
        view, = [r["data"] for r in refs if r["type"]["class"] == "ECPeriodicityInteractionStateView"]
        for field, _, path_id in ROOTS[display["EntityClassId"]]:
            if view[field] != dict(m_FileID=24, m_PathID=path_id):
                raise ValueError("Original stele performance reference changed")
    if {d["EntityClassId"] for d in selection["Displays"]} != set(ROOTS):
        raise ValueError("Original stele display coverage differs")

    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Wrong selected original stele version")
    assets, missing = collect(apk, [BUNDLE])
    if missing:
        raise ValueError("Original stele performance CAB closure is incomplete: " + repr(missing))
    files = list(serialized_files(apk.loaded[BUNDLE]))
    if len(files) != 1:
        raise ValueError("Inspect original stele performance serialized files")
    file = files[0]
    prefabs = []
    for entity_class_id, entries in ROOTS.items():
        for field, name, path_id in entries:
            obj = file.objects[path_id]
            if obj.type.name != "GameObject" or obj.read().m_Name != name:
                raise ValueError("Original stele performance root changed")
            prefabs.append(dict(entityClassId=entity_class_id, field=field, name=name,
                cab=file.name, path_id=str(path_id), source=apk.provenance(obj),
                object_raw_sha256=sha(obj.get_raw_data())))

    code = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
    code_manifest = code.parent / "code-manifest.json"
    manifest = json.loads(code_manifest.read_bytes())
    if manifest["manifest"] != VERSION:
        raise ValueError("Wrong original assembly selection")
    assemblies = []
    for name, entry in manifest["assemblies"].items():
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != entry["decoded_sha256"]:
            raise ValueError("Changed original schema assembly: " + name)
        assemblies.append(record(path))
    tool_paths = [Path(__file__).resolve()] + [ROOT / "scripts" / name for name in (
        "prepare_unity_map13_water_entity_inputs.py", "official_managed_reference_conversion.py",
        "map13_native_schemas.py", "official_unity_schema.py", "unity_schema_types.cs",
        "official_manifest.py", "official_metadata.py", "prepare_unity_bundle_inputs.py")]
    tools = [record(path) for path in tool_paths]
    schemas = NativeSchemas(code, unity_project=unity_project)
    inputs, components, changes, unions, errors, checked_objects = [], [], [], [], [], 0
    print("Original stele performance closure:", len(apk.loaded), "bundles", flush=True)
    for name in sorted(apk.loaded):
        original, entry = read_source(apk, name)
        if name.startswith("assets/bin/Data/"):
            converted, fields, changed, typed = original, [], [], []
            checked = sum(len(f.objects) for f in serialized_files(apk.loaded[name]))
        else:
            try:
                converted, fields, changed, typed, checked = hydrate(apk.loaded[name], schemas)
            except Exception as error:
                errors.append(dict(bundle=name, error=str(error), cause=str(error.__cause__)))
                print("Unresolved original stele schema:", errors[-1], flush=True)
                continue
        components.extend(fields); changes.extend(changed); unions.extend(typed); checked_objects += checked
        entry.update(output=Path(name).name, converter_input_sha256=sha(converted),
                     converter_input_bytes=len(converted), restored_components=len(fields),
                     changed_payloads=len(changed), checked_objects=checked)
        write(stage_dir / "inputs" / entry["output"], converted)
        inputs.append(entry)
    if errors:
        write(stage_dir / "unresolved-schemas.json", encode(errors))
        raise ValueError("Resolve every original stele schema before conversion")
    for path, entry in zip(tool_paths, tools):
        if sha(path.read_bytes()) != entry["sha256"]:
            raise ValueError("Conversion source changed during extraction; rerun")
    write(OUTPUT, encode(dict(manifest=VERSION, mapClassId=12, prefabs=prefabs, components=components)))
    meta(OUTPUT)
    result = dict(format=1, manifest=VERSION, mapClassId=12, roots=[BUNDLE], prefabs=prefabs,
        selection=[record(SOURCE), record(SOURCE_RECEIPT)], inputs=inputs,
        serialized_files=list(assets.values()), assemblies=assemblies,
        code_manifest=record(code_manifest), tools=tools,
        schema_reference_assemblies=[record(p) for p in schemas.reference_inputs],
        managed_reference_unions=unions, converted_objects=changes, outputs=[record(OUTPUT)],
        counts=dict(prefabs=len(prefabs), bundles=len(inputs), components=len(components),
            checked_objects=checked_objects, converted_registries=len(changes)),
        operation="Verify the two original stele displays and their exact four performance PPtrs against the selected source receipt. Close the actual original CAB dependencies, verify APK first and downloaded cloud bytes against the selected size and MD5, restore stripped component schemas from verified original assemblies, reopen and compare every component field and every unmodified object byte. Preserve root path IDs, component order, original particle systems, materials, meshes and textures. No Unity execution or replacement art.",
        limitations=["Verified converter inputs only. Native hierarchy, particle/material and script bindings, original periodic-interaction lifecycle, Windows import and actual runtime verification remain required."])
    write(RECEIPT, encode(result)); write(stage_dir / "inputs/source-manifest.json", encode(result))
    (stage_dir / "unresolved-schemas.json").unlink(missing_ok=True)
    print("Original stele performance inputs prepared:", result["counts"], flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage-dir", type=Path, required=True)
    parser.add_argument("--unity-project", type=Path, default=ROOT / "unity")
    args = parser.parse_args()
    build(args.stage_dir, args.unity_project)
