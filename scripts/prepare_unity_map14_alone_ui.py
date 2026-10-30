"""Decode the official Map15 group-world preparation UI used by Map14 stories."""
from __future__ import annotations

import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from home_particle_schema import load as load_particle_schema
from map11_support_ui import load_support_ui
from prepare_unity_map10_entities import sha256, write_generated
from prepare_unity_map13_water_components import import_meta
from prepare_unity_map11_ui import extract_panels


BUNDLE = "defaultpackage_assets_prefabs_ui_aloneworldmap.bundle"
PANEL_KEYS = (
    "AloneWorldMapEntryItemContentMapForGroupMap",
    "AloneGroupWorldPrepareTouchDialog",
    "AloneGroupWorldTouchDialog",
    "AloneWorldTouchDialog",
)
OUTPUT_ROOT = ROOT / "unity/Assets/Official/World/Map/14/UI/Alone"
OUTPUT_JSON = OUTPUT_ROOT / "map14-alone-ui.json"
OUTPUT_MANIFEST = OUTPUT_ROOT / "map14-alone-ui-source-manifest.json"


def _tree(exporter, name):
    native, _ = exporter.object_tree(name, BUNDLE)
    nodes = native["nodes"]
    for node in nodes:
        node["children"] = []
    for node in nodes[1:]:
        nodes[node["parentIndex"]]["children"].append(node)
    return nodes[0], dict(native["source"], path=native["source_path"])


def _ensure_meta(path, importer, folder=False):
    if not Path(str(path) + ".meta").exists():
        import_meta(path, importer, folder=folder)


def build():
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Unexpected Map14 standalone-world source version: " + apk.version)
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    schemas = ScriptSchemas(code_root=code)
    load_particle_schema(schemas)
    support = load_support_ui(apk, schemas, 14, code)
    extra_trees, extra_sources, extra_resources, _, _, _, ui_schemas, exporter = support
    source, prefabs = {}, {}
    for key in PANEL_KEYS:
        source[key], prefabs[key] = _tree(exporter, key)

    manifest = {
        "manifest": apk.version,
        "mapClassId": 14,
        "prefabs": prefabs,
        "resources": dict(extra_resources),
    }
    raw = json.dumps(source, ensure_ascii=False, separators=(",", ":"), sort_keys=True).encode("utf-8")
    panels, components, sprites, shader_programs = extract_panels(
        apk, schemas, source, manifest, PANEL_KEYS, extra_trees, ui_schemas,
        exporter, raw, OUTPUT_ROOT, preserve_native_transforms=True,
        restore_component_image_colors=True)

    records = []
    for uri, metadata in sorted(sprites.items()):
        original = ROOT / uri.removeprefix("res://")
        raw_sprite = original.read_bytes()
        provenance = manifest["resources"][uri]
        if sha256(raw_sprite) != provenance["sha256"]:
            raise ValueError("Standalone-world sprite differs from source: " + uri)
        target = OUTPUT_ROOT / "Sprites" / original.name
        write_generated(target, raw_sprite)
        records.append({
            "uri": uri,
            "file": original.name,
            "unityAssetPath": target.relative_to(ROOT / "unity").as_posix(),
            "sha256": sha256(raw_sprite),
            "width": metadata["width"],
            "height": metadata["height"],
            "border": metadata.get("border", [0, 0, 0, 0]),
            "pivot": metadata.get("pivot", [0, 0]),
            "pixelsPerUnit": metadata.get("ppu", 100),
        })

    payload = {
        "manifest": apk.version,
        "mapClassId": 14,
        "sourceUiSha256": sha256(raw),
        "panels": panels,
        "sprites": records,
        "components": components,
        "shaderPrograms": shader_programs,
    }
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    OUTPUT_JSON.write_bytes(json.dumps(payload, ensure_ascii=False, indent=2).encode("utf-8"))
    receipt = {
        "manifest": apk.version,
        "mapClassId": 14,
        "operation": "Decode original Map15 group-world UI prefabs referenced by Map14 story node  OpenAloneGroupWorldPrepareTouchDialogNode.",
        "bundle": apk.provenance(apk.asset(BUNDLE, "/AloneWorldMap/AloneGroupWorldPrepareTouchDialog.prefab")),
        "prefabs": prefabs,
        "schemas": schemas.records,
        "outputs": {
            OUTPUT_JSON.relative_to(ROOT).as_posix(): sha256(OUTPUT_JSON.read_bytes()),
        },
    }
    OUTPUT_MANIFEST.write_bytes(json.dumps(receipt, ensure_ascii=False, indent=2).encode("utf-8"))
    for folder in (OUTPUT_ROOT, OUTPUT_ROOT / "Sprites", OUTPUT_ROOT.parent / "Sprites"):
        _ensure_meta(folder, "DefaultImporter", folder=True)
    for path in (OUTPUT_JSON, OUTPUT_MANIFEST):
        _ensure_meta(path, "TextScriptImporter")
    for folder in (OUTPUT_ROOT / "Sprites", OUTPUT_ROOT.parent / "Sprites"):
        for path in folder.glob("*.png"):
            _ensure_meta(path, "TextureImporter")
    print("Decoded official Map14 standalone-world UI:", ", ".join(PANEL_KEYS))


if __name__ == "__main__":
    build()
