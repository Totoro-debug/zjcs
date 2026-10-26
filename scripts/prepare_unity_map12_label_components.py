"""Port the original components used by Map12's world-space region labels.

The support component always supplies null MiniMap/regionPosList. All 99 Map12
regions have no rank/time release limit. Check these scope boundaries explicitly;
the unrelated minimap and timed-region implementations are not replacements.
"""
from pathlib import Path
import json
import re
import struct
import xxhash

from official_apk import ROOT, Apk
from official_source import OfficialSource
from prepare_unity_map12_sources import encode, sha, source_path, write
from prepare_unity_map13_water_components import import_meta

DEST = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeMap12Labels"
NS = "Zjcs.Official.World.NativeMap12Labels"
CORE = "Zjcs.Official.World.NativeLabelUI"
NAMES = ["MapRegionDescSupportUI", "MapRegionDescItemUI", "MapRegionStrengthDisplayUI",
         "MapRegionDescHelper", "MapRegionLimitStatusRefreshHelper",
         "MapRegionLimitStatusRefreshProviderForLevel", "IMapRegionLimitStatusRefreshProvider", "UIColorByLevel"]


def remove_method(text, signature):
    start = text.index(signature)
    brace = text.index("{", start); depth = 1; end = brace + 1
    while depth:
        depth += (text[end] == "{") - (text[end] == "}"); end += 1
    return text[:start] + text[end:]


