"""Build the pinned Map11 Unity definition and source manifest."""
import hashlib
import json
from pathlib import Path
from official_apk import Apk
from official_tables import Constructors


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "unity/Assets/Official/World/Map/11"
ADVENTURE = ROOT / "assets/official/adventure.json"
REGION = ROOT / "assets/official/regions/11.json"
ENTITY_COMPONENTS = ROOT / "assets/official/regions/11-entities.json"
MATERIALS = ROOT / "assets/reference/official-map/materials-11-source-manifest.json"
MAP10_DEFINITION = ROOT / "unity/Assets/Official/World/Map/10/map10.json"


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def write_meta(path, relative, folder=False):
    guid = hashlib.md5(("zjcs-official:Assets/Official/World/Map/11/" + relative).encode("utf-8")).hexdigest()
    if folder:
        body = "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        body = "TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    target = Path(str(path) + ".meta")
    payload = f"fileFormatVersion: 2\nguid: {guid}\n{body}".encode("utf-8")
    if target.exists() and target.read_bytes() != payload:
        raise ValueError("Refusing to overwrite changed Map11 Unity metadata: " + str(target))
    if not target.exists():
        target.write_bytes(payload)


def selected(rows, map_id, keys=("ClassId", "MapClassId", "MapClass")):
    return [row for row in rows if any(row.get(key) == map_id for key in keys)]


