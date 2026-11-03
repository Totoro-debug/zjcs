"""Resolve official mainline entity and map-display prefab addresses.

This verifies Android source objects and CAB dependencies. The resulting JSON
is converter input only; it does not claim Android assets are Windows-ready.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / ".tools/python-packages"))
from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_bundle_inputs import collect


def resolve(map_id: int) -> dict:
    map_root = ROOT / "unity/Assets/Official/World/Map" / str(map_id)
    source_manifest_path = map_root / "source-manifest.json"
    source_manifest = json.loads(source_manifest_path.read_bytes())
    if source_manifest.get("mapClassId") != map_id:
        raise ValueError("Official map source manifest identity differs")
    apk = Apk(manifest=source_manifest["manifest"])

    source_root = map_root / "Source"
    placement_path = source_root / "placement.json"
    navigation_path = source_root / "navigation.json"
    for path in (placement_path, navigation_path):
        relative = path.relative_to(ROOT).as_posix()
        proof = next((row for row in source_manifest["inputs"] if row["path"] == relative), None)
        if proof is None or hashlib.sha256(path.read_bytes()).hexdigest() != proof["sha256"]:
            raise ValueError("Official map input is not covered by its source receipt: " + relative)

    placement = json.loads(placement_path.read_bytes())
    navigation = json.loads(navigation_path.read_bytes())
    entities = placement.get("entities")
    if placement.get("id") != map_id or not isinstance(entities, list) or not entities:
        raise ValueError("Official map placement identity or entity list is invalid")

    index_asset = apk.asset("config_ec", "index.g.txt")
    index = json.loads(index_asset.read().m_Script)
    directories = {}
    for directory, class_ids in index.items():
        for class_id in class_ids:
            if class_id in directories:
                raise ValueError("Official entity index duplicates class id " + str(class_id))
            directories[class_id] = directory

    class_ids = {item["entity"] for item in entities}
    class_ids.update((navigation["character"], navigation["member"]))
    missing_ids = sorted(class_ids - directories.keys())
    if missing_ids:
        raise ValueError("Official entity index is missing map class ids: " + str(missing_ids))

    requests = {}
    for class_id in sorted(class_ids):
        path = "Assets/Prefabs/EcEntity/{}/{:d}.prefab".format(directories[class_id], class_id)
        requests.setdefault(path, []).append({"kind": "ec_entity", "class_id": class_id})

    display_asset = apk.asset("defaultpackage_assets_config_luban_map.bundle",
                              "/Map/entity_map_display.g.bytes")
    display_bytes = display_asset.read().m_Script.encode("utf8", "surrogateescape")
    constructors = Constructors()
    mapping = {match[2]: match[1] for match in
               re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source("Tables"))}
    if "Map/entity_map_display" not in mapping:
        raise ValueError("Official entity display table constructor is unavailable")
    displays = constructors.table(display_bytes, mapping["Map/entity_map_display"])
    for row_number, row in enumerate(displays):
        if (row.get("EntityClassId") not in class_ids or row.get("GroupId") not in (0, map_id)
                or row.get("MapClassId") not in (0, map_id) or not row.get("DisplayPath")):
            continue
        path = "Assets/Prefabs/" + row["DisplayPath"] + ".prefab"
        requests.setdefault(path, []).append({"kind": "entity_display", "row": row_number, "selector": row})

    roots, records = set(), []
    for path, uses in sorted(requests.items()):
        parts = path.removesuffix(".prefab").lower().split("/")
        candidates = ["defaultpackage_" + "_".join(parts[:count]) + ".bundle"
                      for count in range(len(parts), 1, -1)]
        matches = []
        for bundle in candidates:
            if bundle not in apk.index:
                continue
            for name, pointer in apk.container(bundle):
                if name.casefold() == path.casefold():
                    obj = pointer.deref()
                    if obj.type.name == "GameObject":
                        matches.append((bundle, name, obj))
        if len(matches) != 1:
            raise ValueError("Resolve original prefab against selected package: " + path
                             + " matches=" + str(len(matches)))
        bundle, actual_path, obj = matches[0]
        roots.add(bundle)
        records.append({"path": path, "container_path": actual_path, "uses": uses,
                        "source": apk.provenance(obj),
                        "object_sha256": hashlib.sha256(obj.get_raw_data()).hexdigest()})

    assets, missing = collect(apk, sorted(roots))
    if missing:
        raise ValueError("Original entity prefab CAB closure is incomplete: " + str(missing))
    source_inputs = []
    for path in (source_manifest_path, placement_path, navigation_path):
        source_inputs.append({"path": path.relative_to(ROOT).as_posix(),
                              "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    tool_inputs = []
    for path in (Path(__file__).resolve(), Path(__file__).resolve().with_name("official_tables.py"),
                 Path(__file__).resolve().with_name("prepare_unity_bundle_inputs.py"),
                 Path(__file__).resolve().with_name("official_apk.py")):
        tool_inputs.append({"path": path.relative_to(ROOT).as_posix(),
                            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    return {
        "format": 1,
        "manifest": apk.version,
        "mapClassId": map_id,
        "source_manifest": source_manifest["manifest"],
        "index": {"source": apk.provenance(index_asset),
                  "object_sha256": hashlib.sha256(index_asset.get_raw_data()).hexdigest()},
        "display_table": {"source": apk.provenance(display_asset),
                          "object_sha256": hashlib.sha256(display_asset.get_raw_data()).hexdigest(),
                          "payload_sha256": hashlib.sha256(display_bytes).hexdigest(),
                          "rows": len(displays), "constructor": mapping["Map/entity_map_display"]},
        "source_inputs": source_inputs,
        "tools": tool_inputs,
        "class_ids": sorted(class_ids),
        "roots": sorted(roots),
        "prefabs": records,
        "serialized_files": list(assets.values()),
        "counts": {"placements": len(entities), "class_ids": len(class_ids),
                   "prefabs": len(records), "root_bundles": len(roots),
                   "serialized_files": len(assets)},
        "operation": "Resolve official placement class IDs and display selectors through the selected official EC index, verify each prefab GameObject in the pinned APK/CDN package, and close serialized CAB dependencies. Android inputs still require native Windows conversion and runtime binding.",
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map-id", type=int, required=True)
    args = parser.parse_args()
    result = resolve(args.map_id)
    output = ROOT / ".tools" / "mainline-entity-prefab-roots" / result["manifest"] / ("map-" + str(args.map_id) + ".json")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    receipt = ROOT / "assets/reference/official-map" / ("map" + str(args.map_id) + "-entity-prefab-roots-source-manifest.json")
    receipt.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Resolved mainline entity prefabs:", result["counts"])
    print("Verified source bundles:", len(result["roots"]))
    print("Converter input:", output.relative_to(ROOT))
    print("Source receipt:", receipt.relative_to(ROOT))


if __name__ == "__main__":
    main()
