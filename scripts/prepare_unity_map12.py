"""Reproduce source-checked mountain Unity payloads from APK and the selected cache."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT, canonical_bundle_name
from prepare_unity_map12_sources import build as decode_sources, encode, sha, source_path, write


OUTPUT = ROOT / "unity/Assets/Official/World/Map/12"
RECEIPT = ROOT / "assets/reference/official-map/map12-unity-source-manifest.json"


def verify_art_bundles():
    """Reusing an exported image requires its original bundle to be identical."""
    apk = Apk.current()
    source_receipts = [ROOT / "assets/reference/official-map" / name for name in (
        "materials-12-source-manifest.json", "models-source-manifest.json",
        "extra-models-source-manifest.json", "story-actors-source-manifest.json",
        "minimaps-source-manifest.json")]
    catalog = json.loads((OUTPUT / "map12-entities.json").read_bytes()) if (OUTPUT / "map12-entities.json").exists() else None
    # Before the first conversion, select all mountain/shared model textures from
    # the original model catalog, never a similarly named replacement image.
    if catalog is None:
        from prepare_unity_map11_definition import build
        from prepare_unity_map10_entities import build as entities
        build(12)
        entities(map_id=12)
        catalog = json.loads((OUTPUT / "map12-entities.json").read_bytes())
    texture_uris = set(catalog["textureUris"])
    from map10_geometry import decode, version
    minimap_catalog = json.loads((ROOT / "assets/official/map-minimaps/catalog.json").read_bytes())
    geometry = ROOT / minimap_catalog["12"]["file"].removeprefix("res://")
    geometry_bytes = geometry.read_bytes()
    texture_uris.update(row["metadata"]["texture"] for row in decode(geometry_bytes, version(geometry_bytes)))
    records = []
    for path in source_receipts:
        data = json.loads(path.read_bytes())
        if path.name == "materials-12-source-manifest.json":
            records.extend(data["inputs"].values())
        for uri, record in data.get("resources", {}).items():
            if path.name == "materials-12-source-manifest.json" or uri in texture_uris:
                records.append(record.get("source", record) if record.get("origin") == "derived_official" else record)
    bundles, changed_objects, objects_by_bundle = {}, [], {}
    for record in records:
        if "bundle" not in record or "bundle_hash" not in record:
            raise ValueError("Original art record has no verifiable bundle identity")
        name = record["bundle"]
        if name in apk.index and apk.index[name] != record["bundle_hash"]:
            if "bytes" not in record or not record.get("sha256"):
                raise ValueError("Changed source bundle requires fresh object conversion: " + name)
            if name not in objects_by_bundle:
                objects_by_bundle[name] = {str(obj.path_id): obj for obj in apk.objects(name)}
            obj = objects_by_bundle[name].get(str(record["path_id"]))
            if obj is None or sha(obj.get_raw_data()) != record["sha256"]:
                raise ValueError("Selected official material object changed: " + name + ":" + str(record["path_id"]))
            changed_objects.append(dict(bundle=name, path_id=str(record["path_id"]),
                legacy_bundle_md5=record["bundle_hash"], selected_bundle_md5=apk.index[name],
                object_sha256=record["sha256"]))
        else:
            name = canonical_bundle_name({"bundle": name, "hash": record["bundle_hash"]}, apk.index)
        bundles[name] = apk.index[name]
    verified = []
    for name, expected in sorted(bundles.items()):
        entry = f"assets/yoo/DefaultPackage/{expected}.bundle"
        if entry in apk.available:
            raw = apk.archive.read(entry)
            origin = dict(origin="apk_zip", apk_entry=entry, apk_sha256=apk.source.apk_sha256)
        else:
            cached = apk.cloud.read("DefaultPackage", apk.bundle_records[name])
            if cached is None:
                raise FileNotFoundError("Selected official art bundle is not cached: " + name)
            raw, origin = cached
        if hashlib.md5(raw).hexdigest() != expected or len(raw) != apk.bundle_records[name]["size"]:
            raise ValueError("Selected art bundle differs from its original indexed identity: " + name)
        verified.append(dict(bundle=name, md5=expected, sha256=sha(raw), bytes=len(raw), source=origin))
    apk.archive.close()
    return verified, source_receipts, changed_objects


def build():
    source = decode_sources()
    from prepare_unity_map11_definition import build as definition
    from prepare_unity_map11_rendering import build as terrain
    from prepare_unity_map10_entities import build as entities
    from prepare_unity_map10_minimap import build as minimap
    from prepare_unity_map10_battle_data import build as battles
    from prepare_unity_map11_base_ground import build as base_ground
    from prepare_unity_map12_runtime_data import build as runtime
    definition(12)
    entities(map_id=12)
    art, art_receipts, changed_objects = verify_art_bundles()
    terrain(12)
    minimap(OUTPUT, 12)
    battles(map_id=12)
    base_ground(12)
    from prepare_unity_map12_coordinates import build as coordinates
    coordinates()
    runtime()
    manifests = [OUTPUT / name for name in ("source-manifest.json", "rendering-source-manifest.json",
        "entity-source-manifest.json", "minimap-source-manifest.json", "battle-source-manifest.json",
        "base-ground-source-manifest.json")]
    manifests += [ROOT / "assets/reference/official-map/map12-coordinate-source-manifest.json",
                  ROOT / "assets/reference/official-map/map12-runtime-source-manifest.json",
                  ROOT / "assets/reference/official-map/map12-source-manifest.json"]
    files = sorted(p for p in OUTPUT.rglob("*") if p.is_file() and p.suffix in (".json", ".bytes", ".png"))
    result = dict(format=1, mapClassId=12, manifest=source["manifest"],
        operation="Decode selected official assemblies/configs/stories; reuse historical conversions only after matching mountain records, complete art bundles or unchanged objects; restore Unity coordinates with current native mesh checks.",
        legacy_conversion_manifest="854_156383", legacy_reuse_checks=source["verified_legacy_equivalence"],
        bundles=art, unchanged_objects_in_updated_bundles=changed_objects,
        source_manifests=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in manifests + art_receipts],
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes()), bytes=p.stat().st_size) for p in files],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        limitations=["Converted payloads are not a completed map. Runtime, original UI, dynamic monsters, puzzles, water/lava/light rendering, audio, saves and Windows Player gameplay must be validated separately."])
    write(RECEIPT, encode(result))
    print(f"Map12 checked conversion: {len(art)} verified official art bundles, {len(files)} payload files", flush=True)
    return result


if __name__ == "__main__":
    build()
