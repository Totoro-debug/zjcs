"""Recover the complete Map13 skeleton data, atlas links and lazy animations.

This is a format conversion using the pinned MIT binary reader, not a Spine
runtime. Original renderer/skin/constraint data remain explicit native inputs.
"""
import argparse
from collections import Counter
import json
from pathlib import Path

from prepare_unity_map13_entity_graph import Inputs, OUTPUT as GRAPH, RECEIPT as GRAPH_RECEIPT, write
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from convert_official_skeletons import reader_type, WHEEL_SHA256
from prepare_unity_map13_entity_inputs import OUTPUT as COMPONENTS, RECEIPT as INPUT_RECEIPT
from prepare_unity_map13_sources import encode, meta, sha, source_path

DESTINATION = ROOT / "unity/Assets/Official/World/Map/13/Source/EntitySkeletons"
RECEIPT = ROOT / "assets/reference/official-map/map13-entity-skeletons-source-manifest.json"


def key(value):
    return value["cab"].casefold(), str(value["path_id"])


def checked(path, expected):
    raw = path.read_bytes()
    if sha(raw) != expected:
        raise ValueError("Map13 skeleton dependency changed: " + str(path))
    return raw


def validate_references(skeleton):
    """Check decoded identifiers independently of the binary reader's cursor."""
    bones, slots = set(), set()
    for bone in skeleton["bones"]:
        if bone["name"] in bones or (bone.get("parent") and bone["parent"] not in bones):
            raise ValueError("Invalid original bone name/parent order")
        bones.add(bone["name"])
    for slot in skeleton.get("slots", []):
        if slot["name"] in slots or slot["bone"] not in bones:
            raise ValueError("Invalid original slot bone/name")
        slots.add(slot["name"])
    names = {kind: {row["name"] for row in skeleton.get(kind, [])}
        for kind in ("ik", "transform", "path", "physics")}
    for kind, wanted in names.items():
        if len(wanted) != len(skeleton.get(kind, [])):
            raise ValueError("Duplicate original constraint name: " + kind)
        for row in skeleton.get(kind, []):
            if any(b not in bones for b in row.get("bones", [])):
                raise ValueError("Unknown original constraint bone: " + kind)
            if row.get("bone") and row["bone"] not in bones:
                raise ValueError("Unknown original physics bone")
            target_names = slots if kind == "path" else bones
            if row.get("target") and row["target"] not in target_names:
                raise ValueError("Unknown original constraint target: " + kind)
    skin_names = set()
    for skin in skeleton.get("skins", []):
        if skin["name"] in skin_names:
            raise ValueError("Duplicate original skin name")
        skin_names.add(skin["name"])
        for kind, wanted in dict(names, bones=bones).items():
            if not set(skin.get(kind, [])) <= wanted:
                raise ValueError("Unknown original skin membership: " + kind)
        if not set(skin.get("attachments", {})) <= slots:
            raise ValueError("Unknown original skin attachment slot")
    for animation in skeleton.get("animations", {}).values():
        for kind, wanted in dict(names, bones=bones, slots=slots).items():
            # Physics may contain a global timeline under the empty name.
            if not (set(animation.get(kind, {})) - ({""} if kind == "physics" else set())) <= wanted:
                raise ValueError("Unknown original animation timeline target: " + kind)
        if not set(animation.get("deform", {})) <= skin_names:
            raise ValueError("Unknown original deform skin")


