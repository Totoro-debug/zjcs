"""Bind Map13 story tags and registrations from the verified native EC closure."""
import json
from pathlib import Path

from prepare_unity_map13_story_runtime import ROOT, SOURCE, read_json, sha, source_record, write_json, json_meta
def foreign_map_subgraphs(stories, map_id):
    """Prove foreign-only calls from paired original Selector inputs/outputs."""
    references = {}
    for graph in stories["stories"] + stories["subgraphs"]:
        nodes = {node["id"]: node for node in graph["nodes"]}
        def connections(node, port):
            return next((p["connections"] for p in node["ports"] if p["name"] == port), [])
        for node in nodes.values():
            if node["type"] != "SubgraphRunNode":
                continue
            calls = references.setdefault(node["subgraphName"], [])
            entry = connections(node, "Entry")
            guard_map = None
            if len(entry) == 1 and entry[0]["port"].startswith("Outs "):
                selector = nodes[entry[0]["nodeId"]]
                if selector["type"] == "SelectorNode":
                    guard = connections(selector, "Ins " + entry[0]["port"].split(" ")[1])
                    if len(guard) == 1 and nodes[guard[0]["nodeId"]]["type"] == "IsInTargetMapNode":
                        guard_map = json.loads(nodes[guard[0]["nodeId"]]["sourceJson"])["MapClassId"]
            calls.append(guard_map)
    return {name: maps for name, maps in references.items()
            if maps and all(value is not None and value != map_id for value in maps)}


def verified(name, receipt):
    path = SOURCE / name
    receipt_path = ROOT / "assets/reference/official-map" / receipt
    proof = read_json(receipt_path)
    expected = next(row["sha256"] for row in proof["outputs"]
                    if row["path"] == path.relative_to(ROOT).as_posix())
    if proof["manifest"] != "871_156598" or sha(path.read_bytes()) != expected:
        raise ValueError("Unverified native source: " + name)
    return read_json(path), [path, receipt_path]


def build():
    graph, inputs = verified("native-entity-object-graph.json", "map13-native-entity-object-graph-source-manifest.json")
    components, paths = verified("native-entity-components.json", "map13-native-entity-input-source-manifest.json")
    inputs += paths
    objects = {(row["cab"], row["path_id"]): row for row in graph["objects"]}
    decoded = {(row["cab"], row["path_id"]): row for row in components["components"]}
    records = {}
    for root in graph["roots"]:
        for use in root["uses"]:
            if use["kind"] != "ec_entity":
                continue
            identity = (root["cab"], root["path_id"])
            record = {"EntityClassId": use["class_id"], "TagType": 0, "RegisterKeys": []}
            for ref in objects[identity]["references"]:
                if not ref["field"].startswith(".m_Component["):
                    continue
                component = decoded.get((ref["cab"], ref["path_id"]))
                if component is None or component["type"] not in ("MapEntityTag", "StoryNodeRegister"):
                    continue
                fields = component["fields"]
                if component["type"] == "MapEntityTag":
                    record["TagType"] = fields["TagType"]
                else:
                    if len(fields["StoryNodeKeys"]) != len(fields["StoryNodes"]):
                        raise ValueError("Mismatched original story registration arrays")
                    for key, target in zip(fields["StoryNodeKeys"], fields["StoryNodes"]):
                        if target != {"m_FileID": 0, "m_PathID": int(root["path_id"])}:
                            raise ValueError("Original story registration requires a child transform: " + key)
                        record["RegisterKeys"].append(key)
            if record["EntityClassId"] in records:
                raise ValueError("Duplicate EC prefab class")
            records[record["EntityClassId"]] = record
    if len(records) != 150 or any(not records[k]["TagType"] & 1 for k in (3001, 3002)):
        raise ValueError("Original Map13 prefab tag closure changed")
    story_path = ROOT / "unity/Assets/Game/Resources/Map13/stories.json"
    story_receipt_path = ROOT / "assets/reference/official-map/map13-story-runtime-source-manifest.json"
    story_receipt = read_json(story_receipt_path)
    if next(r["sha256"] for r in story_receipt["outputs"] if r["path"] == story_path.relative_to(ROOT).as_posix()) != sha(story_path.read_bytes()):
        raise ValueError("Unverified Map13 story runtime")
    stories = read_json(story_path)
    foreign = foreign_map_subgraphs(stories, 13)
    required = {json.loads(node["sourceJson"]).get("RegisterKey")
                for story in stories["stories"] + stories["subgraphs"] if story["key"] not in foreign
                for node in story["nodes"] if node["type"] == "GetPosAroundStoryObjectNode"}
    required.discard(None)
    required.discard("")
    if required - {key for record in records.values() for key in record["RegisterKeys"]}:
        raise ValueError("Unresolved original Map13 story registration: " + repr(required))
    settings_path = ROOT / "assets/reference/official-map/map12-dialogue-source-manifest.json"
    settings_receipt = read_json(settings_path)
    if settings_receipt["inputs"]["packageManifest"]["manifest"] != "871_156598":
        raise ValueError("Shared StorySettings source version differs")
    settings = settings_receipt["inputs"]["storySettings"]["fields"]
    output = ROOT / "unity/Assets/Official/World/Map/13/Resources/Map13/story-spatial.json"
    payload = {"manifest": "871_156598", "mapClassId": 13,
               **{key: settings[key] for key in ("PlayerArroundRangeSize", "PlayerTalkDistException", "GetEntityPosOffset")},
               "Entities": [records[k] for k in sorted(records)]}
    write_json(output, payload)
    json_meta(output)
    inputs += [story_path, story_receipt_path, settings_path, Path(__file__)]
    write_json(ROOT / "assets/reference/official-map/map13-story-spatial-source-manifest.json", {
        "manifest": "871_156598", "mapClassId": 13,
        "operation": "Resolve original prefab root components, StoryTalk bits and root registrations; reuse version-matched shared StorySettings.",
        "inputs": [source_record(p) for p in inputs], "outputs": [source_record(output)],
        "counts": {"entities": len(records), "registerKeys": sum(len(r["RegisterKeys"]) for r in records.values())},
        "foreignMapSubgraphs": foreign, "requiredLocalRegisterKeys": sorted(required)})
    print("Map13 story spatial:", len(records), "original prefab root bindings")


if __name__ == "__main__":
    build()
