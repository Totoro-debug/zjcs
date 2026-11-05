"""Restore the selected client map tables and their original EC dependencies."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

from official_apk import ROOT, Apk
from official_map_codec import unpack
from official_tables import Constructors
from prepare_mainline_shared_world_data import meta, record
from validation_workspace import validation_directory

DEST = ROOT / "unity/Assets/Official/World/Shared/SharedMapConfiguration"
SOURCE = ROOT / "unity/Assets/Game/Runtime/Official/World/SelectedMapConfiguration"
RECEIPT = ROOT / "assets/reference/official-map/mainline-shared-map-configuration-source-manifest.json"
TABLES = (
    ("config_luban", "equip_inlay_site", "EquipInlaySiteInfo"),
    ("config_luban", "function_limit", "FunctionLimitInfo"),
    ("config_luban", "conditions", "ConditionInfo"),
    ("config_luban_map", "Map/map", "MapInfo"),
    ("config_luban_map", "Map/map_world", "MapWorldInfo"),
    ("config_luban_map", "Map/map_region", "MapRegionInfo"),
    ("config_luban_map", "Map/entity_extend_parameter", "EntityExtendParameterInfo"),
)
UNITY = Path("D:/Tool/Unity/Editor/Data")


def normalized(text):
    return re.sub(r"\s+", "", re.sub(r"//[^\n]*", "", text))


def constructors(text, name):
    result = []
    for match in re.finditer(r"public " + name + r"\(ByteBuf [^)]*\)\s*: base\(_buf\)\s*\{", text):
        start, end, depth = match.end(), match.end(), 1
        while depth:
            depth += (text[end] == "{") - (text[end] == "}")
            end += 1
        result.append(text[start:end - 1])
    return result


def verify_client_map_identity(common, shared, work):
    def body(text, name):
        start = text.index("{", text.index(name))
        end, depth = start + 1, 1
        while depth:
            depth += (text[end] == "{") - (text[end] == "}")
            end += 1
        return normalized(text[start:end])

    for name in ("Common.PlayerMap", "MsgMapStart"):
        subprocess.run([str(ROOT / ".tools/dotnet/dotnet.exe"),
            str(ROOT / ".tools/ilspycmd/tools/net10.0/any/ilspycmd.dll"),
            "-t", name, "-o", str(work), str(shared / "Console.bytes")], check=True)
    old = (work / "Common.PlayerMap.decompiled.cs").read_text("utf8")
    current = (common / "PlayerMap.cs").read_text("utf8")
    methods = (
        "public void SetConatainerStatus(", "public bool TryGetContainerIdByStatus(",
        "public bool TryGetStatusByContainerId(", "public EcEntityIdentifier GetEntIdByLoEntId(int",
        "public EcEntityIdentifier GetEntIdByLoEntId(long", "public bool IsMixMap(",
        "public int GetMapLineIndex()", "public void SetGroupMapIdOrderList(",
    )
    for name in methods:
        if body(old, name) != body(current, name):
            raise ValueError("Retained original map identity method differs: " + name)
    old_message = (work / "MsgMapStart.decompiled.cs").read_text("utf8")
    current_message = (common.parent / "MsgMapStart.cs").read_text("utf8")
    if normalized(old_message.split("mapData.EnsureContainer(", 1)[1]) != normalized(current_message.split("mapData.EnsureContainer(", 1)[1]):
        raise ValueError("Retained original map enter identity branch differs")
    return dict(player_map_methods=list(methods), map_enter_identity_branch_verified=True)


def build():
    if RECEIPT.exists():
        for output in json.loads(RECEIPT.read_bytes())["outputs"]:
            if record(ROOT / output["path"])["sha256"] != output["sha256"]:
                raise ValueError("Preserve changed selected map configuration: " + output["path"])
    work = validation_directory() / "selected-map-configuration"
    work.mkdir(parents=True, exist_ok=True)
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review selected original map configuration version")
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    common = code / "common-source/Common"
    shared = ROOT / "unity/Assets/Official/World/Map/14/SharedRuntime"
    if record(shared / "Console.bytes")["sha256"] != "0ac0102db6f31c88da07a2ddec32b1937fe69391347f9770ff854edaa63d0397":
        raise ValueError("The independently retained shared IL differs")
    identity_evidence = verify_client_map_identity(common, shared, work)

    # Verify the exact inherited constructor prefixes against the delivered IL,
    # rather than assuming that the selected and historical schemas agree.
    additions = {
        "MapWorldInfo": ("SupportFastClear = _buf.ReadBool();", "EnterNavigateKey = _buf.ReadString();"),
        "EntityExtendParameterInfo": ("IgnoreFastClear = _buf.ReadBool();",),
    }
    evidence = []
    for name, tail in additions.items():
        subprocess.run([str(ROOT / ".tools/dotnet/dotnet.exe"),
            str(ROOT / ".tools/ilspycmd/tools/net10.0/any/ilspycmd.dll"),
            "-t", "Common." + name, "-o", str(work), str(shared / "Console.bytes")], check=True)
        old = (work / ("Common." + name + ".decompiled.cs")).read_text("utf8")
        current = (common / (name + ".cs")).read_text("utf8")
        old_bodies = constructors(old, name) if name == "MapWorldInfo" else []
        current_bodies = constructors(current, name) if name == "MapWorldInfo" else []
        if name == "EntityExtendParameterInfo":
            # This generated bean has no explicit base-constructor call.
            old_bodies = constructors(old.replace(")\n\t{", ")\n        : base(_buf)\n\t{"), name)
            current_bodies = constructors(current.replace(")\n\t{", ")\n        : base(_buf)\n\t{"), name)
        if not old_bodies or len(old_bodies) != len(current_bodies):
            raise ValueError("Original constructor overloads differ: " + name)
        for old_body, current_body in zip(old_bodies, current_bodies):
            for assignment in tail:
                if current_body.count(assignment) != 1:
                    raise ValueError("Review appended original field: " + assignment)
                current_body = current_body.replace(assignment, "")
            if normalized(old_body) != normalized(current_body):
                raise ValueError("Original constructor prefix differs: " + name)
        evidence.append(dict(type=name, inherited_constructor_prefix=True, appended_fields=list(tail)))

    subprocess.run([str(ROOT / ".tools/dotnet/dotnet.exe"),
        str(ROOT / ".tools/ilspycmd/tools/net10.0/any/ilspycmd.dll"),
        "-t", "Luban.ByteBuf", "-o", str(work), str(shared / "Luban.bytes")], check=True)
    old_reader = (work / "Luban.ByteBuf.decompiled.cs").read_text("utf8")
    reader_source = code / "luban-source/Luban/ByteBuf.cs"
    current_reader = reader_source.read_text("utf8")
    def string_reader(text):
        return text.split("public string ReadString()", 1)[1].split("public string ReadNonNullString()", 1)[0]
    if normalized(string_reader(old_reader)) != normalized(string_reader(current_reader)):
        raise ValueError("Original nullable string reader differs")

    # This source compiles separately against the real dynamically loaded ABI.
    # Unity imports it as source but does not compile these names into its own
    # assemblies; the resulting verified payload is loaded by the map scope.
    source = """#if ZJCS_SELECTED_MAP_CONFIGURATION
