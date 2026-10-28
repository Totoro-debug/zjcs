"""Recover the selected official Map13 MapTalk graphs and follower EC closure."""
import hashlib
import json
from pathlib import Path
import re
import struct
import xxhash
from official_apk import Apk, ROOT
from UnityPy.classes.PPtr import PPtr
from official_ec_codec import Formats
from official_tables import Constructors
from official_map_codec import unpack
from official_manifest import read_asset_manifest
from official_unity_schema import ScriptSchemas
from prepare_unity_map13_sources import encode, meta, sha, source_path, write

SOURCE = ROOT / "unity/Assets/Official/World/Map/13/Source"
BASE_RECEIPT = ROOT / "assets/reference/official-map/map13-source-manifest.json"
OUTPUT = SOURCE / "story-dependencies.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-story-dependencies-source-manifest.json"


def build(map_class_id=13):
    if map_class_id not in (13, 14):
        raise ValueError("Unsupported current mainline story dependency map")
    SOURCE = ROOT / f"unity/Assets/Official/World/Map/{map_class_id}/Source"
    BASE_RECEIPT = ROOT / f"assets/reference/official-map/map{map_class_id}-source-manifest.json"
    OUTPUT = SOURCE / "story-dependencies.json"
    RECEIPT = ROOT / f"assets/reference/official-map/map{map_class_id}-story-dependencies-source-manifest.json"
    base = json.loads(BASE_RECEIPT.read_bytes())
    apk = Apk.current()
    if base["manifest"] != apk.version or base["mapClassId"] != map_class_id:
        raise ValueError("Original Map" + str(map_class_id) + " source version differs")
    expected = {Path(r["path"]).name: r["sha256"] for r in base["outputs"]}
    inputs = [BASE_RECEIPT]
    def read(name):
        path = SOURCE / name
        if sha(path.read_bytes()) != expected[name]: raise ValueError("Pinned source changed: " + name)
        inputs.append(path)
        return json.loads(path.read_bytes())
    tables, templates, original = read("tables.json"), read("templates.json"), read("stories.json")
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    for record in base["assemblies"].values():
        if sha((ROOT / record["path"]).read_bytes()) != record["decoded_sha256"]:
            raise ValueError("Official assembly differs from the pinned source")
    schemas = ScriptSchemas(code_root=code)
    logic = [r for r in tables["Map/map_logic_entity_tag"] if r["MapClass"] == map_class_id]
    tags = {(f"entity_{r['EntityClass']}_name", r["Tag"]) for r in logic}
    tags.update((f"entity_{i}_name", "") for i in templates)
    talks = [r for r in tables["Map/map_talk"] if (r["EntityId"], r["Tag"]) in tags]
    story_rows = {r["StoryName"]: r for r in tables["story"]}
    required = {r["TalkId"] for r in talks if r["TalkId"]} - set(original)
    roots = {}
    manifest_assets = read_asset_manifest(apk.source.manifest_bytes("DefaultPackage"))
    resource_bindings = {}
    bundles = set()
    for name in sorted(required):
        configured = "Assets/ScriptableConfig/Story/" + story_rows[name]["ResPath"] + ".asset"
        matches = [row for row in manifest_assets if row["path"] == configured]
        if not matches:
            # Preserve the configured path discrepancy as evidence. Recovery
            # requires a single official StoryGraph asset with the configured
            # StoryName, its actual manifest GUID and bundle, then exact m_Name
            # and object-type checks below. No graph or condition is invented.
            matches = [row for row in manifest_assets if row["path"].startswith("Assets/ScriptableConfig/Story/")
                       and Path(row["path"]).name == name + ".asset"]
        if len(matches) != 1:
            raise ValueError("Original story asset identity is unresolved or ambiguous: " + name)
        row = matches[0]
        resource_bindings[name] = dict(configuredPath=configured, assetPath=row["path"], guid=row["guid"],
                                       bundle=row["bundle"], configuredPathMatches=row["path"] == configured)
        bundles.add(row["bundle"])
    for bundle in sorted(bundles):
        for path, ptr in apk.container(bundle):
            name = Path(path).stem
            if name not in required: continue
            obj = ptr.deref()
            if obj.type.name != "MonoBehaviour" or obj.read(check_read=False).m_Script.read().m_ClassName != "StoryGraph": continue
            if path != resource_bindings[name]["assetPath"] or obj.read(check_read=False).m_Name != name:
                raise ValueError("Original story resource path differs: " + name)
            if name in roots and roots[name].path_id != obj.path_id: raise ValueError("Ambiguous original story: " + name)
            roots[name] = obj
    if set(roots) != required: raise ValueError("Missing original MapTalk graph roots: " + repr(required - set(roots)))
    graphs, graph_sources, node_sources = {}, {}, {}
    queue = list(roots.values())
    while queue:
        graph = queue.pop(0); definition = schemas.read(graph); name = definition["m_Name"]
        if name in original:
            if base["story_graphs"][name]["object_sha256"] != sha(graph.get_raw_data()):
                raise ValueError("Shared subgraph identity changed: " + name)
            continue
        if name in graphs: continue
        nodes, by_path = {}, {}
        for ptr in definition["nodes"]:
            obj = PPtr(**ptr, assetsfile=graph.assets_file).deref(); data = schemas.read(obj)
            data["_type"] = obj.read(check_read=False).m_Script.read().m_ClassName
            identity = str(data["NodeId"])
            if identity in nodes: raise ValueError("Duplicate original node: " + name + ":" + identity)
            by_path[str(obj.path_id)] = identity; nodes[identity] = data
            node_sources[name + ":" + identity] = dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
            sub = data.get("Subgraph", {})
            if sub.get("m_PathID"): queue.append(PPtr(**sub, assetsfile=obj.assets_file).deref())
        for data in nodes.values():
            ports = {}
            for port in data.pop("ports", {}).get("values", []):
                connections = []
                for connection in port["connections"]:
                    target = str(connection["node"]["m_PathID"])
                    if target != "0": connections.append(dict(node=by_path[target], port=connection["fieldName"]))
                ports[port["_fieldName"]] = dict(direction=port["_direction"], flow=port.get("_flowConnectionType", 0), connections=connections)
            data["ports"] = ports
            for field in ("WrappingInputNode", "WrappingOutputNode", "GoTo"):
                if field in data: data[field] = by_path.get(str(data[field]["m_PathID"]), "")
            sub = data.get("Subgraph", {})
            if sub.get("m_PathID"):
                data["subgraph_name"] = schemas.read(PPtr(**sub, assetsfile=graph.assets_file).deref())["m_Name"]
            for field in ("m_GameObject", "m_Script", "m_Enabled", "graph", "position", "Mono", "IsMonoShow"): data.pop(field, None)
        graphs[name] = dict(nodes=nodes, properties={k: v for k, v in definition.items() if k != "nodes"})
        graph_sources[name] = dict(source=apk.provenance(graph), object_sha256=sha(graph.get_raw_data()))
    followers = sorted({i for graph in [*original.values(), *graphs.values()] for n in graph["nodes"].values()
        if n["_type"] == "AddNpcFollowerNode" for i in n["NpcClassIds"]})
    extra_templates, template_sources = {}, {}
    formats = Formats(code)
    candidates = {}
    for path, ptr in apk.container("config_ec"):
        name = path.rsplit("/", 1)[-1]
        if name.startswith("map_") or not name.startswith(("World", "ActionWorld", "ActionCommon")): continue
        obj = ptr.deref(); raw = obj.read().m_Script.encode("utf8", "surrogateescape"); legacy = False
        try: rows = unpack(raw)[0]
        except (ValueError, TypeError): rows = unpack(raw[2:])[0]; legacy = True
        for parent, identity, components in rows:
            if parent: continue
            if identity in candidates and legacy and not candidates[identity][1]: continue
            candidates[identity] = (components, legacy, dict(source=apk.provenance(obj), asset=path, payload_sha256=sha(raw)))
    def action_refs(value):
        if isinstance(value, list):
            for v in value: yield from action_refs(v)
        elif isinstance(value, dict):
            for k, v in value.items():
                if k.endswith(("ActionId", "ActionIds")):
                    for i in v if isinstance(v, list) else [v]:
                        if isinstance(i, int) and i > 0: yield i
                yield from action_refs(v)
        elif isinstance(value, str) and value.startswith("{"):
            try: yield from action_refs(json.loads(re.sub(r'([A-Za-z_][A-Za-z_0-9]*):', r'"\1":', value)))
            except json.JSONDecodeError: pass
    pending = set(followers) - {int(k) for k in templates}
    while pending:
        identity = min(pending); pending.remove(identity)
        if str(identity) in templates or str(identity) in extra_templates: continue
        components, legacy, record = candidates[identity]
        values = {typ.removeprefix("Common."): formats.component(typ, raw[2:] if legacy and raw else raw) for typ, raw in components}
        extra_templates[str(identity)] = values; template_sources[str(identity)] = record
        pending.update(action_refs(values))
    constructors = Constructors(code)
    quick_buy_ids = {n["ItemId"] for graph in [*original.values(), *graphs.values()] for n in graph["nodes"].values() if n["_type"] == "ItemQuikBuyNode"}
    obj = apk.asset("config_luban", "/quick_buy.g.bytes")
    quick_raw = obj.read().m_Script.encode("utf8", "surrogateescape")
    quick_buys = [r for r in constructors.table(quick_raw, "TbQuickBuyInfo") if r["ItemId"] in quick_buy_ids]
    if {r["ItemId"] for r in quick_buys} != quick_buy_ids: raise ValueError("Unresolved original story quick buys")
    quick_source = dict(source=apk.provenance(obj), payload_sha256=sha(quick_raw), object_sha256=sha(obj.get_raw_data()))
    dialogues = {}
    for row in tables["dialog"]:
        if row["StoryName"] in graphs: dialogues.setdefault(row["StoryName"], {}).setdefault(str(row["Id"]), []).append(row)
    for groups in dialogues.values():
        for rows in groups.values(): rows.sort(key=lambda r: r["Order"])
    keys = {f"dialog_{name}_{r['Id']}_{r['Order']}" for name, groups in dialogues.items() for rows in groups.values() for r in rows}
    keys.update(f"story_{r['StoryName']}_{r['Id']}_option" for r in tables["story_option_dialog"] if r["StoryName"] in graphs)
    hashes = {xxhash.xxh64_intdigest(k.encode()): k for k in keys}; localized, language_sources = {}, []
    for record in apk.source.manifest("WwiseBnk")["bundles"]:
        if record["name"] not in ("wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile", "wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile"): continue
        entry = "assets/yoo/WwiseBnk/" + record["file"]
        if entry in apk.available: raw = apk.archive.read(entry); origin = dict(origin="apk_zip", apk_entry=entry, apk_sha256=apk.source.apk_sha256)
        else:
            cached = apk.cloud.read("WwiseBnk", record)
            if cached is None: raise FileNotFoundError(record["name"])
            raw, origin = cached
        if len(raw) != record["size"] or hashlib.md5(raw).hexdigest() != record["hash"]: raise ValueError("Official language rawfile hash differs")
        language_sources.append(dict(origin, name=record["name"], sha256=sha(raw)))
        count = struct.unpack_from("<i", raw)[0]; start = 4 + count * 12
        for index in range(count):
            identity, offset = struct.unpack_from("<Qi", raw, 4 + index * 12)
            if identity not in hashes: continue
            pos = start + offset; size = struct.unpack_from("<H", raw, pos)[0]
            if offset < 0 or pos + 2 + size > len(raw): raise ValueError("Invalid original language entry range")
            localized[hashes[identity]] = raw[pos + 2:pos + 2 + size].decode("utf8")
    if keys - set(localized): raise ValueError("Unresolved original dialogue text: " + repr(keys - set(localized)))
    server = {}
    for path, ptr in apk.container("config_story"):
        if not path.endswith(".json") or Path(path).stem not in graphs: continue
        obj = ptr.deref(); raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        server[Path(path).stem] = dict(records=json.loads(raw), source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
    data = dict(manifest=apk.version, mapClassId=map_class_id, mapTalkEntries=talks, primaryStories=sorted(required), graphs=graphs,
        dialogues=dialogues, localization=localized, followerEntityClassIds=followers, entityTemplates=extra_templates, quickBuys=quick_buys, serverStoryRecords=server)
    write(OUTPUT, encode(data)); meta(OUTPUT)
    tools = [Path(__file__).resolve(), ROOT / "scripts/official_ec_codec.py", ROOT / "scripts/official_unity_schema.py", ROOT / "scripts/official_manifest.py"]
    if map_class_id == 14: tools.append(ROOT / "scripts/prepare_unity_map14_story_dependencies.py")
    source_files = [code / "client-source" / (n["_type"] + ".cs") for g in graphs.values() for n in g["nodes"].values()]
    source_files += [code / "client-source/StoryResourceManager.cs"]
    source_files += list(formats.sources.values())
    for name in constructors.sources:
        source_files += [code / "common-source" / folder / (name + ".cs") for folder in ("Common", "Common.LubanGen")]
    receipt = dict(manifest=apk.version, mapClassId=map_class_id, inputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in inputs],
        resourceBindings=resource_bindings, graphs=graph_sources, nodes=node_sources, templates=template_sources, quickBuyTable=quick_source, languages=language_sources, schemas=schemas.records,
        tools=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in tools],
        officialCode=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in sorted(set(source_files)) if p.is_file()],
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in (OUTPUT, Path(str(OUTPUT) + ".meta"))],
        counts=dict(mapTalkEntries=len(talks), primaryStories=len(required), extraGraphs=len(graphs), extraNodes=sum(len(g["nodes"]) for g in graphs.values()),
            followerClasses=len(followers), extraEntityTemplates=len(extra_templates), quickBuys=len(quick_buys), dialogueLines=sum(len(rows) for groups in dialogues.values() for rows in groups.values())),
        operation=f"Recover original Map{map_class_id} MapTalk shared graph/subgraph closure and follower EC templates missing from the map-local source selection; preserve raw node order/ports, source field values and Chinese text. No server behavior is inferred; runtime binding remains required.")
    write(RECEIPT, encode(receipt)); apk.archive.close(); print("Map" + str(map_class_id) + " story dependencies:", receipt["counts"], flush=True)
    return data


if __name__ == "__main__": build()
