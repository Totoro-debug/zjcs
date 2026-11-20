"""Verify the complete first/second profession skill migration boundary.

The official tables remain the source of truth.  This check only asserts that
the local adapters preserve every scoped item's value source, execution graph,
status dependency and original presentation binding.  It does not invent a
second skill description or replace an official asset with a placeholder.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Iterable


ROOT = Path(__file__).resolve().parents[1]
PROFESSIONS = (1, 10, 2, 6, 11, 15)
PAGES = {
    1: "warrior",
    10: "mage",
    2: "knight",
    6: "fighter",
    11: "warlock",
    15: "sage",
}
GRAPH_RESOURCE_ROOT = ROOT / "unity/Assets/Official/Combat/SkillGraphRuntime/Resources"
SCOPE_RECEIPT = ROOT / "assets/reference/skill-migration-scope-source-manifest.json"
COMMON_MODULES = (
    "unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.cs",
    "unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs",
    "unity/Assets/Game/Runtime/Official/Combat/FightDamageFormula.cs",
    "unity/Assets/Game/Runtime/Official/Combat/FightBattleHitExecutor.cs",
    "unity/Assets/Game/Runtime/Official/Combat/FightStatusLifecycle.cs",
    "unity/Assets/Game/Runtime/Offline/Combat/FightSkillCastModule.cs",
    "unity/Assets/Game/Runtime/Offline/Combat/FightSkillLogicRuntime.cs",
    "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs",
)


def read(path: str | Path) -> Any:
    return json.loads((ROOT / path).read_text(encoding="utf-8-sig"))


def digest(value: Any) -> str:
    payload = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode()
    return hashlib.sha256(payload).hexdigest()


def file_digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def references(value: Any) -> Iterable[int]:
    """Yield EC child references carried by the official ClassId links."""
    if isinstance(value, dict):
        for key, child in value.items():
            if key == "ClassId" and isinstance(child, int) and child > 0:
                yield child
            yield from references(child)
    elif isinstance(value, list):
        for child in value:
            yield from references(child)


def collect_graph_entities(root_id: int, entities: dict[str, Any]) -> list[int]:
    found: list[int] = []
    pending = [root_id]
    seen: set[int] = set()
    while pending:
        entity_id = pending.pop()
        if entity_id in seen:
            continue
        seen.add(entity_id)
        entity = entities.get(str(entity_id))
        if entity is None:
            raise AssertionError(f"official EC entity {entity_id} is missing")
        found.append(entity_id)
        for child in references(entity):
            if child not in seen and str(child) in entities:
                pending.append(child)
    return sorted(found)


def scoped_rows() -> tuple[list[int], dict[int, list[int]]]:
    rules = read("unity/Assets/Official/Combat/rules.json")
    professions = {row["Profession"]: row for row in rules["profession_base"]}
    rows: dict[int, list[int]] = {}
    for profession in PROFESSIONS:
        row = professions.get(profession)
        if row is None:
            raise AssertionError(f"profession {profession} is missing")
        values = list(row["Skills"]) + list(row["FightUsedSkills"])
        rows[profession] = values
    ids = sorted({item_id for values in rows.values() for item_id in values})
    if len(ids) != 104:
        raise AssertionError(f"expected 104 unique scoped skills, got {len(ids)}")
    return ids, rows


def verify(require_complete: bool = False) -> dict[str, Any]:
    ids, profession_rows = scoped_rows()
    rules = read("unity/Assets/Official/Combat/rules.json")
    combat = read("unity/Assets/Official/Combat/skill-rules.json")
    graphs = read("unity/Assets/Game/Resources/Combat/skill-native-graphs.json")
    playables = read("unity/Assets/Game/Resources/Combat/skill-playables.json")
    views = read("unity/Assets/Game/Resources/Combat/skill-entity-views.json")
    skills = {row["ClassId"]: row for row in rules["skill"]}
    item_types = {row["ClassId"]: row["ItemType"] for row in rules["item"]}
    entities = combat["entities"]
    graph_by_id = {row["skill_entity_id"]: row for row in graphs["skills"]}
    playable_by_id = {row["skill_entity_id"]: row for row in playables["skills"]}
    view_by_id = {row["skillEntityId"]: row for row in views["skills"]}
    docs: dict[int, tuple[str, str]] = {}
    for profession in PROFESSIONS:
        page = ROOT / f"docs/features/skills-{PAGES[profession]}.md"
        for line in page.read_text(encoding="utf-8-sig").splitlines():
            fields = [value.strip() for value in line.strip("|").split("|")]
            if len(fields) == 7 and fields[1].isdigit():
                docs[int(fields[1])] = (fields[5], fields[6])
    records: list[dict[str, Any]] = []
    for item_id in ids:
        row = skills.get(item_id)
        if row is None:
            raise AssertionError(f"skill item {item_id} is missing")
        item_type = item_types.get(item_id)
        if item_type not in (100, 101):
            raise AssertionError(f"skill item {item_id} has unsupported item type {item_type}")
        entity_id = row["EcEntityId"]
        status_ids = list(row.get("PassiveStatusIdList") or [])
        record: dict[str, Any] = {
            "item_id": item_id,
            "item_type": item_type,
            "entity_id": entity_id,
            "status_ids": status_ids,
            "item_sha256": digest(row),
        }
        if item_type == 100:
            if entity_id <= 0:
                raise AssertionError(f"active skill {item_id} has no fight entity")
            entity = entities.get(str(entity_id))
            if entity is None or "FightSkillComponent" not in entity:
                raise AssertionError(f"active skill {item_id}/{entity_id} has no FightSkillComponent")
            component = entity["FightSkillComponent"]
            if not component.get("Range") and component.get("SkillTargetType") != 4:
                raise AssertionError(f"active skill {item_id}/{entity_id} has no target range")
            if not component.get("HitList"):
                raise AssertionError(f"active skill {item_id}/{entity_id} has no hit graph root")
            graph = graph_by_id.get(entity_id)
            playable = playable_by_id.get(entity_id)
            view = view_by_id.get(entity_id)
            if graph is None or not graph.get("entity_resource"):
                raise AssertionError(f"active skill {item_id}/{entity_id} has no native graph")
            if playable is None or not playable.get("playables"):
                raise AssertionError(f"active skill {item_id}/{entity_id} has no native animation binding")
            if view is None:
                raise AssertionError(f"active skill {item_id}/{entity_id} has no entity view binding")
            resource = GRAPH_RESOURCE_ROOT / (graph["entity_resource"] + ".prefab")
            if not resource.is_file():
                raise AssertionError(f"active skill {item_id}/{entity_id} prefab is missing: {resource}")
            playable_resources = []
            for binding in graph.get("playables", []):
                path = binding.get("resource")
                if not path:
                    raise AssertionError(f"active skill {item_id}/{entity_id} has an empty playable resource")
                playable_path = GRAPH_RESOURCE_ROOT / (path + ".prefab")
                if not playable_path.is_file():
                    raise AssertionError(f"active skill {item_id}/{entity_id} playable is missing: {playable_path}")
                playable_resources.append(path)
            record.update({
                "graph_entities": collect_graph_entities(entity_id, entities),
                "graph_resource": graph["entity_resource"],
                "graph_playable_count": len(graph.get("playables", [])),
                "graph_playable_resources": playable_resources,
                "animation_count": len(playable["playables"]),
                "view_bound": True,
                "properties": row.get("PassivePropFactors") or {},
            })
        else:
            if entity_id > 0:
                entity = entities.get(str(entity_id))
                if entity is None or "FightStatusComponent" not in entity:
                    raise AssertionError(f"passive skill {item_id}/{entity_id} has no status definition")
            elif not row.get("PassiveRankPropId"):
                raise AssertionError(f"passive skill {item_id} has neither a status nor a property source")
            for status_id in status_ids:
                status = entities.get(str(status_id))
                if status is None or "FightStatusComponent" not in status:
                    raise AssertionError(f"passive skill {item_id} status {status_id} is missing")
            record.update({
                "status_entities": [status_id for status_id in status_ids],
                "properties": row.get("PassivePropFactors") or {},
                "passive_rank_prop_id": row.get("PassiveRankPropId", 0),
                "passive_group_level_prop_id": row.get("PassiveGroupLevelPropId", 0),
            })
        if require_complete and docs.get(item_id, ("", ""))[0] != "已完成":
            raise AssertionError(f"documentation for skill {item_id} is not marked 已完成")
        records.append(record)
    module_hashes = []
    for module in COMMON_MODULES:
        path = ROOT / module
        if not path.is_file():
            raise AssertionError(f"common skill module is missing: {module}")
        module_hashes.append({"path": module, "sha256": file_digest(path)})
    source_manifests = []
    for manifest in (
        "unity/Assets/Official/Combat/rules-source-manifest.json",
        "unity/Assets/Official/Combat/skill-rules-source-manifest.json",
        "unity/Assets/Official/Combat/battle-rules-source-manifest.json",
        "unity/Assets/Game/Resources/Combat/skill-native-graphs.json",
        "unity/Assets/Game/Resources/Combat/skill-playables.json",
        "unity/Assets/Game/Resources/Combat/skill-entity-views.json",
    ):
        path = ROOT / manifest
        source_manifests.append({"path": manifest, "sha256": file_digest(path)})
    return {
        "scope": "first-second-profession-skills",
        "profession_ids": list(PROFESSIONS),
        "scope_count": len(ids),
        "profession_rows": {str(key): value for key, value in profession_rows.items()},
        "source_manifests": source_manifests,
        "common_modules": module_hashes,
        "skills": records,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true", help="write the machine-verifiable scope receipt")
    parser.add_argument("--require-complete", action="store_true", help="require every scoped documentation row to be 已完成")
    args = parser.parse_args()
    result = verify(require_complete=args.require_complete)
    if args.write:
        SCOPE_RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"First/second profession skill migration verified: {result['scope_count']} skills")


if __name__ == "__main__":
    main()
