"""Extract original map monster reward inputs from the selected official version.

This catalog is source data only; it does not implement a server settlement.
"""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_map13_story_runtime import json_meta, source_record, write_json


OUTPUT = ROOT / "unity/Assets/Game/Resources/World/Mainline/monster-reward-inputs.json"
RECEIPT = ROOT / "assets/reference/official-map/mainline-monster-rewards-source-manifest.json"


def build():
    apk = Apk()
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    constructors = Constructors(code)
    inputs = {}
    tables = {}
    for name, kind in (("stage", "TbStageInfo"), ("monster_reward", "TbMonsterRewardInfo"),
                       ("reward_rule", "TbRewardRuleInfo"), ("level_number", "TbLevelNumber"),
                       ("conditions", "TbConditionInfo")):
        obj = apk.asset("config_luban", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        inputs[name] = dict(apk.provenance(obj), sha256=hashlib.sha256(raw).hexdigest())
        tables[name] = constructors.table(raw, kind)

    stages = [{key: row[key] for key in ("Id", "Awards", "MonsterAwardGroupId",
               "AwardUsePlayerLevelAndSubRank", "WaveMonsterIds")}
              for row in tables["stage"]]
    reward_ids = {rule for stage in stages for rule in (stage["Awards"] or [])}
    reward_ids.update(rule for row in tables["monster_reward"] for rule in row["RuleIds"])
    rules = [dict(row, HasCondition=row["Condition"] is not None)
             for row in tables["reward_rule"] if row["Id"] in reward_ids]
    conditions_by_id = {row["ClassId"]: row for row in tables["conditions"]}
    condition_ids = set()
    pending = [condition for row in rules if (condition := row.get("Condition"))]
    while pending:
        condition = pending.pop()
        for identity in condition.get("Ids", []):
            if identity in condition_ids:
                continue
            if identity not in conditions_by_id:
                raise ValueError("Original reward condition is missing: " + str(identity))
            condition_ids.add(identity)
            pending.append(conditions_by_id[identity]["Condition"])
    conditions = [conditions_by_id[identity] for identity in sorted(condition_ids)]
    level_ids = set()
    for row in rules:
        for field in ("LevelCountId", "SubRankCountId"):
            values = row.get(field) or []
            if isinstance(values, int):
                values = [values]
            level_ids.update(value for value in values if value > 0)
    levels = [row for row in tables["level_number"] if row["Id"] in level_ids]
    if len(levels) != len(level_ids):
        raise ValueError("Original reward level multipliers are incomplete")
    missing = sorted(reward_ids - {row["Id"] for row in rules})
    payload = dict(manifest=apk.version, stages=stages, monsterRewards=tables["monster_reward"],
                   rewardRules=rules, levelNumbers=levels, conditions=conditions, missingRewardRuleIds=missing)
    raw = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf8")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    if not OUTPUT.exists() or OUTPUT.read_bytes() != raw:
        OUTPUT.write_bytes(raw)
    json_meta(OUTPUT)
    code_paths = ("client-source/MonsterInfoDialog.cs", "client-source/AutoInteractionMonsterDropItemSupporter.cs",
                  "common-source/Common/MapEntityHelper.cs", "common-source/Common/RewardRuleInfoParser.cs")
    receipt = dict(format=1, manifest=apk.version,
        operation="Extract stage awards, monster reward group bindings, reward rules and level multipliers; no settlement implementation is implied.",
        officialSource=source_record(ROOT / "official-source.json"), inputs=inputs,
        officialCode=[source_record(code / path) for path in code_paths],
        constructors=[source_record(path) for name in sorted(constructors.sources)
                      for path in (code / "common-source/Common" / (name + ".cs"),
                                   code / "common-source/Common.LubanGen" / (name + ".cs"))
                      if path.is_file()],
        tools=[source_record(Path(__file__))], outputs=[source_record(OUTPUT), source_record(Path(str(OUTPUT) + ".meta"))],
        counts=dict(stages=len(stages), monsterRewards=len(tables["monster_reward"]),
                    rewardRules=len(rules), levelNumbers=len(levels), conditions=len(conditions)), missingRewardRuleIds=missing)
    write_json(RECEIPT, receipt)
    print(json.dumps(receipt["counts"]))


if __name__ == "__main__":
    build()
