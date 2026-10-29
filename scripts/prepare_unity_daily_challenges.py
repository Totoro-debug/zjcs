"""Decode the current official daily challenge configuration with full byte consumption."""
from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

from official_apk import Apk, ROOT
from official_tables import Constructors, SOURCE


OUT = ROOT / "assets/official/daily-challenges/daily-challenges.json"
UNITY_OUT = ROOT / "unity/Assets/Game/Resources/DailyChallenges/daily-challenges.json"
RECEIPT = ROOT / "assets/reference/daily-challenges/source-manifest.json"
TABLE_NAMES = {
    "ActivityDomainBoss/activity_domain_boss": "activity_domain_boss",
    "ActivityDomainBoss/activity_domain_boss_difficulty": "activity_domain_boss_difficulty",
    "ActivityDomainBoss/activity_domain_boss_prop_correction": "activity_domain_boss_prop_correction",
    "ActivityDomainFunnyBoss/activity_domain_funny_boss": "activity_domain_funny_boss",
    "activity": "activity",
    "chest": "chest",
    "step_award": "step_award",
    "reward_rule": "reward_rule",
}
ACTIVITY_IDS = {209, 212, 217, 226}
ACTIVITY_TYPES = {33, 46}
CHEST_IDS = {6525}
STEP_AWARD_GROUPS = {1010}
REWARD_RULE_IDS: set[int] = set()


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def bundle_name(table_name: str) -> str:
    folder = table_name.rsplit("/", 1)[0] if "/" in table_name else ""
    return "config_luban_" + folder.replace("/", "_").lower() if folder else "config_luban"


def should_keep(name: str, row: dict) -> bool:
    if name == "activity":
        return row.get("ActivityType") in ACTIVITY_TYPES and row.get("Id") in ACTIVITY_IDS
    if name == "chest":
        return row.get("ClassId") in CHEST_IDS
    if name == "step_award":
        return row.get("Index") in STEP_AWARD_GROUPS
    if name == "reward_rule":
        return row.get("Id") in REWARD_RULE_IDS
    return True


def main() -> None:
    apk = Apk.current()
    constructors = Constructors()
    mapping = {
        match[2]: match[1]
        for match in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source("Tables"))
    }
    source_records = {}
    tables = {}
    for source_name, output_name in TABLE_NAMES.items():
        constructor = mapping.get(source_name)
        if constructor is None:
            raise ValueError("Official table is not registered in Tables.cs: " + source_name)
        obj = apk.asset(bundle_name(source_name), "/" + source_name.rsplit("/", 1)[-1] + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        rows = constructors.table(raw, constructor)
        if output_name == "activity_domain_boss_difficulty":
            REWARD_RULE_IDS.update(rule_id for row in rows for rule_id in row.get("AwardRuleIds", []))
        elif output_name == "chest":
            REWARD_RULE_IDS.update(rule_id for row in rows if should_keep(output_name, row)
                                   for rule_id in row.get("RewardRules", []))
        elif output_name == "step_award":
            REWARD_RULE_IDS.update(row["RewardId"] for row in rows if should_keep(output_name, row))
        selected = []
        for source_row in rows:
            if not should_keep(output_name, source_row):
                continue
            row = dict(source_row)
            if output_name == "activity_domain_boss":
                row["DailyChallengeAwards"] = [
                    {"Id": int(item_id), "Value": value}
                    for item_id, value in (row.get("DailyChallengeAwardDict") or {}).items()
                ]
            elif output_name == "activity_domain_boss_difficulty":
                row["DifficultyPropEntries"] = [
                    {"Id": int(prop_id), "Value": value}
                    for prop_id, value in (row.get("DifficultyProps") or {}).items()
                ]
            elif output_name == "activity_domain_boss_prop_correction":
                row["CorrectionPropEntries"] = [
                    {"Id": int(prop_id), "Value": value}
                    for prop_id, value in (row.get("CorrectionProps") or {}).items()
                ]
            selected.append(row)
        if not selected:
            raise ValueError("Official table selection is empty: " + source_name)
        tables[output_name] = selected
        source_records[output_name] = {
            **apk.provenance(obj),
            "constructor": constructor,
            "constructor_sha256": sha((SOURCE / (constructor + ".cs")).read_bytes()),
            "source_bytes": len(raw),
            "source_sha256": sha(raw),
            "source_rows": len(rows),
            "selected_rows": len(selected),
        }
        print(source_name, "rows", len(selected), "of", len(rows), flush=True)

    payload = {"manifest": apk.version, "activityTypes": {"domainBoss": 33, "domainFunnyBoss": 46}, **tables}
    encoded = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_bytes(encoded)
    UNITY_OUT.parent.mkdir(parents=True, exist_ok=True)
    UNITY_OUT.write_bytes(encoded)
    previous_receipt = json.loads(RECEIPT.read_text(encoding="utf-8")) if RECEIPT.exists() else {}
    receipt = {
        "manifest": apk.version,
        "tables": source_records,
        "decoder": {"script_sha256": sha(Path(__file__).read_bytes())},
        "outputs": {
            OUT.relative_to(ROOT).as_posix(): {"bytes": len(encoded), "sha256": sha(encoded)},
            UNITY_OUT.relative_to(ROOT).as_posix(): {"bytes": len(encoded), "sha256": sha(encoded)},
        },
    }
    if previous_receipt.get("unity_assets"):
        receipt["unity_assets"] = previous_receipt["unity_assets"]
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("wrote", OUT.relative_to(ROOT), "bytes", len(encoded), flush=True)


if __name__ == "__main__":
    main()
