"""Check native water-entity export and restore original reference registries.

Outputs stay in the conversion cache until all original runtime types have
been migrated and explicitly bound. This is not a playable prefab import.
"""
from collections import Counter
from pathlib import Path
import argparse
import json
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
from prepare_unity_bundle_inputs import collect
from prepare_unity_map13_water_entity_inputs import CACHE, ROOT_BUNDLES, OUTPUT, RECEIPT
from prepare_unity_map13_sources import encode, sha, source_path, write
from UnityPy.classes import PPtr
import yaml


EXPORTED = CACHE / "export/ExportedProject/Assets"
DESTINATION = CACHE / "restored"
PROOF = ROOT / "assets/reference/official-map/map13-native-water-entity-export-source-manifest.json"
HEADER = re.compile(r"(?m)^--- !u!\d+ &(-?\d+)\n")
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")


def convert():
    """Run a private hidden converter; never attach to another session's server."""
    runner = CACHE / "runner"
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
                                "--log-path", str(CACHE / "ripper.log")], cwd=runner,
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
        for endpoint, folder in [("/LoadFolder", CACHE / "inputs"), ("/Export/UnityProject", CACHE / "export")]:
            print("Private native converter:", endpoint, flush=True)
            request = urllib.request.Request(base + endpoint,
                data=urllib.parse.urlencode(dict(path=str(folder))).encode(), method="POST")
            with urllib.request.urlopen(request, timeout=300) as response:
                if response.status != 200:
                    raise RuntimeError("Native converter failed: " + endpoint)
    finally:
        process.terminate()
        process.wait(timeout=15)


def documents(text):
    parts = HEADER.split(text)
    return {int(parts[index]): next(iter(yaml.safe_load(parts[index + 1]).items()))
            for index in range(1, len(parts), 2)}


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


