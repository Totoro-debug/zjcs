"""Restore the official world-label call path for all 106 Map13 regions.

The original support passes null minimap/positions. Source tables prove that no
Map13 region has a server-rank/date release limit; expanded scope is rejected.
"""
from pathlib import Path
import json
import re
import subprocess

from official_apk import ROOT, Apk
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_map13_water_components import import_meta

DEST = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeMap13Labels"
NS = "Zjcs.Official.World.NativeMap13Labels"
CORE = "Zjcs.Official.World.NativeLabelUI"
NAMES = ["MapRegionDescSupportUI", "MapRegionDescItemUI", "MapRegionStrengthDisplayUI",
         "MapRegionDescHelper", "MapRegionLimitStatusRefreshHelper",
         "MapRegionLimitStatusRefreshProviderForLevel", "IMapRegionLimitStatusRefreshProvider",
         "UIColorByLevel", "ShowRemainTimeInfo", "MapUIBase", "CombatRatingConfig"]


def extract(text, signature):
    start = text.index(signature)
    brace = text.index("{", start); depth = 1; end = brace + 1
    while depth:
        depth += (text[end] == "{") - (text[end] == "}"); end += 1
    return text[start:end]


def remove(text, signature):
    return text.replace(extract(text, signature), "")


def clean(text):
    text = re.sub(r"^\s*//IL_[^\n]*\n", "\n", text, flags=re.M)
    for namespace in ["Common", "Framework", "Framework.DependencyInjection", "R3", "Framework.Logging",
                      "Framework.Logging.InterpolatedStringHandler", "Microsoft.Extensions.Logging", "GFrameCore.Extensions.ZLogger", "ZLogger"]:
        text = text.replace("using " + namespace + ";\n", "")
    return text.replace("\t[Inject]\n", "").replace("[ScriptableObjectConfig]\n", "")


