"""Build Map16 battle inputs from its pinned official definition and current APK."""
from __future__ import annotations

import hashlib
import inspect
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack
from official_tables import Constructors
from prepare_unity_map10_battle_data import collect_battle_data, json_bytes, map_stage_ids, write_meta


VERSION = "884_156692"
SOURCE = ROOT / "unity/Assets/Official/World/Map/16/Source"
OUTPUT = SOURCE.parent / "map16-battles-source.json"
RECEIPT = ROOT / "assets/reference/official-map/map16-battles-source-manifest.json"
CODE = ROOT / ".tools/source-packages/current" / VERSION / "official-code"


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def sha_source(path: Path) -> str:
    return sha(path.read_bytes().replace(b"\r\n", b"\n"))


def build():
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Map16 battle source version changed; review the new source first")
    constructors, formats = Constructors(CODE), Formats(CODE)
    source_receipt = json.loads((ROOT / "assets/reference/official-map/map16-definition-source-manifest.json").read_bytes())
    if source_receipt["manifest"] != VERSION or source_receipt["mapClassId"] != 16:
        raise ValueError("Map16 decoded source version differs")
    source_records = {row["path"]: row for row in source_receipt["outputs"]}
    inputs = []

    def verified_source(name: str):
        path = SOURCE / name
        raw = path.read_bytes()
        record = source_records.get(path.relative_to(ROOT).as_posix())
        if record is None or record["sha256"] != sha(raw):
            raise ValueError("Map16 decoded source changed: " + name)
        inputs.append({"path": path.relative_to(ROOT).as_posix(), "sha256": sha(raw)})
        return json.loads(raw)

    def table(name: str, kind: str):
        obj = apk.asset("config_luban", "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        inputs.append(dict(apk.provenance(obj), asset=name + ".g.bytes",
                           payload_sha256=sha(raw), object_sha256=sha(obj.get_raw_data())))
        return constructors.table(raw, kind)

    placements = verified_source("placement.json")
    local = verified_source("entities.json")
    templates = verified_source("templates.json")
    tables = verified_source("tables.json")
    story_path = SOURCE / "story-source.json"
    story_raw = story_path.read_bytes()
    story_receipt = json.loads((ROOT / "assets/reference/official-map/map16-story-source-manifest.json").read_bytes())
    story_record = next((row for row in story_receipt["outputs"]
                         if row["path"] == story_path.relative_to(ROOT).as_posix()), None)
    if story_receipt["manifest"] != VERSION or story_record is None or story_record["sha256"] != sha(story_raw):
        raise ValueError("Map16 story source differs from its pinned official receipt")
    inputs.append({"path": story_path.relative_to(ROOT).as_posix(), "sha256": sha(story_raw)})
    story_source = json.loads(story_raw)
    if story_source.get("mapClassId") != 16 or story_source.get("manifest") != VERSION:
        raise ValueError("Map16 story source identity differs")
    stories = story_source["graphs"]
    classes = {int(row["id"]): int(row["entity"]) for row in placements["entities"]}
    stages = map_stage_ids(16, placements, tables.get("map_stage", []), len(placements["entities"]))
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
    result = {"manifest": VERSION, "mapClassId": 16,
              "stages": sorted(rows, key=lambda row: int(row["Id"])),
              "monsterArrays": selected_arrays, "battleScenes": scenes,
              "entityStageBindings": component_bindings, "storyStageBindings": story_bindings}
    raw = json_bytes(result)
    previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    if OUTPUT.exists() and previous is None:
        raise ValueError("Refusing to replace unrecorded Map16 battle output")
    if OUTPUT.exists() and previous["output"]["sha256"] != sha(OUTPUT.read_bytes()):
        raise ValueError("Map16 battle output was modified outside its generator")
    receipt = {"format": 1, "manifest": VERSION, "mapClassId": 16,
               "operation": "Decode current official stage and monster-array tables, entity bindings, and fight-stage EC fragments. Roster/property projection is not included because the installed skill and combat rules are a different source version.",
               "inputs": inputs,
               "generator": {"path": Path(__file__).relative_to(ROOT).as_posix(),
                             "sha256": sha_source(Path(__file__))},
               "sharedPorts": [{"symbol": function.__name__,
                                "module": "scripts/prepare_unity_map10_battle_data.py",
                                "source_sha256_lf": sha(inspect.getsource(function).replace("\r\n", "\n").encode())}
                               for function in (map_stage_ids, collect_battle_data)],
               "constructorSources": [{"path": (constructors.source_root / (name + ".cs")).relative_to(ROOT).as_posix(),
                                       "sha256": sha((constructors.source_root / (name + ".cs")).read_bytes())}
                                      for name in constructors.sources
                                      if (constructors.source_root / (name + ".cs")).is_file()],
               "formatterSources": [{"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path.read_bytes())}
                                    for path in formats.sources.values()],
               "output": {"path": OUTPUT.relative_to(ROOT).as_posix(), "sha256": sha(raw), "size": len(raw)},
               "counts": {"stages": len(rows), "scenes": len(scenes),
                          "arrays": len(selected_arrays), "entityStageBindings": len(component_bindings),
                          "storyStages": len(story_bindings)}}
    OUTPUT.write_bytes(raw)
    meta_path = OUTPUT.with_suffix(OUTPUT.suffix + ".meta")
    relative = OUTPUT.relative_to(ROOT / "unity").as_posix()
    guid = hashlib.md5(("zjcs-official:" + relative).encode("utf-8")).hexdigest()
    expected_meta = (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n  externalObjects: {{}}\n"
                     "  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode("utf-8")
    if meta_path.exists():
        existing_meta = meta_path.read_bytes().replace(b" \r\n", b"\r\n").replace(b" \n", b"\n")
        if existing_meta != expected_meta:
            raise ValueError("Existing Map16 battle source meta differs from its deterministic importer data")
    else:
        write_meta(OUTPUT, "TextScriptImporter")
        meta_path.write_bytes(meta_path.read_bytes().replace(b" \r\n", b"\r\n").replace(b" \n", b"\n"))
    RECEIPT.write_bytes(json_bytes(receipt))
    print(json.dumps(receipt["counts"], ensure_ascii=False))
    return result


if __name__ == "__main__":
    build()
