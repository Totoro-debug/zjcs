"""Decode current Map13 EC/entity-display prefabs and their actual CAB closure."""
from pathlib import Path
import argparse
import json
import sys
import importlib.metadata

# Keep this long conversion independent of shared extraction-cache cleanup.
_python = Path(__file__).resolve().parents[1] / ".tools/map13-entity-native-cache-871_156598/python"
if (_python / "UnityPy/__init__.py").is_file():
    sys.path.insert(0, str(_python))

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from map13_entity_dynamic_textures import collect_textures
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map13_sources import encode, sha, source_path, write, meta


CACHE = ROOT / ".tools/map13-entity-native-cache-871_156598"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-entity-input-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Official/World/Map/13/Source/native-entity-components.json"


def roots(apk):
    data_path = ROOT / "unity/Assets/Game/Resources/Map13/entity-presentation.json"
    map_path = ROOT / "unity/Assets/Official/World/Map/13/map13.json"
    data, map_data = json.loads(data_path.read_bytes()), json.loads(map_path.read_bytes())
    if data["manifest"] != apk.version or map_data["ClassId"] != 13:
        raise ValueError("Map13 entity selector source differs")
    directories = {}
    for item in data["Directories"]:
        for value in item["Ids"]:
            directories.setdefault(value, item["Directory"])
    class_ids = {entity["EntityId"] for entity in map_data["Entities"]}
    class_ids.update([map_data["Navigation"]["CharacterId"], map_data["Navigation"]["MemberId"]])
    requested = {}
    for class_id in sorted(class_ids):
        path = "Assets/Prefabs/EcEntity/" + directories[class_id] + "/" + str(class_id) + ".prefab"
        requested.setdefault(path, []).append(dict(kind="ec_entity", class_id=class_id))
    for number, row in enumerate(data["Displays"]):
        if row["EntityClassId"] not in class_ids or row["GroupId"] not in (0, 13) or row["MapClassId"] not in (0, 13):
            continue
        if row["DisplayPath"]:
            path = "Assets/Prefabs/" + row["DisplayPath"] + ".prefab"
            requested.setdefault(path, []).append(dict(kind="entity_display", row=number, selector=row))
    records, bundles = [], set()
    for path, uses in requested.items():
        parts = path.removesuffix(".prefab").lower().split("/")
        candidates = ["defaultpackage_" + "_".join(parts[:count]) + ".bundle" for count in range(len(parts), 1, -1)]
        found = []
        for bundle in candidates:
            if bundle not in apk.index:
                continue
            matches = [(name, ptr.deref()) for name, ptr in apk.container(bundle) if name.casefold() == path.casefold()]
            # One prefab address can also expose baked Spine Mesh subassets.
            # Official LoadAsset<GameObject> selects the GameObject, not those.
            found.extend((bundle, name, obj) for name, obj in matches if obj.type.name == "GameObject")
        if len(found) != 1:
            raise ValueError("Resolve original prefab container before conversion: " + path + " matches=" + str(len(found)))
        bundle, actual_path, obj = found[0]
        if obj.type.name != "GameObject":
            raise ValueError("Original entity prefab is not a native GameObject")
        bundles.add(bundle)
        records.append(dict(path=path, container_path=actual_path, uses=uses, source=apk.provenance(obj),
                            object_sha256=sha(obj.get_raw_data())))
    result = dict(manifest=apk.version, roots=sorted(bundles), prefabs=records,
                  map=dict(path=source_path(map_path), sha256=sha(map_path.read_bytes())),
                  selectors=dict(path=source_path(data_path), sha256=sha(data_path.read_bytes())))
    write(CACHE / "roots.json", encode(result))
    print("Resolved original entity/display prefabs:", len(records), "in", len(bundles), "root bundles", flush=True)
    return result


