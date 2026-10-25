"""Build the official challenge stage, scene and formation catalog for Unity."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE_IDS = (21153, 21154, 22600, 72100)
RULES = ROOT / "assets/official/rules.json"
BATTLES = ROOT / "assets/official/battle-stages.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/DailyChallenges/battle-stages.json"
RECEIPT = ROOT / "assets/reference/daily-challenges/battle-stages-source-manifest.json"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def main() -> None:
    rules_bytes = RULES.read_bytes()
    battle_bytes = BATTLES.read_bytes()
    rules = json.loads(rules_bytes)
    battles = json.loads(battle_bytes)
    source_stages = {int(row["Id"]): row for row in rules["stage"]}
    missing = sorted(set(STAGE_IDS).difference(source_stages))
    if missing:
        raise ValueError("Official challenge stage configuration is missing: " + repr(missing))

    stages = []
    game_ids = set()
    formation_ids = set()
    for stage_id in STAGE_IDS:
        row = source_stages[stage_id]
        game_id = int(row["GameId"])
        wave_arrays = [int(value) for value in (row.get("WaveArrayIds") or [])]
        wave_monsters = [[int(value) for value in wave] for wave in (row.get("WaveMonsterIds") or [])]
        if not wave_monsters or any(not wave for wave in wave_monsters):
            raise ValueError("Official challenge stage has an empty wave: " + str(stage_id))
        game_ids.add(game_id)
        formation_ids.update(value for value in wave_arrays if value > 0)
        stages.append({
            "id": stage_id, "gameId": game_id, "level": row["Level"],
            "subRank": int(row["SubRank"]), "maxRound": int(row["MaxRound"]),
            "waveArrayIds": wave_arrays,
            "waveArrayCatalogs": [int(value) for value in (row.get("WaveArrayCatalogs") or [])],
            "waveMonsterIds": wave_monsters,
            "monsterGroupNo": int(row["MonsterGroupNo"]),
            "monsterLevelAdd": int(row["MonsterLevelAdd"]),
            "playerRoundLimit": int(row["PlayerRoundLimit"]),
            "isPlayerRoundLimit": bool(row["IsPlayerRoundLimit"]),
            "usePlayerHp": bool(row["UsePlayerHp"]), "background": bool(row["Background"]),
            "awards": row["Awards"], "monsterAwardGroupId": int(row["MonsterAwardGroupId"]),
        })

    scenes = []
    for game_id in sorted(game_ids):
        row = battles["stages"].get(str(game_id))
        if row is None:
            raise ValueError("Official battle scene is missing for challenge GameId " + str(game_id))
        grid = row["FightGridGroundComponent"]
        fight = row["FightComponent"]
        normal = row.get("FightRoleMonsterNormalComponent") or {}
        factions = [{
            "id": int(value["Faction"]),
            "entryPositions": [[int(pos["X"]), int(pos["Y"])] for pos in value["EntryPosList"]],
            "entryDirections": [int(pos) for pos in value["EntryDirList"]],
        } for value in fight["FactionInfoList"]]
        if not any(value["id"] == 0 and value["entryPositions"] for value in factions):
            raise ValueError("Challenge battle scene has no player faction: " + str(game_id))
        if not normal.get("WaveOriPos"):
            raise ValueError("Challenge battle scene has no monster wave origins: " + str(game_id))
        scenes.append({
            "gameId": game_id, "sceneName": fight["SceneName"],
            "gridWidth": int(grid["Size"]["X"]), "gridHeight": int(grid["Size"]["Y"]),
            "gridOrigin": grid["OriginPos"],
            "cells": [{"x": int(value["Pos"]["X"]), "y": int(value["Pos"]["Y"]),
                       "groundType": int(value["GroundType"]), "height": int(value["Height"])}
                      for value in grid["PosInfos"]],
            "factions": factions,
            "waveOrigins": [[float(pos["X"]), float(pos["Y"])]
                            for pos in (normal.get("WaveOriPos") or [])],
        })

    formations = []
    for formation_id in sorted(formation_ids):
        row = battles["formations"].get(str(formation_id))
        if row is None:
            raise ValueError("Official challenge formation is missing: " + str(formation_id))
        formations.append({
            "id": formation_id, "catalog": row["Catalog"],
            "monsters": [{
                "groupId": int(value["GroupId"]), "x": int(value["Pos"]["X"]),
                "y": int(value["Pos"]["Y"]), "rotation": int(value["Rot"]),
                "levelAdd": int(value["LevelAdd"]), "faction": int(value["Faction"]),
                "viewGroup": int(value["ViewGroup"]), "monsterType": int(value["MonsterType"]),
                "flipX": bool(value["FlipX"]),
            } for value in row["Monsters"]],
        })
    for stage in stages:
        for wave_index, array_id in enumerate(stage["waveArrayIds"]):
            if array_id <= 0:
                continue
            formation = next((value for value in formations if value["id"] == array_id), None)
            if formation is None or wave_index >= len(stage["waveMonsterIds"]):
                raise ValueError("Challenge stage formation mapping is incomplete: " + str(stage["id"]))
            group_count = len({value["groupId"] for value in formation["monsters"]})
            if group_count > len(stage["waveMonsterIds"][wave_index]):
                raise ValueError("Challenge stage formation exceeds its official wave: " + str(stage["id"]))

    payload = {
        "manifest": "854_156383",
        "stageIds": list(STAGE_IDS), "stages": stages,
        "battleScenes": scenes, "formations": formations,
    }
    encoded = (json.dumps(payload, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(encoded)
    receipt = {
        "manifest": "854_156383",
        "inputs": {
            RULES.relative_to(ROOT).as_posix(): {"bytes": len(rules_bytes), "sha256": sha(rules_bytes)},
            BATTLES.relative_to(ROOT).as_posix(): {"bytes": len(battle_bytes), "sha256": sha(battle_bytes)},
        },
        "stage_ids": list(STAGE_IDS), "game_ids": sorted(game_ids),
        "formation_ids": sorted(formation_ids),
        "outputs": {OUTPUT.relative_to(ROOT).as_posix(): {"bytes": len(encoded), "sha256": sha(encoded)}},
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Prepared", len(stages), "official challenge stages,", len(scenes), "scenes and",
          len(formations), "formations.", flush=True)


if __name__ == "__main__":
    main()
