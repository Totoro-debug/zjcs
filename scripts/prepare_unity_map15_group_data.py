"""Package the pinned Map15 tower map group as per-map Unity Resources."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = "854_156383"
ART = ROOT / "assets/official"
OUT = ROOT / "unity/Assets/Game/Resources/Map15/Group"
RECEIPT = ROOT / "assets/reference/official-map/map15-group-data-source-manifest.json"


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def encode(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode("utf-8")


def source(path):
    raw = path.read_bytes()
    return {"path": path.relative_to(ROOT).as_posix(), "bytes": len(raw), "sha256": digest(raw)}


def write_json(path, value):
    raw = encode(value)
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != raw:
        path.write_bytes(raw)
    return digest(raw)


def write_meta(path, identity):
    guid = hashlib.md5(("zjcs-map15-group:" + identity).encode()).hexdigest()
    raw = (f"fileFormatVersion: 2\nguid: {guid}\nTextScriptImporter:\n"
           "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode()
    target = Path(str(path) + ".meta")
    if target.exists() and target.read_bytes() != raw:
        raise ValueError("Refusing to replace unrelated Unity metadata: " + str(target))
    if not target.exists():
        target.write_bytes(raw)


def write_folder_meta(path, identity):
    guid = hashlib.md5(("zjcs-map15-group:" + identity).encode()).hexdigest()
    raw = (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n"
           "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode()
    target = Path(str(path) + ".meta")
    if target.exists():
        existing = target.read_text(encoding="utf-8")
        if "guid: " not in existing or "folderAsset: yes" not in existing or "DefaultImporter:" not in existing:
            raise ValueError("Existing Unity metadata is not a folder asset: " + str(target))
        existing_guid = next(line[6:].strip() for line in existing.splitlines() if line.startswith("guid: "))
        if len(existing_guid) != 32:
            raise ValueError("Existing Unity folder metadata has an invalid GUID: " + str(target))
        normalized = (f"fileFormatVersion: 2\nguid: {existing_guid}\nfolderAsset: yes\nDefaultImporter:\n"
                      "  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode()
        if target.read_bytes() != normalized:
            target.write_bytes(normalized)
    else:
        target.write_bytes(raw)


def build(output=OUT, receipt=RECEIPT):
    adventure_path = ART / "adventure.json"
    tables = json.loads(adventure_path.read_bytes())["tables"]
    maps = sorted((r for r in tables["Map/map"] if r.get("MapGroupId") == 15), key=lambda r: r["ClassId"])
    ids = {r["ClassId"] for r in maps}
    all_map_ids = {r["ClassId"] for r in tables["Map/map"]}
    regions = [r for r in tables["Map/map_region"] if r.get("ClassId") in ids]
    if len(maps) != 60 or len(regions) != 118 or 15 not in ids:
        raise ValueError("Pinned Map15 group must contain 60 maps and 118 regions")

    template_path = ART / "map-entities.json"
    template_receipt_path = ROOT / "assets/reference/official-map/entities-source-manifest.json"
    template_receipt = json.loads(template_receipt_path.read_bytes())
    template_raw = template_path.read_bytes()
    if template_receipt.get("manifest") != VERSION or template_receipt.get("output_sha256") != digest(template_raw):
        raise ValueError("Pinned official EC templates differ from their source receipt")
    templates = json.loads(template_raw)["templates"]
    inputs = [source(adventure_path), source(template_path), source(template_receipt_path)]
    outputs, catalog = [], []
    totals = {"maps": len(maps), "regions": len(regions), "placements": 0,
              "placementComponents": 0, "templates": 0, "templateComponents": 0}
    used_classes = set()
    for row in maps:
        map_id = int(row["ClassId"])
        region_path = ART / "regions" / f"{map_id}.json"
        components_path = ART / "regions" / f"{map_id}-entities.json"
        data_raw, components_raw = region_path.read_bytes(), components_path.read_bytes()
        data, components = json.loads(data_raw), json.loads(components_raw)
        placements = data.get("placement", {}).get("entities", [])
        if data.get("id") != map_id or {str(e["id"]) for e in placements} != set(components):
            raise ValueError("Map15 placement/component closure differs: " + str(map_id))
        classes = {str(e["entity"]) for e in placements}
        if classes - set(templates):
            raise ValueError("Missing official entity template on Map15 map " + str(map_id))
        used_classes.update(classes)
        map_regions = sorted((r for r in regions if r["ClassId"] == map_id), key=lambda r: r["Region"])
        payload = {"manifest": VERSION, "map": row, "regions": map_regions,
                   "regionData": data, "entityComponents": components}
        rel = f"maps/{map_id}.json"
        target = output / rel
        output_hash = write_json(target, payload)
        write_meta(target, "Map15/Group/" + rel)
        try:
            output_name = target.relative_to(ROOT).as_posix()
        except ValueError:
            output_name = target.as_posix()
        outputs.append({"path": output_name, "bytes": target.stat().st_size, "sha256": output_hash})
        inputs.extend((source(region_path), source(components_path)))
        totals["placements"] += len(placements)
        totals["placementComponents"] += sum(map(len, components.values()))
        catalog.append({"ClassId": map_id, "NextMapClassId": row["NextMapClassId"],
                        "MapGroundPath": row["MapGroundPath"], "MapBgmId": row["MapBgmId"],
                        "RegionCount": len(map_regions), "PlacementCount": len(placements),
                        "Resource": "Map15/Group/maps/" + str(map_id)})

    template_catalog = {key: templates[key] for key in sorted(used_classes, key=int)}
    totals["templates"] = len(template_catalog)
    totals["templateComponents"] = sum(len(value) for value in template_catalog.values())
    totals["components"] = totals["placementComponents"] + totals["templateComponents"]
    if totals["placements"] != 2899 or totals["templates"] != 82 or totals["components"] != 23291:
        raise ValueError("Pinned Map15 EC closure differs from 2,899 placements, 82 templates and 23,291 components")
    if any(r["NextMapClassId"] not in all_map_ids and r["NextMapClassId"] != 0 for r in catalog):
        raise ValueError("Map15 transition points to an unknown official map")
    templates_path = output / "entity-templates.json"
    templates_hash = write_json(templates_path, {"manifest": VERSION, "templates": template_catalog})
    write_meta(templates_path, "Map15/Group/entity-templates.json")
    try:
        templates_name = templates_path.relative_to(ROOT).as_posix()
    except ValueError:
        templates_name = templates_path.as_posix()
    outputs.append({"path": templates_name, "bytes": templates_path.stat().st_size, "sha256": templates_hash})
    catalog_path = output / "group-catalog.json"
    catalog_hash = write_json(catalog_path, {"manifest": VERSION, "mapGroupId": 15,
        "maps": catalog, "counts": totals, "EntityTemplatesResource": "Map15/Group/entity-templates"})
    write_meta(catalog_path, "Map15/Group/group-catalog.json")
    try:
        catalog_name = catalog_path.relative_to(ROOT).as_posix()
    except ValueError:
        catalog_name = catalog_path.as_posix()
    outputs.append({"path": catalog_name, "bytes": catalog_path.stat().st_size, "sha256": catalog_hash})
    write_folder_meta(output.parent, "Map15")
    write_folder_meta(output, "Map15/Group")
    write_folder_meta(output / "maps", "Map15/Group/maps")
    result = {"manifest": VERSION, "mapGroupId": 15, "counts": totals,
              "operation": "Package official map rows, regions, placements and decoded EC components; preserve source gates and transitions without applying server-only release behavior.",
              "inputs": inputs, "outputs": outputs}
    receipt.parent.mkdir(parents=True, exist_ok=True)
    receipt.write_bytes(encode(result))
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-root", type=Path, default=OUT)
    parser.add_argument("--receipt", type=Path, default=RECEIPT)
    args = parser.parse_args()
    result = build(args.output_root.resolve(), args.receipt.resolve())
    print("Packaged Map15:", result["counts"])
