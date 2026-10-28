"""Port the original item UI lifecycle and attachment components without UI synthesis."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError("Inspect the original source before adapting: " + old[:100])
    return text.replace(old, new)


def build():
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    out = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
    names = {
        "UIBase": "framework-firstpass-source/UIBase.cs",
        "IWindowOperator": "framework-firstpass-source/IWindowOperator.cs",
        "IGameObjectPoolItem": "framework-source/IGameObjectPoolItem.cs",
        "ItemIconAttachmentType": "client-source/ItemIconAttachmentType.cs",
        "IItemIconAttachmentManager": "client-source/IItemIconAttachmentManager.cs",
        "ItemIconAttachment": "client-source/ItemIconAttachment.cs",
        "ItemIconAttachmentCell": "client-source/ItemIconAttachmentCell.cs",
        "ItemIconAttachmentHint": "client-source/ItemIconAttachmentHint.cs",
        "ItemIconCustomAttachment": "client-source/ItemIconCustomAttachment.cs",
        "ItemIconAttachmentConfig": "client-source/ItemIconAttachmentConfig.cs",
        "ItemIconConfig": "client-source/ItemIconConfig.cs",
        "Quality": "common-source/Common/Quality.cs",
    }
    ports = []
    for name, relative in names.items():
        source = code / relative
        text = source.read_text(encoding="utf-8-sig")
        text = re.sub(r"^using (?:Framework(?:\.[\w.]+)?|GFrameCore[\w.]*|Microsoft.Extensions.Logging|ZLogger|Common);\n", "", text, flags=re.M)
        text = re.sub(r"^\s*//IL_[^\n]*\n", "", text, flags=re.M)
        text = text.replace("namespace Common;\n", "")
        text = text.replace("\t[Inject]\n", "").replace("[ScriptableObjectConfig]\n", "")
        if name == "UIBase":
            text = replace_once(text, "\tprivate ILogger<UIBase> Logger { get; set; }\n", "")
            pattern = r"\t\tcatch \(Exception ex\)\n\t\t\{.*?\n\t\t\}"
            blocks = list(re.finditer(pattern, text, re.S))
            if len(blocks) != 2:
                raise ValueError("Inspect UIBase exception handlers")
            for match, label in reversed(list(zip(blocks, ("OnEnableAndInited", "OnDisableAndInited")))):
                context = "gameObject.name" if label == "OnEnableAndInited" else "GetFullName(gameObject)"
                replacement = '\t\tcatch (Exception ex)\n\t\t{\n\t\t\tDebug.LogError("' + label + ' excep, object name:" + ' + context + ', this);\n\t\t\tDebug.LogException(ex, this);\n\t\t}'
                text = text[:match.start()] + replacement + text[match.end():]
        if name == "ItemIconAttachment":
            text = replace_once(text,
                "\tprivate IItemIconAttachmentManager mItemIconAttachmentManager { get; set; }",
                "\tprivate IItemIconAttachmentManager mItemIconAttachmentManager { get; set; }\n\n\tpublic void Bind(IItemIconAttachmentManager manager) => mItemIconAttachmentManager = manager;")
            pattern = r"\t\t\t\tGLoggerWarnInterpolatedStringHandler message = .*?\n\t\t\t\tLog.Warn\([^\n]+\n"
            text, count = re.subn(pattern, '\t\t\t\tDebug.LogWarning("TryGetAttachment type: " + type + " attachment is null", this);\n', text, flags=re.S)
            if count != 1:
                raise ValueError("Inspect attachment diagnostic handler")
        aliases = "using Object = UnityEngine.Object;\nusing PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;\nusing MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\n"
        text = "namespace Zjcs.Official.ProfessionUI\n{\n" + aliases + text + "\n}\n"
        path = out / (name + ".cs")
        path.write_text(text, encoding="utf-8", newline="\n")
        meta = path.with_name(path.name + ".meta")
        guid = hashlib.md5(("zjcs-profession-ui-" + name).encode()).hexdigest()
        meta.write_text("fileFormatVersion: 2\nguid: " + guid + "\n", encoding="utf-8", newline="\n")
        ports.append(dict(source=record(source), runtime=record(path), meta=record(meta)))
    editor = ROOT / "unity/Assets/Game/Editor/ProfessionUI"
    result = dict(format=1, manifest="871_156598", ports=ports, generator=record(Path(__file__)),
        validation=record(editor / "ProfessionItemAttachmentValidation.cs"),
        validation_meta=record(editor / "ProfessionItemAttachmentValidation.cs.meta"),
        editor_assembly=record(editor / "Zjcs.Editor.ProfessionUI.asmdef"),
        operation="Copy original lifecycle, initialization wrappers, exception boundaries, reset order, attachment lookup/sorting/recycling, custom attachment behavior and configuration fields. Replace framework logging with Unity logging, dependency injection with an explicit manager binding, and use existing original PopupWindow/MultiLanguageText ports. Keep enum values and original empty pool hooks.",
        limitations=["ItemIcon and attachment manager/pools are migrated and verified separately; full profession panel service binding and item detail popup contents remain pending.",
            "These source ports do not constitute rendered profession UI or mouse interaction validation."])
    target = ROOT / "assets/reference/profession-ui/item-components-source-manifest.json"
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Ported", len(ports), "original item lifecycle/attachment/configuration types")


if __name__ == "__main__":
    build()
