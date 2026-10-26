"""Build the small Map10 battle and story payloads consumed by Unity runtime."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk
from official_tables import Constructors


ROOT = Path(__file__).resolve().parents[1]
BATTLE_ROOT = ROOT / "unity/Assets/Official/World/Map/10"
ADVENTURE_PATH = ROOT / "assets/official/adventure.json"
STORY_SOURCE_MANIFEST_PATH = ROOT / "assets/reference/official-map/story-source-manifest.json"
LOCALIZATION_PATH = ROOT / "assets/official/map-localization.json"
PLAYABLES_PATH = ROOT / "assets/official/map-playables.json"
PLAYABLES_SOURCE_MANIFEST_PATH = ROOT / "assets/reference/official-map/playables-source-manifest.json"
RESOURCE_ROOT = BATTLE_ROOT / "Resources/Map10"
RUNTIME_BATTLE_PATH = RESOURCE_ROOT / "battle-runtime.json"
RUNTIME_STORY_PATH = RESOURCE_ROOT / "stories.json"
RUNTIME_MANIFEST_PATH = ROOT / "assets/reference/official-map/map10-runtime-source-manifest.json"


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def named_string(value, name):
    match = re.search(r"(?:^|[,{])\s*" + re.escape(name) + r"\s*:\s*'([^']*)'", value or "")
    return match.group(1) if match else ""


def named_int(value, name):
    match = re.search(r"(?:^|[,{])\s*" + re.escape(name) + r"\s*:\s*(-?\d+)", value or "")
    return int(match.group(1)) if match else 0


def native_dialog_action(row, profession_ids):
    action_type = row["ActionType"]
    params = row.get("ActionParam") or ""
    result = {"ActionType": action_type, "ActionParam": params}
    if action_type == 0:
        return result
    if action_type == 1:
        profession = named_string(params, "Profession")
        if profession not in profession_ids:
            raise ValueError("Map10 dialog profession action is malformed: " + params)
        result.update({"Profession": profession_ids[profession],
                       "ReturnDialogId": named_int(params, "ReturnDialogId"),
                       "ReturnNodeId": named_int(params, "ReturnNodeId")})
    elif action_type == 4:
        award = re.search(r"AwardItems\s*:\s*\{([^}]*)\}", params)
        items = [] if award is None else [
            {"ItemId": int(item_id), "Count": int(count)}
            for item_id, count in re.findall(r"(\d+)\s*:\s*(\d+)", award.group(1))]
        if not items:
            raise ValueError("Map10 dialog award action has no fixed items: " + params)
        result.update({"AwardItems": items,
                       "NeedPopup": bool(re.search(r"NeedPopup\s*:\s*true", params)),
                       "AwardLimitedDialogId": named_int(params, "AwardLimitedDialogId"),
                       "AwardLimitedNodeId": named_int(params, "AwardLimitedNodeIdt")
                       or named_int(params, "AwardLimitedNodeId")})
    elif action_type == 6:
        result["Story"] = named_string(params, "Story")
        if not result["Story"]:
            raise ValueError("Map10 dialog story action is malformed: " + params)
    elif action_type in (10, 11):
        playable_key = "AppearPlayable" if action_type == 10 else "DisappearPlayable"
        result.update({"NpcEntityClassId": named_int(params, "NpcEntityClassId"),
                       "PlayableKey": named_string(params, playable_key)})
        if result["NpcEntityClassId"] <= 0:
            raise ValueError("Map10 dialog NPC action is malformed: " + params)
    else:
        raise ValueError("Unsupported action in fixed Map10 dialog: " + str(action_type))
    return result


def build():
    battle_source_path = BATTLE_ROOT / "map10-battles.json"
    battle_source_manifest_path = BATTLE_ROOT / "battle-source-manifest.json"
    battle_source_manifest = json.loads(battle_source_manifest_path.read_text(encoding="utf-8"))
    battle_source = json.loads(battle_source_path.read_text(encoding="utf-8"))
    battle_source_hash = sha256(battle_source_path)
    if battle_source_manifest.get("manifest") != "854_156383" \
            or battle_source_manifest.get("mapClassId") != 10 \
            or battle_source_manifest.get("output", {}).get("sha256") != battle_source_hash:
        raise ValueError("Pinned Map10 battle source does not match its source manifest.")

    scenes = battle_source.get("battleScenes")
    if not isinstance(scenes, dict):
        raise ValueError("Map10 battle scenes are not keyed by GameId.")
    battle_runtime = {
        "manifest": battle_source["manifest"],
        "mapClassId": battle_source["mapClassId"],
        "sourceSha256": battle_source_hash,
        "stages": battle_source["stages"],
        "monsterRosters": battle_source["monsterRosters"],
        "battleScenes": [
            dict({"GameId": int(game_id)}, **scene)
            for game_id, scene in sorted(scenes.items(), key=lambda pair: int(pair[0]))
        ],
    }
    if len(battle_runtime["stages"]) != 21 or len(battle_runtime["monsterRosters"]) != 21:
        raise ValueError("Map10 battle data no longer contains the pinned 21 stage and roster records.")

    adventure = json.loads(ADVENTURE_PATH.read_text(encoding="utf-8"))
    adventure_hash = sha256(ADVENTURE_PATH)
    source_manifest = json.loads(STORY_SOURCE_MANIFEST_PATH.read_text(encoding="utf-8"))
    if source_manifest.get("manifest") != "854_156383" or source_manifest.get("adventure_sha256") != adventure_hash:
        raise ValueError("Pinned official adventure data does not match its story source manifest.")

    playable_source = json.loads(PLAYABLES_PATH.read_text(encoding="utf-8"))
    playable_manifest = json.loads(PLAYABLES_SOURCE_MANIFEST_PATH.read_text(encoding="utf-8"))
    playable_hash = sha256(PLAYABLES_PATH)
    if playable_manifest.get("manifest") != "854_156383" \
            or playable_manifest.get("outputs", {}).get("map-playables.json") != playable_hash:
        raise ValueError("Pinned official Map10 playables do not match their source manifest.")
    playable_scenes = playable_source.get("scenes")
    playable_registry = playable_source.get("registry")
    if not isinstance(playable_scenes, dict) or not isinstance(playable_registry, dict):
        raise ValueError("Pinned official playable scenes or registry are missing.")

    story_names = (
        "FirstEnterMap",
        "FirstBattle",
        "FirstMeetChaos",
        "SavingChick",
        "ChickYell",
        "ChickYell2",
        "ChickChat",
        "FangjianbeiActive",
        "UnlockFangBei",
    )
    story_table = {row["StoryName"]: row for row in adventure["tables"]["story"]}
    if any(name not in story_table for name in story_names):
        raise ValueError("Official Map10 story metadata is incomplete.")

    story_records = []
    option_keys = set()
    for name in story_names:
        graph = adventure["stories"].get(name)
        dialogue = adventure["dialogues"].get(name, {})
        if graph is None or not graph.get("nodes") or not isinstance(dialogue, dict):
            raise ValueError("Official Map10 story graph is missing: " + name)
        groups = []
        for group_id, lines in sorted(dialogue.items(), key=lambda pair: int(pair[0])):
            groups.append({"Id": int(group_id), "Lines": lines})
        nodes = []
        for node_id, source in sorted(graph["nodes"].items(), key=lambda pair: int(pair[0])):
            source_ports = source.get("ports", {})
            ports = [
                {"name": port_name,
                 "connections": [{"nodeId": int(connection["node"]), "port": connection["port"]}
                                 for connection in port.get("connections", []) if connection.get("node")]}
                for port_name, port in source_ports.items()
            ]
            position = source.get("Pos", {})
            position_size = source.get("Size") or {}
            destination = source.get("Destination", {})
            move_target = source.get("MoveToTargetPos", {})
            offset = source.get("Offset", {})
            move = source.get("MoveBehaviour", {})
            compare_target = source.get("Target", {})
            story_playable = source.get("StoryPlayable") or {}
            remove_playables = [item.get("StoryPlayableSelect") or item.get("StoryPlayable") or {}
                                for item in source.get("RemoveEntities", [])]
            remove_playable = remove_playables[0] if remove_playables else {}
            remove_proxy = remove_playable.get("CustomAssetProxy") or {}
            remove_resource = remove_proxy.get("mProxyRes") or {}
            if source.get("_type") == "OptionDialogNode":
                option_keys.update((name, int(option_id)) for option_id in source.get("Options", []))
            nodes.append({
                "id": int(node_id),
                "type": source.get("_type", ""),
                "dialogId": source.get("DialogId", -1),
                "storyName": source.get("StoryName", source.get("Name", "")),
                "count": source.get("Count", 0),
                "compareOption": source.get("Option", -1),
                "compareTargetInt": compare_target.get("mInt", 0) if isinstance(compare_target, dict) else 0,
                "storyStatus": source.get("Status", source.get("State", 0)),
                "entityClassId": source.get("EntityClassId", 0),
                "entityType": source.get("EntityType", 0),
                "positionX": position.get("X", 0),
                "positionY": position.get("Y", 0),
                "positionWidth": position_size.get("x", 0),
                "positionHeight": position_size.get("y", 0),
                "destinationX": destination.get("X", 0),
                "destinationY": destination.get("Y", 0),
                "moveTargetX": move_target.get("x", 0),
                "moveTargetY": move_target.get("y", 0),
                "seconds": source.get("Seconds", 0),
                "moveSpeed": move.get("Speed", 4),
                "range": source.get("Range", 1),
                "selectGetType": source.get("SelectGetType", -1),
                "loEntityId": source.get("LoEntityId", 0),
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
                "playableAssetGuid": next((value.get("mAssetGuid", "")
                                            for value in source.get("PlayablesProxy", [])
                                            if value.get("mAssetGuid")), ""),
                "playableKey": story_playable.get("PreDefineKey", ""),
                "removePlayableKey": remove_playable.get("PreDefineKey", ""),
                "removePlayableAssetGuid": remove_resource.get("mAssetGuid", ""),
                "playableDuration": float(playable_scenes.get(next((value.get("mAssetGuid", "")
                    for value in source.get("PlayablesProxy", []) if value.get("mAssetGuid")), ""), {})
                    .get("duration", 0)),
                "waitPlayableStop": bool(source.get("IsWaitPlayableStop", False)),
                "playableKeys": source.get("Playables", []),
                "mapLogicStates": [
                    {"Tag": state.get("LoEntTag", ""), "State": state.get("State", 0)}
                    for state in source.get("MapLoEntTagDatas", [])
                ],
                "options": source.get("Options", []),
                "removeEntityClassIds": [item.get("EntityClassId", 0)
                                         for item in source.get("RemoveEntities", [])],
                "ports": ports,
            })
        start_nodes = [node for node in nodes if node["type"] == "StartStoryNode"]
        if len(start_nodes) != 1:
            raise ValueError("Official Map10 story must have one StartStoryNode: " + name)
        story_records.append({
            "key": name,
            "maxCount": story_table[name]["MaxCount"],
            "entryNodeId": start_nodes[0]["id"],
            "graphJson": json.dumps(graph, ensure_ascii=False, separators=(",", ":")),
            "nodes": nodes,
            "dialogueGroups": groups,
        })

    map_logic_tags = [row for row in adventure["tables"]["Map/map_logic_entity_tag"]
                      if row["MapClass"] == 10]
    placement_ids = {row["LogicId"] for row in map_logic_tags}
    if len(placement_ids) != len(map_logic_tags):
        raise ValueError("Map10 official logic entity tags do not uniquely identify placements.")
    talk_tag_keys = {(f"entity_{row['EntityClass']}_name", row["Tag"])
                     for row in map_logic_tags}
    map_talk_rows = [row for row in adventure["tables"]["Map/map_talk"]
                     if (row["EntityId"], row["Tag"]) in talk_tag_keys]
    map_talk_rows.sort(key=lambda row: next(
        index for index, candidate in enumerate(adventure["tables"]["Map/map_talk"])
        if candidate is row))
    for tag in map_logic_tags:
        if tag["LogicId"] <= 0 or tag["EntityClass"] <= 0 or not tag["Tag"]:
            raise ValueError("Map10 official logic entity tag is malformed.")

    localization = json.loads(LOCALIZATION_PATH.read_text(encoding="utf-8"))
    dialog_table = adventure["tables"].get("Dialog/dialog_node", [])
    dialog_ids = sorted({row["DialogId"] for row in map_talk_rows if row.get("DialogId", 0) > 0})
    if dialog_ids != [1, 2, 3]:
        raise ValueError("Map10 MapTalk dialog ids differ from the pinned fixed-version set: " + repr(dialog_ids))
    profession_ids = {name: int(value) for value, name in adventure["enums"]["Profession"].items()}
    native_dialogs = []
    native_role_ids = set()
    for dialog_id in dialog_ids:
        rows = sorted((row for row in dialog_table if row["DialogId"] == dialog_id),
                      key=lambda row: row["NodeId"])
        if not rows:
            raise ValueError("Fixed Map10 dialog node table is missing: " + str(dialog_id))
        node_records = []
        for row in rows:
            node_id = int(row["NodeId"])
            text_key = f"dialog_node_{dialog_id}_{node_id}"
            text = localization.get(text_key, "")
            if row["NodeType"] in (0, 1) and not text:
                raise ValueError("Fixed Map10 dialog localization is missing: " + text_key)
            action = native_dialog_action(row, profession_ids)
            role_id = int(row.get("RoleId", 0))
            if role_id > 0:
                native_role_ids.add(role_id)
            filter_profession = named_string(row.get("OptionFilterParam", ""), "Profession")
            if row.get("OptionFilterType", 0) and filter_profession not in profession_ids:
                raise ValueError("Fixed Map10 dialog option filter is malformed: " + text_key)
            node_records.append({
                "NodeId": node_id,
                "NodeType": int(row["NodeType"]),
                "NextNodes": [int(value) for value in row.get("NextNodes", [])],
                "RoleId": role_id,
                "RoleFaceType": row.get("RoleFaceType", ""),
                "DialogType": int(row.get("DialogType", 0)),
                "VoiceEventKey": row.get("VoiceEventKey", ""),
                "TextKey": text_key if text else "",
                "Text": text,
                "OptionIconPath": row.get("OptionIconPath", ""),
                "OptionFilterType": int(row.get("OptionFilterType", 0)),
                "OptionFilterProfession": profession_ids.get(filter_profession, 0),
                "IsSave": bool(row.get("IsSave", False)),
                **action,
            })
        if rows[0]["NodeId"] != 1:
            raise ValueError("Fixed Map10 dialog entry node differs from its pinned graph: " + str(dialog_id))
        native_dialogs.append({"Id": int(dialog_id), "EntryNodeId": 1, "Nodes": node_records})

    playable_keys = sorted({node["PlayableKey"] for dialog in native_dialogs for node in dialog["Nodes"]
                            if node.get("PlayableKey")})
    playable_guids = {playable_registry[key] for key in playable_keys if key in playable_registry}
    playable_guids.update(node["playableAssetGuid"] for story in story_records for node in story["nodes"]
                          if node.get("playableAssetGuid"))
    if len(playable_guids) < len(playable_keys):
        missing = sorted(key for key in playable_keys if key not in playable_registry)
        raise ValueError("Fixed Map10 dialog playable is absent from the official registry: " + repr(missing))
    playable_records = []
    for guid in sorted(playable_guids):
        scene = playable_scenes.get(guid)
        if scene is None or float(scene.get("duration", 0)) <= 0:
            raise ValueError("Fixed Map10 playable has no finite official duration: " + guid)
        keys = [key for key in playable_keys if playable_registry.get(key) == guid]
        playable_records.append({"AssetGuid": guid, "Keys": keys, "Duration": float(scene["duration"])})

    condition_ids = {
        condition_id
        for row in map_talk_rows
        for condition_id in row.get("Condition", {}).get("Ids", [])
    }
    apk = Apk()
    condition_object = apk.asset("config_luban", "Assets/Config/Luban/conditions.g.bytes")
    condition_payload = condition_object.read().m_Script.encode("utf-8", "surrogateescape")
    condition_rows = Constructors().table(condition_payload, "TbConditionInfo")
    map_talk_conditions = [row for row in condition_rows if row["ClassId"] in condition_ids]
    if {row["ClassId"] for row in map_talk_conditions} != condition_ids:
        raise ValueError("Map10 MapTalk references a missing official condition.")
    map_talk_conditions.sort(key=lambda row: row["ClassId"])

    options_by_key = {(row["StoryName"], row["Id"]): row
                      for row in adventure["tables"]["story_option_dialog"]}
    missing_options = option_keys - set(options_by_key)
    if missing_options:
        raise ValueError("Official Map10 story option is missing: " + repr(sorted(missing_options)))
    story_options = [options_by_key[key] for key in sorted(option_keys)]

    role_ids = {
        line["RoleId"]
        for story in story_records
        for group in story["dialogueGroups"]
        for line in group["Lines"]
        if line.get("RoleId", 0) > 0
    }
    role_ids.update(native_role_ids)
    role_bundle = "config_luban_dialog"
    role_path = "Assets/Config/Luban/Dialog/dialog_role.g.bytes"
    role_object = apk.asset(role_bundle, role_path)
    role_payload = role_object.read().m_Script.encode("utf-8", "surrogateescape")
    role_rows = Constructors().table(role_payload, "TbDialogRoleInfo")
    role_records = [row for row in role_rows if row["RoleId"] in role_ids]
    if {row["RoleId"] for row in role_records} != role_ids:
        raise ValueError("Map10 dialogues reference a missing official dialog role.")
    role_records.sort(key=lambda row: row["RoleId"])
    if [(row["RoleId"], row["EntityClassId"], row["PortraitPath"]) for row in role_records] != [
            (840200, 8402, "UI/Portrait/Npc_Portrait_8402"),
            (840401, 8404, "UI/Portrait/Npc_Portrait_8404"),
            (840402, 8404, "UI/Portrait/Npc_Portrait_8404"),
            (841700, 8417, "UI/Portrait/Npc_Portrait_8417")]:
        raise ValueError("Pinned Map10 dialog roles differ from fixed-version portrait bindings.")
    dialog_roles = [
        {"RoleId": row["RoleId"], "EntityClassId": row["EntityClassId"],
         "RoleName": row["RoleName"], "PortraitPath": row["PortraitPath"]}
        for row in role_records
    ]
    role_source = dict(apk.provenance(role_object), object=role_path,
                       raw_sha256=hashlib.sha256(role_object.get_raw_data()).hexdigest(),
                       payload_sha256=hashlib.sha256(role_payload).hexdigest())
    story_runtime = {
        "manifest": "854_156383",
        "mapClassId": 10,
        "sourceSha256": adventure_hash,
        "dialogRoles": dialog_roles,
        "mapLogicTags": map_logic_tags,
        "mapTalkEntries": map_talk_rows,
        "mapTalkConditions": map_talk_conditions,
        "storyOptions": story_options,
        "nativeDialogs": native_dialogs,
        "playables": playable_records,
        "stories": story_records,
    }

    write_json(RUNTIME_BATTLE_PATH, battle_runtime)
    write_json(RUNTIME_STORY_PATH, story_runtime)
    manifest = {
        "manifest": "854_156383",
        "mapClassId": 10,
        "inputs": {
            "battles": {
                "path": battle_source_path.relative_to(ROOT).as_posix(),
                "sha256": battle_source_hash,
                "sourceManifest": battle_source_manifest_path.relative_to(ROOT).as_posix(),
                "sourceManifestSha256": sha256(battle_source_manifest_path),
            },
            "adventure": {
                "path": ADVENTURE_PATH.relative_to(ROOT).as_posix(),
                "sha256": adventure_hash,
                "sourceManifest": STORY_SOURCE_MANIFEST_PATH.relative_to(ROOT).as_posix(),
                "sourceManifestSha256": sha256(STORY_SOURCE_MANIFEST_PATH),
            },
            "localization": {
                "path": LOCALIZATION_PATH.relative_to(ROOT).as_posix(),
                "sha256": sha256(LOCALIZATION_PATH),
            },
            "playables": {
                "path": PLAYABLES_PATH.relative_to(ROOT).as_posix(),
                "sha256": playable_hash,
                "sourceManifest": PLAYABLES_SOURCE_MANIFEST_PATH.relative_to(ROOT).as_posix(),
                "sourceManifestSha256": sha256(PLAYABLES_SOURCE_MANIFEST_PATH),
            },
            "dialogRoles": role_source,
            "mapTalkConditions": dict(apk.provenance(condition_object),
                                      object="Assets/Config/Luban/conditions.g.bytes",
                                      raw_sha256=hashlib.sha256(condition_object.get_raw_data()).hexdigest(),
                                      payload_sha256=hashlib.sha256(condition_payload).hexdigest()),
        },
        "outputs": {
            "battles": {
                "path": RUNTIME_BATTLE_PATH.relative_to(ROOT).as_posix(),
                "sha256": sha256(RUNTIME_BATTLE_PATH),
                "stageCount": len(battle_runtime["stages"]),
                "rosterCount": len(battle_runtime["monsterRosters"]),
                "sceneCount": len(battle_runtime["battleScenes"]),
            },
            "stories": {
                "path": RUNTIME_STORY_PATH.relative_to(ROOT).as_posix(),
                "sha256": sha256(RUNTIME_STORY_PATH),
                "names": [record["key"] for record in story_records],
                "dialogRoles": [record["RoleId"] for record in dialog_roles],
                "mapLogicTagCount": len(map_logic_tags),
                "mapTalkEntryCount": len(map_talk_rows),
                "mapTalkConditionIds": [row["ClassId"] for row in map_talk_conditions],
                "storyOptionCount": len(story_options),
                "nativeDialogIds": [record["Id"] for record in native_dialogs],
                "nativeDialogNodeCounts": {
                    str(record["Id"]): len(record["Nodes"]) for record in native_dialogs
                },
                "playables": playable_records,
                "dialogueLineCounts": {
                    record["key"]: sum(len(group["Lines"]) for group in record["dialogueGroups"])
                    for record in story_records
                },
            },
        },
    }
    write_json(RUNTIME_MANIFEST_PATH, manifest)
    print("Map10 runtime data: 21 stages, 21 rosters, 4 GameId scenes, and "
          + str(len(story_records)) + " official stories.")


if __name__ == "__main__":
    build()
