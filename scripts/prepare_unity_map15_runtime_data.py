"""Build Map15's shared world definition and entity catalog from verified official data."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAP_ROOT = ROOT / "unity/Assets/Official/World/Map/15"
RESOURCE_ROOT = ROOT / "unity/Assets/Game/Resources/Map15"
REGION = ROOT / "assets/official/regions/15.json"
ENTITY_SOURCE = ROOT / "assets/official/regions/15-entities.json"
GROUP = RESOURCE_ROOT / "Group/maps/15.json"
DEFINITION = MAP_ROOT / "map15.json"
MANIFEST = MAP_ROOT / "source-manifest.json"
CATALOG = RESOURCE_ROOT / "Entities/catalog.json"


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def encode(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")


def write_meta(path: Path, relative: str) -> None:
    guid = hashlib.md5(("zjcs-map15:" + relative).encode("utf-8")).hexdigest()
    raw = ("fileFormatVersion: 2\n" + f"guid: {guid}\n"
           + "TextScriptImporter:\n  externalObjects: {}\n  userData: \n"
           + "  assetBundleName: \n  assetBundleVariant: \n").encode("utf-8")
    target = Path(str(path) + ".meta")
    if target.exists() and target.read_bytes() != raw:
        raise ValueError("Refusing to overwrite changed Map15 metadata: " + str(target))
    if not target.exists():
        target.write_bytes(raw)


def build() -> dict:
    region = json.loads(REGION.read_bytes())
    entity_components = json.loads(ENTITY_SOURCE.read_bytes())
    group = json.loads(GROUP.read_bytes())
    navigation = region["navigation"]
    placement = region["placement"]
    entities = placement["entities"]
    if region.get("id") != 15 or placement.get("id") != 15 or navigation.get("id") != 15:
        raise ValueError("Map15 source identity differs")
    if len(entities) != 26 or len(entity_components) != 26:
        raise ValueError("Map15 placement/component closure must contain 26 entries")
    if set(entity_components) != {str(item["id"]) for item in entities}:
        raise ValueError("Map15 placements and entity components do not match one-to-one")

    mover = {
        "CharacterClassId": navigation["character"],
        "MoveActionId": 22021,
        "MoveMask": 65,
        "ServerOverrideGroundTypes": [7],
        "WalkableGroundTypeKey": 3,
        "WalkableGroundTypeServerOverrideKey": 1,
        "WalkableGroundTypes": [0, 6],
    }
    definition = {
        "CharacterMover": mover,
        "ClassId": 15,
        "Configs": [{
            "Json": json.dumps({
                "ClassId": 15, "MapGroupId": 15, "MapTagId": 0, "MapBgmId": 15,
                "DefEntryId": 101, "NextMapClassId": 42001, "CanExplore": False,
            }, ensure_ascii=False, separators=(",", ":"), sort_keys=True),
            "RecordId": "15", "Sha256": "", "SourceBundle": "Map15/regions/15.json",
            "SourcePathId": "Map/map_world", "TableName": "TbMapWorldInfo",
        }],
        "EntityComponents": [
            {"EntityId": int(identity), "Json": json.dumps(value, ensure_ascii=False,
             separators=(",", ":"), sort_keys=True)}
            for identity, value in sorted(entity_components.items(), key=lambda pair: int(pair[0]))
        ],
        "Entities": [{
            "Id": item["id"], "Region": item["region"], "Refresh": item["refresh"],
            "Public": item["public"], "Private": item["private"], "Grid": item["grid"],
            "Direction": item["direction"], "Unique": item["unique"], "Area": item["area"],
            "EntityId": item["entity"], "TypeId": item["type"],
            "Stages": [{"Key": str(key), "ValueJson": json.dumps(value, ensure_ascii=False,
             separators=(",", ":"))} for key, value in sorted(item.get("stages", {}).items(),
             key=lambda pair: str(pair[0]))],
        } for item in entities],
        "MapBgmId": 15,
        "MapGroupId": 15,
        "MapTagId": 0,
        "MaxRegionProgress": [{"Region": int(key), "Value": value}
                              for key, value in sorted(placement["progress"].items(),
                                                       key=lambda pair: int(pair[0]))],
        "Navigation": {
            "Width": navigation["size"][0], "Height": navigation["size"][1],
            "Origin": {"X": navigation["origin"][0], "Y": navigation["origin"][1],
                        "Z": navigation["origin"][2]},
            "CharacterId": navigation["character"], "MemberId": navigation.get("member", 3002),
            "Rows": [{"Cells": [{"RawValues": cell} for cell in row]} for row in navigation["cells"]],
            "AreaMappings": [{"Cell": cell, "Value": value} for cell, value in navigation["areas"]],
            "MaxSteps": navigation["max_steps"],
        },
        "NextMapClassId": 42001,
        "OriginalGroundPrefab": "Assets/Partition/MapGround_Tower15_SnowFloor1/MapGround_Tower15_SnowFloor1.prefab",
        "OriginalMiniMapPrefab": "",
        "SourceManifest": "Assets/Official/World/Map/15/source-manifest.json",
    }
    for config in definition["Configs"]:
        config["Sha256"] = sha(config["Json"].encode("utf-8"))

    catalog = {
        "mapClassId": 15,
        "entities": [{
            "placementId": item["id"], "entityClassId": item["entity"], "typeId": item["type"],
            "grid": item["grid"], "region": item["region"], "direction": item["direction"],
            "unique": item["unique"], "area": item["area"], "state": 0,
            "active": bool(item["public"][0]), "lockType": 0,
            "lockedByEntityGroups": [], "lockConditionEntityIds": [], "lockParamsJson": "{}",
            "conditionEntityNeverDestroy": False, "conditionEntityInactiveInUnlock": False,
            "awardItems": [], "changeMapClassId": 0,
            "modelPath": "Map15/NativeEntity/empty", "stageIds": [], "stageBindings": [],
            "periodicityGroupIds": [], "playerPeriodicityGroupIds": [],
        } for item in entities],
        "models": [{"modelPath": "Map15/NativeEntity/empty", "modelId": "Map15Empty",
                     "geometryResource": "", "geometryBatchCount": 0, "spines": []}],
        "textureUris": [],
    }
    inputs = [{"path": path.as_posix().replace("\\", "/"), "sha256": sha(path.read_bytes())}
              for path in (REGION, ENTITY_SOURCE, GROUP)]
    inputs_json = json.dumps(inputs, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    inputs_sha = sha(inputs_json.encode("utf-8"))
    definition["SourceManifestSha256"] = inputs_sha
    definition_bytes = encode(definition)
    manifest = {"format": 1, "mapClassId": 15, "sourceInputsJson": inputs_json,
                "sourceInputsSha256": inputs_sha,
                "output": {"path": "map15.json", "size": len(definition_bytes),
                           "sha256": sha(definition_bytes)}, "inputs": inputs}
    for path, raw in ((DEFINITION, definition_bytes), (MANIFEST, encode(manifest)),
                      (CATALOG, encode(catalog))):
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and path in (DEFINITION, MANIFEST) and path.read_bytes() != raw:
            # Generated outputs are owned by this script; a changed source should be reproducible.
            pass
        path.write_bytes(raw)
        relative = path.relative_to(ROOT / "unity/Assets").as_posix()
        write_meta(path, relative)
    return {"mapClassId": 15, "entities": len(entities), "components": len(entity_components),
            "navigation": [navigation["size"][0], navigation["size"][1]],
            "sha256": sha(definition_bytes)}


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False))
