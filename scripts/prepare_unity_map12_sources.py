"""Decode the selected official Map12 source without activating an unfinished map.

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

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import entities, ground, unpack
from official_tables import Constructors
from official_unity_schema import ScriptSchemas


DESTINATION = ROOT / "unity/Assets/Official/World/Map/12/Source"
RECEIPT = ROOT / "assets/reference/official-map/map12-source-manifest.json"
TERMINALS = {746, 1416, 2815, 4417, 4631, 5220, 6326, 6918, 7923, 8721, 9716}


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
    return (json.dumps(finite(value), ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf8")


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
    body += "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    target = path.with_name(path.name + ".meta")
    raw = f"fileFormatVersion: 2\nguid: {guid}\n{body}".encode()
    if target.exists() and target.read_bytes() != raw:
        raise ValueError("Refusing to replace unrelated import metadata: " + str(target))
    write(target, raw)


def verify_legacy_equivalence(navigation, placement, local, templates, tables, graphs):
    """Allow reuse only after comparing selected-version decoded records."""
    navigation, placement, local, templates, tables, graphs = [json.loads(encode(value))
        for value in (navigation, placement, local, templates, tables, graphs)]
    region_path = ROOT / "assets/official/regions/12.json"
    component_path = ROOT / "assets/official/regions/12-entities.json"
    template_path = ROOT / "assets/official/map-entities.json"
    adventure_path = ROOT / "assets/official/adventure.json"
    region = json.loads(region_path.read_bytes())
    old_local = json.loads(component_path.read_bytes())
    old_templates = json.loads(template_path.read_bytes())["templates"]
    old_adventure = json.loads(adventure_path.read_bytes())
    checks = {"navigation": navigation == region["navigation"],
              "placement": placement == region["placement"],
              "components": {str(k): v for k, v in local.items()} == old_local,
              "templates": all(old_templates.get(str(k)) == v for k, v in templates.items())}
    for name in ("Map/map", "Map/map_region", "Map/map_area", "Map/map_world", "Map/map_logic_entity_tag", "map_stage"):
        select = lambda rows: [r for r in rows if any(r.get(k) == 12 for k in ("ClassId", "MapClassId", "MapClass"))]
        checks[name] = select(tables[name]) == select(old_adventure["tables"][name])
    def connected(value):
        if isinstance(value, list):
            return [connected(item) for item in value if not isinstance(item, dict) or "node" not in item or item["node"]]
        if isinstance(value, dict):
            return {key: connected(item) for key, item in value.items()}
        return value
    checks["storyNodesAndPorts"] = all(connected(g["nodes"]) == connected(old_adventure["stories"].get(name, {}).get("nodes"))
                                       for name, g in graphs.items())
    if not all(checks.values()):
        raise ValueError("Legacy Map12 data differs from selected sources: " + repr([k for k, v in checks.items() if not v]))
    return dict(checks=checks, normalization="Canonical JSON object-key strings; ignore only XNode connections with null node pointers; preserve all live ports and their order.",
        legacy_inputs=[dict(path=source_path(p), sha256=sha(p.read_bytes()))
                       for p in (region_path, component_path, template_path, adventure_path)])


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

    navigation = ground(payload("config_ec_maprootentitylite", "/12.g.bytes"))
    placement = entities(payload("config_mapinfowrap", "/mapinfowrap_12.g.bytes"))
    if navigation["id"] != 12 or placement["id"] != 12:
        raise ValueError("Official navigation/placement identity differs from Map12")
    local_raw = payload("config_ec", "/map_12.g.bytes")
    local = {}
    for parent, identity, components in unpack(local_raw)[0]:
        if parent != 12 or identity in local:
            raise ValueError("Duplicate or foreign Map12 EC placement: " + str(identity))
        local[identity] = {typ.removeprefix("Common."): formats.component(typ, raw)
                           for typ, raw in components}
    if set(local) != {row["id"] for row in placement["entities"]}:
        raise ValueError("Map12 EC and MapInfoWrap placement closure is incomplete")
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
                raise ValueError("Missing official Map12 EC template/action: " + str(identity))
            components, legacy, source = candidates[identity]
            values = {typ.removeprefix("Common."):
                formats.component(typ, data[2:] if legacy and data else data) for typ, data in components}
            templates[identity] = values
            template_sources[identity] = source
            class_ids.update(references(values))
    print(f"Map12: decoded {len(local)} placements and {len(templates)} official entity templates", flush=True)

    mapping = {name: cls for cls, name in re.findall(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source("Tables"))}
    wanted = {name for name in mapping if name.startswith(("Map/", "Dialog/"))}
    wanted.update(("EC/entity_grid_mover", "conditions", "story", "stage_story", "story_option_dialog",
                   "story_entity_listen", "story_entity_status", "task", "map_stage", "monster",
                   "monster_reward", "monster_segment_reward", "hand_in_item", "fixed_reward",
                   "reward_rule", "status_effect", "fight_status", "witch", "ruin", "bgm_event_key"))
    tables = {}
    for name in sorted(wanted):
        bundle = "config_luban_" + name.rsplit("/", 1)[0].replace("/", "_").lower() if "/" in name else "config_luban"
        raw = payload(bundle, "/" + name + ".g.bytes")
        tables[name] = constructors.table(raw, mapping[name])
    regions = [row for row in tables["Map/map_region"] if row["ClassId"] == 12]
    maps = [row for row in tables["Map/map"] if row["ClassId"] == 12]
    if len(maps) != 1 or not regions:
        raise ValueError("Map12 official map/region table is missing")

    schemas = ScriptSchemas(code_root=code)
    graphs = {}
    graph_inputs = {}
    pending = []
    for bundle in sorted(name for name in apk.index if "_scriptableconfig_story_map12story" in name):
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
        raise ValueError("No official Map12 story graphs were decoded")
    print(f"Map12: decoded {len(regions)} regions, {len(tables)} tables and {len(graphs)} story graphs", flush=True)

    terminals = {}
    for identity, components in local.items():
        terminal = components.get("LoEntityPuzzleTerminalComponent")
        if not terminal:
            continue
        params = json.loads(re.sub(r'([A-Za-z_][A-Za-z_0-9]*):', r'"\1":', terminal["Params"]))
        if terminal["MapWorldPuzzleType"] == 3:
            if set(params) != {"NeedRemoveLoEntityIds", "BoxLoEntityIds", "ResetTransferActionId"}:
                raise ValueError("Unexpected mountain HuarongDao parameters: " + str(identity))
            references = params["NeedRemoveLoEntityIds"] + params["BoxLoEntityIds"]
        elif terminal["MapWorldPuzzleType"] in (5, 6):
            if set(params) != {"LockLoEntityIds"}:
                raise ValueError("Unexpected mountain lock-terminal parameters: " + str(identity))
            references = params["LockLoEntityIds"]
        else:
            raise ValueError("Unexpected mountain terminal type: " + str(identity))
        if not set(references) <= set(local):
            raise ValueError("Map12 puzzle terminal refers to missing placement: " + str(identity))
        terminals[identity] = params
    if set(terminals) != TERMINALS:
        raise ValueError("Map12 must preserve all eleven original puzzle terminals")

    equivalence = verify_legacy_equivalence(navigation, placement, local, templates, tables, graphs)

    files = {
        "navigation.json": navigation, "placement.json": placement,
        "entities.json": local, "templates.json": templates,
        "tables.json": tables, "stories.json": graphs, "puzzle-terminals.json": terminals,
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
    meta(destination, folder=True)
    source_files = set(formats.sources.values())
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
        resource_version=apk.source.profile["resource_version"], mapClassId=12,
        operation="Decode original APK/verified cloud bytes using selected official assemblies; no source version substitution.",
        status="Decoded source data only; Unity integration, graphics and Windows Player validation remain required.",
        code_manifest=dict(path=source_path(code_manifest_path), sha256=sha(code_manifest_path.read_bytes())),
        assemblies=assemblies, inputs=inputs, ec_templates=template_sources, story_graphs=graph_inputs,
        decompiled_sources=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in sorted(source_files)],
        schemas=schemas.records, outputs=outputs, verified_legacy_equivalence=equivalence,
        counts=dict(regions=len(regions), placements=len(local), templates=len(templates),
                    stories=len(graphs), tables=len(tables), puzzle_terminals=len(terminals)),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())))
    write(receipt, encode(result))
    apk.archive.close()
    print("Map12 source receipt: " + source_path(receipt), flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=DESTINATION)
    parser.add_argument("--receipt", type=Path, default=RECEIPT)
    args = parser.parse_args()
    build(args.output.resolve(), args.receipt.resolve())
