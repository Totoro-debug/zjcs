"""Build Map11 story and battle payloads for the offline world runtime."""
import argparse
import hashlib
import json
import math
import re
from pathlib import Path

from official_apk import Apk
from official_tables import Constructors


ROOT = Path(__file__).resolve().parents[1]
MAP_ROOT = ROOT / "unity/Assets/Official/World/Map/11"
ADVENTURE_PATH = ROOT / "assets/official/adventure.json"
LOCALIZATION_PATH = ROOT / "assets/official/map-localization.json"
PLAYABLES_PATH = ROOT / "assets/official/map-playables.json"
PLAYABLES_SOURCE_MANIFEST_PATH = ROOT / "assets/reference/official-map/playables-source-manifest.json"
MAP_SOURCE_MANIFEST_PATH = MAP_ROOT / "source-manifest.json"
BATTLE_SOURCE_PATH = MAP_ROOT / "map11-battles.json"
BATTLE_SOURCE_MANIFEST_PATH = MAP_ROOT / "battle-source-manifest.json"
ENTITY_TEMPLATES_PATH = ROOT / "assets/official/map-entities.json"
ENTITY_TEMPLATES_MANIFEST_PATH = ROOT / "assets/reference/official-map/entities-source-manifest.json"
DEFAULT_OUTPUT_ROOT = MAP_ROOT / "Resources/Map11"
DEFAULT_MANIFEST_PATH = ROOT / "assets/reference/official-map/map11-runtime-source-manifest.json"
MAINLINE_TASK_IDS = tuple(range(1045, 1161, 5))


def build_witch_results(tables, entity_templates, rules, map_id=11):
    """Resolve EC result indices without losing their original type/index identity."""
    template = next(row for row in entity_templates if row["EntityClassId"] == 6620)
    components = {row["Type"]: json.loads(row["Json"]) for row in template["Components"]}
    statuses = {row["ClassId"]: row for row in tables["status_effect"]}
    results = []
    for index, info in enumerate(components["StageResultAwardComponent"]["AwardInfos"]):
        awards = []
        for rule_id in info["RuleIds"]:
            matches = [row for row in rules if row["Id"] == rule_id]
            if len(matches) != 1:
                raise ValueError("Witch reward rule is not a single deterministic row: " + str(rule_id))
            rule = matches[0]
            if rule["Condition"] is not None or rule["Percent"] != 1 or len(rule["IdList"]) != 1 \
                    or rule["LevelCountId"] or rule["SubRankCountId"] or rule["IndexMininum"] \
                    or len(rule["CountList"]) != 1 or int(rule["CountList"][0]) != rule["CountList"][0]:
                raise ValueError("Unsupported non-deterministic witch reward: " + str(rule_id))
            awards.append({"ItemId": rule["IdList"][0], "Count": int(rule["CountList"][0])})
        results.append({"ComponentType": 200, "ComponentIndex": index, "AwardItems": awards})
    for index, amount in enumerate(components["StageResultModifyHpPercentComponent"]["HpModifyInfos"]):
        results.append({"ComponentType": 212, "ComponentIndex": index, "HpPercentChange": amount})
    for index, info in enumerate(components["StageResultAwardStatusEffectComponent"]["AwardStatusEffectInfos"]):
        ids = info["mAwardStatusEffects"]
        if any(statuses[value]["Duration"] != 0 or statuses[value]["MaxActivateTimes"] != 1 for value in ids):
            raise ValueError("Witch status is not a one-battle, saved-until-used effect.")
        results.append({"ComponentType": 235, "ComponentIndex": index, "StatusEffects": [
            {"StatusEffectId": value, "FightStatusClassIds": statuses[value]["FightStatusIds"],
             "EntryId": statuses[value]["EntryId"], "EffectType": statuses[value]["EffectType"]} for value in ids]})
    for index, info in enumerate(components["StageResultSpawnEntityComponent"]["SpawnEntityInfos"]):
        matches = [row for row in tables["Map/map_entity_group"] if row["Group"] == info["GroupId"]
                   and row["FilterCondition"].get("MapClassId") == map_id]
        if len(matches) != 1 or len(matches[0]["Entitys"]) != 1:
            raise ValueError("Witch spawn must resolve its pinned Map" + str(map_id) + " group.")
        extra = matches[0]["ExtraDataInfos"]
        if len(extra) != 1 or extra[0]["_type"] != "MapEntityGroupInfoExtraStageIdsData" \
                or len(extra[0]["StageIdsList"]) != 1 or len(extra[0]["StageIdsList"][0]) != 1:
            raise ValueError("Witch spawn must resolve one official battle stage.")
        results.append(dict({"ComponentType": 245, "ComponentIndex": index,
                             "SpawnEntityClassId": matches[0]["Entitys"][0],
                             "SpawnStageId": extra[0]["StageIdsList"][0][0]}, **info))
    periodic = [dict({key: row[key] for key in ("PeriodicityGroupId", "AwardGroupId", "PeriodicityId",
                                                "Weight", "Action", "ActionTime", "IsFirstReward")},
                     StageResults=[{"ComponentType": int(kind), "ComponentIndex": int(index)}
                                   for kind, index in row["StageResultComps"].items()])
                for row in tables["Map/map_periodicity_random_award_interaction"]
                if row["PeriodicityGroupId"] == 1 and row["AwardGroupId"] == 1]
    if len(periodic) != 7 or sum(row["Weight"] for row in periodic) != 100:
        raise ValueError("Pinned witch periodic group must retain its seven weights totaling 100.")
    return results, periodic


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_path(path):
    path = Path(path).resolve()
    try:
        return path.relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def load_entity_templates():
    manifest = json.loads(ENTITY_TEMPLATES_MANIFEST_PATH.read_text(encoding="utf-8"))
    if manifest.get("manifest") != "854_156383" \
            or manifest.get("output_sha256") != sha256(ENTITY_TEMPLATES_PATH):
        raise ValueError("Official EC entity templates differ from their pinned source manifest.")
    templates = json.loads(ENTITY_TEMPLATES_PATH.read_text(encoding="utf-8"))["templates"]
    entities = json.loads((MAP_ROOT / "map11-entities.json").read_text(encoding="utf-8"))["entities"]
    class_ids = sorted({entity["entityClassId"] for entity in entities})
    if len(class_ids) != 110 or any(str(class_id) not in templates for class_id in class_ids):
        raise ValueError("Map11 must resolve all 110 official EC entity templates.")
    # Component order is the order decoded from the original EC pack. Do not sort it:
    # the stage result arrays and component initialization order belong to the source.
    return [{"EntityClassId": class_id,
             "Components": [{"Type": name,
                              "Json": json.dumps(value, ensure_ascii=False, separators=(",", ":"))}
                             for name, value in templates[str(class_id)].items()]}
            for class_id in class_ids]