using System.Collections.Generic;
using Common;
using Luban;
namespace Zjcs.Mainline.SelectedConfiguration {
public sealed class SelectedMapWorldInfo : MapWorldInfo {
 public readonly bool SupportFastClear;
 public readonly string EnterNavigateKey;
 public SelectedMapWorldInfo(ByteBuf buf) : base(buf) { SupportFastClear=buf.ReadBool(); EnterNavigateKey=buf.ReadString(); }
 public SelectedMapWorldInfo(ByteBuf buf, Dictionary<int,List<int>>[] rewards, HashSet<int>[] treasures) : base(buf,rewards,treasures) { SupportFastClear=buf.ReadBool(); EnterNavigateKey=buf.ReadString(); }
}
public sealed class SelectedEntityExtendParameterInfo : EntityExtendParameterInfo {
 public bool IgnoreFastClear;
 public SelectedEntityExtendParameterInfo(ByteBuf buf) : base(buf) { IgnoreFastClear=buf.ReadBool(); }
}
public sealed class SelectedMapWorldInfoParser : MapWorldInfoParser {
 protected override IReadOnlyDictionary<int,MapWorldInfo> LoadLubanTable(ByteBuf buf) => new SelectedTbMapWorldInfo(buf).DataMap;
}
public sealed class SelectedEntityExtendParameterInfoParser : EntityExtendParameterInfoParser {
 protected override IReadOnlyDictionary<int,EntityExtendParameterInfo> LoadLubanTable(ByteBuf buf) => new SelectedTbEntityExtendParameterInfo(buf).DataMap;
}
"""
    for name in additions:
        text = (common / ("Tb" + name + ".cs")).read_text("utf8")
        text = text.replace("namespace Common;", "").replace("Tb" + name, "SelectedTb" + name)
        text = text.replace(name + ".Deserialize" + name + "(_buf, array, array2)", "new Selected" + name + "(_buf, array, array2)")
        text = text.replace(name + ".Deserialize" + name + "(_buf)", "new Selected" + name + "(_buf)")
        text = re.sub(r"^using .*;\n", "", text, flags=re.M)
        source += "\n" + text
    source += "\n}\n#endif\n"
    SOURCE.mkdir(parents=True, exist_ok=True)
    source_path = SOURCE / "SelectedMapConfiguration.cs"
    source_path.write_text(source, encoding="utf8", newline="\n")
    meta(source_path)
    references = [UNITY / "MonoBleedingEdge/lib/mono/4.8-api" / name for name in
        ("mscorlib.dll", "System.dll", "System.Core.dll", "Facades/netstandard.dll")]
    references += [shared / "Console.bytes", shared / "Luban.bytes", ROOT / "unity/Library/ScriptAssemblies/Config.dll"]
    compiled = work / "Zjcs.Mainline.SelectedConfiguration.dll"
    response = work / "compile.rsp"
    response.write_text("\n".join(["-nostdlib", "-target:library", "-langversion:latest", "-deterministic", "-optimize",
        "-define:ZJCS_SELECTED_MAP_CONFIGURATION", '-out:"' + str(compiled) + '"',
        *('-r:"' + str(path) + '"' for path in references), '"' + str(source_path) + '"']), encoding="utf8")
    subprocess.run([str(UNITY / "NetCoreRuntime/dotnet.exe"), str(UNITY / "DotNetSdkRoslyn/csc.dll"),
        "-noconfig", "@" + str(response)], check=True)

    inputs, outputs, entries = [], [], []
    folder = DEST / "Resources/MainlineSharedWorld/MapConfiguration"
    folder.mkdir(parents=True, exist_ok=True)
    raw_tables = {}
    for bundle, table, config in TABLES:
        obj = apk.asset(bundle, "/" + table + ".g.bytes")
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        raw_tables[config] = raw
        target = folder / (table.rsplit("/", 1)[-1] + ".g.bytes")
        target.write_bytes(raw)
        meta(target)
        outputs.extend((record(target), record(Path(str(target) + ".meta"))))
        inputs.append(dict(apk.provenance(obj), payload_sha256=hashlib.sha256(raw).hexdigest()))
        entries.append(dict(Config=config, OriginalPath="Config/Luban/" + table + ".g.bytes",
            Resource="MainlineSharedWorld/MapConfiguration/" + target.name.removesuffix(".bytes"),
            Sha256=hashlib.sha256(raw).hexdigest()))

    # Include every original template queried by the unchanged PostParse, even
    # when its filename belongs to another feature. No entity from these files
    # is spawned merely because its configuration is needed here.
    constructors_reader = Constructors(code)
    rows = constructors_reader.table(raw_tables["EntityExtendParameterInfo"], "TbEntityExtendParameterInfo")
    required = {row["EntityClassId"] for row in rows} | {row["InteractionActionId"] for row in rows if row["InteractionActionId"] > 0}
    candidates = {}
    for address, pointer in apk.container("config_ec"):
        if not address.endswith(".g.bytes"):
            continue
        obj = pointer.deref()
        raw = obj.read().m_Script.encode("utf8", "surrogateescape")
        legacy = False
        try:
            records = unpack(raw)[0]
        except (ValueError, TypeError, IndexError):
            try:
                records, legacy = unpack(raw[2:])[0], True
            except (ValueError, TypeError, IndexError):
                continue
        if not isinstance(records, list):
            continue
        for row in records:
            if not isinstance(row, list) or len(row) != 3:
                continue
            parent, identity, components = row
            if parent or identity not in required:
                continue
            previous = candidates.get(identity)
            if previous and previous[1] == legacy and previous[2] != components:
                raise ValueError("Conflicting original EC template: " + str(identity))
            if previous is None or (previous[1] and not legacy):
                candidates[identity] = (address, legacy, components, obj, raw)
    if required - candidates.keys():
        raise ValueError("Missing original metadata templates: " + str(sorted(required - candidates.keys())))
    installed = json.loads((ROOT / "unity/Assets/Official/World/Shared/SharedWorldDataCurrent/Resources/MainlineSharedWorld/10/catalog.json").read_bytes())
    installed_paths = {row["OriginalPath"]: row["Sha256"] for row in installed["Entries"]}
    extra = []
    for address, legacy, components, obj, raw in sorted({row[0]: row for row in candidates.values()}.values(), key=lambda row: row[0]):
        original = address.removeprefix("Assets/")
        digest = hashlib.sha256(raw).hexdigest()
        if original in installed_paths:
            if installed_paths[original] != digest:
                raise ValueError("Existing official EC record differs: " + original)
            continue
        target = folder / "EC" / Path(original).name
        target.parent.mkdir(exist_ok=True)
        target.write_bytes(raw)
        meta(target)
        inputs.append(dict(apk.provenance(obj), payload_sha256=digest))
        outputs.extend((record(target), record(Path(str(target) + ".meta"))))
        extra.append(dict(OriginalPath=original, Resource="MainlineSharedWorld/MapConfiguration/EC/" + target.name.removesuffix(".bytes"), Sha256=digest))
    for name, payload in (
        ("entities.json", dict(MapClassId=0, Entries=extra)),
        # The general Python table decoder represents zero-length strings as
        # ''. Both verified original ReadString implementations return null.
        ("expected.json", dict(World=[dict(ClassId=row["ClassId"], SupportFastClear=row["SupportFastClear"],
            EnterNavigateKey=row["EnterNavigateKey"], EnterNavigateKeyIsNull=not row["EnterNavigateKey"])
            for row in constructors_reader.table(raw_tables["MapWorldInfo"], "TbMapWorldInfo")],
            Entities=[{key: row[key] for key in ("EntityClassId", "IgnoreFastClear")} for row in rows])),
    ):
        target = folder / name
        target.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
        meta(target)
        outputs.extend((record(target), record(Path(str(target) + ".meta"))))
    assembly = folder / "selected-readers.bytes"
    assembly.write_bytes(compiled.read_bytes())
    meta(assembly)
    outputs.extend((record(assembly), record(Path(str(assembly) + ".meta"))))
    catalog = folder / "catalog.json"
    catalog.write_text(json.dumps(dict(Manifest=apk.version, Entries=entries,
        AssemblyResource="MainlineSharedWorld/MapConfiguration/selected-readers",
        AssemblySha256=record(assembly)["sha256"], EntityCatalog="MainlineSharedWorld/MapConfiguration/entities"), indent=2) + "\n", encoding="utf8", newline="\n")
    meta(catalog)
    outputs.extend((record(catalog), record(Path(str(catalog) + ".meta"))))
    for directory in (DEST, *sorted(path for path in DEST.rglob("*") if path.is_dir()), SOURCE):
        meta(directory)
        outputs.append(record(Path(str(directory) + ".meta")))
    outputs.extend((record(source_path), record(Path(str(source_path) + ".meta"))))
    runtime = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities"
    new_binding = runtime / "MainlineSharedMapConfiguration.cs"
    meta(new_binding)
    acceptance = ROOT / "unity/Assets/Game/Editor/MainlineWorldStartup/OfficialMainlineWorldStartupAcceptance.ClientMaps.cs"
    meta(acceptance)
    names = [name for _, _, name in TABLES] + [name + "Parser" for _, _, name in TABLES]
    names += ["TbMapWorldInfo", "TbEntityExtendParameterInfo", "MapPartInfoBase", "MapInfoWrapParser", "MapInfoManager", "MapRegionEntityInfoManager", "LoEntityCampComponentInfo", "EntityCampComponent", "PlayerMap", "MapLoEntityIdManager", "PublicMapLineStatus"]
    receipt = dict(manifest=apk.version, shared_il=dict(record(shared / "Console.bytes"), manifest="871_156598"),
        operation="Keep original payloads and shared IL. Derive original DTOs with verified appended selected fields, reuse selected table readers and original parser/PostParse/query implementations. Include complete original EC dependency records.",
        constructor_compatibility=evidence, client_map_identity=identity_evidence, inputs=inputs,
        code_inputs=[record(common / (name + ".cs")) for name in names] + [record(reader_source), record(common.parent / "MsgMapStart.cs")],
        shared_reader=record(shared / "Luban.bytes"), nullable_string_reader_verified=True,
        config_abi_sources=[record(path) for path in sorted((ROOT / "unity/Assets/Game/Runtime/Official/SharedConfig").rglob("*.cs"))],
        bindings=[record(path) for path in (new_binding, Path(str(new_binding) + ".meta"), runtime / "MainlineSharedWorld.cs", runtime / "NativeMap14SharedData.cs", runtime / "MainlineSharedEntityCreation.cs", runtime / "MainlineSharedClientContext.cs", runtime / "MainlineSharedClientContext.cs.meta")],
        validation=[record(path) for path in sorted(acceptance.parent.iterdir()) if path.is_file()],
        tools=[record(Path(__file__).resolve()), dict(path=str(UNITY / "DotNetSdkRoslyn/csc.dll"),
            sha256=hashlib.sha256((UNITY / "DotNetSdkRoslyn/csc.dll").read_bytes()).hexdigest())],
        outputs=outputs, required_template_ids=sorted(required), additional_ec_records=len(extra))
    # Tool paths can be external; record their absolute path without pretending
    # that compiler binaries are repository assets.
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf8", newline="\n")
    print("Bound", len(entries), "map tables and", len(extra), "original EC dependency records", flush=True)


if __name__ == "__main__":
    build()
