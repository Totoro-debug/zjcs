"""Project verified original placement components into the shared map host."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re

from prepare_unity_map10_entities import entity_runtime_rules, stage_behavior_bindings, stage_ids_for_entity

ROOT = Path(__file__).resolve().parents[1]


def read_inputs(map_id: int, manifest: str, root: Path = ROOT):
    directory = root / f"unity/Assets/Official/World/Map/{map_id}"
    definition_path = directory / f"map{map_id}.json"
    definition_receipt = directory / "source-manifest.json"
    components_path = directory / "Source/entities.json"
    components_receipt = root / f"assets/reference/official-map/map{map_id}-source-manifest.json"
    raw = definition_path.read_bytes()
    definition = json.loads(raw)
    receipt = json.loads(definition_receipt.read_bytes())
    if (definition.get("ClassId") != map_id or receipt.get("mapClassId") != map_id
            or receipt.get("output", {}).get("sha256") != hashlib.sha256(raw).hexdigest()):
        raise ValueError(f"Map{map_id} definition differs from its source receipt")
    components_raw = components_path.read_bytes()
    receipt = json.loads(components_receipt.read_bytes())
    matches = [row for row in receipt.get("outputs", [])
               if row.get("path") == components_path.relative_to(root).as_posix()]
    if (receipt.get("manifest") != manifest or receipt.get("mapClassId") != map_id
            or len(matches) != 1 or matches[0].get("sha256") != hashlib.sha256(components_raw).hexdigest()):
        raise ValueError(f"Map{map_id} placement components differ from their source receipt")
    # Also verify the definition's own declared inputs, including its independent
    # source version. A valid output hash alone does not establish provenance.
    for entry in json.loads(definition_receipt.read_bytes()).get("inputs", []):
        if hashlib.sha256((root / entry["path"]).read_bytes()).hexdigest() != entry["sha256"]:
            raise ValueError(f"Map{map_id} definition input differs: {entry['path']}")
    return definition, json.loads(components_raw), (definition_path, definition_receipt, components_path, components_receipt)


def project_entities(definition: dict, components: dict, model_path: str) -> list[dict]:
    placements = definition.get("Entities")
    if not placements or len({row.get("Id") for row in placements}) != len(placements):
        raise ValueError("Original entity placement identities are missing or duplicated")
    if set(components) != {str(row["Id"]) for row in placements}:
        raise ValueError("Original entity placement/component closure differs")
    result = []
    for source in placements:
        identity = source["Id"]
        grid = source.get("Grid")
        if not isinstance(identity, int) or identity <= 0 or not isinstance(grid, list) or len(grid) != 2:
            raise ValueError("Original entity identity or grid is malformed")
        local = dict(components[str(identity)])
        # Official inline object syntax permits unquoted integer keys (e.g.
        # EntityStateDict:{208:1}). Only JSON punctuation is normalized.
        lock = local.get("LoEntityLockComponent")
        if lock:
            lock = dict(lock)
            normalized = re.sub(r'([{,]\s*)([A-Za-z_][A-Za-z0-9_]*|[0-9]+)(\s*:)',
                                r'\1"\2"\3', lock.get("Params") or "{}")
            lock["Params"] = json.dumps(json.loads(normalized), ensure_ascii=False, separators=(",", ":"))
            local["LoEntityLockComponent"] = lock
        scoped = {str(identity): local}
        result.append({
            "placementId": identity, "entityClassId": int(source.get("EntityId", 0)),
            "typeId": int(source.get("TypeId", 0)), "grid": [int(grid[0]), int(grid[1])],
            "region": int(source.get("Region", 0)), "direction": int(source.get("Direction", 0)),
            "unique": bool(source.get("Unique", False)), "area": int(source.get("Area", 0)),
            "state": local.get("LoWorldStageComponent", {}).get("State", 0),
            "active": local.get("LoWorldActiveableEntityComponent", {}).get("Active", True),
            **entity_runtime_rules(identity, scoped), "modelPath": model_path,
            "stageIds": stage_ids_for_entity(source, scoped),
            "stageBindings": stage_behavior_bindings(identity, scoped),
        })
    return result
