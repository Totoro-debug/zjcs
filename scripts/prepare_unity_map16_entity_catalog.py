"""Project Whale Island's pinned placements through the shared entity converter."""
import hashlib
import json
from pathlib import Path

from mainline_entity_catalog import project_entities, read_inputs
from prepare_unity_map13_story_runtime import json_meta

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map16/Entities/catalog.json"
RECEIPT = ROOT / "assets/reference/official-map/map16-entity-catalog-source-manifest.json"


def build():
    definition, components, inputs = read_inputs(
        16, "884_156692", components_receipt="assets/reference/official-map/map16-definition-source-manifest.json")
    model_path = "Map16/NativeEntity/empty"
    entities = project_entities(definition, components, model_path)
    if len(entities) != 1021:
        raise ValueError("Whale Island's original placement closure differs.")
    payload = {"mapClassId": 16, "entities": entities,
               "models": [{"modelPath": model_path, "modelId": "native-prefab",
                           "geometryResource": "", "geometryBatchCount": 0, "spines": []}],
               "textureUris": []}
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    raw = (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf8")
    OUTPUT.write_bytes(raw)
    json_meta(OUTPUT)
    inputs += (Path(__file__), ROOT / "scripts/mainline_entity_catalog.py",
               ROOT / "scripts/prepare_unity_map10_entities.py",
               ROOT / "scripts/prepare_unity_map13_story_runtime.py")
    receipt = {"format": 1, "manifest": "884_156692", "mapClassId": 16,
               "inputs": [{"path": path.relative_to(ROOT).as_posix(),
                           "sha256": hashlib.sha256(path.read_bytes()).hexdigest()} for path in inputs],
               "outputs": [{"path": path.relative_to(ROOT).as_posix(),
                            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                           for path in (OUTPUT, OUTPUT.with_name(OUTPUT.name + ".meta"))],
               "entityCount": len(entities),
               "operation": "Project pinned official placement state, activation, locks, rewards, map targets, battle bindings and periodicity through the shared converter.",
               "limitations": ["Placement configuration only; no server spawn/refresh algorithm or saved snapshot migration.",
                               "The model entry contains no replacement art. Original Windows prefab binding and the formal Map16 host remain pending."]}
    RECEIPT.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + "\n").encode("utf8"))
    print("Map16 verified entity catalog:", len(entities))


if __name__ == "__main__":
    build()
