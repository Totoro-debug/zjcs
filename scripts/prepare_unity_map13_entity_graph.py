"""Follow native object references from the actual Map13 entity prefab roots.

Bundle inventories include other maps and character appearances. Keep that
verified inventory intact; derive the object closure to drive explicit native
bindings, including the original on-demand appearance texture references.
"""
from collections import Counter, OrderedDict, deque
import gc
import importlib.metadata
import json
from pathlib import Path

from prepare_unity_map13_entity_inputs import CACHE, OUTPUT as COMPONENTS, RECEIPT as INPUT_RECEIPT
from prepare_unity_map13_entity_materials import pointers
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from official_apk import ROOT
import UnityPy


OUTPUT = ROOT / "unity/Assets/Official/World/Map/13/Source/native-entity-object-graph.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-entity-object-graph-source-manifest.json"


def identity(cab, path_id):
    return cab.casefold(), str(path_id)


def files(environment, entry):
    for file in (child for bundle in environment.files.values() for child in serialized_files(bundle)):
        yield ("unity default resources" if entry["output"] == "unity default resources" else file.name), file


class Inputs:
    def __init__(self, entries):
        self.entries = entries
        self.cache = OrderedDict()

    def load(self, index):
        if index not in self.cache:
            entry = self.entries[index]
            raw = (CACHE / "inputs" / entry["output"]).read_bytes()
            if sha(raw) != entry["converter_input_sha256"]:
                raise ValueError("Native entity input changed: " + entry["output"])
            self.cache[index] = dict(files(UnityPy.load(raw), entry))
            if len(self.cache) > 12:
                self.cache.popitem(last=False)
        self.cache.move_to_end(index)
        return self.cache[index]

    def reader(self, info):
        return self.load(info["input_index"])[info["cab"]].objects[int(info["path_id"])]


