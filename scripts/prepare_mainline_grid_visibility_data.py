"""Decode per-map official inputs for the shared visibility Module."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats, Reader
from official_map_codec import ground, unpack
from prepare_unity_map13_sources import encode, write

DEST = ROOT / "unity/Assets/Official/World/Mainline/Visibility"


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=digest(path.read_bytes()))


def meta(path, folder=False):
    guid = hashlib.md5(("zjcs-mainline-visibility:" + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
    body = "folderAsset: yes\nDefaultImporter:\n" if folder else "TextScriptImporter:\n"
    write(Path(str(path) + ".meta"), ("fileFormatVersion: 2\nguid: " + guid + "\n" + body + "  externalObjects: {}\n").encode())


def build(map_ids):
    apk = Apk.current()
    code = ROOT / ".tools/source-packages/official-code"
    formats = Formats(code)
    prepared = code.parent / "prepared-code-source-manifest.json"
    manifest = json.loads(prepared.read_bytes())
    expected = {row["path"]: row["sha256"] for row in manifest["outputs"]}
    assembly = code / "managed/decoded/Console.dll"
    if manifest["manifest"] != apk.version or digest(assembly.read_bytes()) != expected[assembly.relative_to(ROOT).as_posix()]:
        raise ValueError("Selected original formatter assembly checksum differs")
    proof = dict(format=1, manifest=apk.version, assembly=record(assembly), maps=[], inputs={}, outputs=[])

    def payload(bundle, suffix):
        obj = apk.asset(bundle, suffix)
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        proof["inputs"][suffix] = dict(apk.provenance(obj), payload_sha256=digest(raw), object_sha256=digest(obj.get_raw_data()))
        return raw

    candidates = {}
    for address, pointer in apk.container("config_ec"):
        name = address.rsplit("/", 1)[-1]
        if not name.startswith("World"):
            continue
        obj = pointer.deref()
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        legacy = False
        try:
            rows = unpack(raw)[0]
        except (ValueError, TypeError):
            rows = unpack(raw[2:])[0]
            legacy = True
        for parent, identity, components in rows:
            if parent or (identity in candidates and legacy and not candidates[identity][1]):
                continue
            candidates[identity] = (components, legacy, dict(apk.provenance(obj), asset=address, payload_sha256=digest(raw)))

    wanted = {"LoGridTransformComponent", "LoEntityComponent", "LoEntityCampComponent", "LoEntityCampMonsterComponent"}
    for map_id in map_ids:
        nav = ground(payload("config_ec_maprootentitylite", f"/{map_id}.g.bytes"))
        if nav["id"] != map_id:
            raise ValueError("Foreign original ground identity")
        original = {}
        for parent, identity, components in unpack(payload("config_ec", f"/map_{map_id}.g.bytes"))[0]:
            if parent != map_id or identity in original:
                raise ValueError("Duplicate or foreign original placement")
            original[identity] = {typ.removeprefix("Common."): formats.component(typ, raw)
                for typ, raw in components if typ.removeprefix("Common.") in wanted}
        class_ids = {nav["character"]}
        class_ids.update(row["LoEntityComponent"]["SpanwInfo"]["ClassId"] for row in original.values() if "LoGridTransformComponent" in row)
        templates = []
        character = None
        template_inputs = {}
        for identity in sorted(class_ids):
            if identity not in candidates:
                raise ValueError(f"Map{map_id} original World template absent: {identity}")
            components, legacy, evidence = candidates[identity]
            template_inputs[str(identity)] = evidence
            for typ, raw in components:
                name = typ.removeprefix("Common.")
                if name not in ("GridTransformComponent", "WorldMapCharacterComponent"):
                    continue
                value = formats.component(typ, raw[2:] if legacy and raw else raw)
                if name == "GridTransformComponent":
                    value["RealCenter"] = dict(x=value["RealCenter"][0], y=value["RealCenter"][1])
                    templates.append(dict(classId=identity, body=value))
                elif identity == nav["character"]:
                    character = value
        if character is None or not any(t["classId"] == nav["character"] for t in templates):
            raise ValueError("Original player body/view range unbound")
        raw = payload("config_ec_mapregionactiveunlockposinfo", f"/{map_id}.g.bytes")
        reader = Reader(raw)
        regions = formats.read(reader, "MapRegionActiveUnlockPosSingleInfo")
        if reader.pos != len(raw) or regions["MapClassId"] != map_id:
            raise ValueError("Original region activation payload differs")
        rows = [dict(region=int(key), **value) for key, value in regions["UnlockPosDict"].items()]
        placements = [dict(placementId=identity, classId=row["LoEntityComponent"]["SpanwInfo"]["ClassId"],
            transform=row["LoGridTransformComponent"], campMembers=row.get("LoEntityCampComponent", {}).get("CampMonsterEntities"),
            campOwner=row.get("LoEntityCampMonsterComponent", {}).get("CampLoEntityId", 0))
            for identity, row in original.items() if "LoGridTransformComponent" in row]
        folder = DEST / f"Resources/World/Mainline/Visibility/Map{map_id}"
        for name, value in [("visibility", dict(manifest=apk.version, mapClassId=map_id, size=nav["size"],
                characterClassId=nav["character"], viewRange=character["ViewRange"], regions=rows)),
                ("bodies", dict(manifest=apk.version, mapClassId=map_id, templates=templates, entities=placements))]:
            path = folder / (name + ".json")
            write(path, encode(value)); meta(path)
        proof["maps"].append(dict(mapClassId=map_id, size=nav["size"], origin=nav["origin"],
            viewRange=character["ViewRange"], characterClassId=nav["character"], regions=len(rows),
            bodies=len(templates), placements=len(placements), templates=template_inputs))
        print(f"Map{map_id}: {nav['size']}, {len(rows)} regions, {len(templates)} bodies, {len(placements)} placements", flush=True)
    for directory in [DEST, *sorted(p for p in DEST.rglob("*") if p.is_dir())]:
        meta(directory, folder=True)
    # Keep historical definition versions distinct; prove only the fields used
    # by this loader against every actually installed placement and grid size.
    from record_mainline_grid_visibility_sources import map_binding
    proof["active_map_bindings"] = [map_binding(map_id,
        f"unity/Assets/Official/World/Mainline/Visibility/Resources/World/Mainline/Visibility/Map{map_id}/visibility.json",
        f"unity/Assets/Official/World/Mainline/Visibility/Resources/World/Mainline/Visibility/Map{map_id}/bodies.json") for map_id in map_ids]
    proof["formatters"] = [record(p) for p in sorted(set(formats.sources.values()))]
    for row in proof["formatters"]:
        if expected.get(row["path"]) != row["sha256"]:
            raise ValueError("Consumed formatter differs from selected prepared source: " + row["path"])
    proof["prepared_source"] = record(prepared)
    proof["outputs"] = [record(p) for p in sorted(DEST.rglob("*")) if p.is_file()]
    proof["tools"] = [record(Path(__file__).resolve()), record(ROOT / "scripts/official_ec_codec.py"), record(ROOT / "scripts/official_map_codec.py"), record(ROOT / "scripts/record_mainline_grid_visibility_sources.py")]
    proof["method"] = "Decode original per-map ground, EC placement/body/view-range and region-active unlock lists with selected official generated formatters. Preserve array ordering, placement transforms and camp references; adapt only RealCenter Vector2 representation consumed by JsonUtility. This receipt does not establish scene integration or playability."
    write(ROOT / "assets/reference/official-map/mainline-grid-visibility-data-source-manifest.json", encode(proof))
    apk.archive.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--maps", type=int, nargs="+", default=[10, 11, 12, 14, 15, 16])
    build(parser.parse_args().maps)
