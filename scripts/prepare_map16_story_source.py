"""Extract the selected official whale-island story graph and text closure."""
from __future__ import annotations

import hashlib
import json
import re
import struct
from pathlib import Path

import xxhash
from UnityPy.classes.PPtr import PPtr

from official_apk import Apk, ROOT
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from prepare_unity_map13_sources import encode, meta


VERSION = "884_156692"
MAP_ID = 16
STORY_PREFIX = "Map13Story/WhaleIsland/"
GRAPH_BUNDLE = "defaultpackage_assets_scriptableconfig_story_map13story_whaleisland.bundle"
DESTINATION = ROOT / "unity/Assets/Official/World/Map/16/Source/story-source.json"
RECEIPT = ROOT / "assets/reference/official-map/map16-story-source-manifest.json"
Wwise_NAMES = (
    "wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile",
    "wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile",
)


def sha(raw: bytes) -> str:
    return hashlib.sha256(raw).hexdigest()


def source_path(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def story_table_rows(rows: list[dict]) -> list[dict]:
    selected = [row for row in rows if row.get("ResPath", "").startswith(STORY_PREFIX)]
    names = [row.get("StoryName") for row in selected]
    if len(selected) != 7 or len(set(names)) != 7 or any(not name for name in names):
        raise ValueError("Official Map16 whale-island story table closure changed")
    return selected


def select_story_graphs(graphs: dict[str, dict], rows: list[dict]) -> dict[str, dict]:
    expected = {row["StoryName"] for row in story_table_rows(rows)}
    if set(graphs) != expected:
        raise ValueError("Official Map16 story graph names differ from the selected story table")
    return {name: graphs[name] for name in sorted(expected)}


def decode_graphs(apk: Apk, schemas: ScriptSchemas) -> tuple[dict, dict, set[str]]:
    if GRAPH_BUNDLE not in apk.index:
        raise FileNotFoundError("Selected APK is missing the whale-island story bundle")
    pending = [obj for obj in apk.objects(GRAPH_BUNDLE)
               if obj.type.name == "MonoBehaviour"
               and obj.read(check_read=False).m_Script.read().m_ClassName == "StoryGraph"]
    graphs, inputs, node_types = {}, {}, set()
    while pending:
        graph = pending.pop(0)
        definition = schemas.read(graph)
        name = definition["m_Name"]
        if name in graphs:
            continue
        nodes, by_path = {}, {}
        for pointer in definition["nodes"]:
            obj = PPtr(**pointer, assetsfile=graph.assets_file).deref()
            data = schemas.read(obj)
            node_type = obj.read(check_read=False).m_Script.read().m_ClassName
            data["_type"] = node_type
            identity = str(data["NodeId"])
            if identity in nodes:
                raise ValueError("Duplicate original Map16 story node: " + name + ":" + identity)
            by_path[str(obj.path_id)] = identity
            nodes[identity] = data
            node_types.add(node_type)
            subgraph = data.get("Subgraph", {})
            if subgraph.get("m_PathID"):
                pending.append(PPtr(**subgraph, assetsfile=obj.assets_file).deref())
        for data in nodes.values():
            ports = {}
            for port in data.pop("ports", {}).get("values", []):
                connections = []
                for connection in port["connections"]:
                    target_path = str(connection["node"]["m_PathID"])
                    if target_path == "0":
                        continue
                    if target_path not in by_path:
                        raise ValueError("Original Map16 story port targets a node outside its graph")
                    connections.append(dict(node=by_path[target_path], port=connection["fieldName"]))
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
        graphs[name] = dict(nodes=nodes,
            properties={key: value for key, value in definition.items() if key != "nodes"})
        raw = graph.get_raw_data()
        inputs[name] = dict(apk.provenance(graph), object_sha256=sha(raw), node_count=len(nodes))
    return graphs, inputs, node_types


def decode_localization(apk: Apk, keys: set[str], required_keys: set[str]) -> tuple[dict[str, str], list[dict]]:
    wanted = {xxhash.xxh64_intdigest(key.encode("utf8")): key for key in sorted(keys)}
    localized, inputs = {}, []
    manifest = {row["name"]: row for row in apk.source.manifest("WwiseBnk")["bundles"]}
    for name in Wwise_NAMES:
        record = manifest[name]
        entry = "assets/yoo/WwiseBnk/" + record["file"]
        if entry in apk.available:
            raw = apk.archive.read(entry)
            origin = dict(origin="apk_zip", apk_entry=entry, apk_sha256=apk.source.apk_sha256)
        else:
            cached = apk.cloud.read("WwiseBnk", record)
            if cached is None:
                raise FileNotFoundError("Selected official Chinese text rawfile is unavailable: " + name)
            raw, origin = cached
        if len(raw) != record["size"] or hashlib.md5(raw).hexdigest() != record["hash"]:
            raise ValueError("Selected official Chinese text rawfile differs from its manifest")
        inputs.append(dict(origin, name=name, payload_sha256=sha(raw), bytes=len(raw), md5=record["hash"]))
        count = struct.unpack_from("<i", raw)[0]
        start = 4 + count * 12
        if start > len(raw):
            raise ValueError("Official Chinese text index is truncated")
        for index in range(count):
            identity, offset = struct.unpack_from("<Qi", raw, 4 + index * 12)
            key = wanted.get(identity)
            if key is None or key in localized:
                continue
            position = start + offset
            if offset < 0 or position + 2 > len(raw):
                raise ValueError("Official Chinese text offset is outside the rawfile")
            size = struct.unpack_from("<H", raw, position)[0]
            if position + 2 + size > len(raw):
                raise ValueError("Official Chinese text payload is truncated")
            localized[key] = raw[position + 2:position + 2 + size].decode("utf8")
    missing = sorted(required_keys - set(localized))
    if missing:
        raise ValueError("Original Map16 Chinese text is missing: " + repr(missing[:20]))
    return localized, inputs


def build() -> dict:
    apk = Apk.current()
    if apk.version != VERSION:
        raise ValueError("Map16 story source differs from the selected official version")
    code_root = ROOT / ".tools/source-packages/current" / VERSION / "official-code"
    code_manifest_path = code_root.parent / "code-manifest.json"
    code_manifest = json.loads(code_manifest_path.read_bytes())
    if code_manifest.get("manifest") != VERSION:
        raise ValueError("Map16 story schemas do not belong to the selected source version")
    constructors = Constructors(code_root)
    schemas = ScriptSchemas(code_root=code_root)
    inputs = []

    def table(name: str, constructor: str, package: str = "config_luban") -> list[dict]:
        obj = apk.asset(package, "/" + name + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        inputs.append(dict(apk.provenance(obj), table=name, payload_sha256=sha(raw),
            object_sha256=sha(obj.get_raw_data()), bytes=len(raw), constructor=constructor))
        return constructors.table(raw, constructor)

    all_story_rows = table("story", "TbStoryInfo")
    selected_rows = story_table_rows(all_story_rows)
    graph_data, graph_inputs, node_types = decode_graphs(apk, schemas)
    graphs = select_story_graphs(graph_data, selected_rows)
    dialogue_names = set(graphs)
    dialogs = [row for row in table("dialog", "TbDialogInfo") if row.get("StoryName") in dialogue_names]
    options = [row for row in table("story_option_dialog", "TbStoryOptionInfo")
        if row.get("StoryName") in dialogue_names]
    if {row["StoryName"] for row in selected_rows} - set(graphs):
        raise ValueError("Map16 story graph closure is missing a configured story")

    dialogues = {}
    for row in dialogs:
        dialogues.setdefault(row["StoryName"], {}).setdefault(row["Id"], []).append(row)
    for groups in dialogues.values():
        for rows in groups.values():
            rows.sort(key=lambda row: row["Order"])
    role_ids = {row["RoleId"] for groups in dialogues.values() for rows in groups.values()
                for row in rows if row["RoleId"] > 0}
    dialog_roles = [row for row in table("Dialog/dialog_role", "TbDialogRoleInfo", "config_luban_dialog")
                    if row["RoleId"] in role_ids]
    if {row["RoleId"] for row in dialog_roles} != role_ids:
        raise ValueError("Map16 story dialogue references an unavailable original role")
    map_logic_tags = [row for row in table("Map/map_logic_entity_tag", "TbMapLogicEntityTagInfo", "config_luban_map")
                      if row["MapClass"] == MAP_ID]
    hand_in_ids = {int(node.get("HandInItemId", node.get("HandleItemId", 0)))
                   for graph in graphs.values() for node in graph["nodes"].values()
                   if node["_type"] in ("CanHandInItemNode", "HandInItemNode")}
    hand_in_ids.discard(0)
    hand_in_items = [row for row in table("hand_in_item", "TbHandInItemInfo")
                     if row["Id"] in hand_in_ids]
    if {row["Id"] for row in hand_in_items} != hand_in_ids:
        raise ValueError("Map16 story references a missing original hand-in item configuration")
    fixed_reward_ids = {int(node.get("FixedRewardId", node.get("AwardId", 0)))
                        for graph in graphs.values() for node in graph["nodes"].values()
                        if node["_type"] in ("GetFixAwardNode", "CanGetFixedRewardNode")}
    fixed_reward_ids.discard(0)
    fixed_rewards = [row for row in table("fixed_reward", "TbFixedRewardInfo")
                     if row["Id"] in fixed_reward_ids]
    if {row["Id"] for row in fixed_rewards} != fixed_reward_ids:
        raise ValueError("Map16 story references a missing original fixed reward")
    keys = {f"dialog_{story}_{row['Id']}_{row['Order']}" for story, groups in dialogues.items()
        for rows in groups.values() for row in rows}
    keys.update(f"story_{row['StoryName']}_{row['Id']}_option" for row in options)
    required_keys = set(keys)
    def collect(value):
        if isinstance(value, dict):
            for key, item in value.items():
                collect(key)
                collect(item)
        elif isinstance(value, list):
            for item in value:
                collect(item)
        elif isinstance(value, str) and re.fullmatch(r"[A-Za-z][A-Za-z0-9_.:{}-]*", value):
            keys.add(value)
    collect(selected_rows)
    collect(options)
    collect(graphs)
    localized, localization_inputs = decode_localization(apk, keys, required_keys)

    server_records = {}
    for path, pointer in apk.container("config_story"):
        if not path.endswith(".json"):
            continue
        name = path.rsplit("/", 1)[-1].removesuffix(".json")
        if name not in graphs:
            continue
        obj = pointer.deref()
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        server_records[name] = json.loads(raw)
        inputs.append(dict(apk.provenance(obj), asset=path, payload_sha256=sha(raw),
            object_sha256=sha(obj.get_raw_data()), bytes=len(raw)))

    story_source = {
        "manifest": VERSION,
        "mapClassId": MAP_ID,
        "storyPrefix": STORY_PREFIX,
        "storyRows": selected_rows,
        "graphs": graphs,
        "dialogues": dialogues,
        "dialogRoles": dialog_roles,
        "mapLogicTags": map_logic_tags,
        "storyOptions": options,
        "fixedRewards": fixed_rewards,
        "handInItems": hand_in_items,
        "localization": localized,
        "serverRecords": server_records,
    }
    output = encode(story_source)
    DESTINATION.parent.mkdir(parents=True, exist_ok=True)
    DESTINATION.write_bytes(output)
    meta(DESTINATION)
    record = dict(format=1, manifest=VERSION, mapClassId=MAP_ID,
        operation="Decode official Map16 whale-island story graphs, dialogue, roles, map logic tags, options, referenced rewards and hand-in configuration, Chinese text and paired server records; no story runtime or server execution is inferred.",
        codeManifestSha256=sha(code_manifest_path.read_bytes()),
        tables=inputs,
        localizationInputs=localization_inputs,
        graphs=[dict(name=name, **graph_inputs[name]) for name in sorted(graph_inputs)],
        schemaRecords=schemas.records,
        nodeTypes=[dict(name=name, sha256=sha((code_root / "client-source" / (name + ".cs")).read_bytes()))
            for name in sorted(node_types) if (code_root / "client-source" / (name + ".cs")).is_file()],
        outputs=[dict(path=source_path(DESTINATION), bytes=len(output), sha256=sha(output)),
            dict(path=source_path(DESTINATION.with_name(DESTINATION.name + ".meta")),
                bytes=DESTINATION.with_name(DESTINATION.name + ".meta").stat().st_size,
                sha256=sha(DESTINATION.with_name(DESTINATION.name + ".meta").read_bytes()))],
        counts=dict(stories=len(graphs), storyNodes=sum(len(value["nodes"]) for value in graphs.values()),
            dialogues=sum(len(value) for value in dialogues.values()), dialogueRows=len(dialogs),
            dialogRoles=len(dialog_roles), mapLogicTags=len(map_logic_tags), options=len(options),
            fixedRewards=len(fixed_rewards), handInItems=len(hand_in_items),
            localizationKeys=len(localized), serverRecords=len(server_records)))
    RECEIPT.write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    apk.archive.close()
    print(json.dumps(record["counts"], ensure_ascii=False, sort_keys=True))
    return record


if __name__ == "__main__":
    build()