def build_puzzle_rules(entity_templates):
    definition = json.loads((MAP_ROOT / "map11.json").read_text(encoding="utf-8"))
    entities = json.loads((MAP_ROOT / "map11-entities.json").read_text(encoding="utf-8"))["entities"]
    placements = {row["EntityId"]: json.loads(row["Json"]) for row in definition["EntityComponents"]}
    templates = {row["EntityClassId"]: {c["Type"]: json.loads(c["Json"]) for c in row["Components"]}
                 for row in entity_templates}
    dependent_ids = {int(target) for entity in entities if entity["lockType"] == 2
                     for target in json.loads(entity["lockParamsJson"])["EntityStateDict"]}
    for local in placements.values():
        if "LoEntityPuzzleTerminalComponent" in local or "LoEntityLockComponent" in local:
            dependent_ids.update(local.get("LoStageResultLoEntitySpawnComponent", {}).get("LoEntityIds", []))
            for updates in local.get("LoStageResultStateUpdateComponent", {}).get("DataList") or []:
                dependent_ids.update(update["LoEntId"] for update in updates.get("StageDatas", []))
    result = []
    for entity in entities:
        template = templates[entity["entityClassId"]]
        local = placements[entity["placementId"]]
        detector = template.get("CharacterPositionOverlapDetectorComponent")
        terminal = local.get("LoEntityPuzzleTerminalComponent")
        if entity["entityClassId"] != 6503 and not detector and not terminal \
                and entity["lockType"] not in (2, 5, 7) and entity["placementId"] not in dependent_ids:
            continue
        params = json.loads(entity["lockParamsJson"])
        conditions = [{"PlacementId": int(k), "State": int(v)} for k, v in params.get("EntityStateDict", {}).items()]
        if not conditions:
            conditions = [{"PlacementId": int(value), "State": 0} for value in entity["lockConditionEntityIds"]] \
                if entity["lockType"] in (5, 7) else []
        terminal_locks = []
        if terminal:
            match = re.fullmatch(r"\{LockLoEntityIds:\[([0-9, ]*)\]\}", terminal["Params"])
            if not match:
                raise ValueError("Unknown official Map11 terminal parameters: " + terminal["Params"])
            terminal_locks = [int(value) for value in match[1].split(",") if value.strip()]
        result.append({"PlacementId": entity["placementId"], "EntityClassId": entity["entityClassId"],
                       "InitialState": entity["state"], "GridX": entity["grid"][0], "GridY": entity["grid"][1],
                       "InitiallySpawned": entity["active"],
                       "LockType": entity["lockType"], "LockConditions": conditions,
                       "ConditionEntityInactiveInUnlock": entity["conditionEntityInactiveInUnlock"],
                       "ConditionEntityNeverDestroy": entity["conditionEntityNeverDestroy"],
                       "TerminalLockIds": terminal_locks,
                       "HasOverlapDetector": detector is not None, "OverlapDetector": detector,
                       "Stage": template.get("WorldStageComponent"),
                       "SelfStates": template.get("StageResultSelfStateUpdateComponent", {}).get("InfoList", []),
                       "SpawnIds": local.get("LoStageResultLoEntitySpawnComponent", {}).get("LoEntityIds", []),
                       "OtherStates": local.get("LoStageResultStateUpdateComponent", {}).get("DataList") or [],
                       # Confirmed offline exception after checking the original APK,
                       # current cached EC/MapInfoWrap and decompiled state/spawn code.
                       # Keep the original OtherStates entry above for provenance.
                       "OfflineIgnoredMissingStateTargets": [423] if entity["placementId"] == 413 else [],
                       "Notify": local.get("LoStageResultNotifyCompleteComponent", {}).get("Configs", [])})
    by_id = {row["PlacementId"]: row for row in result}
    for row in result:
        for target in row["LockConditions"]:
            if target["PlacementId"] not in by_id:
                raise ValueError("Unresolved Map11 puzzle dependency: " + str(target["PlacementId"]))
    if len([row for row in result if row["TerminalLockIds"]]) != 7:
        raise ValueError("Map11 must preserve all seven puzzle terminals")
    return result


