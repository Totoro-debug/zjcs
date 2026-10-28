"""Build Map13 story payloads from the selected, hash-pinned official sources."""
from __future__ import annotations

import ast
import hashlib
import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/13/Source"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map13-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map13/stories.json"
OUTPUT_RECEIPT = ROOT / "assets/reference/official-map/map13-story-runtime-source-manifest.json"
VERSION = "871_156598"


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def read_json(path: Path):
    return json.loads(path.read_bytes())


def parse_inline_object(param: str) -> dict:
    try:
        value = json.loads(param)
    except json.JSONDecodeError:
        normalized = re.sub(r"(?<![\w\"'])([A-Za-z_]\w*)\s*:", r"'\1':", param)
        value = ast.literal_eval(normalized)
    if not isinstance(value, dict):
        raise ValueError("Map13 player condition parameter is not an object: " + param)
    return value


def composite_condition_ids(param: str) -> list[int]:
    value = parse_inline_object(param)
    ids = value.get("ids")
    if not isinstance(ids, list) or any(not isinstance(item, int) or item <= 0 for item in ids):
        raise ValueError("Map13 composite player condition IDs are malformed: " + param)
    return ids


def write_json(path: Path, value) -> None:
    raw = (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf8")
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != raw:
        path.write_bytes(raw)


def source_record(path: Path) -> dict:
    raw = path.read_bytes()
    return {"path": path.relative_to(ROOT).as_posix(), "bytes": len(raw), "sha256": sha(raw)}


def json_meta(path: Path) -> None:
    identity = path.relative_to(ROOT).as_posix().removeprefix("unity/")
    guid = hashlib.md5(("zjcs-official:" + identity).encode()).hexdigest()
    raw = (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n"
           "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode()
    meta = path.with_name(path.name + ".meta")
    if meta.exists() and meta.read_bytes() != raw:
        raise ValueError("Refusing to replace unrelated Unity metadata: " + str(meta))
    if not meta.exists():
        meta.write_bytes(raw)


def story_lines(story_name: str, group_id: str, rows: list, localization: dict) -> list:
    lines = []
    for row in sorted(rows, key=lambda item: item["Order"]):
        order = int(row["Order"])
        key = f"dialog_{story_name}_{group_id}_{order}"
        text = localization.get(key)
        if not text:
            raise ValueError("Official Map13 dialogue localization is missing: " + key)
        lines.append({
            "Order": order,
            "DialogType": "Voice" if int(row.get("DialogType", 0)) == 1 else "Text",
            "Text": text,
            "VoiceEventKey": row.get("VoiceEventKey", ""),
            "RoleId": int(row.get("RoleId", 0)),
            "RoleFaceType": row.get("RoleFaceType", ""),
            "PlayablePath": row.get("PlayablePath", ""),
        })
    return lines


def normalize_story(name: str, graph: dict, metadata: dict, dialogues: dict,
                    localization: dict, subgraph_names: set[str]) -> dict:
    nodes = []
    for node_key, source in sorted(graph["nodes"].items(), key=lambda item: int(item[0])):
        ports = []
        for port_name, port in source.get("ports", {}).items():
            connections = [{"nodeId": int(connection["node"]), "port": connection["port"]}
                           for connection in port.get("connections", []) if connection.get("node")]
            ports.append({"name": port_name, "connections": connections})
        position = source.get("Position") or source.get("Pos") or {}
        destination = source.get("Destination") or {}
        target = source.get("Target") or {}
        move = source.get("MoveBehaviour") or {}
        map_logic_states = [
            {"Tag": row.get("LoEntTag", ""), "State": int(row.get("State", 0))}
            for row in source.get("MapLoEntTagDatas", [])
        ]
        playable_guids = [row.get("mAssetGuid", "") for row in source.get("PlayablesProxy", [])]
        player_condition_type = source.get("Type", "") if source.get("_type") == "PlayerConditionNode" else ""
        player_condition_param = None
        player_condition_ids = []
        if player_condition_type:
            player_condition_param = parse_inline_object(source.get("Param") or "{}")
            if player_condition_type == "CompositeOr":
                player_condition_ids = composite_condition_ids(source.get("Param") or "{}")
        node = {
            "id": int(node_key),
            "type": source.get("_type", ""),
            "sourceJson": json.dumps(source, ensure_ascii=False, separators=(",", ":")),
            "dialogId": int(source.get("DialogId", -1)),
            "awardId": int(source.get("AwardId", 0)),
            "fixedRewardId": int(source.get("FixedRewardId", source.get("AwardId", 0))),
            "handInItemId": int(source.get("HandleItemId", 0)),
            "storyName": source.get("StoryName", source.get("Name", "")),
            "subgraphName": source.get("subgraph_name", source.get("SubgraphName", "")),
            "storyStatus": int(source.get("Status", source.get("State", 0))),
            "entityState": int(source.get("State", 0)),
            "count": int(source.get("Count", 0)),
            "playerLevel": int(source.get("Level", 0)),
            "compareOption": int(source.get("Option", -1)),
            "compareTargetInt": int(target.get("mInt", 0)) if isinstance(target, dict) else 0,
            "entityClassId": int(source.get("EntityClassId", 0)),
            "entityType": int(source.get("EntityType", 0)),
            "positionX": int(position.get("X", position.get("x", 0))),
            "positionY": int(position.get("Y", position.get("y", 0))),
            "destinationX": int(destination.get("X", 0)),
            "destinationY": int(destination.get("Y", 0)),
            "seconds": float(source.get("Seconds", 0)),
            "moveSpeed": float(move.get("Speed", source.get("Speed", 4))),
            "range": int(source.get("Range", 1)),
            "loEntityId": int(source.get("LoEntityId", 0)),
            "entityTag": source.get("Tag", source.get("LoEntTag", "")),
            "needHideUi": bool(source.get("NeedHideUi", source.get("IsHideOtherUI", 0))),
            "needShowUi": bool(source.get("NeedShowUi", 0)),
            "viewSizeOffset": float(source.get("ViewSizeOffset", 0)),
            "duration": float(source.get("Duration", 0)),
            "unlockRadius": int(source.get("UnlockRadius", 0)),
            "playableAssetGuid": playable_guids[0] if playable_guids else "",
            "playableAssetGuids": playable_guids,
            "playableKeys": source.get("Playables", []),
            "playableDuration": float(source.get("PlayableDura", -1))
                if source.get("_type") == "PlayPlayableNode" else float(source.get("Duration", 0)),
            "waitPlayableStop": bool(source.get("IsWaitPlayableStop", False)),
            "mapLogicStates": map_logic_states,
            "endAsStoryFinish": bool(source.get("EndAsStoryFinish", 0)),
            "options": source.get("Options", []),
            "removeEntityClassIds": [int(row.get("EntityClassId", 0))
                                     for row in source.get("RemoveEntities", [])],
            "ports": ports,
        }
        if player_condition_type:
            node.update({
                "playerConditionType": player_condition_type,
                "playerConditionParam": player_condition_param,
                "playerConditionIds": player_condition_ids,
                "playerConditionNot": int(source.get("Not", 0)),
            })
        nodes.append(node)

    starts = [node["id"] for node in nodes if node["type"] == "StartStoryNode"]
    if len(starts) != 1:
        raise ValueError(f"Official Map13 story must have exactly one start node: {name}")
    groups = [
        {"Id": int(group_id), "Lines": story_lines(name, group_id, rows, localization)}
        for group_id, rows in sorted(dialogues.get(name, {}).items(), key=lambda item: int(item[0]))
    ]
    return {
        "key": name,
        "maxCount": int(metadata.get("MaxCount", -1)),
        "entryNodeId": starts[0],
        "graphJson": json.dumps(graph, ensure_ascii=False, separators=(",", ":")),
        "nodes": nodes,
        "dialogueGroups": groups,
        "isSubgraph": name in subgraph_names,
    }


def mainline_tasks(rows: list[dict], map_id: int) -> list[dict]:
    """Follow original NextIds, including intervening non-region task 1355."""
    by_id = {r["ClassId"]: r for r in rows if r["TaskType"] == 10}
    seeds = {i for i, r in by_id.items() if r["FinishCondition"].get("MapClassId") == map_id}
    if not seeds: raise ValueError("No original map mainline tasks")
    reachable = {}
    for seed in seeds:
        queue, seen = [seed], set()
        while queue:
            current = queue.pop(0)
            if current in seen: continue
            seen.add(current)
            row = by_id[current]
            target_map = row["FinishCondition"].get("MapClassId")
            if target_map is not None and target_map != map_id: continue
            reachable[current] = row
            for child in row["NextIds"]:
                if child not in by_id: raise ValueError("Broken original mainline task edge: " + str(child))
                queue.append(child)
    if not seeds <= set(reachable): raise ValueError("Incomplete original mainline task chain")
    return sorted(reachable.values(), key=lambda r: (r["Order"], r["ClassId"]))


def condition_closure(talks: list[dict], conditions: list[dict], extra_ids=()) -> list[dict]:
    by_id = {r["ClassId"]: r for r in conditions}
    pending = [*extra_ids, *[i for r in talks for i in (r.get("Condition") or {}).get("Ids", [])]]
    found = {}
    while pending:
        identity = pending.pop()
        if identity in found: continue
        if identity not in by_id: raise ValueError("Missing original MapTalk condition: " + str(identity))
        found[identity] = by_id[identity]
        pending.extend((by_id[identity].get("Condition") or {}).get("Ids", []))
    return [found[i] for i in sorted(found)]


def build() -> dict:
    source_receipt = read_json(SOURCE_RECEIPT)
    if source_receipt.get("manifest") != VERSION or source_receipt.get("mapClassId") != 13:
        raise ValueError("Map13 source receipt does not match the selected official version.")
    inputs = {name: SOURCE / name for name in
              ("stories.json", "tables.json", "dialogues.json", "localization.json")}
    expected = {Path(row["path"]).name: row["sha256"] for row in source_receipt["outputs"]}
    for name, path in inputs.items():
        if not path.is_file() or sha(path.read_bytes()) != expected.get(name):
            raise ValueError("Pinned Map13 source is missing or changed: " + name)

    graphs = read_json(inputs["stories.json"])
    tables = read_json(inputs["tables.json"])
    dialogues = read_json(inputs["dialogues.json"])
    localization = read_json(inputs["localization.json"])
    dependencies_path = SOURCE / "story-dependencies.json"
    dependencies_receipt = ROOT / "assets/reference/official-map/map13-story-dependencies-source-manifest.json"
    dependencies = read_json(dependencies_path)
    dep_receipt = read_json(dependencies_receipt)
    if dependencies["manifest"] != VERSION or dep_receipt["outputs"][0]["sha256"] != sha(dependencies_path.read_bytes()):
        raise ValueError("Map13 shared story dependencies differ from verified source")
    for row in dep_receipt["inputs"]:
        if sha((ROOT / row["path"]).read_bytes()) != row["sha256"]:
            raise ValueError("Map13 dependency input changed: " + row["path"])
    if set(graphs) & set(dependencies["graphs"]): raise ValueError("Duplicate Map13 shared graph identity")
    graphs.update(dependencies["graphs"])
    dialogues.update(dependencies["dialogues"])
    for key, text in dependencies["localization"].items():
        if key in localization and localization[key] != text: raise ValueError("Conflicting original Chinese text: " + key)
        localization[key] = text
    templates_path = SOURCE / "templates.json"
    if sha(templates_path.read_bytes()) != expected[templates_path.name]: raise ValueError("Pinned Map13 EC templates changed")
    templates = read_json(templates_path)
    if set(templates) & set(dependencies["entityTemplates"]): raise ValueError("Duplicate original EC template identity")
    templates.update(dependencies["entityTemplates"])
    inputs.update(dependencies=dependencies_path, dependenciesReceipt=dependencies_receipt, templates=templates_path)
    story_rows = {row["StoryName"]: row for row in tables["story"]
                  if row.get("ResPath", "").startswith("Map13Story/") or row["StoryName"] in dependencies["graphs"]}
    missing_graphs = set(story_rows) - set(graphs)
    if missing_graphs:
        raise ValueError("Map13 story table references missing graphs: " + repr(sorted(missing_graphs)))

    subgraph_names = {
        node.get("subgraph_name", node.get("SubgraphName", ""))
        for graph in graphs.values()
        for node in graph.get("nodes", {}).values()
        if node.get("_type") == "SubgraphRunNode"
        and node.get("subgraph_name", node.get("SubgraphName", ""))
    }
    if subgraph_names - set(graphs):
        raise ValueError("Map13 story subgraph closure is incomplete: "
                         + repr(sorted(subgraph_names - set(graphs))))

    records = [normalize_story(name, graph, story_rows.get(name, {}), dialogues, localization, subgraph_names)
               for name, graph in sorted(graphs.items())]
    main_stories = [row for row in records if not row["isSubgraph"]]
    subgraphs = [row for row in records if row["isSubgraph"]]
    condition_rows = {row["ClassId"]: row for row in tables["conditions"]}
    condition_ids = set()
    for story in records:
        for node in story["nodes"]:
            if node["type"] != "PlayerConditionNode":
                continue
            if not node["playerConditionType"]:
                continue
            condition_ids.update(node["playerConditionIds"])
    player_conditions = []
    while condition_ids:
        condition_id = condition_ids.pop()
        row = condition_rows.get(condition_id)
        if row is None:
            raise ValueError("Map13 story references a missing official condition: " + str(condition_id))
        player_conditions.append(row)
        if row["Condition"].get("_type") == "CompositeOrCondition":
            condition_ids.update(set(row["Condition"].get("Ids", [])) -
                                 {value["ClassId"] for value in player_conditions})
    player_conditions.sort(key=lambda row: row["ClassId"])
    logic_tags = [row for row in tables["Map/map_logic_entity_tag"] if row["MapClass"] == 13]
    role_ids = {line["RoleId"] for story in records for group in story["dialogueGroups"]
                for line in group["Lines"] if line["RoleId"] > 0}
    roles = [row for row in tables["Dialog/dialog_role"] if row["RoleId"] in role_ids]
    if {row["RoleId"] for row in roles} != role_ids:
        raise ValueError("Map13 dialogue references a missing official role.")
    option_rows = [row for row in tables["story_option_dialog"] if row["StoryName"] in graphs]
    fixed_rows = [row for row in tables["fixed_reward"]
                  if any(node["type"] == "GetFixAwardNode" and node["awardId"] == row["Id"]
                         for story in records for node in story["nodes"])]
    hand_in_ids = {node["handInItemId"] for story in records for node in story["nodes"]
                   if node["type"] == "HandInItemNode"}
    hand_in_rows = [row for row in tables["hand_in_item"] if row["Id"] in hand_in_ids]
    if {row["Id"] for row in hand_in_rows} != hand_in_ids:
        raise ValueError("Map13 story references a missing official hand-in item configuration.")
    access_condition_ids = sorted({r["AccessibleConditionId"] for r in tables["Map/map_region"]
                                   if r["ClassId"] == 13 and r["AccessibleConditionId"] > 0})
    runtime = {
        "manifest": VERSION,
        "mapClassId": 13,
        "sourceSha256": sha(inputs["stories.json"].read_bytes()),
        "dialogRoles": [{"RoleId": row["RoleId"], "EntityClassId": row["EntityClassId"],
                         "RoleName": row["RoleName"], "PortraitPath": row["PortraitPath"]}
                        for row in roles],
        "mapLogicTags": logic_tags,
        "playerConditions": player_conditions,
        "mapTalkEntries": dependencies["mapTalkEntries"],
        "mapTalkConditions": condition_closure(dependencies["mapTalkEntries"], tables["conditions"], access_condition_ids),
        "requiredAccessConditionIds": access_condition_ids,
        "storyOptions": option_rows,
        "fixedRewards": [{"Id": row["Id"], "CanGetTimes": row["CanGetTimes"],
                           "Items": [{"ItemId": int(item_id), "Count": count}
                                     for item_id, count in row["Reward"].items()]}
                          for row in fixed_rows],
        "handInItems": [{"Id": row["Id"], "CanHandTimes": row["CanHandTimes"],
                         "Items": [{"ItemId": int(item_id), "Count": count}
                                   for item_id, count in row["Items"].items()]}
                        for row in hand_in_rows],
        "nativeDialogs": [],
        "playables": [],
        "stories": main_stories,
        "subgraphs": subgraphs,
        "entityTemplates": [{"EntityClassId": int(key), "Components": [
            {"Type": name, "Json": json.dumps(fields, ensure_ascii=False, separators=(",", ":"))}
            for name, fields in value.items()]} for key, value in sorted(templates.items(), key=lambda p: int(p[0]))],
        "quickBuys": dependencies["quickBuys"],
        "mainlineTasks": mainline_tasks(tables["task"], 13),
    }
    write_json(OUTPUT, runtime)
    json_meta(OUTPUT)
    receipt = {
        "manifest": VERSION,
        "mapClassId": 13,
        "operation": "Preserve official Map13 story graphs, subgraph closure, dialogue, roles, tags, options, and configured fixed rewards for the shared offline runtime.",
        "inputs": [source_record(path) for path in [SOURCE_RECEIPT, *inputs.values(), Path(__file__)]],
        "outputs": [source_record(OUTPUT), source_record(OUTPUT.with_name(OUTPUT.name + ".meta"))],
        "counts": {"graphs": len(records), "mainStories": len(main_stories), "subgraphs": len(subgraphs),
                   "nodes": sum(len(row["nodes"]) for row in records),
                   "dialogueGroups": sum(len(row["dialogueGroups"]) for row in records),
                   "dialogueLines": sum(len(group["Lines"]) for row in records for group in row["dialogueGroups"]),
                   "dialogRoles": len(roles), "mapLogicTags": len(logic_tags),
                   "playerConditions": len(player_conditions),
                   "fixedRewards": len(fixed_rows), "handInItems": len(hand_in_rows),
                   "mapTalkEntries": len(runtime["mapTalkEntries"]), "mapTalkConditions": len(runtime["mapTalkConditions"]),
                   "entityTemplates": len(runtime["entityTemplates"]), "mainlineTasks": len(runtime["mainlineTasks"]),
                   "quickBuys": len(runtime["quickBuys"]), "requiredAccessConditions": len(access_condition_ids)},
        "pendingRuntimeBindings": ["official playables", "native dialog UI", "puzzle and witch runtime result bindings"],
    }
    write_json(OUTPUT_RECEIPT, receipt)
    print(json.dumps(receipt["counts"], ensure_ascii=False, sort_keys=True))
    return receipt


if __name__ == "__main__":
    build()
