"""Bind the original configuration closure used by fresh map client players."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT, Apk
from prepare_mainline_shared_world_data import meta, record

DESTINATION = ROOT / "unity/Assets/Official/World/Shared/SharedClientConfiguration"
RECEIPT = ROOT / "assets/reference/official-map/mainline-shared-client-configuration-source-manifest.json"
TABLES = (
    ("config_luban", "item", "ItemInfo"),
    ("config_luban", "treasure_suit", "TreasureSuitInfo"),
    ("config_luban", "equip_resonance", "EquipResonanceInfo"),
    ("config_luban", "level_exp", "LevelExp"),
    ("config_luban", "prop_cfg", "PropCfgInfo"),
    ("config_luban", "level_prop_default_value", "LevelPropDefaultValue"),
    ("config_luban", "role_prop_group", "RolePropGroupInfo"),
    ("config_luban", "profession_base", "ProfessionBaseInfo"),
    ("config_luban", "skill", "SkillInfo"),
    ("config_luban_astrologicaldestiny", "AstrologicalDestiny/astrological_season_config", "AstrologicalSeasonConfig"),
    ("config_luban", "periodic_card_pay", "PeriodicCardPayInfo"),
    ("config_luban_astrologicaldestiny", "AstrologicalDestiny/astrological_level", "AstrologicalLevelInfo"),
    ("config_luban", "system_level_limit", "SystemLevelLimitInfo"),
)
BINARY = ROOT / "unity/Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes"
BINARY_SHA256 = "1b8e2cf7029ba8a80750f54aa35658878166d1bfbe2e206317473b43f6af3f1f"


def build():
    if RECEIPT.exists():
        for output in json.loads(RECEIPT.read_bytes())["outputs"]:
            if record(ROOT / output["path"])["sha256"] != output["sha256"]:
                raise ValueError("Preserve changed client configuration: " + output["path"])
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review selected original client table version")
    binary_proof_path = ROOT / "assets/reference/partner-npc-runtime-source-manifest.json"
    binary_proof = json.loads(binary_proof_path.read_bytes())
    if (binary_proof["manifest"] != apk.version
            or binary_proof["installed_sha256"][BINARY.relative_to(ROOT).as_posix()] != BINARY_SHA256
            or record(BINARY)["sha256"] != BINARY_SHA256):
        raise ValueError("Original shared property binary provenance differs")
    inputs, outputs, entries = [], [], []
    folder = DESTINATION / "Resources/MainlineSharedWorld/ClientConfiguration"
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
        entries.append(dict(Config=config, OriginalPath="Config/Luban/" + table + ".g.bytes",
            Resource="MainlineSharedWorld/ClientConfiguration/" + target.name.removesuffix(".bytes"), Sha256=digest))
    catalog = folder / "catalog.json"
    catalog.write_text(json.dumps(dict(Manifest=apk.version, Entries=entries,
        BinaryFile=BINARY.relative_to(ROOT / "unity/Assets/StreamingAssets").as_posix(),
        BinarySha256=BINARY_SHA256), indent=2) + "\n", encoding="utf8", newline="\n")
    meta(catalog)
    outputs.extend((record(catalog), record(Path(str(catalog) + ".meta"))))
    for directory in (DESTINATION, *sorted(p for p in DESTINATION.rglob("*") if p.is_dir())):
        meta(directory)
        outputs.append(record(Path(str(directory) + ".meta")))
    source = ROOT / ".tools/source-packages/current" / apk.version / "official-code/common-source/Common"
    names = [config for _, _, config in TABLES] + [config + "Parser" for _, _, config in TABLES]
    names += ["LevelPropParser", "LevelPropBinaryReader", "RawFileUtility", "Player", "PlayerPropContext"]
    shared = ROOT / "unity/Assets/Official/World/Map/14/SharedRuntime/Console.bytes"
    if record(shared)["sha256"] != "0ac0102db6f31c88da07a2ddec32b1937fe69391347f9770ff854edaa63d0397":
        raise ValueError("Preserve independently pinned original shared IL")
    native = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities"
    binding = native / "MainlineSharedClientConfiguration.cs"
    meta(binding)
    validation = ROOT / "unity/Assets/Game/Editor/MainlineWorldStartup"
    receipt = dict(manifest=apk.version,
        operation="Restore original fresh-player tables and reuse the verified local level-property binary. Replace only YooAsset path discovery; retain original ParserBase initialization, binary reader, 1000-entry LRU, default values and calculations.",
        inputs=inputs, binary_input=record(BINARY), binary_provenance=record(binary_proof_path),
        code_inputs=[record(source / (name + ".cs")) for name in names],
        shared_il=dict(record(shared), manifest="871_156598"),
        bindings=[record(path) for path in (binding, Path(str(binding) + ".meta"), native / "NativeMap14SharedConfiguration.cs")],
        validation=[record(path) for path in sorted(validation.iterdir()) if path.is_file()],
        tools=[record(Path(__file__).resolve()), record(ROOT / "scripts/prepare_mainline_world_startup_validation.py")],
        outputs=outputs)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Bound", len(entries), "original player tables and shared property binary", flush=True)


if __name__ == "__main__":
    build()
