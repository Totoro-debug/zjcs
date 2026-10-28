"""Recover the original Map12 stele entity hierarchies and converter inputs.

This stages verified source data only; it never imports or launches Unity.
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
from validation_workspace import validation_directory

VERSION = "871_156598"
BUNDLE = "defaultpackage_assets_prefabs_map_map_12_entitydisplays_worldgameplay.bundle"
ROOTS = {6650: (-8618333079236681710, 1426876232665358354),
         6655: (-2656386750380352969, -497571002071623113)}
SOURCE = ROOT / "unity/Assets/Official/World/Map/12/Source/stele-operations.json"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map12-steles-source-manifest.json"
OUTPUT = SOURCE.with_name("native-stele-entity-components.json")
RECEIPT = ROOT / "assets/reference/official-map/map12-stele-entity-input-source-manifest.json"


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
    expected = {entry["path"]: entry["sha256"] for entry in source_receipt["outputs"]}
    if (selection["manifest"] != VERSION or source_receipt["manifest"] != VERSION
            or expected.get(source_path(SOURCE)) != sha(SOURCE.read_bytes())):
        raise ValueError("Selected original stele display receipt differs")
    displays = {entry["EntityClassId"]: entry for entry in selection["Displays"]}
    if set(displays) != set(ROOTS):
        raise ValueError("Original stele entity coverage differs")

    code = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
    code_manifest = code.parent / "code-manifest.json"
    manifest = json.loads(code_manifest.read_bytes())
    if manifest["manifest"] != VERSION:
        raise ValueError("Wrong original schema assembly selection")
    guards = [SOURCE, SOURCE_RECEIPT, code_manifest]
    assemblies = []
    for name, entry in manifest["assemblies"].items():
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != entry["decoded_sha256"]:
            raise ValueError("Original schema assembly differs: " + name)
        assemblies.append(record(path)); guards.append(path)
    tool_paths = [Path(__file__).resolve()] + [ROOT / "scripts" / name for name in (
        "official_apk.py", "prepare_unity_map13_water_entity_inputs.py",
        "official_managed_reference_conversion.py", "map13_native_schemas.py",
        "map13_entity_native_schemas.py", "map14_entity_native_schemas.py",
        "official_unity_schema.py", "unity_schema_types.cs", "official_manifest.py",
        "official_metadata.py", "read_official_native.py", "prepare_unity_bundle_inputs.py",
        "prepare_unity_map13_sources.py", "validation_workspace.py")]
    guards.extend(tool_paths)
    guarded = {path: sha(path.read_bytes()) for path in guards}
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Wrong selected original stele version")
    assets, missing = collect(apk, [BUNDLE])
    if missing:
        raise ValueError("Original stele entity CAB closure is incomplete: " + repr(missing))
    files = list(serialized_files(apk.loaded[BUNDLE]))
    if len(files) != 1:
        raise ValueError("Inspect additional original stele entity serialized files")
    file = files[0]
    schemas = NativeSchemas(code, unity_project=unity_project)
    native_schema = install(schemas, code)
    inputs, components, changes, unions, errors, checked_objects = [], [], [], [], [], 0
    print("Original stele entity closure:", len(apk.loaded), "bundles", flush=True)
    for ordinal, name in enumerate(sorted(apk.loaded)):
        original, entry = read_source(apk, name)
        try:
            if name.startswith("assets/bin/Data/"):
                converted, fields, changed, typed = original, [], [], []
                checked = sum(len(f.objects) for f in serialized_files(apk.loaded[name]))
            else:
                converted, fields, changed, typed, checked = hydrate(
                    apk.loaded[name], schemas, include_source_identity=True)
        except Exception as error:
            errors.append(dict(bundle=name, error=str(error), cause=str(error.__cause__)))
            print("Unresolved original stele entity schema:", errors[-1], flush=True)
            continue
        components.extend(fields); changes.extend(changed); unions.extend(typed)
        checked_objects += checked
        entry.update(output=Path(name).name, converter_input_sha256=sha(converted),
            converter_input_bytes=len(converted), restored_components=len(fields),
            changed_payloads=len(changed), checked_objects=checked)
        write(stage_dir / "inputs" / entry["output"], converted); inputs.append(entry)
        if ordinal % 32 == 0:
            print("Original stele entity inputs:", ordinal + 1, "/", len(apk.loaded), flush=True)
    if errors:
        write(stage_dir / "unresolved-entity-schemas.json", encode(errors))
        raise ValueError("Resolve every original entity schema before conversion")
    index = {(entry["cab"], entry["path_id"]): entry for entry in components}
    prefabs, objects, selected = [], [], []
    seen = set()

    def walk(obj, parent=""):
        go = obj.read(check_read=True)
        path = parent + "/" + go.m_Name if parent else go.m_Name
        owned = [obj] + [entry.component.deref() for entry in go.m_Component]
        for owned_obj in owned:
            identity = (owned_obj.assets_file.name, str(owned_obj.path_id))
            if identity in seen:
                raise ValueError("Original stele hierarchy object is repeated")
            seen.add(identity)
            if owned_obj.type.name == "MonoBehaviour":
                entry = index[identity]
                if entry["type"] != "ECViewDriver":
                    raise ValueError("Additional original entity component requires migration")
                fields = entry["fields"]
            else:
                fields = owned_obj.read_typetree(check_read=True)
            payload_hash = (entry["payload_sha256"] if owned_obj.type.name == "MonoBehaviour"
                            else sha(owned_obj.get_raw_data()))
            objects.append(dict(cab=identity[0], path_id=identity[1], hierarchy=path,
                type=owned_obj.type.name, payload_sha256=payload_hash, fields=fields))
        transform, = [entry.component.deref() for entry in go.m_Component
                      if entry.component.deref().type.name == "Transform"]
        for child in transform.read(check_read=True).m_Children:
            walk(child.deref().read(check_read=True).m_GameObject.deref(), path)

    for entity_class_id, (root_id, driver_id) in ROOTS.items():
        root = file.objects[root_id]
        if root.type.name != "GameObject" or root.read().m_Name != str(entity_class_id) + "_12_view":
            raise ValueError("Original stele entity root identity differs")
        driver = index[(file.name, str(driver_id))]
        if driver["fields"] != displays[entity_class_id]["fields"]:
            raise ValueError("Original stele entity fields differ from verified display selection")
        selected.append(driver); walk(root)
        prefabs.append(dict(entityClassId=entity_class_id, name=root.read().m_Name,
            cab=file.name, path_id=str(root_id), driver_path_id=str(driver_id),
            source=apk.provenance(root), object_raw_sha256=sha(root.get_raw_data())))
    if len(objects) != 20 or len(selected) != 2:
        raise ValueError("Inspect additional original stele hierarchy objects")
    for path, expected_hash in guarded.items():
        if sha(path.read_bytes()) != expected_hash:
            raise ValueError("Original input or conversion tool changed during extraction: " + str(path))
    write(OUTPUT, encode(dict(manifest=VERSION, mapClassId=12, prefabs=prefabs,
        components=selected, objects=objects))); meta(OUTPUT)
    result = dict(format=1, manifest=VERSION, mapClassId=12, roots=[BUNDLE], prefabs=prefabs,
        selection=[record(SOURCE), record(SOURCE_RECEIPT)], inputs=inputs,
        serialized_files=list(assets.values()), assemblies=assemblies,
        code_manifest=record(code_manifest), tools=[record(path) for path in tool_paths],
        schema_reference_assemblies=[record(path) for path in schemas.reference_inputs],
        native_schema=native_schema, managed_reference_unions=unions,
        converted_objects=changes, outputs=[record(OUTPUT), record(Path(str(OUTPUT) + ".meta"))],
        counts=dict(prefabs=2, selected_components=2, hierarchy_objects=20,
            bundles=len(inputs), components=len(components), checked_objects=checked_objects,
            converted_registries=len(changes)),
        operation="Verify selected original displays, APK-first source bytes and the complete CAB dependency closure. Restore stripped schemas from verified assemblies and native metadata, preserving every unmodified object byte. Reopen converter payloads and compare all restored fields. Select the exact two original ECViewDriver registries and their original four-node hierarchies without changing Int64 identifiers, transforms, component order, sprite/material references or managed-reference order. Temporary converter source-identity fields are not original fields and remain confined to conversion inputs.",
        limitations=["Verified source and converter inputs only; native prefab/script/sprite bindings and entity lifecycle remain required. Unity import, Play Mode, Windows Player and publishing have not been executed by this tool."])
    write(RECEIPT, encode(result)); write(stage_dir / "inputs/source-manifest.json", encode(result))
    write(stage_dir / "all-components.json", encode(dict(manifest=VERSION, mapClassId=12, components=components)))
    (stage_dir / "unresolved-entity-schemas.json").unlink(missing_ok=True)
    print("Original stele entity inputs prepared:", result["counts"], flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage-dir", type=Path, required=True)
    parser.add_argument("--unity-project", type=Path, default=ROOT / "unity")
    args = parser.parse_args()
    build(args.stage_dir, args.unity_project)
