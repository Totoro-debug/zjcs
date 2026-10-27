"""Build Map14 battles from the selected current official tables and EC fragments."""
from __future__ import annotations
import hashlib
import json
import re
import inspect
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack
from official_tables import Constructors
from prepare_unity_character_catalog import decode_level_props
from prepare_unity_map10_battle_data import (map_stage_ids, collect_battle_data,
    collect_monster_rosters, _read_property_ids, json_bytes, write_meta)

VERSION = "871_156598"
SOURCE = ROOT / "unity/Assets/Official/World/Map/14/Source"
OUTPUT = SOURCE.parent / "map14-battles.json"
RECEIPT = ROOT / "assets/reference/official-map/map14-battles-source-manifest.json"
CODE = ROOT / ".tools/source-packages/current" / VERSION / "official-code"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Map14 battle source version has changed; review the new source first")
    constructors, formats = Constructors(CODE), Formats(CODE)
    source_receipt_path = ROOT / "assets/reference/official-map/map14-source-manifest.json"
    source_receipt = json.loads(source_receipt_path.read_bytes())
    if source_receipt["manifest"] != VERSION or source_receipt["mapClassId"] != 14:
        raise ValueError("Map14 decoded source version differs")
    source_records = {row["path"]: row for row in source_receipt["outputs"]}
    inputs = []

    def verified_source(name):
        path = SOURCE / name
        raw = path.read_bytes()
        if source_records[path.relative_to(ROOT).as_posix()]["sha256"] != sha(raw):
            raise ValueError("Map14 decoded source changed: " + name)
        inputs.append({"path": path.relative_to(ROOT).as_posix(), "sha256": sha(raw)})
        return json.loads(raw)

    def table(name, kind, bundle="config_luban"):
        obj = apk.asset(bundle, "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        inputs.append(dict(apk.provenance(obj), asset=name + ".g.bytes",
                           payload_sha256=sha(raw), object_sha256=sha(obj.get_raw_data())))
        return constructors.table(raw, kind)

    placements = verified_source("placement.json")
    local = verified_source("entities.json")
    templates = verified_source("templates.json")
    tables = verified_source("tables.json")
    stories = verified_source("stories.json")
    classes = {int(row["id"]): int(row["entity"]) for row in placements["entities"]}
    stages = map_stage_ids(14, placements, tables["map_stage"], 2961)
    component_bindings = []
    for key, components in local.items():
        placement = int(key)
        entity_template = templates[str(classes[placement])]
        bindings = []
        for name, component in components.items():
            if not isinstance(component, dict):
                continue
            if name == "LoStageBehStageComponent" and not any(
                    key.startswith("StageBehStage") for key in entity_template):
                continue
            ids = component.get("StageIds") or []
            if component.get("StageId"):
                ids = list(ids) + [component["StageId"]]
            if ids:
                if any(not isinstance(value, int) or value <= 0 for value in ids):
                    raise ValueError("Invalid original stage binding: " + str(placement))
                stages.update(ids)
                bindings.append({"Component": name, "StageIds": ids})
        if bindings:
            component_bindings.append({"PlacementId": placement, "StageComponents": bindings})
    groups = [row for row in tables["Map/map_entity_group"] if row["Group"] == 2000
              and row["FilterCondition"].get("MapClassId") == 14]
    if len(groups) != 1:
        raise ValueError("Map14 witch summon group is ambiguous")
    for extra in groups[0]["ExtraDataInfos"]:
        if extra["_type"] != "MapEntityGroupInfoExtraStageIdsData":
            raise ValueError("Unsupported original witch stage binding")
        stages.update(value for group in extra["StageIdsList"] for value in group)
    story_bindings = []
    for key, story in stories.items():
        for node in story["nodes"].values():
            if node["_type"] == "StoryStartStageNode":
                stages.add(int(node["StageId"]))
                story_bindings.append({"Story": key, "NodeId": int(node["NodeId"]),
                                       "StageId": int(node["StageId"]),
                                       "UsePlayerLevel": bool(node["IsUsePlayerLevelAsStageLevel"])})
    all_stages = table("stage", "TbStageInfo")
    rows = [row for row in all_stages if int(row["Id"]) in stages]
    if stages != {int(row["Id"]) for row in rows}:
        raise ValueError("Missing original stages: " + str(stages - {int(row["Id"]) for row in rows}))
    arrays = table("stage_monster_array", "TbStageMonsterArrayInfo")["InfoDict"]
    skill_rows = table("skill", "TbSkillInfo")
    items = table("item", "TbItemInfo")
    monsters = table("monster_group", "TbMonsterGroupInfo")
    role_groups = table("role_prop_group", "TbRolePropGroupInfo")
    game_ids = {int(row["GameId"]) for row in rows}
    scenes = {}
    for path, pointer in apk.container("config_ec"):
        if not re.search(r"/FightStage_part\d+\.g\.bytes$", path):
            continue
        obj = pointer.deref()
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        for parent, identity, components in unpack(raw)[0]:
            if identity not in game_ids:
                continue
            if parent or str(identity) in scenes:
                raise ValueError("Ambiguous original battle scene: " + str(identity))
            scenes[str(identity)] = {typ.removeprefix("Common."): formats.component(typ, value)
                                     for typ, value in components}
        inputs.append(dict(apk.provenance(obj), asset=path, payload_sha256=sha(raw)))
    selected_arrays, scenes = collect_battle_data(rows, arrays, scenes)
    group_keys = {(int(monster), int(row["MonsterGroupNo"])) for row in rows
                  for wave in (row.get("WaveMonsterIds") or []) for monster in wave}
    selected_monsters = [row for row in monsters if (int(row["MonsterId"]), int(row["GroupNo"])) in group_keys]
    prop_groups = {int(row["PropCfg"]["PropGroupId"]) for row in selected_monsters}
    selected_roles = [row for row in role_groups if int(row["ID"]) in prop_groups]
    prop_ids = {int(row[name]) for row in selected_roles for name in ("LevelPropID", "ExtraLevelPropID") if row.get(name)}
    bank = next(row for row in apk.source.manifest("WwiseBnk")["bundles"]
                if row["name"] == "wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile")
    packaged = "assets/yoo/WwiseBnk/" + bank["file"]
    if packaged in apk.available:
        level_raw = apk.archive.read(packaged)
        origin = {"origin": "apk", "entry": packaged}
    else:
        cached = apk.cloud.read("WwiseBnk", bank)
        if cached is None:
            historic = ROOT / ".tools/source-packages/WwiseBnk" / bank["name"]
            level_raw = historic.read_bytes()
            origin = {"origin": "verified_identical_retained_payload", "path": historic.relative_to(ROOT).as_posix()}
        else:
            level_raw, origin = cached
    if len(level_raw) != bank["size"] or hashlib.md5(level_raw).hexdigest() != bank["hash"]:
        raise ValueError("Current level_prop payload hash mismatch")
    inputs.append(dict(origin, manifest=VERSION, bundle=bank, sha256=sha(level_raw)))
    props = decode_level_props(level_raw, prop_ids)
    property_ids, _ = _read_property_ids()
    formula_sources = []
    for name in ("BattleFormulaHandler.cs", "RolePropGroupInfo.cs", "LevelPropParser.cs", "LevelPropBinaryReader.cs",
                 "Formula.cs", "StageMonsterArrayInfoParser.cs", "SkillInfoParser.cs", "TbSkillInfo.cs", "PropType.cs"):
        current = CODE / "common-source/Common" / name
        baseline = ROOT / ".tools/source-packages/official-code/common-source/Common" / name
        if current.read_bytes() != baseline.read_bytes():
            raise ValueError("Shared formula port requires source review: " + name)
        formula_sources.append({"path": current.relative_to(ROOT).as_posix(), "sha256": sha(current.read_bytes())})
    skill_path = ROOT / "assets/official/skill-rules.json"
    skill_catalog = json.loads(skill_path.read_bytes())
    inputs.append({"path": skill_path.relative_to(ROOT).as_posix(), "sha256": sha(skill_path.read_bytes()),
                   "usage": "Verify each original active skill EC identity exists in the installed combat catalog"})
    combat = {"tables": {"monster_group": selected_monsters, "role_prop_group": selected_roles}, "props": props}
    rosters = collect_monster_rosters(rows, arrays, scenes, combat, {"item": items}, skill_rows,
                                      skill_catalog["entities"], property_ids, allow_unused_monster_ids=True)
    result = {"manifest": VERSION, "mapClassId": 14, "stages": sorted(rows, key=lambda row: int(row["Id"])),
              "monsterArrays": selected_arrays, "battleScenes": scenes, "monsterRosters": rosters,
              "entityStageBindings": component_bindings, "storyStageBindings": story_bindings}
    raw = json_bytes(result)
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    if OUTPUT.exists() and previous is None:
        raise ValueError("Refusing to replace unrecorded Map14 battle output")
    if OUTPUT.exists() and previous["output"]["sha256"] != sha(OUTPUT.read_bytes()):
        raise ValueError("Map14 battle output was modified outside its generator")
    receipt = {"format": 1, "manifest": VERSION, "mapClassId": 14,
               "operation": "Decode current official Luban tables and fight-stage EC fragments, expand original formations and reuse byte-identical official formula implementations.",
               "inputs": inputs, "formulaSources": formula_sources,
               "generator": {"path": Path(__file__).relative_to(ROOT).as_posix(), "sha256": sha(Path(__file__).read_bytes())},
               "sharedPorts": [{"symbol": function.__name__, "module": "scripts/prepare_unity_map10_battle_data.py",
                                "source_sha256_lf": sha(inspect.getsource(function).replace("\r\n", "\n").encode())}
                               for function in (map_stage_ids, collect_battle_data, collect_monster_rosters, _read_property_ids)],
               "constructorSources": [{"path": (constructors.source_root / (name + ".cs")).relative_to(ROOT).as_posix(),
                                       "sha256": sha((constructors.source_root / (name + ".cs")).read_bytes())}
                                      for name in constructors.sources if (constructors.source_root / (name + ".cs")).is_file()],
               "formatterSources": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path.read_bytes())}
                                    for path in formats.sources.values()],
               "output": {"path": OUTPUT.relative_to(ROOT).as_posix(), "sha256": sha(raw), "size": len(raw)},
               "counts": {"stages": len(rows), "monsterRosters": len(rosters), "scenes": len(scenes),
                          "arrays": len(selected_arrays), "entityStageBindings": len(component_bindings),
                          "storyStages": len(story_bindings), "monsterGroups": len(selected_monsters)}}
    receipt_raw = json_bytes(receipt)
    OUTPUT.write_bytes(raw)
    write_meta(OUTPUT, "TextScriptImporter")
    RECEIPT.write_bytes(receipt_raw)
    print(json.dumps(receipt["counts"], ensure_ascii=False))
    return result


if __name__ == "__main__":
    build()
