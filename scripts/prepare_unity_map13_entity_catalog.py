"""Build the shared world entity index for official Map13 placements.

The runtime catalog keeps the exact placement/state/lock metadata from the
official map definition. Visual prefab binding is kept in the native entity
closure and is added by the Map13 native world host.
"""
import hashlib
import json
import re
from pathlib import Path

from prepare_unity_map10_entities import entity_runtime_rules, stage_behavior_bindings, stage_ids_for_entity

ROOT = Path(__file__).resolve().parents[1]
MAP = ROOT / "unity/Assets/Official/World/Map/13/map13.json"
MANIFEST = ROOT / "unity/Assets/Official/World/Map/13/source-manifest.json"
OUT = ROOT / "unity/Assets/Game/Resources/Map13/Entities/catalog.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-entity-catalog-source-manifest.json"


def digest(value):
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def main():
    definition_text = MAP.read_text(encoding="utf-8")
    definition = json.loads(definition_text)
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if definition.get("ClassId") != 13 or definition.get("MapGroupId") != 13:
        raise ValueError("Map13 definition identity differs from the selected map.")
    if manifest.get("mapClassId") != 13 or manifest.get("output", {}).get("sha256") != digest(definition_text):
        raise ValueError("Map13 definition does not match its source manifest.")
    entities = []
    components_path = ROOT / "unity/Assets/Official/World/Map/13/Source/entities.json"
    receipt_path = ROOT / "assets/reference/official-map/map13-source-manifest.json"
    receipt = json.loads(receipt_path.read_bytes())
    expected = next(row["sha256"] for row in receipt["outputs"]
                    if row["path"] == components_path.relative_to(ROOT).as_posix())
    if receipt["manifest"] != "871_156598" or hashlib.sha256(components_path.read_bytes()).hexdigest() != expected:
        raise ValueError("Map13 original placement components differ from their source receipt.")
    components = json.loads(components_path.read_bytes())
    # Original EC inline dictionaries also use unquoted numeric entity IDs.
    # Normalize JSON syntax only; retain every key, value and list order.
    for local in components.values():
        lock = local.get("LoEntityLockComponent")
        if lock:
            normalized = re.sub(r'([{,]\s*)([A-Za-z_][A-Za-z0-9_]*|[0-9]+)(\s*:)', r'\1"\2"\3', lock.get("Params") or "{}")
            lock["Params"] = json.dumps(json.loads(normalized), ensure_ascii=False, separators=(",", ":"))
    for source in definition.get("Entities", []):
        if not source or int(source.get("Id", 0)) <= 0:
            raise ValueError("Map13 contains an invalid entity placement.")
        grid = source.get("Grid") or []
        if len(grid) != 2:
            raise ValueError("Map13 entity placement has no two-dimensional grid.")
        entities.append({
            "placementId": int(source["Id"]),
            "entityClassId": int(source.get("EntityId", 0)),
            "typeId": int(source.get("TypeId", 0)),
            "grid": [int(grid[0]), int(grid[1])],
            "region": int(source.get("Region", 0)),
            "direction": int(source.get("Direction", 0)),
            "unique": bool(source.get("Unique", False)),
            "area": int(source.get("Area", 0)),
            "state": components[str(source["Id"])].get("LoWorldStageComponent", {}).get("State", 0),
            "active": components[str(source["Id"])].get("LoWorldActiveableEntityComponent", {}).get("Active", True),
            **entity_runtime_rules(int(source["Id"]), components),
            "modelPath": "Map13/NativeEntity/empty",
            "stageIds": stage_ids_for_entity(source, components),
            "stageBindings": stage_behavior_bindings(int(source["Id"]), components),
        })
    ids = [row["placementId"] for row in entities]
    if len(entities) != 2690 or len(set(ids)) != len(ids):
        raise ValueError("Map13 entity placement count or identity differs from the official definition.")
    payload = {
        "mapClassId": 13,
        "entities": entities,
        "models": [{"modelPath": "Map13/NativeEntity/empty", "modelId": "empty", "geometryResource": "", "geometryBatchCount": 0, "spines": []}],
        "textureUris": [],
    }
    text = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_bytes(text.encode("utf-8"))
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_bytes((json.dumps({
        "format": 1,
        "manifest": "871_156598",
        "mapClassId": 13,
        "source": {"path": "unity/Assets/Official/World/Map/13/map13.json", "sha256": digest(definition_text), "entityCount": len(entities)},
        "inputs": [{"path": path.relative_to(ROOT).as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                   for path in (components_path, receipt_path, Path(__file__),
                                ROOT / "scripts/prepare_unity_map10_entities.py")],
        "output": {"path": "unity/Assets/Game/Resources/Map13/Entities/catalog.json", "sha256": digest(text), "entityCount": len(entities)},
        "limitations": ["Native prefab rendering is bound separately by the Map13 native entity host."]
    }, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    print(json.dumps({"entities": len(entities), "sha256": digest(text)}))


if __name__ == "__main__":
    main()
