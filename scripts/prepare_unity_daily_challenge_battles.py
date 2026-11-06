"""Build the official challenge stage, scene and formation catalog for Unity."""
from __future__ import annotations

import hashlib
import json
import argparse
import copy
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STAGE_IDS = (21153, 21154, 22600, 72100)
RULES = ROOT / "assets/official/rules.json"
BATTLES = ROOT / "assets/official/battle-stages.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/DailyChallenges/battle-stages.json"
RECEIPT = ROOT / "assets/reference/daily-challenges/battle-stages-source-manifest.json"


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def repair_serialized_arrays() -> None:
    """Adapt the committed official catalog without changing source values.

    Some historical extraction inputs live only in the source archive. This
    format-only repair consumes the previously migrated, committed payload;
    its exact Git identity and before/after hashes remain in a separate receipt.
    """
    original = OUTPUT.read_bytes()
    relative = OUTPUT.relative_to(ROOT).as_posix()
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT).decode().strip()
    committed = subprocess.check_output(['git', 'show', head + ':' + relative], cwd=ROOT)
    if original.replace(b'\r\n', b'\n') != committed.replace(b'\r\n', b'\n'):
        raise ValueError('Serialization repair requires the unchanged committed catalog')
    payload = json.loads(original)
    if payload['manifest'] != '854_156383' or payload['stageIds'] != list(STAGE_IDS):
        raise ValueError('Review the input identity before adapting its serialization')
    updated = copy.deepcopy(payload)
    for stage in updated['stages']:
        flat, offsets = [], [0]
        for wave in stage['waveMonsterIds']:
            flat.extend(wave)
            offsets.append(len(flat))
        stage.update(waveMonsterIdsFlat=flat, waveMonsterOffsets=offsets)
        if [flat[offsets[i]:offsets[i + 1]] for i in range(len(offsets) - 1)] != stage['waveMonsterIds']:
            raise ValueError('Monster-wave serialization changed source values')
    for scene in updated['battleScenes']:
        for faction in scene['factions']:
            entries = faction['entryPositions']
            if any(len(pos) != 2 for pos in entries): raise ValueError('Malformed source entry')
            faction['entryPosition'] = entries[0] if entries else []
            faction['entryPositionsFlat'] = [coordinate for pos in entries for coordinate in pos]
        origins = scene['waveOrigins']
        if any(len(pos) != 2 for pos in origins): raise ValueError('Malformed source origin')
        scene['waveOriginsFlat'] = [coordinate for pos in origins for coordinate in pos]
        scene['waveOriginOffsets'] = list(range(0, len(scene['waveOriginsFlat']) + 1, 2))
    # Only additive serialization fields may differ from the committed source.
    def unchanged(before, after):
        if isinstance(before, dict): return all(key in after and unchanged(value, after[key]) for key, value in before.items())
        if isinstance(before, list): return len(before) == len(after) and all(unchanged(a, b) for a, b in zip(before, after))
        return before == after
    if not unchanged(payload, updated): raise ValueError('Source catalog values changed')
    encoded = (json.dumps(updated, ensure_ascii=False, separators=(',', ':')) + '\n').encode('utf8')
    receipt = dict(manifest=payload['manifest'],
        input=dict(path=relative, commit=head, bytes=len(original), sha256=sha(original), git_sha256=sha(committed)),
        generator=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes())),
        outputs={relative: dict(bytes=len(encoded), sha256=sha(encoded))},
        adaptation='Lossless additive flat arrays/offsets for the existing common battle adapter; all original nested values retained unchanged.')
    OUTPUT.write_bytes(encoded)
    target = ROOT / 'assets/reference/daily-challenges/battle-stages-serialization-source-manifest.json'
    target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Adapted', len(updated['stages']), 'official stages and', len(updated['battleScenes']),
          'scenes for Unity serialization; original values unchanged.', flush=True)


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
        # JsonUtility does not restore jagged arrays. Use the same lossless
        # representation already consumed by the shared dungeon battle adapter.
        wave_ids_flat = []
        wave_offsets = [0]
        for wave in wave_monsters:
            wave_ids_flat.extend(wave)
            wave_offsets.append(len(wave_ids_flat))
        stages.append({
            "id": stage_id, "gameId": game_id, "level": row["Level"],
            "subRank": int(row["SubRank"]), "maxRound": int(row["MaxRound"]),
            "waveArrayIds": wave_arrays,
            "waveArrayCatalogs": [int(value) for value in (row.get("WaveArrayCatalogs") or [])],
            "waveMonsterIds": wave_monsters,
            "waveMonsterIdsFlat": wave_ids_flat,
            "waveMonsterOffsets": wave_offsets,
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
            "entryPosition": ([int(value["EntryPosList"][0]["X"]), int(value["EntryPosList"][0]["Y"])]
                              if value["EntryPosList"] else []),
            "entryPositionsFlat": [int(coordinate) for pos in value["EntryPosList"]
                                   for coordinate in (pos["X"], pos["Y"])],
            "entryPositions": [[int(pos["X"]), int(pos["Y"])] for pos in value["EntryPosList"]],
            "entryDirections": [int(pos) for pos in value["EntryDirList"]],
        } for value in fight["FactionInfoList"]]
        if not any(value["id"] == 0 and value["entryPositions"] for value in factions):
            raise ValueError("Challenge battle scene has no player faction: " + str(game_id))
        if not normal.get("WaveOriPos"):
            raise ValueError("Challenge battle scene has no monster wave origins: " + str(game_id))
        wave_origins = [[float(pos["X"]), float(pos["Y"])] for pos in normal["WaveOriPos"]]
        origins_flat = [coordinate for origin in wave_origins for coordinate in origin]
        scenes.append({
            "gameId": game_id, "sceneName": fight["SceneName"],
            "gridWidth": int(grid["Size"]["X"]), "gridHeight": int(grid["Size"]["Y"]),
            "gridOrigin": grid["OriginPos"],
            "cells": [{"x": int(value["Pos"]["X"]), "y": int(value["Pos"]["Y"]),
                       "groundType": int(value["GroundType"]), "height": int(value["Height"])}
                      for value in grid["PosInfos"]],
            "factions": factions,
            "waveOrigins": wave_origins,
            "waveOriginsFlat": origins_flat,
            "waveOriginOffsets": list(range(0, len(origins_flat) + 1, 2)),
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
        "generator": {"path": Path(__file__).relative_to(ROOT).as_posix(),
                      "sha256": sha(Path(__file__).read_bytes())},
        "serialization": "Original nested values retained; lossless flat arrays and offsets bind the existing JsonUtility-compatible common battle adapter.",
        "outputs": {OUTPUT.relative_to(ROOT).as_posix(): {"bytes": len(encoded), "sha256": sha(encoded)}},
    }
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Prepared", len(stages), "official challenge stages,", len(scenes), "scenes and",
          len(formations), "formations.", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repair-serialized-arrays', action='store_true')
    options = parser.parse_args()
    if options.repair_serialized_arrays: repair_serialized_arrays()
    else: main()
