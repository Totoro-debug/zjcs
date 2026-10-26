"""Export the original mountain story closure and combat inputs, without inventing server rules."""
from __future__ import annotations

import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_map11_runtime_data import build_story_node, build_witch_results, sha256, write_json
from prepare_unity_map10_entities import write_meta


MAP_ROOT = ROOT / "unity/Assets/Official/World/Map/12"
OUTPUT = MAP_ROOT / "Resources/Map12"
REFERENCE = ROOT / "assets/reference/official-map/map12-runtime-source-manifest.json"


def load_verified(path, manifest_path, expected_map=None):
    receipt = json.loads(manifest_path.read_text(encoding="utf-8"))
    if receipt.get("manifest", "854_156383") != "854_156383" \
            or (expected_map is not None and receipt.get("mapClassId") != expected_map) \
            or receipt.get("output", {}).get("sha256") != sha256(path):
        raise ValueError("Source payload differs from its receipt: " + str(path))
    return json.loads(path.read_text(encoding="utf-8"))


def parse_terminal(params):
    # This is the original non-JSON inline object grammar used by the EC data.
    value = re.sub(r"([A-Za-z][A-Za-z0-9_]*):", r'"\1":', params)
    parsed = json.loads(value)
    if not isinstance(parsed, dict):
        raise ValueError("Puzzle terminal must contain an object")
    return parsed


def puzzle_rules(definition, catalog, templates, mover_rows):
    components = {r["EntityId"]: json.loads(r["Json"]) for r in definition["EntityComponents"]}
    movers = {r["Id"]: r["GroundTypes"] for r in mover_rows}
    result = []
    # Preserve every original entity's component ordering and original result
    # indices. The runtime selects the puzzle entities; conversion does not
    # remove dependent rewards, obstacles or inactive spawns.
    for entity in catalog["entities"]:
        local = components[entity["placementId"]]
        template = templates[str(entity["entityClassId"])]
        terminal = local.get("LoEntityPuzzleTerminalComponent")
        terminal_data = parse_terminal(terminal["Params"]) if terminal else {}
        params = json.loads(entity["lockParamsJson"])
        conditions = [{"PlacementId": int(k), "State": int(v)}
                      for k, v in params.get("EntityStateDict", {}).items()]
        if not conditions and entity["lockType"] in (5, 7):
            conditions = [{"PlacementId": int(v), "State": 0} for v in entity["lockConditionEntityIds"]]
        transform = template.get("GridTransformComponent", {})
        grid = local.get("LoGridTransformComponent", {})
        mover = template.get("GridMoverComponent", {})
        ground_types = movers.get(mover.get("WalkableGroundTypeKey"), [])
        pushed = local.get("LoStageResultEntityPushedComponent", {})
        result.append({
            "PlacementId": entity["placementId"], "EntityClassId": entity["entityClassId"],
            "InitialState": entity["state"], "GridX": entity["grid"][0], "GridY": entity["grid"][1],
            "InitiallySpawned": entity["active"], "LockType": entity["lockType"],
            "LockConditions": conditions,
            "PushBoxDetectorIds": params.get("LoOverlapDetectorEntityIds", []),
            "PushBoxIds": params.get("LoBoxEntityIds", []),
            "HasPushResult": "StageResultEntityPushedComponent" in template,
            "MoveMask": sum(1 << ground_type for ground_type in ground_types),
            "MovableRect": pushed.get("MovableRect"),
            "InitialDirection": grid.get("Dir", 0), "InitialFlipX": grid.get("FlipX", False),
            "Body": transform.get("BodyRangeInfos", []),
            "RealCenter": transform.get("RealCenter", [0, 0]),
            "ConditionEntityInactiveInUnlock": entity["conditionEntityInactiveInUnlock"],
            "ConditionEntityNeverDestroy": entity["conditionEntityNeverDestroy"],
            "TerminalLockIds": terminal_data.get("LockLoEntityIds", []),
            "TerminalPuzzleType": terminal["MapWorldPuzzleType"] if terminal else 0,
            "TerminalRemoveIds": terminal_data.get("NeedRemoveLoEntityIds", []),
            "TerminalBoxIds": terminal_data.get("BoxLoEntityIds", []),
            "ResetTransferActionId": terminal_data.get("ResetTransferActionId", 0),
            "TerminalJson": json.dumps(terminal, ensure_ascii=False, separators=(",", ":")) if terminal else "",
            "HasOverlapDetector": "CharacterPositionOverlapDetectorComponent" in template,
            "OverlapDetector": template.get("CharacterPositionOverlapDetectorComponent"),
            "Stage": template.get("WorldStageComponent"),
            "SelfStates": template.get("StageResultSelfStateUpdateComponent", {}).get("InfoList", []),
            "SpawnIds": local.get("LoStageResultLoEntitySpawnComponent", {}).get("LoEntityIds", []),
            "OtherStates": local.get("LoStageResultStateUpdateComponent", {}).get("DataList") or [],
            "OfflineIgnoredMissingStateTargets": [],
            "Notify": local.get("LoStageResultNotifyCompleteComponent", {}).get("Configs", []),
        })
    ids = {r["PlacementId"] for r in result}
    for rule in result:
        referenced = (rule["SpawnIds"] + rule["TerminalLockIds"] + rule["TerminalRemoveIds"]
                      + rule["TerminalBoxIds"] + rule["PushBoxDetectorIds"] + rule["PushBoxIds"])
        referenced += [r["PlacementId"] for r in rule["LockConditions"]]
        referenced += [r["LoEntId"] for group in rule["OtherStates"] for r in group.get("StageDatas", [])]
        if set(referenced) - ids:
            raise ValueError(f"Unresolved Map12 puzzle targets for {rule['PlacementId']}: {set(referenced) - ids}")
    if sum(bool(r["TerminalJson"]) for r in result) != 11:
        raise ValueError("Expected all eleven original mountain puzzle terminals")
    return result


