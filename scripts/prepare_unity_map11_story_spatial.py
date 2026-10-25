"""Recover Map11 story tags and registered objects from the original EC prefabs."""
from __future__ import annotations

import hashlib
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


def build():
    entities_path = ROOT / "unity/Assets/Official/World/Map/11/map11-entities.json"
    stories_path = OUTPUT.parent / "stories.json"
    entities = json.loads(entities_path.read_bytes())["entities"]
    stories = json.loads(stories_path.read_bytes())
    classes = {value["entityClassId"] for value in entities}
    classes.update(node["entityClassId"] for story in stories["stories"] + stories["subgraphs"]
                   for node in story["nodes"] if node["type"].startswith("AddMap") and node["entityClassId"] > 0)
    # The original player prefabs carry the same StoryTalk bit as NPCs.
    classes.update((3001, 3002))
    apk, schemas = Apk(), ScriptSchemas()
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
    if len(records) != 113 or any(not next(value for value in records if value["EntityClassId"] == player)["TagType"] & 1
                                  for player in (3001, 3002)):
        raise ValueError("Pinned Map11 story prefab closure changed: " + str(len(records)))
    settings_path = ROOT / "assets/reference/official-map/map11-dialogue-source-manifest.json"
    settings = json.loads(settings_path.read_bytes())["inputs"]["storySettings"]["fields"]
    keys = {node["registerKey"] for story in stories["stories"] + stories["subgraphs"]
            for node in story["nodes"] if node["type"] == "GetPosAroundStoryObjectNode"}
    registrations = {key for record in records for key in record["RegisterKeys"]}
    if not keys <= registrations:
        raise ValueError("Unresolved original Map11 story registration: " + repr(keys - registrations))
    payload = {"manifest": apk.version, "mapClassId": 11,
               "PlayerArroundRangeSize": settings["PlayerArroundRangeSize"],
               "PlayerTalkDistException": settings["PlayerTalkDistException"],
               "GetEntityPosOffset": settings["GetEntityPosOffset"], "Entities": records}
    previous = {}
    if MANIFEST.exists():
        previous[MANIFEST.resolve()] = digest(MANIFEST.read_bytes())
        for value in json.loads(MANIFEST.read_bytes())["outputs"]:
            target = ROOT / value["path"]
            if digest(target.read_bytes()) != value["sha256"]:
                raise ValueError("Changed spatial binding output: " + str(target))
            previous[target.resolve()] = value["sha256"]
    raw = json_bytes(payload)
    write_generated(OUTPUT, raw, previous)
    write_meta(OUTPUT, "TextScriptImporter")
    meta = OUTPUT.with_name(OUTPUT.name + ".meta")
    meta.write_text("\n".join(line.rstrip() for line in meta.read_text(encoding="utf-8").splitlines()) + "\n",
                    encoding="utf-8")
    code_root = ROOT / ".tools/source-packages/official-code/client-source"
    code = [code_root / name for name in ("StoryUtils.cs", "GetEntityBaseNode.cs", "GetEntityPosNode.cs", "GetPosAroundStoryObjectNode.cs",
            "MapEntityTag.cs", "EMapEntityTagType.cs", "StoryNodeRegister.cs", "Common/MyEntityIndexHandler.cs")]
    manifest = {"manifest": apk.version, "mapClassId": 11,
                "inputs": {"entitiesSha256": digest(entities_path.read_bytes()),
                           "storiesSha256": digest(stories_path.read_bytes()),
                           "storySettingsManifestSha256": digest(settings_path.read_bytes()),
                           "index": apk.provenance(index_object), "prefabs": sources,
                           "code": [{"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path.read_bytes())}
                                    for path in code]},
                "operation": "Read original EC index and prefabs, root StoryTalk tags, root story registrations and StorySettings; no inferred tags.",
                "outputs": [{"path": OUTPUT.relative_to(ROOT).as_posix(), "sha256": digest(raw)}]}
    write_generated(MANIFEST, json_bytes(manifest), previous)
    print(f"Map11 story spatial: {len(records)} original entity prefabs, tags and registrations.")


if __name__ == "__main__":
    build()
