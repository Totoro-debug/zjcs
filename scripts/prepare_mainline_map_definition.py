"""Project selected official map records into the shared Unity map definition.

Map identity is the only map-specific input. This does not register a playable
map, supply missing server rules, or substitute another map's configuration.
"""
import argparse
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import entities, ground, unpack
from official_tables import Constructors
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map14_sources import meta


def compact(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False)


def action_ids(value):
    if isinstance(value, list):
        for item in value:
            yield from action_ids(item)
    elif isinstance(value, dict):
        for key, item in value.items():
            if key.endswith(("ActionId", "ActionIds")):
                for identity in item if isinstance(item, list) else [item]:
                    if isinstance(identity, int) and identity > 0:
                        yield identity
            yield from action_ids(item)
    elif isinstance(value, str) and value.startswith("{"):
        try:
            yield from action_ids(json.loads(re.sub(r'([A-Za-z_][A-Za-z_0-9]*):', r'"\1":', value)))
        except json.JSONDecodeError:
            pass


class MainlineMapSource:
    def __init__(self, map_id):
        if map_id <= 0:
            raise ValueError("A positive original map identity is required")
        self.map_id = map_id
        self.apk = Apk.current()
        self.code = ROOT / ".tools/source-packages/official-code"
        self.prepared_path = ROOT / ".tools/source-packages/prepared-code-source-manifest.json"
        prepared = json.loads(self.prepared_path.read_bytes())
        if prepared["manifest"] != self.apk.version:
            raise ValueError("Prepared official code version differs from selected map source")
        self.checked = {row["path"]: row["sha256"] for row in prepared["outputs"]}
        self.constructors, self.formats = Constructors(self.code), Formats(self.code)
        self.inputs, self.template_sources = {}, {}

    def payload(self, bundle, suffix):
        obj = self.apk.asset(bundle, suffix)
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        self.inputs[suffix] = dict(self.apk.provenance(obj), payload_sha256=sha(raw),
            object_sha256=sha(obj.get_raw_data()), bytes=len(raw))
        return obj, raw

    def table(self, bundle, name, kind):
        obj, raw = self.payload(bundle, "/" + name + ".g.bytes")
        return self.constructors.table(raw, kind), obj

    def templates(self, required):
        candidates = {}
        for address, pointer in self.apk.container("config_ec"):
            if not address.rsplit("/", 1)[-1].startswith(("World", "ActionWorld", "ActionCommon")):
                continue
            obj = pointer.deref()
            raw = obj.read().m_Script.encode("utf8", "surrogateescape")
            legacy = False
            try:
                rows = unpack(raw)[0]
            except (ValueError, TypeError):
                rows, legacy = unpack(raw[2:])[0], True
            for parent, identity, components in rows:
                if parent or (identity in candidates and legacy and not candidates[identity][1]):
                    continue
                if identity in candidates and candidates[identity][1] == legacy:
                    if components != candidates[identity][0]:
                        raise ValueError("Conflicting original EC template: " + str(identity))
                candidates[identity] = (components, legacy, obj, address, sha(raw))
        result = {}
        while pending := sorted(required - set(result)):
            for identity in pending:
                if identity not in candidates:
                    raise ValueError("Missing original map template/action: " + str(identity))
                components, legacy, obj, address, digest = candidates[identity]
                result[identity] = {kind.removeprefix("Common."):
                    self.formats.component(kind, raw[2:] if legacy and raw else raw) for kind, raw in components}
                self.template_sources[identity] = dict(self.apk.provenance(obj), asset=address,
                    legacy=legacy, payload_sha256=digest, object_sha256=sha(obj.get_raw_data()))
                required.update(action_ids(result[identity]))
        return result

    def checked_code(self):
        paths = set(self.formats.sources.values())
        for name in self.constructors.sources:
            paths.add(next(path for path in (self.code / "common-source" / folder / (name + ".cs")
                for folder in ("Common", "Common.LubanGen")) if path.is_file()))
        paths.add(self.code / "global-metadata.dat")
        for name in ("MapRootEntityLiteSingleInfo", "MapRootEntityLiteGridInfo", "MapRootEntityLiteAreaInfo",
                "MapInfoWrap", "MapWorldLoEntityInfoWrap", "EntityPositionInfo", "SpawnEntityLiteInfo",
                "LoEntityComponentSpawnSupportInfo", "GridMoverComponent", "GridMoverComponentInfo"):
            paths.add(self.code / "common-source/Common" / (name + ".cs"))
        result = []
        for path in sorted(paths):
            digest = sha(path.read_bytes())
            if self.checked.get(source_path(path)) != digest:
                raise ValueError("Consumed official source differs from prepared receipt: " + str(path))
            result.append(dict(path=source_path(path), sha256=digest))
        return result

    def decode(self):
        identity = self.map_id
        nav = ground(self.payload("config_ec_maprootentitylite", f"/{identity}.g.bytes")[1])
        placement = entities(self.payload("config_mapinfowrap", f"/mapinfowrap_{identity}.g.bytes")[1])
        if nav["id"] != identity or placement["id"] != identity:
            raise ValueError("Original navigation/placement identity differs")
        local = {}
        for parent, entity_id, components in unpack(self.payload("config_ec", f"/map_{identity}.g.bytes")[1])[0]:
            if parent != identity or entity_id in local:
                raise ValueError("Duplicate or foreign original EC placement")
            local[entity_id] = {kind.removeprefix("Common."): self.formats.component(kind, raw)
                for kind, raw in components}
        if len(placement["entities"]) != len(local) or set(local) != {r["id"] for r in placement["entities"]}:
            raise ValueError("MapInfoWrap and full EC placements differ")
        templates = self.templates({r["entity"] for r in placement["entities"]}
            | {nav["character"], nav["member"]} | set(action_ids(list(local.values()))))
        configs, tables = [], {}
        for name, kind, scoped in (("map", "TbMapInfo", True), ("map_world", "TbMapWorldInfo", True),
                ("map_region", "TbMapRegionInfo", True), ("map_area", "TbMapAreaInfo", True),
                ("entity_extend_parameter", "TbEntityExtendParameterInfo", False)):
            rows, obj = self.table("config_luban_map", name, kind)
            rows = [r for r in rows if r["ClassId"] == identity] if scoped else rows
            if not rows:
                raise ValueError("Missing original map configuration: " + name)
            tables[kind] = rows
            for row in rows:
                text = compact(row)
                configs.append(dict(TableName=kind, RecordId=str(row["ClassId"] if scoped else row["EntityClassId"]),
                    Json=text, Sha256=sha(text.encode()), SourceBundle=self.inputs["/" + name + ".g.bytes"]["bundle"],
                    SourcePathId=str(obj.path_id)))
        map_info, = tables["TbMapInfo"]
        world, = tables["TbMapWorldInfo"]
        regions = tables["TbMapRegionInfo"]
        if {r["Region"] for r in regions} != set(placement["progress"]):
            raise ValueError("Region table and original progress maxima differ")
        if len(regions) != len(placement["progress"]) or any(r["region"] not in placement["progress"] for r in placement["entities"]):
            raise ValueError("Duplicate regions or entity references to absent region")
        if map_info["MapGroupId"] != placement["group"] or map_info["MapTagId"] != placement["tag"]:
            raise ValueError("Original map identity fields disagree")
        if world["DefEntryId"] not in local:
            raise ValueError("Original entry placement is absent")
        condition_ids = {r["AccessibleConditionId"] for r in regions if r["AccessibleConditionId"] > 0}
        if condition_ids:
            # Do not emit a definition whose condition closure has been omitted.
            raise ValueError("Add original recursive access-condition projection before using this map: " + str(sorted(condition_ids)))
        mover_rows, _ = self.table("config_luban_ec", "entity_grid_mover", "TbEntityGridMoverInfo")
        tables["TbEntityGridMoverInfo"] = mover_rows
        types = {r["Id"]: r["GroundTypes"] for r in mover_rows}
        mover = templates[nav["character"]]["GridMoverComponent"]
        walk = types[mover["WalkableGroundTypeKey"]]
        override = types.get(mover["WalkableGroundTypeServerOverrideKey"], [])
        if any(not 0 <= t < 32 for t in walk + override):
            raise ValueError("Ground type outside original movement mask")
        move_mask = 0
        for value in walk:
            move_mask |= 1 << value
        definition = dict(ClassId=identity, MapGroupId=map_info["MapGroupId"], MapTagId=map_info["MapTagId"],
            MapBgmId=map_info["MapBgmId"], NextMapClassId=map_info["NextMapClassId"],
            OriginalGroundPrefab="Assets/Prefabs/Map/" + map_info["MapGroundPath"] + ".prefab",
            OriginalMiniMapPrefab="Assets/Prefabs/Map/" + map_info["MiniMapPath"] + ".prefab" if map_info["MiniMapPath"] else "",
            SourceManifest=f"Assets/Official/World/Map/{identity}/source-manifest.json",
            CharacterMover=dict(CharacterClassId=nav["character"], **mover, WalkableGroundTypes=walk,
                ServerOverrideGroundTypes=override, MoveMask=move_mask),
            MaxRegionProgress=[dict(Region=k, Value=v) for k, v in placement["progress"].items()],
            Navigation=dict(Width=nav["size"][0], Height=nav["size"][1], Origin=dict(zip(("X", "Y", "Z"), nav["origin"])),
                CharacterId=nav["character"], MemberId=nav["member"], MaxSteps=nav["max_steps"],
                Rows=[dict(Cells=[dict(RawValues=cell) for cell in row]) for row in nav["cells"]],
                AreaMappings=[dict(Cell=cell, Value=value) for cell, value in nav["areas"]]),
            Entities=[dict(Id=r["id"], Region=r["region"], Refresh=r["refresh"], Public=r["public"], Private=r["private"],
                Grid=r["grid"], Direction=r["direction"], Unique=r["unique"], Area=r["area"], EntityId=r["entity"], TypeId=r["type"],
                Stages=[dict(Key=str(k), ValueJson=compact(v)) for k, v in r["stages"].items()]) for r in placement["entities"]],
            EntityComponents=[dict(EntityId=k, Json=compact(v)) for k, v in local.items()], Configs=configs)
        return definition, dict(navigation=nav, placement=placement, entities=local, templates=templates, tables=tables)

    def build(self):
        try:
            destination = ROOT / f"unity/Assets/Official/World/Map/{self.map_id}"
            receipt_path = ROOT / f"assets/reference/official-map/map{self.map_id}-definition-source-manifest.json"
            if (destination / f"map{self.map_id}.json").exists():
                if not receipt_path.is_file():
                    raise ValueError("Existing map definition uses another source generator; reconcile its source before replacing it")
                previous = json.loads(receipt_path.read_bytes())
                for row in previous["outputs"]:
                    if sha((ROOT / row["path"]).read_bytes()) != row["sha256"]:
                        raise ValueError("Preserve changed generated map input: " + row["path"])
                previous_manifest = json.loads((destination / "source-manifest.json").read_bytes())
                if sha((destination / f"map{self.map_id}.json").read_bytes()) != previous_manifest["output"]["sha256"]:
                    raise ValueError("Preserve changed installed map definition")
            definition, decoded = self.decode()
            code = self.checked_code()
            outputs = []
            for name, value in decoded.items():
                path = destination / "Source" / (name + ".json")
                raw = encode(value)
                write(path, raw)
                meta(path)
                outputs.append(dict(path=source_path(path), sha256=sha(raw), bytes=len(raw)))
            for folder in (destination, destination / "Source"):
                meta(folder, folder=True)
            receipt = dict(format=1, manifest=self.apk.version, mapClassId=self.map_id, inputs=self.inputs,
                apk_sha256=self.apk.source.apk_sha256,
                prepared_source=dict(path=source_path(self.prepared_path), sha256=sha(self.prepared_path.read_bytes())),
                original_code=code, templates=self.template_sources, outputs=outputs,
                tools=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in
                    (Path(__file__), ROOT / "scripts/official_map_codec.py", ROOT / "scripts/official_ec_codec.py", ROOT / "scripts/official_tables.py")],
                operation="Project exact selected navigation, MapInfoWrap and full EC placements, recursive original templates/actions, map/world/region/area and entity parameter records. No map registration, server-time substitution, art, HUD or playable completion is asserted.")
            write(receipt_path, encode(receipt))
            inputs = [dict(path=source_path(receipt_path), sha256=sha(receipt_path.read_bytes())), *outputs]
            input_json = compact(inputs)
            definition["SourceManifestSha256"] = sha(input_json.encode())
            raw = encode(definition)
            manifest = dict(format=1, manifest=self.apk.version, mapClassId=self.map_id, inputs=inputs,
                sourceInputsJson=input_json, sourceInputsSha256=sha(input_json.encode()),
                output=dict(path=f"map{self.map_id}.json", size=len(raw), sha256=sha(raw)))
            for path, data in ((destination / f"map{self.map_id}.json", raw), (destination / "source-manifest.json", encode(manifest))):
                write(path, data)
                meta(path)
            print(f"Projected Map{self.map_id}: {len(definition['Entities'])} placements, {len(decoded['templates'])} templates/actions, {len(definition['MaxRegionProgress'])} regions.", flush=True)
        finally:
            self.apk.archive.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--map-id", type=int, required=True)
    MainlineMapSource(parser.parse_args().map_id).build()
