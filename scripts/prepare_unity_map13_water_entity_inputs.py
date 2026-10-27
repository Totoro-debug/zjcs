"""Prepare the original water-entity prefab closure for native conversion.

Heterogeneous managed-reference registries are reversibly exposed as ordinary
typed vectors. Changed converter payloads are explicitly recorded; original
source bundles remain untouched and are never shipped as Windows assets.
"""
from collections import defaultdict
from pathlib import Path
import json

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas, read
from official_managed_reference_conversion import forward, identity, inverse, normalize, schema
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from UnityPy.helpers.Tpk import get_typetree_node
from UnityPy.helpers import TypeTreeHelper
from UnityPy.streams import EndianBinaryReader
import UnityPy


CACHE = ROOT / ".tools/map13-water-entity-native-cache-871_156598"
ROOT_BUNDLES = ["defaultpackage_assets_prefabs_ecentity_mapworld_worldgameplay_map_13_play.bundle"]
OUTPUT = ROOT / "unity/Assets/Official/World/Map/13/Source/native-water-entity-components.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-water-entity-input-source-manifest.json"


def hydrate(bundle, schemas):
    originals, fields, groups = {}, {}, defaultdict(set)
    records, changes, unions = [], [], []
    for file in serialized_files(bundle):
        originals[file.name] = {obj.path_id: sha(obj.get_raw_data()) for obj in file.objects.values()}
        for obj in file.objects.values():
            if obj.type.name != "MonoBehaviour":
                continue
            script = obj.read(check_read=False).m_Script.read()
            value, tree = read(schemas, obj)
            key = (file.name, obj.serialized_type.script_type_index)
            fields[(file.name, obj.path_id)] = (value, tree, key)
            if "references" in value:
                groups[key].update(identity(record) for record in value["references"]["RefIds"])
            records.append(dict(cab=file.name, path_id=str(obj.path_id), assembly=script.m_AssemblyName,
                type=(script.m_Namespace + "." if script.m_Namespace else "") + script.m_ClassName,
                payload_sha256=sha(obj.get_raw_data()), fields=value))
    nodes = {}
    for file in serialized_files(bundle):
        for obj in file.objects.values():
            pair = (file.name, obj.path_id)
            if pair not in fields:
                continue
            value, tree, key = fields[pair]
            if key not in nodes:
                types = sorted(groups[key])
                nodes[key] = schema(tree, types, schemas) if "references" in value else normalize(tree)
                if "references" in value:
                    unions.append(dict(cab=file.name, script_type_index=key[1], types=types,
                        schema=[{k: v for k, v in node.to_dict().items() if k != "m_Children"}
                                for node in nodes[key].traverse()]))
            node = nodes[key]
            obj.serialized_type.node = node
            if "references" in value:
                converted = forward(value, sorted(groups[key]))
                raw = obj.save_typetree(converted, nodes=node)
                reader = EndianBinaryReader(raw, endian=obj.reader.endian)
                restored = TypeTreeHelper.read_typetree(node, reader, as_dict=True, assetsfile=file,
                                                       byte_size=len(raw), check_read=True)
                if inverse(restored) != value:
                    raise ValueError("Managed-reference binary round trip differs: " + str(pair))
                changes.append(dict(cab=file.name, path_id=str(obj.path_id),
                    original_sha256=originals[file.name][obj.path_id], converted_sha256=sha(raw),
                    converted_bytes=len(raw), reference_count=len(value["references"]["RefIds"])))
        for typ in file.types:
            if typ.node is None:
                if typ.class_id == 114:
                    raise ValueError("Unrestored original MonoBehaviour schema")
                typ.node = get_typetree_node(typ.class_id, file.version)
            if typ.type_dependencies is None:
                typ.type_dependencies = ()
            normalize(typ.node)
        # All registry type identities/data are now present in explicit ordinary
        # fields. Stripped ref-type descriptions are unnecessary to this temporary
        # converter representation and cannot describe its typed vectors.
        file.ref_types = []
        file._enable_type_tree = True
    raw = bundle.save(packer="lz4")
    rebuilt = UnityPy.load(raw)
    for cab, file in bundle.environment.cabs.items():
        if cab not in rebuilt.cabs:
            rebuilt.register_cab(cab, file)
    expected_changes = {(entry["cab"], int(entry["path_id"])): entry["converted_sha256"] for entry in changes}
    checked = 0
    for file in serialized_files(next(iter(rebuilt.files.values()))):
        for obj in file.objects.values():
            key = (file.name, obj.path_id)
            expected = expected_changes.get(key, originals[file.name][obj.path_id])
            if sha(obj.get_raw_data()) != expected:
                raise ValueError("Converter changed an unrecorded object: " + str(key))
            if key in fields and inverse(obj.read_typetree()) != fields[key][0]:
                raise ValueError("Reopened converter bundle fields differ: " + str(key))
            checked += 1
    return raw, records, changes, unions, checked