def build_story_node(name, node_id, source, playable_scenes, option_keys):
    ports = [
        {"name": port_name,
         "connections": [{"nodeId": int(connection["node"]), "port": connection["port"]}
                         for connection in port.get("connections", []) if connection.get("node")]}
        for port_name, port in source.get("ports", {}).items()
    ]
    position = source.get("Pos", {})
    size = source.get("Size") or {}
    destination = source.get("Destination", {})
    move_target = source.get("MoveToTargetPos", {})
    offset = source.get("Offset", {})
    move = source.get("MoveBehaviour", {})
    compare_target = source.get("Target", {})
    playable = source.get("StoryPlayable") or {}
    proxies = source.get("PlayablesProxy", [])
    playable_guid = next((value.get("mAssetGuid", "") for value in proxies if value.get("mAssetGuid")), "")
    remove_playables = [item.get("StoryPlayableSelect") or item.get("StoryPlayable") or {}
                        for item in source.get("RemoveEntities", [])]
    remove_playable = remove_playables[0] if remove_playables else {}
    remove_proxy = remove_playable.get("CustomAssetProxy") or {}
    remove_resource = remove_proxy.get("mProxyRes") or {}
    if source.get("_type") == "OptionDialogNode":
        option_keys.update((name, int(option_id)) for option_id in source.get("Options", []))
    playable_scene = playable_scenes.get(playable_guid, {})
    duration = float(playable_scene.get("duration", 0))
    # PlayPlayableNode.DoRun applies SetAutoStopCountDown(PlayableDura)
    # after starting the prefab. A finite node countdown also stops an
    # otherwise indefinite animator (e.g. mountain playable_npc_idle).
    if float(source.get("PlayableDura", -1)) > 0:
        duration = float(source["PlayableDura"])
    wait_playable = bool(source.get("IsWaitPlayableStop", False))
    if playable_guid and (not math.isfinite(duration) or (wait_playable and duration <= 0)):
        raise ValueError("Map11 story playable duration is invalid for its wait policy: " + playable_guid)
    source_json = json.dumps(source, ensure_ascii=False, separators=(",", ":"))
    return {
        "id": int(node_id),
        "type": source.get("_type", ""),
        "sourceJson": source_json,
        "dialogId": source.get("DialogId", -1),
        "awardId": source.get("AwardId", 0),
        "fixedRewardId": source.get("FixedRewardId", 0),
        "storyName": source.get("StoryName", source.get("Name", "")),
        "subgraphName": source.get("subgraph_name", ""),
        "count": source.get("Count", 0),
        "entityState": source.get("State", 1),
        "compareOption": source.get("Option", -1),
        "compareTargetInt": compare_target.get("mInt", 0) if isinstance(compare_target, dict) else 0,
        "storyStatus": source.get("Status", source.get("State", 0)),
        "entityClassId": source.get("EntityClassId", 0),
        "entityType": source.get("EntityType", 0),
        "playerLevel": source.get("Level", 0),
        "entityTag": source.get("LoEntityTag", ""),
        "positionType": source.get("PosType", 0),
        "direction": source.get("Direction", 0),
        "movable": bool(source.get("Movable", 0)),
        "positionX": position.get("X", 0),
        "positionY": position.get("Y", 0),
        "positionWidth": size.get("x", 0),
        "positionHeight": size.get("y", 0),
        "destinationX": destination.get("X", 0),
        "destinationY": destination.get("Y", 0),
        "moveTargetX": move_target.get("x", 0),
        "moveTargetY": move_target.get("y", 0),
        "seconds": source.get("Seconds", 0),
        "moveSpeed": move.get("Speed", 4),
        "range": source.get("Range", 1),
        "selectGetType": source.get("SelectGetType", -1),
        "loEntityId": source.get("LoEntityId", 0),
        "sceneType": source.get("SceneType", 0),
        "mapClassId": source.get("MapClassId", 0),
        "regionId": source.get("RegionId", 0),
        "followerClassIds": source.get("FollowerClassId", []),
        "endAsStoryFinish": bool(source.get("EndAsStoryFinish", 0)),
        "tag": source.get("Tag", ""),
        "needHideUi": bool(source.get("NeedHideUi", 0)),
        "needShowUi": bool(source.get("NeedShowUi", 0)),
        "needMask": bool(source.get("NeedMask", 0)),
        "canDrag": bool(source.get("CanDrag", 0)),
        "checkInteractiveRange": bool(source.get("mIsCheckInteractiveRange", 0)),
        "skipStageOperation": bool(source.get("IsSkipStageOperation", 0)),
        "actionType": source.get("ActionType", 0),
        "revert": source.get("Revert", 0),
        "storyUiKey": source.get("StoryUIKey", ""),
        "waitRegister": bool(source.get("WaitRegister", 0)),
        "endOnUnregister": bool(source.get("EndOnUnregister", 0)),
        "clickTimes": source.get("ClickTimes", 1),
        "viewSizeOffset": source.get("ViewSizeOffset", 0),
        "duration": source.get("Duration", 0),
        "unlockRadius": source.get("UnlockRadius", 0),
        "registerKey": source.get("RegisterKey", ""),
        "offsetX": offset.get("x", 0),
        "offsetY": offset.get("y", 0),
        "playableAssetGuid": playable_guid,
        "playableKey": playable.get("PreDefineKey", ""),
        "removePlayableKey": remove_playable.get("PreDefineKey", ""),
        "removePlayableAssetGuid": remove_resource.get("mAssetGuid", ""),
        "playableDuration": duration,
        "waitPlayableStop": wait_playable,
        "playableKeys": source.get("Playables", []),
        "mapLogicStates": [
            {"Tag": state.get("LoEntTag", ""), "State": state.get("State", 0)}
            for state in source.get("MapLoEntTagDatas", [])
        ],
        "options": source.get("Options", []),
        "removeEntityClassIds": [item.get("EntityClassId", 0)
                                  for item in source.get("RemoveEntities", [])],
        "ports": ports,
    }


