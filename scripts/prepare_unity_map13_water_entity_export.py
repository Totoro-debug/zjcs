"""Check native water-entity export and restore original reference registries.

Outputs stay in the conversion cache until all original runtime types have
been migrated and explicitly bound. This is not a playable prefab import.
"""
from collections import Counter
from pathlib import Path
import argparse
import json
import os
import re
import shutil
import socket
import struct
import subprocess
import time
import urllib.parse
import urllib.request

from official_apk import Apk, ROOT
from official_managed_reference_conversion import restore_exported_yaml
from official_native_yaml_verification import documents, element, float_bits, reference_shapes, unpack_array
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map13_water_entity_inputs import CACHE, ROOT_BUNDLES, OUTPUT, RECEIPT
from prepare_unity_map13_sources import encode, sha, source_path, write
from UnityPy.classes import PPtr


EXPORTED = CACHE / "export/ExportedProject/Assets"
DESTINATION = CACHE / "restored"
PROOF = ROOT / "assets/reference/official-map/map13-native-water-entity-export-source-manifest.json"
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")


def native_io_path(path):
    """Use Windows extended paths for I/O while preserving receipt identities."""
    path = Path(path).resolve()
    if os.name == "nt" and not str(path).startswith("\\\\?\\"):
        return Path("\\\\?\\" + str(path))
    return path


