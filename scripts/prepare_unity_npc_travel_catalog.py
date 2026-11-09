"""Export the selected official partner travel and NPC adventure tables."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors


VERSION = "884_156692"
CURRENT = ROOT / ".tools" / "source-packages" / "current" / VERSION
CODE_ROOT = CURRENT / "official-code"
OUTPUT = ROOT / "unity" / "Assets" / "StreamingAssets" / "Home" / "travel-catalog.json"
RECEIPT = ROOT / "assets" / "reference" / "partner-travel" / "travel-catalog-source-manifest.json"
TABLES = ("game_settings", "npc_adventure", "npc_adventure_travel", "npc", "reward_rule")


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def compact_json(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"), allow_nan=False)
            + "\n").encode("utf-8")


def build():
    selected = json.loads((ROOT / "official-source.json").read_text(encoding="utf-8"))
    if selected.get("active") != VERSION:
        raise ValueError("NPC travel catalog is pinned to the selected official profile " + VERSION)
    if not CODE_ROOT.is_dir():
        raise FileNotFoundError("Selected official client source is missing: " + str(CODE_ROOT))

    constructors = Constructors(code_root=CODE_ROOT)
    table_types = {match[2]: match[1] for match in re.finditer(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source("Tables"))}
    parsed, inputs, constructor_records = {}, [], {}
    with Apk.current() as apk:
        if apk.version != VERSION:
            raise ValueError("Official APK/resource resolver selected an unexpected manifest: " + apk.version)
        for name in TABLES:
            table_type = table_types.get(name)
            if not table_type:
                raise ValueError("Selected official table constructor is missing: " + name)
            obj = apk.asset("config_luban", "/" + name + ".g.bytes")
            raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
            rows = constructors.table(raw, table_type)
            parsed[name] = rows
            provenance = apk.provenance(obj)
            if provenance.get("manifest") != VERSION:
                raise ValueError("Table resolved from an unexpected official manifest: " + name)
            constructor_path = constructors.source_root / (table_type + ".cs")
            if not constructor_path.is_file():
                raise FileNotFoundError("Selected official table constructor source is missing: " + str(constructor_path))
            constructor_records[table_type] = {
                "path": constructor_path.relative_to(ROOT).as_posix(),
                "sha256": digest(constructor_path.read_bytes()),
            }
            inputs.append({
                "table": name,
                "table_type": table_type,
                "bytes": len(raw),
                "sha256": digest(raw),
                "object": provenance,
            })

    game_settings = parsed["game_settings"]
    settings = game_settings["NpcAdventureSettings"]
    reset_match = re.fullmatch(r"\((\d{2}):(\d{2}):(\d{2})\)", game_settings["DailyResetTime"])
    if not reset_match or int(reset_match[3]) != 0:
        raise ValueError("Selected official daily reset time has an unsupported format")
    quality_ids = sorted({int(key) for field in (
        "QualityTravelBaseRewardAddDict", "QualityTravelExtraRewardBaseAddDict",
        "QualityTravelExtraRewardConditionAddDict", "QualityTravelRefreshProbabilityDict",
        "QualityTravelNpcCountDict", "QualityTravelConditionCountDict", "QualityTravelDurationDict",
        "QualityTravelRefreshThresholdDict") for key in settings[field]})
    quality_fields = {
        "base_reward_add": "QualityTravelBaseRewardAddDict",
        "extra_base_add": "QualityTravelExtraRewardBaseAddDict",
        "extra_condition_add": "QualityTravelExtraRewardConditionAddDict",
        "refresh_weight": "QualityTravelRefreshProbabilityDict",
        "npc_count": "QualityTravelNpcCountDict",
        "condition_count": "QualityTravelConditionCountDict",
        "duration": "QualityTravelDurationDict",
        "pity_threshold": "QualityTravelRefreshThresholdDict",
    }
    qualities = []
    for quality in quality_ids:
        row = {"quality": quality}
        for field, source in quality_fields.items():
            row[field] = int(settings[source].get(quality, settings[source].get(str(quality), 0)))
        qualities.append(row)

    def requirements(row, field):
        value = row.get(field) or {}
        return [{"id": int(key), "count": int(count)}
                for key, count in sorted(value.items(), key=lambda pair: int(pair[0]))]

    travel_rows = parsed["npc_adventure_travel"]
    travels = [{
        "travel_id": row["TravelId"], "map_group_id": row["MapGroupId"],
        "region_id": row["RegionId"], "position_id": row["PositionId"],
        "node_id": row["PositionNodeId"], "quality": row["Quality"],
        "weight": row["Weight"], "extra_reward_rule_id": row["ExtraRewardRuleId"],
        "professions": requirements(row, "ProfessionDict"),
        "map_groups": requirements(row, "MapGroupDict"),
        "preferences": requirements(row, "PreferenceDict"),
    } for row in travel_rows]
    adventure_rows = parsed["npc_adventure"]
    nodes = [{
        "map_group_id": row["MapGroupId"], "region_id": row["RegionId"],
        "node_id": row["NodeId"], "stage_id": row["StageId"],
        "base_add_count": row["TravelNodeBaseRewardAddCount"],
        "base_add_percent": row["TravelNodeBaseRewardAddPercent"],
        "extra_probability_add": row["TravelNodeExtraRewardAddProbability"],
    } for row in adventure_rows]
    reward_rule_ids = sorted({row["ExtraRewardRuleId"] for row in travel_rows})
    reward_rules = [{
        "id": row["Id"], "order": row["Order"], "percent": row["Percent"],
        "item_ids": row["IdList"] or [], "weights": row["WeightList"] or [],
        "counts": row["CountList"] or [],
    } for row in parsed["reward_rule"] if row["Id"] in reward_rule_ids]
    npcs = [{
        "id": row["Id"], "name_key": row.get("NameKey", ""),
        "icon": row.get("Icon", ""), "figure_image": row.get("FigureImage", ""),
        "figure_model_ui": row.get("FigureModelUI", ""),
        "figure_model_with_weapon_ui": row.get("FigureModelWithWeaponUI", ""),
        "idle_anim_name": row.get("IdleAnimName", ""), "run_anim_name": row.get("RunAnimName", ""),
        "travel_profession": row.get("TravelProfession", 0),
        "map_group_id": row.get("MapGroupId", 0),
        "preference_type": row.get("PreferenceGiftType", 0),
        "can_develop": bool(row.get("CanDevelop", False)), "npc_type": row.get("NpcType", 0),
    } for row in parsed["npc"]]

    result = {
        "manifest": VERSION,
        "daily_battle_limit": settings["DailyBattleMaxCount"],
        "daily_reset_hour": int(reset_match[1]),
        "daily_reset_minute": int(reset_match[2]),
        "daily_start_limit": settings["DailyStartTravelMaxCount"],
        "npc_daily_limit": settings["NpcDailyTravelCount"],
        "refresh_cost_item_id": settings["RefreshCostItemId"],
        "refresh_costs": settings["RefreshCostCountList"],
        "base_reward_item_id": settings["BaseRewardItemId"],
        "base_reward_item_count": settings["BaseRewardItemCount"],
        "extra_normal_threshold": settings["ExtraRewardNormalProbabilityThreshold"],
        "extra_super_threshold": settings["ExtraRewardSuperProbabilityThreshold"],
        "extra_rates": [{"type": int(key), "rate": int(value)}
                         for key, value in sorted(settings["ExtraRewardRateDict"].items(),
                                                  key=lambda pair: int(pair[0]))],
        "travel_npc_prop_types": settings["TravelNpcPropTypeList"],
        "travel_treasure_prop_types": settings["TravelTreasurePropTypeList"],
        "travel_node_prop_types": settings["TravelNodePropTypeList"],
        "positions": settings["TravelPositionIdList"],
        "first_auto_refresh": settings["FirstAutoRefreshTravelIdList"],
        "first_manual_refresh": settings["FirstManualRefreshTravelIdList"],
        "qualities": qualities, "travels": travels, "nodes": nodes, "npcs": npcs,
        "reward_rules": reward_rules,
        "source_sha256": {row["table"]: row["sha256"] for row in inputs},
    }
    expected_qualities = {2: (7000, 0), 3: (2600, 5), 4: (350, 30), 5: (50, 100)}
    actual_qualities = {row["quality"]: (row["refresh_weight"], row["pity_threshold"]) for row in qualities}
    if len(travels) != 1728 or len(nodes) != 880 or len(npcs) != 148 or len(reward_rules) != 4:
        raise ValueError("Selected official partner travel/adventure table counts changed")
    if actual_qualities != expected_qualities or sum(row["refresh_weight"] for row in qualities) != 10000:
        raise ValueError("Selected official travel quality probabilities or pity thresholds changed")
    if (result["daily_battle_limit"] != 10 or result["daily_reset_hour"] != 8
            or result["daily_reset_minute"] != 0 or result["daily_start_limit"] != 4
            or result["npc_daily_limit"] != 1 or result["positions"] != [1, 2, 3, 4]
            or result["refresh_cost_item_id"] != 2 or result["refresh_costs"] != [100] * 6):
        raise ValueError("Selected official partner adventure/travel settings changed")

    output = compact_json(result)
    source_manifest = json.loads((CURRENT / "client-decompilation-manifest.json").read_text(encoding="utf-8"))
    code_manifest = json.loads((CURRENT / "code-manifest.json").read_text(encoding="utf-8"))
    receipt = {
        "format": 1,
        "manifest": VERSION,
        "apk": {
            "path": ".tools/source-packages/zjcs_M509085.apk",
            "sha256": source_manifest.get("apk_sha256"),
            "apk_manifest": source_manifest.get("apk_manifest"),
        },
        "client_decompilation_manifest_sha256": digest((CURRENT / "client-decompilation-manifest.json").read_bytes()),
        "code_manifest_sha256": digest((CURRENT / "code-manifest.json").read_bytes()),
        "constructor_bundle": "defaultpackage_assets_config_luban.bundle",
        "inputs": inputs,
        "constructors": [dict(table_type=name, **value)
                         for name, value in sorted(constructor_records.items())],
        "tools": [
            {"path": "scripts/prepare_unity_npc_travel_catalog.py",
             "sha256": digest(Path(__file__).read_bytes())},
            {"path": "scripts/official_tables.py",
             "sha256": digest((ROOT / "scripts" / "official_tables.py").read_bytes())},
        ],
        "operation": "Resolve each table through the APK-first selected OfficialSource, verify the original "
                     "config_luban TextAsset identity and manifest, decode the complete bytes with the selected "
                     "884_156692 Luban constructors, then project only the official travel, node, NPC and reward "
                     "fields consumed by the Unity runtime.",
        "counts": {"travel_rows": len(travels), "adventure_nodes": len(nodes),
                   "npcs": len(npcs), "reward_rules": len(reward_rules)},
        "output": {"path": "unity/Assets/StreamingAssets/Home/travel-catalog.json",
                   "bytes": len(output), "sha256": digest(output)},
    }
    return output, compact_json(receipt), result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--write", action="store_true", help="Write the verified selected-version payload and receipt")
    args = parser.parse_args()
    output, receipt, result = build()
    if args.write:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        RECEIPT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_bytes(output)
        RECEIPT.write_bytes(receipt)
    elif not OUTPUT.is_file() or OUTPUT.read_bytes() != output:
        raise ValueError("NPC travel runtime payload differs; run with --write after reviewing the source manifest")
    elif not RECEIPT.is_file() or RECEIPT.read_bytes() != receipt:
        raise ValueError("NPC travel source receipt differs; run with --write after reviewing the source manifest")
    print("Verified selected official tables:", result["manifest"], len(result["travels"]), "travels,",
          len(result["nodes"]), "adventure nodes,", len(result["npcs"]), "NPCs")


if __name__ == "__main__":
    main()
