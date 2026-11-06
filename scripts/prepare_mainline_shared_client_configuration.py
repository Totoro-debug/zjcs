"""Bind the original configuration closure used by fresh map client players."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

from official_apk import ROOT, Apk
from prepare_mainline_shared_world_data import meta, record
from validation_workspace import validation_directory

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
    ("config_luban", "stage", "StageInfo"),
    ("config_luban", "stage_entry", "StageEntry"),
    ("config_luban", "stage_monster", "StageMonsters"),
    ("config_luban", "stage_monster_array", "StageMonsterArrayInfo"),
    ("config_ec", "EC/FightStageEntityLiteInfoPack", "FightStageEntityLiteInfoPack"),
)
BINARY = ROOT / "unity/Assets/StreamingAssets/PartnerNpcShared/Config/Binary/level_prop.g.bytes"
BINARY_SHA256 = "1b8e2cf7029ba8a80750f54aa35658878166d1bfbe2e206317473b43f6af3f1f"


def method_body(text, method):
    start = text.index("{", text.index(method))
    end, depth = start + 1, 1
    while depth:
        depth += (text[end] == "{") - (text[end] == "}")
        end += 1
    return re.sub(r"\s+", "", re.sub(r"//[^\n]*", "", text[start:end]))


def verify_player_projection(source, shared, work):
    work.mkdir(parents=True, exist_ok=True)
    subprocess.run([str(ROOT / ".tools/dotnet/dotnet.exe"),
        str(ROOT / ".tools/ilspycmd/tools/net10.0/any/ilspycmd.dll"),
        "-t", "Common.Player", "-o", str(work), str(shared)], check=True)
    old = (work / "Common.Player.decompiled.cs").read_text("utf8")
    current = (source / "Player.cs").read_text("utf8")
    methods = ("public static Player CreateNewPlayer()", "public List<int> GetWorldMapProgresses(",
        "public int GetWorldMapRegionProgress(", "public int GetStoryCount(",
        "public StoryStatus GetStoryStatus(", "public DateTime GetStoryLastPlayTime(",
        "public T GetBlackboardDataValue<T>(", "public int GetStageCount(",
        "public PlayerStageDataWrap GetStageData(", "public bool IsGoddessActive(",
        "public int GetMapActiveEntCount(", "public int GetAllMapActiveEntCount(",
        "public List<int> GetActivedMapEntities(", "private bool IsMapEntityActived(",
        "public void OnUpdateActiveableEntity(")
    for method in methods:
        if method_body(old, method) != method_body(current, method):
            raise ValueError("Retained original player projection method differs: " + method)
    return dict(verified_methods=list(methods), scope="Only the listed original player methods; not whole-version equivalence")


def verify_activation_component(source, shared, work):
    work.mkdir(parents=True, exist_ok=True)
    name = "WorldActiveableEntityComponent"
    subprocess.run([str(ROOT / ".tools/dotnet/dotnet.exe"),
        str(ROOT / ".tools/ilspycmd/tools/net10.0/any/ilspycmd.dll"),
        "-t", "Common." + name, "-o", str(work), str(shared)], check=True)
    old = (work / ("Common." + name + ".decompiled.cs")).read_text("utf8")
    current = (source / (name + ".cs")).read_text("utf8")
    def prefix(text):
        body = method_body(text, "public bool Active")
        return body[:body.index("catch")]
    if prefix(old) != prefix(current):
        raise ValueError("Retained original activation flag/event implementation differs.")
    return dict(type=name, property="Active", before_token="catch", identical=True,
        scope="Original getter, equality guard, assignment, Dirty and OnActived; exception logging is excluded.")


def verify_stage_condition_readers(source, shared, work):
    evidence = []
    segments = {
        "PlayerStageDataWrap": (("public int GetWinCount(", None, "default:"),),
        "TimeManager": (("private long GetAndUpdateEnvTime()", None, None),
            ("private long GetTimeImpl()", "longnum=", None),
            ("public void InternalHandleServerTime(", "TimeSynced=true;", None)),
    }
    for name, methods in segments.items():
        subprocess.run([str(ROOT / ".tools/dotnet/dotnet.exe"),
            str(ROOT / ".tools/ilspycmd/tools/net10.0/any/ilspycmd.dll"),
            "-t", "Common." + name, "-o", str(work), str(shared)], check=True)
        old = (work / ("Common." + name + ".decompiled.cs")).read_text("utf8")
        current = (source / (name + ".cs")).read_text("utf8")
        for method, start, end in methods:
            def segment(text):
                body = method_body(text, method)
                return body[body.index(start) if start else 0:body.index(end) if end else len(body)]
            if segment(old) != segment(current):
                raise ValueError("Retained stage condition calculation differs: " + name + "." + method)
            evidence.append(dict(type=name, method=method, from_token=start, before_token=end, identical=True))
    return dict(segments=evidence,
        scope="Only the listed calculation segments. Logger lowering differs; no full-method or whole-version equivalence claimed.")


def verify_stage_configuration(source, shared, work):
    work.mkdir(parents=True, exist_ok=True)
    names = ("StageInfo", "StageEntry", "StageMonsters", "StageMonsterArrayInfo",
        "FactionInfo", "GameMonsterInfo", "GameArrayMonsterInfo", "GameArrayMonsterPosInfo")
    evidence = []
    def bodies(text, name):
        result = []
        for match in re.finditer(r"public " + name + r"\(ByteBuf\b[^)]*\)\s*(?::\s*base\([^)]*\)\s*)?\{", text):
            start, end, depth = match.end(), match.end(), 1
            while depth:
                depth += (text[end] == "{") - (text[end] == "}")
                end += 1
            result.append(re.sub(r"\s+", "", text[start:end - 1]))
        return result
    for name in names:
        subprocess.run([str(ROOT / ".tools/dotnet/dotnet.exe"),
            str(ROOT / ".tools/ilspycmd/tools/net10.0/any/ilspycmd.dll"),
            "-t", "Common." + name, "-o", str(work), str(shared)], check=True)
        old = bodies((work / ("Common." + name + ".decompiled.cs")).read_text("utf8"), name)
        current = bodies((source / (name + ".cs")).read_text("utf8"), name)
        if not old or old != current:
            raise ValueError("Retained original stage reader differs: " + name)
        evidence.append(dict(type=name, bytebuf_constructors=len(old), identical=True))
    return evidence


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
        prefix = "Config/" if bundle == "config_ec" else "Config/Luban/"
        entries.append(dict(Config=config, OriginalPath=prefix + table + ".g.bytes",
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
    names += ["LevelPropParser", "LevelPropBinaryReader", "RawFileUtility", "Player", "PlayerPropContext",
        "PlayerProfessionData", "PlayerProfessionDataWrap", "PlayerStory", "PlayerStoryDataWrap",
        "BlackboardDataKeyMapLogicEntityEnd", "MapLogicEntityEndByTagCondition",
        "MapWorldActiveableEntityType", "MapEntityWorldStageType", "MapRegionEntityInfoManager", "FormulaHandler"]
    shared = ROOT / "unity/Assets/Official/World/Map/14/SharedRuntime/Console.bytes"
    if record(shared)["sha256"] != "0ac0102db6f31c88da07a2ddec32b1937fe69391347f9770ff854edaa63d0397":
        raise ValueError("Preserve independently pinned original shared IL")
    projection_evidence = verify_player_projection(source, shared, validation_directory() / "player-projection-sources")
    activation_evidence = verify_activation_component(source, shared, validation_directory() / "activation-component-sources")
    stage_evidence = verify_stage_configuration(source, shared, validation_directory() / "stage-configuration-sources")
    condition_evidence = verify_stage_condition_readers(source, shared, validation_directory() / "stage-configuration-sources")
    names += ["FactionInfo", "GameMonsterInfo", "GameArrayMonsterInfo", "GameArrayMonsterPosInfo",
        "PlayerStageDataWrap", "PlayerStageManager", "StageResetType", "TimeManager",
        "WorldActiveableEntityComponent", "MapActiveableEntityInfo"]
    native = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities"
    binding = native / "MainlineSharedClientConfiguration.cs"
    meta(binding)
    validation = ROOT / "unity/Assets/Game/Editor/MainlineWorldStartup"
    receipt = dict(manifest=apk.version,
        operation="Restore original fresh-player tables and reuse the verified local level-property binary. Replace only YooAsset path discovery; retain original ParserBase initialization, binary reader, 1000-entry LRU, default values and calculations.",
        inputs=inputs, binary_input=record(BINARY), binary_provenance=record(binary_proof_path),
        code_inputs=[record(source / (name + ".cs")) for name in names],
        shared_il=dict(record(shared), manifest="871_156598"), player_projection=projection_evidence,
        activation_component_compatibility=activation_evidence,
        stage_reader_compatibility=stage_evidence,
        stage_condition_calculations=condition_evidence,
        bindings=[record(path) for path in (binding, Path(str(binding) + ".meta"), native / "NativeMap14SharedConfiguration.cs")]
            + [record(path) for path in sorted((native.parent / "MainlinePlayerState").iterdir()) if path.is_file()]
            + [record(native.parent / "MainlinePlayerState.meta")],
        validation=[record(path) for path in sorted(validation.iterdir()) if path.is_file()],
        tools=[record(Path(__file__).resolve()), record(ROOT / "scripts/prepare_mainline_world_startup_validation.py")],
        outputs=outputs)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Bound", len(entries), "original player tables and shared property binary", flush=True)


if __name__ == "__main__":
    build()
