"""Verify the four fourth-promotion profession skill projections.

Fourth-promotion skills use the same official graph pipeline as the lower
promotions.  This verifier keeps the scope explicit, checks that the four
profession rows and the detail pages agree, then follows every EC reference
from each skill item. This is a static configuration projection, not evidence
that the runtime implements every effect or that a skill is complete.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any

from verify_third_profession_skills import projection


ROOT = Path(__file__).resolve().parents[1]
RULES = ROOT / "unity/Assets/Official/Combat/rules.json"
SKILL_RULES = ROOT / "unity/Assets/Official/Combat/skill-rules.json"
BATTLE_RULES = ROOT / "unity/Assets/Official/Combat/battle-rules.json"
ANIMATION_MANIFEST = ROOT / "assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json"
DOCS = ROOT / "docs/character"
MANIFEST = ROOT / "assets/reference/battle-rules/fourth-profession-skill-migration-source-manifest.json"

PROFESSION_FILES = {
    4: "skills-knight.md",
    8: "skills-fighter.md",
    13: "skills-warlock.md",
    17: "skills-sage.md",
}

# These two rows are official combat-only variants shown on the detail pages;
# they are intentionally absent from profession_base.Skills.
BUILT_IN = {10184: 4, 10185: 8}


def read_json(path: Path) -> Any:
    return json.loads(path.read_bytes().decode("utf-8-sig"))


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def rank_four_ids(path: Path) -> list[int]:
    text = path.read_text(encoding="utf-8-sig")
    block = re.search(r"### 四阶职业.*?(?=### 五阶职业|\Z)", text, re.S)
    if not block:
        raise AssertionError(f"missing rank-four section: {path}")
    return [int(value) for value in re.findall(r"\|\s*(\d+)\s*\|", block.group(0))]


def rank_four_statuses(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8-sig")
    block = re.search(r"### 四阶职业.*?(?=### 五阶职业|\Z)", text, re.S)
    if not block:
        raise AssertionError(f"missing rank-four section: {path}")
    return re.findall(r"\|\s*(?:\d+)\s*\|[^\n]*?\|\s*(已完成|未完成)\s*\|", block.group(0))


def verify(write: bool = False) -> dict[str, Any]:
    rules = read_json(RULES)
    skill_rules = read_json(SKILL_RULES)
    battle_rules = read_json(BATTLE_RULES)
    animation_manifest = read_json(ANIMATION_MANIFEST)
    animation_bindings = {
        int(row["skill_id"]): row
        for row in animation_manifest["official_entity_binding_source"]["skills"]
    }
    entities = dict(skill_rules["entities"])
    entities.update(battle_rules["entities"])
    by_item = {int(row["ClassId"]): row for row in rules["skill"]}
    professions = {int(row["Profession"]): row for row in rules["profession_base"]}
    skills: list[dict[str, Any]] = []

    for profession, file_name in PROFESSION_FILES.items():
        path = DOCS / file_name
        ids = rank_four_ids(path)
        statuses = rank_four_statuses(path)
        if len(ids) != len(statuses):
            raise AssertionError(f"{file_name}: malformed rank-four table")
        expected = list(professions[profession]["Skills"])
        official = expected + [item for item, owner in BUILT_IN.items() if owner == profession]
        if ids != official:
            raise AssertionError(f"{file_name}: docs and official rows differ: {ids} != {official}")
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
            item["rank"] = 4
            if item["entity_id"] > 0 and item_id < 10500:
                binding = animation_bindings.get(item["entity_id"])
                if not binding or not binding.get("entity_prefab") or not binding.get("playables"):
                    raise AssertionError(f"skill {item_id} has no official animation binding: {item['entity_id']}")
            skills.append(item)

    result = {
        "manifest": "854_156383",
        "scope": "fourth-promotion-skills",
        "profession_ids": sorted(PROFESSION_FILES),
        "built_in_skill_ids": sorted(BUILT_IN),
        "skill_count": len(skills),
        "source_files": [
            {"path": RULES.relative_to(ROOT).as_posix(), "sha256": sha256(RULES)},
            {"path": SKILL_RULES.relative_to(ROOT).as_posix(), "sha256": sha256(SKILL_RULES)},
            {"path": BATTLE_RULES.relative_to(ROOT).as_posix(), "sha256": sha256(BATTLE_RULES)},
            {"path": ANIMATION_MANIFEST.relative_to(ROOT).as_posix(), "sha256": sha256(ANIMATION_MANIFEST)},
        ],
        "skills": skills,
    }
    if write:
        MANIFEST.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    else:
        expected = read_json(MANIFEST)
        if expected != result:
            raise AssertionError("fourth-promotion source manifest differs; rerun with --write after source review")
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    result = verify(args.write)
    print(f"Fourth-promotion configuration checked: {result['skill_count']} skills, "
          f"{len(result['source_files'])} official payloads")


if __name__ == "__main__":
    main()
