"""Build selected-official NPC Adventure battle data from the active APK."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack
from prepare_unity_character_catalog import decode_level_props
from prepare_unity_map10_battle_data import (
    _read_property_ids,
    collect_battle_data,
    collect_monster_rosters,
    json_bytes,
    write_meta,
)


OUTPUT = ROOT / "unity/Assets/Game/Resources/NpcAdventure/battle-stages.json"
RESOURCE_RECEIPT = ROOT / "unity/Assets/Game/Resources/NpcAdventure/battle-stages-source-manifest.json"
SOURCE_RECEIPT = ROOT / "assets/reference/npc-adventure/battle-stages-source-manifest.json"
TABLE_CONSTRUCTORS = {
    "npc_adventure": "TbNpcAdventureInfo",
    "stage": "TbStageInfo",
    "stage_monster_array": "TbStageMonsterArrayInfo",
    "skill": "TbSkillInfo",
    "item": "TbItemInfo",
    "monster_group": "TbMonsterGroupInfo",
    "role_prop_group": "TbRolePropGroupInfo",
    "reward_rule": "TbRewardRuleInfo",
    "level_number": "TbLevelNumberInfo",
}
AUTHORIZED_MAP_GROUP = 17
EXPECTED_NODES = 720


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def source_tables(apk: Apk, source_root: Path) -> tuple[dict, list[dict]]:
    tables_path = source_root / "official-data/tables.json"
    raw = tables_path.read_bytes()
    proof_path = ROOT / "assets/reference/official-map/current-resource-preparation-source-manifest.json"
    proof_raw = proof_path.read_bytes()
    proof = json.loads(proof_raw.decode("utf-8"))
    if (proof.get("manifest") != apk.version or proof.get("apk_sha256") != apk.source.apk_sha256
            or proof.get("table_payload", {}).get("sha256") != sha(raw)):
        raise ValueError("Selected official table cache does not match its APK proof")
    rows_by_name = {Path(row["path"]).name.removesuffix(".g.bytes"): row
                    for row in proof.get("luban_inputs", [])}
    tables = json.loads(raw.decode("utf-8"))
    inputs = [
        {"path": tables_path.relative_to(ROOT).as_posix(), "sha256": sha(raw)},
        {"path": proof_path.relative_to(ROOT).as_posix(), "sha256": sha(proof_raw)},
    ]

    def table(name: str) -> list[dict] | dict:
        rows = tables.get(name)
        row_proof = rows_by_name.get(name)
        payload_path = source_root / "official-data" / (name + ".g.bytes")
        if rows is None or row_proof is None or not payload_path.is_file():
            raise ValueError("Selected official table lacks a matching decoded payload: " + name)
        payload = payload_path.read_bytes()
        if len(payload) != row_proof.get("bytes") or sha(payload) != row_proof.get("sha256"):
            raise ValueError("Selected official table payload differs from its proof: " + name)
        inputs.append({"path": payload_path.relative_to(ROOT).as_posix(),
                       "sha256": sha(payload), "manifest": apk.version,
                       "asset": name + ".g.bytes", "decoded_table": tables_path.relative_to(ROOT).as_posix()})
        return rows

    selected = {}
    for name, constructor in TABLE_CONSTRUCTORS.items():
        rows = table(name)
        if name == "stage_monster_array":
            if not isinstance(rows, dict) or not isinstance(rows.get("InfoDict"), dict):
                raise ValueError("Selected official stage monster array table schema changed")
        elif not isinstance(rows, list):
            raise ValueError("Selected official table is not a row list: " + name)
        selected[name] = rows
    return selected, inputs


def verify_rewards(stages: list[dict], tables: dict) -> tuple[list[dict], list[dict]]:
    rules_by_id: dict[int, list[dict]] = {}
    for row in tables["reward_rule"]:
        rules_by_id.setdefault(int(row["Id"]), []).append(row)
    item_ids = {int(row["ClassId"]) for row in tables["item"]}
    award_ids = sorted({int(value) for stage in stages for value in stage.get("Awards", [])})
    reward_rows = []
    level_ids = set()
    for rule_id in award_ids:
        rows = sorted(rules_by_id.get(rule_id, []), key=lambda value: int(value.get("Order", -1)))
        if len(rows) != 1:
            raise ValueError("NPC Adventure award rule is missing or has multiple rows: " + str(rule_id))
        row = rows[0]
        condition = row.get("Condition")
        ids = [int(value) for value in row.get("IdList") or []]
        weights = [int(value) for value in row.get("WeightList") or []]
        counts = [float(value) for value in row.get("CountList") or []]
        sub_rank_ids = [int(value) for value in row.get("SubRankCountId") or []]
        if condition not in (None, {}) or not ids or len(ids) != len(weights) or len(ids) != len(counts):
            raise ValueError("NPC Adventure award rule needs unsupported condition or mismatched arrays: " + str(rule_id))
        if any(value <= 0 or value not in item_ids for value in ids):
            raise ValueError("NPC Adventure award references a missing or invalid item: " + str(rule_id))
        if any(value <= 0 for value in weights) or any(value <= 0 for value in counts):
            raise ValueError("NPC Adventure award rule has non-positive weight or count: " + str(rule_id))
        level_count_id = int(row.get("LevelCountId") or 0)
        if level_count_id > 0:
            level_ids.add(level_count_id)
        level_ids.update(value for value in sub_rank_ids if value > 0)
        reward_rows.append({
            "Id": rule_id, "Order": int(row.get("Order", -1)), "Percent": float(row["Percent"]),
            "IdList": ids, "WeightList": weights, "CountList": counts,
            "LevelCountId": level_count_id, "SubRankCountId": sub_rank_ids,
        })
    number_rows = {int(row["Id"]): [float(value) for value in row["Nums"]]
                   for row in tables["level_number"]}
    missing = level_ids.difference(number_rows)
    if missing:
        raise ValueError("NPC Adventure reward multiplier is missing: " + repr(sorted(missing)))
    levels = [{"id": key, "values": number_rows[key]} for key in sorted(level_ids)]
    return reward_rows, levels


def build() -> dict:
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("NPC Adventure data must follow the selected source manifest: " + apk.version)
    source_root = ROOT / ".tools/source-packages/current" / apk.version
    tables, inputs = source_tables(apk, source_root)

    nodes = [row for row in tables["npc_adventure"]
             if int(row["MapGroupId"]) <= AUTHORIZED_MAP_GROUP]
    nodes.sort(key=lambda row: (int(row["MapGroupId"]), int(row["RegionId"]), int(row["NodeId"])))
    if len(nodes) != EXPECTED_NODES or (int(nodes[0]["MapGroupId"]), int(nodes[0]["RegionId"]),
            int(nodes[0]["NodeId"])) != (11, 1, 1) or (int(nodes[-1]["MapGroupId"]),
            int(nodes[-1]["RegionId"]), int(nodes[-1]["NodeId"])) != (17, 8, 20):
        raise ValueError("Selected NPC Adventure authorization must contain the confirmed 720-node range")

    stage_by_id = {int(row["Id"]): row for row in tables["stage"]}
    stage_ids = {int(row["StageId"]) for row in nodes}
    if len(stage_ids) != EXPECTED_NODES or not stage_ids.issubset(stage_by_id):
        raise ValueError("Selected NPC Adventure nodes reference missing or duplicate official StageInfo rows")
    stages = [stage_by_id[value] for value in sorted(stage_ids)]
    if any(int(row["Type"]) != 41 for row in stages):
        raise ValueError("NPC Adventure stage type differs from the official enum value")

    array_table = tables["stage_monster_array"]
    arrays = {int(key): value for key, value in array_table["InfoDict"].items()}
    game_ids = {int(row["GameId"]) for row in stages}
    ec_bundle_name = "defaultpackage_assets_config_ec.bundle"
    ec_bundle = apk.bundle_records[ec_bundle_name]
    verified = apk.reader.read("DefaultPackage", ec_bundle["name"])
    ec_raw = verified.data
    ec_source = dict(verified.provenance)
    if ec_raw is None or len(ec_raw) != ec_bundle["size"] or hashlib.md5(ec_raw).hexdigest() != ec_bundle["hash"]:
        raise ValueError("Selected official EC bundle is unavailable or has an invalid package hash")
    formats = Formats(source_root / "official-code")
    loaded_ec = apk.env.load_file(apk.decode(ec_raw), name="npc-adventure-battles-" + apk.version)
    ec_container = {path: pointer for asset in loaded_ec.files.values()
                    if hasattr(asset, "container") for path, pointer in asset.container.items()}
    scene_rows: dict[str, dict] = {}
    for path, pointer in ec_container.items():
        if not re.search(r"/FightStage_part\d+\.g\.bytes$", path):
            continue
        payload = pointer.deref().read().m_Script.encode("utf-8", "surrogateescape")
        for parent, identity, components in unpack(payload)[0]:
            if int(identity) not in game_ids:
                continue
            if parent or str(identity) in scene_rows:
                raise ValueError("Selected official NPC Adventure battle scene is ambiguous: " + str(identity))
            scene_rows[str(identity)] = {
                kind.removeprefix("Common."): formats.component(kind, value)
                for kind, value in components
            }
        inputs.append({"manifest": apk.version, "bundle": ec_bundle_name,
                       "bundle_hash": ec_bundle["hash"], "bundle_sha256": ec_source.get("raw_sha256", ""),
                       "asset": path, "payload_sha256": sha(payload)})
    if game_ids != {int(value) for value in scene_rows}:
        raise ValueError("Selected official NPC Adventure scene fragments are missing: "
                         + repr(sorted(game_ids - {int(value) for value in scene_rows})))

    selected_arrays, selected_scenes = collect_battle_data(stages, arrays, scene_rows)
    skill_rules_path = ROOT / "unity/Assets/Official/Combat/skill-rules.json"
    skill_rules_raw = skill_rules_path.read_bytes()
    skill_rules = json.loads(skill_rules_raw.decode("utf-8"))
    skill_manifest_path = ROOT / "unity/Assets/Official/Combat/skill-rules-source-manifest.json"
    skill_manifest_raw = skill_manifest_path.read_bytes()
    skill_manifest = json.loads(skill_manifest_raw.decode("utf-8"))
    if skill_manifest.get("manifest") != "854_156383":
        raise ValueError("NPC Adventure battle entity dependency has an unexpected combat source")
    inputs.extend([
        {"path": skill_rules_path.relative_to(ROOT).as_posix(), "sha256": sha(skill_rules_raw),
         "purpose": "Battle runtime entity bindings used by the shared official combat engine"},
        {"path": skill_manifest_path.relative_to(ROOT).as_posix(), "sha256": sha(skill_manifest_raw)},
    ])

    group_keys = {(monster, int(stage["MonsterGroupNo"])) for stage in stages for wave in
                  (stage.get("WaveMonsterIds") or []) for monster in wave}
    selected_monsters = [row for row in tables["monster_group"]
                         if (int(row["MonsterId"]), int(row["GroupNo"])) in group_keys]
    if {(int(row["MonsterId"]), int(row["GroupNo"])) for row in selected_monsters} != group_keys:
        raise ValueError("Selected official NPC Adventure monster group data is incomplete")
    property_ids, property_source_hash = _read_property_ids()
    prop_groups = {int(row["PropCfg"]["PropGroupId"]) for row in selected_monsters}
    selected_roles = [row for row in tables["role_prop_group"] if int(row["ID"]) in prop_groups]
    if prop_groups - {int(row["ID"]) for row in selected_roles}:
        raise ValueError("Selected official NPC Adventure role property groups are incomplete")
    prop_ids = {int(row[key]) for row in selected_roles
                for key in ("LevelPropID", "ExtraLevelPropID") if row.get(key)}
    bank = next(row for row in apk.source.manifest("WwiseBnk")["bundles"]
                if row["name"] == "wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile")
    level_raw = apk.reader.read("WwiseBnk", bank["name"]).data
    if level_raw is None:
        raise ValueError("Selected official level property rawfile is not locally available")
    inputs.append({"manifest": apk.version, "bundle": bank, "sha256": sha(level_raw),
                   "source": dict(apk.reader.read("WwiseBnk", bank["name"]).provenance)})
    props = decode_level_props(level_raw, prop_ids)
    combat = {"tables": {"monster_group": selected_monsters, "role_prop_group": selected_roles},
              "props": props}
    rosters = collect_monster_rosters(stages, arrays, selected_scenes, combat,
        {"item": tables["item"]}, tables["skill"], skill_rules["entities"], property_ids,
        allow_unused_monster_ids=True)
    reward_rules, level_numbers = verify_rewards(stages, tables)
    scene_records = [{"GameId": int(key), **value}
                     for key, value in sorted(selected_scenes.items(), key=lambda item: int(item[0]))]
    unused_monster_ids = 0
    for stage in stages:
        for wave_index, wave_monsters in enumerate(stage.get("WaveMonsterIds") or []):
            array_id = int(stage["WaveArrayIds"][wave_index])
            formations = arrays[array_id]["Monsters"]
            groups = len({int(row["GroupId"]) for row in formations})
            if groups > len(wave_monsters):
                raise ValueError("Official monster formation exceeds its stage roster: " + str(stage["Id"]))
            unused_monster_ids += len(wave_monsters) - groups

    payload = {
        "manifest": apk.version,
        "authorizedNodeCount": len(nodes),
        "stageIds": sorted(stage_ids),
        "stages": stages,
        "monsterArrays": selected_arrays,
        "battleScenes": scene_records,
        "monsterRosters": rosters,
        "rewardRules": reward_rules,
        "levelNumbers": level_numbers,
        "unusedMonsterIds": unused_monster_ids,
    }
    encoded = json_bytes(payload)
    old_source_receipt = json.loads(SOURCE_RECEIPT.read_text(encoding="utf-8")) if SOURCE_RECEIPT.is_file() else None
    if OUTPUT.is_file():
        old_hash = None if old_source_receipt is None else old_source_receipt.get("output", {}).get("sha256")
        if old_hash != sha(OUTPUT.read_bytes()):
            raise ValueError("Refusing to overwrite NPC Adventure battle data changed outside this generator")
    counts = {"nodes": len(nodes), "stages": len(stages), "scenes": len(scene_records),
              "monsterArrays": len(selected_arrays), "monsterRosters": len(rosters),
              "rewardRules": len(reward_rules), "levelNumbers": len(level_numbers),
              "unusedMonsterIds": unused_monster_ids}
    receipt = {
        "format": 1,
        "manifest": apk.version,
        "authorization": {"first": [11, 1, 1], "last": [17, 8, 20], "nodeCount": len(nodes)},
        "operation": "Decode selected official NPC Adventure nodes, StageInfo, formations, EC battle scenes, monster rosters and configured stage.Awards without changing values.",
        "inputs": inputs,
        "tableProofSha256": sha((ROOT / "assets/reference/official-map/current-resource-preparation-source-manifest.json").read_bytes()),
        "propertyTypeSourceSha256": property_source_hash,
        "generator": {"path": Path(__file__).relative_to(ROOT).as_posix(), "sha256": sha(Path(__file__).read_bytes())},
        "output": {"path": OUTPUT.relative_to(ROOT).as_posix(), "sha256": sha(encoded), "size": len(encoded)},
        "counts": counts,
    }
    receipt_raw = json_bytes(receipt)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(encoded)
    write_meta(OUTPUT, "TextScriptImporter")
    RESOURCE_RECEIPT.write_bytes(receipt_raw)
    write_meta(RESOURCE_RECEIPT, "TextScriptImporter")
    SOURCE_RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    SOURCE_RECEIPT.write_bytes(receipt_raw)
    print(json.dumps(counts, ensure_ascii=False))
    return receipt


if __name__ == "__main__":
    build()
