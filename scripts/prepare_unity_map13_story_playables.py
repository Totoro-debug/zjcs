"""Recover Map13 story performance hierarchy and original component fields.

This is an extraction input for native asset migration. It does not replace
prefabs with a duration-only playback implementation or claim a playable map.
"""
from __future__ import annotations

import argparse
import base64
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from prepare_unity_map13_sources import encode, meta, sha, source_path, write

SOURCE = ROOT / "unity/Assets/Official/World/Map/13/Source/stories.json"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map13-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Official/World/Map/13/Source/story-playables.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-story-playables-source-manifest.json"
VERSION = "871_156598"


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()), bytes=path.stat().st_size)


def selections(value):
    if isinstance(value, dict):
        if value.get("mAssetGuid"):
            yield value["mAssetGuid"]
        for child in value.values():
            yield from selections(child)
    elif isinstance(value, list):
        for child in value:
            yield from selections(child)


def selection_keys(value):
    if isinstance(value, dict):
        if "CustomAssetProxy" in value and int(value.get("Type", 0)) == 2:
            yield value["PreDefineKey"]
        for child in value.values():
            yield from selection_keys(child)
    elif isinstance(value, list):
        for child in value:
            yield from selection_keys(child)


def serializable(value):
    if isinstance(value, (bytes, bytearray)):
        return {"encoding": "base64", "bytes": base64.b64encode(value).decode("ascii")}
    if isinstance(value, dict):
        return {str(key): serializable(child) for key, child in value.items()}
    if isinstance(value, (list, tuple)):
        return [serializable(child) for child in value]
    return value