def build():
    receipt = json.loads(RECEIPT.read_bytes())
    if receipt["manifest"] != "871_156598":
        raise ValueError("Wrong water-entity input version")
    for entry in receipt["tools"] + receipt["outputs"]:
        if sha((ROOT / entry["path"]).read_bytes()) != entry["sha256"]:
            raise ValueError("Water-entity input receipt is stale: " + entry["path"])
    for entry in receipt["inputs"]:
        if sha((CACHE / "inputs" / entry["output"]).read_bytes()) != entry["converter_input_sha256"]:
            raise ValueError("Water-entity converter input changed")
    apk = Apk.current()
    _, missing = collect(apk, ROOT_BUNDLES)
    if missing:
        raise ValueError("Missing original water entity CAB dependencies")
    source = json.loads(OUTPUT.read_bytes())
    objects = {str(obj.path_id): obj for obj in apk.objects(ROOT_BUNDLES[0])}
    originals = {}
    for record in source["components"]:
        if "references" not in record["fields"]:
            continue
        obj = objects[record["path_id"]]
        if sha(obj.get_raw_data()) != record["payload_sha256"]:
            raise ValueError("Original managed-reference payload changed")
        key = original_identity(obj)
        if key in originals:
            raise ValueError("Original water component identity is not unique: " + str(key))
        originals[key] = record, obj
    index = {}
    for path in EXPORTED.rglob("*.meta"):
        match = GUID.search(path.read_text(encoding="utf8"))
        if match:
            if match[1] in index:
                raise ValueError("Duplicate water export GUID")
            index[match[1]] = path.with_suffix("")
    cache = {}

    def read_documents(path):
        if path not in cache:
            cache[path] = documents(path.read_text(encoding="utf8"))
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
            kind = index[data["m_Script"]["guid"]].stem
            if data["m_GameObject"]["fileID"]:
                return kind, exported_identity(path, data["m_GameObject"]["fileID"])[1]
        return kind, "" if data.get("m_Name") is None else str(data["m_Name"])

    counts = Counter()

    def compare(expected, actual, obj, path, key):
        if isinstance(expected, dict):
            if set(expected) == {"m_FileID", "m_PathID"}:
                if not expected["m_PathID"]:
                    if actual != {"fileID": 0}:
                        raise ValueError("Null native pointer changed: " + key)
                else:
                    target = PPtr(**expected, assetsfile=obj.assets_file).deref()
                    export_path = index[actual["guid"]] if "guid" in actual else path
                    if original_identity(target) != exported_identity(export_path, actual["fileID"]):
                        raise ValueError("Native pointer identity changed: " + key + " " + str((original_identity(target), exported_identity(export_path, actual["fileID"]))))
                    counts["native_pointers"] += 1
                return
            if not isinstance(actual, dict) or set(expected) != set(actual):
                raise ValueError("Exported reference field names differ: " + key)
            for field in expected:
                compare(expected[field], actual[field], obj, path, key + "." + field)
        elif isinstance(expected, list):
            if not isinstance(actual, list) or len(expected) != len(actual):
                raise ValueError("Exported reference list shape differs: " + key)
            for number, (left, right) in enumerate(zip(expected, actual)):
                compare(left, right, obj, path, key + "[" + str(number) + "]")
        elif isinstance(expected, float):
            if struct.pack("<f", expected) != struct.pack("<f", actual):
                raise ValueError("Exported reference float bits differ: " + key)
        elif expected != ("" if actual is None and expected == "" else actual):
            raise ValueError("Exported reference scalar differs: " + key)
        counts["checked_values"] += 1

    outputs, checked = [], set()
    for path in sorted(EXPORTED.rglob("*.prefab")):
        raw = path.read_bytes()
        text = raw.decode("utf8")
        if "Map13OriginalOrder:" not in text:
            continue
        restored, orders = restore_exported_yaml(text)
        restored_docs = documents(restored)
        for file_id, (kind, fields) in restored_docs.items():
            if kind != "MonoBehaviour" or "references" not in fields:
                continue
            key = exported_identity(path, file_id)
            record, obj = originals[key]
            if key in checked:
                raise ValueError("Duplicate restored water-entity component")
            checked.add(key)
            for name, value in record["fields"].items():
                if name not in ("m_GameObject", "m_Script", "m_Enabled", "m_Name"):
                    compare(value, fields[name], obj, path, str(key) + "." + name)
            counts["registries"] += 1
            counts["reference_records"] += len(fields["references"]["RefIds"])
        if len(orders) != sum("references" in value for _, value in restored_docs.values()):
            raise ValueError("Not every exported registry was restored")
        target = DESTINATION / path.relative_to(EXPORTED)
        write(target, restored.encode())
        write(Path(str(target) + ".meta"), Path(str(path) + ".meta").read_bytes())
        outputs.append(dict(input=source_path(path), input_sha256=sha(raw),
                            path=source_path(target), sha256=sha(target.read_bytes()),
                            registries=len(orders)))
    if checked != set(originals) or counts["registries"] != 22 or len(outputs) != 11:
        raise ValueError("Water-entity native export coverage differs")
    ripper = CACHE / "runner/AssetRipper.GUI.Free.exe"
    result = dict(format=1, manifest=apk.version, mapClassId=13,
        input_receipt=dict(path=source_path(RECEIPT), sha256=sha(RECEIPT.read_bytes())),
        tool=dict(path=source_path(ripper), sha256=sha(ripper.read_bytes()), version="1.3.14.0"),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        counts=dict(counts), outputs=outputs,
        operation="AssetRipper 1.3.14 UnityProject export of the verified input closure; restore typed registry vectors to original ordered RefIds while retaining exported YAML record text. Compare every original reference field/list/scalar, float32 bits, descriptor and RID; resolve every non-null pointer on both sides and compare native component type and complete original GameObject hierarchy (or named asset identity).",
        limitations=["Cache conversion only. Original runtime migration, explicit assembly/script binding, Windows shaders/import, Play Mode interaction and saves are not implemented by this step."])
    write(PROOF, encode(result))
    print("Water entity native export references verified:", dict(counts), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--convert", action="store_true", help="Run a private AssetRipper export before verification")
    if parser.parse_args().convert:
        convert()
    build()
