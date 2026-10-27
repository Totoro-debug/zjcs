"""Bind current Map13 data and copy its original AStar implementation.

This does not register an unfinished map in the mainline menu. All data comes
from the verified selected-version decode, never the legacy Map10 projection.
"""
from pathlib import Path
import json

from official_apk import ROOT
from prepare_unity_map13_sources import DESTINATION, RECEIPT, encode, meta, sha, source_path, write
from prepare_unity_map13_water_components import import_meta


OUTPUT = ROOT / "unity/Assets/Official/World/Map/13"
CODE = ROOT / "unity/Assets/Game/Runtime/Official/World/Map13Navigation"
PROOF = ROOT / "assets/reference/official-map/map13-navigation-source-manifest.json"
NAMESPACE = "Zjcs.Official.World.Map13Navigation"
ASTAR = ("AStarUtils", "FindPathArgContext", "IAStarMovableHandler",
         "IAStarMoveCustomContext", "DefaultAStarMoveCustomContext")


def build():
    receipt = json.loads(RECEIPT.read_bytes())
    if receipt["manifest"] != "871_156598" or receipt["mapClassId"] != 13:
        raise ValueError("Wrong Map13 source version")
    inputs = []
    for row in receipt["outputs"]:
        path = ROOT / row["path"]
        if sha(path.read_bytes()) != row["sha256"]:
            raise ValueError("Changed Map13 source: " + str(path))
        inputs.append(dict(path=row["path"], sha256=row["sha256"]))
    code_manifest = ROOT / receipt["code_manifest"]["path"]
    if sha(code_manifest.read_bytes()) != receipt["code_manifest"]["sha256"]:
        raise ValueError("Changed current official code manifest")
    code = code_manifest.parent / "official-code"
    assembly = code / "managed/decoded/Console.dll"
    if sha(assembly.read_bytes()) != json.loads(code_manifest.read_bytes())["assemblies"]["Console.dll"]["decoded_sha256"]:
        raise ValueError("Changed Console.dll")

    tables = json.loads((DESTINATION / "tables.json").read_bytes())
    nav = json.loads((DESTINATION / "navigation.json").read_bytes())
    placement = json.loads((DESTINATION / "placement.json").read_bytes())
    components = json.loads((DESTINATION / "entities.json").read_bytes())
    templates = json.loads((DESTINATION / "templates.json").read_bytes())
    water_path = OUTPUT / "Resources/Map13/water-rules.json"
    water_proof_path = ROOT / "assets/reference/official-map/map13-water-source-manifest.json"
    water_proof = json.loads(water_proof_path.read_bytes())
    water_record, = [r for r in water_proof["outputs"] if r["path"] == source_path(water_path)]
    if sha(water_path.read_bytes()) != water_record["sha256"]:
        raise ValueError("Changed original water projection")
    inputs.extend([dict(path=source_path(water_path), sha256=sha(water_path.read_bytes())),
                   dict(path=source_path(water_proof_path), sha256=sha(water_proof_path.read_bytes()))])
    for rule in json.loads(water_path.read_bytes())["Rules"]:
        transform = components[str(rule["PlacementId"])]["LoGridTransformComponent"]
        body = templates[str(rule["EntityClassId"])]["GridTransformComponent"]
        if transform["Dir"] != 0 or transform["FlipX"] or body["RealCenter"] != [0, 0] or body["BodyCrossBlockedInfos"]:
            raise ValueError("Water navigation requires the original unrotated/no-cross-block scope")
    if nav["id"] != 13 or nav["size"] != [240, 240] or len(placement["entities"]) != 2690:
        raise ValueError("Unexpected original Map13 scope")
    if {str(row["id"]) for row in placement["entities"]} != set(components):
        raise ValueError("Map13 placement/component closure differs")
    ground_types = {row["Id"]: row["GroundTypes"] for row in tables["EC/entity_grid_mover"]}

    def mover(identity):
        data = templates[str(identity)]["GridMoverComponent"]
        walk = ground_types[data["WalkableGroundTypeKey"]]
        override = ground_types.get(data["WalkableGroundTypeServerOverrideKey"], [])
        if any(not 0 <= value < 32 for value in walk + override):
            raise ValueError("Ground type outside original bit mask")
        mask = 0
        for value in walk:
            mask |= 1 << value
        return dict(CharacterClassId=identity, **data, WalkableGroundTypes=walk,
                    ServerOverrideGroundTypes=override, MoveMask=mask)

    map_info, = [row for row in tables["Map/map"] if row["ClassId"] == 13]
    classes = {row["entity"] for row in placement["entities"]}
    selections = [
        ("TbMapInfo", "Map/map", lambda r: r["ClassId"] == 13),
        ("TbMapRegionInfo", "Map/map_region", lambda r: r["ClassId"] == 13),
        ("TbMapAreaInfo", "Map/map_area", lambda r: r["ClassId"] == 13),
        ("TbMapWorldInfo", "Map/map_world", lambda r: r["ClassId"] == 13),
        ("TbMapLogicEntityTag", "Map/map_logic_entity_tag", lambda r: r["MapClass"] == 13),
        ("TbEntityExtendParameterInfo", "Map/entity_extend_parameter", lambda r: r["EntityClassId"] in classes),
        ("TbEntityMapDisplayInfo", "Map/entity_map_display", lambda r: r["EntityClassId"] in classes and r["MapClassId"] in (0, 13)),
        ("TbEntityMapUiDisplayInfo", "Map/entity_map_ui_display", lambda r: r["EntityClassId"] in classes and r["MapClassId"] in (0, 13)),
        ("TbMapEntityStageAction", "Map/map_entity_stage_action", lambda r: r["EntityClassId"] in classes),
        ("TbMapRegionBgmInfo", "Map/map_region_bgm", lambda r: r["MapClassId"] == 13),
        ("TbMapToStageInfo", "map_stage", lambda r: r["MapClassId"] == 13),
    ]
    regions = [r for r in tables["Map/map_region"] if r["ClassId"] == 13]
    condition_ids = {r["AccessibleConditionId"] for r in regions if r["AccessibleConditionId"] > 0}
    selections.append(("TbConditionInfo", "conditions", lambda r: r["ClassId"] in condition_ids))
    configs = []
    for target, table, predicate in selections:
        source = receipt["inputs"]["/" + table + ".g.bytes"]
        for row in tables[table]:
            if not predicate(row):
                continue
            if table in ("Map/entity_map_display", "Map/entity_map_ui_display"):
                # These official tables are keyed by multiple selectors, not
                # merely MapClassId/EntityClassId. Preserve every original row.
                selectors = ["EntityClassId", "GroupId", "MapClassId", "AreaTag"]
                if table.endswith("entity_map_display"):
                    selectors.append("GroundType")
                selectors.append("Tag")
                key = json.dumps([row[k] for k in selectors], ensure_ascii=False, separators=(",", ":"))
            elif table in ("Map/map_region", "Map/map_area"):
                key = str(row["ClassId"]) + ":" + str(row["Region" if table.endswith("region") else "Area"])
            elif table == "Map/map_region_bgm":
                key = str(row["MapClassId"]) + ":" + str(row["Region"])
            elif table == "map_stage":
                key = json.dumps([row["MapClassId"], row["AreaTag"], row["StageId"]], ensure_ascii=False, separators=(",", ":"))
            elif "EntityClassId" in row:
                key = (str(row["MapClassId"]) + ":" if "MapClassId" in row else "") + str(row["EntityClassId"])
            else:
                key = next(str(row[k]) for k in ("ClassId", "Region", "LogicId", "MapClassId", "StageId") if k in row)
            raw = json.dumps(row, ensure_ascii=False, separators=(",", ":"), sort_keys=True)
            configs.append(dict(TableName=target, RecordId=key, Json=raw,
                                Sha256=sha(raw.encode()), SourceBundle=source["bundle"], SourcePathId=source["path_id"]))
    inputs.append(dict(path=source_path(RECEIPT), sha256=sha(RECEIPT.read_bytes())))
    input_json = json.dumps(inputs, ensure_ascii=False, separators=(",", ":"), sort_keys=True)
    digest = sha(input_json.encode())
    definition = dict(
        ClassId=13, MapGroupId=map_info["MapGroupId"], MapTagId=map_info["MapTagId"],
        MapBgmId=map_info["MapBgmId"], NextMapClassId=map_info["NextMapClassId"],
        OriginalGroundPrefab="Assets/Partition/MapGround_13/MapGround_13_bare.prefab",
        OriginalMiniMapPrefab="Assets/Prefabs/Map/Map_13/MiniMap/MiniMap_13.prefab",
        SourceManifest="Assets/Official/World/Map/13/source-manifest.json", SourceManifestSha256=digest,
        CharacterMover=mover(nav["character"]),
        MaxRegionProgress=[dict(Region=int(k), Value=v) for k, v in sorted(placement["progress"].items(), key=lambda p: int(p[0]))],
        Navigation=dict(Width=nav["size"][0], Height=nav["size"][1],
            Origin=dict(zip(("X", "Y", "Z"), nav["origin"])), CharacterId=nav["character"], MemberId=nav["member"],
            Rows=[dict(Cells=[dict(RawValues=cell) for cell in row]) for row in nav["cells"]],
            AreaMappings=[dict(Cell=cell, Value=value) for cell, value in nav["areas"]], MaxSteps=nav["max_steps"]),
        Entities=[dict(Id=r["id"], Region=r["region"], Refresh=r["refresh"], Public=r["public"], Private=r["private"],
            Grid=r["grid"], Direction=r["direction"], Unique=r["unique"], Area=r["area"], EntityId=r["entity"], TypeId=r["type"],
            Stages=[dict(Key=str(k), ValueJson=json.dumps(v, ensure_ascii=False, separators=(",", ":")))
                    for k, v in r["stages"].items()]) for r in placement["entities"]],
        EntityComponents=[dict(EntityId=int(k), Json=json.dumps(v, ensure_ascii=False, separators=(",", ":")))
                          for k, v in components.items()], Configs=configs)
    if len(regions) != 106 or len(definition["MaxRegionProgress"]) != 106:
        raise ValueError("Map13 region progress scope differs")
    outputs = []

    def output(path, raw, importer=None):
        write(path, raw)
        if importer:
            import_meta(path, importer)
        else:
            meta(path)
        outputs.append(dict(path=source_path(path), sha256=sha(raw)))

    data = encode(definition)
    output(OUTPUT / "map13.json", data)
    output(OUTPUT / "source-manifest.json", encode(dict(format=1, mapClassId=13, manifest="871_156598",
        sourceInputsJson=input_json, sourceInputsSha256=digest, inputs=inputs,
        output=dict(path="map13.json", size=len(data), sha256=sha(data)))))
    output(OUTPUT / "Resources/Map13/navigation-movers.json", encode(dict(Manifest="871_156598", MapClassId=13,
        Movers=[mover(int(k)) for k, v in templates.items() if "GridMoverComponent" in v])))
    CODE.mkdir(parents=True, exist_ok=True)
    source_files = []
    for name in ASTAR:
        path = code / "common-source/AStar" / (name + ".cs")
        original = path.read_text(encoding="utf8")
        imported = original.replace("using Common;", "using Zjcs.Official.World.NativeTerrain.Common;")
        imported = imported.replace("namespace AStar;", "namespace " + NAMESPACE + "\n{") + "\n}\n"
        output(CODE / path.name, imported.encode(), "MonoImporter")
        source_files.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    output(CODE / (NAMESPACE + ".asmdef"), encode(dict(name=NAMESPACE,
        references=["Zjcs.Official.World.NativeTerrain"], autoReferenced=True)), "AssemblyDefinitionImporter")
    import_meta(CODE, "DefaultImporter", folder=True)
    for directory, importer in (
        (ROOT / "unity/Assets/Game/Runtime/Offline/World/Map13Navigation", "MonoImporter"),
        (ROOT / "unity/Assets/Game/Editor/Map13Navigation", "MonoImporter"),
    ):
        import_meta(directory, "DefaultImporter", folder=True)
        for path in sorted(directory.iterdir()):
            if path.suffix not in (".cs", ".asmdef"):
                continue
            import_meta(path, "AssemblyDefinitionImporter" if path.suffix == ".asmdef" else importer)
            outputs.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    for name in ("Pos2d", "GridGroundComponent", "GridGroundComponentInfo", "GridMoverComponent", "GamePosData", "WorldConveyanceComponent"):
        path = code / "common-source/Common" / (name + ".cs")
        source_files.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    terrain_proof = ROOT / "assets/reference/official-map/map13-native-terrain-code-source-manifest.json"
    for row in json.loads(terrain_proof.read_bytes())["outputs"]:
        if row["path"].endswith("/Common/Pos2d.cs") and sha((ROOT / row["path"]).read_bytes()) != row["sha256"]:
            raise ValueError("The original migrated Pos2d was changed")
    write(PROOF, encode(dict(format=1, mapClassId=13, manifest="871_156598",
        operation="Current map navigation and entity/config binding; original AStar code migration",
        inputs=inputs, original_code=source_files, outputs=outputs,
        assembly=dict(path=source_path(assembly), sha256=sha(assembly.read_bytes())),
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        adaptation="AStar: namespace/import changes only; original Pos2d reused from verified terrain migration. Map data: structural projection only, no legacy-map inputs.",
        limitations=["No mainline menu registration, player visuals, terminal payment/reward transaction or map save binding.",
                     "Navigation ground integration covers original water bodies and explicit host-supplied occupancy; the host must supply other spawned entity state."])))
    print(f"Map13 navigation: {len(data)} bytes, 57600 cells, 2690 entities, {len(configs)} config records, five original AStar types")


if __name__ == "__main__":
    build()
