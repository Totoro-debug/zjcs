"""Verify all original entity Timeline fields and reference identities after export."""
from collections import Counter
import json
from pathlib import Path
import re
import UnityPy

from official_apk import ROOT
from official_native_yaml_verification import documents, element, unpack_array
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map14_entity_timeline_inputs import CACHE, COMPONENTS, RECEIPT, SOURCE
from verify_unity_map14_story_playable_hierarchy import shape, same_float_bits

EXPORTED = CACHE / "export/ExportedProject/Assets"
PROOF = ROOT / "assets/reference/official-map/map14-entity-timeline-native-fields-source-manifest.json"
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def build():
    receipt = json.loads(RECEIPT.read_bytes())
    if receipt["manifest"] != "871_156598" or receipt["mapClassId"] != 14:
        raise ValueError("Wrong original entity Timeline selection")
    for entry in [receipt["source"], *receipt["outputs"]]:
        if sha((ROOT / entry["path"]).read_bytes()) != entry["sha256"]:
            raise ValueError("Original Timeline input changed")
    source = json.loads(SOURCE.read_bytes())
    types = {(r["cab"].casefold(), int(r["path_id"])): r["type"]
             for r in json.loads(COMPONENTS.read_bytes())["components"]}
    files = {r["serialized_file"].casefold(): r for r in receipt["serialized_files"]}
    inputs = {r["name"]: r for r in receipt["inputs"]}
    loaded, parsed, used, index = {}, {}, set(), {}
    for path in EXPORTED.rglob("*.meta"):
        match = GUID.search(path.read_text(encoding="utf8"))
        if match:
            if match[1] in index:
                raise ValueError("Duplicate native export GUID")
            index[match[1]] = path.with_suffix("")

    def reader(identity):
        cab, path_id = identity
        if cab not in loaded:
            entry = inputs[files[cab]["source"]]
            path = CACHE / "inputs" / entry["output"]
            raw = path.read_bytes()
            if sha(raw) != entry["converter_input_sha256"]:
                raise ValueError("Original Timeline converter input changed")
            env = UnityPy.load(raw)
            loaded[cab] = next(f for b in env.files.values() for f in serialized_files(b)
                               if f.name.casefold() == cab)
        return loaded[cab].objects[path_id]

    def docs(path):
        used.update((path, Path(str(path) + ".meta")))
        if path not in parsed:
            parsed[path] = documents(path.read_text(encoding="utf8"))
        return parsed[path]

    def script_type(fields):
        path = index[fields["m_Script"]["guid"]]
        used.update((path, Path(str(path) + ".meta")))
        return path.stem

    counts, bindings, reverse, pending = Counter(), {}, {}, []

    def bind(identity, path, file_id):
        native = path, file_id
        if identity in bindings:
            if bindings[identity] != native:
                raise ValueError("Original asset resolves to different exported objects: " + repr(identity))
            return
        if native in reverse and reverse[native] != identity:
            raise ValueError("Distinct original objects collapsed during export")
        obj = reader(identity)
        kind, fields = docs(path)[file_id]
        expected_type = types[identity].rsplit(".", 1)[-1] if obj.type.name == "MonoBehaviour" else obj.type.name
        actual_type = script_type(fields) if kind == "MonoBehaviour" else kind
        if actual_type != expected_type or (obj.peek_name() and obj.peek_name() != fields.get("m_Name")):
            raise ValueError("Original dependency type/name differs: " + repr(identity))
        bindings[identity] = native
        reverse[native] = identity
        if kind == "MonoBehaviour":
            pending.append(identity)
        counts["boundObjects"] += 1

    def compare(left, right, schema, owner, path, label):
        if isinstance(left, dict) and set(left) == {"m_FileID", "m_PathID"}:
            counts["pointers"] += 1
            if not left["m_PathID"]:
                if right["fileID"] != 0:
                    raise ValueError("Original null pointer changed: " + label)
                return
            cab = owner[0]
            if left["m_FileID"]:
                cab = files[cab]["externals"][left["m_FileID"] - 1].replace("\\", "/").rsplit("/", 1)[-1].casefold()
            target = index[right["guid"]] if right.get("guid") else path
            bind((cab, left["m_PathID"]), target, right["fileID"])
            return
        if isinstance(left, dict):
            for key, value in left.items():
                if key not in right:
                    raise ValueError("Original Timeline field missing: " + label + "." + key)
                compare(value, right[key], schema["children"][key], owner, path, label + "." + key)
        elif isinstance(left, (list, tuple)):
            if not isinstance(right, list):
                right = unpack_array(right, schema, len(left))
            if len(left) != len(right):
                raise ValueError("Original Timeline array count differs: " + label)
            for i, (a, b) in enumerate(zip(left, right)):
                compare(a, b, element(schema), owner, path, label + "[" + str(i) + "]")
        elif isinstance(left, float):
            if not same_float_bits(left, right, schema):
                raise ValueError("Original Timeline float bits differ: " + label)
        elif isinstance(left, str) and schema["type"] in ("string", "PropertyName"):
            if left != ("" if right is None else str(right)):
                raise ValueError("Original Timeline string differs: " + label)
        elif left != ("" if right is None and left == "" else right):
            raise ValueError("Original Timeline value differs: " + label + " " + repr((left, right)))
        counts["values"] += 1

    roots = []
    for timeline in source["timelines"]:
        asset = timeline["asset"]
        name = asset["fields"]["m_Name"]
        paths = list(EXPORTED.rglob(name + ".playable"))
        if len(paths) != 1:
            raise ValueError("Original Timeline export root is ambiguous: " + name)
        path = paths[0]
        candidates = [i for i, (kind, fields) in docs(path).items()
                      if kind == "MonoBehaviour" and fields.get("m_Name") == name
                      and script_type(fields) == "TimelineAsset"]
        if len(candidates) != 1:
            raise ValueError("Original Timeline root identity differs")
        identity = asset["cab"].casefold(), int(asset["path_id"])
        bind(identity, path, candidates[0])
        roots.append(dict(name=name, source=identity, path=source_path(path), fileId=candidates[0]))
    checked = set()
    while pending:
        identity = pending.pop()
        if identity in checked:
            continue
        checked.add(identity)
        obj = reader(identity)
        path, file_id = bindings[identity]
        fields = docs(path)[file_id][1]
        tree = shape(obj.serialized_type.node)
        if "references" in obj.read_typetree(check_read=True):
            raise ValueError("Timeline reference registry requires restored export validation")
        for key, value in obj.read_typetree(check_read=True).items():
            if key == "m_Script":
                continue
            compare(value, fields[key], tree["children"][key], identity, path, types[identity] + "." + key)
        counts["scriptObjects"] += 1
    expected = {(a["cab"].casefold(), int(a["path_id"]))
                for t in source["timelines"] for a in [t["asset"], *t["objects"]]}
    if not expected.issubset(checked):
        raise ValueError("Original Timeline track/clip coverage is incomplete")
    result = dict(manifest="871_156598", mapClassId=14, roots=roots,
        inputs=[record(SOURCE), record(RECEIPT), record(COMPONENTS)],
        tools=[record(Path(__file__)), record(ROOT / "scripts/official_native_yaml_verification.py"),
               record(ROOT / "scripts/verify_unity_map14_story_playable_hierarchy.py")],
        counts=dict(counts, timelines=len(roots), originalTrackClipObjects=len(expected)),
        bindings=[dict(cab=cab, path_id=pid, path=source_path(path), fileId=file_id,
                       type=types.get((cab, pid), reader((cab, pid)).type.name))
                  for (cab, pid), (path, file_id) in sorted(bindings.items())],
        outputs=[record(p) for p in sorted(used)],
        operation="Traverse all five original Timeline roots and every original MonoBehaviour pointer. Compare all fields, track/clip order, original float widths and bits, and a bijection of original CAB/PathID to exported file/local IDs. Native AnimationClip dependencies are identified and hashed; their curve playback is not certified by this check. Exported script stubs are inspected only for asset type identity and are never imported as game runtime.",
        limitations=["Cache verification only. Script/actor/material bindings, native curve playback, split audio playback and formal map integration remain required."])
    write(PROOF, encode(result))
    print("Original entity Timeline native fields:", result["counts"], flush=True)
    return result


if __name__ == "__main__":
    build()