def build():
    source = OfficialSource()
    code = ROOT / ".tools/source-packages/current" / source.version / "official-code"
    code_manifest_path = code.parent / "code-manifest.json"
    code_manifest = json.loads(code_manifest_path.read_text(encoding="utf8"))
    assemblies = []
    for name in ["Assembly-CSharp.dll", "Console.dll", "Framework.dll", "Framework.FirstPass.Runtime.dll"]:
        p = code / "managed/decoded" / name
        digest = sha(p.read_bytes())
        if digest != code_manifest["assemblies"][name]["decoded_sha256"]:
            raise ValueError("Selected official label assembly differs: " + name)
        assemblies.append(dict(path=source_path(p), sha256=digest))
    tables_path = ROOT / "unity/Assets/Official/World/Map/12/Source/tables.json"
    tables = json.loads(tables_path.read_text(encoding="utf8"))
    regions = [r for r in tables["Map/map_region"] if r["ClassId"] == 12]
    if len(regions) != 99 or any(r[k] for r in regions for k in
            ["LimitServerRank", "LimitServerRankUnlockDayOffset", "ReleaseTimeId", "ReleaseTimeOffset"]):
        raise ValueError("Restore original timed/rank limit providers before expanding the verified Map12 scope")
    support = code / "client-source/MapRegionDescSupportUI.cs"
    if "mMapRegionDescItemUI.Init(MapRootManagerProxy.CurrentMapClassId, num, null, null);" not in support.read_text(encoding="utf8"):
        raise ValueError("Original world-label support now requires minimap services")
    inputs, outputs = [], []
    for name in NAMES:
        original = code / "client-source" / (name + ".cs")
        text = original.read_text(encoding="utf8")
        text = re.sub(r"^\s*//IL_[^\n]*\n", "\n", text, flags=re.M)
        for use in ["Common", "Framework", "Framework.DependencyInjection", "R3"]:
            text = text.replace("using " + use + ";\n", "")
        text = text.replace("\t[Inject]\n", "")
        text = text.replace("MeManager.MainPlayer", "Services.Player")
        text = text.replace(", MapPlanesRankInfo.CreateFromPlanes()", "")
        text = text.replace("new Action<Rank>", "new Action<int>").replace("OnPlanesRankUnlock(Rank rank)", "OnPlanesRankUnlock(int rank)")
        text = text.replace("public GAction<MapRegionLimitType> OnStatusUpdate { get; set; } = new GAction<MapRegionLimitType>();",
            "public Action<MapRegionLimitType> OnStatusUpdate { get; set; } = delegate { };")
        if name == "MapRegionDescSupportUI":
            text = text.replace("private IMapRootManagerProxy MapRootManagerProxy { get; set; }", "")
            text = text.replace("private IMyMapPositionHandlerProxy MyMapPositionHandler { get; set; }", "")
            text = text.replace("Pos2d pos = MyMapPositionHandler.PosToGrid(((Component)this).transform.position);\n\t\tint num = MyMapPositionHandler.QueryRegion(pos);",
                "int num = Services.QueryRegion(((Component)this).transform.position);")
            text = text.replace("MapRootManagerProxy.CurrentMapClassId, num, null, null", "Services.MapClassId, num")
        if name == "MapRegionDescItemUI":
            for declaration in ["\tprivate MiniMap mMinimap;", "\tprivate DisposableBag mDisposableBag;", "\tprivate Transform mLineTransform;", "\tprivate Transform mHighlightTransform;",
                                "\tprivate IResourceLoader ResourceLoader { get; set; }"]:
                text = text.replace(declaration, "")
            text = text.replace("int mapClassId, int region, MiniMap minimap, List<Pos2d> regionPosList", "int mapClassId, int region")
            text = text.replace("\t\tmMinimap = minimap;\n", "")
            text = text.replace("Parsers.Instance.MapRegionInfoParser.Get(mMapClassId, mRegion)", "Services.GetRegion(mMapClassId, mRegion)")
            text = text.replace("MeManager.MainTeam != null && MeManager.MainTeam.TeamLeaderId != Services.Player.Id", "Services.IsTeamMember")
            text = text.replace("Refresh(mRegionCfg, regionPosList)", "Refresh(mRegionCfg)")
            start = text.index("\t\tif ((Object)(object)mMinimap != (Object)null)")
            end = text.index("\n\t}\n", start)
            text = text[:start] + text[end:]
            for signature in ["\tprivate void OnDestroy()", "\tprivate void Minimap_OnShowFullScreen()", "\tprivate void Minimap_OnHideFullScreen()"]:
                text = remove_method(text, signature)
            start = text.index("\tprivate void RefreshDisplayByConfig(")
            text = text[:start] + "}\n"
            text = text.replace("private void Refresh(MapRegionInfo regionInfo, List<Pos2d> regionPosList)", "private void Refresh(MapRegionInfo regionInfo)")
            start = text.index("\t\tif ((Object)(object)mMinimap != (Object)null)")
            end = text.index("\n\t\tif ((Object)(object)mRegionLevelText", start)
            text = text[:start] + "\t\tstring regionNameKey = Services.GetRegionNameKey(regionInfo.ClassId, regionInfo.Region);\n\t\tforeach (MultiLanguageText text in mRegionNameTexts) text.SetKey(regionNameKey);" + text[end:]
            text = text.replace("CultureNumberArg.Compact(regionInfo.CombatRating)", "Services.CompactNumber(regionInfo.CombatRating)")
            text = text.replace("Formula.Instance.CalcWorldMapRegionProgress", "Services.CalcWorldMapRegionProgress")
            text = text.replace("Formula.Int2Float(num)", "((float)num / 10000f)")
        if name == "MapRegionStrengthDisplayUI":
            text = text.replace("public IScriptableObjectConfig<CombatRatingConfig> CombatRatingConfig { get; set; }", "")
            text = text.replace("BattleFormula.Instance.CalcRankSuppression", "Services.CalcRankSuppression")
            text = text.replace("Formula.Instance.GetCombatRatingLevel", "Map12LabelFormula.GetCombatRatingLevel")
        if name == "MapRegionLimitStatusRefreshHelper":
            text = text.replace("((MapRegionLimitStatusRefreshForRankUnlockTime)(mRefreshProvider = new MapRegionLimitStatusRefreshForRankUnlockTime())).Init(mMapRegionInfo, mRemainTimeInfoUI, (Action)RefreshStatus);",
                'throw new InvalidOperationException("Map12 original region tables contain no timed limits.");')
            text = text.replace("new MapRegionLimitStatusRefreshProviderForLevel()", "new MapRegionLimitStatusRefreshProviderForLevel { Services = Services }")
        if name == "UIColorByLevel":
            text = text.replace("public IScriptableObjectConfig<CombatRatingConfig> CombatRatingConfig { get; set; }", "")
            text = text.replace("CombatRatingConfig.Value.TryGetLevelColor", "Services.TryGetLevelColor")
        base = name in ["MapRegionDescSupportUI", "MapRegionStrengthDisplayUI"]
        if not base and name != "IMapRegionLimitStatusRefreshProvider":
            class_declaration = re.search(r"public class " + name + r"[^\n]*\n\{", text)
            if not class_declaration: raise ValueError("Original label class declaration differs: " + name)
            binding = "\n\tpublic IMap12LabelServices Services { get; set; }\n"
            if name == "MapRegionStrengthDisplayUI": raise AssertionError()
            at = class_declaration.end(); text = text[:at] + binding + text[at:]
        if name == "MapRegionDescHelper":
            text = text.replace("\t\tmMapRegionInfo = mapRegionInfo;", "\t\tmLimitStatusRefreshHelper.Services = Services;\n\t\tmMapRegionInfo = mapRegionInfo;")
        if name == "MapRegionStrengthDisplayUI":
            text = text.replace("\t\tmRegionDescHelper.Init(mRegionCfg);", "\t\tmRegionDescHelper.Services = Services;\n\t\tmRegionDescHelper.Init(mRegionCfg);")
        if any(token in text for token in ["MeManager", "Parsers.", "MapPlanesRankInfo", "IResourceLoader", "mMinimap", "regionPosList", "GAction", "[Inject]"]):
            raise ValueError("Unadapted original label dependency: " + name)
        text = re.sub(r"(?m)[ \t]+$", "", text)
        raw = ("// Selected official Map12 world-label implementation; see source receipt.\nnamespace " + NS + "\n{\nusing Object = UnityEngine.Object;\nusing " + CORE + ";\n" + text + "\n}\n").encode()
        target = DEST / (name + ".cs"); write(target, raw); import_meta(target, "MonoImporter")
        inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes()))); outputs.append(target)
    for name in ["MapRegionStrengthDisplayType", "MapRegionLimitType"]:
        original = code / "common-source/Common" / (name + ".cs")
        text = original.read_text(encoding="utf8").replace("namespace Common;", "namespace " + NS + "\n{") + "\n}\n"
        target = DEST / (name + ".cs"); write(target, text.encode()); import_meta(target, "MonoImporter"); outputs.append(target)
        inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes())))
    original = code / "client-source/CombatRateingColorType.cs"
    target = DEST / original.name; write(target, ("namespace " + NS + "\n{\n" + original.read_text(encoding="utf8") + "\n}\n").encode()); import_meta(target, "MonoImporter"); outputs.append(target)
    inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes())))
    for p in DEST.glob("*.cs"):
        if p not in outputs:
            import_meta(p, "MonoImporter"); outputs.append(p)
    asmdef = DEST / (NS + ".asmdef")
    write(asmdef, encode(dict(name=NS, references=["Unity.TextMeshPro", "Unity.ugui", CORE], autoReferenced=True)))
    import_meta(asmdef, "AssemblyDefinitionImporter"); outputs.append(asmdef)
    import_meta(DEST, "DefaultImporter", folder=True)
    adapter_root = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap12Labels"
    adapter_asmdef = adapter_root / "Zjcs.Offline.World.NativeMap12Labels.asmdef"
    write(adapter_asmdef, encode(dict(name="Zjcs.Offline.World.NativeMap12Labels",
        references=["Unity.TextMeshPro", "Unity.ugui", NS, CORE], autoReferenced=True)))
    import_meta(adapter_root, "DefaultImporter", folder=True)
    import_meta(adapter_asmdef, "AssemblyDefinitionImporter")
    adapters = [adapter_asmdef, *sorted(adapter_root.glob("*.cs"))]
    for p in adapter_root.glob("*.cs"): import_meta(p, "MonoImporter")
    # Flatten only existing official table/navigation/UI values for local native loading.
    ui_path = ROOT / "unity/Assets/Game/Resources/Map12/UI/catalog.json"
    navigation_path = ROOT / "unity/Assets/Official/World/Map/12/Source/navigation.json"
    definition_path = ROOT / "unity/Assets/Official/World/Map/12/map12.json"
    ui = json.loads(ui_path.read_text(encoding="utf8")); nav = json.loads(navigation_path.read_text(encoding="utf8"))
    definition = json.loads(definition_path.read_text(encoding="utf8"))
    area_to_region = {a: r["Region"] for r in regions for a in r["Areas"] or []}
    def enum_values(name):
        text = (code / "common-source/Common" / (name + ".cs")).read_text(encoding="utf8")
        names = re.findall(r"(?m)^\t(\w+),?$", text)
        if not names: raise ValueError("Original rank enum scope differs")
        return {name: i for i, name in enumerate(names)}
    subranks, ranks = enum_values("SubRank"), enum_values("Rank")
    enum_path = code / "common-source/Common/EnumExt.cs"
    enum_text = enum_path.read_text(encoding="utf8")
    enum_text = enum_text[enum_text.index("public static Rank ToRank(this SubRank subRank)"):]
    enum_text = enum_text[:enum_text.index("\t\tdefault:")]
    rank_map = {}
    for cases, rank in re.findall(r"((?:\t\tcase SubRank\.\w+:\n)+)\t\t\treturn Rank\.(\w+);", enum_text):
        for subrank in re.findall(r"SubRank\.(\w+):", cases): rank_map[subranks[subrank]] = ranks[rank]
    if set(rank_map) != set(subranks.values()): raise ValueError("Original sub-rank mapping is incomplete")
    for region in regions:
        region["RecommendCombatRatingIndex"] = ui["recommendCombatRatingIndex"]
        region["HasCombatRatingLevelValues"] = region["CombatRatingLevelValues"] is not None
    text_values = {t["key"]: t["value"] for t in ui["texts"]}
    from prepare_unity_map12_native_terrain import EXPORTED, closure, GUID
    _, _, native_index = closure()
    multilingual = next(g for g, p in native_index.items() if p.name == "MultiLanguageText.cs")
    required = set()
    for prefab in (EXPORTED / "Partition/MapGround_12").rglob("*.prefab"):
        text = prefab.read_text(encoding="utf8")
        if multilingual in text:
            required.update(k.strip().strip('"') for k in re.findall(r"(?m)^  myKey: (.+)$", text) if k.strip().strip('"'))
    wanted = {xxhash.xxh64_intdigest(k.encode()): k for k in required - text_values.keys()}
    language_path = ROOT / "assets/reference/official-map/localization-source-manifest.json"
    language_inputs = json.loads(language_path.read_text(encoding="utf8"))["inputs"]
    apk = Apk.current()
    for record in language_inputs.values():
        raw = apk.archive.read(record["source"]["path"])
        if sha(raw) != record["sha256"]: raise ValueError("Original APK language payload differs")
        count = struct.unpack_from("<i", raw)[0]; start = 4 + count * 12
        for i in range(count):
            identity, offset = struct.unpack_from("<Qi", raw, 4 + i * 12)
            if identity not in wanted: continue
            pos = start + offset; size = struct.unpack_from("<H", raw, pos)[0]
            text_values[wanted[identity]] = raw[pos+2:pos+2+size].decode("utf8")
    if required - text_values.keys(): raise ValueError("Original region-label text is unresolved: " + str(required - text_values.keys()))
    data_path = ROOT / "unity/Assets/Official/World/Map/12/Resources/Map12/Native/region-label-data.json"
    write(data_path, encode(dict(manifest=source.version, mapClassId=12, width=nav["size"][0], height=nav["size"][1], origin=nav["origin"],
        regions=regions, regionGrid=[area_to_region.get(c[0], -1) for row in nav["cells"] for c in row],
        maxProgress=[r["Value"] for r in sorted(definition["MaxRegionProgress"], key=lambda r: r["Region"])],
        suppressUnlockRank=ui["suppressUnlockRank"], subRankToRank=[rank_map[i] for i in range(len(rank_map))],
        combatRatingColors=ui["combatRatingColors"], texts=[dict(key=k, value=v) for k, v in sorted(text_values.items())])))
    import_meta(data_path, "TextScriptImporter"); import_meta(data_path.parent, "DefaultImporter", folder=True)
    outputs.append(data_path)
    write(ROOT / "assets/reference/official-map/map12-label-components-code-source-manifest.json", encode(dict(
        format=1, mapClassId=12, manifest=source.version, inputs=inputs, assemblies=assemblies,
        code_manifest=dict(path=source_path(code_manifest_path), sha256=sha(code_manifest_path.read_bytes())),
        language_inputs=language_inputs, localization_manifest=dict(path=source_path(language_path), sha256=sha(language_path.read_bytes())),
        additional_original_inputs=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [
            code / "framework-firstpass-source/UIBase.cs", code / "client-source/MapUIBase.cs", code / "client-source/ShowRemainTimeInfo.cs",
            code / "common-source/Common/MapRegionInfo.cs", code / "common-source/Common/FormulaHandler.cs", code / "common-source/Common/BattleFormulaHandler.cs",
            code / "common-source/Common/GridGroundComponentInfo.cs", code / "common-source/Common/EntityHandler.cs", enum_path,
            code / "common-source/Common/MapRegionProgressManager.cs", code / "common-source/Common/Player.cs", code / "common-source/Common/LanguageUtils.cs",
            code / "common-source/Common/MapRegionStatus.cs", code / "common-source/Common/Rank.cs", code / "common-source/Common/SubRank.cs",
            ui_path, navigation_path, definition_path]],
        scope=dict(tables=source_path(tables_path), tables_sha256=sha(tables_path.read_bytes()), regions=99,
            level_limited_regions=sum(r["LimitLevel"] > 0 for r in regions), time_or_server_rank_limits=0,
            world_label_call="MapRegionDescItemUI.Init(CurrentMapClassId, region, null, null)",
            excluded="MiniMap-only layout/highlight branches and unreachable timed-region provider; reject expanded table scope rather than invent behavior."),
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes()), meta_sha256=sha(Path(str(p)+".meta").read_bytes())) for p in sorted(outputs)],
        adapters=[dict(path=source_path(p), sha256=sha(p.read_bytes()), meta_sha256=sha(Path(str(p)+".meta").read_bytes())) for p in adapters],
        validation=[dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [
            ROOT / "unity/Assets/Game/Editor/Map12/OfficialMap12LabelTools.cs", ROOT / "unity/Assets/Game/Editor/Map12/OfficialMap12LabelTools.Binding.cs",
            ROOT / "unity/Assets/Game/Editor/Map12/OfficialMap12LabelAcceptance.cs", ROOT / "unity/Assets/Game/Editor/Map12/OfficialMap12EnvironmentSceneAcceptance.cs",
            ROOT / "unity/Assets/Game/Editor/Map12/Zjcs.Editor.Map12.asmdef"]],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation="Copy the official reachable world-label methods and serialized fields; replace DI/global player services with explicit pre-activation binding. Preserve original UI initialization order, status branches, progress special case, event subscription/removal and state switcher data.",
        limitations=["Formal local player/save services, normal progression and published Windows Player remain required; complete custom TMP engine equivalence is not established."])))
    print("Map12 original label components:", len(inputs), "official inputs; 99 regions, 16 level limits, no time/rank release limits")


if __name__ == "__main__": build()