def build():
    definition = load_verified(MAP_ROOT / "map12.json", MAP_ROOT / "source-manifest.json", 12)
    catalog = load_verified(MAP_ROOT / "map12-entities.json", MAP_ROOT / "entity-source-manifest.json", 12)
    battles = load_verified(MAP_ROOT / "map12-battles.json", MAP_ROOT / "battle-source-manifest.json", 12)
    adventure_path = ROOT / "assets/official/adventure.json"
    adventure = json.loads(adventure_path.read_text(encoding="utf-8"))
    definition_receipt = json.loads((MAP_ROOT / "source-manifest.json").read_text(encoding="utf-8"))
    expected_adventure = next(r["sha256"] for r in definition_receipt["inputs"]
                              if r["path"] == "assets/official/adventure.json")
    if sha256(adventure_path) != expected_adventure:
        raise ValueError("Adventure tables changed after the mountain definition was prepared")
    tables = adventure["tables"]
    template_path = ROOT / "assets/official/map-entities.json"
    template_receipt_path = ROOT / "assets/reference/official-map/entities-source-manifest.json"
    template_receipt = json.loads(template_receipt_path.read_text(encoding="utf-8"))
    if template_receipt["output_sha256"] != sha256(template_path):
        raise ValueError("Entity templates differ from their official source receipt")
    templates = json.loads(template_path.read_text(encoding="utf-8"))["templates"]
    class_ids = sorted({r["entityClassId"] for r in catalog["entities"]})
    if len(class_ids) != 135 or any(str(i) not in templates for i in class_ids):
        raise ValueError("All 135 mountain EC templates are required")
    entity_templates = [{"EntityClassId": i, "Components": [
        {"Type": name, "Json": json.dumps(value, ensure_ascii=False, separators=(",", ":"))}
        for name, value in templates[str(i)].items()]} for i in class_ids]

    logic = [r for r in tables["Map/map_logic_entity_tag"] if r["MapClass"] == 12]
    tags = {(f"entity_{r['EntityClass']}_name", r["Tag"]) for r in logic}
    tags.update((f"entity_{i}_name", "") for i in class_ids)
    talks = [r for r in tables["Map/map_talk"] if (r["EntityId"], r["Tag"]) in tags]
    primary = list(dict.fromkeys(r["TalkId"] for r in talks if r.get("TalkId")))
    if len(primary) != 49 or len(logic) != 35:
        raise ValueError("Mountain MapTalk or logic-tag closure changed")
    playable_path = ROOT / "assets/official/map-playables.json"
    playable_source = json.loads(playable_path.read_text(encoding="utf-8"))
    playable_receipt = json.loads((ROOT / "assets/reference/official-map/playables-source-manifest.json").read_text(encoding="utf-8"))
    if playable_receipt["outputs"]["map-playables.json"] != sha256(playable_path):
        raise ValueError("Official playable registry differs from its source receipt")
    story_configs = {r["StoryName"]: r for r in tables["story"]}
    records, subgraphs, seen = [], [], set()
    queue = [(name, False) for name in primary]
    option_keys, roles, fixed_ids, playable_ids = set(), set(), set(), set()
    while queue:
        name, subgraph = queue.pop(0)
        identity = (name, subgraph)
        if identity in seen:
            continue
        seen.add(identity)
        graph = adventure["stories"][name]
        groups = [{"Id": int(key), "Lines": lines} for key, lines in sorted(
            adventure["dialogues"].get(name, {}).items(), key=lambda p: int(p[0]))]
        nodes = [build_story_node(name, key, node, playable_source["scenes"], option_keys)
                 for key, node in sorted(graph["nodes"].items(), key=lambda p: int(p[0]))]
        starts = [n for n in nodes if n["type"] == "StartStoryNode"]
        if len(starts) != 1:
            raise ValueError("Story needs one original start node: " + name)
        record = {"key": name, "maxCount": story_configs.get(name, {}).get("MaxCount", -1),
                  "entryNodeId": starts[0]["id"], "graphJson": json.dumps(graph, ensure_ascii=False, separators=(",", ":")),
                  "nodes": nodes, "dialogueGroups": groups}
        (subgraphs if subgraph else records).append(record)
        roles.update(line["RoleId"] for group in groups for line in group["Lines"] if line.get("RoleId", 0) > 0)
        for node in nodes:
            if node["type"] == "SubgraphRunNode":
                queue.append((node["subgraphName"], True))
            if node["type"] in ("GetFixAwardNode", "CanGetFixedRewardNode"):
                fixed_ids.add(node["awardId"] if node["type"] == "GetFixAwardNode" else node["fixedRewardId"])
            if node["type"] == "NativeDialogNode":
                raise ValueError("NativeDialogNode requires original action conversion: " + name)
            source = json.loads(node["sourceJson"])
            playable_ids.update(p["mAssetGuid"] for p in source.get("PlayablesProxy", []) if p.get("mAssetGuid"))
            for value in source.get("RemoveEntities", []):
                selected = value.get("StoryPlayableSelect") or value.get("StoryPlayable") or {}
                proxy = ((selected.get("CustomAssetProxy") or {}).get("mProxyRes") or {})
                if proxy.get("mAssetGuid"):
                    playable_ids.add(proxy["mAssetGuid"])
            if node["playableAssetGuid"]:
                playable_ids.add(node["playableAssetGuid"])
            if node["playableKey"] in playable_source["registry"]:
                playable_ids.add(playable_source["registry"][node["playableKey"]])

    apk, constructors, sources = Apk(), Constructors(), []
    def table(bundle, path, type_name):
        obj = apk.asset(bundle, path)
        payload = obj.read().m_Script.encode("utf-8", "surrogateescape")
        import hashlib
        sources.append(dict(apk.provenance(obj), path=path, payload_sha256=hashlib.sha256(payload).hexdigest()))
        return constructors.table(payload, type_name)

    role_rows = table("config_luban_dialog", "Assets/Config/Luban/Dialog/dialog_role.g.bytes", "TbDialogRoleInfo")
    dialog_roles = [{k: r[k] for k in ("RoleId", "EntityClassId", "RoleName", "PortraitPath")}
                    for r in role_rows if r["RoleId"] in roles]
    if {r["RoleId"] for r in dialog_roles} != roles:
        raise ValueError("Unresolved official mountain dialogue roles")
    conditions = table("config_luban", "Assets/Config/Luban/conditions.g.bytes", "TbConditionInfo")
    condition_ids = {i for row in talks for i in (row.get("Condition") or {}).get("Ids", [])}
    by_id = {r["ClassId"]: r for r in conditions}
    pending = list(condition_ids)
    while pending:
        condition = by_id[pending.pop()]
        for child in (condition.get("Condition") or {}).get("Ids", []):
            if child not in condition_ids:
                condition_ids.add(child)
                pending.append(child)
    quick_buy_ids = {json.loads(n["sourceJson"])["ItemId"] for r in records + subgraphs
                     for n in r["nodes"] if n["type"] == "ItemQuikBuyNode"}
    quick_buys = [r for r in table("config_luban", "Assets/Config/Luban/quick_buy.g.bytes", "TbQuickBuyInfo")
                 if r["ItemId"] in quick_buy_ids]
    if {r["ItemId"] for r in quick_buys} != quick_buy_ids:
        raise ValueError("Unresolved mountain quick buys")
    witch_effects, witch_awards = build_witch_results(tables, entity_templates,
        table("config_luban", "Assets/Config/Luban/reward_rule.g.bytes", "TbRewardRuleInfo"))
    options = {(r["StoryName"], r["Id"]): r for r in tables["story_option_dialog"]}
    fixed = {r["Id"]: r for r in tables["fixed_reward"]}
    tasks = [r for r in tables["task"] if r["ClassId"] in range(1165, 1271, 5)]
    if sorted(r["ClassId"] for r in tasks) != list(range(1165, 1271, 5)):
        raise ValueError("Incomplete original mountain mainline task chain")
    story_runtime = dict(manifest="854_156383", mapClassId=12, sourceSha256=sha256(adventure_path),
        mapDefinitionSha256=sha256(MAP_ROOT / "map12.json"), entityCatalogSha256=sha256(MAP_ROOT / "map12-entities.json"),
        entityTemplates=entity_templates, puzzleRules=puzzle_rules(definition, catalog, templates,
            json.loads((MAP_ROOT / "Source/tables.json").read_text(encoding="utf8"))["EC/entity_grid_mover"]),
        witchInfos=[{"ActiveCount": r["WitchActivedCount"], "StoryBookId": r["StoryBookId"],
            "IsNextState": r["IsNextState"], "StageResults": [{"ComponentType": int(k), "ComponentIndex": v}
            for k, v in r["StageResultComps"].items()]} for r in tables["witch"]],
        witchResultEffects=witch_effects, witchPeriodicityAwards=witch_awards,
        dialogRoles=dialog_roles, mapLogicTags=logic, mapTalkEntries=talks,
        mapTalkConditions=[by_id[i] for i in sorted(condition_ids)],
        storyOptions=[options[key] for key in sorted(option_keys)], nativeDialogs=[],
        fixedRewards=[{"Id": i, "CanGetTimes": fixed[i]["CanGetTimes"], "Items": [
            {"ItemId": int(k), "Count": int(v)} for k, v in fixed[i]["Reward"].items()]} for i in sorted(fixed_ids)],
        quickBuys=quick_buys, mainlineTasks=tasks, stories=records, subgraphs=subgraphs,
        playables=[{"AssetGuid": guid, "Duration": playable_source["scenes"][guid]["duration"],
            "Keys": sorted(k for k, v in playable_source["registry"].items() if v == guid)} for guid in sorted(playable_ids)])
    battle_runtime = {k: battles[k] for k in ("manifest", "mapClassId", "stages", "monsterRosters", "entityStageBindings")}
    battle_runtime["sourceSha256"] = sha256(MAP_ROOT / "map12-battles.json")
    battle_runtime["battleScenes"] = [dict(GameId=int(key), **value) for key, value in sorted(battles["battleScenes"].items(), key=lambda p: int(p[0]))]
    write_json(OUTPUT / "stories.json", story_runtime)
    write_json(OUTPUT / "battle-runtime.json", battle_runtime)
    for path in (OUTPUT / "stories.json", OUTPUT / "battle-runtime.json"):
        write_meta(path, "TextScriptImporter")
    inputs = [adventure_path, template_path, template_receipt_path, playable_path,
        MAP_ROOT / "map12.json", MAP_ROOT / "source-manifest.json", MAP_ROOT / "map12-entities.json",
        MAP_ROOT / "entity-source-manifest.json", MAP_ROOT / "map12-battles.json", MAP_ROOT / "battle-source-manifest.json",
        MAP_ROOT / "Source/tables.json"]
    current_code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    puzzle_code = [current_code / folder / (name + ".cs") for folder, names in (
        ("common-source/Common", ("StageResultEntityPushedComponent", "GridMoverComponent", "GridGroundComponent",
            "GridTransformComponent", "MyMath", "EntityLockPushBox", "MapPuzzleTerminalDataHuarongDao")),
        ("client-source", ("ECStageResultEntityPushedView", "MyStageResultEntityPushed"))) for name in names]
    write_json(REFERENCE, dict(manifest="854_156383", mapClassId=12,
        operation="Preserve original EC component order, raw story nodes and ports, recursive subgraphs, dialogue roles, fixed rewards and all eleven puzzle terminal payloads. No server decisions are synthesized.",
        inputs=[{"path": p.relative_to(ROOT).as_posix(), "sha256": sha256(p)} for p in inputs],
        puzzleClientCode=[{"path": p.relative_to(ROOT).as_posix(), "sha256": sha256(p)} for p in puzzle_code],
        confirmedOfflinePushRules={"playerAfterPush": "previous box cell",
            "complete": "all listed detector cells occupied by listed boxes", "latchedUnlock": True,
            "results": "execute original unlock spawn/state-update stage; rewards still require claiming"},
        officialTables=sources, outputs={p.name: {"path": p.relative_to(ROOT).as_posix(), "sha256": sha256(p)}
            for p in (OUTPUT / "stories.json", OUTPUT / "battle-runtime.json")},
        counts=dict(stages=len(battles["stages"]), bindings=len(battles["entityStageBindings"]),
            stories=len(records), subgraphs=len(subgraphs), nodes=sum(len(r["nodes"]) for r in records + subgraphs),
            roles=len(roles), terminals=11, tasks=len(tasks))))
    print(f"Map12 runtime payload: {len(records)} stories, {len(subgraphs)} recursive subgraphs, {len(battles['stages'])} stages", flush=True)


if __name__ == "__main__":
    build()