def build():
    apk = Apk.current(); code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    manifest_path = code.parent / "code-manifest.json"
    manifest = json.loads(manifest_path.read_bytes())
    assemblies = []
    for name in ["Assembly-CSharp.dll", "Console.dll", "Framework.dll", "Framework.FirstPass.Runtime.dll"]:
        p = code / "managed/decoded" / name; digest = sha(p.read_bytes())
        if digest != manifest["assemblies"][name]["decoded_sha256"]: raise ValueError("Original label assembly differs: " + name)
        assemblies.append(dict(path=source_path(p), sha256=digest))
    source_dir = ROOT / "unity/Assets/Official/World/Map/13/Source"
    table_path = source_dir / "tables.json"
    regions = [r for r in json.loads(table_path.read_bytes())["Map/map_region"] if r["ClassId"] == 13]
    limit_fields = ["LimitServerRank", "LimitServerRankUnlockDayOffset", "ReleaseTimeId", "ReleaseTimeOffset"]
    if len(regions) != 106 or any(r[k] for r in regions for k in limit_fields):
        raise ValueError("Migrate original rank/date services before expanding verified Map13 world-label scope")
    support = code / "client-source/MapRegionDescSupportUI.cs"
    if "mMapRegionDescItemUI.Init(MapRootManagerProxy.CurrentMapClassId, num, null, null);" not in support.read_text(encoding="utf8"):
        raise ValueError("Original world label call requires minimap services")
    inputs, outputs = [], []
    def output(name, text, original=None, methods=None):
        target = DEST / (name + ".cs")
        header = "// Migrated official Map13 world-label call path; see source receipt.\nnamespace " + NS + "\n{\nusing Object = UnityEngine.Object;\nusing " + CORE + ";\nusing " + CORE + ".Framework;\n"
        write(target, (header + text + "\n}\n").encode()); import_meta(target, "MonoImporter"); outputs.append(target)
        if original is not None: inputs.append(dict(path=source_path(original), sha256=sha(original.read_bytes()), extracted_methods=methods))
    for name in NAMES:
        original = code / "client-source" / (name + ".cs"); text = clean(original.read_text(encoding="utf8"))
        text = text.replace("MeManager.MainPlayer", "Services.Player").replace(", MapPlanesRankInfo.CreateFromPlanes()", "")
        if name == "MapRegionDescSupportUI":
            text = text.replace("private IMapRootManagerProxy MapRootManagerProxy { get; set; }", "")
            text = text.replace("private IMyMapPositionHandlerProxy MyMapPositionHandler { get; set; }", "")
            text = text.replace("Pos2d pos = MyMapPositionHandler.PosToGrid(((Component)this).transform.position);\n\t\tint num = MyMapPositionHandler.QueryRegion(pos);", "int num = Services.QueryRegion(((Component)this).transform.position);")
            text = text.replace("MapRootManagerProxy.CurrentMapClassId, num, null, null", "Services.MapClassId, num")
        if name == "MapRegionDescItemUI":
            for declaration in ["\tprivate MiniMap mMinimap;", "\tprivate DisposableBag mDisposableBag;", "\tprivate Transform mLineTransform;", "\tprivate Transform mHighlightTransform;", "\tprivate IResourceLoader ResourceLoader { get; set; }"]:
                text = text.replace(declaration, "")
            text = text.replace("int mapClassId, int region, MiniMap minimap, List<Pos2d> regionPosList", "int mapClassId, int region")
            text = text.replace("\t\tmMinimap = minimap;\n", "")
            text = text.replace("Parsers.Instance.MapRegionInfoParser.Get(mMapClassId, mRegion)", "Services.GetRegion(mMapClassId, mRegion)")
            text = text.replace("MeManager.MainTeam != null && MeManager.MainTeam.TeamLeaderId != Services.Player.Id", "Services.IsTeamMember")
            text = text.replace("Refresh(mRegionCfg, regionPosList)", "Refresh(mRegionCfg)")
            start = text.index("\t\tif ((Object)(object)mMinimap != (Object)null)"); end = text.index("\n\t}\n", start)
            text = text[:start] + text[end:]
            for signature in ["\tprivate void OnDestroy()", "\tprivate void Minimap_OnShowFullScreen()", "\tprivate void Minimap_OnHideFullScreen()"]:
                text = remove(text, signature)
            text = text[:text.index("\tprivate void RefreshDisplayByConfig(")] + "}\n"
            text = text.replace("private void Refresh(MapRegionInfo regionInfo, List<Pos2d> regionPosList)", "private void Refresh(MapRegionInfo regionInfo)")
            start = text.index("\t\tif ((Object)(object)mMinimap != (Object)null)"); end = text.index("\n\t\tif ((Object)(object)mRegionLevelText", start)
            text = text[:start] + '\t\tstring regionNameKey = "region_" + regionInfo.ClassId + "_" + regionInfo.Region;\n\t\tforeach (MultiLanguageText nameText in mRegionNameTexts) nameText.SetKey(regionNameKey);' + text[end:]
            text = text.replace("Formula.Instance.CalcWorldMapRegionProgress", "Services.Data.CalcWorldMapRegionProgress").replace("Formula.Int2Float(num)", "((float)num / 10000f)")
        if name == "MapRegionStrengthDisplayUI":
            text = text.replace("public IScriptableObjectConfig<CombatRatingConfig> CombatRatingConfig { get; set; }", "")
            text = text.replace("BattleFormula.Instance.CalcRankSuppression", "Services.Data.CalcRankSuppression").replace("Formula.Instance.GetCombatRatingLevel", "Map13LabelFormula.GetCombatRatingLevel")
            text = text.replace("\t\tmRegionDescHelper.Init(mRegionCfg);", "\t\tmRegionDescHelper.Services = Services;\n\t\tmRegionDescHelper.Init(mRegionCfg);")
        if name == "MapRegionDescHelper":
            text = text.replace("\t\tmMapRegionInfo = mapRegionInfo;", "\t\tmLimitStatusRefreshHelper.Services = Services;\n\t\tmMapRegionInfo = mapRegionInfo;")
        if name == "MapRegionLimitStatusRefreshHelper":
            text = text.replace("((MapRegionLimitStatusRefreshForRankUnlockTime)(mRefreshProvider = new MapRegionLimitStatusRefreshForRankUnlockTime())).Init(mMapRegionInfo, mRemainTimeInfoUI, (Action)RefreshStatus);", 'throw new InvalidOperationException("Verified Map13 source has no server-rank/date region limits.");')
            text = text.replace("new MapRegionLimitStatusRefreshProviderForLevel()", "new MapRegionLimitStatusRefreshProviderForLevel { Services = Services }")
        if name == "UIColorByLevel":
            text = text.replace("public IScriptableObjectConfig<CombatRatingConfig> CombatRatingConfig { get; set; }", "")
            text = text.replace("CombatRatingConfig.Value.TryGetLevelColor", "Services.Colors.TryGetLevelColor")
        if name == "ShowRemainTimeInfo":
            text = text.replace("TimeManager.Instance.GetDateTime()", "Services.Now").replace("TimeManager.Instance.CalcRemainSecond", "Services.CalcRemainSecond")
        if name == "MapUIBase":
            text = text.replace("protected MapComponent mMap;", "protected object mMap;")
            text = text.replace("private IMapRootManagerProxy MapRootManager { get; set; }", "")
            text = text.replace("MapRootManager.Map", "Services.Map").replace("MapRootManager.OnInitialized", "Services.Initialized")
        if name == "CombatRatingConfig":
            text = text.replace("\t\tGLoggerErrorInterpolatedStringHandler message;\n\t\tbool enabled;\n", "")
            text = re.sub(r"\t+message = new GLoggerErrorInterpolatedStringHandler.*?Log.Error\([^\n]+;", '\t\t\tUnityEngine.Debug.LogError("Original combat rating color index or level is absent.");', text, flags=re.S)
            text = re.sub(r"\t+GLoggerErrorInterpolatedStringHandler message = new GLoggerErrorInterpolatedStringHandler.*?Log.Error\([^\n]+;", '\t\t\tUnityEngine.Debug.LogError("Original combat rating color type is absent.");', text, flags=re.S)
        if name not in ["MapRegionDescSupportUI", "MapRegionStrengthDisplayUI", "ShowRemainTimeInfo", "CombatRatingConfig", "IMapRegionLimitStatusRefreshProvider", "MapUIBase"]:
            declaration = re.search(r"public class " + name + r"[^\n]*\n\{", text); at = declaration.end()
            text = text[:at] + "\n\tpublic IMap13LabelServices Services { get; set; }\n" + text[at:]
        if any(token in text for token in ["MeManager", "Parsers.", "MapPlanesRankInfo", "IResourceLoader", "mMinimap", "regionPosList", "[Inject]", "GLogger", "ILogger"]):
            raise ValueError("Unadapted original world label: " + name)
        output(name, text, original)
    original = code / "framework-firstpass-source/UIBase.cs"; text = clean(original.read_text(encoding="utf8"))
    text = text.replace(" : MonoBehaviour, IWindowOperator", " : MonoBehaviour")
    text = text.replace("\tprivate PopupWindow mPopupWindow;", "\tpublic IMap13LabelServices Services { get; set; }")
    text = remove(text, "\tpublic PopupWindow PopupWindow")
    text = text.replace("\tprivate ILogger<UIBase> Logger { get; set; }", "")
    text = re.sub(r"\t+ILogger logger = .*?GZLoggerExtensions.Error\([^\n]+;", "\t\t\tUnityEngine.Debug.LogException(ex);", text, flags=re.S)
    output("UIBase", text, original)
    original = code / "framework-firstpass-source/Framework/GAction.cs"
    text = extract(original.read_text(encoding="utf8"), "public sealed class GAction<T>\n")
    text = re.sub(r"\t+GLoggerErrorInterpolatedStringHandler message = .*?Log.Error\([^\n]+;", "\t\t\t\t\tUnityEngine.Debug.LogException(exception);", text, flags=re.S)
    output("GAction", "using System;\nusing System.Runtime.CompilerServices;\nusing System.Threading;\n" + text, original, ["GAction<T>"])
    original = code / "framework-firstpass-source/MonoDictionary.cs"
    text = clean(original.read_text(encoding="utf8")).replace("Remove(key, ref val)", "Remove(key, out val)").replace("Remove(key, ref value)", "Remove(key, out value)")
    output("MonoDictionary", text, original)
    cecil = ROOT / ".tools/map13-ilspy/tools/net10.0/any/Mono.Cecil.dll"
    assembly = code / "managed/decoded/Framework.FirstPass.Runtime.dll"
    command = "$ErrorActionPreference='Stop'\nAdd-Type -Path '" + cecil.as_posix() + "'\n$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly('" + assembly.as_posix() + "')\n"
    command += "try { $t=$a.MainModule.Types | Where-Object FullName -eq 'MonoDictionary`2'; @($t.Methods | Where-Object Name -eq 'Remove' | ForEach-Object { @{signature=$_.FullName;parameters=@($_.Parameters | ForEach-Object { @{name=$_.Name;isOut=$_.IsOut;type=$_.ParameterType.FullName} });instructions=@($_.Body.Instructions | ForEach-Object ToString)} }) | ConvertTo-Json -Depth 8 -Compress } finally {$a.Dispose()}"
    il = json.loads(subprocess.check_output(["pwsh", "-NoProfile", "-Command", command]))
    single = next(r for r in il if len(r["parameters"]) == 1)
    byref = next(r for r in il if len(r["parameters"]) == 2)
    if len([i for i in single["instructions"] if "callvirt" in i]) != 2 or not byref["parameters"][1]["isOut"]:
        raise ValueError("Original MonoDictionary remove call order or out parameter differs")
    il_target = source_dir / "native-label-dictionary-il.json"
    write(il_target, encode(dict(tool=dict(path=source_path(cecil),sha256=sha(cecil.read_bytes())),methods=sorted(il,key=lambda r:r["signature"]))))
    import_meta(il_target,"TextScriptImporter"); outputs.append(il_target)
    for folder, names in [("common-source/Common", ["MapRegionStrengthDisplayType", "MapRegionLimitType", "MapRegionStatus", "Rank", "SubRank"]), ("client-source", ["CombatRateingColorType"])]:
        for name in names:
            original = code / folder / (name + ".cs")
            output(name, original.read_text(encoding="utf8").replace("namespace Common;", ""), original)
    # Pure original formulas and rank switch. No approximation/table interpolation.
    formula = code / "common-source/Common/FormulaHandler.cs"; battle = code / "common-source/Common/BattleFormulaHandler.cs"
    enums = code / "common-source/Common/EnumExt.cs"
    formula_text = extract(formula.read_text(encoding="utf8"), "public int GetCombatRatingLevel(").replace("public int", "public static int", 1)
    rank_text = extract(enums.read_text(encoding="utf8"), "public static Rank ToRank(this SubRank subRank)")
    rank_text = re.sub(r"\t+GLoggerErrorInterpolatedStringHandler message = .*?Log.Error\([^\n]+;", '\t\t\tUnityEngine.Debug.LogError("Original sub-rank has no rank mapping: " + subRank);', rank_text, flags=re.S)
    output("Map13LabelFormula", "using System.Collections.Generic;\npublic static class Map13LabelFormula\n{\n" + formula_text + "\n" + rank_text + "\n}", formula, ["GetCombatRatingLevel"])
    inputs.extend(dict(path=source_path(p), sha256=sha(p.read_bytes())) for p in [battle, enums, code / "common-source/Common/MapRegionInfo.cs", code / "common-source/Common/LanguageUtils.cs"])
    for name in ["Map13LabelServices", "Map13LabelData", "MapRegionInfo"]:
        p = DEST / (name + ".cs"); import_meta(p, "MonoImporter"); outputs.append(p)
    asm = DEST / (NS + ".asmdef")
    write(asm, encode(dict(name=NS, references=["Unity.TextMeshPro", "Unity.ugui", CORE], autoReferenced=True)))
    import_meta(asm, "AssemblyDefinitionImporter"); import_meta(DEST, "DefaultImporter", folder=True); outputs.append(asm)
    game = apk.asset("config_luban", "/game_settings.g.bytes")
    constructors = Constructors(code)
    settings = constructors.table(game.read().m_Script.encode("utf8", "surrogateescape"), "TbGameSettings")
    settings = settings[0] if isinstance(settings, list) else settings
    rating = apk.asset("scriptableconfig_modules_combatrating", "/CombatRatingConfig.asset")
    colors = ScriptSchemas(code_root=code).read(rating)
    navpath = source_dir / "navigation.json"; placepath = source_dir / "placement.json"
    nav = json.loads(navpath.read_bytes()); place = json.loads(placepath.read_bytes())
    area = {a: r["Region"] for r in regions for a in r["Areas"] or []}
    for region in regions:
        region["RecommendCombatRatingIndex"] = settings["RecommendCombatRatingIndex"]
        region["HasCombatRatingLevelValues"] = region["CombatRatingLevelValues"] is not None
    data = dict(manifest=apk.version, mapClassId=13, width=nav["size"][0], height=nav["size"][1], origin=nav["origin"],
                regionGrid=[area.get(c[0], -1) for row in nav["cells"] for c in row], regions=regions,
                maxProgress=[place["progress"].get(str(i), 0) for i in range(106)], suppressUnlockRank=settings["SuppressUnlockRank"])
    data_target = ROOT / "unity/Assets/Official/World/Map/13/Resources/Map13/Native/region-label-data.json"
    color_target = source_dir / "native-label-combat-colors.json"
    write(data_target, encode(data)); import_meta(data_target, "TextScriptImporter"); outputs.append(data_target)
    write(color_target, encode(colors)); import_meta(color_target, "TextScriptImporter"); outputs.append(color_target)
    for p in [table_path, navpath, placepath,
              code / "common-source/Common/GridGroundComponentInfo.cs", code / "common-source/Common/EntityHandler.cs",
              code / "common-source/Common/MapRegionProgressManager.cs", code / "common-source/Common/Formula.cs"]:
        if not isinstance(p, Path): continue
        inputs.append(dict(path=source_path(p), sha256=sha(p.read_bytes())))
    for name in constructors.sources:
        for folder in ["common-source/Common", "common-source/Common.LubanGen"]:
            p = code / folder / (name + ".cs")
            if p.is_file(): inputs.append(dict(path=source_path(p), sha256=sha(p.read_bytes()))); break
    write(ROOT / "assets/reference/official-map/map13-label-components-code-source-manifest.json", encode(dict(
        format=1, mapClassId=13, manifest=apk.version, assemblies=assemblies, inputs=inputs,
        native_config_inputs=[dict(bundle=bundle, path_id=str(obj.path_id), source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data())) for bundle, obj in [("config_luban",game),("scriptableconfig_modules_combatrating",rating)]],
        scope=dict(regions=106, level_limited_regions=sum(r["LimitLevel"]>0 for r in regions), time_or_server_rank_limits=0,
                   world_call="Init(CurrentMapClassId, region, null, null)",
                   excluded="Minimap-only branches/window ancestor API and unreachable server/date limit provider; reject changed scope."),
        outputs=[dict(path=source_path(p), sha256=sha(p.read_bytes()), meta_sha256=sha(Path(str(p)+".meta").read_bytes())) for p in sorted(outputs)],
        generator=dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        adapters=[dict(path=source_path(p),sha256=sha(p.read_bytes())) for p in sorted((ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13Labels").glob("*.cs"))],
        operation="Port original reachable world-label components and initialization/event order. Explicit services bind original player/map/clock/config inputs before activation. Keep original GAction<T> subscription/version semantics, original MonoDictionary first-key behavior, official culture arguments, state switches, rank switch and formulas. Serialize original decoded regions/navigation/progress/settings and full combat-color config; no chosen replacement colors or layouts.",
        limitations=["This is the original world-label code/data scope. Native prefab binding and actual Play Mode verification are recorded separately; formal world/player state and map gameplay remain unbound."])))
    apk.archive.close(); print("Map13 world-label components:",len(outputs),"outputs;106 regions/16 level limits")


if __name__ == "__main__": build()
