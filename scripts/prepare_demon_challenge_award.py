"""Extract the selected official first demon challenge award without inventing a reward formula."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build() -> dict:
    version = OfficialSource().version
    tables_path = ROOT / ".tools/source-packages/current" / version / "official-data/tables.json"
    tables = json.loads(tables_path.read_bytes())
    settings = tables["game_settings"]["DemonInvade"]
    rule_id = settings["ChallengeAwardRuleId"]
    rules = [row for row in tables["reward_rule"] if row["Id"] == rule_id]
    if len(rules) != 1:
        raise ValueError("Original demon challenge award rule is not unique")
    rule = rules[0]
    # Only this selected, deterministic rule is supported. Future conditional,
    # weighted, level-scaled or shared-record rules need their original implementation.
    if settings["ChallengeVaildCount"] != 1 or rule["Order"] != -1 or rule["Percent"] != 1:
        raise ValueError("Selected challenge count or reward selection semantics changed")
    if rule["Condition"] is not None or rule["LevelCountId"] is not None or rule["SubRankCountId"] is not None:
        raise ValueError("Original reward has an unresolved condition or scaling rule")
    if rule["IndexMininum"] or rule["RecordSharedId"] is not None or rule["WeightList"] != [100]:
        raise ValueError("Original reward requires unresolved selection or shared-record behavior")
    if len(rule["IdList"]) != 1 or len(rule["CountList"]) != 1:
        raise ValueError("Original reward is no longer the single fixed item")
    count = rule["CountList"][0]
    if count <= 0 or count != int(count) or rule["IdList"][0] <= 0:
        raise ValueError("Original fixed reward amount is invalid")
    payload = {
        "schema": 1,
        "manifest": version,
        "challengeValidCount": settings["ChallengeVaildCount"],
        "awardRuleId": rule_id,
        "items": [{"itemId": rule["IdList"][0], "count": int(count)}],
    }
    target = ROOT / "unity/Assets/Game/Resources/WeeklyChallenges/demon-challenge-award.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    meta = Path(str(target) + ".meta")
    guid = hashlib.md5(b"zjcs:weekly-demon-challenge-award").hexdigest()
    meta.write_text("fileFormatVersion: 2\nguid: " + guid + "\nTextScriptImporter:\n  externalObjects: {}\n",
                    encoding="utf-8", newline="\n")
    generator = Path(__file__).resolve()
    return {
        "manifest": version,
        "inputs": {tables_path.relative_to(ROOT).as_posix(): digest(tables_path)},
        "generator": {"path": generator.relative_to(ROOT).as_posix(), "sha256": digest(generator)},
        "selected_settings": settings,
        "selected_reward_rule": rule,
        "resourcePath": "WeeklyChallenges/demon-challenge-award",
        "outputs": {path.relative_to(ROOT).as_posix(): digest(path) for path in (target, meta)},
        "processing": "Retain original ChallengeVaildCount and ChallengeAwardRuleId; verify fixed single-item rule semantics before converting its exact integral CountList amount to a local inventory count.",
    }


if __name__ == "__main__":
    target = ROOT / "assets/reference/battle-rules/demon-challenge-award-source-manifest.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(build(), ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(target.relative_to(ROOT).as_posix())
