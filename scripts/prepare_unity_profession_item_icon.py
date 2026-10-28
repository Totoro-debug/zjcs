"""Migrate original ItemIcon branches and serialized layout with explicit service boundaries."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    runtime = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
    sources = {"ItemIcon": "client-source/ItemIcon.cs", "ThemeColorType": "common-source/ThemeColorType.cs",
        "ColorType": "common-source/ColorType.cs", "ItemSourceType": "common-source/Common/ItemSourceType.cs",
        "EUIInstantiateFlag": "client-source/EUIInstantiateFlag.cs"}
    ports = []
    for name, relative in sources.items():
        source = code / relative
        text = source.read_text(encoding="utf-8-sig")
        text = re.sub(r"^using (?:AK.Wwise|Common|Framework(?:\.[\w.]+)?);\n", "", text, flags=re.M)
        text = re.sub(r"^\s*//IL_[^\n]*\n", "", text, flags=re.M)
        text = text.replace("namespace Common;\n", "")
        if name == "ItemIcon":
            text, count = re.subn(r"\t\[Inject\]\n\tprivate IScriptableObjectConfig<[^>]+> \w+ \{ get; set; \}\n", "", text)
            if count != 3:
                raise ValueError("Inspect original ItemIcon config injection")
            substitutions = {
                "ItemInfo": "IProfessionItemInfo", "PlayerItemDataWrap": "IProfessionItemData",
                "AwardSourceData": "object", "LanguageUtils.TextKeyArgWrap": "ProfessionItemText",
                "ItemIconAttachmentTimeLimit": "IProfessionItemTimeLimitAttachment",
                "ItemSourceData": "IProfessionItemSource", "ItemMapLocationData": "object",
                "RightSubstringArg": "object", "Switch": "ProfessionItemSoundSwitch",
            }
            for old, new in substitutions.items():
                text = re.sub(r"\b" + re.escape(old) + r"\b", new, text)
            replacements = {
                "Parsers.Instance.ItemInfoParser.Get(classId)": "Host.GetItemInfo(classId)",
                "ItemIconConfig.Value": "Host.Config", "GlobalUIConfig.Value": "Host.GrayConfig",
                "GlobalColors.Value": "Host.Colors", "MeManager.MainPlayer": "Host.Player",
                "UIUtils.InstantiateAsync": "Host.InstantiateAsync", "UIUtils.Instantiate": "Host.Instantiate",
                "UIUtils.SetUIGray": "Host.SetUIGray", "CultureNumberArg.Compact": "Host.Compact",
                "new CultureTextArg(LanguageUtils.GetItemNameKey(myInfo.ClassId))": "Host.CultureText(Host.GetItemNameKey(myInfo.ClassId))",
                "ItemSourceDataManager.Instance.TryGetSourceData": "Host.TryGetSourceData",
                "Parsers.Instance.ItemMergeInfoParser.CanMerge(Host.Player, myInfo.ClassId, recursion: true)": "Host.CanMerge(myInfo.ClassId, recursion: true)",
                "Parsers.Instance.ItemMapLocationInfoParser.GetLocationInfos": "Host.GetLocationInfos",
                "effectConfig.SoundSwitch.SetValue(((Component)this).gameObject)": "Host.SetSoundSwitch(effectConfig.SoundSwitch, ((Component)this).gameObject)",
                "ItemDetailDialog.PopupInfoDialog": "Host.PopupInfoDialog",
                "GetAttachment<IProfessionItemTimeLimitAttachment>(ItemIconAttachmentType.TimeLimit)": "GetAttachment(ItemIconAttachmentType.TimeLimit) as IProfessionItemTimeLimitAttachment",
                "mProgress.Init(textKeyArgWrap, color)": "mProgress.Init(textKeyArgWrap.Key, textKeyArgWrap.Args, color)",
                "BottomGo.gameObject.SetActive": "BottomGo.SetActive",
                "Object.op_Implicit((Object)(object)QualityImg)": "(Object)(object)QualityImg != null",
                "Object.op_Implicit((Object)(object)BottomGo)": "(Object)(object)BottomGo != null",
            }
            for old, new in replacements.items():
                if old not in text:
                    raise ValueError("Inspect original ItemIcon dependency: " + old)
                text = text.replace(old, new)
            text = text.replace("\t\t\tBgTransform.DestroyChildren();", "\t\t\tfor (int i = BgTransform.childCount - 1; i >= 0; i--) Object.Destroy(BgTransform.GetChild(i).gameObject);")
            pattern = r"\t\tGLoggerErrorInterpolatedStringHandler message = .*?\n\t\tLog.Error\([^\n]+\n"
            text, count = re.subn(pattern, '\t\tDebug.LogError("PopupInfoDialog Error Info Is Null", this);\n', text, flags=re.S)
            if count != 2:
                raise ValueError("Inspect original item popup error branches")
            marker = "public class ItemIcon : UIBase, IGameObjectPoolItem\n{"
            if text.count(marker) != 1:
                raise ValueError("Inspect original item root class")
            text = text.replace(marker, marker + '\n\tprivate IProfessionItemIconContext mContext;\n\tprivate IProfessionItemIconContext Host => mContext ?? throw new InvalidOperationException("Original ItemIcon services are unbound.");\n\tpublic void Bind(IProfessionItemIconContext context) => mContext = context ?? throw new ArgumentNullException(nameof(context));\n')
        aliases = "using Object = UnityEngine.Object;\nusing MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\nusing LayoutElementClamper = Zjcs.Official.WorldUI.WorldLayoutElementClamper;\nusing ItemProgress = Zjcs.Official.WorldUI.WorldItemProgress;\nusing TMPEffect = TMPro.TMPEffect;\n"
        text = "namespace Zjcs.Official.ProfessionUI\n{\n" + aliases + text + "\n}\n"
        path = runtime / (name + ".cs")
        path.write_text(text, encoding="utf-8", newline="\n")
        meta = path.with_name(path.name + ".meta")
        meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(("zjcs-profession-ui-" + name).encode()).hexdigest() + "\n", encoding="utf-8")
        ports.append(dict(source=record(source), runtime=record(path), meta=record(meta)))
    context = runtime / "IProfessionItemIconContext.cs"
    meta = context.with_name(context.name + ".meta")
    meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(b"zjcs-profession-ui-IProfessionItemIconContext").hexdigest() + "\n", encoding="utf-8")
    result = dict(format=1, manifest="871_156598", ports=ports, context=record(context), context_meta=record(meta),
        generator=record(Path(__file__)), reused_action_source=record(runtime / "SafeAction.cs"),
        destroy_children_source=record(code / "framework-source/Framework/TransformExtensions.cs"),
        operation="Retain original ItemIcon serialized fields, enum values, initialization/clear order, quality/effect branches, counts/progress, attachment priority, listener behavior and detail popup dispatch. Adapt player/parser/config/resource/formatting/window dependencies to explicit consumed interfaces; reuse existing original ImageProxy, SafeAction and shared UI ports. Preserve reverse DestroyChildren traversal. Correct decompiler GameObject.gameObject and Object.op_Implicit expressions.",
        validation_sources=[record(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionItemIconValidation.cs"), record(runtime / "Zjcs.Official.ProfessionUI.asmdef"), record(ROOT / "unity/Assets/Game/Editor/ProfessionUI/Zjcs.Editor.ProfessionUI.asmdef")],
        limitations=["Native prefab, quality configuration, local Sprite lookup and font binding are verified separately by the item catalog proof. Complete player provider, detail popup contents and full profession panel wiring remain incomplete.",
            "The profession default prefab has an empty FrameEffectInfo list. Sound-switch SDK service and time-limited inventory attachments are external consumed interfaces, not claimed as SDK or inventory feature ports."])
    target = ROOT / "assets/reference/profession-ui/item-icon-source-manifest.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Ported original ItemIcon and four consumed enums; service integration pending")


if __name__ == "__main__":
    build()