def build():
    apk = Apk.current()
    assets, missing = collect(apk, ROOT_BUNDLES)
    if missing:
        raise ValueError("Original water entity CAB closure is incomplete: " + str(missing))
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    code_manifest = code.parent / "code-manifest.json"
    manifest = json.loads(code_manifest.read_bytes())
    if apk.version != "871_156598" or manifest["manifest"] != apk.version:
        raise ValueError("Incorrect current official version")
    assemblies = []
    for name, expected in manifest["assemblies"].items():
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != expected["decoded_sha256"]:
            raise ValueError("Changed official schema assembly: " + name)
        assemblies.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    schemas = NativeSchemas(code)
    destination = CACHE / "inputs"
    destination.mkdir(parents=True, exist_ok=True)
    inputs, components, changes, unions, count = [], [], [], [], 0
    for name in sorted(apk.loaded):
        original, record = read_source(apk, name)
        if name.startswith("assets/bin/Data/"):
            converted, fields, altered, typed = original, [], [], []
            checked = sum(len(file.objects) for file in serialized_files(apk.loaded[name]))
        else:
            converted, fields, altered, typed, checked = hydrate(apk.loaded[name], schemas)
        components.extend(fields); changes.extend(altered); unions.extend(typed); count += checked
        record.update(output=Path(name).name, converter_input_sha256=sha(converted), converter_input_bytes=len(converted),
                      restored_components=len(fields), changed_payloads=len(altered), checked_objects=checked)
        write(destination / record["output"], converted)
        inputs.append(record)
        print("Water entity input", name, len(fields), "components", len(altered), "converted registries", flush=True)
    write(OUTPUT, encode(dict(manifest=apk.version, mapClassId=13, components=components)))
    meta(OUTPUT)
    tools = [Path(__file__), ROOT / "scripts/official_managed_reference_conversion.py", ROOT / "scripts/map13_native_schemas.py"]
    result = dict(format=1, manifest=apk.version, mapClassId=13, roots=ROOT_BUNDLES,
        inputs=inputs, serialized_files=list(assets.values()), assemblies=assemblies,
        code_manifest=dict(path=source_path(code_manifest), sha256=sha(code_manifest.read_bytes())),
        tools=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in tools],
        managed_reference_unions=unions, converted_objects=changes,
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(OUTPUT.read_bytes()))],
        counts=dict(bundles=len(inputs), components=len(components), checked_objects=count, converted_registries=len(changes)),
        operation="Decode original native fields strictly; reversibly group heterogeneous reference registries into typed vectors for AssetRipper. Preserve each RID, descriptor, data field, pointer and original order. Compare original fields after native binary encode/read and reopen; retain every other original object payload byte. Modified converter payloads are explicitly hashed; original APK/cloud bytes are not modified.",
        limitations=["Converter inputs only; native prefab and managed-reference restoration, runtime code, shaders, Windows import and Play Mode remain required."])
    write(RECEIPT, encode(result)); write(destination / "source-manifest.json", encode(result))
    print("Water entity conversion inputs ready:", result["counts"], flush=True)


if __name__ == "__main__":
    build()
