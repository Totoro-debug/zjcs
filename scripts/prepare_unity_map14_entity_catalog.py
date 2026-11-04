"""Build Map14's shared host input from its verified original EC components."""
import hashlib
import json
from pathlib import Path

from mainline_entity_catalog import read_inputs, project_entities

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map14/Entities/catalog.json"
RECEIPT = ROOT / "assets/reference/official-map/map14-entity-catalog-source-manifest.json"


def build():
    definition, components, inputs = read_inputs(14, "871_156598")
    entities = project_entities(definition, components, "Map14/NativeEntity/empty")
    # Keep the established Map14 JSON field order for reviewable data changes.
    for entity in entities:
        entity["periodicityGroupIds"] = entity.pop("periodicityGroupIds")
        entity["playerPeriodicityGroupIds"] = entity.pop("playerPeriodicityGroupIds")
    result = {"mapClassId": 14, "entities": entities,
              "models": [{"modelPath": "Map14/NativeEntity/empty", "modelId": "native-prefab",
                           "geometryResource": "", "geometryBatchCount": 0, "spines": [],
                           "interactionsJson": "{}", "monsterSlotsJson": "{}",
                           "overlapDisplay": False, "states": []}], "textureUris": []}
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    raw = (json.dumps(result, ensure_ascii=False, indent=2) + "\n").encode("utf8")
    OUTPUT.write_bytes(raw)
    inputs += (Path(__file__), ROOT / "scripts/mainline_entity_catalog.py",
               ROOT / "scripts/prepare_unity_map10_entities.py")
    receipt = {"format": 1, "manifest": "871_156598", "mapClassId": 14,
               "inputs": [{"path": path.relative_to(ROOT).as_posix(),
                           "sha256": hashlib.sha256(path.read_bytes()).hexdigest()} for path in inputs],
               "output": {"path": OUTPUT.relative_to(ROOT).as_posix(),
                          "sha256": hashlib.sha256(raw).hexdigest(), "entityCount": len(entities)},
               "operation": "Project the verified original placement EC state, active flag, locks, awards, map changes, battle bindings and periodicity groups through the shared entity catalog implementation.",
               "limitations": ["This is placement configuration, not a server entity snapshot or a spawn/refresh algorithm. Native prefab presentation and local saved state remain with the owning map host."]}
    RECEIPT.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + "\n").encode("utf8"))
    print("Map14 verified entity catalog:", len(entities))


if __name__ == "__main__":
    build()