def build(unity_project=None):
    receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    expected = {item["path"]: item["sha256"] for item in receipt["outputs"]}
    if receipt["manifest"] != VERSION or expected.get(source_path(SOURCE)) != sha(SOURCE.read_bytes()):
        raise ValueError("Map13 story extraction does not match its pinned source receipt")
    stories = json.loads(SOURCE.read_bytes())
    dependencies_path = SOURCE.with_name("story-dependencies.json")
    dependencies_receipt_path = ROOT / "assets/reference/official-map/map13-story-dependencies-source-manifest.json"
    dependencies = json.loads(dependencies_path.read_bytes())
    dependencies_receipt = json.loads(dependencies_receipt_path.read_bytes())
    dependency_outputs = {row["path"]: row["sha256"] for row in dependencies_receipt["outputs"]}
    if dependencies.get("manifest") != VERSION or dependencies.get("mapClassId") != 13 \
            or dependency_outputs.get(source_path(dependencies_path)) != sha(dependencies_path.read_bytes()):
        raise ValueError("Map13 shared story performance selections differ from their source receipt")
    if set(stories) & set(dependencies["graphs"]):
        raise ValueError("Duplicate original Map13 story identity")
    stories.update(dependencies["graphs"])
    requested = set(selections(stories))
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Map13 story performance version differs from the selected source")
    code = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
    schemas = NativeSchemas(code, unity_project=unity_project)
    schema_proof = install(schemas, code)
    registry_bundle = "defaultpackage_assets_scriptableconfig_modules_story.bundle"
    registry = apk.asset(registry_bundle, "/StoryPlayableRegisterConfig.asset")
    registry_data, _ = schemas.read(registry)
    aliases = {}
    for item in registry_data["StoryPlayableRegisterItems"]["mSerializeBuffer"]:
        # Preserve official serialized first-key behavior.
        aliases.setdefault(item["Key"], item["Value"]["mProxyRes"]["mAssetGuid"])
    used_aliases = {}
    for key in sorted(set(selection_keys(stories))):
        guid = aliases.get(key)
        if not guid:
            raise ValueError("Official Map13 predefined playable is unbound: " + key)
        used_aliases[key] = guid
        requested.add(guid)

    manifest_bytes = apk.source.manifest_bytes("DefaultPackage", apk.archive)
    selected = {row["guid"]: row for row in read_asset_manifest(manifest_bytes) if row["guid"] in requested}
    apk_bytes = apk.archive.read("assets/yoo/DefaultPackage/PackageManifest_DefaultPackage_854_156383.v3.bytes")
    apk_rows = {row["guid"]: row for row in read_asset_manifest(apk_bytes) if row["guid"] in requested}
    if requested - selected.keys():
        raise ValueError("Original Map13 performance GUIDs are missing from the selected asset manifest: " + repr(sorted(requested - selected.keys())))
    objects = {}
    def capture(obj):
        key = obj.assets_file.name.lower() + ":" + str(obj.path_id)
        if key not in objects:
            if obj.type.name == "MonoBehaviour":
                basic = obj.read(check_read=False)
                script = basic.m_Script.read()
                kind = (script.m_Namespace + "." if script.m_Namespace else "") + script.m_ClassName
                fields, _ = schemas.read(obj)
            else:
                kind = obj.type.name
                fields = obj.read_typetree()
            objects[key] = dict(identity=key, type=kind, source=apk.provenance(obj),
                                object_sha256=sha(obj.get_raw_data()), fields=serializable(fields))
        return key

    def hierarchy(obj):
        go = obj.read()
        component_ids, children = [], []
        for component in go.m_Component:
            child = component.component.deref()
            component_ids.append(capture(child))
            if child.type.name in ("Transform", "RectTransform"):
                children = [hierarchy(pointer.read().m_GameObject.deref()) for pointer in child.read().m_Children]
        return dict(object=capture(obj), components=component_ids, children=children)

    prefabs = []
    for guid in sorted(requested):
        row = selected[guid]
        bundle = apk.bundle_records[row["bundle"]]
        apk_entry = "assets/yoo/DefaultPackage/" + bundle["hash"] + ".bundle"
        root = apk.asset(row["bundle"], row["path"])
        if root.type.name != "GameObject":
            raise ValueError("Official Map13 performance asset is not a GameObject: " + row["path"])
        prefabs.append(dict(guid=guid, path=row["path"], apk_asset=apk_rows.get(guid),
                            selected_asset=row, selected_bytes_packaged=apk_entry in apk.available,
                            source=apk.provenance(root), hierarchy=hierarchy(root)))
        print("Decoded official Map13 story performance:", row["path"], flush=True)
    write(OUTPUT, encode(dict(manifest=VERSION, mapClassId=13, aliases=used_aliases,
                             registry=dict(source=apk.provenance(registry), fields=registry_data),
                             prefabs=prefabs, objects=list(objects.values()), schema_proof=schema_proof)))
    meta(OUTPUT)
    counts = dict(prefabs=len(prefabs), aliases=len(used_aliases), hierarchyObjects=len(objects),
                  components=sum(item["type"] != "GameObject" for item in objects.values()))
    write(RECEIPT, encode(dict(manifest=VERSION, mapClassId=13,
        operation="Decode original story playable prefab hierarchy and full component serialization; native import, external dependency closure and live performance remain pending.",
        inputs=[record(SOURCE), record(SOURCE_RECEIPT), record(dependencies_path),
                record(dependencies_receipt_path), record(Path(__file__).resolve()),
                record(ROOT / "scripts/map13_native_schemas.py"),
                record(ROOT / "scripts/map13_entity_native_schemas.py"),
                record(ROOT / "scripts/official_unity_schema.py")],
        manifests=dict(apk_sha256=sha(apk_bytes), selected_sha256=sha(manifest_bytes)),
        registry_source=apk.provenance(registry), outputs=[record(OUTPUT), record(OUTPUT.with_name(OUTPUT.name + ".meta"))],
        counts=counts)))
    print(json.dumps(counts, sort_keys=True), flush=True)
    return counts


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-project", type=Path, help="Use matching pinned Unity reference DLLs from an isolated import")
    args = parser.parse_args()
    build(args.unity_project)
