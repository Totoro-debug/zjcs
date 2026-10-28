"""Normalize the pinned Map14 story graphs and dialogue for the Unity runtime."""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/14/Source"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map14-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map14/stories.json"
OUTPUT_RECEIPT = ROOT / "assets/reference/official-map/map14-story-runtime-source-manifest.json"
VERSION = "871_156598"

sys.path.insert(0, str(ROOT / "scripts"))
from prepare_unity_map14_interactions import load_inputs, witch_payload  # noqa: E402
from prepare_unity_map13_story_runtime import (  # noqa: E402
    json_meta,
    normalize_story,
    read_json,
    sha,
    source_record,
    write_json,
)


def build() -> dict:
    source_receipt = read_json(SOURCE_RECEIPT)
    if source_receipt.get("manifest") != VERSION or source_receipt.get("mapClassId") != 14:
        raise ValueError("Map14 source receipt does not match the selected official version.")

    inputs = {name: SOURCE / name for name in
              ("stories.json", "tables.json", "dialogues.json", "localization.json", "templates.json")}
    expected = {Path(row["path"]).name: row["sha256"] for row in source_receipt["outputs"]}
    for name, path in inputs.items():
        if not path.is_file() or sha(path.read_bytes()) != expected.get(name):
            raise ValueError("Pinned Map14 source is missing or changed: " + name)

    graphs, tables, dialogues, localization = (read_json(inputs[name]) for name in
        ("stories.json", "tables.json", "dialogues.json", "localization.json"))
    _, verified_tables, entity_templates, _ = load_inputs()
    if tables != verified_tables:
        raise ValueError("Map14 story and interaction source tables differ")
    witch = witch_payload(tables, entity_templates)
    story_rows = {row["StoryName"]: row for row in tables["story"]
                  if row.get("ResPath", "").startswith("Map14Story/")}
    if len(story_rows) != 74 or set(story_rows) - set(graphs):
        raise ValueError("Map14 story table or graph closure differs from the pinned source.")

    dependency_path = SOURCE / "story-dependencies.json"
    dependency_receipt_path = ROOT / "assets/reference/official-map/map14-story-dependencies-source-manifest.json"
    dependency_receipt = read_json(dependency_receipt_path)
    if dependency_receipt.get("manifest") != VERSION or dependency_receipt.get("mapClassId") != 14:
        raise ValueError("Map14 public story dependency version differs")
    pinned_dependencies = {row["path"]: row["sha256"] for row in dependency_receipt["outputs"]}
    if sha(dependency_path.read_bytes()) != pinned_dependencies.get(dependency_path.relative_to(ROOT).as_posix()):
        raise ValueError("Map14 public story dependency source changed")
    dependency = read_json(dependency_path)
    if set(dependency["graphs"]) & set(graphs):
        raise ValueError("Map14 public story dependency duplicates a map-local graph")
    graphs.update(dependency["graphs"])
    for target, extra in ((dialogues, dependency["dialogues"]),
                          (localization, dependency["localization"])):
        for key, value in extra.items():
            if key in target and target[key] != value:
                raise ValueError("Conflicting original Map14 public story data: " + key)
            target[key] = value
    entity_ids = {row["EntityClassId"] for row in entity_templates}
    for identity, components in dependency["entityTemplates"].items():
        if int(identity) in entity_ids:
            raise ValueError("Duplicated Map14 follower EC template")
        entity_templates.append({"EntityClassId": int(identity), "Components": [
            {"Type": name, "Json": json.dumps(value, ensure_ascii=False, separators=(",", ":"))}
            for name, value in components.items()]})
        entity_ids.add(int(identity))
    all_story_rows = {row["StoryName"]: row for row in tables["story"]}
    for name in dependency["graphs"]:
        if name in all_story_rows:
            story_rows[name] = all_story_rows[name]

    subgraph_names = {
        node.get("subgraph_name", node.get("SubgraphName", ""))
        for graph in graphs.values()
        for node in graph.get("nodes", {}).values()
        if node.get("_type") == "SubgraphRunNode"
        and node.get("subgraph_name", node.get("SubgraphName", ""))
    }
    if subgraph_names - set(graphs):
        raise ValueError("Map14 story subgraph closure is incomplete: "
                         + repr(sorted(subgraph_names - set(graphs))))

    talk_entries = dependency["mapTalkEntries"]
    if any(row.get("TalkId") and row["TalkId"] not in graphs for row in talk_entries):
        raise ValueError("Map14 public MapTalk graph closure is incomplete")
    all_conditions = {row["ClassId"]: row for row in tables["conditions"]}
    pending_conditions = {
        condition_id
        for entry in talk_entries
        for condition_id in (entry.get("Condition") or {}).get("Ids", [])
    }
    condition_ids = set()
    while pending_conditions:
        condition_id = pending_conditions.pop()
        if condition_id in condition_ids:
            continue
        if condition_id not in all_conditions:
            raise ValueError("Map14 MapTalk references a missing official condition: "
                             + str(condition_id))
        condition_ids.add(condition_id)
        condition = all_conditions[condition_id].get("Condition") or {}
        pending_conditions.update(set(condition.get("Ids", [])) - condition_ids)
    talk_conditions = [{"ClassId": condition_id,
                        "Condition": all_conditions[condition_id]["Condition"]}
                       for condition_id in sorted(condition_ids)]

    records = [normalize_story(name, graph, story_rows.get(name, {}), dialogues, localization,
                               subgraph_names)
               for name, graph in sorted(graphs.items())]
    for story in records:
        for node in story["nodes"]:
            if node["type"] == "CanHandInItemNode":
                source = json.loads(node["sourceJson"])
                node["handInItemId"] = int(source.get("HandInItemId", 0))
    main_stories = [row for row in records if not row["isSubgraph"]]
    subgraphs = [row for row in records if row["isSubgraph"]]
    logic_tags = [row for row in tables["Map/map_logic_entity_tag"] if row["MapClass"] == 14]
    role_ids = {line["RoleId"] for story in records for group in story["dialogueGroups"]
                for line in group["Lines"] if line["RoleId"] > 0}
    roles = [row for row in tables["Dialog/dialog_role"] if row["RoleId"] in role_ids]
    if {row["RoleId"] for row in roles} != role_ids:
        raise ValueError("Map14 dialogue references a missing official role.")
    option_rows = [row for row in tables["story_option_dialog"] if row["StoryName"] in graphs]
    fixed_ids = {node["fixedRewardId"] for story in records for node in story["nodes"]
                 if node["type"] in ("GetFixAwardNode", "CanGetFixedRewardNode")}
    fixed_rows = [row for row in tables["fixed_reward"] if row["Id"] in fixed_ids]
    if {row["Id"] for row in fixed_rows} != fixed_ids:
        raise ValueError("Map14 story graph references a missing fixed reward.")
    hand_in_ids = {node["handInItemId"] for story in records for node in story["nodes"]
                   if node["type"] == "HandInItemNode"}
    hand_in_rows = [row for row in tables["hand_in_item"] if row["Id"] in hand_in_ids]
    if {row["Id"] for row in hand_in_rows} != hand_in_ids:
        raise ValueError("Map14 story graph references a missing hand-in configuration.")

    runtime = {
        "manifest": VERSION,
        "mapClassId": 14,
        "sourceSha256": sha(inputs["stories.json"].read_bytes()),
        "dialogRoles": [{"RoleId": row["RoleId"], "EntityClassId": row["EntityClassId"],
                         "RoleName": row["RoleName"], "PortraitPath": row["PortraitPath"]}
                        for row in roles],
        "mapLogicTags": logic_tags,
        "mapTalkEntries": talk_entries,
        "mapTalkConditions": talk_conditions,
        "storyOptions": option_rows,
        "fixedRewards": [{"Id": row["Id"], "CanGetTimes": row["CanGetTimes"],
                           "Items": [{"ItemId": int(item_id), "Count": count}
                                     for item_id, count in row["Reward"].items()]}
                          for row in fixed_rows],
        "handInItems": [{"Id": row["Id"], "CanHandTimes": row["CanHandTimes"],
                         "Items": [{"ItemId": int(item_id), "Count": count}
                                   for item_id, count in row["Items"].items()]}
                        for row in hand_in_rows],
        "quickBuys": dependency["quickBuys"],
        "nativeDialogs": [],
        "playables": [],
        "stories": main_stories,
        "subgraphs": subgraphs,
        "entityTemplates": entity_templates,
        **witch,
    }
    write_json(OUTPUT, runtime)
    json_meta(OUTPUT)
    receipt = {
        "manifest": VERSION,
        "mapClassId": 14,
        "operation": "Normalize the pinned Map14 story graphs, subgraph closure, localized dialogue, roles, options, configured fixed rewards, hand-in item rules, ordered EC templates and map-specific witch results for the shared offline runtime.",
        "inputs": [source_record(path) for path in [SOURCE_RECEIPT, *inputs.values(), Path(__file__), ROOT / "scripts/prepare_unity_map14_interactions.py",
                    ROOT / "scripts/prepare_unity_map11_runtime_data.py",
                    ROOT / "scripts/prepare_unity_map13_story_runtime.py",
                    dependency_path, dependency_receipt_path,
                    ROOT / "scripts/prepare_unity_map14_story_dependencies.py",
                    ROOT / "scripts/prepare_unity_map13_story_dependencies.py"]],
        "outputs": [source_record(OUTPUT), source_record(OUTPUT.with_name(OUTPUT.name + ".meta"))],
        "counts": {"graphs": len(records), "mainStories": len(main_stories),
                   "subgraphs": len(subgraphs), "nodes": sum(len(row["nodes"]) for row in records),
                   "dialogueGroups": sum(len(row["dialogueGroups"]) for row in records),
                   "dialogueLines": sum(len(group["Lines"]) for row in records
                                         for group in row["dialogueGroups"]),
                   "dialogRoles": len(roles), "mapLogicTags": len(logic_tags),
                   "mapTalkEntries": len(talk_entries), "mapTalkConditions": len(talk_conditions),
                   "fixedRewards": len(fixed_rows), "handInItems": len(hand_in_rows),
                   "entityTemplates": len(entity_templates),
                   "quickBuys": len(dependency["quickBuys"]),
                   "followerClasses": len(dependency["followerEntityClassIds"]),
                   "templateComponents": sum(len(row["Components"]) for row in entity_templates),
                   **{key: len(value) for key, value in witch.items()}},
        "pendingRuntimeBindings": ["Map14 map-talk entries and conditions", "native dialogs",
                                    "story playables", "mainline map host"],
    }
    write_json(OUTPUT_RECEIPT, receipt)
    print(json.dumps(receipt["counts"], ensure_ascii=False, sort_keys=True))
    return receipt


if __name__ == "__main__":
    build()
