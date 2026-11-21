"""Verify the complete third-promotion skill projection against official payloads.

The battle executor is data driven: skill items select an EC root, the root
selects hit entities, and the shared hit/status pipeline resolves damage,
movement, cures, shields and summons.  This verifier checks that every skill
listed by the four rank-three profession rows is present in the immutable
payloads and that its complete graph can be projected without inventing a
fallback implementation.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any, Iterable


ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / "unity/Assets/Official/Combat/rules.json"
SKILL_RULES = ROOT / "unity/Assets/Official/Combat/skill-rules.json"
BATTLE_RULES = ROOT / "unity/Assets/Official/Combat/battle-rules.json"
DOCS = ROOT / "docs/character"
MANIFEST = ROOT / "assets/reference/battle-rules/third-profession-skill-migration-source-manifest.json"

PROFESSION_FILES = {
    3: "skills-knight.md",
    7: "skills-fighter.md",
    12: "skills-warlock.md",
    16: "skills-sage.md",
}


def read_json(path: Path) -> Any:
    return json.loads(path.read_bytes().decode("utf-8-sig"))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def doc_skill_ids(path: Path) -> list[int]:
    text = path.read_text(encoding="utf-8-sig")
    block = re.search(r"### 三阶职业.*?(?=### 四阶职业|\Z)", text, re.S)
    if not block:
        raise AssertionError(f"missing rank-three section: {path}")
    return [int(value) for value in re.findall(r"\|\s*(\d+)\s*\|", block.group(0))]


def ints(value: Any) -> Iterable[int]:
    if isinstance(value, bool):
        return ()
    if isinstance(value, int):
        return (value,)
    if isinstance(value, list):
        return (item for child in value for item in ints(child))
    if isinstance(value, dict):
        return (item for child in value.values() for item in ints(child))
    return ()


def graph_references(entity_id: int, entities: dict[str, dict[str, Any]]) -> set[int]:
    """Follow entity-shaped references in the serialized official graph.

    The generated EC payload uses ClassId for hit edges and EntityId/SkillId
    for a few polymorphic child edges. Restricting candidates to known entity
    keys prevents status/property numbers from being treated as graph roots.
    """
    entity = entities.get(str(entity_id))
    if not entity:
        return set()
    result: set[int] = set()
    def visit(value: Any, key: str = "") -> None:
        if isinstance(value, dict):
            for name, child in value.items():
                if name in {"ClassId", "EntityId", "SkillId", "ChildSkillId", "TriggerSkillId"}:
                    for candidate in ints(child):
                        if str(candidate) in entities:
                            result.add(candidate)
                visit(child, name)
        elif isinstance(value, list):
            for child in value:
                visit(child, key)
    visit(entity)
    return result


def reachable(root_ids: Iterable[int], entities: dict[str, dict[str, Any]]) -> list[int]:
    seen: set[int] = set()
    pending = [value for value in root_ids if value > 0]
    while pending:
        current = pending.pop()
        if current in seen or str(current) not in entities:
            continue
        seen.add(current)
        pending.extend(graph_references(current, entities) - seen)
    return sorted(seen)


def projection(item_id: int, row: dict[str, Any], entities: dict[str, dict[str, Any]]) -> dict[str, Any]:
    roots = [row.get("EcEntityId", 0)] + list(row.get("ViewPropEntities") or [])
    graph = reachable(roots, entities)
    damage_types: set[int] = set()
    elements: set[int] = set()
    status_ids: set[int] = set(row.get("PassiveStatusIdList") or [])
    movement_ids: set[int] = set()
    summons: set[int] = set()
    cures = 0
    shields = 0
    property_ids = sorted(int(key) for key in (row.get("PassivePropFactors") or {}))

    for entity_id in graph:
        entity = entities[str(entity_id)]
        for component_name, component in entity.items():
            if not isinstance(component, dict):
                continue
            for key in ("DamageType", "HitDamageType"):
                if isinstance(component.get(key), int):
                    damage_types.add(component[key])
            if isinstance(component.get("ElementType"), int):
                elements.add(component["ElementType"])
            if component_name == "FightStatusComponent":
                status_ids.add(entity_id)
            if component_name == "FightActionSkillMoveComponent":
                movement_ids.add(entity_id)
            if component_name.startswith("FightHitSummon"):
                summon = component.get("SummonCfg") or {}
                if isinstance(summon, dict) and isinstance(summon.get("SummonId"), int):
                    summons.add(summon["SummonId"])
            if component_name == "FightCureComponent" or "Cure" in component_name:
                cures += 1
            if component_name == "FightStatusComponent" and component.get("StatusType") == 2:
                shields += 1
    return {
        "item_id": item_id,
        "entity_id": row.get("EcEntityId", 0),
        "roots": sorted(set(value for value in roots if value > 0)),
        "graph_entities": graph,
        "graph_count": len(graph),
        "elements": sorted(elements),
        "damage_types": sorted(damage_types),
        "status_ids": sorted(status_ids),
        "movement_entities": sorted(movement_ids),
        "summon_ids": sorted(summons),
        "cure_components": cures,
        "shield_components": shields,
        "passive_property_ids": property_ids,
        "has_passive_property": bool(row.get("PassiveRankPropId") or property_ids),
    }


def verify(write: bool = False) -> dict[str, Any]:
    rules = read_json(RULES)
    skill_rules = read_json(SKILL_RULES)
    battle_rules = read_json(BATTLE_RULES)
    entities = dict(skill_rules["entities"])
    entities.update(battle_rules["entities"])
    by_item = {int(row["ClassId"]): row for row in rules["skill"]}
    professions = {int(row["Profession"]): row for row in rules["profession_base"]}
    skills: list[dict[str, Any]] = []
    for profession, file_name in PROFESSION_FILES.items():
        ids = doc_skill_ids(DOCS / file_name)
        expected = list(professions[profession]["Skills"])
        if ids != expected:
            raise AssertionError(f"{file_name}: docs and official profession row differ: {ids} != {expected}")
        for item_id in ids:
            row = by_item.get(item_id)
            if row is None:
                raise AssertionError(f"missing official skill item: {item_id}")
            roots = [row.get("EcEntityId", 0)] + list(row.get("ViewPropEntities") or [])
            missing = [root for root in roots if root > 0 and str(root) not in entities]
            if missing:
                raise AssertionError(f"skill {item_id} references missing entity roots: {missing}")
            item = projection(item_id, row, entities)
            if item["graph_count"] == 0 and not item["has_passive_property"]:
                raise AssertionError(f"skill {item_id} has no reachable official graph")
            item["profession"] = profession
            item["rank"] = 3
            skills.append(item)

    result = {
        "manifest": "854_156383",
        "scope": "third-promotion-skills",
        "profession_ids": sorted(PROFESSION_FILES),
        "skill_count": len(skills),
        "source_files": [
            {"path": RULES.relative_to(ROOT).as_posix(), "sha256": sha256(RULES)},
            {"path": SKILL_RULES.relative_to(ROOT).as_posix(), "sha256": sha256(SKILL_RULES)},
            {"path": BATTLE_RULES.relative_to(ROOT).as_posix(), "sha256": sha256(BATTLE_RULES)},
        ],
        "skills": skills,
    }
    if write:
        MANIFEST.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    else:
        expected = read_json(MANIFEST)
        if expected != result:
            raise AssertionError("third-promotion source manifest differs; rerun with --write after source review")
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    result = verify(args.write)
    print(f"Third-promotion skills verified: {result['skill_count']} skills, "
          f"{len(result['source_files'])} official payloads")


if __name__ == "__main__":
    main()
