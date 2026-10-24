"""Build verified official Fantasy Tower stages, formations and battle scenes."""
from __future__ import annotations

import hashlib
import json
import shutil
import sys
from pathlib import Path

from official_apk import Apk
from official_tables import Constructors
import prepare_unity_map10_battle_data as battle


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = "854_156383"
TOWER_TABLE = ("config_luban", "Assets/Config/Luban/stage_demon_battlefield.g.bytes",
               "TbStagePartInfoDemonBattlefield")
OUTPUTS = (
    ROOT / "assets/official/tower-push/tower-battles.json",
    ROOT / "unity/Assets/Game/Resources/TowerPush/tower-battles.json",
)
SOURCE_MANIFEST = ROOT / "assets/reference/tower-push/tower-battle-source-manifest.json"
RESOURCE_MANIFEST = ROOT / "unity/Assets/Game/Resources/TowerPush/tower-battle-source-manifest.json"
PANEL_DATA = ROOT / "assets/official/tower-push/panels.json"
PANEL_SOURCE_MANIFEST = ROOT / "assets/reference/tower-push/source-manifest.json"
SPRITE_SOURCE = ROOT / "assets/official/tower-push/sprites"
SPRITE_OUTPUT = ROOT / "unity/Assets/Official/TowerPush/Sprites"


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(data) -> bytes:
    return (json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def write_meta(path: Path) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    content = (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n"
               "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    meta = Path(str(path) + ".meta")
    if meta.exists() and meta.read_text(encoding="utf-8") != content:
        raise ValueError("Refusing to replace changed Unity meta file: " + str(meta))
    if not meta.exists():
        meta.write_text(content, encoding="utf-8", newline="\n")


def write_sprite_meta(path: Path) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    content = (f"fileFormatVersion: 2\nguid: {guid}\nTextureImporter:\n"
        "  serializedVersion: 13\n  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n"
        "    sRGBTexture: 1\n    linearTexture: 0\n  isReadable: 0\n  streamingMipmaps: 0\n"
        "  textureFormat: 3\n  maxTextureSize: 4096\n  textureSettings:\n    serializedVersion: 2\n"
        "    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n"
        "  spriteMode: 1\n  spritePixelsToUnits: 100\n  alphaUsage: 1\n  alphaIsTransparency: 1\n"
        "  textureType: 8\n  textureShape: 1\n  spriteSheet:\n    serializedVersion: 2\n"
        "    sprites: []\n    outline: []\n    physicsShape: []\n    bones: []\n"
        f"    spriteID: {guid}\n    internalID: 0\n    vertices: []\n    indices: \n"
        "    edges: []\n    weights: []\n    secondaryTextures: []\n    nameFileIdTable: {}\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    meta = Path(str(path) + ".meta")
    if meta.exists() and meta.read_text(encoding="utf-8") != content:
        raise ValueError("Refusing to replace changed tower Sprite meta: " + str(meta))
    if not meta.exists():
        meta.write_text(content, encoding="utf-8", newline="\n")


def build() -> dict:
    apk = Apk()
    constructors = Constructors()
    obj = apk.asset(TOWER_TABLE[0], TOWER_TABLE[1])
    raw = obj.read().m_Script.encode("utf-8", "surrogateescape")
    tower_rows = constructors.table(raw, TOWER_TABLE[2])
    if len(tower_rows) != 589:
        raise ValueError("Pinned Fantasy Tower table no longer has 589 rows")
    layers = [row for row in tower_rows if int(row["Layer"]) > 0]
    expected = {1: 110, 2: 115, 3: 160, 4: 200}
    if {index: sum(int(row["EntryIndex"]) == index for row in layers)
            for index in expected} != expected or len(layers) != 585:
        raise ValueError("Fantasy Tower layer distribution differs from the pinned official table")
    stage_ids = {int(row["Id"]) for row in layers}

    stage_obj = apk.asset(battle.STAGE_TABLE[0], battle.STAGE_TABLE[1])
    stage_raw = stage_obj.read().m_Script.encode("utf-8", "surrogateescape")
    all_stages = constructors.table(stage_raw, battle.STAGE_TABLE[2])
    stages_by_id = {int(row["Id"]): row for row in all_stages}
    if not stage_ids.issubset(stages_by_id):
        raise ValueError("Fantasy Tower references StageInfo rows absent from the pinned APK")
    stages = [stages_by_id[stage_id] for stage_id in sorted(stage_ids)]

    rules = json.loads((ROOT / "assets/official/rules.json").read_text(encoding="utf-8"))
    reward_rule_ids = {int(rule_id) for stage in stages for rule_id in stage.get("Awards", [])}
    reward_rules_by_id = {}
    for row in rules["reward_rule"]:
        reward_rules_by_id.setdefault(int(row["Id"]), []).append(row)
    missing_reward_rules = reward_rule_ids.difference(reward_rules_by_id)
    if missing_reward_rules:
        raise ValueError("Fantasy Tower reward rules are missing: " + repr(sorted(missing_reward_rules)))
    reward_rows = []
    condition_ids = set()
    level_number_ids = set()
    random_reward_ids = set()
    for rule_id in sorted(reward_rule_ids):
        for row in reward_rules_by_id[rule_id]:
            condition = row.get("Condition") or {}
            condition_ids.update(int(value) for value in condition.get("Ids", []) if int(value) > 0)
            if row.get("LevelCountId"):
                level_number_ids.add(int(row["LevelCountId"]))
            level_number_ids.update(int(value) for value in (row.get("SubRankCountId") or []) if int(value) > 0)
            random_reward_ids.update(int(value) for value in row["IdList"] if int(value) in {
                int(item["Id"]) for item in rules["random_reward"]})
            reward_rows.append({
                "Id": int(row["Id"]), "Order": int(row["Order"]), "Percent": float(row["Percent"]),
                "IdList": row["IdList"], "WeightList": row["WeightList"], "CountList": row["CountList"],
                "LevelCountId": int(row["LevelCountId"] or 0),
                "SubRankCountId": row["SubRankCountId"] or [],
                "ConditionType": condition.get("_type", ""),
                "ConditionProfession": int(condition.get("Profession", 0)),
                "ConditionIds": [int(value) for value in condition.get("Ids", [])],
            })
    condition_rows = {int(row["ClassId"]): row["Condition"] for row in rules["conditions"]}
    pending_conditions = list(condition_ids)
    while pending_conditions:
        condition_id = pending_conditions.pop()
        condition = condition_rows.get(condition_id)
        if condition is None:
            raise ValueError("Fantasy Tower reward condition is missing: " + str(condition_id))
        for child in condition.get("Ids", []):
            child = int(child)
            if child > 0 and child not in condition_ids:
                condition_ids.add(child)
                pending_conditions.append(child)
    reward_conditions = [{"id": condition_id, "type": condition_rows[condition_id].get("_type", ""),
        "andIds": [int(value) for value in condition_rows[condition_id].get("Ids", [])],
        "orIds": [int(value) for value in condition_rows[condition_id].get("Ids", [])]
            if condition_rows[condition_id].get("_type") == "CompositeOrCondition" else [],
        "rank": int(condition_rows[condition_id].get("Rank", 0)),
        "level": int(condition_rows[condition_id].get("Level", 0)),
        "subRank": int(condition_rows[condition_id].get("SubRank", 0)),
        "profession": int(condition_rows[condition_id].get("Profession", 0)),
        "itemId": int(condition_rows[condition_id].get("ClassId", 0)),
        "count": int(condition_rows[condition_id].get("Count", 0)),
        "op": condition_rows[condition_id].get("Op", "")}
        for condition_id in sorted(condition_ids)]
    level_numbers = {int(row["Id"]): [float(value) for value in row["Nums"]]
                     for row in rules["level_number"]}
    missing_level_numbers = level_number_ids.difference(level_numbers)
    if missing_level_numbers:
        raise ValueError("Fantasy Tower reward level multipliers are missing: " + repr(sorted(missing_level_numbers)))
    random_rewards = [row for row in rules["random_reward"] if int(row["Id"]) in random_reward_ids]

    array_obj = apk.asset(battle.ARRAY_TABLE[0], battle.ARRAY_TABLE[1])
    array_raw = array_obj.read().m_Script.encode("utf-8", "surrogateescape")
    array_table = constructors.table(array_raw, battle.ARRAY_TABLE[2])
    all_arrays = array_table.InfoDict
    scenes_data = json.loads(battle.BATTLE_SCENES_PATH.read_text(encoding="utf-8"))
    scenes = scenes_data["stages"]
    combat, combat_provenance = battle._read_verified_json(
        battle.COMBAT_RULES_PATH, battle.COMBAT_RULES_MANIFEST_PATH, "output_sha256")
    skills, skills_provenance = battle._read_verified_json(
        battle.SKILL_RULES_PATH, battle.SKILL_RULES_MANIFEST_PATH, "output_sha256")
    general, general_provenance = battle._read_verified_json(
        battle.GENERAL_RULES_PATH, battle.GENERAL_RULES_MANIFEST_PATH, "data_sha256.rules.json")
    skill_obj = apk.asset(battle.SKILL_TABLE[0], battle.SKILL_TABLE[1])
    skill_raw = skill_obj.read().m_Script.encode("utf-8", "surrogateescape")
    skill_rows = constructors.table(skill_raw, battle.SKILL_TABLE[2])
    property_ids, property_hash = battle._read_property_ids()
    source_code = {}
    for name in ("StageMonsterArrayInfoParser.cs", "LevelPropParser.cs", "BattleFormulaHandler.cs",
                 "StageInfoParser.cs", "PlayerStageDemonBattlefieldUtils.cs"):
        path = battle.COMMON_SOURCE / name
        source_code[name] = {"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path.read_bytes())}

    referenced_arrays = set()
    referenced_games = {int(stage["GameId"]) for stage in stages}
    for stage in stages:
        wave_ids = stage.get("WaveArrayIds") or []
        wave_monsters = stage.get("WaveMonsterIds") or []
        if len(wave_ids) != len(wave_monsters):
            raise ValueError("Tower stage wave arrays and monster ids differ in length: " + str(stage["Id"]))
        for array_id, monster_ids in zip(wave_ids, wave_monsters):
            array_id = int(array_id)
            if array_id not in all_arrays:
                raise ValueError("Tower stage references a missing monster array: " + str(array_id))
            groups = {int(monster["GroupId"]) for monster in all_arrays[array_id]["Monsters"]}
            if len(groups) > len(monster_ids) or not groups:
                raise ValueError("Tower monster array GroupId values differ from StageInfo roster: "
                                 + str(array_id) + " " + repr(sorted(groups)) + " " + repr(monster_ids))
            referenced_arrays.add(array_id)
    missing_games = referenced_games - {int(key) for key in scenes}
    if missing_games:
        raise ValueError("Tower stages lack official battle scenes: " + ", ".join(map(str, sorted(missing_games))))
    for stage in stages:
        wave_ids = stage.get("WaveArrayIds") or []
        origins = scenes[str(int(stage["GameId"]))]["FightRoleMonsterNormalComponent"]["WaveOriPos"]
        if len(origins) < len(wave_ids):
            raise ValueError("Official tower scene has too few monster wave origins: " + str(stage["Id"]))
    arrays = {str(array_id): {
        "Catalog": int(all_arrays[array_id]["Catalog"]),
        "ArrayId": int(all_arrays[array_id]["ArrayId"]),
        "Monsters": sorted(all_arrays[array_id]["Monsters"], key=lambda value: int(value["GroupId"])),
    } for array_id in sorted(referenced_arrays)}
    selected_scenes = {str(game_id): scenes[str(game_id)] for game_id in sorted(referenced_games)}
    rosters = battle.collect_monster_rosters(stages, all_arrays, selected_scenes, combat,
        general, skill_rows, skills["entities"], property_ids, allow_unused_monster_ids=True)
    if len(rosters) != 585:
        raise ValueError("Not all 585 Fantasy Tower layers have battle rosters")
    payload = {
        "manifest": MANIFEST,
        "entries": [{"EntryIndex": index,
                     "StageIds": [int(row["Id"]) for row in sorted(layers, key=lambda item: int(item["Layer"]))
                                  if int(row["EntryIndex"]) == index],
                     "LayerCount": expected[index]} for index in expected],
        "towerStages": tower_rows,
        "stages": stages,
        "monsterArrays": arrays,
        "battleScenes": [dict({"GameId": int(game_id)}, **scene)
                          for game_id, scene in sorted(selected_scenes.items(), key=lambda pair: int(pair[0]))],
        "monsterRosters": rosters,
        "rewardRules": reward_rows,
        "rewardConditions": reward_conditions,
        "levelNumbers": [{"id": key, "values": value} for key, value in sorted(level_numbers.items())
                         if key in level_number_ids],
        "randomRewards": random_rewards,
        "battleRulesSha256": combat_provenance["sha256"],
        "skillRulesSha256": skills_provenance["sha256"],
        "generalRulesSha256": general_provenance["sha256"],
    }
    payload_raw = json_bytes(payload)
    panel_bytes = PANEL_DATA.read_bytes()
    panel_manifest = json.loads(PANEL_SOURCE_MANIFEST.read_text(encoding="utf-8"))
    if panel_manifest.get("manifest") != MANIFEST or panel_manifest.get("outputs", {}).get(
            "res://assets/official/tower-push/panels.json") != digest(panel_bytes):
        raise ValueError("Official Fantasy Tower prefab data differs from its source manifest")
    panel_data = json.loads(panel_bytes.decode("utf-8"))
    sprite_names = set()

    def visit(node):
        if isinstance(node, dict):
            image = node.get("image")
            sprite = image.get("sprite") if isinstance(image, dict) else None
            if isinstance(sprite, dict):
                source_uri = sprite.get("file", "")
                if source_uri.startswith("res://assets/official/tower-push/sprites/"):
                    sprite_names.add(Path(source_uri).name)
            for child in node.values(): visit(child)
        elif isinstance(node, list):
            for child in node: visit(child)

    visit(panel_data)
    sprite_records = {}
    sprite_outputs = {}
    for name in sorted(sprite_names):
        source = SPRITE_SOURCE / name
        target = SPRITE_OUTPUT / name
        if not source.is_file(): raise ValueError("Official tower UI Sprite is missing: " + name)
        source_bytes = source.read_bytes()
        if target.exists() and target.read_bytes() != source_bytes:
            raise ValueError("Refusing to overwrite changed tower Sprite: " + str(target))
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        write_sprite_meta(target)
        sprite_records[name] = {"source": source.relative_to(ROOT).as_posix(),
                                "sourceSha256": digest(source_bytes),
                                "unityPath": target.relative_to(ROOT).as_posix(),
                                "sha256": digest(target.read_bytes())}
        sprite_outputs[target.relative_to(ROOT).as_posix()] = digest(target.read_bytes())
    manifest = {
        "format": 1,
        "manifest": MANIFEST,
        "layerCount": len(layers),
        "runtimeOutputSha256": digest(payload_raw),
        "entryLayerCounts": {str(key): value for key, value in expected.items()},
        "operation": "Decode the pinned Luban tower table and StageInfo; expand each official monster formation and use the shared official Map10 battle roster and scene conversion rules.",
        "uiSprites": sprite_records,
        "inputs": {
            "towerTable": {"path": TOWER_TABLE[1], "size": len(raw), "sha256": digest(raw), **apk.provenance(obj)},
            "stageTable": {"path": battle.STAGE_TABLE[1], "size": len(stage_raw), "sha256": digest(stage_raw), **apk.provenance(stage_obj)},
            "monsterArrayTable": {"path": battle.ARRAY_TABLE[1], "size": len(array_raw), "sha256": digest(array_raw), **apk.provenance(array_obj)},
            "skillTable": {"path": battle.SKILL_TABLE[1], "size": len(skill_raw), "sha256": digest(skill_raw), **apk.provenance(skill_obj)},
            "battleScenes": {"path": battle.BATTLE_SCENES_PATH.relative_to(ROOT).as_posix(), "sha256": digest(battle.BATTLE_SCENES_PATH.read_bytes())},
            "towerPrefabs": {"path": PANEL_DATA.relative_to(ROOT).as_posix(), "sha256": digest(panel_bytes),
                             "sourceManifest": PANEL_SOURCE_MANIFEST.relative_to(ROOT).as_posix(),
                             "sourceManifestSha256": digest(PANEL_SOURCE_MANIFEST.read_bytes())},
            "battleRules": combat_provenance,
            "skillRules": skills_provenance,
            "generalRules": general_provenance,
            "rewardRules": {"path": "assets/official/rules.json",
                            "sha256": digest((ROOT / "assets/official/rules.json").read_bytes()),
                            "sourceManifest": "assets/reference/official-city/rules-manifest.json",
                            "sourceManifestSha256": digest((ROOT / "assets/reference/official-city/rules-manifest.json").read_bytes())},
            "officialFormula": {"sources": source_code,
                "propertyTypes": {"path": (battle.COMMON_SOURCE / "PropType.cs").relative_to(ROOT).as_posix(),
                                  "sha256": property_hash}},
        },
        "outputs": {**{path.relative_to(ROOT).as_posix(): digest(payload_raw) for path in OUTPUTS},
                    **sprite_outputs},
    }
    manifest_raw = json_bytes(manifest)
    old_manifest = None
    if SOURCE_MANIFEST.exists():
        old_manifest = json.loads(SOURCE_MANIFEST.read_text(encoding="utf-8"))
    for path in OUTPUTS:
        if path.exists() and path.read_bytes() != payload_raw:
            old_hash = None if old_manifest is None else old_manifest.get("outputs", {}).get(path.relative_to(ROOT).as_posix())
            if old_hash != digest(path.read_bytes()):
                raise ValueError("Refusing to overwrite changed Fantasy Tower data: " + str(path))
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(payload_raw)
        if path.is_relative_to(ROOT / "unity"):
            write_meta(path)
    SOURCE_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    SOURCE_MANIFEST.write_bytes(manifest_raw)
    if RESOURCE_MANIFEST.exists() and RESOURCE_MANIFEST.read_bytes() != manifest_raw:
        old_hash = None if old_manifest is None else digest(json_bytes(old_manifest))
        if old_hash != digest(RESOURCE_MANIFEST.read_bytes()):
            raise ValueError("Refusing to overwrite changed Unity Fantasy Tower manifest")
    RESOURCE_MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    RESOURCE_MANIFEST.write_bytes(manifest_raw)
    write_meta(RESOURCE_MANIFEST)
    return manifest


if __name__ == "__main__":
    result = build()
    print("Fantasy Tower layers:", result["layerCount"])
    print("Tower data SHA256:", next(iter(result["outputs"].values())))
