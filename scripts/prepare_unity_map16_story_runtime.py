"""Build the shared offline story payload for Whale Island from pinned sources."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unity/Assets/Official/World/Map/16/Source/story-source.json"
SOURCE_RECEIPT = ROOT / "assets/reference/official-map/map16-story-source-manifest.json"
OUTPUT = ROOT / "unity/Assets/Game/Resources/Map16/stories.json"
OUTPUT_RECEIPT = ROOT / "assets/reference/official-map/map16-story-runtime-source-manifest.json"
VERSION = "884_156692"
MAP_ID = 16

import sys
sys.path.insert(0, str(ROOT / "scripts"))
from prepare_unity_map13_story_runtime import (  # noqa: E402
    json_meta,
    normalize_story,
    sha,
    write_json,
)


def read_json(path: Path):
    return json.loads(path.read_bytes())


def source_record(path: Path) -> dict:
    raw = path.read_bytes()
    return {"path": path.relative_to(ROOT).as_posix(), "bytes": len(raw), "sha256": sha(raw)}


def build() -> dict:
    receipt = read_json(SOURCE_RECEIPT)
    if receipt.get("manifest") != VERSION or receipt.get("mapClassId") != MAP_ID:
        raise ValueError("Map16 story source receipt does not match the selected official version.")
    expected = {row["path"]: row["sha256"] for row in receipt.get("outputs", [])}
    source_key = SOURCE.relative_to(ROOT).as_posix()
    if not SOURCE.is_file() or sha(SOURCE.read_bytes()) != expected.get(source_key):
        raise ValueError("Pinned Map16 story source is missing or changed.")

    source = read_json(SOURCE)
    if source.get("manifest") != VERSION or source.get("mapClassId") != MAP_ID:
        raise ValueError("Map16 story source payload does not match its receipt.")
    graphs = source.get("graphs") or {}
    story_rows = {row["StoryName"]: row for row in source.get("storyRows", [])}
    if set(story_rows) != set(graphs) or len(graphs) != 7:
        raise ValueError("Map16 story table and graph closure is incomplete.")

    subgraph_names = {
        node.get("subgraph_name", node.get("SubgraphName", ""))
        for graph in graphs.values()
        for node in graph.get("nodes", {}).values()
        if node.get("_type") == "SubgraphRunNode"
        and node.get("subgraph_name", node.get("SubgraphName", ""))
    }
    if subgraph_names - set(graphs):
        raise ValueError("Map16 story subgraph closure is incomplete: "
                         + repr(sorted(subgraph_names - set(graphs))))

    records = [normalize_story(name, graphs[name], story_rows[name], source["dialogues"],
                               source["localization"], subgraph_names)
               for name in sorted(graphs)]
    main_stories = [row for row in records if not row["isSubgraph"]]
    subgraphs = [row for row in records if row["isSubgraph"]]

    role_ids = {line["RoleId"] for story in records for group in story["dialogueGroups"]
                for line in group["Lines"] if line["RoleId"] > 0}
    roles = [row for row in source.get("dialogRoles", []) if row["RoleId"] in role_ids]
    if {row["RoleId"] for row in roles} != role_ids:
        raise ValueError("Map16 dialogue references a missing official role.")
    tags = [row for row in source.get("mapLogicTags", []) if row.get("MapClass") == MAP_ID]
    if len(tags) != len(source.get("mapLogicTags", [])):
        raise ValueError("Map16 source contains a map logic tag for another map.")
    option_rows = [row for row in source.get("storyOptions", []) if row.get("StoryName") in graphs]
    hand_in_rows = source.get("handInItems", [])
    fixed_rows = source.get("fixedRewards", [])

    runtime = {
        "manifest": VERSION,
        "mapClassId": MAP_ID,
        "sourceSha256": sha(SOURCE.read_bytes()),
        "dialogRoles": [{"RoleId": row["RoleId"], "EntityClassId": row["EntityClassId"],
                         "RoleName": row["RoleName"], "PortraitPath": row["PortraitPath"]}
                        for row in roles],
        "mapLogicTags": tags,
        "playerConditions": [],
        "mapTalkEntries": [],
        "mapTalkConditions": [],
        "storyOptions": option_rows,
        "fixedRewards": [{"Id": row["Id"], "CanGetTimes": row.get("CanGetTimes", 0),
                          "Items": [{"ItemId": int(item_id), "Count": count}
                                    for item_id, count in row.get("Reward", {}).items()]}
                         for row in fixed_rows],
        "handInItems": [{"Id": row["Id"], "CanHandTimes": row["CanHandTimes"],
                         "Items": [{"ItemId": int(item_id), "Count": count}
                                   for item_id, count in row.get("Items", {}).items()]}
                        for row in hand_in_rows],
        "nativeDialogs": [],
        "playables": [],
        "mainlineTasks": [],
        "stories": main_stories,
        "subgraphs": subgraphs,
        "entityTemplates": [],
        "witchInfos": [],
        "witchResultEffects": [],
        "witchPeriodicityAwards": [],
        "puzzleRules": [],
        "quickBuys": [],
    }
    write_json(OUTPUT, runtime)
    json_meta(OUTPUT)
    result = {
        "manifest": VERSION,
        "mapClassId": MAP_ID,
        "operation": "Normalize the pinned Whale Island story graphs, subgraph closure, localized dialogue, roles, options, and hand-in item rules for the shared offline runtime.",
        "inputs": [source_record(path) for path in (SOURCE_RECEIPT, SOURCE, Path(__file__),
                                                     ROOT / "scripts/prepare_unity_map13_story_runtime.py")],
        "outputs": [source_record(OUTPUT), source_record(OUTPUT.with_name(OUTPUT.name + ".meta"))],
        "counts": {
            "graphs": len(records), "mainStories": len(main_stories), "subgraphs": len(subgraphs),
            "nodes": sum(len(row["nodes"]) for row in records),
            "dialogueGroups": sum(len(row["dialogueGroups"]) for row in records),
            "dialogueLines": sum(len(group["Lines"]) for row in records for group in row["dialogueGroups"]),
            "dialogRoles": len(roles), "mapLogicTags": len(tags), "storyOptions": len(option_rows),
            "fixedRewards": len(fixed_rows), "handInItems": len(hand_in_rows),
        },
        "pendingRuntimeBindings": ["official playables", "Map16 battle-runtime", "mainline map host"],
    }
    write_json(OUTPUT_RECEIPT, result)
    print(json.dumps(result["counts"], ensure_ascii=False, sort_keys=True))
    return result


if __name__ == "__main__":
    build()
