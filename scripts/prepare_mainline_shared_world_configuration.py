"""Bind original mainline movement and client visibility tables without rewriting IL."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT, Apk
from prepare_mainline_shared_world_data import meta, record

DESTINATION = ROOT / "unity/Assets/Official/World/Shared/SharedWorldConfiguration"
RECEIPT = ROOT / "assets/reference/official-map/mainline-shared-world-configuration-source-manifest.json"
TABLES = (
    ("config_luban_ec", "EC/entity_grid_mover", "EntityGridMoverInfo"),
    ("config_luban_map", "Map/map_logic_entity_tag", "MapLogicEntityTagInfo"),
    ("config_luban", "map_entity_condition_show", "MapEntityConditionShowInfo"),
)


def build():
    if RECEIPT.exists():
        for output in json.loads(RECEIPT.read_bytes())["outputs"]:
            if record(ROOT / output["path"])["sha256"] != output["sha256"]:
                raise ValueError("Preserve changed world configuration: " + output["path"])
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review selected original world table version")
    inputs, outputs, entries = [], [], []
    folder = DESTINATION / "Resources/MainlineSharedWorld/Configuration"
    folder.mkdir(parents=True, exist_ok=True)
    for bundle, table, config in TABLES:
        obj = apk.asset(bundle, "/" + table + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        digest = hashlib.sha256(raw).hexdigest()
        target = folder / (table.rsplit("/", 1)[-1] + ".g.bytes")
        target.write_bytes(raw)
        meta(target)
        outputs.extend((record(target), record(Path(str(target) + ".meta"))))
        inputs.append(dict(apk.provenance(obj), table=table, payload_sha256=digest))
        entries.append(dict(OriginalPath="Config/Luban/" + table + ".g.bytes",
            Resource="MainlineSharedWorld/Configuration/" + target.name.removesuffix(".bytes"),
            Sha256=digest, Parser=config + "Parser", Config=config))
    catalog = folder / "catalog.json"
    catalog.write_text(json.dumps(dict(Manifest=apk.version, Entries=entries), indent=2) + "\n",
                       encoding="utf8", newline="\n")
    meta(catalog)
    outputs.extend((record(catalog), record(Path(str(catalog) + ".meta"))))
    for directory in (DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())):
        meta(directory)
        outputs.append(record(Path(str(directory) + ".meta")))
    source = ROOT / ".tools/source-packages/current" / apk.version / "official-code/common-source/Common"
    code = [source / (name + ".cs") for name in
            ("EntityGridMoverInfo", "EntityGridMoverInfoParser", "GridMoverComponentInfo",
             "MapLogicEntityTagInfo", "MapLogicEntityTagInfoParser", "MapEntityConditionShowInfo",
             "MapEntityConditionShowInfoParser", "LubanKeyCsvParser", "LubanParserHelper")]
    # The native loader retains its independently pinned historical IL. Record
    # that dependency separately from these current table/source inputs.
    shared = ROOT / "unity/Assets/Official/World/Map/14/SharedRuntime/Console.bytes"
    if record(shared)["sha256"] != "0ac0102db6f31c88da07a2ddec32b1937fe69391347f9770ff854edaa63d0397":
        raise ValueError("Preserve the separately pinned original shared IL")
    integration = [ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities" / name
                   for name in ("MainlineSharedWorld.cs", "MainlineSharedWorldConfiguration.cs")]
    validation = ROOT / "unity/Assets/Game/Editor/MainlineWorldStartup"
    receipt = dict(manifest=apk.version,
        operation="Load unchanged movement, logical tag and conditional visibility Luban tables through the original parsers, phases and PostParse hooks in the public shared world scope.",
        inputs=inputs, code_inputs=[record(path) for path in code],
        shared_il=dict(record(shared), manifest="871_156598"),
        bindings=[record(path) for path in integration],
        validation=[record(path) for path in sorted(validation.iterdir()) if path.is_file()],
        tools=[record(path) for path in (Path(__file__).resolve(),
            ROOT / "scripts/prepare_mainline_world_startup_validation.py",
            ROOT / "scripts/prepare_mainline_minimap_offline_validation.py")], outputs=outputs)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Bound", len(entries), "original mainline world tables", flush=True)


if __name__ == "__main__":
    build()
