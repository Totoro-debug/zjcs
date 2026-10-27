"""Recover the original profession tip-dot aggregation core without network services."""
import hashlib
import json
from pathlib import Path
import re
import uuid

from official_apk import ROOT

SOURCE = ROOT / ".tools/source-packages/official-code/client-source"
DEST = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
NAMES = ("TipDotNodeBase", "TipDotConditionNodeBase", "TipDotNodePresetData", "TipDotNodeKey",
         "TipDotStateInfo", "ETipDotLevelType", "INodeInstanceInfo", "ITipDotBanManager", "TipDotCommonNode")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def log_adapter(source):
    return re.sub(r"GLoggerErrorInterpolatedStringHandler message =.*?Log.Error\([^;]*;",
                  "UnityEngine.Debug.LogException(exception);", source, flags=re.S)


def write(name, source):
    path = DEST / (name + ".cs")
    if "using System;" in source and "using UnityEngine;" in source:
        source = "using Object = UnityEngine.Object;\n" + source
    source = "// Ported from the verified official source; only dependency/serialization adapters differ.\n" + source
    source = "\n".join(line.rstrip(" \t") for line in source.splitlines()) + "\n"
    path.write_text(source, encoding="utf-8")
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        identity = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs/" + path.relative_to(ROOT).as_posix()).hex
        meta.write_text("fileFormatVersion: 2\nguid: " + identity + "\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path), meta_sha256=sha(meta))


def wrap(source):
    lines = source.splitlines()
    imports = [line for line in lines if line.startswith("using ") and
        not any(value in line for value in ("Common;", "Framework", "GFrameCore", "MessagePack", "MessagePipe"))]
    body = "\n".join(line for line in lines if not line.startswith("using "))
    return "\n".join(imports) + "\n\nnamespace Zjcs.Official.ProfessionUI\n{\n" + body.strip() + "\n}\n"


def graph_ports():
    """Native graph reading only; the game's graph authoring editor is outside the UI."""
    inputs, outputs = [], []
    def source(name, folder=SOURCE):
        path = folder / (name + ".cs")
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
        return path.read_text(encoding="utf-8")
    def method(text, signature):
        start = text.index(signature)
        brace = text.index("{", start)
        depth = 1
        end = brace + 1
        while depth:
            depth += (text[end] == "{") - (text[end] == "}")
            end += 1
        return text[start:end]
    node = source("Node", SOURCE / "XNode")
    port = source("NodePort", SOURCE / "XNode")
    source("NodeGraph", SOURCE / "XNode")
    dictionary = method(node, "public class NodePortDictionary")
    # Unity 2022 supports the original using declaration lifetime.
    native = """using System;
using System.Collections.Generic;
using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
    // Original native node fields and dictionary serialization. Only graph reading
    // is consumed by profession UI; no graph-editing or story/server execution API.
    public abstract class ProfessionGraphAssetBase : ScriptableObject
    {
        [SerializeField] public List<ProfessionGraphAssetNode> nodes = new List<ProfessionGraphAssetNode>();
    }
    public abstract class ProfessionGraphAssetNode : ScriptableObject
    {
        public enum ConnectionType { Multiple, Override }
        public enum TypeConstraint { None, Inherited, Strict, InheritedInverse, InheritedAny }
        public enum FlowConnectionType { None, Branch, Main }
        [Serializable]
        DICTIONARY
        [SerializeField] public ProfessionGraphAssetBase graph;
        [SerializeField] public Vector2 position;
        [SerializeField] protected NodePortDictionary ports = new NodePortDictionary();
        [SerializeField] public int NodeId = -1;
        [SerializeField] public string Mono = "";
        [SerializeField] public bool IsMonoShow;
        public ProfessionGraphPort GetPort(string fieldName)
        {
            if (ports.TryGetValue(fieldName, out var value)) return value;
            return null;
        }
        public virtual object GetValue(ProfessionGraphPort port) { return null; }
    }
}
""".replace("DICTIONARY", dictionary)
    native = native.replace("NodePort>", "ProfessionGraphPort>").replace("NodePort ", "ProfessionGraphPort ")
    outputs.append(write("ProfessionGraphAssetNode", native))
    body = port[port.index("public class NodePort"):port.index("[JsonIgnore]\n\tpublic int ConnectionCount")]
    body += "\npublic ProfessionGraphAssetNode node => _node;\npublic string fieldName => _fieldName;\n"
    body += method(port, "public List<NodePort> GetConnections()") + "\n" + method(port, "public NodePort GetConnection(int i)") + "\n}"
    body = re.sub(r"^\s*\[Json(?:Ignore|Required)\]\s*$", "", body, flags=re.M)
    body = re.sub(r"\bNodePort\b", "ProfessionGraphPort", body)
    body = re.sub(r"\bNode\b", "ProfessionGraphAssetNode", body)
    outputs.append(write("ProfessionGraphPort", "using System;\nusing System.Collections.Generic;\nusing UnityEngine;\nnamespace Zjcs.Official.ProfessionUI\n{\n[Serializable]\n" + body + "\n}\n"))
    for name in ("BaseNodeInstanceInfo", "ProfessionNodeInstanceInfo", "TipDotGraphNode", "TipDotGraphConditionNode", "TipDotGraphNodeSelector"):
        body = source(name)
        body = body.replace("using XNode;", "")
        body = re.sub(r"^\s*\[(?:CreateNodeMenu|NodeWidth|Input|Output|Header)[^\n]*\]\s*$", "", body, flags=re.M)
        body = re.sub(r"\bNodePort\b", "ProfessionGraphPort", body)
        body = re.sub(r"\bNode\b", "ProfessionGraphAssetNode", body)
        body = re.sub(r"GLoggerErrorInterpolatedStringHandler message =.*?Log.Error\([^;]*;", 'UnityEngine.Debug.LogError("Original profession graph binding is invalid.");', body, flags=re.S)
        body = body.replace('assembly.GetName().Name == "Assembly-CSharp"', 'assembly == typeof(TipDotConditionNodeBase).Assembly')
        body = body.replace(" : IFillable<TipDotGraphNodeSelector>, ICloneable<TipDotGraphNodeSelector>", "")
        body = body.replace("ClonerResolver.Instance.GetClonerWithVerify<TipDotGraphNode>().Fill(ref GraphNode, obj.GraphNode);", "GraphNode = obj.GraphNode;")
        if name == "ProfessionNodeInstanceInfo":
            body = body.replace("public Profession Profession;", "public int Profession;").replace("(Profession profession)", "(int profession)").replace("HashCode.Combine<Profession>", "HashCode.Combine<int>")
        outputs.append(write(name, wrap(body)))
    for name in ("TipDotGraphType", "TipDotGraphScope"):
        body = source(name, ROOT / ".tools/source-packages/official-code/common-source/Common")
        outputs.append(write(name, wrap(body.replace("namespace Common;", ""))))
    source("TipDotNodeGraph")
    outputs.append(write("TipDotNodeGraph", """using System;
using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
    // Original runtime graph payload. Node ID generation/copy/removal belong to
    // the graph authoring editor and are not performed while reading native data.
    public sealed class TipDotNodeGraph : ProfessionGraphAssetBase
    {
        public TipDotGraphType Type;
        public TipDotGraphScope Scope;
        [NonSerialized] public WeakReference<INodeInstanceInfo> RuntimeShowNodeInstanceInfo =
            new WeakReference<INodeInstanceInfo>(BaseNodeInstanceInfo.kDefault);
    }
}
"""))
    source("EnumExtensions")
    outputs.append(write("TipDotNodeKeyExtensions", """namespace Zjcs.Official.ProfessionUI
{
    public static class TipDotNodeKeyExtensions
    {
        public static TipDotNodeKey SetInstanceInfo(this TipDotNodeKey key, INodeInstanceInfo info)
        {
            return new TipDotNodeKey(key.GraphName, key.NodeName, info);
        }
    }
}
"""))
    return inputs, outputs


def ui_ports():
    inputs, outputs = [], []
    for name in ("TipDotNodeLinker", "TipDotShowImageController"):
        path = SOURCE / (name + ".cs")
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
        body = path.read_text(encoding="utf-8")
        body = re.sub(r"^\s*\[(?:Inject|Header|Tooltip)[^\n]*\]\s*$", "", body, flags=re.M)
        body = re.sub(r"GLoggerErrorInterpolatedStringHandler message =.*?Log.Error\([^;]*;", 'UnityEngine.Debug.LogError("Original profession tip-dot graph configuration is missing.");', body, flags=re.S)
        body = re.sub(r"GLoggerWarnInterpolatedStringHandler message =.*?Log.Warn\([^;]*;", 'UnityEngine.Debug.LogWarning("Original profession tip-dot content binding is missing.");', body, flags=re.S)
        body = body.replace("StateSwitcher<", "Zjcs.Official.WorldUI.WorldStateSwitcher<")
        body = body.replace("MultiLanguageText", "Zjcs.Official.WorldUI.WorldMultiLanguageText")
        body = body.replace("private ITipDotSystemController Controller", "public IProfessionTipDotSystemController Controller")
        body = body.replace("private TipDotSystem TipDotSystem", "public IProfessionTipDotSystem TipDotSystem")
        body = body.replace("TipDotSystem tipDotSystem = TipDotSystem;", "IProfessionTipDotSystem tipDotSystem = TipDotSystem;")
        body = body.replace("TipDotSystem.sTempLimitShowTip", "TipDotSystem.TempLimitShowTip")
        body = body.replace("TipDotInfo tipDotInfo = Parsers.Instance.TipDotInfoParser.Get(tipDotNodeKey.GraphName);", "TipDotGraphType? tipDotInfo = TipDotSystem.GetGraphType(tipDotNodeKey.GraphName);")
        body = body.replace("tipDotInfo.GraphType == TipDotGraphType.Static", "tipDotInfo.Value == TipDotGraphType.Static")
        if any(x in body for x in ("Parsers.", "GLogger", "Log.", "GetFullName", "[Inject]")):
            raise ValueError("Unresolved tip-dot UI dependency: " + name)
        outputs.append(write(name, wrap(body)))
    original = SOURCE / "ITipDotSystemController.cs"
    inputs.append(dict(path=original.relative_to(ROOT).as_posix(), sha256=sha(original)))
    original = SOURCE / "TipDotSystem.cs"
    inputs.append(dict(path=original.relative_to(ROOT).as_posix(), sha256=sha(original)))
    outputs.append(write("IProfessionTipDotSystem", """using System;
using UnityEngine;
namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionTipDotSystem
    {
        bool IsShowTipDot { get; }
        bool TempLimitShowTip { get; }
        Action OnTipDotShowChanged { get; set; }
        SafeAction OnTipDotSystemInited { get; set; }
        SafeAction OnTipDotSystemPreClear { get; set; }
        TipDotGraphType? GetGraphType(string graphName);
    }
    public interface IProfessionTipDotSystemController
    {
        bool LinkNodeStateChange(TipDotNodeKey key, GameObject linker, Action<TipDotStateInfo> callback);
        bool UnlinkNodeStateChange(TipDotNodeKey key, GameObject linker, Action<TipDotStateInfo> callback);
    }
}
"""))
    return inputs, outputs


def condition_ports():
    inputs, outputs = [], []
    names = ("TipDotAnyProfessionPromotionCondtionNode", "TipDotFreedomProfessioningConditionDot",
             "TipDotProfessionPromotionCondtionNode", "TipDotProfessionCanUpgradeConditionNode", "TipDotAchievementConditionNode", "AchievementTypeAndParamItem")
    for name in names:
        path = SOURCE / (name + ".cs")
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
        body = path.read_text(encoding="utf-8")
        body = re.sub(r"^\s*\[(?:MenuPath|SearchableEnum|Tooltip)[^\n]*\]\s*$", "", body, flags=re.M)
        body = body.replace("using NaughtyAttributes;", "")
        body = body.replace(", IFillable<TipDotNodeBase>", "")
        body = body.replace(" : ICloneable<AchievementTypeAndParamItem>, IFillable<AchievementTypeAndParamItem>", "")
        if "[Serializable]" not in body:
            body = body.replace("public class " + name, "[Serializable]\npublic class " + name, 1)
        if "using System;" not in body:
            body = "using System;\n" + body
        body = body.replace("CloneableHelper.ThrowException(source == null, GetType().Name);", "throw new ArgumentException(GetType().Name, nameof(source));")
        body = body.replace("Player mainPlayer = MeManager.MainPlayer;", "IProfessionTipDotPlayer mainPlayer = Player;")
        body = body.replace("MeManager.MainPlayer", "Player")
        body = body.replace("Player.PlayerChangeProfessionData.StartChangeProfession", "Player.StartChangeProfession")
        body = body.replace("Profession.None", "0")
        body = re.sub(r"(?<!\.)\bProfession(?=\s+\w|>)", "int", body)
        body = body.replace("PlayerTaskDataWrap", "object").replace("PlayerItemDataWrap", "ProfessionTipDotItem")
        body = body.replace("AwardSourceData", "object")
        body = body.replace("ItemType.Badge", "316").replace("ItemType.ActiveSkill", "100").replace("ItemType.PassiveSkill", "101")
        body = body.replace("FunctionType.Profession", "39")
        body = body.replace("ProfessionUtils.", "Rules.")
        body = body.replace("Parsers.Instance.ProfessionBaseInfoParser.GetAllProfession()", "Rules.GetAllProfessions()")
        body = body.replace("ProfessionLevelCostInfoParser professionLevelCostInfoParser = Parsers.Instance.ProfessionLevelCostInfoParser;", "IProfessionTipDotRules professionLevelCostInfoParser = Rules;")
        body = body.replace("ProfessionBaseInfo professionBaseInfo = Parsers.Instance.ProfessionBaseInfoParser.Get(pro);", "ProfessionTipDotBaseInfo professionBaseInfo = Rules.GetProfessionBaseInfo(pro);")
        body = body.replace("ClonerResolver.Instance.GetClonerWithVerify<List<int>>().Fill(ref Professions, obj.Professions);", "Professions = obj.Professions == null ? null : new List<int>(obj.Professions);")
        body = body.replace("AchievementDataKey", "ProfessionTipDotAchievementKey")
        body = re.sub(r"(?<!\.)\bAchievementType(?=\s+\w)", "int", body)
        body = body.replace("Parsers.Instance.AchievementGroupInfoParser.GetAllAchievementGroupIds()", "Rules.GetAllAchievementGroupIds()")
        body = body.replace("AchievementRawInfoParser achievementRawInfoParser = Parsers.Instance.AchievementRawInfoParser;", "IProfessionTipDotRules achievementRawInfoParser = Rules;")
        body = body.replace("Parsers.GameSettings.AchievementTypeListForShowAllInfosInSameType", "Rules.AchievementTypeListForShowAllInfosInSameType")
        body = body.replace("ProfessionTipDotAchievementKey.Create(data.AchievementType, data.JsonParam)", "Rules.CreateAchievementDataKey(data.AchievementType, data.JsonParam)")
        body = body.replace("Parsers.Instance.FunctionLimitInfoParser.IsLimit(Player, FunctionType.AchievementGroup)", "Player.IsFunctionLimit(136)")
        body = body.replace("GetProfessionTipDotAchievementKeysBySameType", "GetAchievementDataKeysBySameType")
        body = body.replace("GetProfessionTipDotAchievementKey", "GetAchievementDataKey")
        # The original condition is registered only when present; the provider
        # preserves that null check and the same registration lifetime.
        body = re.sub(r"ICondition condition = Parsers.Instance.FunctionLimitInfoParser.Get\(FunctionType.AchievementGroup\)\?\.Condition;\s*if \(condition != null\)\s*\{\s*ConditionHandler.Instance.Register\(condition, SetDirty\);\s*\}", "Rules.RegisterAchievementGroupLimitCondition(SetDirty);", body)
        body = re.sub(r"if \(ConditionHandler.Instance != null\)\s*\{\s*ICondition condition = Parsers.Instance.FunctionLimitInfoParser.Get\(FunctionType.AchievementGroup\)\?\.Condition;\s*if \(condition != null\)\s*\{\s*ConditionHandler.Instance.Deregister\(condition, SetDirty\);\s*\}\s*\}", "Rules.DeregisterAchievementGroupLimitCondition(SetDirty);", body)
        body = body.replace("ClonerResolver.Instance.GetClonerWithVerify<List<AchievementTypeAndParamItem>>().Fill(ref AchievementTypeAndParamList, obj.AchievementTypeAndParamList);", "AchievementTypeAndParamList = obj.AchievementTypeAndParamList == null ? null : obj.AchievementTypeAndParamList.ConvertAll(value => value == null ? null : value.Clone());")
        if any(x in body for x in ("Parsers.", "ClonerResolver", "CloneableHelper", "MeManager", "ConditionHandler.", "FunctionType.", "ProfessionUtils.")):
            raise ValueError("Unresolved original profession condition dependency: " + name)
        outputs.append(write(name, wrap(body)))
    outputs.append(write("IProfessionTipDotRules", """using System;
using System.Collections.Generic;
namespace Zjcs.Official.ProfessionUI
{
    // Read-only projections of the original parser/player operations used by
    // these nodes. Local providers must preserve the original event payloads.
    public sealed class ProfessionTipDotBaseInfo { public int Profession, BadgeClassId; }
    public sealed class ProfessionTipDotLevelCost { public Dictionary<int, long> UpgradeCost; }
    public sealed class ProfessionTipDotItemInfo { public int ItemType; }
    public sealed class ProfessionTipDotItem { public ProfessionTipDotItemInfo Info; }
    public sealed class ProfessionTipDotAchievementKey { public int AchievementType, GroupId; }
    public interface IProfessionTipDotPlayer
    {
        int Profession { get; }
        bool StartChangeProfession { get; }
        event Action<int> OnProfessionPromote;
        event Action<int, int> OnProfessionChange;
        event Action<int, int> OnProfessionUpgrade;
        event Action<object> OnTaskAdd;
        event Action<ProfessionTipDotItem, long, object> OnItemCountUpdate;
        event Action<int, int> OnSkillRankRewardUpdate;
        event Action<ProfessionTipDotItem, int, int> OnSkillRankUp;
        event Action<Dictionary<int, int>, Dictionary<int, int>> OnSkillRankUpBatch;
        event Action<int> OnRecordAlreadyGotAchievementRewardByGroupId;
        event Action OnRecordAlreadyGotAchievementRewardBatch;
        event Action<ProfessionTipDotAchievementKey> OnUpdateAchievementData;
        bool IsProfessionUnlocked(int profession);
        int GetProfessionLevel(int profession);
        long GetItemAmount(int itemId);
        bool IsFunctionLimit(int function);
        bool IsAnyCanGetAchievementReward(ProfessionTipDotAchievementKey key);
        bool IsAnyCanGetAchievementReward(List<ProfessionTipDotAchievementKey> keys);
    }
    public interface IProfessionTipDotRules
    {
        bool AnyPromotionRequirementCompleted();
        bool HasAvailableHigherPromotion();
        bool IsPromotionRequirementCompleted(int profession);
        bool IsPromotableAndInNewRank(int profession);
        List<int> GetAllProfessions();
        ProfessionTipDotBaseInfo GetProfessionBaseInfo(int profession);
        bool TryGetLevelInfo(int profession, int level, out ProfessionTipDotLevelCost info);
        long CalcAwardBadge(ProfessionTipDotBaseInfo info, IProfessionTipDotPlayer player);
        List<int> GetAllAchievementGroupIds();
        List<int> AchievementTypeListForShowAllInfosInSameType { get; }
        List<ProfessionTipDotAchievementKey> GetAchievementDataKeysBySameType(ProfessionTipDotAchievementKey key);
        ProfessionTipDotAchievementKey CreateAchievementDataKey(int type, string jsonParam);
        void RegisterAchievementGroupLimitCondition(Action onChanged);
        void DeregisterAchievementGroupLimitCondition(Action onChanged);
    }
}
"""))
    return inputs, outputs


def build():
    inputs, outputs = [], []
    for name in NAMES:
        path = SOURCE / (name + ".cs")
        source = path.read_text(encoding="utf-8")
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
        source = re.sub(r"^\s*\[(?:IgnoreMember|Inject|MenuPath|Header|Tooltip)[^\n]*\]\s*$", "", source, flags=re.M)
        source = log_adapter(source)
        if name == "TipDotConditionNodeBase":
            source = source.replace("private bool mIsDirty;", "public IProfessionTipDotPlayer Player { get; set; }\n\npublic IProfessionTipDotRules Rules { get; set; }\n\nprivate bool mIsDirty;")
        source = source.replace(" : ICloneable<TipDotNodeBase>", "")
        source = source.replace(" : ICloneable<TipDotNodePresetData>, IFillable<TipDotNodePresetData>", "")
        source = source.replace(" : TipDotNodeBase, IFillable<TipDotNodeBase>", " : TipDotNodeBase")
        source = source.replace("protected ITipDotNodeManager NodeManager { get; set; }", "public ITipDotNodeManager NodeManager { get; set; }")
        source = source.replace("private ITipDotBanManager TipDotBanManager { get; set; }", "public ITipDotBanManager TipDotBanManager { get; set; }")
        source = re.sub(r"\s*protected ITipDotNodeEventCenter EventCenter \{ get; set; \}", "", source)
        source = re.sub(r"\s*protected Player Player => MeManager.MainPlayer;", "", source)
        source = source.replace("ClonerResolver.Instance.GetClonerWithVerify<List<ETipDotLevelType>>().Fill(ref IgnoreLevels, obj.IgnoreLevels);", "IgnoreLevels = obj.IgnoreLevels == null ? null : new List<ETipDotLevelType>(obj.IgnoreLevels);")
        source = source.replace("CloneableHelper.ThrowException(source == null, GetType().Name);", "throw new ArgumentException(GetType().Name, nameof(source));")
        if name == "TipDotNodeKey":
            source = source.replace("TipDotNodeKey(string graphName, string nodeName, INodeInstanceInfo instanceInfo)", "TipDotNodeKey")
            source = source.replace("public string GraphName = (string.IsNullOrEmpty(graphName) ? graphName : string.Intern(graphName));", "public string GraphName;")
            source = source.replace("public string NodeName = (string.IsNullOrEmpty(nodeName) ? nodeName : string.Intern(nodeName));", "public string NodeName;")
            source = source.replace("public INodeInstanceInfo InstanceInfo = instanceInfo;", """public INodeInstanceInfo InstanceInfo;

    public TipDotNodeKey(string graphName, string nodeName, INodeInstanceInfo instanceInfo)
    {
        GraphName = string.IsNullOrEmpty(graphName) ? graphName : string.Intern(graphName);
        NodeName = string.IsNullOrEmpty(nodeName) ? nodeName : string.Intern(nodeName);
        InstanceInfo = instanceInfo;
    }""")
        if any(value in source for value in ("ClonerResolver", "CloneableHelper", "Log.Error", "MeManager", "GLogger", "ITipDotNodeEventCenter")):
            raise ValueError("Unresolved original dependency in " + name)
        outputs.append(write(name, wrap(source)))
    path = ROOT / ".tools/source-packages/official-code/framework-firstpass-source/Framework/SafeAction.cs"
    source = path.read_text(encoding="utf-8")
    inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
    source = source[:source.index("public class SafeAction<T1, T2>")]
    source = source.replace("namespace Framework;", "")
    outputs.append(write("SafeAction", wrap(log_adapter(source))))
    outputs.append(write("ITipDotNodeManager", """using System;
namespace Zjcs.Official.ProfessionUI
{
    // Exact operations consumed by the original aggregation core. Graph ownership
    // and local player event registration are supplied by the runtime adapter.
    public interface ITipDotNodeManager
    {
        Action OnAddScopeDataCollector { get; set; }
        Action OnRemoveScopeDataCollector { get; set; }
        TipDotNodeBase FindNodeOrCreate(TipDotNodeKey key);
        TipDotNodeBase FindNode(TipDotNodeKey key);
        void AddDirtyConditionNode(TipDotConditionNodeBase node);
        void RemoveDirtyConditionNode(TipDotConditionNodeBase node);
    }
}
"""))
    condition_inputs, condition_outputs = condition_ports()
    inputs.extend(condition_inputs)
    outputs.extend(condition_outputs)
    ui_inputs, ui_outputs = ui_ports()
    inputs.extend(ui_inputs)
    outputs.extend(ui_outputs)
    graph_inputs, graph_outputs = graph_ports()
    inputs.extend(graph_inputs)
    outputs.extend(graph_outputs)
    original_manager = SOURCE / "ITipDotNodeManager.cs"
    inputs.append(dict(path=original_manager.relative_to(ROOT).as_posix(), sha256=sha(original_manager)))
    for relative in ("common-source/Common/ItemType.cs", "common-source/Common/FunctionType.cs"):
        path = ROOT / ".tools/source-packages/official-code" / relative
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path)))
    validation = []
    for relative in ("unity/Assets/Game/Editor/ProfessionUI/ProfessionTipDotValidation.cs",
                     "unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeTipDotValidation.cs",
                     "scripts/prepare_unity_profession_tipdot_probe.py",
                     "assets/reference/profession-ui/native-export-source-manifest.json"):
        path = ROOT / relative
        validation.append(dict(path=relative, sha256=sha(path)))
    receipt = dict(format=1, manifest="871_156598", inputs=inputs, outputs=outputs, validation=validation,
        generator=dict(path=Path(__file__).resolve().relative_to(ROOT).as_posix(), sha256=sha(Path(__file__))),
        operation="Copy the original node aggregation, activation, condition dirty/force-update, reversal, ban, cloning and SafeAction snapshot/deduplication algorithms. Adapt logging and dependency injection; scope the node manager interface to consumed operations; express C# primary constructor and list cloner in Unity-compatible C#.",
        limitations=["Condition-specific local player providers, graph lifetime/controller adapters and actual profession red-dot rendering remain to be connected. Graph ports preserve runtime serialized fields and original connection iteration; graph-authoring and story/server execution APIs are outside this UI scope."])
    (ROOT / "assets/reference/profession-ui/tipdot-source-manifest.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Restored original profession tip-dot core:", len(outputs), "source ports")


if __name__ == "__main__":
    build()