def load_checked_sources():
    battle_manifest = json.loads(BATTLE_SOURCE_MANIFEST_PATH.read_text(encoding="utf-8"))
    battle_hash = sha256(BATTLE_SOURCE_PATH)
    if battle_manifest.get("manifest") != "854_156383" or battle_manifest.get("mapClassId") != 11 \
            or battle_manifest.get("output", {}).get("sha256") != battle_hash:
        raise ValueError("Pinned Map11 battle data does not match its source manifest.")
    battle_source = json.loads(BATTLE_SOURCE_PATH.read_text(encoding="utf-8"))
    scenes = battle_source.get("battleScenes")
    if battle_source.get("mapClassId") != 11 or battle_source.get("manifest") != "854_156383" \
            or not isinstance(scenes, dict):
        raise ValueError("Pinned Map11 battle input is malformed.")
    if (len(battle_source.get("stages", [])) != 130
            or len(battle_source.get("monsterRosters", [])) != 130
            or len(battle_source.get("entityStageBindings", [])) != 591):
        raise ValueError("Pinned Map11 battle input must contain 129 placed stages, the witch summon stage and 591 entity bindings.")
    battle_runtime = {
        "manifest": battle_source["manifest"],
        "mapClassId": battle_source["mapClassId"],
        "sourceSha256": battle_hash,
        "stages": battle_source["stages"],
        "monsterRosters": battle_source["monsterRosters"],
        "entityStageBindings": battle_source["entityStageBindings"],
        "battleScenes": [dict({"GameId": int(game_id)}, **scene)
                         for game_id, scene in sorted(scenes.items(), key=lambda pair: int(pair[0]))],
    }

    adventure_hash = sha256(ADVENTURE_PATH)
    playable_hash = sha256(PLAYABLES_PATH)
    playable_manifest = json.loads(PLAYABLES_SOURCE_MANIFEST_PATH.read_text(encoding="utf-8"))
    if playable_manifest.get("manifest") != "854_156383" \
            or playable_manifest.get("outputs", {}).get("map-playables.json") != playable_hash:
        raise ValueError("Pinned map playable registry does not match its source manifest.")
    map_hash = sha256(MAP_ROOT / "map11.json")
    map_manifest = json.loads(MAP_SOURCE_MANIFEST_PATH.read_text(encoding="utf-8"))
    map_input_hashes = {row.get("path"): row.get("sha256") for row in map_manifest.get("inputs", [])}
    if map_manifest.get("format") != 1 or map_manifest.get("mapClassId") != 11 \
            or map_manifest.get("output", {}).get("sha256") != map_hash \
            or map_input_hashes.get("assets/official/adventure.json") != adventure_hash:
        raise ValueError("Pinned Map11 definition does not match its source manifest.")
    entity_hash = sha256(MAP_ROOT / "map11-entities.json")
    entity_manifest = json.loads((MAP_ROOT / "entity-source-manifest.json").read_text(encoding="utf-8"))
    if entity_manifest.get("manifest") != "854_156383" \
            or entity_manifest.get("mapClassId") != 11 \
            or entity_manifest.get("output", {}).get("sha256") != entity_hash:
        raise ValueError("Pinned Map11 entity catalog does not match its source manifest.")

    return battle_runtime, adventure_hash, playable_hash, map_hash, entity_hash


