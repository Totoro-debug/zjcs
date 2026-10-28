"""Check converted original Map14 hierarchy and serialized script values.

Native particle/renderer payload verification, runtime script migration and live
performance remain separate requirements. This checks actual conversion output,
including original internal object identity and resolvable external pointers.
"""
from collections import Counter, OrderedDict
import json
from pathlib import Path
import re
from official_apk import ROOT
import UnityPy
from official_native_yaml_verification import documents, float_bits, unpack_array, element
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map14_story_playable_inputs import CACHE, SOURCE, RECEIPT, OUTPUT as COMPONENTS

EXPORTED = CACHE / "export/ExportedProject/Assets"
PROOF = ROOT / "assets/reference/official-map/map14-story-playable-hierarchy-source-manifest.json"
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")

def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()), bytes=path.stat().st_size)

def shape(node):
    return dict(type=node.m_Type, name=node.m_Name,
                children={child.m_Name: shape(child) for child in node.m_Children})

def same_float_bits(expected, actual, schema):
    # YAML gives "-0" an integer tag, although the original Unity field is a
    # float. Use its preserved lexical scalar with the original type tree;
    # converting the parsed integer 0 directly would lose its sign bit.
    actual = float(getattr(actual, "scalar", actual))
    return float_bits(expected, schema) == float_bits(actual, schema)

