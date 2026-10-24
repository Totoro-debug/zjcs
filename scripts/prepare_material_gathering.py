"""Prepare the pinned official material-gathering rules for Unity Resources."""
import argparse
import ast
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = "871_156598"


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tables", type=Path,
                        default=ROOT / ".tools/source-packages/current/871_156598/official-data/tables.json")
    parser.add_argument("--output", type=Path,
                        default=ROOT / "unity/Assets/Game/Resources/Official/Progression/material-gathering.json")
    parser.add_argument("--manifest-output", type=Path)
    args = parser.parse_args()
    source = args.tables.read_bytes()
    tables = json.loads(source)
    dungeons = tables["mop_up_dungeon"]
    materials = tables["mop_up_material"]
    material_rows = materials
    reward_ids = {reward_id for row in material_rows
                  for reward_id in row["AttackRewardIds"] + row["BreakRewardIds"]}
    reward_ids.update(row["GenerateMaterialRuleId"] for row in dungeons)
    rewards = [row for row in tables["reward_rule"] if row["Id"] in reward_ids]
    level_ids = {value for row in rewards for key in ("LevelCountId", "SubRankCountId")
                 for value in (row.get(key) or []) if value > 0}
    levels = [row for row in tables["level_number"] if row["Id"] in level_ids]
    unlock_stage_ids = {stage_id for row in dungeons for stage_id in row["UnlockStages"]}
    stage_locations = []
    for row in tables["stage_map_location"]:
        stage_id = row["StageId"]
        if stage_id not in unlock_stage_ids:
            continue
        params = ast.literal_eval(row["Params"])
        for map_class_id, regions in params["RegionInfos"].items():
            for region in regions:
                stage_locations.append({"StageId": stage_id, "MapClassId": int(map_class_id), "Region": region})
    settings = tables["game_settings"]["MopUp"]
    runtime_dungeons = []
    for row in dungeons:
        costs = row["CostDict"] or {}
        if len(costs) > 1:
            raise ValueError(f"Unexpected multiple tool costs for dungeon {row['Id']}")
        cost = next(iter(costs.items()), ("0", 0))
        runtime_dungeons.append({
            "Id": row["Id"], "Type": row["Type"], "UnlockStages": row["UnlockStages"],
            "GenerateMaterialRuleId": row["GenerateMaterialRuleId"],
            "CostItemId": int(cost[0]), "CostCount": cost[1],
            "AttackProp": row["AttackProp"], "CritRateProp": row["CritRateProp"],
            "CritPowerProp": row["CritPowerProp"], "AddtionAwardProp": row["AddtionAwardProp"],
            "NewbieRewardMaterials": row["NewbieRewardMaterials"],
        })
    payload = {
        "manifest": MANIFEST,
        "damageFloat": settings["DamageFloat"],
        "maximumActionCount": settings["MopUpOnceMaxCount"],
        "dungeons": runtime_dungeons,
        "materials": material_rows,
        "rewards": rewards,
        "levelNumbers": levels,
        "stageLocations": stage_locations,
    }
    encoded = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(encoded)
    manifest_output = args.manifest_output or args.output.with_name("material-gathering-source-manifest.json")
    manifest = {
        "manifest": MANIFEST,
        "source": str(args.tables.resolve().relative_to(ROOT)).replace("\\", "/"),
        "source_sha256": sha256(source),
        "output": str(args.output.resolve().relative_to(ROOT / "unity")).replace("\\", "/"),
        "output_sha256": sha256(encoded),
    }
    manifest_output.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {args.output.relative_to(ROOT)} ({len(material_rows)} materials, "
          f"{len(rewards)} reward rules including target-generation rules, {len(levels)} level rows)")


if __name__ == "__main__":
    main()