def build():
    adventure_bytes = ADVENTURE.read_bytes()
    region_bytes = REGION.read_bytes()
    entity_components_bytes = ENTITY_COMPONENTS.read_bytes()
    materials_bytes = MATERIALS.read_bytes()
    tables = json.loads(adventure_bytes)["tables"]
    region_data = json.loads(region_bytes)
    entity_components = json.loads(entity_components_bytes)
    materials = json.loads(materials_bytes)
    map10 = json.loads(MAP10_DEFINITION.read_text(encoding="utf-8"))
    apk = Apk()

    if materials.get("manifest") != "854_156383" or materials.get("map") != 11 or region_data.get("id") != 11:
        raise ValueError("Map11 inputs do not match the pinned original package")
    map_rows = selected(tables["Map/map"], 11)
    region_rows = selected(tables["Map/map_region"], 11)
    area_rows = selected(tables["Map/map_area"], 11)
    world_rows = selected(tables["Map/map_world"], 11)
    logic_tags = selected(tables["Map/map_logic_entity_tag"], 11)
    entity_classes = {item["entity"] for item in region_data["placement"]["entities"]}
    entity_parameters = [row for row in tables["Map/entity_extend_parameter"]
                        if row["EntityClassId"] in entity_classes]
    entity_displays = [row for row in tables["Map/entity_map_display"]
                       if row["EntityClassId"] in entity_classes and row["MapClassId"] in (0, 11)]
    entity_ui_displays = [row for row in tables["Map/entity_map_ui_display"]
                          if row["EntityClassId"] in entity_classes and row["MapClassId"] in (0, 11)]
    entity_stage_actions = [row for row in tables["Map/map_entity_stage_action"]
                            if row["EntityClassId"] in entity_classes]
    region_bgm = selected(tables["Map/map_region_bgm"], 11)
    map_stages = selected(tables["map_stage"], 11)
    if len(map_rows) != 1 or len(region_rows) != 77 or len(world_rows) != 1:
        raise ValueError("Official Map11 map, region, or world table rows are incomplete")
    if len(region_data.get("placement", {}).get("entities", [])) != 1803:
        raise ValueError("Map11 placement count differs from its converted official source")
    placement_ids = {str(item["id"]) for item in region_data["placement"]["entities"]}
    if placement_ids != set(entity_components):
        raise ValueError("Map11 placements and official entity component records do not match one-to-one")

    condition_ids = {row["AccessibleConditionId"] for row in region_rows if row["AccessibleConditionId"] > 0}
    condition_object = apk.asset("config_luban", "Assets/Config/Luban/conditions.g.bytes")
    condition_bytes = condition_object.read().m_Script.encode("utf-8", "surrogateescape")
    all_conditions = Constructors().table(condition_bytes, "TbConditionInfo")
    conditions = [row for row in all_conditions if row["ClassId"] in condition_ids]
    if {row["ClassId"] for row in conditions} != condition_ids:
        raise ValueError("Map11 region access conditions are missing from the pinned official condition table")

    navigation = region_data["navigation"]
    if map10["CharacterMover"]["CharacterClassId"] != navigation["character"]:
        raise ValueError("Map11 player mover does not match the checked Map10 character mover")
    mover = map10["CharacterMover"]
    map_info = map_rows[0]
    entities = []
    for item in region_data["placement"]["entities"]:
        entities.append({
            "Id": item["id"], "Region": item["region"], "Refresh": item["refresh"],
            "Public": item["public"], "Private": item["private"], "Grid": item["grid"],
            "Direction": item["direction"], "Unique": item["unique"], "Area": item["area"],
            "EntityId": item["entity"], "TypeId": item["type"],
            "Stages": [{"Key": str(key), "ValueJson": json.dumps(value, ensure_ascii=False, separators=(",", ":"))}
                       for key, value in sorted(item.get("stages", {}).items(), key=lambda pair: str(pair[0]))],
        })

    config_sets = [
        ("TbMapInfo", map_rows), ("TbMapRegionInfo", region_rows), ("TbMapAreaInfo", area_rows),
        ("TbMapWorldInfo", world_rows), ("TbMapLogicEntityTag", logic_tags), ("TbConditionInfo", conditions),
        ("TbEntityExtendParameterInfo", entity_parameters), ("TbEntityMapDisplayInfo", entity_displays),
        ("TbEntityMapUiDisplayInfo", entity_ui_displays), ("TbMapEntityStageAction", entity_stage_actions),
        ("TbMapRegionBgmInfo", region_bgm), ("TbMapToStageInfo", map_stages),
    ]
    configs = []
    for table_name, rows in config_sets:
        for row in rows:
            if "EntityClassId" in row:
                key = str(row.get("MapClassId", 0)) + ":" + str(row["EntityClassId"])
            else:
                key = next((str(row[field]) for field in ("ClassId", "Region", "LogicId", "MapClassId", "StageId")
                            if field in row), "")
            row_json = json.dumps(row, ensure_ascii=False, separators=(",", ":"), sort_keys=True)
            configs.append({
                "TableName": table_name, "RecordId": key, "Json": row_json,
                "Sha256": sha256(encoded(row)),
                "SourceBundle": "assets/official/adventure.json" if table_name != "TbConditionInfo"
                    else apk.provenance(condition_object)["bundle"],
                "SourcePathId": table_name if table_name != "TbConditionInfo"
                    else apk.provenance(condition_object)["path_id"],
            })

    source_inputs = [
        {"path": "assets/official/adventure.json", "sha256": sha256(adventure_bytes)},
        {"path": "assets/official/regions/11.json", "sha256": sha256(region_bytes)},
        {"path": "assets/official/regions/11-entities.json", "sha256": sha256(entity_components_bytes)},
        {"path": "assets/reference/official-map/materials-11-source-manifest.json", "sha256": sha256(materials_bytes)},
        {"bundle": apk.provenance(condition_object)["bundle"],
         "path": "Assets/Config/Luban/conditions.g.bytes",
         "pathId": apk.provenance(condition_object)["path_id"], "sha256": sha256(condition_bytes)},
    ]
    source_inputs_json = json.dumps(source_inputs, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    inputs_hash = sha256(source_inputs_json.encode("utf-8"))
    definition = {
        "ClassId": 11, "MapGroupId": map_info["MapGroupId"], "MapTagId": map_info["MapTagId"],
        "MapBgmId": map_info["MapBgmId"], "NextMapClassId": map_info["NextMapClassId"],
        "OriginalGroundPrefab": "Assets/Prefabs/Map/Map_11/MapGround_11.prefab",
        "OriginalMiniMapPrefab": "Assets/Prefabs/Map/Map_11/MiniMap/MiniMap_11.prefab",
        "SourceManifest": "Assets/Official/World/Map/11/source-manifest.json",
        "SourceManifestSha256": inputs_hash,
        "CharacterMover": mover,
        "MaxRegionProgress": [{"Region": key, "Value": value}
                              for key, value in sorted(region_data["placement"]["progress"].items(), key=lambda pair: int(pair[0]))],
        "Navigation": {
            "Width": navigation["size"][0], "Height": navigation["size"][1],
            "Origin": {"X": navigation["origin"][0], "Y": navigation["origin"][1], "Z": navigation["origin"][2]},
            "CharacterId": navigation["character"], "MemberId": navigation["member"],
            "Rows": [{"Cells": [{"RawValues": cell} for cell in row]} for row in navigation["cells"]],
            "AreaMappings": [{"Cell": cell, "Value": value} for cell, value in navigation["areas"]],
            "MaxSteps": navigation["max_steps"],
        },
        "Entities": entities,
        "EntityComponents": [
            {"EntityId": int(entity_id), "Json": json.dumps(components, ensure_ascii=False, separators=(",", ":"), sort_keys=True)}
            for entity_id, components in sorted(entity_components.items(), key=lambda pair: int(pair[0]))
        ],
        "Configs": configs,
    }
    definition_bytes = encoded(definition)
    manifest = {
        "format": 1, "mapClassId": 11, "sourceInputsJson": source_inputs_json,
        "sourceInputsSha256": inputs_hash,
        "output": {"path": "map11.json", "size": len(definition_bytes), "sha256": sha256(definition_bytes)},
        "inputs": source_inputs,
    }
    OUTPUT.mkdir(parents=True, exist_ok=True)
    previous_manifest_path = OUTPUT / "source-manifest.json"
    previous_manifest = json.loads(previous_manifest_path.read_text(encoding="utf-8")) \
        if previous_manifest_path.is_file() else None
    previous_definition_path = OUTPUT / "map11.json"
    previous_definition_verified = previous_manifest is not None and previous_manifest.get("mapClassId") == 11 \
        and previous_manifest.get("output", {}).get("sha256") == sha256(previous_definition_path.read_bytes())
    for name, payload in (("map11.json", definition_bytes), ("source-manifest.json", encoded(manifest))):
        target = OUTPUT / name
        if target.exists() and target.read_bytes() != payload and not previous_definition_verified:
            raise ValueError("Refusing to overwrite changed Map11 Unity data: " + str(target))
        if not target.exists() or target.read_bytes() != payload:
            target.write_bytes(payload)
        write_meta(target, name)
    write_meta(OUTPUT, "", folder=True)
    print(f"Map11 definition: {len(region_rows)} regions, {len(area_rows)} areas, {len(entities)} entities")


if __name__ == "__main__":
    build()
