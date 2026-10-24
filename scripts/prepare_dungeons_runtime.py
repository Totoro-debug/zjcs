"""Build the compact local runtime catalog for official daily dungeons."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE_MANIFEST = "854_156383"
INPUTS = (
    ROOT / "assets/official/dungeons.json",
    ROOT / "assets/official/rules.json",
    ROOT / "assets/official/battle-stages.json",
)
OUTPUT = ROOT / "unity/Assets/Game/Resources/Dungeons/dungeons-runtime.json"
RECEIPT = ROOT / "assets/reference/dungeons-runtime-source-manifest.json"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def int_map_values(value: dict | None) -> dict:
    return {int(key): int(item) for key, item in (value or {}).items()}


def condition_record(class_id: int, value: dict) -> dict:
    return {
        "id": class_id, "type": value["_type"],
        "andIds": [int(item) for item in value.get("Ids", [])],
        "orIds": [int(item) for item in value.get("Ids", [])]
        if value["_type"] == "CompositeOrCondition" else [],
        "combatRating": int(value.get("CombatRating", 0)),
        "rank": int(value.get("Rank", 0)),
        "level": int(value.get("Level", 0)),
        "dungeonId": int(value.get("DungeonId", 0)),
        "difficulty": int(value.get("Difficulty", 0)),
        "gradeType": int(value.get("GradeType", 0)),
        "days": int(value.get("Days", 0)),
        "onlyRank": bool(value.get("IsOnlyRank", False)),
        "profession": int(value.get("Profession", 0)),
        "itemId": int(value.get("ClassId", 0)),
        "count": int(value.get("Count", 0)),
        "op": value.get("Op", ""),
    }


def build() -> dict:
    dungeons = json.loads(INPUTS[0].read_text(encoding="utf-8"))
    rules = json.loads(INPUTS[1].read_text(encoding="utf-8"))
    battle_stages = json.loads(INPUTS[2].read_text(encoding="utf-8"))
    battle_rows = dungeons["tables"]["daily_dungeon_battle"]
    condition_ids = set()
    for row in battle_rows:
        for field in ("UnlockConditionIdDict", "EntryConditionIdDict", "TabItemConditionIdDict"):
            condition_ids.update(value for value in int_map_values(row.get(field)).values() if value > 0)

    condition_rows = {row["ClassId"]: row["Condition"] for row in rules["conditions"]}
    pending_conditions = list(condition_ids)
    while pending_conditions:
        condition_id = pending_conditions.pop()
        value = condition_rows.get(condition_id)
        if value is None:
            continue
        for child_id in value.get("Ids", []):
            child_id = int(child_id)
            if child_id > 0 and child_id not in condition_ids:
                condition_ids.add(child_id)
                pending_conditions.append(child_id)
    missing = condition_ids.difference(condition_rows)
    if missing:
        raise ValueError("Daily dungeon conditions are missing from official rules: " + repr(sorted(missing)))

    battle_output = []
    for row in battle_rows:
        unlock = int_map_values(row.get("UnlockConditionIdDict"))
        entry = int_map_values(row.get("EntryConditionIdDict"))
        tab = int_map_values(row.get("TabItemConditionIdDict"))
        map_ids = int_map_values(row.get("DifficultyMapIdDict"))
        difficulties = []
        for difficulty in row["DifficultyList"]:
            difficulties.append({
                "difficulty": difficulty,
                "unlockConditionId": unlock.get(difficulty, 0),
                "entryConditionId": entry.get(difficulty, 0),
                "tabConditionId": tab.get(difficulty, 0),
                "mapGroupId": map_ids.get(difficulty, 0),
                "retrieveGrade": int_map_values(row.get("DifficultyRetrieveGradeDict")).get(difficulty, 0),
            })
        battle_output.append({
            "id": row["DungeonBattleId"],
            "title": dungeons.get("texts", {}).get(
                "ui_daily_dungeon_item_title_without_difficulty_" + str(row["DungeonBattleId"]),
                "副本 " + str(row["DungeonBattleId"])),
            "entryMapClassId": row["EntryMapClassId"],
            "entryEntityId": int(dungeons.get("locations", {}).get(str(row["DungeonBattleId"]), {}).get("entity", 0)),
            "entryRegionId": int(dungeons.get("locations", {}).get(str(row["DungeonBattleId"]), {}).get("region", 0)),
            "rank": row["Rank"],
            "isShowStory": row["IsShowStory"],
            "difficulties": difficulties,
        })

    map_output = []
    reward_rule_ids = set()
    for row in dungeons["tables"]["daily_dungeon_map_group"]:
        reward_rows = []
        for difficulty, grade_map in row["DifficultyGradeAndRewardRulesDict"].items():
            for grade, reward_ids in grade_map.items():
                reward_rows.append({
                    "difficulty": int(difficulty), "grade": int(grade),
                    "rewardRuleIds": [int(value) for value in reward_ids],
                })
                reward_rule_ids.update(int(value) for value in reward_ids)
        equipment_rows = []
        for difficulty, levels in row["DifficultyEquipDropLevelDict"].items():
            bless = row["DifficultyEquipDropBlessLevelDict"].get(difficulty, [0, 0])
            equipment_rows.append({
                "difficulty": int(difficulty),
                "minimumLevel": int(levels[0]), "maximumLevel": int(levels[1]),
                "minimumBlessLevel": int(bless[0]), "maximumBlessLevel": int(bless[1]),
            })
        map_output.append({
            "id": row["GroupId"], "rowCount": row["RowCount"],
            "columnCount": row["ColumnCount"], "startRoomId": row["StartRoomId"],
            "roomArray": [int(value) for line in row["RoomArray"] for value in line],
            "gradeRows": [
                {"difficulty": int(difficulty), "grade": int(grade), "row": int(count)}
                for difficulty, grade_map in row["DifficultyGradeDict"].items()
                for grade, count in grade_map.items()
            ],
            "equipmentDrops": equipment_rows,
            "gradeRewards": reward_rows,
        })

    room_output = []
    for row in dungeons["tables"]["daily_dungeon_room"]:
        surprise_rows = []
        for difficulty, reward_ids in (row.get("DifficultySurpriseAwardRulesDict") or {}).items():
            surprise_rows.append({
                "difficulty": int(difficulty),
                "rewardRuleIds": [int(value) for value in reward_ids],
            })
            reward_rule_ids.update(int(value) for value in reward_ids)
        room_output.append({
            "id": row["RoomId"], "type": row["RoomType"], "stageId": row["StageId"],
            "nextRoomIds": [int(value) for value in row["NextRoomIdList"]],
            "surpriseRewards": surprise_rows,
        })

    condition_output = []
    for class_id in sorted(condition_ids):
        value = condition_rows[class_id]
        condition_output.append(condition_record(class_id, value))

    stage_ids = {row["stageId"] for row in room_output if row["stageId"] > 0}
    stage_rows = {int(row["Id"]): row for row in rules["stage"]}
    missing_stages = stage_ids.difference(stage_rows)
    if missing_stages:
        raise ValueError("Daily dungeon stages are missing from official rules: " + repr(sorted(missing_stages)))
    reward_rows_by_id = {}
    for row in rules["reward_rule"]:
        reward_rows_by_id.setdefault(int(row["Id"]), []).append(row)
    level_numbers = {int(row["Id"]): [float(value) for value in row["Nums"]]
                     for row in rules["level_number"]}
    missing_rewards = reward_rule_ids.difference(reward_rows_by_id)
    if missing_rewards:
        raise ValueError("Daily dungeon reward rules are missing from official rules: " + repr(sorted(missing_rewards)))
    level_number_ids = set()
    for key in reward_rule_ids:
        for reward in reward_rows_by_id[key]:
            if reward.get("LevelCountId"):
                level_number_ids.add(int(reward["LevelCountId"]))
            level_number_ids.update(int(value) for value in (reward.get("SubRankCountId") or []))
    missing_level_numbers = level_number_ids.difference(level_numbers)
    if missing_level_numbers:
        raise ValueError("Daily dungeon level multipliers are missing: " + repr(sorted(missing_level_numbers)))
    reward_output = []
    reward_condition_ids = set()
    for key in sorted(reward_rule_ids):
        for row in reward_rows_by_id[key]:
            condition = row.get("Condition") or {}
            condition_type = condition.get("_type", "")
            condition_children = [int(value) for value in condition.get("Ids", [])]
            if condition_type not in ("", "PlayerProfessionCondition", "PlayerProfessionFactionCondition",
                                      "CompositeAndCondition", "CompositeOrCondition"):
                raise ValueError("Unsupported daily dungeon reward condition: " + repr(condition_type))
            if condition_type in ("CompositeAndCondition", "CompositeOrCondition"):
                reward_condition_ids.update(value for value in condition_children if value > 0)
            reward_output.append({
                "Id": row["Id"], "Order": row["Order"], "Percent": row["Percent"],
                "IdList": row["IdList"], "WeightList": row["WeightList"], "CountList": row["CountList"],
                "LevelCountId": row["LevelCountId"] or 0,
                "SubRankCountId": row["SubRankCountId"] or [],
                "IndexMininum": row["IndexMininum"] or [],
                "ConditionType": condition_type,
                "ConditionProfession": int(condition.get("Profession", 0)),
                "ConditionIds": condition_children,
            })
    random_equipment_pools = []
    random_reward_rows = {int(row["Id"]): row for row in rules.get("random_reward", [])}
    equip_ids = {int(row["ClassId"]) for row in rules.get("equip", [])}
    item_types = {int(row["ClassId"]): int(row["ItemType"]) for row in rules["item"]}
    random_equipment_ids = {
        item_id for key in reward_rule_ids for reward in reward_rows_by_id[key]
        for item_id in reward.get("IdList", [])
        if item_types.get(int(item_id), 0) == 110
    }
    for item_id in sorted(random_equipment_ids):
        pool = random_reward_rows.get(item_id)
        if pool is None:
            raise ValueError("Daily dungeon random equipment pool is missing: " + str(item_id))
        candidates = [int(value) for value in (pool.get("IdList") or [])]
        weights = [int(value) for value in (pool.get("WeightOfIdList") or [])]
        if len(candidates) != len(weights) or not candidates or any(
                candidate not in equip_ids for candidate, weight in zip(candidates, weights) if weight > 0):
            raise ValueError("Daily dungeon random equipment pool is invalid: " + str(item_id))
        random_equipment_pools.append({"id": item_id, "equipIds": candidates, "weights": weights})
    pending_reward_conditions = list(reward_condition_ids)
    while pending_reward_conditions:
        condition_id = pending_reward_conditions.pop()
        value = condition_rows.get(condition_id)
        if value is None:
            raise ValueError("Daily dungeon reward condition is missing: " + str(condition_id))
        for child_id in value.get("Ids", []):
            child_id = int(child_id)
            if child_id > 0 and child_id not in condition_ids:
                condition_ids.add(child_id)
                pending_reward_conditions.append(child_id)
        if condition_id not in {row["id"] for row in condition_output}:
            condition_output.append(condition_record(condition_id, value))

    stage_output = []
    game_ids = set()
    formation_ids = set()
    for stage_id in sorted(stage_ids):
        row = stage_rows[stage_id]
        game_id = int(row["GameId"])
        game_ids.add(game_id)
        wave_arrays = [int(value) for value in (row["WaveArrayIds"] or [])]
        formation_ids.update(value for value in wave_arrays if value > 0)
        stage_output.append({
            "id": stage_id, "gameId": game_id, "level": row["Level"],
            "subRank": int(row["SubRank"]), "maxRound": int(row["MaxRound"]),
            "waveArrayIds": wave_arrays,
            "waveArrayCatalogs": [int(value) for value in (row["WaveArrayCatalogs"] or [])],
            "waveMonsterIds": [[int(value) for value in wave] for wave in (row["WaveMonsterIds"] or [])],
            "monsterGroupNo": int(row["MonsterGroupNo"]), "monsterLevelAdd": int(row["MonsterLevelAdd"]),
            "playerRoundLimit": int(row["PlayerRoundLimit"]),
            "isPlayerRoundLimit": bool(row["IsPlayerRoundLimit"]),
            "usePlayerHp": bool(row["UsePlayerHp"]), "background": bool(row["Background"]),
            "awards": row["Awards"], "monsterAwardGroupId": int(row["MonsterAwardGroupId"]),
        })

    battle_scene_output = []
    for game_id in sorted(game_ids):
        row = battle_stages["stages"].get(str(game_id))
        if row is None:
            raise ValueError("Official battle grid is missing for daily dungeon GameId " + str(game_id))
        grid = row["FightGridGroundComponent"]
        fight = row["FightComponent"]
        normal = row.get("FightRoleMonsterNormalComponent") or {}
        battle_scene_output.append({
            "gameId": game_id,
            "sceneName": fight["SceneName"],
            "gridWidth": grid["Size"]["X"], "gridHeight": grid["Size"]["Y"],
            "gridOrigin": grid["OriginPos"],
            "cells": [
                {"x": value["Pos"]["X"], "y": value["Pos"]["Y"],
                 "groundType": value["GroundType"], "height": value["Height"]}
                for value in grid["PosInfos"]
            ],
            "factions": [
                {"id": value["Faction"],
                 "entryPositions": [[pos["X"], pos["Y"]] for pos in value["EntryPosList"]],
                 "entryDirections": value["EntryDirList"]}
                for value in fight["FactionInfoList"]
            ],
            "waveOrigins": [[float(pos["X"]), float(pos["Y"])]
                            for pos in (normal.get("WaveOriPos") or [])],
        })

    formation_output = []
    for formation_id in sorted(formation_ids):
        row = battle_stages["formations"].get(str(formation_id))
        if row is None:
            raise ValueError("Official daily dungeon formation is missing: " + str(formation_id))
        formation_output.append({
            "id": formation_id, "catalog": row["Catalog"],
            "monsters": [
                {"groupId": value["GroupId"], "x": value["Pos"]["X"],
                 "y": value["Pos"]["Y"], "rotation": value["Rot"],
                 "levelAdd": value["LevelAdd"], "faction": value["Faction"],
                 "viewGroup": value["ViewGroup"], "monsterType": value["MonsterType"],
                 "flipX": value["FlipX"]}
                for value in row["Monsters"]
            ],
        })

    formations_by_id = {row["id"]: row for row in formation_output}
    scenes_by_game_id = {row["gameId"]: row for row in battle_scene_output}
    for stage in stage_output:
        if not stage["waveArrayIds"]:
            continue
        scene = scenes_by_game_id[stage["gameId"]]
        if len(scene["waveOrigins"]) < len(stage["waveArrayIds"]):
            raise ValueError("Official dungeon battle scene has too few wave origins for stage " + str(stage["id"]))
        for array_id, monster_ids in zip(stage["waveArrayIds"], stage["waveMonsterIds"]):
            formation = formations_by_id.get(array_id)
            group_count = len({row["groupId"] for row in (formation or {}).get("monsters", [])})
            if formation is None or group_count > len(monster_ids):
                raise ValueError("Official dungeon formation differs from stage wave " + str(stage["id"]))

    payload = {
        "manifest": SOURCE_MANIFEST,
        "settings": dict(rules["game_settings"]["DailyDungeonSetting"],
                         SuppressUnlockRank=rules["game_settings"]["SuppressUnlockRank"]),
        "dungeons": battle_output,
        "maps": map_output,
        "rooms": room_output,
        "stages": stage_output,
        "battleScenes": battle_scene_output,
        "formations": formation_output,
        "conditions": condition_output,
        "rewardRules": reward_output,
        "randomEquipmentPools": random_equipment_pools,
        "levelNumbers": [
            {"id": key, "values": level_numbers[key]} for key in sorted(level_number_ids)
        ],
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    encoded = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    OUTPUT.write_bytes(encoded)
    receipt = {
        "manifest": SOURCE_MANIFEST,
        "inputs": [
            {"path": path.relative_to(ROOT).as_posix(), "bytes": path.stat().st_size, "sha256": sha256(path)}
            for path in INPUTS
        ],
        "output": {
            "path": OUTPUT.relative_to(ROOT).as_posix(),
            "bytes": len(encoded), "sha256": hashlib.sha256(encoded).hexdigest(),
        },
        "rows": {
            "dungeons": len(battle_output), "maps": len(map_output),
            "rooms": len(room_output), "conditions": len(condition_output),
            "stages": len(stage_output),
            "battleScenes": len(battle_scene_output),
            "formations": len(formation_output),
            "rewardRules": len(payload["rewardRules"]),
            "randomEquipmentPools": len(payload["randomEquipmentPools"]),
        },
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


if __name__ == "__main__":
    print(json.dumps(build(), ensure_ascii=False, indent=2))
