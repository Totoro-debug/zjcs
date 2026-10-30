"""Build the Map14 placement catalog consumed by the existing native entity view."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/14/Source/placement.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map14/Entities/catalog.json"


def build():
    source = json.loads(SOURCE.read_bytes())
    if source.get("id") != 14 or len(source.get("entities", [])) != 2961:
        raise ValueError("Selected Map14 placement count differs")
    entities = []
    for item in source["entities"]:
        grid = item.get("grid")
        if not isinstance(grid, list) or len(grid) != 2:
            raise ValueError("Map14 placement has no two-dimensional grid")
        entities.append({
            "placementId": item["id"], "entityClassId": item["entity"], "typeId": item["type"],
            "grid": grid, "region": item["region"], "direction": item.get("direction", 0),
            "unique": item.get("unique", False), "area": item.get("area", 0), "state": 0,
            "active": bool(item.get("public", [True])[0]), "lockType": 0,
            "lockedByEntityGroups": [], "lockConditionEntityIds": [], "lockParamsJson": "{}",
            "conditionEntityNeverDestroy": False, "conditionEntityInactiveInUnlock": False,
            "awardItems": [], "changeMapClassId": 0, "modelPath": "Map14/NativeEntity/empty",
            "stageIds": sorted(int(key) for key in item.get("stages", {}).keys()),
            "stageBindings": [], "periodicityGroupIds": [], "playerPeriodicityGroupIds": []
        })
    result = {"mapClassId": 14, "entities": entities,
              "models": [{"modelPath": "Map14/NativeEntity/empty", "modelId": "native-prefab",
                           "geometryResource": "", "geometryBatchCount": 0, "spines": [],
                           "interactionsJson": "{}", "monsterSlotsJson": "{}",
                           "overlapDisplay": False, "states": []}], "textureUris": []}
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Map14 entity catalog:", len(entities))


if __name__ == "__main__":
    build()