def build():
    receipt = json.loads(INPUT_RECEIPT.read_bytes())
    if receipt["manifest"] != "871_156598" or receipt["mapClassId"] != 13:
        raise ValueError("Wrong Map13 source selection")
    for output in receipt["outputs"]:
        if sha((ROOT / output["path"]).read_bytes()) != output["sha256"]:
            raise ValueError("Native component source changed: " + output["path"])
    components = {identity(c["cab"], c["path_id"]): c
                  for c in json.loads(COMPONENTS.read_bytes())["components"]}
    inputs = Inputs(receipt["inputs"])
    inventory, bundle_keys, externals = {}, {}, {}
    for index, entry in enumerate(receipt["inputs"]):
        keys = bundle_keys.setdefault(entry["name"], [])
        for cab, file in inputs.load(index).items():
            externals[cab] = [external.path for external in file.externals]
            for obj in file.objects.values():
                key = identity(cab, obj.path_id)
                if key in inventory:
                    raise ValueError("Duplicated native object identity: " + str(key))
                if obj.type.name == "MonoBehaviour" and key not in components and entry["output"] != "unity default resources":
                    raise ValueError("Original managed fields missing: " + str(key))
                inventory[key] = dict(cab=cab, path_id=str(obj.path_id), type=obj.type.name,
                    input_index=index)
                keys.append(key)
        if (index + 1) % 128 == 0:
            print("Indexed original entity input", index + 1, flush=True)
            gc.collect()
    if len(inventory) != receipt["counts"]["checked_objects"]:
        raise ValueError("Original native object inventory changed")
    roots = []
    pending = deque()
    queued = set()

    def enqueue(key):
        if key not in inventory:
            raise ValueError("Native object reference is unresolved: " + str(key))
        if key not in queued:
            queued.add(key)
            pending.append(key)

    for prefab in receipt["selection"]["prefabs"]:
        source = prefab["source"]
        candidates = [key for key in bundle_keys[source["bundle"]]
                      if key[1] == source["path_id"] and inventory[key]["type"] == "GameObject"]
        if len(candidates) != 1:
            raise ValueError("Original prefab root is ambiguous: " + prefab["path"])
        key = candidates[0]
        if sha(inputs.reader(inventory[key]).get_raw_data()) != prefab["object_sha256"]:
            raise ValueError("Original prefab root payload changed: " + prefab["path"])
        roots.append(dict(path=prefab["path"], cab=inventory[key]["cab"], path_id=key[1], uses=prefab["uses"]))
        enqueue(key)
    dynamic = {}
    for binding in receipt["selection"]["dynamic_textures"]["bindings"]:
        source = binding["source"]
        targets = [key for key in bundle_keys[source["bundle"]]
                   if key[1] == source["path_id"] and inventory[key]["type"] == "Texture2D"]
        if len(targets) != 1:
            raise ValueError("Original on-demand texture target is ambiguous")
        dynamic.setdefault(identity(binding["cab"], binding["path_id"]), []).append((binding, targets[0]))
    objects, dynamic_count, native_schemas = [], 0, {}
    while pending:
        key = pending.popleft()
        info = inventory[key]
        obj = inputs.reader(info)
        raw = obj.get_raw_data()
        if key in components:
            component = components[key]
            fields = component["fields"]
        else:
            # Engine resource files omit embedded trees. UnityPy selects the
            # native class schema for this file's actual Unity version; this
            # must succeed before an object can be classified as pointer-free.
            node = obj._get_typetree_node()
            tree_hash = sha(node.dump_structure().encode())
            schema_key = (info["type"], str(obj.version), tree_hash)
            native_schemas.setdefault(schema_key, dict(type=info["type"], unity_version=str(obj.version),
                schema_sha256=tree_hash, embedded=obj.serialized_type.node is not None))
            # Reading a texture's pixel payload is unnecessary for reference
            # discovery. Only a complete native type tree can prove no PPtr.
            fields = obj.read_typetree(check_read=True) if any(
                child.m_Type.startswith("PPtr<") for child in node.traverse()) else {}
        edges = list(pointers(fields, info["cab"], externals[info["cab"]]))
        record = dict(cab=info["cab"], path_id=info["path_id"], type=info["type"],
            input=receipt["inputs"][info["input_index"]]["output"], payload_sha256=sha(raw), references=edges)
        if "m_Name" in fields:
            record["name"] = fields["m_Name"]
        elif info["type"] not in ("MonoBehaviour", "Shader"):
            name = obj.peek_name()
            if name:
                record["name"] = name
        if info["type"] == "Shader":
            form = fields["m_ParsedForm"]
            record.update(name=form["m_Name"], fallback=form["m_FallbackName"])
        if key in components:
            record.update(assembly=component["assembly"], managed_type=component["type"],
                          original_payload_sha256=component["payload_sha256"])
        for edge in edges:
            enqueue(identity(edge["cab"], edge["path_id"]))
        if key in dynamic:
            record["dynamic_textures"] = []
            for binding, target in dynamic[key]:
                if sha(inputs.reader(inventory[target]).get_raw_data()) != binding["payload_sha256"]:
                    raise ValueError("Original on-demand texture payload changed")
                record["dynamic_textures"].append(dict(mapping=binding["mapping"], role=binding["role"],
                    asset=binding["asset"], cab=inventory[target]["cab"], path_id=target[1]))
                enqueue(target)
                dynamic_count += 1
        objects.append(record)
        if len(objects) % 2048 == 0:
            print("Followed original native objects", len(objects), "pending", len(pending), flush=True)
            gc.collect()
    objects.sort(key=lambda o: (o["cab"].casefold(), int(o["path_id"])))
    counts = dict(roots=len(roots), inventory_objects=len(inventory), reachable_objects=len(objects),
        native_pointers=sum(len(o["references"]) for o in objects), dynamic_texture_bindings=dynamic_count,
        reachable_types=dict(sorted(Counter(o["type"] for o in objects).items())),
        reachable_managed_types=dict(sorted(Counter(o["managed_type"] for o in objects
                                                    if "managed_type" in o).items())))
    write(OUTPUT, encode(dict(manifest=receipt["manifest"], mapClassId=13, roots=roots, objects=objects)))
    meta(OUTPUT)
    tools = [Path(__file__), ROOT / "scripts/prepare_unity_map13_entity_materials.py",
             ROOT / "scripts/prepare_unity_bundle_inputs.py"]
    write(RECEIPT, encode(dict(format=1, manifest=receipt["manifest"], mapClassId=13,
        input_receipt=dict(path=source_path(INPUT_RECEIPT), sha256=sha(INPUT_RECEIPT.read_bytes())),
        component_source=dict(path=source_path(COMPONENTS), sha256=sha(COMPONENTS.read_bytes())),
        inputs=[dict(path=source_path(CACHE / "inputs" / e["output"]), sha256=e["converter_input_sha256"])
                for e in receipt["inputs"]],
        counts=counts, native_schemas=list(native_schemas.values()),
        libraries=[dict(package="UnityPy", version=importlib.metadata.version("UnityPy"),
            schema_database=dict(path=source_path(Path(UnityPy.__file__).parent / "resources/lzma.tpk"),
                sha256=sha((Path(UnityPy.__file__).parent / "resources/lzma.tpk").read_bytes())))],
        tools=[dict(path=source_path(t), sha256=sha(t.read_bytes())) for t in tools],
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(OUTPUT.read_bytes()))],
        operation="Follow every nonzero native PPtr from the 335 byte-verified prefab roots, resolving CAB and object ID across the complete verified bundle inventory. Use original managed fields, traverse serialized map pairs as well as arrays, and add original on-demand appearance texture GUID edges only for reachable loader components. Reject unresolved references and ambiguous targets; retain cycles without repeated traversal.",
        limitations=["Object-reference closure for entity prefab roots, not runtime code analysis. String-based scene/UI/audio and dynamically instantiated gameplay content require their own official loading paths and bindings. Shader fallback names require separate source-name bindings. Windows asset import, runnable entities, input, saves and full-map validation remain required."])))
    print("Original entity object graph:", json.dumps(counts, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    build()
