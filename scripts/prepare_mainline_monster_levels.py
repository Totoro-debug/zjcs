"""Extract placed monster level bindings from the selected official source."""
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import entities, unpack
from official_tables import Constructors
from prepare_unity_map13_story_runtime import json_meta, source_record, write_json
from prepare_unity_map10_entities import sha256

OUTPUT = ROOT / "unity/Assets/Game/Resources/World/Mainline/monster-levels.json"
RECEIPT = ROOT / "assets/reference/official-map/mainline-monster-levels-source-manifest.json"


def build():
    apk = Apk()
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    formats, constructors = Formats(code), Constructors(code)
    sources = {}

    def raw(bundle, suffix):
        obj = apk.asset(bundle, suffix)
        data = obj.read().m_Script.encode("utf8", "surrogateescape")
        sources[suffix] = dict(apk.provenance(obj), sha256=sha256(data))
        return data

    parameters = constructors.table(raw("config_luban_map", "/entity_extend_parameter.g.bytes"),
                                    "TbEntityExtendParameterInfo")
    regions = constructors.table(raw("config_luban_map", "/map_region.g.bytes"), "TbMapRegionInfo")
    parameter_by_id = {row["EntityClassId"]: row for row in parameters}
    region_by_id = {(row["ClassId"], row["Region"]): row for row in regions}
    records, missing = [], []
    for map_id in (10, 11, 12, 13, 14, 16):
        placed = entities(raw("config_mapinfowrap", f"/mapinfowrap_{map_id}.g.bytes"))["entities"]
        local = {identity: components for parent, identity, components in
                 unpack(raw("config_ec", f"/map_{map_id}.g.bytes"))[0]}
        for entity in placed:
            if entity["type"] not in (101, 102, 103, 104):
                continue
            parameter = parameter_by_id[entity["entity"]]
            region = region_by_id[(map_id, entity["region"])]
            levels = [formats.component(typ.removeprefix("Common."), data)
                      for typ, data in local[entity["id"]] if typ.endswith("LoLevelComponent")]
            if not levels:
                missing.append(dict(MapClassId=map_id, PlacementId=entity["id"], EntityClassId=entity["entity"]))
                continue
            if len(levels) != 1:
                raise ValueError("Duplicate LoLevelComponent for " + str(entity["id"]))
            level = levels[0]
            records.append(dict(MapClassId=map_id, PlacementId=entity["id"], EntityClassId=entity["entity"],
                Region=entity["region"], Level=level["Level"], SubRank=level["SubRank"], Absolute=level["Absolute"],
                RegionLevel=region["Level"], RegionSubRank=region["SubRank"],
                PrivateLevelType=parameter["LevelTypeInPrivateMap"], PublicLevelType=parameter["LevelTypeInPublicMap"]))
    if len({(row["MapClassId"], row["PlacementId"]) for row in records}) != len(records):
        raise ValueError("Duplicate map monster placement")
    data = dict(manifest=apk.version, monsters=records, missing=missing)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf8", newline="\n")
    json_meta(OUTPUT)
    code_paths = ("common-source/Common/WorldEntityLevelComponent.cs", "common-source/Common/LoLevelComponentInfo.cs",
                  "common-source/Common/EntityExtendParameterInfo.cs")
    receipt = dict(format=1, manifest=apk.version, operation="Extract placed monster LoLevel and public/private level types.",
        officialSource=source_record(ROOT / "official-source.json"), inputs=sources,
        officialCode=[source_record(code / path) for path in code_paths],
        formatters=[source_record(path) for path in sorted(set(formats.sources.values()))],
        tools=[source_record(Path(__file__))], outputs=[source_record(OUTPUT), source_record(Path(str(OUTPUT) + ".meta"))],
        count=len(records), missing=missing)
    write_json(RECEIPT, receipt)
    print("Monster level bindings:", len(records), "missing:", missing)


if __name__ == "__main__":
    build()
