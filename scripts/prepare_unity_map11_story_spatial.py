"""Recover Map11 story tags and registered objects from the original EC prefabs."""
from __future__ import annotations

import hashlib
import argparse
import json
from pathlib import Path

from official_apk import Apk
from official_unity_schema import ScriptSchemas
from prepare_unity_map10_entities import json_bytes, write_generated, write_meta

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "unity/Assets/Official/World/Map/11/Resources/Map11/story-spatial.json"
MANIFEST = ROOT / "assets/reference/official-map/map11-story-spatial-source-manifest.json"


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


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


def build(map_id=11):
    if map_id not in (11, 12, 14):
        raise ValueError("Story spatial recovery is only pinned for Maps 11, 12 and 14.")
    apk = Apk.current() if map_id in (12, 14) else Apk()
    output = (ROOT / "unity/Assets/Game/Resources/Map14/story-spatial.json" if map_id == 14 else
              ROOT / f"unity/Assets/Official/World/Map/{map_id}/Resources/Map{map_id}/story-spatial.json")
    manifest_path = ROOT / f"assets/reference/official-map/map{map_id}-story-spatial-source-manifest.json"
    if map_id == 14:
        entities_path = ROOT / "unity/Assets/Official/World/Map/14/Source/entities.json"
        stories_path = ROOT / "unity/Assets/Game/Resources/Map14/stories.json"
        map_source_path = ROOT / "assets/reference/official-map/map14-source-manifest.json"
        story_source_path = ROOT / "assets/reference/official-map/map14-story-runtime-source-manifest.json"
        map_source = json.loads(map_source_path.read_bytes())
        story_source = json.loads(story_source_path.read_bytes())
        if (map_source.get("manifest") != apk.version or map_source.get("mapClassId") != 14
                or story_source.get("manifest") != map_source["manifest"] or story_source.get("mapClassId") != 14):
            raise ValueError("Map14 spatial input receipts do not match the selected official resource version.")
        expected_entities = next((value["sha256"] for value in map_source["outputs"]
                                  if value["path"] == entities_path.relative_to(ROOT).as_posix()), None)
        expected_stories = next((value["sha256"] for value in story_source["outputs"]
                                 if value["path"] == stories_path.relative_to(ROOT).as_posix()), None)
        if expected_entities != digest(entities_path.read_bytes()) or expected_stories != digest(stories_path.read_bytes()):
            raise ValueError("Map14 spatial inputs differ from their pinned source receipts.")
        source_receipts = {
            "map14": {"path": map_source_path.relative_to(ROOT).as_posix(),
                      "sha256": digest(map_source_path.read_bytes())},
            "storyRuntime": {"path": story_source_path.relative_to(ROOT).as_posix(),
                             "sha256": digest(story_source_path.read_bytes())}
        }
        raw_entities = json.loads(entities_path.read_bytes())
        entities = []
        for placement_id, components in raw_entities.items():
            entity = components.get("LoEntityComponent", {})
            spawn = entity.get("SpanwInfo", entity.get("SpawnInfo", {}))
            params = json.loads(spawn.get("Params") or "{}")
            class_id = params.get("EntityClassId")
            if class_id is None:
                raise ValueError("Map14 entity prefab class is missing from original spawn config: " + placement_id)
            entities.append({"placementId": int(placement_id), "entityClassId": int(class_id)})
    else:
        source_receipts = {}
        entities_path = ROOT / f"unity/Assets/Official/World/Map/{map_id}/map{map_id}-entities.json"
        stories_path = output.parent / "stories.json"
        entities = json.loads(entities_path.read_bytes())["entities"]
    stories = json.loads(stories_path.read_bytes())
    classes = {value["entityClassId"] for value in entities}
    classes.update(node["entityClassId"] for story in stories["stories"] + stories["subgraphs"]
                   for node in story["nodes"] if node["type"].startswith("AddMap") and node["entityClassId"] > 0)
    # The original player prefabs carry the same StoryTalk bit as NPCs.
    classes.update((3001, 3002))
    current_code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    code_root = current_code / "client-source" if map_id in (12, 14) else ROOT / ".tools/source-packages/official-code/client-source"
    schemas = ScriptSchemas(code_root=current_code) if map_id in (12, 14) else ScriptSchemas()
    index_object = next(pointer.deref() for path, pointer in apk.container("config_ec")
                        if path.endswith("/index.g.txt"))
    index = json.loads(index_object.read().m_Script)
    records, sources = [], []
    for class_id in sorted(classes):
        directory = next(key for key, values in index.items() if class_id in values)
        path = f"assets/prefabs/ecentity/{directory}/{class_id}.prefab".lower()
        bundle_key = "defaultpackage_" + path.replace("/", "_")
        bundles = sorted((name for name in apk.index if bundle_key.startswith(name.removesuffix(".bundle"))),
                         key=len, reverse=True)
        root = next(pointer.deref() for bundle in bundles for name, pointer in apk.container(bundle)
                    if name.lower() == path and pointer.type.name == "GameObject")
        record = {"EntityClassId": class_id, "TagType": 0, "RegisterKeys": []}
        source = {"path": path, "root": apk.provenance(root), "components": []}
        for component in root.read().m_Component:
            obj = component.component.deref()
            if obj.type.name != "MonoBehaviour":
                continue
            name = obj.read(check_read=False).m_Script.read().m_ClassName
            if name not in ("MapEntityTag", "StoryNodeRegister"):
                continue
            fields = schemas.read(obj)
            source["components"].append({"class": name, "source": apk.provenance(obj), "fields": fields})
            if name == "MapEntityTag":
                record["TagType"] = fields["TagType"]
            else:
                if len(fields["StoryNodeKeys"]) != len(fields["StoryNodes"]):
                    raise ValueError("Mismatched original story registration arrays")
                for key, target in zip(fields["StoryNodeKeys"], fields["StoryNodes"]):
                    if target != {"m_FileID": 0, "m_PathID": root.path_id}:
                        raise ValueError("Story registration requires a child transform: " + key)
                    record["RegisterKeys"].append(key)
        records.append(record)
        sources.append(source)
    if len(records) != len(classes) or (map_id == 11 and len(records) != 113) or any(not next(value for value in records if value["EntityClassId"] == player)["TagType"] & 1
                                  for player in (3001, 3002)):
        raise ValueError(f"Pinned Map{map_id} story prefab closure changed: " + str(len(records)))
    settings_path = ROOT / f"assets/reference/official-map/map{12 if map_id == 14 else map_id}-dialogue-source-manifest.json"
    settings_receipt = json.loads(settings_path.read_bytes())
    if settings_receipt.get("manifest") != apk.version:
        raise ValueError("StorySettings source receipt does not match the selected Map" + str(map_id) + " package.")
    settings_source = settings_receipt["inputs"]["storySettings"]
    if map_id == 14:
        active = json.loads((ROOT / "official-source.json").read_bytes())
        resource_version = active["profiles"][active["active"]]["resource_version"]
        inventory_path = ROOT / ".tools/source-packages/cloud" / "runtime" / resource_version / "inventory.json"
        inventory = json.loads(inventory_path.read_bytes())
        story_bundle = next((item for item in inventory["bundles"]
                             if item["name"] == settings_source["bundle"]), None)
        if story_bundle is None or story_bundle["hash"] != settings_source["bundle_hash"]:
            raise ValueError("Map14 StorySettings bundle differs from the selected official resource version.")
    settings = settings_source["fields"]
    foreign = foreign_map_subgraphs(stories, map_id) if map_id in (12, 14) else {}
    keys = set()
    for story in stories["stories"] + stories["subgraphs"]:
        if story["key"] in foreign:
            continue
        for node in story["nodes"]:
            if node["type"] != "GetPosAroundStoryObjectNode":
                continue
            key = (json.loads(node["sourceJson"]).get("RegisterKey") if map_id == 14
                   else node["registerKey"])
            if key:
                keys.add(key)
    registrations = {key for record in records for key in record["RegisterKeys"]}
    if not keys <= registrations:
        raise ValueError(f"Unresolved original Map{map_id} story registration: " + repr(keys - registrations))
    payload = {"manifest": apk.version, "mapClassId": map_id,
               "PlayerArroundRangeSize": settings["PlayerArroundRangeSize"],
               "PlayerTalkDistException": settings["PlayerTalkDistException"],
               "GetEntityPosOffset": settings["GetEntityPosOffset"], "Entities": records}
    previous = {}
    if manifest_path.exists():
        previous[manifest_path.resolve()] = digest(manifest_path.read_bytes())
        for value in json.loads(manifest_path.read_bytes())["outputs"]:
            target = ROOT / value["path"]
            if digest(target.read_bytes()) != value["sha256"]:
                raise ValueError("Changed spatial binding output: " + str(target))
            previous[target.resolve()] = value["sha256"]
    raw = json_bytes(payload)
    write_generated(output, raw, previous)
    write_meta(output, "TextScriptImporter")
    meta = output.with_name(output.name + ".meta")
    meta.write_text("\n".join(line.rstrip() for line in meta.read_text(encoding="utf-8").splitlines()) + "\n",
                    encoding="utf-8")
    code = [code_root / name for name in ("StoryUtils.cs", "GetEntityBaseNode.cs", "GetEntityPosNode.cs", "GetPosAroundStoryObjectNode.cs",
            "MapEntityTag.cs", "EMapEntityTagType.cs", "StoryNodeRegister.cs", "Common/MyEntityIndexHandler.cs")]
    if map_id == 12:
        code += [code_root / name for name in ("IsInTargetMapNode.cs", "WaitMapInitedNode.cs", "SelectorNode.cs")]
        code += [code_root / "MapInputSupportHandlerBase.cs",
                 current_code / "common-source/Common/WorldStageComponent.cs"]
    manifest = {"manifest": apk.version, "mapClassId": map_id,
                "inputs": {"entitiesSha256": digest(entities_path.read_bytes()),
                           "storiesSha256": digest(stories_path.read_bytes()),
                           "storySettingsManifestSha256": digest(settings_path.read_bytes()),
                           "storySettings": settings_source,
                           "sourceReceipts": source_receipts,
                           "index": apk.provenance(index_object), "prefabs": sources,
                           "code": [{"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path.read_bytes())}
                                    for path in code]},
                "operation": "Read original EC index and prefabs, root StoryTalk tags, root story registrations and StorySettings; no inferred tags.",
                "outputs": [{"path": output.relative_to(ROOT).as_posix(), "sha256": digest(raw)}]}
    if map_id in (12, 14):
        manifest["foreignMapSubgraphs"] = foreign
        manifest["requiredLocalRegisterKeys"] = sorted(keys)
    manifest["generator"] = {"path": Path(__file__).relative_to(ROOT).as_posix(),
                             "sha256": digest(Path(__file__).read_bytes())}
    write_generated(manifest_path, json_bytes(manifest), previous)
    print(f"Map{map_id} story spatial: {len(records)} original entity prefabs, tags and registrations.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Recover official mainline story spatial bindings.")
    parser.add_argument("--map", type=int, choices=(11, 12, 14), default=11)
    build(parser.parse_args().map)
