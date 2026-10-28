"""Restore and verify the exact two original Map12 stele reference registries.

The selected export stays in the owning validation directory. No runtime assets
or Unity processes are produced by this intermediate conversion step.
"""
import argparse
import json
from pathlib import Path
import re
import shutil
from collections import Counter

from official_apk import Apk, ROOT
from UnityPy.classes import PPtr
from UnityPy.helpers.Tpk import get_typetree_node
from official_native_yaml_verification import documents, element, float_bits, unpack_array
from prepare_unity_map12_stele_entity_inputs import BUNDLE, OUTPUT, RECEIPT
from prepare_unity_map12_stele_playables import shape
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_map13_water_entity_export import build as verify, convert
from validation_workspace import validation_directory

PROOF = ROOT / "assets/reference/official-map/map12-stele-entity-export-source-manifest.json"
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")
REFERENCE = re.compile(r"guid: ([a-f0-9]{32})")


def verify_hierarchies(cache, index):
    source = json.loads(OUTPUT.read_bytes())
    apk = Apk.current()
    originals = {(obj.assets_file.name.casefold(), str(obj.path_id)): obj for obj in apk.objects(BUNDLE)}
    records = {(entry["cab"].casefold(), entry["path_id"]): entry for entry in source["objects"]}
    native, bindings, counts, defaults, zero_signs = {}, {}, Counter(), [], []

    def docs(path):
        if path not in native:
            native[path] = documents(path.read_text(encoding="utf8"))
        return native[path]

    def identity(obj):
        return obj.assets_file.name.casefold(), str(obj.path_id)

    def bind(obj, path, file_id):
        kind, fields = docs(path)[file_id]
        original = obj.read(check_read=True)
        if kind != "GameObject" or fields["m_Name"] != original.m_Name:
            raise ValueError("Original stele hierarchy node differs")
        bindings[identity(obj)] = path, file_id
        if len(original.m_Component) != len(fields["m_Component"]):
            raise ValueError("Original stele component count differs")
        for entry, pointer in zip(original.m_Component, fields["m_Component"]):
            component = entry.component.deref()
            actual_id = pointer["component"]["fileID"]
            if docs(path)[actual_id][0] != component.type.name:
                raise ValueError("Original stele component order differs")
            bindings[identity(component)] = path, actual_id
            if component.type.name == "Transform":
                transform, native_transform = component.read(), docs(path)[actual_id][1]
        if len(transform.m_Children) != len(native_transform["m_Children"]):
            raise ValueError("Original stele child count differs")
        for child, pointer in zip(transform.m_Children, native_transform["m_Children"]):
            child_go = docs(path)[pointer["fileID"]][1]["m_GameObject"]["fileID"]
            bind(child.read().m_GameObject.deref(), path, child_go)

    for prefab in source["prefabs"]:
        paths = list((cache / "restored").rglob(prefab["name"] + ".prefab"))
        if len(paths) != 1:
            raise ValueError("Restored original stele hierarchy is ambiguous")
        path = paths[0]
        root_id, = [file_id for file_id, (kind, fields) in docs(path).items()
                    if kind == "GameObject" and fields["m_Name"] == prefab["name"]]
        bind(originals[(prefab["cab"].casefold(), prefab["path_id"])], path, root_id)
    if set(bindings) != set(records):
        raise ValueError("Original stele hierarchy object coverage differs")

    def compare(expected, actual, owner, path, schema, label, field_keys):
        if isinstance(expected, dict) and set(expected) == {"m_FileID", "m_PathID"}:
            if not expected["m_PathID"]:
                if actual != dict(fileID=0):
                    raise ValueError("Original stele null pointer differs: " + label)
            else:
                target = PPtr(**expected, assetsfile=owner.assets_file).deref()
                target_path = index[actual["guid"]] if actual.get("guid") else path
                if identity(target) in bindings:
                    if (target_path, actual["fileID"]) != bindings[identity(target)]:
                        raise ValueError("Original stele hierarchy pointer differs: " + label)
                else:
                    kind, fields = docs(target_path)[actual["fileID"]]
                    if kind != target.type.name or fields.get("m_Name") != target.peek_name():
                        raise ValueError("Original stele image/material pointer differs: " + label)
            counts["pointers"] += 1
        elif isinstance(expected, dict):
            if not isinstance(actual, dict) or set(actual) != set(expected):
                raise ValueError("Original stele field structure differs: " + label)
            for field, value in expected.items():
                compare(value, actual[field], owner, path, schema["children"][field], label + "." + field, field_keys + [field])
        elif isinstance(expected, list):
            if not isinstance(actual, list):
                actual = unpack_array(actual, schema, len(expected))
            if len(actual) != len(expected):
                raise ValueError("Original stele array length differs: " + label)
            for number, (left, right) in enumerate(zip(expected, actual)):
                compare(left, right, owner, path, element(schema, left), label + "[" + str(number) + "]", field_keys + [number])
        elif isinstance(expected, float):
            # Unity reads -0 as a floating-point field. YAML's generic integer
            # parser loses its sign, so use the retained lexical scalar here.
            actual_float = float(getattr(actual, "scalar", actual))
            if float_bits(expected, schema) != float_bits(actual_float, schema):
                raise ValueError("Original stele float bits differ: " + label)
            if actual == 0 and getattr(actual, "scalar", "") == "-0":
                zero_signs.append(dict(cab=owner.assets_file.name, path_id=str(owner.path_id),
                    field=".".join(field_keys), lexical_scalar=actual.scalar,
                    original_float_bits=int.from_bytes(float_bits(expected, schema), "little")))
            counts["values"] += 1
        elif expected != actual:
            raise ValueError("Original stele scalar differs: " + label)
        else:
            counts["values"] += 1

    for identity_key, entry in records.items():
        if entry["type"] == "MonoBehaviour":
            continue  # Every registry field was checked by the shared verifier.
        obj = originals[identity_key]
        if sha(obj.get_raw_data()) != entry["payload_sha256"]:
            raise ValueError("Original stele native object bytes differ")
        path, file_id = bindings[identity_key]
        actual = docs(path)[file_id][1]
        schema = shape(obj.serialized_type.node or get_typetree_node(obj.type.value, obj.version))
        for field, value in entry["fields"].items():
            if field == "m_Tag":
                if value != 0 or actual["m_TagString"] != "Untagged":
                    raise ValueError("Original stele runtime tag differs")
                counts["runtime_tag_to_editor_name"] += 1
            elif field not in actual:
                required = {"m_LightmapIndex": 65535, "m_LightmapIndexDynamic": 65535,
                    "m_LightmapTilingOffset": dict(x=1.0, y=1.0, z=0.0, w=0.0),
                    "m_LightmapTilingOffsetDynamic": dict(x=1.0, y=1.0, z=0.0, w=0.0)}
                if entry["type"] != "SpriteRenderer" or field not in required or value != required[field]:
                    raise ValueError("Original stele native field is missing: " + field)
                defaults.append(dict(cab=entry["cab"], path_id=entry["path_id"], field=field, original=value))
            else:
                compare(value, actual[field], obj, path, schema["children"][field], entry["hierarchy"] + "." + field, [field])
        counts[entry["type"]] += 1
    return dict(counts=dict(counts), original_runtime_lightmap_defaults=defaults,
        retained_lexical_negative_zeros=zero_signs,
        operation="Compare every original GameObject, Transform and SpriteRenderer field and float bit, component/child order and local hierarchy pointer. Map original runtime tag 0 only to Unity editor Untagged. Guard and record the original unbaked lightmap defaults omitted from editor YAML. Original registry fields and PPtrs are independently verified by the reference-registry checker. Sprite/material external pointers retain original source identities in the source projection; subsequent Windows bindings must verify those exact objects, not names alone.")


