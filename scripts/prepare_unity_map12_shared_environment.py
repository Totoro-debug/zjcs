"""Prove that Map12 can reuse global APK pipeline and current environment code."""
from pathlib import Path
import json
from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map12_sources import encode, sha, source_path, write


def build():
    apk = Apk.current(); apk.load_builtin("assets/bin/Data/data.unity3d")
    receipts, records, sources = [], {}, []
    def checked(record):
        path = ROOT / record["path"]
        if sha(path.read_bytes()) != record["sha256"]: raise ValueError("Shared original resource changed: " + record["path"])
        if record.get("meta_sha256") and sha(Path(str(path) + ".meta").read_bytes()) != record["meta_sha256"]:
            raise ValueError("Shared original resource identity changed: " + record["path"])
        records[record["path"]] = record
    def load(name):
        path = ROOT / "assets/reference/official-map" / name
        receipts.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
        return json.loads(path.read_text(encoding="utf8"))
    config = load("map13-render-config-source-manifest.json")
    for original in config["inputs"]:
        if original["source"]["bundle"] != "assets/bin/Data/data.unity3d": raise ValueError("Expected global APK pipeline source")
        files = [f for b in apk.loaded.values() for f in serialized_files(b) if f.name == "globalgamemanagers.assets"]
        if len(files) != 1: raise ValueError("Global APK settings are not unique")
        obj = files[0].objects[int(original["source"]["path_id"])]
        if sha(obj.get_raw_data()) != original["object_sha256"]: raise ValueError("Global original pipeline object differs")
        sources.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data())))
    for out in config["outputs"]: checked(out)
    native = load("map13-native-pipeline-source-manifest.json")
    for out in native["outputs"]: checked(out)
    for out in native["engine_shaders"]: checked(out)
    checked(native["configuration"]); checked(native["bindings"])
    textures = load("map13-pipeline-textures-source-manifest.json")
    for out in textures["outputs"]: checked(out)
    pipeline = load("map13-native-pipeline-binding-source-manifest.json")
    for out in pipeline["imported"]: checked(out)
    checked(pipeline["catalog"])
    environment = load("map13-environment-code-source-manifest.json")
    checked(environment["assembly"])
    for out in environment["inputs"] + environment["outputs"]: checked(out)
    original = environment["configuration"]
    obj = next(o for o in apk.objects(original["source"]["bundle"]) if str(o.path_id) == original["source"]["path_id"])
    if sha(obj.get_raw_data()) != original["object_sha256"]: raise ValueError("Current mountain environment settings differ")
    sources.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data())))
    binding = load("map13-environment-binding-source-manifest.json")
    checked(binding["source"]); checked(binding["output"])
    write(ROOT / "assets/reference/official-map/map12-shared-environment-source-manifest.json", encode(dict(
        format=1, mapClassId=12, manifest=apk.version, global_objects=sources, receipts=receipts,
        outputs=list(records.values()), pipeline_catalog=pipeline["catalog"]["path"], environment_data=binding["output"]["path"],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Reuse only global APK renderer/pipeline settings and current shared environment implementation. Reverify actual original objects, assemblies, source code, native assets and explicit resource catalogs. No Map13 partition, navigation, region or gameplay data is used.",
        limitations=["This proof is not rendered map or gameplay validation."])))
    print("Verified shared global mountain environment:", len(sources), "original objects,", len(records), "explicit assets/code payloads")


if __name__ == "__main__": build()
