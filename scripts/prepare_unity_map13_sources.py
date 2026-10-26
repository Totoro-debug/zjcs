"""Decode the selected official Map13 source without activating an unfinished map.

Reads APK entries before the verified cloud cache. Uses the selected Console.dll
formatters/constructors and Assembly-CSharp.dll schemas, never legacy projections.
The outputs are source data, not a claim of Windows rendering or playability.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import struct

import xxhash

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import entities, ground, unpack
from official_tables import Constructors
from official_unity_schema import ScriptSchemas


DESTINATION = ROOT / "unity/Assets/Official/World/Map/13/Source"
RECEIPT = ROOT / "assets/reference/official-map/map13-source-manifest.json"
TERMINALS = {2634, 3337, 5334, 7439, 9338}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def finite(value):
    if isinstance(value, float) and not math.isfinite(value):
        return str(value)
    if isinstance(value, dict):
        return {key: finite(item) for key, item in value.items()}
    if isinstance(value, list):
        return [finite(item) for item in value]
    return value


def encode(value):
    # EC components and story ports retain their serialized insertion order.
    return (json.dumps(finite(value), ensure_ascii=False, indent=2) + "\n").encode("utf8")


def source_path(path):
    return path.relative_to(ROOT).as_posix()


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != raw:
        path.write_bytes(raw)


def meta(path, folder=False):
    identity = source_path(path).removeprefix("unity/")
    guid = hashlib.md5(("zjcs-official:" + identity).encode()).hexdigest()
    body = "folderAsset: yes\nDefaultImporter:\n" if folder else "TextScriptImporter:\n"
    body += "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    target = path.with_name(path.name + ".meta")
    raw = f"fileFormatVersion: 2\nguid: {guid}\n{body}".encode()
    if target.exists() and b"\n".join(line.rstrip() for line in target.read_bytes().splitlines()) + b"\n" != raw:
        raise ValueError("Refusing to replace unrelated import metadata: " + str(target))
    write(target, raw)


def build(destination=DESTINATION, receipt=RECEIPT):
    apk = Apk.current()
    source_root = ROOT / ".tools/source-packages/current" / apk.version
    code = source_root / "official-code"
    code_manifest_path = source_root / "code-manifest.json"
    code_manifest = json.loads(code_manifest_path.read_bytes())
    if code_manifest["manifest"] != apk.version:
        raise ValueError("Decompiled code does not belong to the selected official source")
    assemblies = {}
    for name in ("Console.dll", "Assembly-CSharp.dll"):
        path = code / "managed/decoded" / name
        record = code_manifest["assemblies"][name]
        if sha(path.read_bytes()) != record["decoded_sha256"]:
            raise ValueError("Decoded official assembly hash mismatch: " + name)
        assemblies[name] = dict(record, path=source_path(path))
    constructors = Constructors(code)
    formats = Formats(code)
    inputs = {}

    def payload(bundle, suffix):
        obj = apk.asset(bundle, suffix)
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        inputs[suffix] = dict(apk.provenance(obj), payload_sha256=sha(raw), bytes=len(raw),
                             object_sha256=sha(obj.get_raw_data()))
        return raw

    navigation = ground(payload("config_ec_maprootentitylite", "/13.g.bytes"))
    placement = entities(payload("config_mapinfowrap", "/mapinfowrap_13.g.bytes"))
    if navigation["id"] != 13 or placement["id"] != 13:
        raise ValueError("Official navigation/placement identity differs from Map13")
    local_raw = payload("config_ec", "/map_13.g.bytes")
    local = {}
    for parent, identity, components in unpack(local_raw)[0]:
        if parent != 13 or identity in local:
            raise ValueError("Duplicate or foreign Map13 EC placement: " + str(identity))
        local[identity] = {typ.removeprefix("Common."): formats.component(typ, raw)
                           for typ, raw in components}
    if set(local) != {row["id"] for row in placement["entities"]}:
        raise ValueError("Map13 EC and MapInfoWrap placement closure is incomplete")
    class_ids = {row["entity"] for row in placement["entities"]}
    class_ids.update((navigation["character"], navigation["member"]))
    templates = {}
    template_sources = {}
    candidates = {}
    for path, pointer in apk.container("config_ec"):
        name = path.rsplit("/", 1)[-1]
        if name.startswith("map_") or not name.startswith(("World", "ActionWorld", "ActionCommon")):
            continue
        obj = pointer.deref()
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        legacy = False
        try:
            rows = unpack(raw)[0]
        except (ValueError, TypeError):
            rows = unpack(raw[2:])[0]
            legacy = True
        for parent, identity, components in rows:
            if parent:
                continue
            if identity in candidates and legacy and not candidates[identity][1]:
                continue
            candidates[identity] = (components, legacy, dict(apk.provenance(obj), asset=path,
                legacy=legacy, payload_sha256=sha(raw)))

    def references(value):
        if isinstance(value, list):
            for item in value:
                yield from references(item)
        elif isinstance(value, dict):
            for key, item in value.items():
                if key.endswith(("ActionId", "ActionIds")):
                    for identity in item if isinstance(item, list) else [item]:
                        if isinstance(identity, int) and identity > 0:
                            yield identity
                yield from references(item)
        elif isinstance(value, str) and value.startswith("{"):
            try:
                yield from references(json.loads(re.sub(r'([A-Za-z_][A-Za-z_0-9]*):', r'"\1":', value)))
            except json.JSONDecodeError:
                pass

    class_ids.update(references(list(local.values())))
    while pending_ids := sorted(class_ids - set(templates)):
        for identity in pending_ids:
            if identity not in candidates:
                raise ValueError("Missing official Map13 EC template/action: " + str(identity))
            components, legacy, source = candidates[identity]
            values = {typ.removeprefix("Common."):
                formats.component(typ, data[2:] if legacy and data else data) for typ, data in components}
            templates[identity] = values
            template_sources[identity] = source
            class_ids.update(references(values))
    print(f"Map13: decoded {len(local)} placements and {len(templates)} official entity templates", flush=True)

    mapping = {name: cls for cls, name in re.findall(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source("Tables"))}
    wanted = {name for name in mapping if name.startswith(("Map/", "Dialog/"))}
    wanted.update(("EC/entity_grid_mover", "conditions", "dialog", "story", "stage_story", "story_option_dialog",
                   "story_entity_listen", "story_entity_status", "task", "map_stage", "monster",
                   "monster_reward", "monster_segment_reward", "hand_in_item", "fixed_reward",
                   "reward_rule", "status_effect", "fight_status", "witch", "ruin", "bgm_event_key"))
    tables = {}
    for name in sorted(wanted):
        bundle = "config_luban_" + name.rsplit("/", 1)[0].replace("/", "_").lower() if "/" in name else "config_luban"
        raw = payload(bundle, "/" + name + ".g.bytes")
        tables[name] = constructors.table(raw, mapping[name])
    regions = [row for row in tables["Map/map_region"] if row["ClassId"] == 13]
    maps = [row for row in tables["Map/map"] if row["ClassId"] == 13]
    if len(maps) != 1 or not regions:
        raise ValueError("Map13 official map/region table is missing")

    schemas = ScriptSchemas(code_root=code)
    graphs = {}
    graph_inputs = {}
    pending = []
    for bundle in sorted(name for name in apk.index if "_scriptableconfig_story_map13story" in name):
        for obj in apk.objects(bundle):
            if obj.type.name == "MonoBehaviour" and obj.read(check_read=False).m_Script.read().m_ClassName == "StoryGraph":
                pending.append(obj)
    while pending:
        graph = pending.pop(0)
        definition = schemas.read(graph)
        name = definition["m_Name"]
        if name in graphs:
            continue
        nodes = {}
        by_path = {}
        for pointer in definition["nodes"]:
            from UnityPy.classes.PPtr import PPtr
            obj = PPtr(**pointer, assetsfile=graph.assets_file).deref()
            data = schemas.read(obj)
            data["_type"] = obj.read(check_read=False).m_Script.read().m_ClassName
            identity = str(data["NodeId"])
            if identity in nodes:
                raise ValueError("Duplicate original story node: " + name + ":" + identity)
            by_path[str(obj.path_id)] = identity
            nodes[identity] = data
            subgraph = data.get("Subgraph", {})
            if subgraph.get("m_PathID"):
                pending.append(PPtr(**subgraph, assetsfile=obj.assets_file).deref())
        for data in nodes.values():
            ports = {}
            for port in data.pop("ports", {}).get("values", []):
                connections = []
                for connection in port["connections"]:
                    target_path = str(connection["node"]["m_PathID"])
                    # XNode serializes disconnected ports with a null pointer.
                    # A non-null pointer outside this graph is a broken binding.
                    if target_path == "0":
                        continue
                    target = by_path[target_path]
                    connections.append(dict(node=target, port=connection["fieldName"]))
                ports[port["_fieldName"]] = dict(direction=port["_direction"],
                    flow=port.get("_flowConnectionType", 0), connections=connections)
            data["ports"] = ports
            for field in ("WrappingInputNode", "WrappingOutputNode", "GoTo"):
                if field in data:
                    data[field] = by_path.get(str(data[field]["m_PathID"]), "")
            subgraph = data.get("Subgraph", {})
            if subgraph.get("m_PathID"):
                data["subgraph_name"] = schemas.read(PPtr(**subgraph, assetsfile=graph.assets_file).deref())["m_Name"]
            for field in ("m_GameObject", "m_Script", "m_Enabled", "graph", "position", "Mono", "IsMonoShow"):
                data.pop(field, None)
        graphs[name] = dict(nodes=nodes, properties={k: v for k, v in definition.items() if k != "nodes"})
        graph_inputs[name] = dict(apk.provenance(graph), object_sha256=sha(graph.get_raw_data()), nodes=len(nodes))
    if not graphs:
        raise ValueError("No official Map13 story graphs were decoded")
    print(f"Map13: decoded {len(regions)} regions, {len(tables)} tables and {len(graphs)} story graphs", flush=True)

    dialogues = {}
    # Current DialogInfoParser consumes TbDialogInfo (the old CSV was removed).
    for row in tables["dialog"]:
        story = row.get("StoryName") or "default"
        if story not in graphs and story != "default":
            continue
        dialogues.setdefault(story, {}).setdefault(row["Id"], []).append(row)
    for groups in dialogues.values():
        for rows in groups.values():
            rows.sort(key=lambda row: row["Order"])

    text_keys = {f"dialog_{story}_{row['Id']}_{row['Order']}" for story, groups in dialogues.items()
                 for rows in groups.values() for row in rows}
    text_keys.update(f"story_{row['StoryName']}_{row['Id']}_option"
                     for row in tables["story_option_dialog"] if row["StoryName"] in graphs)
    required_dialogues = set(text_keys)
    def collect_text_keys(value):
        if isinstance(value, dict):
            for key, item in value.items():
                collect_text_keys(key)
                collect_text_keys(item)
        elif isinstance(value, list):
            for item in value:
                collect_text_keys(item)
        elif isinstance(value, str) and re.fullmatch(r"[A-Za-z][A-Za-z0-9_.:{}-]*", value):
            text_keys.add(value)
    collect_text_keys(tables)
    collect_text_keys(graphs)
    wanted_hashes = {xxhash.xxh64_intdigest(key.encode("utf8")): key for key in sorted(text_keys)}
    localized = {}
    wwise = {row["name"]: row for row in apk.source.manifest("WwiseBnk")["bundles"]}
    for name in ("wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile",
                 "wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile"):
        record = wwise[name]
        entry = "assets/yoo/WwiseBnk/" + record["file"]
        if entry in apk.available:
            raw = apk.archive.read(entry)
            origin = dict(origin="apk_zip", apk_entry=entry, apk_sha256=apk.source.apk_sha256)
        else:
            cached = apk.cloud.read("WwiseBnk", record)
            if cached is None:
                raise FileNotFoundError("Selected original Chinese rawfile is not cached: " + name)
            raw, origin = cached
        if len(raw) != record["size"] or hashlib.md5(raw).hexdigest() != record["hash"]:
            raise ValueError("Selected original Chinese rawfile differs from its manifest: " + name)
        inputs[name] = dict(origin, payload_sha256=sha(raw), bytes=len(raw), md5=record["hash"])
        count = struct.unpack_from("<i", raw)[0]
        start = 4 + count * 12
        for index in range(count):
            identity, offset = struct.unpack_from("<Qi", raw, 4 + index * 12)
            if identity not in wanted_hashes:
                continue
            position = start + offset
            size = struct.unpack_from("<H", raw, position)[0]
            if offset < 0 or position + 2 + size > len(raw):
                raise ValueError("Original Chinese entry is outside the rawfile")
            localized[wanted_hashes[identity]] = raw[position + 2:position + 2 + size].decode("utf8")
    missing_dialogues = sorted(required_dialogues - set(localized))
    if missing_dialogues:
        raise ValueError("Original Map13 dialogue text was not resolved: " + str(missing_dialogues[:20]))

    server_graphs = {}
    for path, pointer in apk.container("config_story"):
        if not path.endswith(".json"):
            continue
        name = path.rsplit("/", 1)[-1].removesuffix(".json")
        if name not in graphs:
            continue
        obj = pointer.deref()
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        # Preserve the serialized Item1/Item2 records without interpreting them
        # as an implementation of server execution.
        server_graphs[name] = json.loads(raw)
        inputs[path] = dict(apk.provenance(obj), payload_sha256=sha(raw),
                            object_sha256=sha(obj.get_raw_data()), bytes=len(raw))

    terminals = {}
    for identity, components in local.items():
        terminal = components.get("LoEntityPuzzleTerminalComponent")
        if not terminal or terminal["MapWorldPuzzleType"] != 2:
            continue
        params = json.loads(re.sub(r'([A-Za-z_][A-Za-z_0-9]*):', r'"\1":', terminal["Params"]))
        if set(params) != {
            "ExtendRoadLoEntityIds", "NeedResetLoEntityIds", "ResetTransferActionId"}:
            raise ValueError("Unexpected Map13 terminal parameters: " + str(identity))
        references = params["ExtendRoadLoEntityIds"] + params["NeedResetLoEntityIds"]
        if not set(references) <= set(local):
            raise ValueError("Map13 water terminal refers to missing placement: " + str(identity))
        terminals[identity] = params
    if set(terminals) != TERMINALS:
        raise ValueError("Map13 must preserve all five original water terminals")

    files = {
        "navigation.json": navigation, "placement.json": placement,
        "entities.json": local, "templates.json": templates,
        "tables.json": tables, "stories.json": graphs, "water-terminals.json": terminals,
        "dialogues.json": dialogues, "server-story-records.json": server_graphs,
        "localization.json": localized,
        "entries.json": unpack(payload("config_map", "/MapEntryInfoPack.g.bytes"))[0],
        "transports.json": unpack(payload("config_map", "/MapTransportEntityInfoPack.g.bytes"))[0],
    }
    outputs = []
    for name, value in files.items():
        path = destination / name
        raw = encode(value)
        write(path, raw)
        meta(path)
        outputs.append(dict(path=source_path(path), sha256=sha(raw), bytes=len(raw)))
    for folder in (destination.parent, destination):
        meta(folder, folder=True)
    source_files = set(formats.sources.values())
    source_files.update(code / "common-source/Common" / (name + ".cs")
                        for name in ("DialogInfoParser", "LanguageUtils"))
    for name in constructors.sources:
        for folder in (code / "common-source/Common", code / "common-source/Common.LubanGen"):
            path = folder / (name + ".cs")
            if path.is_file():
                source_files.add(path)
    for name in {node["_type"] for graph in graphs.values() for node in graph["nodes"].values()}:
        path = code / "client-source" / (name + ".cs")
        if path.is_file():
            source_files.add(path)
    result = dict(format=1, manifest=apk.version, apk_sha256=apk.source.apk_sha256,
        resource_version=apk.source.profile["resource_version"], mapClassId=13,
        operation="Decode original APK/verified cloud bytes using selected official assemblies; no source version substitution.",
        status="Decoded source data only; Unity integration, graphics and Windows Player validation remain required.",
        code_manifest=dict(path=source_path(code_manifest_path), sha256=sha(code_manifest_path.read_bytes())),
        assemblies=assemblies, inputs=inputs, ec_templates=template_sources, story_graphs=graph_inputs,
        decompiled_sources=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in sorted(source_files)],
        schemas=schemas.records, outputs=outputs,
        counts=dict(regions=len(regions), placements=len(local), templates=len(templates),
                    stories=len(graphs), tables=len(tables), water_terminals=len(terminals),
                    dialogue_groups=len(dialogues), server_story_records=len(server_graphs),
                    chinese_keys=len(localized)),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())))
    write(receipt, encode(result))
    apk.archive.close()
    print("Map13 source receipt: " + source_path(receipt), flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DESTINATION)
    parser.add_argument("--receipt", type=Path, default=RECEIPT)
    args = parser.parse_args()
    build(args.output.resolve(), args.receipt.resolve())