def build(stage_dir, run_converter=False):
    stage_dir = stage_dir.resolve()
    validation = validation_directory()
    if stage_dir == validation or not stage_dir.is_relative_to(validation):
        raise ValueError("Use the owning validation session for original conversion")
    guarded_paths = [Path(__file__).resolve(), ROOT / "scripts/prepare_unity_map13_water_entity_export.py",
        ROOT / "scripts/official_managed_reference_conversion.py", ROOT / "scripts/official_native_yaml_verification.py",
        ROOT / "scripts/prepare_unity_map12_stele_playables.py"]
    guarded = {path: sha(path.read_bytes()) for path in guarded_paths}
    receipt = json.loads(RECEIPT.read_bytes())
    for entry in receipt["tools"] + receipt["outputs"] + receipt["selection"]:
        if sha((ROOT / entry["path"]).read_bytes()) != entry["sha256"]:
            raise ValueError("Original stele entity input receipt is stale: " + entry["path"])
    if run_converter:
        convert(stage_dir)
    exported = stage_dir / "export/ExportedProject/Assets"
    index = {}
    for path in exported.rglob("*.meta"):
        match = GUID.search(path.read_text(encoding="utf8"))
        if match:
            if match[1] in index:
                raise ValueError("Ambiguous original stele export GUID")
            index[match[1]] = path.with_suffix("")
    pending = []
    for prefab in receipt["prefabs"]:
        paths = list(exported.rglob(prefab["name"] + ".prefab"))
        if len(paths) != 1:
            raise ValueError("Original stele native root is absent or ambiguous")
        pending.extend(paths)
    selected = set()
    while pending:
        path = pending.pop()
        if path in selected:
            continue
        selected.add(path)
        if path.suffix in (".prefab", ".asset", ".mat", ".controller", ".anim", ".overrideController"):
            for guid in REFERENCE.findall(path.read_text(encoding="utf8")):
                if guid in index:
                    pending.append(index[guid])
                elif guid not in ("0000000000000000e000000000000000", "0000000000000000f000000000000000", "00000000000000000000000000000000"):
                    raise ValueError("Original stele export dependency is unresolved: " + guid)
    cache = stage_dir / "selected-stele-entities"
    if cache.exists():
        # Never recursively remove a validation directory or unrelated export.
        # Repeated runs must reproduce the exact selected file set instead.
        previous = {path.relative_to(cache / "export/ExportedProject/Assets")
                    for path in (cache / "export/ExportedProject/Assets").rglob("*") if path.is_file()}
        expected = {path.relative_to(exported) for path in selected}
        expected |= {Path(str(path) + ".meta") for path in expected}
        if previous != expected:
            raise ValueError("Selected conversion closure changed; use a new validation session")
    for path in selected:
        target = cache / "export/ExportedProject/Assets" / path.relative_to(exported)
        write(target, path.read_bytes())
        write(Path(str(target) + ".meta"), Path(str(path) + ".meta").read_bytes())
    for entry in receipt["inputs"]:
        source = stage_dir / "inputs" / entry["output"]
        if sha(source.read_bytes()) != entry["converter_input_sha256"]:
            raise ValueError("Original stele entity converter payload changed")
        target = cache / "inputs" / entry["output"]
        target.parent.mkdir(parents=True, exist_ok=True)
        if not target.exists():
            target.hardlink_to(source)
        if sha(target.read_bytes()) != entry["converter_input_sha256"]:
            raise ValueError("Selected stele conversion input differs")
    runner = cache / "runner/AssetRipper.GUI.Free.exe"
    runner.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(stage_dir / "runner/AssetRipper.GUI.Free.exe", runner)
    verify(receipt_path=RECEIPT, component_path=OUTPUT, roots=[BUNDLE], cache_root=cache,
        proof_path=PROOF, expected_counts=(2, 2), entry_path=Path(__file__).resolve(),
        map_class_id=12, subject="stele entity")
    hierarchy = verify_hierarchies(cache, index)
    for path, expected in guarded.items():
        if sha(path.read_bytes()) != expected:
            raise ValueError("Stele export verification code changed during execution")
    proof = json.loads(PROOF.read_bytes())
    proof["hierarchy_verification"] = hierarchy
    for output in proof["outputs"]:
        output["sha256"] = sha((ROOT / output["path"]).read_bytes())
    proof["hierarchy_verification_tools"] = [dict(path=path.relative_to(ROOT).as_posix(), sha256=value)
        for path, value in guarded.items()]
    write(PROOF, encode(proof))
    print("Original stele entity selected closure:", len(selected), "assets", flush=True)
    print("Original stele hierarchy fields:", hierarchy["counts"], flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stage-dir", type=Path, required=True)
    parser.add_argument("--convert", action="store_true")
    args = parser.parse_args()
    build(args.stage_dir, args.convert)