def build(graph_path=GRAPH, graph_receipt_path=GRAPH_RECEIPT):
    graph_receipt = json.loads(graph_receipt_path.read_bytes())
    graph = json.loads(checked(graph_path, graph_receipt["outputs"][0]["sha256"]))
    if graph["manifest"] != "871_156598" or graph["mapClassId"] != 13:
        raise ValueError("Wrong original Map13 skeleton selection")
    input_receipt = json.loads(checked(INPUT_RECEIPT, graph_receipt["input_receipt"]["sha256"]))
    components = {key(c): c for c in json.loads(checked(COMPONENTS,
        graph_receipt["component_source"]["sha256"]))["components"]}
    objects = {key(o): o for o in graph["objects"]}
    indices = {entry["output"]: index for index, entry in enumerate(input_receipt["inputs"])}
    inputs = Inputs(input_receipt["inputs"])
    reader_class, binary_input = reader_type()
    apk = Apk.current()
    manifest_raw = apk.source.manifest_bytes("DefaultPackage", apk.archive)
    assets = read_asset_manifest(manifest_raw)
    by_guid = {}
    for asset in assets:
        if asset["guid"]:
            if asset["guid"] in by_guid:
                raise ValueError("Ambiguous original animation AssetTable GUID")
            by_guid[asset["guid"]] = asset
    outputs, original_texts, skeletons = [], {}, []

    def save(path, raw):
        parent = path.parent
        while parent != DESTINATION.parent:
            parent.mkdir(parents=True, exist_ok=True)
            meta(parent, folder=True)
            parent = parent.parent
        write(path, raw)
        meta(path)
        outputs.append(dict(path=source_path(path), sha256=sha(raw),
            meta_sha256=sha(Path(str(path) + ".meta").read_bytes())))
        return outputs[-1]

    def edge(owner, field):
        matches = [r for r in owner["references"] if r["field"] == field]
        if len(matches) != 1:
            raise ValueError("Original skeleton reference is absent/ambiguous: " + field)
        return objects[key(matches[0])]

    def fields(owner):
        component = components[key(owner)]
        if component["payload_sha256"] != owner["original_payload_sha256"]:
            raise ValueError("Original skeleton component fields changed")
        return component["fields"]

    def text(owner):
        if owner["type"] != "TextAsset":
            raise ValueError("Original skeleton/atlas reference is not a TextAsset")
        info = dict(owner, input_index=indices[owner["input"]])
        obj = inputs.reader(info)
        checked_raw = obj.get_raw_data()
        if sha(checked_raw) != owner["payload_sha256"]:
            raise ValueError("Original skeleton TextAsset payload changed")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        original_texts[key(owner)] = dict(cab=owner["cab"], path_id=owner["path_id"],
            name=owner.get("name", ""), input=owner["input"],
            payload_sha256=owner["payload_sha256"], content_sha256=sha(raw))
        return raw

    renderers = [o for o in graph["objects"] if o.get("managed_type") in
        ("Spine.Unity.SkeletonMecanim", "Spine.Unity.SkeletonAnimation", "Spine.Unity.SkeletonGraphic")]
    lazy_by_go = {}
    for obj in graph["objects"]:
        if obj.get("managed_type") == "SpineLazyAnimLoad":
            go = edge(obj, ".m_GameObject")
            if key(go) in lazy_by_go:
                raise ValueError("Multiple original lazy animation loaders on one model")
            lazy_by_go[key(go)] = edge(obj, ".mSplitBinarySo")
    loaders, renderer_bindings = {}, []
    for renderer in renderers:
        source = fields(renderer)
        if not source["skeletonDataAsset"]["m_PathID"]:
            renderer_bindings.append(dict(cab=renderer["cab"], path_id=renderer["path_id"],
                managed_type=renderer["managed_type"], skeleton=None, fields=source))
            continue
        skeleton = edge(renderer, ".skeletonDataAsset")
        go = edge(renderer, ".m_GameObject")
        loader = lazy_by_go.get(key(go))
        if loader:
            previous = loaders.setdefault(key(skeleton), loader)
            if key(previous) != key(loader):
                raise ValueError("One original skeleton uses different lazy animation tables")
        renderer_bindings.append(dict(cab=renderer["cab"], path_id=renderer["path_id"],
            managed_type=renderer["managed_type"], skeleton=dict(cab=skeleton["cab"],
                path_id=skeleton["path_id"]), fields=source))

    for original in [o for o in graph["objects"] if o.get("managed_type") == "Spine.Unity.SkeletonDataAsset"]:
        source = fields(original)
        folder = DESTINATION / sha((original["cab"].casefold() + ":" + original["path_id"]).encode())[:24]
        folder.mkdir(parents=True, exist_ok=True)
        meta(folder, folder=True)
        raw = text(edge(original, ".skeletonJSON"))
        raw_output = save(folder / "original.skel.bytes", raw)
        reader = reader_class()
        json_source = raw.lstrip().startswith(b"{")
        if json_source:
            decoded = json.loads(raw)
            strings = []
        else:
            decoded = reader.convert(raw)
            if reader.inp.index != len(raw):
                raise ValueError("Original skeleton binary was not fully consumed: " + original["name"])
            strings = list(reader.inp.strings)
        embedded_names = list(decoded.get("animations", {}))
        split_records = []
        loader = loaders.get(key(original))
        if loader:
            table = fields(loader)
            if strings != table["StringArray"]:
                raise ValueError("Original lazy animation string table differs: " + original["name"])
            seen = set()
            for row in table["ListAnimationContent"]:
                name, guid = row["ContentName"], row["TextAsset"]["mAssetGuid"]
                if name in seen or not guid or guid not in by_guid:
                    raise ValueError("Unresolved/duplicate original lazy animation: " + name)
                seen.add(name)
                asset = by_guid[guid]
                candidates = [(path, ptr.deref()) for path, ptr in apk.container(asset["bundle"])
                    if path.casefold() == asset["path"].casefold()]
                if len(candidates) != 1 or candidates[0][1].type.name != "TextAsset":
                    raise ValueError("Original lazy animation GUID does not identify one TextAsset")
                obj = candidates[0][1]
                split_raw = obj.read().m_Script.encode("utf8", "surrogateescape")
                reader.inp = binary_input(split_raw)
                reader.inp.strings = strings
                if reader.inp.read_string() != name:
                    raise ValueError("Original lazy animation table/name differs")
                animation = reader._read_animation()
                if reader.inp.index != len(split_raw):
                    raise ValueError("Original lazy animation bytes were not fully consumed: " + name)
                if name in decoded.setdefault("animations", {}) and decoded["animations"][name] != animation:
                    raise ValueError("Original lazy animation conflicts with an embedded animation: " + name)
                decoded["animations"][name] = animation
                binary_output = save(folder / "Split" / (sha(name.encode()) + ".bytes"), split_raw)
                split_records.append(dict(name=name, asset=asset, source=apk.provenance(obj),
                    payload_sha256=sha(obj.get_raw_data()), output=binary_output))
        atlases = []
        for index in range(len(source["atlasAssets"])):
            atlas = edge(original, f".atlasAssets[{index}]")
            atlas_source = fields(atlas)
            atlas_output = save(folder / (f"atlas-{index}.txt"), text(edge(atlas, ".atlasFile")))
            atlases.append(dict(cab=atlas["cab"], path_id=atlas["path_id"], fields=atlas_source,
                references=atlas["references"], output=atlas_output))
        validate_references(decoded)
        payload = (json.dumps(decoded, ensure_ascii=False, separators=(",", ":"),
            allow_nan=False) + "\n").encode("utf8")
        output = save(folder / "skeleton.json", payload)
        skeletons.append(dict(cab=original["cab"], path_id=original["path_id"],
            name=original["name"], fields=source, references=original["references"],
            source_format="json" if json_source else "binary",
            original=raw_output, decoded=output, atlases=atlases, embedded_animations=embedded_names,
            lazy_loader=dict(cab=loader["cab"], path_id=loader["path_id"], fields=fields(loader)) if loader else None,
            declared_non_split_names_absent_from_binary=[name for name in fields(loader)["ListNoSplitAnimationNames"]
                if name not in embedded_names] if loader else [],
            split_animations=split_records, counts={k: len(decoded.get(k, [])) for k in
                ("bones", "slots", "skins", "ik", "transform", "path", "physics", "animations")}))
        print("Decoded original Map13 skeleton", original["name"], skeletons[-1]["counts"], flush=True)
    catalog = DESTINATION.parent / "native-entity-skeletons.json"
    save(catalog, encode(dict(manifest=graph["manifest"], mapClassId=13,
        skeletons=skeletons, renderers=renderer_bindings)))
    counts = dict(skeletons=len(skeletons), renderers=len(renderer_bindings),
        bound_renderers=sum(r["skeleton"] is not None for r in renderer_bindings),
        original_null_skeleton_renderers=sum(r["skeleton"] is None for r in renderer_bindings),
        source_formats=dict(Counter(s["source_format"] for s in skeletons)),
        absent_non_split_declarations=sum(len(s["declared_non_split_names_absent_from_binary"]) for s in skeletons),
        atlases=sum(len(s["atlases"]) for s in skeletons),
        split_animations=sum(len(s["split_animations"]) for s in skeletons),
        decoded_totals=dict(sum((Counter(s["counts"]) for s in skeletons), Counter())))
    tools = [Path(__file__), ROOT / "scripts/convert_official_skeletons.py"]
    client_sources = [ROOT / ".tools/source-packages/official-code/client-source" / name for name in
        ("SpineAnimationSplitBinarySO.cs", "SpineLazyAnimLoad.cs", "AppearanceCombineSkin.cs")]
    write(RECEIPT, encode(dict(format=1, manifest=graph["manifest"], mapClassId=13,
        graph=dict(path=source_path(graph_path), sha256=sha(graph_path.read_bytes())),
        graph_receipt=dict(path=source_path(graph_receipt_path), sha256=sha(graph_receipt_path.read_bytes())),
        input_receipt=dict(path=source_path(INPUT_RECEIPT), sha256=sha(INPUT_RECEIPT.read_bytes())),
        component_source=dict(path=source_path(COMPONENTS), sha256=sha(COMPONENTS.read_bytes())),
        asset_table_sha256=sha(manifest_raw), original_texts=list(original_texts.values()),
        tool=dict(name="SkelToJson", version="1.0.1", license="MIT", wheel_sha256=WHEEL_SHA256),
        tools=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in tools],
        client_sources=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in client_sources],
        counts=counts, outputs=outputs,
        operation="Decode every reachable original skeleton and its exact lazy-animation GUID table with complete binary consumption and original float32 values. Preserve all skins, bone/constraint membership, weighted meshes, clipping, deform, physics, paths, mixing, events and curve data; retain original atlas/material/renderer references by CAB and PathID. Reject absent GUIDs, ambiguous assets, mismatched names/string tables and conflicting embedded/split animations.",
        original_metadata="The two NPC ListNoSplitAnimationNames tables declare map/idle_01, while both actual main binaries contain only map/run_01. All 1205 actual split GUIDs are resolved. Preserve these absent declarations explicitly: the original GetAnimationAssetGuid reads ListAnimationContent and never consults ListNoSplitAnimationNames; do not synthesize an idle animation or replace a requested name.",
        limitations=["Source format conversion only. Native prefab restoration, independent native animation evaluation, actual geometry/rendering, follower movement, input/save integration and complete Map13 Player validation remain required."])))
    print("Original Map13 skeleton conversion:", counts, flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--graph", type=Path, default=GRAPH)
    parser.add_argument("--graph-receipt", type=Path, default=GRAPH_RECEIPT)
    args = parser.parse_args()
    build(args.graph.resolve(), args.graph_receipt.resolve())