def build(roots_only=False):
    tool_paths = [Path(__file__).resolve()] + [ROOT / "scripts" / name for name in
        ("prepare_unity_map13_water_entity_inputs.py", "official_managed_reference_conversion.py",
         "map13_native_schemas.py", "map13_entity_native_schemas.py", "map13_entity_dynamic_textures.py",
         "official_manifest.py", "official_unity_schema.py", "unity_schema_types.cs", "official_metadata.py",
         "read_official_native.py", "prepare_unity_bundle_inputs.py")]
    tool_records = [dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in tool_paths]
    apk = Apk.current()
    selection = roots(apk)
    if roots_only:
        return
    assets, missing = collect(apk, selection["roots"])
    if missing:
        raise ValueError("Original entity CAB closure is incomplete: " + str(missing))
    print("Original Map13 entity CAB closure:", len(apk.loaded), "inputs", len(assets), "serialized files", flush=True)
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    code_manifest = code.parent / "code-manifest.json"
    manifest = json.loads(code_manifest.read_bytes())
    assembly_records = []
    for name, entry in manifest["assemblies"].items():
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != entry["decoded_sha256"]:
            raise ValueError("Original entity schema assembly differs: " + name)
        assembly_records.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    schemas = NativeSchemas(code)
    schema_proof = install(schemas, code)
    selection["dynamic_textures"] = collect_textures(apk, schemas)
    assets, missing = collect(apk, selection["roots"] + selection["dynamic_textures"]["roots"])
    if missing:
        raise ValueError("Original dynamic entity closure is incomplete: " + str(missing))
    write(CACHE / "roots.json", encode(selection))
    print("Entity closure including original dynamic textures:", len(apk.loaded), len(selection["dynamic_textures"]["bindings"]), flush=True)
    inputs, components, changes, unions, errors, object_count = [], [], [], [], [], 0
    for name in sorted(apk.loaded):
        original, record = read_source(apk, name)
        if name.startswith("assets/bin/Data/"):
            converted, fields, changed, typed = original, [], [], []
            checked = sum(len(file.objects) for file in serialized_files(apk.loaded[name]))
        else:
            try:
                converted, fields, changed, typed, checked = hydrate(apk.loaded[name], schemas, include_source_identity=True)
            except Exception as error:
                errors.append(dict(bundle=name, error=str(error), cause=str(error.__cause__)))
                print("Unresolved original schema:", errors[-1], flush=True)
                continue
        components.extend(fields); changes.extend(changed); unions.extend(typed); object_count += checked
        record.update(output=Path(name).name, converter_input_sha256=sha(converted), converter_input_bytes=len(converted),
                      restored_components=len(fields), changed_payloads=len(changed), checked_objects=checked)
        write(CACHE / "inputs" / record["output"], converted)
        inputs.append(record)
        if fields:
            print("Entity converter input", name, len(fields), "components", len(changed), "registries", flush=True)
    if errors:
        write(CACHE / "unresolved-schemas.json", encode(errors))
        raise ValueError("Resolve all original component schemas before exporting: " + str(len(errors)))
    for path, entry in zip(tool_paths, tool_records):
        if sha(path.read_bytes()) != entry["sha256"]:
            raise ValueError("Conversion source changed during extraction; rerun against stable sources: " + str(path))
    write(OUTPUT, encode(dict(manifest=apk.version, mapClassId=13, components=components)))
    meta(OUTPUT)
    result = dict(format=1, manifest=apk.version, mapClassId=13, selection=selection, inputs=inputs,
        serialized_files=list(assets.values()), assemblies=assembly_records,
        code_manifest=dict(path=source_path(code_manifest), sha256=sha(code_manifest.read_bytes())), native_schema=schema_proof,
        tools=tool_records,
        libraries={name: importlib.metadata.version(name) for name in
                   ("UnityPy", "TypeTreeGeneratorAPI", "capstone", "pyelftools")},
        managed_reference_unions=unions, converted_objects=changes,
        counts=dict(prefabs=len(selection["prefabs"]), root_bundles=len(selection["roots"]), bundles=len(inputs),
                    components=len(components), checked_objects=object_count, converted_registries=len(changes)),
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(OUTPUT.read_bytes()))],
        operation="Resolve original EC index and display-selector prefab paths against actual selected APK/cloud containers; close every CAB and dynamic texture dependency. Strictly decode all original component fields and reversibly expose heterogeneous managed references to the converter. Temporary CAB/path ID fields distinguish copied reference nodes during export and are independently verified and stripped. Compare original fields after native binary encoding, strict reading and reopening; preserve every other object payload byte. Record modified temporary payload hashes independently.",
        limitations=["Converter inputs only; original entity runtime and Windows-native prefab/script/shader/animation binding and complete map verification remain required."])
    write(RECEIPT, encode(result)); write(CACHE / "inputs/source-manifest.json", encode(result))
    (CACHE / "unresolved-schemas.json").unlink(missing_ok=True)
    print("Original Map13 entity converter inputs prepared:", result["counts"], flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--roots-only", action="store_true")
    build(parser.parse_args().roots_only)
