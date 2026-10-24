"""Export the pinned official NPC travel tables to the Unity home payload."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OFFICIAL = ROOT / "assets" / "official"
OUTPUT = ROOT / "unity" / "Assets" / "StreamingAssets" / "Home" / "travel-catalog.json"


def main():
    rules_path = OFFICIAL / "rules.json"
    adventure_path = OFFICIAL / "dungeons.json"
    rules = json.loads(rules_path.read_text(encoding="utf-8"))
    adventure = json.loads(adventure_path.read_text(encoding="utf-8"))
    settings = rules["game_settings"]["NpcAdventureSettings"]
    quality_ids = sorted({int(key) for name in (
        "QualityTravelBaseRewardAddDict", "QualityTravelExtraRewardBaseAddDict",
        "QualityTravelExtraRewardConditionAddDict", "QualityTravelRefreshProbabilityDict",
        "QualityTravelNpcCountDict", "QualityTravelConditionCountDict",
        "QualityTravelDurationDict", "QualityTravelRefreshThresholdDict")
        for key in settings[name]})
    qualities = []
    fields = {
        "base_reward_add": "QualityTravelBaseRewardAddDict",
        "extra_base_add": "QualityTravelExtraRewardBaseAddDict",
        "extra_condition_add": "QualityTravelExtraRewardConditionAddDict",
        "refresh_weight": "QualityTravelRefreshProbabilityDict",
        "npc_count": "QualityTravelNpcCountDict",
        "condition_count": "QualityTravelConditionCountDict",
        "duration": "QualityTravelDurationDict",
        "pity_threshold": "QualityTravelRefreshThresholdDict",
    }
    for quality in quality_ids:
        row = {"quality": quality}
        for name, source in fields.items():
            row[name] = int(settings[source].get(str(quality), 0))
        qualities.append(row)

    def requirements(row, field):
        value = row.get(field) or {}
        return [{"id": int(key), "count": int(count)} for key, count in sorted(value.items(), key=lambda pair: int(pair[0]))]

    travel_rows = adventure["tables"]["npc_adventure_travel"]
    travels = []
    for row in travel_rows:
        travels.append({
            "travel_id": row["TravelId"], "map_group_id": row["MapGroupId"],
            "region_id": row["RegionId"], "position_id": row["PositionId"],
            "node_id": row["PositionNodeId"], "quality": row["Quality"],
            "weight": row["Weight"], "extra_reward_rule_id": row["ExtraRewardRuleId"],
            "professions": requirements(row, "ProfessionDict"),
            "map_groups": requirements(row, "MapGroupDict"),
            "preferences": requirements(row, "PreferenceDict"),
        })
    nodes = [{
        "map_group_id": row["MapGroupId"], "region_id": row["RegionId"],
        "node_id": row["NodeId"], "stage_id": row["StageId"],
        "base_add_count": row["TravelNodeBaseRewardAddCount"],
        "base_add_percent": row["TravelNodeBaseRewardAddPercent"],
        "extra_probability_add": row["TravelNodeExtraRewardAddProbability"],
    } for row in adventure["tables"]["npc_adventure"]]
    reward_rule_ids = sorted({row["ExtraRewardRuleId"] for row in travel_rows})
    reward_rules = []
    for row in rules["reward_rule"]:
        if row["Id"] not in reward_rule_ids:
            continue
        reward_rules.append({
            "id": row["Id"], "order": row["Order"], "percent": row["Percent"],
            "item_ids": row["IdList"] or [], "weights": row["WeightList"] or [],
            "counts": row["CountList"] or [],
        })
    npcs = [{
        "id": row["Id"], "name_key": row.get("NameKey", ""),
        "icon": row.get("Icon", ""),
        "figure_image": row.get("FigureImage", ""),
        "figure_model_ui": row.get("FigureModelUI", ""),
        "figure_model_with_weapon_ui": row.get("FigureModelWithWeaponUI", ""),
        "idle_anim_name": row.get("IdleAnimName", ""),
        "run_anim_name": row.get("RunAnimName", ""),
        "travel_profession": row.get("TravelProfession", 0),
        "map_group_id": row.get("MapGroupId", 0),
        "preference_type": row.get("PreferenceGiftType", 0),
        "can_develop": bool(row.get("CanDevelop", False)),
        "npc_type": row.get("NpcType", 0),
    } for row in rules["npc"]]
    result = {
        "manifest": "854_156383",
        "daily_start_limit": settings["DailyStartTravelMaxCount"],
        "npc_daily_limit": settings["NpcDailyTravelCount"],
        "refresh_cost_item_id": settings["RefreshCostItemId"],
        "refresh_costs": settings["RefreshCostCountList"],
        "base_reward_item_id": settings["BaseRewardItemId"],
        "base_reward_item_count": settings["BaseRewardItemCount"],
        "extra_normal_threshold": settings["ExtraRewardNormalProbabilityThreshold"],
        "extra_super_threshold": settings["ExtraRewardSuperProbabilityThreshold"],
        "extra_rates": [{"type": int(key), "rate": int(value)} for key, value in sorted(settings["ExtraRewardRateDict"].items(), key=lambda pair: int(pair[0]))],
        "travel_npc_prop_types": settings["TravelNpcPropTypeList"],
        "travel_treasure_prop_types": settings["TravelTreasurePropTypeList"],
        "travel_node_prop_types": settings["TravelNodePropTypeList"],
        "positions": settings["TravelPositionIdList"],
        "first_auto_refresh": settings["FirstAutoRefreshTravelIdList"],
        "first_manual_refresh": settings["FirstManualRefreshTravelIdList"],
        "qualities": qualities, "travels": travels, "nodes": nodes, "npcs": npcs,
        "reward_rules": reward_rules,
        "source_sha256": {
            "rules.json": hashlib.sha256(rules_path.read_bytes()).hexdigest(),
            "dungeons.json": hashlib.sha256(adventure_path.read_bytes()).hexdigest(),
        },
    }
    OUTPUT.write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    print(f"Wrote {len(travels)} travel rows, {len(nodes)} nodes, {len(npcs)} NPCs to {OUTPUT}")


if __name__ == "__main__":
    main()
