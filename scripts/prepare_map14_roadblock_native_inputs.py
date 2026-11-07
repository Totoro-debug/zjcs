"""Rehydrate the selected official roadblock displays in an owned validation workspace."""
import json
import os
from pathlib import Path
import UnityPy

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map13_water_entity_export import convert, build as verify
from prepare_unity_map14_sources import encode, sha, source_path, write

IDS = (8625, 8626, 8634, 8637, 8633)


def workspace():
    parent = Path(os.environ["ZJCS_VALIDATION_DIR"]).resolve()
    if (parent.parent != (ROOT / "output/validation").resolve()
            or not (parent / ".zjcs-validation.json").is_file()
            or Path(os.environ["ZJCS_REPOSITORY_ROOT"]).resolve() != ROOT.resolve()):
        raise ValueError("Use an owned repository validation workspace")
    return parent / "roadblock-native-closure"


def build():
    cache = workspace()
    graph = json.loads((ROOT / "unity/Assets/Official/World/Map/14/Source/native-entity-object-graph.json").read_bytes())
    old = {(r["cab"], r["path_id"]): r for r in graph["objects"]}
    selected = [r for r in graph["roots"] if any(r["path"].endswith(f"/{n}_14_view.prefab") for n in IDS)]
    if len(selected) != 5:
        raise ValueError("Expected the five original Map14 roadblock displays")
    reachable, pending = set(), [(r["cab"], r["path_id"]) for r in selected]
    while pending:
        key = pending.pop()
        if key in reachable:
            continue
        reachable.add(key)
        pending.extend((r["cab"], r["path_id"]) for r in old[key]["references"])
    apk = Apk.current()
    roots = sorted({old[r["cab"], r["path_id"]]["input"] for r in selected})
    _, missing = collect(apk, roots)
    if missing:
        raise ValueError("Resolve roadblock CAB dependencies: " + str(missing))
    prefabs = []
    for row in selected:
        original = old[row["cab"], row["path_id"]]
        obj = apk.asset(original["input"], row["path"])
        if sha(obj.get_raw_data()) != original["payload_sha256"]:
            raise ValueError("Current roadblock root differs from historical verified graph")
        prefabs.append(dict(path=row["path"], source=apk.provenance(obj),
                            object_sha256=sha(obj.get_raw_data()), uses=row["uses"]))
    schemas = NativeSchemas(ROOT / ".tools/source-packages/current" / apk.version / "official-code")
    inputs, fields, changed, unions = [], [], [], []
    reference_only_changes = []
    count = 0
    for name in sorted(apk.loaded):
        raw, record = read_source(apk, name)
        if name.startswith("assets/bin/Data/"):
            converted, component, changes, typed = raw, [], [], []
            checked = sum(len(f.objects) for f in serialized_files(apk.loaded[name]))
        else:
            # Export the exact object closure. A dependency bundle can also hold
            # unrelated character models; they are not roadblock dependencies.
            kept_count = 0
            for file in serialized_files(apk.loaded[name]):
                keep = []
                for obj in file.objects.values():
                    key = (file.name, str(obj.path_id))
                    if key in reachable:
                        if sha(obj.get_raw_data()) != old[key]["payload_sha256"]:
                            if obj.type.name not in ("Material", "Shader", "Texture2D", "Sprite", "SpriteAtlas"):
                                raise ValueError("Selected native dependency changed: " + str(key))
                            # Final rendering binds the separately verified historical
                            # native material/Sprite libraries. These objects only
                            # provide address identities to the temporary converter.
                            reference_only_changes.append(dict(cab=key[0], path_id=key[1],
                                type=obj.type.name, historical_sha256=old[key]["payload_sha256"],
                                converter_source_sha256=sha(obj.get_raw_data())))
                        keep.append(obj)
                    elif obj.type.name == "AssetBundle":
                        keep.append(obj)
                payload = [o for o in keep if o.type.name != "AssetBundle"]
                kept_count += len(payload)
                for obj in keep:
                    if obj.type.name != "AssetBundle":
                        continue
                    body = obj.read_typetree()
                    ids = {o.path_id for o in payload}
                    body["m_PreloadTable"] = [dict(m_FileID=0, m_PathID=o.path_id) for o in payload]
                    body["m_Container"] = [r for r in body["m_Container"]
                        if r[1]["asset"]["m_FileID"] == 0 and r[1]["asset"]["m_PathID"] in ids]
                    for _, entry in body["m_Container"]:
                        entry["preloadIndex"] = 0; entry["preloadSize"] = len(payload)
                    obj.save_typetree(body)
                indices, types = {}, []
                for obj in keep:
                    if obj.type_id not in indices:
                        indices[obj.type_id] = len(types)
                        types.append(obj.serialized_type)
                for obj in keep:
                    obj.type_id = indices[obj.type_id]
                file.objects = {o.path_id: o for o in keep}
                file.types = types
            if not kept_count:
                continue
            trimmed = apk.loaded[name].save(packer="lz4")
            environment = UnityPy.load(trimmed)
            for cab, file in apk.loaded[name].environment.cabs.items():
                if cab not in environment.cabs:
                    environment.register_cab(cab, file)
            bundle = next(iter(environment.files.values()))
            converted, component, changes, typed, checked = hydrate(bundle, schemas, include_source_identity=True)
        record.update(output=Path(name).name, converter_input_sha256=sha(converted),
                      converter_input_bytes=len(converted), checked_objects=checked)
        write(cache / "inputs" / record["output"], converted)
        inputs.append(record); fields.extend(component); changed.extend(changes); unions.extend(typed)
        count += checked
        print("Verified native roadblock input:", name, len(component), flush=True)
    components = cache / "components.json"
    write(components, encode(dict(manifest=apk.version, mapClassId=14, components=fields)))
    tools = [Path(__file__).resolve(), ROOT / "scripts/prepare_unity_map13_water_entity_export.py"]
    receipt = cache / "input-source-manifest.json"
    write(receipt, encode(dict(manifest=apk.version, mapClassId=14,
        selection=dict(roots=roots, prefabs=prefabs), inputs=inputs,
        tools=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in tools],
        outputs=[dict(path=source_path(components), sha256=sha(components.read_bytes()))],
        converted_objects=changed, managed_reference_unions=unions,
        referenceOnlyPayloadChanges=reference_only_changes,
        counts=dict(checked_objects=count))))
    convert(cache)
    verify(receipt_path=receipt, component_path=components, roots=roots, cache_root=cache,
           proof_path=cache / "export-source-manifest.json", expected_counts=None,
           map_class_id=14, subject="roadblock", expected_manifest=apk.version,
           entry_path=Path(__file__).resolve())


if __name__ == "__main__":
    build()
