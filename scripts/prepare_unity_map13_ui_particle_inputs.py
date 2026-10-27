"""Stage verified original CAB closure for Map13 UI native particle conversion."""
import argparse
import json
from pathlib import Path
from official_apk import Apk, ROOT
from prepare_unity_bundle_inputs import collect, read_source
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_water_entity_export import convert

CACHE = ROOT / ".tools/map13-ui-particle-native-cache-871_156598"
SOURCE = ROOT / "unity/Assets/Official/World/Map/13/Source/native-ui-components.json"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-ui-particle-input-source-manifest.json"


def build():
    source = json.loads(SOURCE.read_bytes())
    source_receipt = ROOT / "assets/reference/official-map/map13-native-ui-components-source-manifest.json"
    previous = json.loads(source_receipt.read_bytes())
    if source["manifest"] != "871_156598" or previous["output"]["sha256"] != sha(SOURCE.read_bytes()):
        raise ValueError("Native UI particle selection differs from verified official source")
    objects = {obj["identity"]: obj for obj in source["objects"]}
    def contains(node):
        return any(objects[c]["type"] == "ParticleSystem" for c in node["components"]) or any(contains(c) for c in node["children"])
    selection = [dict(key=p["key"], source=objects[p["hierarchy"]["object"]]["source"]) for p in source["prefabs"] if contains(p["hierarchy"])]
    roots = sorted({row["source"]["bundle"] for row in selection})
    apk = Apk.current()
    assets, missing = collect(apk, roots)
    if missing:
        raise ValueError("Missing original UI particle CAB dependencies: " + str(missing))
    inputs = []
    for name in sorted(apk.loaded):
        raw, record = read_source(apk, name)
        record["output"] = Path(name).name
        record["converter_input_sha256"] = sha(raw)
        write(CACHE / "inputs" / record["output"], raw)
        inputs.append(record)
    result = dict(manifest=apk.version, mapClassId=13,
        operation="Export unmodified decoded original bundles and every actual CAB dependency to recover native engine ParticleSystem/Renderer YAML. UI script restoration and Windows assets remain separate; unknown stripped scripts may not be used as restored UI implementations.",
        sources=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in (SOURCE, source_receipt)],
        selection=selection, roots=roots, inputs=inputs, serialized_files=list(assets.values()),
        tools=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in
            (Path(__file__).resolve(), ROOT / "scripts/prepare_unity_bundle_inputs.py")])
    write(RECEIPT, encode(result)); write(CACHE / "inputs/source-manifest.json", encode(result))
    print("Native UI particle input:", len(selection), "prefabs", len(inputs), "bundles", flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--convert", action="store_true")
    args = parser.parse_args()
    build()
    if args.convert:
        convert(CACHE)
