"""Build the verified Map14 definition consumed by the shared world session."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MAP_ROOT = ROOT / "unity/Assets/Official/World/Map/14"
SOURCE_ROOT = MAP_ROOT / "Source"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map14-source-manifest.json"
MAP10_DEFINITION = ROOT / "unity/Assets/Official/World/Map/10/map10.json"
OUTPUT = MAP_ROOT / "map14.json"
MANIFEST = MAP_ROOT / "source-manifest.json"


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def encode(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")


def write_meta(path: Path, relative: str, folder: bool = False) -> None:
    guid = hashlib.md5(("zjcs-official:Assets/Official/World/Map/14/" + relative).encode("utf-8")).hexdigest()
    importer = "folderAsset: yes\nDefaultImporter:\n" if folder else "TextScriptImporter:\n"
    body = importer + "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    target = Path(str(path) + ".meta")
    raw = f"fileFormatVersion: 2\nguid: {guid}\n{body}".encode("utf-8")
    if target.exists() and target.read_bytes() != raw:
        raise ValueError("Refusing to overwrite changed Map14 metadata: " + str(target))
    if not target.exists():
        target.write_bytes(raw)


def read_verified_inputs():
    receipt_bytes = SOURCE_RECEIPT.read_bytes()
    receipt = json.loads(receipt_bytes)
    if receipt.get("mapClassId") != 14 or receipt.get("manifest") != "871_156598":
        raise ValueError("Map14 source receipt does not match the selected resource version")
    for item in receipt.get("outputs", []):
        path = ROOT / item["path"]
        if not path.is_file() or sha(path.read_bytes()) != item["sha256"]:
            raise ValueError("Map14 source output differs from its receipt: " + item["path"])
    source_inputs = [{"path": "assets/reference/official-map/map14-source-manifest.json",
                      "sha256": sha(receipt_bytes)}]
    source_inputs.extend({"path": item["path"], "sha256": item["sha256"]}
                         for item in receipt.get("outputs", []))
    return receipt, source_inputs


def build() -> dict:
    receipt, source_inputs = read_verified_inputs()
    def load(name: str):
        return json.loads((SOURCE_ROOT / name).read_bytes())

    navigation = load("navigation.json")
    placement = load("placement.json")
    components = load("entities.json")
    templates = load("templates.json")
    tables = load("tables.json")
    if navigation.get("id") != 14 or placement.get("id") != 14:
        raise ValueError("Map14 navigation or placement identity differs")
    if len(placement.get("entities", [])) != 2961 or len(templates) < 1:
        raise ValueError("Map14 placement or template closure is incomplete")

    map_rows = [row for row in tables["Map/map"] if row.get("ClassId") == 14]
    regions = [row for row in tables["Map/map_region"] if row.get("ClassId") == 14]
    areas = [row for row in tables["Map/map_area"] if row.get("ClassId") == 14]
    worlds = [row for row in tables["Map/map_world"] if row.get("ClassId") == 14]
    if len(map_rows) != 1 or len(regions) != 111 or len(worlds) != 1:
        raise ValueError("Map14 official map, region, or world table rows are incomplete")
    gated_regions = [row["Region"] for row in regions
                     if row.get("LimitServerRank", 0) != 0
                     or row.get("LimitServerRankUnlockDayOffset", 0) != 0
                     or row.get("ReleaseTimeId", 0) != 0
                     or row.get("ReleaseTimeOffset", 0) != 0]
    if gated_regions:
        raise ValueError("Map14 region release rules need explicit offline migration: " + str(gated_regions))
    world = worlds[0]
    if world.get("DefEntryId") != 101 or world.get("AccessibleCondition", {}).get("MapClassId") != 14:
        raise ValueError("Map14 world entry or activation condition differs from its official table")
    placement_ids = {str(item["id"]) for item in placement["entities"]}
    if placement_ids != set(components):
        raise ValueError("Map14 placements and decoded entity components do not match one-to-one")

    map10 = json.loads(MAP10_DEFINITION.read_bytes())
    if map10["CharacterMover"]["CharacterClassId"] != navigation["character"]:
        raise ValueError("Map14 mover does not match the already verified original player mover")
    mover = map10["CharacterMover"]
    map_info = map_rows[0]
    condition_rows = {row["ClassId"]: row for row in tables["conditions"]}
    condition_ids = {row["AccessibleConditionId"] for row in regions if row["AccessibleConditionId"] > 0}
    pending_conditions = list(condition_ids)
    while pending_conditions:
        condition_id = pending_conditions.pop()
        row = condition_rows.get(condition_id)
        if row is None:
            raise ValueError("Map14 region references a missing official condition: " + str(condition_id))
        condition = row.get("Condition") or {}
        if condition.get("_type") == "CompositeAndCondition":
            for child in condition.get("Ids") or []:
                if child not in condition_ids:
                    condition_ids.add(child)
                    pending_conditions.append(child)
    if any((condition_rows[value].get("Condition") or {}).get("_type") not in {
        "WorldMapProgressCondition", "StoryCountCondition", "CompositeAndCondition", "MapLogicEntityEndByTagCondition"
    } for value in condition_ids):
        raise ValueError("Map14 contains an unbound official region condition type")
    table_names = {
        "Map/map": "TbMapInfo", "Map/map_region": "TbMapRegionInfo", "Map/map_area": "TbMapAreaInfo",
        "Map/map_world": "TbMapWorldInfo", "Map/map_logic_entity_tag": "TbMapLogicEntityTag",
        "Map/entity_extend_parameter": "TbEntityExtendParameterInfo",
        "Map/entity_map_display": "TbEntityMapDisplayInfo",
        "Map/entity_map_ui_display": "TbEntityMapUiDisplayInfo",
        "Map/map_entity_stage_action": "TbMapEntityStageAction", "Map/map_region_bgm": "TbMapRegionBgmInfo",
        "map_stage": "TbMapToStageInfo",
    }
    definition = {
        "ClassId": 14,
        "MapGroupId": map_info["MapGroupId"],
        "MapTagId": map_info["MapTagId"],
        "MapBgmId": map_info["MapBgmId"],
        "NextMapClassId": map_info["NextMapClassId"],
        "OriginalGroundPrefab": "Assets/Partition/MapGround_14/MapGround_14_bare.prefab",
        "OriginalMiniMapPrefab": map_info["MiniMapPath"],
        "SourceManifest": "Assets/Official/World/Map/14/source-manifest.json",
        "CharacterMover": mover,
        "MaxRegionProgress": [
            {"Region": int(key), "Value": value}
            for key, value in sorted(placement["progress"].items(), key=lambda pair: int(pair[0]))
        ],
        "Navigation": {
            "Width": navigation["size"][0], "Height": navigation["size"][1],
            "Origin": {"X": navigation["origin"][0], "Y": navigation["origin"][1], "Z": navigation["origin"][2]},
            "CharacterId": navigation["character"], "MemberId": navigation["member"],
            "Rows": [{"Cells": [{"RawValues": cell} for cell in row]} for row in navigation["cells"]],
            "AreaMappings": [{"Cell": cell, "Value": value} for cell, value in navigation["areas"]],
            "MaxSteps": navigation["max_steps"],
        },
        "Entities": [
            {"Id": item["id"], "Region": item["region"], "Refresh": item["refresh"],
             "Public": item["public"], "Private": item["private"], "Grid": item["grid"],
             "Direction": item["direction"], "Unique": item["unique"], "Area": item["area"],
             "EntityId": item["entity"], "TypeId": item["type"],
             "Stages": [{"Key": str(key), "ValueJson": json.dumps(value, ensure_ascii=False, separators=(",", ":"))}
                        for key, value in sorted(item.get("stages", {}).items(), key=lambda pair: str(pair[0]))]}
            for item in placement["entities"]
        ],
        "EntityComponents": [
            {"EntityId": int(identity), "Json": json.dumps(value, ensure_ascii=False, separators=(",", ":"), sort_keys=True)}
            for identity, value in sorted(components.items(), key=lambda pair: int(pair[0]))
        ],
        "Configs": [
            {"TableName": table_names[name],
             "RecordId": str(row.get("ClassId", row.get("Region", ""))),
             "Json": json.dumps(row, ensure_ascii=False, separators=(",", ":"), sort_keys=True),
             "Sha256": sha(encode(row)), "SourceBundle": "Map14/Source/tables.json", "SourcePathId": name}
            for name in ("Map/map", "Map/map_region", "Map/map_area", "Map/map_world", "Map/map_logic_entity_tag",
                         "Map/entity_extend_parameter", "Map/entity_map_display", "Map/entity_map_ui_display",
                         "Map/map_entity_stage_action", "Map/map_region_bgm", "map_stage")
            for row in tables.get(name, [])
            if row.get("ClassId", row.get("MapClassId", row.get("MapClass"))) == 14
            or ("EntityClassId" in row and row["EntityClassId"] in {item["entity"] for item in placement["entities"]})
        ],
    }
    definition["Configs"].extend(
        {"TableName": "TbConditionInfo", "RecordId": str(condition_id),
         "Json": json.dumps(condition_rows[condition_id], ensure_ascii=False, separators=(",", ":"), sort_keys=True),
         "Sha256": sha(encode(condition_rows[condition_id])),
         "SourceBundle": "Map14/Source/tables.json", "SourcePathId": "conditions"}
        for condition_id in sorted(condition_ids)
    )
    definition_bytes = encode(definition)
    inputs = source_inputs + [{"path": "unity/Assets/Official/World/Map/10/map10.json", "sha256": sha(MAP10_DEFINITION.read_bytes())}]
    inputs_json = json.dumps(inputs, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    inputs_sha = sha(inputs_json.encode("utf-8"))
    definition["SourceManifestSha256"] = inputs_sha
    definition_bytes = encode(definition)
    manifest = {"format": 1, "mapClassId": 14, "sourceInputsJson": inputs_json,
                "sourceInputsSha256": inputs_sha,
                "output": {"path": "map14.json", "size": len(definition_bytes), "sha256": sha(definition_bytes)},
                "inputs": inputs}
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    if OUTPUT.exists() or MANIFEST.exists():
        if not OUTPUT.is_file() or not MANIFEST.is_file():
            raise ValueError("Map14 definition and source manifest must exist together")
        previous = json.loads(MANIFEST.read_bytes())
        current_output = OUTPUT.read_bytes()
        if previous.get("mapClassId") != 14 or (
            previous.get("output", {}).get("sha256") != sha(current_output)
            and current_output != definition_bytes
        ):
            raise ValueError("Refusing to overwrite a changed Map14 definition")
    for path, raw, relative in ((OUTPUT, definition_bytes, "map14.json"), (MANIFEST, encode(manifest), "source-manifest.json")):
        if not path.exists() or path.read_bytes() != raw:
            path.write_bytes(raw)
        write_meta(path, relative)
    return {"mapClassId": 14, "regions": len(regions), "regionAccessConditions": len(condition_ids),
            "timedOrRankRegionGates": 0, "entities": len(definition["Entities"]),
            "entityComponents": len(definition["EntityComponents"]), "entryId": world["DefEntryId"],
            "sha256": sha(definition_bytes)}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False))