def convert(cache=CACHE):
    """Run a private hidden converter; never attach to another session's server."""
    runner = cache / "runner"
    runner.mkdir(parents=True, exist_ok=True)
    distribution = ROOT / ".tools/assetripper-1.3.14"
    for name in ("AssetRipper.GUI.Free.exe", "capstone.dll"):
        source, target = distribution / name, runner / name
        if not target.is_file() or sha(target.read_bytes()) != sha(source.read_bytes()):
            shutil.copyfile(source, target)
    settings = json.loads((distribution / "AssetRipper.Settings.json").read_bytes())
    settings["Export"]["SaveSettingsToDisk"] = False
    write(runner / "AssetRipper.Settings.json", encode(settings))
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        port = listener.getsockname()[1]
    process = subprocess.Popen([str(runner / "AssetRipper.GUI.Free.exe"), "--headless", "--port", str(port),
                                "--log-path", str(cache / "ripper.log")], cwd=runner,
                               creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    base = "http://127.0.0.1:" + str(port)
    try:
        deadline = time.monotonic() + 30
        while True:
            try:
                with urllib.request.urlopen(base + "/openapi.json", timeout=2) as response:
                    if response.status == 200:
                        break
            except OSError:
                if process.poll() is not None or time.monotonic() >= deadline:
                    raise RuntimeError("Private AssetRipper server did not start")
                time.sleep(0.25)
        for endpoint, folder in [("/LoadFolder", cache / "inputs"), ("/Export/UnityProject", cache / "export")]:
            print("Private native converter:", endpoint, flush=True)
            request = urllib.request.Request(base + endpoint,
                data=urllib.parse.urlencode(dict(path=str(folder))).encode(), method="POST")
            with urllib.request.urlopen(request, timeout=1200) as response:
                if response.status != 200:
                    raise RuntimeError("Native converter failed: " + endpoint)
    finally:
        process.terminate()
        process.wait(timeout=15)


def original_identity(obj):
    data = obj.read(check_read=False)
    kind = obj.type.name
    if kind == "GameObject":
        transform = next(pair.component.deref() for pair in data.m_Component
                         if pair.component.deref().type.name in ("Transform", "RectTransform"))
        value = transform.read()
        parent = value.m_Father
        prefix = original_identity(parent.deref().read().m_GameObject.deref())[1] + "/" if parent.m_PathID else ""
        return kind, prefix + data.m_Name
    if kind in ("Transform", "RectTransform"):
        return kind, original_identity(data.m_GameObject.deref())[1]
    if kind == "MonoBehaviour":
        kind = data.m_Script.read().m_ClassName
        if data.m_GameObject.m_PathID:
            return kind, original_identity(data.m_GameObject.deref())[1]
    return kind, getattr(data, "m_Name", "")


def build(receipt_path=RECEIPT, component_path=OUTPUT, roots=ROOT_BUNDLES,
          cache_root=CACHE, proof_path=PROOF, expected_counts=(22, 11), entry_path=None, map_class_id=13, subject="water entity"):
    exported = cache_root / "export/ExportedProject/Assets"
    destination = cache_root / "restored"
    receipt = json.loads(receipt_path.read_bytes())
    if receipt["manifest"] != "871_156598":
        raise ValueError("Wrong water-entity input version")
    for entry in receipt["tools"] + receipt["outputs"]:
        raw = (ROOT / entry["path"]).read_bytes()
        if entry.get("hashLineEndings") == "LF":
            raw = raw.replace(b"\r\n", b"\n")
        if sha(raw) != entry["sha256"]:
            raise ValueError("Water-entity input receipt is stale: " + entry["path"])
    for entry in receipt["inputs"]:
        if sha((cache_root / "inputs" / entry["output"]).read_bytes()) != entry["converter_input_sha256"]:
            raise ValueError("Water-entity converter input changed")
    apk = Apk.current()
    _, missing = collect(apk, roots)
    if missing:
        raise ValueError("Missing original water entity CAB dependencies")
    source = json.loads(component_path.read_bytes())
    explicit_identities = any("temporary_source_identity" in entry for entry in receipt["converted_objects"])
    objects = {(obj.assets_file.name, str(obj.path_id)): obj
               for bundle in list(apk.loaded.values()) for file in serialized_files(bundle)
               for obj in file.objects.values()}
    component_records = {(entry["cab"], entry["path_id"]): entry for entry in source["components"]}
    shapes = reference_shapes(receipt["managed_reference_unions"])

    def reference_signature(fields):
        registry = fields.get("references", {})
        if "RefIds" in registry:
            return tuple(entry["rid"] for entry in registry["RefIds"])
        return tuple(entry["rid"] for entry in registry.get("Map13OriginalOrder", []))

    def source_identity(obj):
        fields = component_records.get((obj.assets_file.name, str(obj.path_id)), {}).get("fields", {})
        if explicit_identities and "references" in fields:
            return original_identity(obj) + (obj.assets_file.name, str(obj.path_id))
        return original_identity(obj) + (reference_signature(fields),)

    originals = {}
    for record in source["components"]:
        if "references" not in record["fields"]:
            continue
        obj = objects[record["cab"], record["path_id"]]
        if sha(obj.get_raw_data()) != record["payload_sha256"]:
            raise ValueError("Original managed-reference payload changed")
        key = source_identity(obj)
        if key in originals:
            raise ValueError("Original water component identity is not unique: " + str(key))
        originals[key] = record, obj
    index = {}
    for path in exported.rglob("*.meta"):
        match = GUID.search(native_io_path(path).read_text(encoding="utf8"))
        if match:
            if match[1] in index:
                raise ValueError("Duplicate water export GUID")
            index[match[1]] = path.with_suffix("")
    cache = {}

    def read_documents(path):
        if path not in cache:
            cache[path] = documents(native_io_path(path).read_text(encoding="utf8"))
        return cache[path]

    def exported_identity(path, file_id):
        docs = read_documents(path)
        kind, data = docs[file_id]
        if kind == "GameObject":
            transform = next(pair["component"]["fileID"] for pair in data["m_Component"]
                             if docs[pair["component"]["fileID"]][0] in ("Transform", "RectTransform"))
            parent = docs[transform][1]["m_Father"]["fileID"]
            prefix = exported_identity(path, docs[parent][1]["m_GameObject"]["fileID"])[1] + "/" if parent else ""
            return kind, prefix + ("" if data["m_Name"] is None else str(data["m_Name"]))
        if kind in ("Transform", "RectTransform"):
            return kind, exported_identity(path, data["m_GameObject"]["fileID"])[1]
        if kind == "MonoBehaviour":
            script_ref = data["m_Script"]
            if str(script_ref["guid"]) == "0000000000000000e000000000000000":
                # AssetRipper preserves built-in MonoScripts (for example Tile)
                # as Unity default-resource pointers, without an exported .cs.
                builtin = apk.env.find_file("unity default resources")
                script = builtin.objects[script_ref["fileID"]]
                if script.type.name != "MonoScript":
                    raise ValueError("Built-in script pointer is not a MonoScript")
                kind = script.read().m_ClassName
            else:
                kind = index[str(script_ref["guid"])].stem
            if data["m_GameObject"]["fileID"]:
                return kind, exported_identity(path, data["m_GameObject"]["fileID"])[1]
        return kind, "" if data.get("m_Name") is None else str(data["m_Name"])

    def export_source_identity(path, file_id):
        fields = read_documents(path)[file_id][1]
        if explicit_identities and "Map13OriginalSourceCab" in fields:
            return exported_identity(path, file_id) + (str(fields["Map13OriginalSourceCab"]), str(fields["Map13OriginalSourcePathId"]))
        return exported_identity(path, file_id) + (reference_signature(fields),)

    counts = Counter()

    def compare(expected, actual, obj, path, key, shape):
        if isinstance(expected, dict):
            if set(expected) == {"m_FileID", "m_PathID"}:
                if not expected["m_PathID"]:
                    if actual != {"fileID": 0}:
                        raise ValueError("Null native pointer changed: " + key)
                else:
                    target = PPtr(**expected, assetsfile=obj.assets_file).deref()
                    export_path = index[str(actual["guid"])] if "guid" in actual else path
                    if source_identity(target) != export_source_identity(export_path, actual["fileID"]):
                        raise ValueError("Native pointer identity changed: " + key + " " + str((source_identity(target), export_source_identity(export_path, actual["fileID"]))))
                    counts["native_pointers"] += 1
                return
            if not isinstance(actual, dict) or set(expected) != set(actual):
                raise ValueError("Exported reference field names differ: " + key)
            for field in expected:
                compare(expected[field], actual[field], obj, path, key + "." + field, shape["children"][field])
        elif isinstance(expected, list):
            if not isinstance(actual, list):
                try:
                    actual = unpack_array(actual, shape, len(expected))
                except ValueError as error:
                    raise ValueError(str(error) + ": " + key + " value=" + repr(actual)) from error
                counts["packed_arrays"] += 1
            if not isinstance(actual, list) or len(expected) != len(actual):
                raise ValueError("Exported reference list shape differs: " + key)
            for number, (left, right) in enumerate(zip(expected, actual)):
                compare(left, right, obj, path, key + "[" + str(number) + "]", element(shape, left))
        elif isinstance(expected, float):
            if float_bits(expected, shape) != float_bits(actual, shape):
                raise ValueError("Exported reference float bits differ: " + key)
        elif isinstance(expected, str):
            if expected != ("" if actual is None else getattr(actual, "scalar", actual)):
                raise ValueError("Exported reference string differs: " + key)
        elif expected != ("" if actual is None and expected == "" else actual):
            raise ValueError("Exported reference scalar differs: " + key)
        counts["checked_values"] += 1

    outputs, checked = [], set()
    for path in sorted(path for path in exported.rglob("*") if path.suffix in (".prefab", ".asset")):
        raw = native_io_path(path).read_bytes()
        text = raw.decode("utf8")
        if "Map13OriginalOrder:" not in text:
            continue
        prepared, identity_fields = re.subn(r"(?m)^  Map13OriginalSource(?:Cab|PathId):[^\n]*\n", "", text)
        prepared, empty_registries = re.subn(
            r"(?m)^  references:\n    version: 2\n    Map13OriginalOrder: \[\]\n(?=---|  [^ ]|\Z)",
            "  references:\n    version: 2\n    RefIds: []\n", prepared)
        restored, orders = restore_exported_yaml(prepared)
        orders.extend([] for _ in range(empty_registries))
        counts["empty_registries"] += empty_registries
        if identity_fields != (len(orders) * 2 if explicit_identities else 0):
            raise ValueError("Not every temporary source identity was stripped")
        if identity_fields:
            counts["source_identities"] += identity_fields // 2
        restored_docs = documents(restored)
        for file_id, (kind, fields) in restored_docs.items():
            if kind != "MonoBehaviour" or "references" not in fields:
                continue
            key = export_source_identity(path, file_id)
            record, obj = originals[key]
            if key in checked:
                raise ValueError("Duplicate restored water-entity component")
            checked.add(key)
            shape = shapes[(record["cab"], obj.serialized_type.script_type_index)]
            for name, value in record["fields"].items():
                if name not in ("m_GameObject", "m_Script", "m_Enabled", "m_Name"):
                    compare(value, fields[name], obj, path, str(key) + "." + name, shape["children"][name])
            counts["registries"] += 1
            counts["reference_records"] += len(fields["references"]["RefIds"])
        if len(orders) != sum("references" in value for _, value in restored_docs.values()):
            raise ValueError("Not every exported registry was restored")
        target = destination / path.relative_to(exported)
        write(native_io_path(target), restored.encode())
        write(native_io_path(Path(str(target) + ".meta")), native_io_path(Path(str(path) + ".meta")).read_bytes())
        outputs.append(dict(input=source_path(path), input_sha256=sha(raw),
                            path=source_path(target), sha256=sha(native_io_path(target).read_bytes()),
                            registries=len(orders)))
    if checked != set(originals) or (expected_counts is not None and
            (counts["registries"], len(outputs)) != expected_counts):
        raise ValueError("Entity native export coverage differs: " + str(dict(counts)) +
                         " outputs=" + str(len(outputs)) + " originals=" + str(len(originals)) +
                         " unchecked=" + str(len(set(originals) - checked)))
    ripper = cache_root / "runner/AssetRipper.GUI.Free.exe"
    result = dict(format=1, manifest=apk.version, mapClassId=map_class_id,
        input_receipt=dict(path=source_path(receipt_path), sha256=sha(receipt_path.read_bytes())),
        tool=dict(path=source_path(ripper), sha256=sha(ripper.read_bytes()), version="1.3.14.0"),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        entry=dict(path=source_path(entry_path or Path(__file__)),
                   sha256=sha((entry_path or Path(__file__)).read_bytes())),
        verification_library=dict(path="scripts/official_native_yaml_verification.py",
                                  sha256=sha((ROOT / "scripts/official_native_yaml_verification.py").read_bytes())),
        counts=dict(counts), outputs=outputs,
        operation="AssetRipper 1.3.14 UnityProject export of the verified input closure; restore typed registry vectors to original ordered RefIds while retaining exported YAML record text, including empty registries. Strip and verify temporary source CAB/path IDs. Compare every original reference field/list/scalar, original float32/float64 bits, descriptor and RID; decode packed primitive arrays with their original type and width. Resolve every non-null pointer on both sides and compare native component type and complete original GameObject hierarchy (or named asset identity), with source IDs for registry components.",
        limitations=["Cache conversion only. Original runtime migration, explicit assembly/script binding, Windows shaders/import, Play Mode interaction and saves are not implemented by this step."])
    if map_class_id is None:
        result.pop("mapClassId")
        result["subject"] = subject
    write(proof_path, encode(result))
    print("Entity native export references verified:", dict(counts), "outputs:", len(outputs), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--convert", action="store_true", help="Run a private AssetRipper export before verification")
    if parser.parse_args().convert:
        convert()
    build()
