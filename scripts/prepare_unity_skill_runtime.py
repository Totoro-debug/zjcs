"""Build verified offline skill progression rules for the Unity runtime."""
import argparse
import hashlib
import json
import re
import os
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
OUTPUT = Path("Assets/Official/Skills/skill-runtime-rules.json")
MODULE_MANIFEST = Path("unity/Assets/Official/Skills/source-manifest.json")
SOURCES = {
    "rules": ("assets/official/rules.json", "assets/reference/official-city/source-manifest.json", "data_sha256.rules.json"),
    "cultivation": ("assets/official/skill-cultivation.json", "assets/reference/skill-cultivation/source-manifest.json", "outputs.skill-cultivation.json"),
    "fidelity": ("assets/official/fidelity-rules.json", "assets/reference/equipment-completion/source-manifest.json", "output_sha256.fidelity-rules.json"),
}


def sha256_bytes(value):
    return hashlib.sha256(value).hexdigest()


def sha256_file(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def manifest_value(manifest, field):
    section, _, key = field.partition(".")
    value = manifest.get(section)
    value = value.get(key) if isinstance(value, dict) else None
    if not isinstance(value, str) or len(value) != 64:
        raise ValueError("Invalid official source hash field: " + field)
    return value


def load_sources(root):
    loaded = {}
    source_records = {}
    for name, (payload_name, manifest_name, hash_field) in SOURCES.items():
        payload = root / payload_name
        manifest_path = root / manifest_name
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        if manifest.get("manifest") != VERSION:
            raise ValueError("Skill source manifest is not pinned to " + VERSION + ": " + manifest_name)
        expected = manifest_value(manifest, hash_field)
        actual = sha256_file(payload)
        if actual != expected:
            raise ValueError("Skill source payload hash differs from its manifest: " + payload_name)
        loaded[name] = json.loads(payload.read_text(encoding="utf-8"))
        source_records[name] = {
            "payload": payload_name,
            "sha256": actual,
            "manifest": manifest_name,
            "manifest_sha256": sha256_file(manifest_path),
            "field": hash_field,
        }
    return loaded, source_records


def item_costs(costs):
    return [{"ItemId": int(item_id), "Count": int(count)}
            for item_id, count in sorted((costs or {}).items(), key=lambda pair: int(pair[0]))]


def rank_of_subrank(subrank):
    return subrank if subrank < 2 else 2 + (subrank - 2) // 3


def build(data):
    rules = data["rules"]
    item_by_id = {item["ClassId"]: item for item in rules["item"]}
    skill_by_id = {skill["ClassId"]: skill for skill in rules["skill"]}
    profession_rows = rules["profession_base"]
    profession_by_id = {row["Profession"]: row for row in profession_rows}
    direct = {}
    unlock_profession = {}
    for row in profession_rows:
        skills = row.get("Skills") or []
        for skill_id in skills:
            direct.setdefault(skill_id, set()).add(row["Profession"])
            unlock_profession[skill_id] = row["Profession"]
        for skill_id in (row.get("FightUsedSkills") or []) if skills else []:
            direct.setdefault(skill_id, set()).add(row["Profession"])

    rank_rows = {row["Rank"]: row for row in rules["skill_rank"]}
    maximum_rank = max(rank_rows)
    money_id = int(rules["game_settings"]["MoneyId"])
    definitions = []
    for item in rules["item"]:
        if item["ItemType"] not in (100, 101) or item["ClassId"] not in skill_by_id:
            continue
        skill_id = item["ClassId"]
        owners = direct.get(skill_id, set())
        available = set()
        for profession_id, profession in profession_by_id.items():
            ancestor = profession_id
            seen = set()
            while ancestor and ancestor not in seen:
                seen.add(ancestor)
                if ancestor in owners:
                    available.add(profession_id)
                    break
                parent = profession_by_id.get(ancestor)
                ancestor = parent.get("PrePro", 0) if parent else 0
        unlock_owner = profession_by_id.get(unlock_profession.get(skill_id), {})
        starting = unlock_owner.get("Rank") == 1
        max_rank = skill_by_id[skill_id].get("MaxRankLimit", 0) or maximum_rank
        costs = []
        for current_rank in range(max(0, int(item["Rank"])), min(maximum_rank, max_rank)):
            current = rank_rows.get(current_rank)
            following = rank_rows.get(current_rank + 1)
            if current is None or following is None:
                continue
            piece_count = following["Piece"] - current["Piece"]
            money_count = following["CostMoney"] - current["CostMoney"]
            entries = []
            if piece_count:
                entries.append({"ItemId": skill_id + 2000, "Count": piece_count})
            if money_count:
                entries.append({"ItemId": money_id, "Count": money_count})
            costs.append({"CurrentRank": current_rank, "Items": entries})
        definitions.append({
            "ClassId": skill_id,
            "ItemType": item["ItemType"],
            "AvailableProfessions": sorted(available),
            "AvailableWithoutProfession": starting,
            "MaximumRank": max_rank,
            "PieceItemId": skill_id + 2000,
            "MoneyItemId": money_id,
            "RankCosts": costs,
        })

    limits = list(data["fidelity"]["system_level_limit"])
    maximum_by_rank = {}
    for row in limits:
        rank = rank_of_subrank(row["SubRank"])
        maximum_by_rank[rank] = max(maximum_by_rank.get(rank, 0), row["SkillLevel"])
    cultivation = data["cultivation"]["tables"]
    return {
        "PlanCount": rules["game_settings"]["SkillPlanMaxCount"],
        "Skills": definitions,
        "GiftShowCount": rules["game_settings"]["AchievementPanelShowCount"],
        "GiftTemplates": [{"RawId": row["Id"], "Group": row["Group"],
                           "RequiredCount": int(re.search(r"Count\s*:\s*(\d+)", row["CompleteParams"]).group(1)) if row["CompleteParams"] else 0,
                           "Increment": int(re.search(r"Count\s*:\s*(\d+)", row["CycleIncrementParams"]).group(1)) if row["CycleIncrementParams"] else 0,
                           "CycleCount": row["CycleCount"], "Rewards": item_costs(row["Reward"])}
                          for row in rules["achievement"] if row["Group"] in (9, 33)],
        "RankDisplays": [{key: row[key] for key in ("Rank", "Quality", "RankAddition", "Score")}
                         for row in rules["skill_rank"]],
        "SlotLevelCosts": [{"Level": row["Level"], "Items": item_costs(row["LevelCostItems"])}
                           for row in cultivation["skill_slot_upgrade_material"]],
        "SlotBlessCosts": [{"Rank": row["Rank"], "BlessLevel": row["BlessLevel"],
                            "Items": item_costs(row["LevelCostItems"])}
                           for row in cultivation["skill_slot_upgrade_bless"]],
        "LevelLimits": [{key: row[key] for key in
                         ("SubRank", "PlayerLevel", "PlayerBlessLevel", "SkillLevel", "SkillBlessLevel")}
                        for row in limits],
        "BaseLevelLimits": [{"SubRank": rank, "SkillLevel": level}
                            for rank, level in sorted(maximum_by_rank.items())],
    }


def atomic_write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(prefix=".skill-runtime-", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "wb") as stream:
            stream.write(content)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def restore(root=ROOT):
    root = Path(root).resolve()
    source_data, source_records = load_sources(root)
    runtime = build(source_data)
    content = (json.dumps(runtime, ensure_ascii=False, sort_keys=True,
                          separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8")
    target = root / "unity" / OUTPUT
    manifest_path = root / MODULE_MANIFEST
    module_manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    previous = {item["path"]: item["sha256"] for item in module_manifest.get("files", [])}
    if target.exists() and sha256_file(target) != previous.get(OUTPUT.as_posix()):
        raise ValueError("Refusing to overwrite modified skill runtime payload: " + str(target))
    atomic_write(target, content)
    output_hash = sha256_bytes(content)
    files = [item for item in module_manifest.get("files", []) if item.get("path") != OUTPUT.as_posix()]
    files.append({"path": OUTPUT.as_posix(), "bytes": len(content), "sha256": output_hash})
    module_manifest["files"] = sorted(files, key=lambda item: item["path"])
    module_manifest["runtime_rules_sources"] = source_records
    atomic_write(manifest_path, (json.dumps(module_manifest, ensure_ascii=False, sort_keys=True,
                                            indent=2) + "\n").encode("utf-8"))
    return {"output": OUTPUT.as_posix(), "sha256": output_hash,
            "skills": len(runtime["Skills"]), "sources": source_records}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT,
                        help="Workspace containing official source payloads and unity/")
    args = parser.parse_args()
    print(json.dumps(restore(args.root), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