def build():
    source = json.loads(SOURCE.read_bytes())
    receipt = json.loads(RECEIPT.read_bytes())
    if source["mapClassId"] != 14 or receipt["mapClassId"] != 14 or source["manifest"] != "871_156598":
        raise ValueError("Wrong original Map14 performance selection")
    if sha(SOURCE.read_bytes()) != receipt["selection"]["source"]["sha256"]:
        raise ValueError("Original performance source changed after input preparation")
    pinned_outputs = {row["path"]: row["sha256"] for row in receipt["outputs"]}
    if sha(COMPONENTS.read_bytes()) != pinned_outputs.get(source_path(COMPONENTS)):
        raise ValueError("Original performance component records changed")
    objects = {entry["identity"]: entry for entry in source["objects"]}
    component_types = {(entry["cab"].casefold(), entry["path_id"]): entry["type"]
                       for entry in json.loads(COMPONENTS.read_bytes())["components"]}
    source_files = {entry["serialized_file"].casefold(): entry for entry in receipt["serialized_files"]}
    input_entries = {entry["name"]: entry for entry in receipt["inputs"]}
    native_cache = OrderedDict()
    def reader(cab, identity):
        key = cab.casefold()
        if key not in native_cache:
            entry = input_entries[source_files[key]["source"]]
            path = CACHE / "inputs" / entry["output"]
            raw = path.read_bytes()
            if sha(raw) != entry["converter_input_sha256"]:
                raise ValueError("Performance conversion input changed")
            env = UnityPy.load(raw)
            native_cache[key] = next(file for bundle in env.files.values() for file in serialized_files(bundle)
                                     if file.name.casefold() == key)
            if len(native_cache) > 12:
                native_cache.popitem(last=False)
        native_cache.move_to_end(key)
        return native_cache[key].objects[int(identity)]
    yaml_cache, used, index, bindings = {}, set(), {}, {}
    for path in EXPORTED.rglob("*.meta"):
        match = GUID.search(path.read_text(encoding="utf8"))
        if match:
            if match[1] in index:
                raise ValueError("Ambiguous native export GUID")
            index[match[1]] = path.with_suffix("")
    def parsed(path):
        used.add(path)
        if path not in yaml_cache:
            yaml_cache[path] = documents(path.read_text(encoding="utf8"))
        return yaml_cache[path]
    def script_type(fields):
        pointer = fields["m_Script"]
        path = index[pointer["guid"]]
        used.add(path); used.add(Path(str(path) + ".meta"))
        return path.stem
    def bind(node, path, file_id):
        docs = parsed(path)
        kind, fields = docs[file_id]
        expected = objects[node["object"]]
        if kind != "GameObject" or fields["m_Name"] != expected["fields"]["m_Name"]:
            raise ValueError("Original performance hierarchy node differs")
        if len(fields["m_Component"]) != len(node["components"]):
            raise ValueError("Original performance component count differs")
        bindings[node["object"]] = (path, file_id)
        transform = None
        for identity, component in zip(node["components"], fields["m_Component"]):
            target_id = component["component"]["fileID"]
            native_kind, native_fields = docs[target_id]
            source_kind = objects[identity]["type"]
            if (script_type(native_fields) if native_kind == "MonoBehaviour" else native_kind) != source_kind.rsplit(".", 1)[-1]:
                raise ValueError("Original performance component order/type differs: " + identity)
            bindings[identity] = (path, target_id)
            if source_kind in ("Transform", "RectTransform"):
                transform = native_fields
        children = transform["m_Children"]
        if len(children) != len(node["children"]):
            raise ValueError("Original performance child count differs")
        for child, pointer in zip(node["children"], children):
            child_go = docs[pointer["fileID"]][1]["m_GameObject"]["fileID"]
            bind(child, path, child_go)
    prefab_paths = []
    for prefab in source["prefabs"]:
        path = EXPORTED / prefab["path"].removeprefix("Assets/")
        root_name = objects[prefab["hierarchy"]["object"]]["fields"]["m_Name"]
        roots = [identity for identity, (kind, fields) in parsed(path).items()
                 if kind == "GameObject" and fields["m_Name"] == root_name
                 and parsed(path)[fields["m_Component"][0]["component"]["fileID"]][1]["m_Father"]["fileID"] == 0]
        if len(roots) != 1:
            raise ValueError("Ambiguous converted performance root")
        bind(prefab["hierarchy"], path, roots[0]); prefab_paths.append(path)
    counts = Counter()
    def compare(expected, actual, owner, path, schema, label):
        if isinstance(expected, dict) and set(expected) == {"m_FileID", "m_PathID"}:
            counts["pointers"] += 1
            if not expected["m_PathID"]:
                if actual.get("fileID", 0) != 0:
                    raise ValueError("Original null pointer changed: " + label)
                return
            cab = owner.split(":")[0]
            if expected["m_FileID"]:
                external = source_files[cab]["externals"][expected["m_FileID"] - 1]
                cab = external.rsplit("/", 1)[-1].casefold()
            identity = cab + ":" + str(expected["m_PathID"])
            destination = index[actual["guid"]] if actual.get("guid") else path
            if identity in bindings:
                if (destination, actual["fileID"]) != bindings[identity]:
                    raise ValueError("Original internal pointer identity changed: " + label)
            else:
                original = reader(cab, expected["m_PathID"])
                if destination.suffix in (".prefab", ".asset", ".mat", ".controller", ".anim", ".overrideController"):
                    kind, fields = parsed(destination)[actual["fileID"]]
                    original_name = original.peek_name()
                    original_kind = original.type.name
                    if original_kind == "MonoBehaviour":
                        original_kind = component_types[cab, str(expected["m_PathID"])].rsplit(".", 1)[-1]
                        kind = script_type(fields)
                    if kind != original_kind or (original_name and fields.get("m_Name") != original_name):
                        raise ValueError("Original external object identity differs: " + label)
                elif not destination.is_file() or actual["fileID"] == 0:
                    raise ValueError("Converted external performance reference is unresolved: " + label)
                used.add(destination); used.add(Path(str(destination) + ".meta"))
            return
        if isinstance(expected, dict):
            for key, value in expected.items():
                if key not in actual:
                    raise ValueError("Original field missing: " + label + "." + key)
                compare(value, actual[key], owner, path, schema["children"][key], label + "." + key)
        elif isinstance(expected, list):
            if not isinstance(actual, list):
                actual = unpack_array(actual, schema, len(expected))
            if len(expected) != len(actual):
                raise ValueError("Original performance array count differs: " + label)
            for i, (left, right) in enumerate(zip(expected, actual)):
                compare(left, right, owner, path, element(schema), label + "[" + str(i) + "]")
        elif isinstance(expected, float):
            if not same_float_bits(expected, actual, schema):
                raise ValueError("Original performance float bits differ: " + label + " source=" + repr(expected) + " converted=" + repr(actual) + " owner=" + owner)
        elif expected != ("" if actual is None and expected == "" else actual):
            raise ValueError("Original performance value differs: " + label)
        counts["values"] += 1
    for identity, (path, file_id) in bindings.items():
        original = objects[identity]
        native_kind, actual = parsed(path)[file_id]
        if original["type"] == "GameObject":
            for field in ("m_Name", "m_Layer", "m_IsActive"):
                if original["fields"][field] != actual[field]:
                    raise ValueError("Original GameObject state differs")
            if original["fields"]["m_Tag"] != 0 or actual["m_TagString"] != "Untagged":
                raise ValueError("Inspect original non-default performance tag")
            counts["gameObjects"] += 1
            continue
        if native_kind not in ("Transform", "RectTransform", "MonoBehaviour"):
            continue
        cab, path_id = identity.split(":")
        obj = reader(cab, path_id)
        tree = shape(obj.serialized_type.node)
        for field, value in original["fields"].items():
            if field == "m_Script":
                continue
            compare(value, actual[field], identity, path, tree["children"][field], original["type"] + "." + field)
        counts["transforms" if native_kind in ("Transform", "RectTransform") else "scriptComponents"] += 1
    used.update(Path(str(path) + ".meta") for path in prefab_paths)
    if counts["gameObjects"] != 621 or counts["transforms"] != 621 or len(prefab_paths) != 16:
        raise ValueError("Original performance hierarchy coverage differs")
    result = dict(manifest="871_156598", mapClassId=14,
        inputs=[record(SOURCE), record(RECEIPT), record(COMPONENTS)], tools=[record(Path(__file__)), record(ROOT / "scripts/official_native_yaml_verification.py")],
        counts=dict(counts, prefabs=len(prefab_paths)), outputs=[record(path) for path in sorted(used)],
        bindings=[dict(source=identity, path=source_path(path), fileId=int(file_id), type=objects[identity]["type"])
                  for identity, (path, file_id) in bindings.items()],
        operation="Bind original GameObject and component IDs by exact hierarchy and component/child order; verify original activation, layers, tags, transforms and every serialized MonoBehaviour field, float bits, array order, internal identity and resolved external target identity. Preserve original bytes and cache conversion output.",
        limitations=["Native particle/renderer field validation, Timeline external dependency restoration, original runtime script/appearance bindings, Windows shader import, live playback and formal map integration remain required."])
    write(PROOF, encode(result)); print("Original Map14 performance hierarchy:", dict(counts), flush=True)
    return result

if __name__ == "__main__":
    build()
