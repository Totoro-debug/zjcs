"""Build weekly challenge battle data from the selected official APK."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack
from official_tables import Constructors
from prepare_unity_character_catalog import decode_level_props
from prepare_unity_map10_battle_data import (
    _read_property_ids,
    collect_battle_data,
    collect_monster_rosters,
    json_bytes,
    write_meta,
)


OUTPUT = ROOT / "unity/Assets/Game/Resources/WeeklyChallenges/battle-stages.json"
RECEIPT = ROOT / "assets/reference/weekly-challenges/battle-stages-source-manifest.json"
WEEKLY_TABLES = {
    "void_fissure": ("stage_voidfissure", "TbStagePartInfoVoidFissure"),
    "demon_battlefield": ("stage_demon_battlefield", "TbStagePartInfoDemonBattlefield"),
}


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def build() -> dict:
    apk = Apk.current()
    source_root = ROOT / ".tools/source-packages/current" / apk.version
    code_root = source_root / "official-code"
    formats = Formats(code_root)
    inputs = []

    def table(name: str, constructor: str):
        rows = tables.get(name)
        proof = table_inputs.get(name)
        if rows is None or proof is None:
            raise ValueError("Current official table has no matching decode proof: " + name)
        payload_path = source_root / "official-data" / (name + ".g.bytes")
        payload = payload_path.read_bytes()
        if len(payload) != proof["bytes"] or sha(payload) != proof["sha256"]:
            raise ValueError("Current official table differs from its decode proof: " + name)
        inputs.append({"path": payload_path.relative_to(ROOT).as_posix(), "sha256": sha(payload),
                       "manifest": apk.version, "asset": name + ".g.bytes",
                       "decoded_table": tables_path.relative_to(ROOT).as_posix()})
        return rows

    tables_path = source_root / "official-data/tables.json"
    tables_raw = tables_path.read_bytes()
    table_proof_path = ROOT / "assets/reference/official-map/current-resource-preparation-source-manifest.json"
    table_proof_raw = table_proof_path.read_bytes()
    table_proof = json.loads(table_proof_raw.decode("utf8"))
    table_inputs = {Path(value["path"]).name.removesuffix(".g.bytes"): value
                    for value in table_proof.get("luban_inputs", [])}
    if (table_proof.get("manifest") != apk.version
            or table_proof.get("apk_sha256") != apk.source.apk_sha256
            or table_proof.get("table_payload", {}).get("sha256") != sha(tables_raw)):
        raise ValueError("Current official table cache does not match its selected APK proof")
    tables = json.loads(tables_raw.decode("utf8"))
    inputs.extend([
        {"path": tables_path.relative_to(ROOT).as_posix(), "sha256": sha(tables_raw)},
        {"path": table_proof_path.relative_to(ROOT).as_posix(), "sha256": sha(table_proof_raw)},
    ])

    weekly_ids = set()
    activity_tables = {}
    for key, (name, constructor) in WEEKLY_TABLES.items():
        rows = table(name, constructor)
        activity_tables[key] = rows
        if key == "void_fissure":
            weekly_ids.update(int(row["Id"]) for row in rows)
    weekly_ids.add(4800)

    stages = table("stage", "TbStageInfo")
    stage_rows = [row for row in stages if int(row["Id"]) in weekly_ids]
    if weekly_ids != {int(row["Id"]) for row in stage_rows}:
        raise ValueError("Weekly challenge references a stage missing from the selected official stage table")
    stage_rows.sort(key=lambda row: int(row["Id"]))
    scene_ids = {int(row["GameId"]) for row in stage_rows}

    arrays_data = table("stage_monster_array", "TbStageMonsterArrayInfo")
    if not isinstance(arrays_data, dict) or not isinstance(arrays_data.get("InfoDict"), dict):
        raise ValueError("Official monster array table schema differs from TbStageMonsterArrayInfo")
    all_arrays = {int(key): value for key, value in arrays_data["InfoDict"].items()}

    skill_rows = table("skill", "TbSkillInfo")
    items = table("item", "TbItemInfo")
    monster_groups = table("monster_group", "TbMonsterGroupInfo")
    role_groups = table("role_prop_group", "TbRolePropGroupInfo")
    group_keys = {(int(monster), int(row["MonsterGroupNo"])) for row in stage_rows
                  for wave in (row.get("WaveMonsterIds") or []) for monster in wave}
    selected_monsters = [row for row in monster_groups
                         if (int(row["MonsterId"]), int(row["GroupNo"])) in group_keys]
    property_ids, property_source_hash = _read_property_ids()

    prop_groups = {int(row["PropCfg"]["PropGroupId"]) for row in selected_monsters}
    selected_roles = [row for row in role_groups if int(row["ID"]) in prop_groups]
    prop_ids = {int(row[key]) for row in selected_roles
                for key in ("LevelPropID", "ExtraLevelPropID") if row.get(key)}
    bank = next(row for row in apk.source.manifest("WwiseBnk")["bundles"]
                if row["name"] == "wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile")
    packaged = "assets/yoo/WwiseBnk/" + bank["file"]
    verified = apk.reader.read('WwiseBnk', bank["name"])
    level_raw, level_origin = verified.data, dict(verified.provenance)
    inputs.append({**level_origin, "manifest": apk.version, "bundle": bank, "sha256": sha(level_raw)})
    props = decode_level_props(level_raw, prop_ids)

    ec_bundle_name = "defaultpackage_assets_config_ec.bundle"
    ec_bundle = apk.bundle_records[ec_bundle_name]
    ec_verified = apk.reader.read("DefaultPackage", ec_bundle["name"])
    ec_raw, ec_source = ec_verified.data, dict(ec_verified.provenance)
    if ec_raw is None or len(ec_raw) != ec_bundle["size"] or hashlib.md5(ec_raw).hexdigest() != ec_bundle["hash"]:
        raise ValueError("Current official EC bundle is not cached or its package hash is invalid")
    loaded_ec = apk.env.load_file(apk.decode(ec_raw), name="weekly-config-ec-" + apk.version)
    ec_container = {path: pointer for asset in loaded_ec.files.values()
                    if hasattr(asset, "container") for path, pointer in asset.container.items()}
    scene_rows = {}
    for path, pointer in ec_container.items():
        if not re.search(r"/FightStage_part\d+\.g\.bytes$", path):
            continue
        obj = pointer.deref()
        payload = obj.read().m_Script.encode("utf8", "surrogateescape")
        for parent, identity, components in unpack(payload)[0]:
            if int(identity) not in scene_ids:
                continue
            if parent or str(identity) in scene_rows:
                raise ValueError("Official weekly battle scene is ambiguous: " + str(identity))
            scene_rows[str(identity)] = {
                kind.removeprefix("Common."): formats.component(kind, value)
                for kind, value in components
            }
        inputs.append({"manifest": apk.version, "bundle": ec_bundle_name,
                       "bundle_hash": ec_bundle["hash"], "bundle_sha256": ec_source["sha256"],
                       "asset": path, "payload_sha256": sha(payload)})
    if scene_ids != {int(value) for value in scene_rows}:
        missing = sorted(scene_ids - {int(value) for value in scene_rows})
        raise ValueError("Official weekly battle scene fragments are missing: " + repr(missing))

    selected_arrays, selected_scenes = collect_battle_data(stage_rows, all_arrays, scene_rows)
    skill_rules_path = ROOT / "unity/Assets/Official/Combat/skill-rules.json"
    skill_rules_raw = skill_rules_path.read_bytes()
    skill_rules = json.loads(skill_rules_raw.decode("utf8"))
    inputs.append({"path": skill_rules_path.relative_to(ROOT).as_posix(),
                   "sha256": sha(skill_rules_raw),
                   "usage": "Verify original active skill entities against the migrated combat catalog"})
    combat = {"tables": {"monster_group": selected_monsters, "role_prop_group": selected_roles},
              "props": props}
    rosters = collect_monster_rosters(stage_rows, all_arrays, selected_scenes, combat,
        {"item": items}, skill_rows, skill_rules["entities"], property_ids,
        allow_unused_monster_ids=False)

    scene_records = [{"GameId": int(key), **value}
                     for key, value in sorted(selected_scenes.items(), key=lambda item: int(item[0]))]
    payload = {
        "manifest": apk.version,
        "voidFissureStageIds": sorted(int(row["Id"]) for row in activity_tables["void_fissure"]),
        "demonBattlefieldStageIds": [4800],
        "stageIds": sorted(weekly_ids),
        "stages": stage_rows,
        "monsterArrays": selected_arrays,
        "battleScenes": scene_records,
        "monsterRosters": rosters,
    }
    encoded = json_bytes(payload)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(encoded)
    write_meta(OUTPUT, "TextScriptImporter")
    receipt = {
        "format": 1,
        "manifest": apk.version,
        "operation": "Decode the selected official weekly stage tables, EC fight-stage grid components, formations and monster rosters without changing their values.",
        "inputs": inputs,
        "tableProofSha256": sha(table_proof_raw),
        "propertyTypeSourceSha256": property_source_hash,
        "generator": {"path": Path(__file__).relative_to(ROOT).as_posix(),
                      "sha256": sha(Path(__file__).read_bytes())},
        "output": {"path": OUTPUT.relative_to(ROOT).as_posix(),
                   "sha256": sha(encoded), "size": len(encoded)},
        "counts": {"voidFissureStages": len(activity_tables["void_fissure"]),
                   "demonBattlefieldStages": len(activity_tables["demon_battlefield"]),
                   "selectedStages": len(stage_rows), "scenes": len(selected_scenes),
                   "monsterArrays": len(selected_arrays), "monsterRosters": len(rosters)},
    }
    receipt_raw = json_bytes(receipt)
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_bytes(receipt_raw)
    print(json.dumps(receipt["counts"], ensure_ascii=False))
    return payload


if __name__ == "__main__":
    build()
