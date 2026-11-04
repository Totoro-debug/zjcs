"""Package the verified 884_156692 Map16 battle closure for the shared host."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_character_catalog import decode_level_props
from prepare_unity_map10_battle_data import (
    _read_property_ids,
    collect_monster_rosters,
    json_bytes,
    write_meta,
)

VERSION = "884_156692"
MAP_ID = 16
SOURCE = ROOT / "unity/Assets/Official/World/Map/16/map16-battles-source.json"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map16-battles-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map16/battle-runtime.json"
RECEIPT = ROOT / "assets/reference/official-map/map16-battle-runtime-source-manifest.json"
CODE = ROOT / ".tools/source-packages/current" / VERSION / "official-code"


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def record(path: Path) -> dict:
    raw = path.read_bytes()
    return {"path": path.relative_to(ROOT).as_posix(), "bytes": len(raw), "sha256": sha(raw)}


def table(apk: Apk, constructors: Constructors, name: str, kind: str) -> tuple[object, dict]:
    obj = apk.asset("config_luban", "/" + name + ".g.bytes")
    raw = obj.read().m_Script.encode("utf8", "surrogateescape")
    return constructors.table(raw, kind), dict(apk.provenance(
        obj), asset=name + ".g.bytes", payload_sha256=sha(raw), object_sha256=sha(obj.get_raw_data()))


def level_prop_payload(apk: Apk) -> tuple[bytes, dict]:
    bank = next(row for row in apk.source.manifest("WwiseBnk")["bundles"]
                if row["name"] == "wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile")
    packaged = "assets/yoo/WwiseBnk/" + bank["file"]
    if packaged in apk.available:
        raw = apk.archive.read(packaged)
        origin = {"origin": "apk", "entry": packaged}
    else:
        cached = apk.cloud.read("WwiseBnk", bank)
        if cached is None:
            retained = ROOT / ".tools/source-packages/WwiseBnk" / bank["name"]
            raw = retained.read_bytes()
            origin = {"origin": "verified_identical_retained_payload",
                      "path": retained.relative_to(ROOT).as_posix()}
        else:
            raw, origin = cached
    if len(raw) != bank["size"] or hashlib.md5(raw).hexdigest() != bank["hash"]:
        raise ValueError("Current Map16 level_prop payload hash mismatch")
    return raw, dict(origin, manifest=VERSION, bundle=bank, sha256=sha(raw))


def build() -> dict:
    source_receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    source = json.loads(SOURCE.read_bytes())
    if source_receipt.get("manifest") != VERSION or source_receipt.get("mapClassId") != MAP_ID:
        raise ValueError("Map16 battle source receipt does not match the selected version")
    if source_receipt.get("output", {}).get("sha256") != sha(SOURCE.read_bytes()):
        raise ValueError("Map16 battle source differs from its extraction receipt")
    if source.get("manifest") != VERSION or source.get("mapClassId") != MAP_ID:
        raise ValueError("Map16 battle source identity differs")

    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Map16 battle runtime requires the selected 884_156692 APK")
    constructors = Constructors(CODE)
    stages = source["stages"]
    arrays = {int(key): value for key, value in source["monsterArrays"].items()}
    scenes = source["battleScenes"]
    monster_groups, monster_source = table(apk, constructors, "monster_group", "TbMonsterGroupInfo")
    role_groups, role_source = table(apk, constructors, "role_prop_group", "TbRolePropGroupInfo")
    skill_rows, skill_source = table(apk, constructors, "skill", "TbSkillInfo")
    item_rows, item_source = table(apk, constructors, "item", "TbItemInfo")
    stage_ids = {int(row["Id"]) for row in stages}
    if len(stage_ids) != len(stages) or len(arrays) != 46 or len(scenes) != 9:
        raise ValueError("Map16 battle source closure counts differ")
    group_keys = {(int(monster), int(row["MonsterGroupNo"]))
                  for row in stages for wave in (row.get("WaveMonsterIds") or [])
                  for monster in (wave if isinstance(wave, list) else str(wave).split())}
    selected_monsters = [row for row in monster_groups
                         if (int(row["MonsterId"]), int(row["GroupNo"])) in group_keys]
    prop_ids = {int(row["PropCfg"]["PropGroupId"]) for row in selected_monsters}
    selected_roles = [row for row in role_groups if int(row["ID"]) in prop_ids]
    if {int(row["ID"]) for row in selected_roles} != prop_ids:
        raise ValueError("Map16 monster role property group closure is incomplete")
    wanted_level_props = {int(row["LevelPropID"]) for row in selected_roles}
    wanted_level_props.update(int(row["ExtraLevelPropID"]) for row in selected_roles
                              if row.get("ExtraLevelPropID"))
    level_raw, level_source = level_prop_payload(apk)
    props = decode_level_props(level_raw, wanted_level_props)
    property_ids, property_source_hash = _read_property_ids()
    combat = {"tables": {"monster_group": selected_monsters, "role_prop_group": selected_roles},
              "props": props}
    skill_entities = {str(int(row["EcEntityId"])): row for row in skill_rows
                      if int(row.get("EcEntityId", 0)) > 0}
    rosters = collect_monster_rosters(
        stages, arrays, scenes, combat, {"item": item_rows}, skill_rows,
        skill_entities, property_ids, allow_unused_monster_ids=True)
    payload = {
        "manifest": VERSION,
        "mapClassId": MAP_ID,
        "sourceSha256": sha(SOURCE.read_bytes()),
        "stages": stages,
        "monsterRosters": rosters,
        "battleScenes": [dict(GameId=int(key), **value)
                         for key, value in sorted(scenes.items(), key=lambda pair: int(pair[0]))],
        "entityStageBindings": source["entityStageBindings"],
    }
    content = json_bytes(payload)
    if OUTPUT.exists() and RECEIPT.exists():
        old = json.loads(RECEIPT.read_bytes())
        if old.get("outputs", [{}])[0].get("sha256") != sha(OUTPUT.read_bytes()):
            raise ValueError("Refusing to replace modified Map16 battle runtime output")
    elif OUTPUT.exists() or RECEIPT.exists():
        raise ValueError("Map16 battle runtime output and receipt must be created together")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(content)
    write_meta(OUTPUT, "TextScriptImporter")
    meta_path = OUTPUT.with_name(OUTPUT.name + ".meta")
    normalized_meta = b"\n".join(line.rstrip() for line in meta_path.read_bytes().splitlines()) + b"\n"
    if normalized_meta != meta_path.read_bytes():
        meta_path.write_bytes(normalized_meta)
    result = {
        "format": 1,
        "manifest": VERSION,
        "mapClassId": MAP_ID,
        "operation": "Expand the pinned 884_156692 Map16 stages, formations, official monster properties, skills, scenes and placement bindings for the shared offline battle host.",
        "inputs": [record(SOURCE), record(SOURCE_RECEIPT), monster_source, role_source,
                   skill_source, item_source, level_source,
                   {"path": (CODE / "common-source/Common/PropType.cs").relative_to(ROOT).as_posix(),
                    "sha256": property_source_hash}],
        "generator": record(Path(__file__)),
        "outputs": [record(OUTPUT), record(OUTPUT.with_name(OUTPUT.name + ".meta"))],
        "counts": {"stages": len(stages), "monsterRosters": len(rosters), "scenes": len(scenes),
                   "monsterGroups": len(selected_monsters),
                   "entityStageBindings": len(source["entityStageBindings"]),
                   "rosterUnits": sum(len(wave["Units"]) for roster in rosters for wave in roster["Waves"])},
        "pendingRuntimeBindings": ["Map16 mainline map host", "official monster visual assets"],
    }
    RECEIPT.write_bytes(json_bytes(result))
    print(json.dumps(result["counts"], ensure_ascii=False, sort_keys=True))
    return result


if __name__ == "__main__":
    build()
