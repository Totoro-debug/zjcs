"""Build pinned map stage, monster formation, and arena data for Unity."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
from pathlib import Path

from official_apk import Apk, ROOT
from official_map_codec import entities as decode_entities
from official_tables import Constructors


VERSION = "854_156383"
OUTPUT = ROOT / "unity/Assets/Official/World/Map/10"
PLACEMENTS_SOURCE = ("config_mapinfowrap", "Assets/Config/MapInfoWrap/mapinfowrap_{map_id}.g.bytes")
MAP_STAGE_TABLE = ("config_luban", "Assets/Config/Luban/map_stage.g.bytes", "TbMapToStageInfo")
STAGE_TABLE = ("config_luban", "Assets/Config/Luban/stage.g.bytes", "TbStageInfo")
ARRAY_TABLE = ("config_luban", "Assets/Config/Luban/stage_monster_array.g.bytes", "TbStageMonsterArrayInfo")
SKILL_TABLE = ("config_luban", "Assets/Config/Luban/skill.g.bytes", "TbSkillInfo")
BATTLE_SCENES_PATH = ROOT / "assets/official/battle-stages.json"
BATTLE_SCENES_MANIFEST_PATH = ROOT / "assets/reference/official-map/battle-stages-source-manifest.json"
COMBAT_RULES_PATH = ROOT / "assets/official/battle-rules.json"
COMBAT_RULES_MANIFEST_PATH = ROOT / "unity/Assets/Official/Combat/battle-rules-source-manifest.json"
SKILL_RULES_PATH = ROOT / "assets/official/skill-rules.json"
SKILL_RULES_MANIFEST_PATH = ROOT / "unity/Assets/Official/Combat/skill-rules-source-manifest.json"
GENERAL_RULES_PATH = ROOT / "assets/official/rules.json"
GENERAL_RULES_MANIFEST_PATH = ROOT / "assets/reference/official-city/source-manifest.json"
PARSER_SOURCE_PATH = ROOT / ".tools/source-packages/official-code/common-source/Common/StageMonsterArrayInfoParser.cs"
COMMON_SOURCE = ROOT / ".tools/source-packages/official-code/common-source/Common"
REFERENCE_MANIFEST_PATH = ROOT / "assets/reference/official-map/map10-battles-source-manifest.json"
MAP11_DEFINITION_PATH = ROOT / "unity/Assets/Official/World/Map/11/map11.json"
MAP11_DEFINITION_MANIFEST_PATH = ROOT / "unity/Assets/Official/World/Map/11/source-manifest.json"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def json_bytes(value) -> bytes:
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")


def text_asset_bytes(obj) -> bytes:
    return obj.read().m_Script.encode("utf-8", "surrogateescape")


def source_record(apk: Apk, obj, path: str, payload: bytes) -> dict:
    raw = obj.get_raw_data()
    return dict(apk.provenance(obj), object=path, raw_size=len(raw), raw_sha256=sha256(raw),
                payload_size=len(payload), payload_sha256=sha256(payload))


def map_stage_ids(map_id: int, placements: dict, map_stage_rows: list[dict], expected_entity_count: int) -> set[int]:
    if placements.get("id") != map_id or not isinstance(placements.get("entities"), list):
        raise ValueError(f"Placement source does not identify Map{map_id}")
    if len(placements["entities"]) != expected_entity_count:
        raise ValueError(f"Map{map_id} placement count differs from the pinned source")
    stage_ids = set()
    for row in map_stage_rows:
        if int(row["MapClassId"]) != map_id:
            continue
        stage_ids.add(int(row["StageId"]))

    for entity in placements["entities"]:
        for value in entity.get("stages", {}).values():
            if not isinstance(value, list) or not value or not isinstance(value[0], list):
                raise ValueError(f"Map{map_id} entity stage state does not match the official stage schema")
            for stage_id in value[0]:
                if not isinstance(stage_id, int) or stage_id <= 0:
                    raise ValueError(f"Map{map_id} entity stage id is invalid")
                stage_ids.add(stage_id)

    if not stage_ids:
        raise ValueError(f"Map{map_id} does not reference any official stages")
    return stage_ids


def map11_component_stage_bindings(definition: dict) -> list[dict]:
    if (definition.get("ClassId") != 11 or not isinstance(definition.get("Entities"), list)
            or not isinstance(definition.get("EntityComponents"), list)
            or len(definition["Entities"]) != 1803
            or len(definition["EntityComponents"]) != 1803):
        raise ValueError("Pinned Map11 definition is missing its 1,803 entity component bindings")

    placement_ids = {int(placement["Id"]) for placement in definition["Entities"]}
    component_ids = [int(component.get("EntityId", 0)) for component in definition["EntityComponents"]]
    if len(placement_ids) != 1803 or len(set(component_ids)) != 1803 or set(component_ids) != placement_ids:
        raise ValueError("Map11 entity components do not map one-to-one to placements")

    bindings = []
    for component_record in sorted(definition["EntityComponents"], key=lambda row: int(row["EntityId"])):
        placement_id = int(component_record["EntityId"])
        try:
            components = json.loads(component_record["Json"])
        except (KeyError, TypeError, json.JSONDecodeError) as error:
            raise ValueError("Map11 entity component JSON is invalid: " + str(placement_id)) from error

        stage_components = []
        for component_name in ("LoStageBehStageComponent", "LoStageBehBattleSteleStageComponent",
                               "LoStageBehStoneCastleStageComponent"):
            component = components.get(component_name)
            if not isinstance(component, dict):
                continue
            stage_ids = component.get("StageIds") or []
            if component.get("StageId", 0):
                stage_ids = list(stage_ids) + [component["StageId"]]
            if not isinstance(stage_ids, list) or any(not isinstance(value, int) or value <= 0
                                                       for value in stage_ids):
                raise ValueError("Map11 entity stage component is malformed: " + str(placement_id))
            if stage_ids:
                stage_components.append({"Component": component_name,
                                         "StageIds": sorted(set(stage_ids))})

        if not stage_components:
            continue
        periodic = components.get("LoStageResultPeriodicityInteractionComponent") or {}
        player_periodic = components.get("LoStageConPlayerPeriodicityInteractionComponent") or {}
        result = {"PlacementId": placement_id, "StageComponents": stage_components}
        for key, source in (("PeriodicityGroupIds", periodic),
                            ("PlayerPeriodicityGroupIds", player_periodic)):
            group_ids = source.get("PeriodicityGroupIds") or []
            if not isinstance(group_ids, list) or any(not isinstance(value, int) or value <= 0
                                                       for value in group_ids):
                raise ValueError("Map11 entity periodicity component is malformed: " + str(placement_id))
            if group_ids:
                result[key] = group_ids
        bindings.append(result)
    return bindings


def map10_stage_ids(placements: dict, map_stage_rows: list[dict], expected_entity_count: int = 82) -> set[int]:
    return map_stage_ids(10, placements, map_stage_rows, expected_entity_count)


def collect_battle_data(stage_rows: list[dict], arrays: dict[int, dict], scenes: dict[str, dict]) -> tuple[dict, dict]:
    referenced_arrays = set()
    referenced_games = set()
    for stage in stage_rows:
        game_id = int(stage["GameId"])
        referenced_games.add(game_id)
        wave_ids = stage.get("WaveArrayIds") or []
        wave_monsters = stage.get("WaveMonsterIds") or []
        catalogs = stage.get("WaveArrayCatalogs")
        if catalogs is not None:
            raise ValueError("Map stage uses randomized WaveArrayCatalogs; preserve its official selection data first")
        if len(wave_ids) != len(wave_monsters):
            if wave_ids or wave_monsters:
                raise ValueError("Map stage wave array ids and monster ids differ in length")
        for wave_index, (array_id, monster_ids) in enumerate(zip(wave_ids, wave_monsters)):
            array_id = int(array_id)
            if array_id not in arrays:
                raise ValueError("Official map stage references a missing monster array: " + str(array_id))
            groups = {int(monster["GroupId"]) for monster in arrays[array_id]["Monsters"]}
            if catalogs is not None and len(groups) != len(monster_ids):
                raise ValueError("Monster array group count differs from the official stage roster: stage "
                                 + str(stage["Id"]) + ", wave " + str(wave_index) + ", array "
                                 + str(array_id) + ", groups " + str(sorted(groups)) + ", monsters "
                                 + str(monster_ids))
            referenced_arrays.add(array_id)

    missing_games = referenced_games - {int(key) for key in scenes}
    if missing_games:
        raise ValueError("Official map stage has no official battle scene: "
                         + ", ".join(map(str, sorted(missing_games))))

    for stage in stage_rows:
        wave_ids = stage.get("WaveArrayIds") or []
        if not wave_ids:
            continue
        scene = scenes[str(int(stage["GameId"]))]
        normal = scene.get("FightRoleMonsterNormalComponent")
        origins = [] if normal is None else normal.get("WaveOriPos") or []
        if len(origins) < len(wave_ids):
            raise ValueError("Official battle scene group count has too few wave origins for stage "
                             + str(stage["Id"]))

    selected_scenes = {str(game_id): scenes[str(game_id)] for game_id in sorted(referenced_games)}
    selected_arrays = {}
    for array_id in sorted(referenced_arrays):
        row = arrays[array_id]
        selected_arrays[str(array_id)] = {
            "Catalog": int(row["Catalog"]),
            "ArrayId": int(row["ArrayId"]),
            "Monsters": sorted(row["Monsters"], key=lambda value: int(value["GroupId"])),
        }

    return selected_arrays, selected_scenes


def _read_verified_json(payload_path: Path, manifest_path: Path, hash_field: str) -> tuple[dict, dict]:
    payload = payload_path.read_bytes()
    manifest_bytes = manifest_path.read_bytes()
    manifest = json.loads(manifest_bytes.decode("utf-8"))
    section, separator, field = hash_field.partition(".")
    expected_hash = manifest.get(section)
    if separator and isinstance(expected_hash, dict):
        expected_hash = expected_hash.get(field)
    if manifest.get("manifest") != VERSION or expected_hash != sha256(payload):
        raise ValueError("Official catalog differs from its pinned source manifest: " + str(payload_path))
    return json.loads(payload.decode("utf-8")), {
        "path": payload_path.relative_to(ROOT).as_posix(),
        "sha256": sha256(payload),
        "source_manifest": manifest_path.relative_to(ROOT).as_posix(),
        "source_manifest_sha256": sha256(manifest_bytes),
    }


def _read_property_ids() -> tuple[dict[str, int], str]:
    path = COMMON_SOURCE / "PropType.cs"
    source = path.read_text(encoding="utf-8-sig")
    values = {name: int(value) for name, value in
              re.findall(r"\b(\w+)\s*=\s*(\d+)", source)}
    if not values or values.get("MaxHp") != 3 or values.get("Attack") != 1:
        raise ValueError("Pinned PropType enum could not be decoded")
    return values, sha256(path.read_bytes())


def calculate_monster_properties(group: dict, level: float, sub_rank: int,
                                 combat_rules: dict, property_ids: dict[str, int]) -> dict[int, int]:
    """Port CalcMonsterBattleProps and LevelPropParser.GetPropsByFloat."""
    role_prop_id = int(group["PropCfg"]["PropGroupId"])
    role_group = next((row for row in combat_rules["tables"]["role_prop_group"]
                       if int(row["ID"]) == role_prop_id and int(row["SubRank"]) == sub_rank), None)
    if role_group is None:
        raise ValueError("Official monster role property group is missing")

    props_by_class = combat_rules["props"]
    prop_class_id = int(role_group["LevelPropID"])
    def level_props(class_id: int) -> dict[int, int]:
        rows = props_by_class.get(str(class_id), {}).get("rows", {})
        source_level = struct.unpack("<f", struct.pack("<f", level))[0]
        low = int(source_level // 1)
        high = low if abs(source_level - low) < 1e-5 else low + 1
        low_row = rows.get(str(low))
        high_row = rows.get(str(high))
        available_levels = sorted(int(key) for key in rows)
        if not available_levels:
            return {}
        if high == low:
            if low_row is not None:
                return {int(key): int(value) for key, value in low_row.items()}
            if low < available_levels[-1]:
                return {}
            return {int(key): int(value) for key, value in rows[str(available_levels[-1])].items()}
        if low_row is None and high_row is None:
            return {int(key): int(value) for key, value in rows[str(available_levels[-1])].items()}
        if low_row is None:
            return {int(key): int(value) for key, value in high_row.items()}
        if high_row is None or low == high:
            return {int(key): int(value) for key, value in low_row.items()}
        t = struct.unpack("<f", struct.pack("<f", source_level - low))[0]
        result = {}
        for key in set(low_row) | set(high_row):
            left = struct.unpack("<f", struct.pack("<f", int(low_row.get(key, 0))))[0]
            right = struct.unpack("<f", struct.pack("<f", int(high_row.get(key, 0))))[0]
            delta = struct.unpack("<f", struct.pack("<f", right - left))[0]
            result[int(key)] = int(struct.unpack("<f", struct.pack("<f", left + delta * t))[0])
        return result

    props = level_props(prop_class_id)
    extra_class_id = int(role_group.get("ExtraLevelPropID") or 0)
    if extra_class_id:
        extra = level_props(extra_class_id)
        for prop_id, value in extra.items():
            props[prop_id] = props.get(prop_id, 0) + value

    def id_of(name: str) -> int:
        return int(property_ids.get(name, 0))

    def get(name: str) -> int:
        return props.get(id_of(name), 0) if id_of(name) else 0

    def increment(name: str, reference: int, rate: int) -> None:
        prop_id = id_of(name)
        if prop_id and rate:
            props[prop_id] = props.get(prop_id, 0) + int(reference * (rate / 10000.0))

    base_properties = ("MaxHp", "Attack", "Defence", "Speed")
    for name in base_properties:
        increment("Base" + name, get("Base" + name), get("Base" + name + "Rate"))
    for name in base_properties:
        rate_name = "Base" + name + "Percent"
        rate = get(rate_name)
        if rate:
            increment(name, get("Base" + name), rate)
            props.pop(id_of(rate_name), None)
    for name in ("ElementMaster", "ElementResistance", "KongFuMaster", "KongFuResistance",
                 "EffectRate", "EffectDodge"):
        increment(name, get(name), get(name + "Percent"))
    for name in base_properties:
        base_id = id_of("Base" + name)
        base_value = props.pop(base_id, 0) if base_id else 0
        if base_value:
            prop_id = id_of(name)
            props[prop_id] = props.get(prop_id, 0) + base_value

    for name in ("MaxHp", "Attack", "Defence", "Speed", "EffectDodge"):
        scale_id = id_of(name + "Scale")
        scale = props.get(scale_id, 0) if scale_id else 0
        if not scale:
            continue
        base_value = get("Base" + name)
        prop_id = id_of(name)
        reference = props.get(prop_id, 0) + base_value
        props[prop_id] = max(1, props.get(prop_id, 0) + int(reference * (scale / 10000.0)))
        props.pop(scale_id, None)

    factors = {int(key): int(value) for key, value in group["PropCfg"]["PropFactors"].items()}
    props = {prop_id: int(value * (factors[prop_id] / 10000.0))
             for prop_id, value in props.items() if prop_id in factors}
    for name in ("MaxHp", "Attack", "Defence", "Speed"):
        prop_id = id_of(name)
        if prop_id in props:
            props[prop_id] = max(1, props[prop_id])
    for key, value in (group.get("ExtraProps") or {}).items():
        prop_id = int(key)
        props[prop_id] = props.get(prop_id, 0) + int(value)
    return props


def collect_monster_rosters(stage_rows: list[dict], arrays: dict[int, dict], scenes: dict[str, dict],
                            combat_rules: dict, general_rules: dict, skill_rows: list[dict],
                            skill_entities: dict, property_ids: dict[str, int],
                            allow_unused_monster_ids: bool = False) -> list[dict]:
    skill_info = {int(row["ClassId"]): row for row in skill_rows}
    items = {int(row["ClassId"]): row for row in general_rules["item"]}
    monster_groups = {(int(row["MonsterId"]), int(row["GroupNo"])): row
                      for row in combat_rules["tables"]["monster_group"]}
    rosters = []
    for stage in sorted(stage_rows, key=lambda row: int(row["Id"])):
        waves = []
        for wave_index, group_monster_ids in enumerate(stage.get("WaveMonsterIds") or []):
            array_id = int(stage["WaveArrayIds"][wave_index])
            formation = sorted(arrays[array_id]["Monsters"], key=lambda value: int(value["GroupId"]))
            origins = scenes[str(int(stage["GameId"]))]["FightRoleMonsterNormalComponent"]["WaveOriPos"]
            origin = origins[wave_index]
            units = []
            group_index = -1
            previous_group_id = None
            for position in formation:
                group_id = int(position["GroupId"])
                if previous_group_id is None or group_id != previous_group_id:
                    group_index += 1
                    previous_group_id = group_id
                if group_index >= len(group_monster_ids):
                    raise ValueError("Official formation group exceeds the stage monster list")
                monster_id = int(group_monster_ids[group_index])
                group_no = int(stage["MonsterGroupNo"])
                monster_group = monster_groups.get((monster_id, group_no))
                if monster_group is None:
                    raise ValueError("Official monster group configuration is missing: "
                                     + str(monster_id) + "/" + str(group_no))
                use_player_level = bool(stage.get("AwardUsePlayerLevelAndSubRank"))
                level_offset = int(stage.get("MonsterLevelAdd") or 0) + float(position.get("LevelAdd") or 0)
                level = (1.0 if use_player_level else float(stage["Level"])) + level_offset
                sub_rank = 0 if use_player_level else int(stage["SubRank"])
                skills = []
                for slot, item_id_value in enumerate(monster_group.get("ActiveSkills") or []):
                    item_id = int(item_id_value)
                    info = skill_info.get(item_id)
                    item = items.get(item_id)
                    if info is None or item is None:
                        raise ValueError("Official monster skill item is missing: " + str(item_id))
                    entity_id = int(info["EcEntityId"])
                    if str(entity_id) not in skill_entities:
                        raise ValueError("Official monster skill entity is missing: " + str(entity_id))
                    skills.append({"Slot": slot, "ItemId": item_id, "EntityId": entity_id,
                                   "Rank": int(item["Rank"])})
                units.append({
                    "MonsterId": monster_id,
                    "MonsterGroupNo": group_no,
                    "Level": level,
                    "SubRank": int(stage["SubRank"]),
                    "Position": {"X": int(origin["X"]) + int(position["Pos"]["X"]),
                                 "Y": int(origin["Y"]) + int(position["Pos"]["Y"])},
                    "FormationGroupId": group_id,
                    "FormationGroupIndex": group_index,
                    "MonsterType": int(position["MonsterType"]),
                    "Faction": int(position["Faction"]),
                    "Rotation": int(position["Rot"]),
                    "FlipX": bool(position["FlipX"]),
                    "ViewGroup": int(position["ViewGroup"]),
                    "WaveIndex": wave_index,
                    "Properties": ([] if use_player_level else [
                        {"Id": prop_id, "Value": value}
                        for prop_id, value in sorted(calculate_monster_properties(
                            monster_group, level, sub_rank, combat_rules, property_ids).items())]),
                    "LevelOffset": level_offset,
                    "ActiveSkills": skills,
                    "PassiveSkills": list(monster_group.get("PassiveSkills") or []),
                    "DeactivePassiveSkills": list(monster_group.get("DeactivePassiveSkills") or []),
                    "HpPliesId": int(monster_group.get("HpPliesId") or 0),
                    "ResilienceInfo": monster_group.get("ResilienceInfo"),
                })
            if (group_index + 1 > len(group_monster_ids)
                    or (not allow_unused_monster_ids and group_index + 1 != len(group_monster_ids))):
                raise ValueError("Official formation group count differs from the stage monster list")
            waves.append({"Index": wave_index, "ArrayId": array_id, "Origin": origin, "Units": units})
        rosters.append({"StageId": int(stage["Id"]), "GameId": int(stage["GameId"]),
                        "MaxRound": int(stage["MaxRound"]), "MonsterGroupNo": int(stage["MonsterGroupNo"]),
                        "Level": float(stage["Level"]), "SubRank": int(stage["SubRank"]), "Waves": waves})
    return rosters


def write_meta(path: Path, importer: str) -> None:
    relative = path.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    content = (f"fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    target = Path(str(path) + ".meta")
    if target.exists() and target.read_text(encoding="utf-8") != content:
        raise ValueError("Refusing to replace an existing Unity meta file: " + str(target))
    if not target.exists():
        target.write_text(content, encoding="utf-8", newline="\n")


def write_generated(path: Path, content: bytes, expected_existing_hash: str | None = None) -> None:
    if path.exists():
        existing = path.read_bytes()
        if existing == content:
            return
        if expected_existing_hash is None or sha256(existing) != expected_existing_hash:
            raise ValueError("Refusing to overwrite changed official map battle data: " + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)


def existing_output_hashes(payload_path: Path, manifest_path: Path, map_id: int = 10) -> tuple[str | None, str | None]:
    if not payload_path.exists() and not manifest_path.exists():
        return None, None
    if not payload_path.is_file() or not manifest_path.is_file():
        raise ValueError("Refusing to replace incomplete official map battle output")
    payload = payload_path.read_bytes()
    manifest_bytes = manifest_path.read_bytes()
    try:
        manifest = json.loads(manifest_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError("Existing map battle manifest is not valid JSON") from exc
    output = manifest.get("output", {})
    if (manifest.get("format") != 1 or manifest.get("manifest") != VERSION
            or manifest.get("mapClassId") != map_id
            or output.get("path") != f"Assets/Official/World/Map/{map_id}/map{map_id}-battles.json"
            or output.get("sha256") != sha256(payload)):
        raise ValueError("Existing map battle output does not match its source manifest")
    return sha256(payload), sha256(manifest_bytes)


def build(apk: Apk | None = None, output: Path | None = None, map_id: int = 10) -> dict:
    if map_id not in (10, 11):
        raise ValueError("Only Maps 10 and 11 have pinned battle placement sources")
    apk = apk or Apk()
    output = Path(output or (ROOT / "unity/Assets/Official/World/Map" / str(map_id))).resolve()
    constructors = Constructors()

    placement_path = PLACEMENTS_SOURCE[1].format(map_id=map_id)
    placements_obj = apk.asset(PLACEMENTS_SOURCE[0], placement_path)
    placements_bytes = text_asset_bytes(placements_obj)
    placements = decode_entities(placements_bytes)
    placements_source = source_record(apk, placements_obj, placement_path, placements_bytes)

    component_stage_bindings = []
    map11_definition_source = None
    if map_id == 11:
        definition_bytes = MAP11_DEFINITION_PATH.read_bytes()
        definition_manifest_bytes = MAP11_DEFINITION_MANIFEST_PATH.read_bytes()
        definition_manifest = json.loads(definition_manifest_bytes.decode("utf-8"))
        if (definition_manifest.get("mapClassId") != 11
                or definition_manifest.get("output", {}).get("sha256") != sha256(definition_bytes)):
            raise ValueError("Pinned Map11 definition differs from its source manifest")
        definition = json.loads(definition_bytes.decode("utf-8"))
        component_stage_bindings = map11_component_stage_bindings(definition)
        component_stage_ids = {
            int(stage_id)
            for binding in component_stage_bindings
            for component in binding["StageComponents"]
            for stage_id in component["StageIds"]
        }
        map11_definition_source = {
            "path": MAP11_DEFINITION_PATH.relative_to(ROOT).as_posix(),
            "sha256": sha256(definition_bytes),
            "source_manifest": MAP11_DEFINITION_MANIFEST_PATH.relative_to(ROOT).as_posix(),
            "source_manifest_sha256": sha256(definition_manifest_bytes),
        }

    map_stage_obj = apk.asset(MAP_STAGE_TABLE[0], MAP_STAGE_TABLE[1])
    map_stage_bytes = text_asset_bytes(map_stage_obj)
    map_stage_rows = constructors.table(map_stage_bytes, MAP_STAGE_TABLE[2])
    stage_count = {10: 82, 11: 1803}[map_id]
    stage_ids = map_stage_ids(map_id, placements, map_stage_rows, stage_count)
    if map_id == 11:
        stage_ids.update(component_stage_ids)
    map_stage_source = source_record(apk, map_stage_obj, MAP_STAGE_TABLE[1], map_stage_bytes)

    stage_obj = apk.asset(STAGE_TABLE[0], STAGE_TABLE[1])
    stage_bytes = text_asset_bytes(stage_obj)
    source_stages = constructors.table(stage_bytes, STAGE_TABLE[2])
    stage_rows = [row for row in source_stages if row["Id"] in stage_ids]
    if stage_ids != {int(row["Id"]) for row in stage_rows}:
        raise ValueError(f"Map{map_id} references a stage absent from the pinned APK")
    stage_source = source_record(apk, stage_obj, STAGE_TABLE[1], stage_bytes)

    array_obj = apk.asset(ARRAY_TABLE[0], ARRAY_TABLE[1])
    array_bytes = text_asset_bytes(array_obj)
    array_data = constructors.table(array_bytes, ARRAY_TABLE[2])
    if not isinstance(array_data, dict) or not isinstance(array_data.get("InfoDict"), dict):
        raise ValueError("Stage monster array table schema differs from the official parser")
    all_arrays = array_data["InfoDict"]
    array_source = source_record(apk, array_obj, ARRAY_TABLE[1], array_bytes)

    skill_obj = apk.asset(SKILL_TABLE[0], SKILL_TABLE[1])
    skill_bytes = text_asset_bytes(skill_obj)
    skill_rows = constructors.table(skill_bytes, SKILL_TABLE[2])
    if not isinstance(skill_rows, list):
        raise ValueError("Official skill table schema differs from TbSkillInfo")
    skill_source = source_record(apk, skill_obj, SKILL_TABLE[1], skill_bytes)

    combat_rules, combat_rules_source = _read_verified_json(
        COMBAT_RULES_PATH, COMBAT_RULES_MANIFEST_PATH, "output_sha256")
    skill_rules, skill_rules_source = _read_verified_json(
        SKILL_RULES_PATH, SKILL_RULES_MANIFEST_PATH, "output_sha256")
    general_rules, general_rules_source = _read_verified_json(
        GENERAL_RULES_PATH, GENERAL_RULES_MANIFEST_PATH, "data_sha256.rules.json")
    property_ids, property_type_hash = _read_property_ids()

    scene_manifest = json.loads(BATTLE_SCENES_MANIFEST_PATH.read_text(encoding="utf-8"))
    scene_payload = BATTLE_SCENES_PATH.read_bytes()
    if scene_manifest.get("manifest") != VERSION or scene_manifest.get("output", {}).get("sha256") != sha256(scene_payload):
        raise ValueError("Official battle scene payload differs from its source manifest")
    all_scenes = json.loads(scene_payload.decode("utf-8"))["stages"]
    arrays, scenes = collect_battle_data(stage_rows, all_arrays, all_scenes)
    rosters = collect_monster_rosters(stage_rows, all_arrays, scenes, combat_rules, general_rules,
                                      skill_rows, skill_rules["entities"], property_ids,
                                      allow_unused_monster_ids=map_id == 11)

    parser_hash = sha256(PARSER_SOURCE_PATH.read_bytes())
    scene_manifest_hash = sha256(BATTLE_SCENES_MANIFEST_PATH.read_bytes())
    formula_sources = {}
    for name in ("BattleFormulaHandler.cs", "RolePropGroupInfo.cs", "LevelPropParser.cs",
                 "LevelPropBinaryReader.cs", "Formula.cs", "StageMonsterArrayInfoParser.cs",
                 "SkillInfoParser.cs", "TbSkillInfo.cs"):
        path = COMMON_SOURCE / name
        formula_sources[name] = {"path": path.relative_to(ROOT).as_posix(), "sha256": sha256(path.read_bytes())}
    formula_sources["PropType.cs"] = {
        "path": (COMMON_SOURCE / "PropType.cs").relative_to(ROOT).as_posix(),
        "sha256": property_type_hash,
    }
    payload = {
        "manifest": VERSION,
        "mapClassId": map_id,
        "stages": sorted(stage_rows, key=lambda row: int(row["Id"])),
        "monsterArrays": arrays,
        "battleScenes": scenes,
        "monsterRosters": rosters,
        **({"entityStageBindings": component_stage_bindings} if map_id == 11 else {}),
        "formationOrder": "StageMonsterArrayInfoParser.PostParse sorts Monsters by GroupId",
        "rosterRules": "StageMonsterArrayInfoParser.GenerateMonsters repeats each monster id for every position in its contiguous GroupId; MonsterGroupNo comes from StageInfo.",
    }
    payload_bytes = json_bytes(payload)
    manifest = {
        "format": 1,
        "manifest": VERSION,
        "mapClassId": map_id,
        "operation": "Read original Luban stage, formation and skill tables; preserve battle grids; expand official GroupId formations and calculate monster properties using CalcMonsterBattleProps.",
        "inputs": {
            "stage_table": stage_source,
            "monster_array_table": array_source,
            "skill_table": skill_source,
            "stage_monster_array_parser": {
                "path": PARSER_SOURCE_PATH.relative_to(ROOT).as_posix(),
                "sha256": parser_hash,
            },
            f"map{map_id}_placements": placements_source,
            f"map{map_id}_stage_index": map_stage_source,
            **({"map11_definition": map11_definition_source} if map_id == 11 else {}),
            "battle_scenes": {
                "path": BATTLE_SCENES_PATH.relative_to(ROOT).as_posix(),
                "sha256": sha256(scene_payload),
                "source_manifest": BATTLE_SCENES_MANIFEST_PATH.relative_to(ROOT).as_posix(),
                "source_manifest_sha256": scene_manifest_hash,
            },
            "battle_rules": combat_rules_source,
            "skill_rules": skill_rules_source,
            "general_rules": general_rules_source,
            "official_formula_sources": formula_sources,
        },
        "output": {
            "path": f"Assets/Official/World/Map/{map_id}/map{map_id}-battles.json",
            "size": len(payload_bytes),
            "sha256": sha256(payload_bytes),
        },
    }
    manifest_bytes = json_bytes(manifest)

    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    payload_path = output / f"map{map_id}-battles.json"
    manifest_path = output / "battle-source-manifest.json"
    payload_hash, manifest_hash = existing_output_hashes(payload_path, manifest_path, map_id)
    reference_hash = None
    reference_manifest_path = ROOT / f"assets/reference/official-map/map{map_id}-battles-source-manifest.json"
    if output.resolve() == (ROOT / "unity/Assets/Official/World/Map" / str(map_id)).resolve() and reference_manifest_path.exists():
        if not manifest_path.exists() or reference_manifest_path.read_bytes() != manifest_path.read_bytes():
            raise ValueError(f"Reference and Unity Map{map_id} battle manifests differ")
        reference_hash = sha256(reference_manifest_path.read_bytes())
    write_generated(payload_path, payload_bytes, payload_hash)
    write_generated(manifest_path, manifest_bytes, manifest_hash)
    if output.resolve().is_relative_to((ROOT / "unity").resolve()):
        write_meta(payload_path, "TextScriptImporter")
        write_meta(manifest_path, "TextScriptImporter")
    if output.resolve() == (ROOT / "unity/Assets/Official/World/Map" / str(map_id)).resolve():
        write_generated(reference_manifest_path, manifest_bytes, reference_hash)
    return manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map-id", type=int, choices=(10, 11), default=10)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    output = args.output or ROOT / "unity/Assets/Official/World/Map" / str(args.map_id)
    manifest = build(output=output, map_id=args.map_id)
    data = json.loads((output / f"map{args.map_id}-battles.json").read_text(encoding="utf-8"))
    print(f"Map{args.map_id} official battle stages:", len(data["stages"]))
    print(f"Map{args.map_id} monster arrays:", len(data["monsterArrays"]))
    print("Battle data SHA256:", manifest["output"]["sha256"])


if __name__ == "__main__":
    main()
