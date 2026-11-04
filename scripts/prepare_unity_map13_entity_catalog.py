"""Build the shared world entity index for official Map13 placements.

The runtime catalog keeps the exact placement/state/lock metadata from the
official map definition. Visual prefab binding is kept in the native entity
closure and is added by the Map13 native world host.
"""
import hashlib
import json
from pathlib import Path

from mainline_entity_catalog import read_inputs, project_entities

ROOT = Path(__file__).resolve().parents[1]
MAP = ROOT / "unity/Assets/Official/World/Map/13/map13.json"
OUT = ROOT / "unity/Assets/Game/Resources/Map13/Entities/catalog.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-entity-catalog-source-manifest.json"


def digest(value):
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def main():
    definition, components, inputs = read_inputs(13, "871_156598")
    definition_text = MAP.read_text(encoding="utf-8")
    if definition.get("MapGroupId") != 13:
        raise ValueError("Map13 definition identity differs from the selected map.")
    entities = project_entities(definition, components, "Map13/NativeEntity/empty")
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
                   for path in inputs + (Path(__file__),
                                         ROOT / "scripts/prepare_unity_map10_entities.py",
                                         ROOT / "scripts/mainline_entity_catalog.py")],
        "output": {"path": "unity/Assets/Game/Resources/Map13/Entities/catalog.json", "sha256": digest(text), "entityCount": len(entities)},
        "limitations": ["Native prefab rendering is bound separately by the Map13 native entity host."]
    }, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))
    print(json.dumps({"entities": len(entities), "sha256": digest(text)}))


if __name__ == "__main__":
    main()