def build(output_root=DEFAULT_OUTPUT_ROOT, manifest_path=None):
    battle_runtime, adventure_hash, playable_hash, map_hash, entity_hash = load_checked_sources()
    entity_templates = load_entity_templates()
    output_root = Path(output_root)
    if not output_root.is_absolute():
        output_root = ROOT / output_root
    output_root.mkdir(parents=True, exist_ok=True)
    battle_output = output_root / "battle-runtime.json"
    story_output = output_root / "stories.json"
    if manifest_path is None:
        manifest_path = DEFAULT_MANIFEST_PATH if output_root == DEFAULT_OUTPUT_ROOT else output_root / "map11-runtime-source-manifest.json"
    manifest_path = Path(manifest_path)

    adventure = json.loads(ADVENTURE_PATH.read_text(encoding="utf-8"))
    playable_source = json.loads(PLAYABLES_PATH.read_text(encoding="utf-8"))
    playable_scenes = playable_source.get("scenes")
    playable_registry = playable_source.get("registry")
    if not isinstance(playable_scenes, dict) or not isinstance(playable_registry, dict):
        raise ValueError("Pinned official playable scenes or registry are missing.")

    tables = adventure["tables"]
    witch_rows = tables.get("witch", [])
    if len(witch_rows) != 14 or [int(row["WitchActivedCount"]) for row in witch_rows] != list(range(14)):
        raise ValueError("Pinned WitchInfo must define activation counts 0 through 13.")
    witch_infos = [{
        "ActiveCount": int(row["WitchActivedCount"]),
        "StoryBookId": int(row["StoryBookId"]),
        "IsNextState": bool(row["IsNextState"]),
        "StageResults": [{"ComponentType": int(component_type), "ComponentIndex": int(component_index)}
                          for component_type, component_index in row["StageResultComps"].items()],
    } for row in witch_rows]
    apk = Apk("854_156383")
    witch_reward_object = apk.asset("config_luban", "Assets/Config/Luban/reward_rule.g.bytes")
    witch_reward_payload = witch_reward_object.read().m_Script.encode("utf-8", "surrogateescape")
    witch_results, witch_periodic_awards = build_witch_results(tables, entity_templates,
        Constructors().table(witch_reward_payload, "TbRewardRuleInfo"))
    map_logic_tags = [row for row in tables["Map/map_logic_entity_tag"] if row["MapClass"] == 11]
    if len(map_logic_tags) != 35 or len({row["LogicId"] for row in map_logic_tags}) != 35:
        raise ValueError("Map11 must have 35 uniquely identified official logic tags.")
    tag_keys = {(f"entity_{row['EntityClass']}_name", row["Tag"]) for row in map_logic_tags}
    # MapTalk first checks the placement's tag, then falls back to the class's
    # untagged rows. Puzzle terminals and several NPCs use only that fallback.
    tag_keys.update((f"entity_{row['EntityClassId']}_name", "") for row in entity_templates)
    map_talk_rows = [row for row in tables["Map/map_talk"]
                     if (row["EntityId"], row["Tag"]) in tag_keys]
    stories_by_name = {row["StoryName"]: row for row in tables["story"]}
    story_names = list(dict.fromkeys(row["TalkId"] for row in map_talk_rows if row.get("TalkId")))
    if len(story_names) != 44 or any(name not in stories_by_name or name not in adventure["stories"]
                                     for name in story_names):
        raise ValueError("Map11 MapTalk must resolve to all 44 pinned story graphs.")

    option_keys = set()
    story_records = []
    subgraph_records = []
    playable_guids = set()
    referenced_role_ids = set()
    fixed_reward_ids = set()
    for name in story_names:
        graph = adventure["stories"][name]
        dialogue = adventure["dialogues"].get(name, {})
        if not graph.get("nodes") or not isinstance(dialogue, dict):
            raise ValueError("Map11 official story graph or dialogue groups are missing: " + name)
        groups = []
        for group_id, lines in sorted(dialogue.items(), key=lambda pair: int(pair[0])):
            groups.append({"Id": int(group_id), "Lines": lines})
            referenced_role_ids.update(line.get("RoleId", 0) for line in lines if line.get("RoleId", 0) > 0)
        nodes = []
        for node_id, source in sorted(graph["nodes"].items(), key=lambda pair: int(pair[0])):
            node = build_story_node(name, node_id, source, playable_scenes, option_keys)
            nodes.append(node)
            if node["type"] == "GetFixAwardNode":
                fixed_reward_ids.add(node["awardId"])
            elif node["type"] == "CanGetFixedRewardNode":
                fixed_reward_ids.add(node["fixedRewardId"])
            if node["playableAssetGuid"]:
                playable_guids.add(node["playableAssetGuid"])
            if node["removePlayableAssetGuid"]:
                playable_guids.add(node["removePlayableAssetGuid"])
        starts = [node for node in nodes if node["type"] == "StartStoryNode"]
        if len(starts) != 1:
            raise ValueError("Map11 story must have one start node: " + name)
        story_records.append({
            "key": name,
            "maxCount": stories_by_name[name]["MaxCount"],
            "entryNodeId": starts[0]["id"],
            "graphJson": json.dumps(graph, ensure_ascii=False, separators=(",", ":")),
            "nodes": nodes,
            "dialogueGroups": groups,
        })

    subgraph_names = sorted({node["subgraphName"] for story in story_records for node in story["nodes"]
                             if node["type"] == "SubgraphRunNode" and node["subgraphName"]})
    if subgraph_names != ["CombatAssistance", "FangjianbeiActive", "GriffonTeamUp", "HolyMonsterActive"]:
        raise ValueError("Map11 story subgraph references differ from the pinned graph closure.")
    for name in subgraph_names:
        graph = adventure["stories"].get(name)
        dialogue = adventure["dialogues"].get(name, {})
        if not graph or not graph.get("nodes") or not isinstance(dialogue, dict):
            raise ValueError("Map11 official story subgraph or dialogue groups are missing: " + name)
        groups = []
        for group_id, lines in sorted(dialogue.items(), key=lambda pair: int(pair[0])):
            groups.append({"Id": int(group_id), "Lines": lines})
            referenced_role_ids.update(line.get("RoleId", 0) for line in lines if line.get("RoleId", 0) > 0)
        nodes = []
        for node_id, source in sorted(graph["nodes"].items(), key=lambda pair: int(pair[0])):
            node = build_story_node(name, node_id, source, playable_scenes, option_keys)
            nodes.append(node)
            if node["type"] == "GetFixAwardNode":
                fixed_reward_ids.add(node["awardId"])
            elif node["type"] == "CanGetFixedRewardNode":
                fixed_reward_ids.add(node["fixedRewardId"])
            if node["playableAssetGuid"]:
                playable_guids.add(node["playableAssetGuid"])
            if node["removePlayableAssetGuid"]:
                playable_guids.add(node["removePlayableAssetGuid"])
        starts = [node for node in nodes if node["type"] == "StartStoryNode"]
        if len(starts) != 1:
            raise ValueError("Map11 story subgraph must have one start node: " + name)
        subgraph_records.append({
            "key": name,
            "maxCount": -1,
            "entryNodeId": starts[0]["id"],
            "graphJson": json.dumps(graph, ensure_ascii=False, separators=(",", ":")),
            "nodes": nodes,
            "dialogueGroups": groups,
        })

    for story in story_records + subgraph_records:
        for node in story["nodes"]:
            source = json.loads(node["sourceJson"])
            if source.get("_type") == "NativeDialogNode":
                raise ValueError("Map11 story uses NativeDialogNode; add its fixed dialog table to the converter.")
            for proxy in source.get("PlayablesProxy", []):
                guid = proxy.get("mAssetGuid", "")
                if guid:
                    playable_guids.add(guid)
            for value in source.get("RemoveEntities", []):
                selected = value.get("StoryPlayableSelect") or value.get("StoryPlayable") or {}
                resource = ((selected.get("CustomAssetProxy") or {}).get("mProxyRes") or {})
                if resource.get("mAssetGuid"):
                    playable_guids.add(resource["mAssetGuid"])

    options_by_key = {(row["StoryName"], row["Id"]): row for row in tables["story_option_dialog"]}
    if option_keys - set(options_by_key):
        raise ValueError("Map11 story references a missing official option: " + repr(sorted(option_keys - set(options_by_key))))
    story_options = [options_by_key[key] for key in sorted(option_keys)]

    fixed_rewards_by_id = {row["Id"]: row for row in tables["fixed_reward"]}
    if fixed_reward_ids - set(fixed_rewards_by_id):
        raise ValueError("Map11 story references a missing fixed reward: "
                         + repr(sorted(fixed_reward_ids - set(fixed_rewards_by_id))))
    fixed_rewards = [
        {"Id": reward_id, "CanGetTimes": int(fixed_rewards_by_id[reward_id]["CanGetTimes"]),
         "Items": [{"ItemId": int(item_id), "Count": int(count)}
                   for item_id, count in sorted(fixed_rewards_by_id[reward_id]["Reward"].items(),
                                                key=lambda pair: int(pair[0]))]}
        for reward_id in sorted(fixed_reward_ids)
    ]

    condition_ids = {condition_id for row in map_talk_rows
                     for condition_id in row.get("Condition", {}).get("Ids", [])}
    quick_buy_object = apk.asset("config_luban", "Assets/Config/Luban/quick_buy.g.bytes")
    quick_buy_payload = quick_buy_object.read().m_Script.encode("utf-8", "surrogateescape")
    quick_buy_ids = {json.loads(node["sourceJson"])["ItemId"]
                     for story in story_records + subgraph_records for node in story["nodes"]
                     if node["type"] == "ItemQuikBuyNode"}
    quick_buys = [row for row in Constructors().table(quick_buy_payload, "TbQuickBuyInfo")
                 if row["ItemId"] in quick_buy_ids]
    if {row["ItemId"] for row in quick_buys} != quick_buy_ids:
        raise ValueError("Map11 story references an unresolved official quick buy.")
    condition_object = apk.asset("config_luban", "Assets/Config/Luban/conditions.g.bytes")
    condition_payload = condition_object.read().m_Script.encode("utf-8", "surrogateescape")
    condition_rows = Constructors().table(condition_payload, "TbConditionInfo")
    conditions_by_id = {row["ClassId"]: row for row in condition_rows}
    pending_ids = list(condition_ids)
    while pending_ids:
        condition_id = pending_ids.pop()
        if condition_id not in conditions_by_id:
            raise ValueError("Map11 MapTalk references a missing pinned official condition: " + str(condition_id))
        for child_id in conditions_by_id[condition_id].get("Condition", {}).get("Ids", []):
            if child_id not in condition_ids:
                condition_ids.add(child_id)
                pending_ids.append(child_id)
    map_talk_conditions = [row for row in condition_rows if row["ClassId"] in condition_ids]
    if {row["ClassId"] for row in map_talk_conditions} != condition_ids:
        raise ValueError("Map11 MapTalk references a missing pinned official condition.")
    map_talk_conditions.sort(key=lambda row: row["ClassId"])

    role_object = apk.asset("config_luban_dialog", "Assets/Config/Luban/Dialog/dialog_role.g.bytes")
    role_payload = role_object.read().m_Script.encode("utf-8", "surrogateescape")
    role_rows = Constructors().table(role_payload, "TbDialogRoleInfo")
    role_records = [row for row in role_rows if row["RoleId"] in referenced_role_ids]
    if {row["RoleId"] for row in role_records} != referenced_role_ids:
        raise ValueError("Map11 story dialogues reference a missing official role.")
    role_records.sort(key=lambda row: row["RoleId"])
    dialog_roles = [{"RoleId": row["RoleId"], "EntityClassId": row["EntityClassId"],
                     "RoleName": row["RoleName"], "PortraitPath": row["PortraitPath"]}
                    for row in role_records]

    playable_guids.update(guid for key, guid in playable_registry.items()
                          if key and any(key == node["playableKey"] for story in story_records for node in story["nodes"]))
    playable_records = []
    wait_playable_guids = {node["playableAssetGuid"] for story in story_records for node in story["nodes"]
                           if node["waitPlayableStop"] and node["playableAssetGuid"]}
    for guid in sorted(playable_guids):
        scene = playable_scenes.get(guid)
        duration = float(scene.get("duration", 0)) if scene else 0
        if scene is None or not math.isfinite(duration) or (guid in wait_playable_guids and duration <= 0):
            raise ValueError("Map11 story playable duration is invalid for its wait policy: " + guid)
        keys = sorted(key for key, value in playable_registry.items() if value == guid)
        playable_records.append({"AssetGuid": guid, "Keys": keys, "Duration": duration})

    mainline_tasks = [row for row in tables["task"] if row.get("ClassId") in MAINLINE_TASK_IDS]
    mainline_tasks.sort(key=lambda row: row["ClassId"])
    if [row["ClassId"] for row in mainline_tasks] != list(MAINLINE_TASK_IDS):
        raise ValueError("Map11 mainline task chain 1045-1160 is incomplete.")
    if any(row.get("FinishCondition", {}).get("MapClassId", 11) not in (0, 11)
           for row in mainline_tasks):
        raise ValueError("Map11 mainline task chain references another map.")

    story_runtime = {
        "manifest": "854_156383",
        "mapClassId": 11,
        "sourceSha256": adventure_hash,
        "mapDefinitionSha256": map_hash,
        "entityCatalogSha256": entity_hash,
        "entityTemplates": entity_templates,
        "witchInfos": witch_infos,
        "witchResultEffects": witch_results,
        "witchPeriodicityAwards": witch_periodic_awards,
        "puzzleRules": build_puzzle_rules(entity_templates),
        "dialogRoles": dialog_roles,
        "mapLogicTags": map_logic_tags,
        "mapTalkEntries": map_talk_rows,
        "mapTalkConditions": map_talk_conditions,
        "storyOptions": story_options,
        "fixedRewards": fixed_rewards,
        "quickBuys": quick_buys,
        "nativeDialogs": [],
        "playables": playable_records,
        "mainlineTasks": mainline_tasks,
        "stories": story_records,
        "subgraphs": subgraph_records,
    }

    write_json(battle_output, battle_runtime)
    write_json(story_output, story_runtime)
    manifest = {
        "manifest": "854_156383",
        "mapClassId": 11,
        "offlineDecisions": {"storyBranchSelection": {
            "selector": "First true input in the original serialized order; otherwise original Exit.",
            "switch": "First equal configured output in the original serialized order; otherwise original Exit.",
            "reason": "User confirmed because the client consumes server SelectedIndex without a packaged server selector."},
            "witchActiveCount": {
                "scope": "All maps; durable keys contain both map class ID and placement ID.",
                "repeatActivation": "Keep the existing record without increasing the count.",
                "reason": "User confirmed the WorldWitchTouchUI all-map count for the missing server query."},
            "witchStageResults": {
                "selection": "Use the pinned WitchInfo row matching the all-map activation count before success.",
                "effects": "Apply every listed StageResult component after successful stage completion in the configured order.",
                "reason": "User confirmed all original configured witch-touch results should apply."},
            "missingStateTargets": [{"placementId": 413, "targetId": 423,
            "action": "Skip only the missing-target update; preserve all other ordered stage results.",
            "reason": "User confirmed after original assembly decompilation and raw EC/MapInfoWrap verification."}]},
        "inputs": {
            "storyBranchCode": [{"path": source_path(path), "sha256": sha256(path)} for path in (
                ROOT / ".tools/source-packages/official-code/client-source/SelectorNode.cs",
                ROOT / ".tools/source-packages/official-code/client-source/SwitchNode.cs",
                ROOT / ".tools/source-packages/official-code/client-source/StoryGraph.cs",
                ROOT / ".tools/source-packages/official-code/client-source/AndNode.cs",
                ROOT / ".tools/source-packages/official-code/client-source/NotNode.cs",
                ROOT / ".tools/source-packages/official-code/client-source/IsStoryStateNode.cs",
                ROOT / ".tools/source-packages/official-code/client-source/IsInTargetMapNode.cs",
                ROOT / ".tools/source-packages/official-code/client-source/GetEntityBaseNode.cs",
                ROOT / ".tools/source-packages/official-code/client-source/GetEntityPosNode.cs",
                ROOT / ".tools/source-packages/official-code/client-source/GetMapWitchExploreInfoNode.cs",
                ROOT / ".tools/source-packages/official-code/client-source/WorldWitchTouchUI.cs",
                ROOT / ".tools/source-packages/official-code/common-source/SerializableObj.cs")],
            "mapDefinition": {"path": MAP_ROOT.joinpath("map11.json").relative_to(ROOT).as_posix(),
                              "sha256": map_hash, "sourceManifestSha256": sha256(MAP_SOURCE_MANIFEST_PATH)},
            "entities": {"path": MAP_ROOT.joinpath("map11-entities.json").relative_to(ROOT).as_posix(),
                         "sha256": entity_hash,
                         "sourceManifestSha256": sha256(MAP_ROOT / "entity-source-manifest.json")},
            "entityTemplates": {"path": source_path(ENTITY_TEMPLATES_PATH),
                                "sha256": sha256(ENTITY_TEMPLATES_PATH),
                                "sourceManifest": source_path(ENTITY_TEMPLATES_MANIFEST_PATH),
                                "sourceManifestSha256": sha256(ENTITY_TEMPLATES_MANIFEST_PATH)},
            "battles": {"path": source_path(BATTLE_SOURCE_PATH), "sha256": sha256(BATTLE_SOURCE_PATH),
                        "sourceManifestSha256": sha256(BATTLE_SOURCE_MANIFEST_PATH)},
            "adventure": {"path": source_path(ADVENTURE_PATH), "sha256": adventure_hash,
                          "sourceManifest": source_path(MAP_SOURCE_MANIFEST_PATH),
                          "sourceManifestSha256": sha256(MAP_SOURCE_MANIFEST_PATH)},
            "localization": {"path": source_path(LOCALIZATION_PATH),
                             "sha256": sha256(LOCALIZATION_PATH)},
            "playables": {"path": source_path(PLAYABLES_PATH), "sha256": playable_hash,
                          "sourceManifestSha256": sha256(PLAYABLES_SOURCE_MANIFEST_PATH)},
            "dialogRoles": dict(apk.provenance(role_object), object="Assets/Config/Luban/Dialog/dialog_role.g.bytes",
                                raw_sha256=hashlib.sha256(role_object.get_raw_data()).hexdigest(),
                                payload_sha256=hashlib.sha256(role_payload).hexdigest()),
            "mapTalkConditions": dict(apk.provenance(condition_object), object="Assets/Config/Luban/conditions.g.bytes",
                                      raw_sha256=hashlib.sha256(condition_object.get_raw_data()).hexdigest(),
                                      payload_sha256=hashlib.sha256(condition_payload).hexdigest()),
            "quickBuys": dict(apk.provenance(quick_buy_object), object="Assets/Config/Luban/quick_buy.g.bytes",
                              raw_sha256=hashlib.sha256(quick_buy_object.get_raw_data()).hexdigest(),
                              payload_sha256=hashlib.sha256(quick_buy_payload).hexdigest()),
            "witchRewardRules": dict(apk.provenance(witch_reward_object),
                object="Assets/Config/Luban/reward_rule.g.bytes",
                payload_sha256=hashlib.sha256(witch_reward_payload).hexdigest()),
        },
        "outputs": {
            "battle-runtime.json": {"path": battle_output.resolve().as_posix(), "sha256": sha256(battle_output),
                                    "stageCount": len(battle_runtime["stages"]),
                                    "rosterCount": len(battle_runtime["monsterRosters"]),
                                    "sceneCount": len(battle_runtime["battleScenes"])},
            "stories.json": {"path": story_output.resolve().as_posix(), "sha256": sha256(story_output),
                             "storyCount": len(story_records), "nodeCount": sum(len(s["nodes"]) for s in story_records),
                             "entityTemplateCount": len(entity_templates),
                             "witchInfoCount": len(witch_infos),
                             "logicTagCount": len(map_logic_tags), "mapTalkCount": len(map_talk_rows),
            "subgraphCount": len(subgraph_records),
            "subgraphNodeCount": sum(len(story["nodes"]) for story in subgraph_records),
                             "mainlineTaskIds": [row["ClassId"] for row in mainline_tasks],
                             "roleIds": [row["RoleId"] for row in dialog_roles],
                             "playableGuids": [row["AssetGuid"] for row in playable_records]},
        },
    }
    write_json(manifest_path, manifest)
    print("Map11 runtime data: " + str(len(battle_runtime["stages"])) + " stages, "
          + str(len(battle_runtime["battleScenes"]))
          + " battle scenes, " + str(len(story_records)) + " stories / "
          + str(sum(len(story["nodes"]) for story in story_records)) + " nodes, 24 mainline tasks.")
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT_ROOT,
                        help="runtime output directory (defaults to Unity Resources/Map11)")
    parser.add_argument("--manifest", type=Path,
                        help="source manifest path (defaults beside a custom --output directory)")
    args = parser.parse_args()
    build(args.output, args.manifest)


if __name__ == "__main__":
    main()
