"""Port the original profession top-bar controls and their consumed service API."""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
CODE = ROOT / ".tools/source-packages/current/871_156598/official-code"
RUNTIME = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def write(name, text):
    target = RUNTIME / (name + ".cs")
    target.write_text(text, encoding="utf-8", newline="\n")
    meta = target.with_name(target.name + ".meta")
    meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(("zjcs-profession-ui-" + name).encode()).hexdigest() + "\n", encoding="utf-8", newline="\n")
    return dict(runtime=record(target), meta=record(meta))


def build():
    cache = ROOT / ".tools/profession-native-ui/871_156598/typed"
    plan = json.loads((cache / "native-bindings-main-dynamic.json").read_text(encoding="utf-8"))
    script_meta = cache / "export/ExportedProject/Assets/Scripts/Assembly-CSharp/TopBarHandler.cs.meta"
    script_guid = re.search(r"(?m)^guid: ([0-9a-f]{32})$", script_meta.read_text(encoding="utf-8"))[1]
    native = []
    for relative, entry in plan["assets"].items():
        if not relative.endswith(".prefab"):
            continue
        path = ROOT / entry["path"]
        for block in re.split(r"(?m)(?=^--- !u!)", path.read_text(encoding="utf-8")):
            if not re.search(r"m_Script: \{fileID: \d+, guid: " + script_guid, block):
                continue
            fields = {key: re.search(r"(?m)^  " + key + r":([^\n]*)$", block)[1].strip() for key in
                ("mShowTypeList", "mShowItemIdList", "mSlot", "mPopupWindow", "mShowWaitEnterAnim", "mHideBeforeExitAnim")}
            if fields["mShowTypeList"] or fields["mShowItemIdList"]:
                raise ValueError("Current main profession top-bar has a nonempty source list: " + relative)
            native.append(dict(prefab=record(path), relative_path=relative, component_header=block.splitlines()[0], serialized_fields=fields))
    if len(native) != 3:
        raise ValueError("Inspect the original main profession top-bar component scope: " + str(len(native)))
    names = {name: "client-source/" + name + ".cs" for name in
        ("TopBarHandler", "TopBarItem", "ITopBarManager", "CurrencyItemData", "PlayerItemUIItemIdProvider", "IPlayerItemUIExtraDataProvider")}
    names["PlayerItemType"] = "common-source/Common/PlayerItemType.cs"
    ports = []
    for name, relative in names.items():
        source = CODE / relative
        text = source.read_text(encoding="utf-8")
        text = re.sub(r"^using (?:Common|Framework.DependencyInjection|NaughtyAttributes);\n", "", text, flags=re.M)
        text = text.replace("namespace Common;\n", "")
        text = re.sub(r"^\s*//IL_[^\n]*\n", "\n", text, flags=re.M)
        text = text.replace("\t[Inject]\n", "").replace("\t[SearchableEnum]\n", "")
        text = text.replace("Vector2.op_Implicit(Vector3.zero)", "(Vector2)Vector3.zero")
        if name == "TopBarHandler":
            old = "private ITopBarManager TopBarManager { get; set; }"
            assert text.count(old) == 1
            text = text.replace(old, old + "\n\n\tpublic void Bind(ITopBarManager manager) => TopBarManager = manager ?? throw new ArgumentNullException(nameof(manager));")
        if name == "TopBarItem":
            text = text.replace("MePlayerItemUI", "IProfessionCurrencyItem")
            text = text.replace("((Component)item)", "item.Component")
        if "op_Implicit" in text or "[Inject]" in text:
            raise ValueError("Inspect original auxiliary port: " + name)
        text = "namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\nusing PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;\n" + text + "\n}\n"
        ports.append(dict(source=record(source), **write(name, text)))
    bridge = RUNTIME / "IProfessionCurrencyItem.cs"
    bridge_meta = bridge.with_name(bridge.name + ".meta")
    bridge_meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(b"zjcs-profession-ui-IProfessionCurrencyItem").hexdigest() + "\n", encoding="utf-8", newline="\n")
    result = dict(format=1, manifest="871_156598", ports=ports, currency_api=record(bridge), currency_api_meta=record(bridge_meta),
        native_components=native, native_script_metadata=record(script_meta),
        generator=record(Path(__file__)),
        validation=record(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionAuxiliaryValidation.cs"),
        operation="Copy original top-bar handler/item, manager interface, currency data/provider and complete PlayerItemType enum. Preserve empty-list guard, native slot parenting/rect reset, popup subscriptions, activation flags, stop-count propagation and ordered updates. Replace DI with explicit binding and adapt only the consumed external currency-item Component/SetStopUpdateCount API; no replacement UI is constructed.",
        limitations=["Currency-item service, original top-bar prefab and nonempty-currency flows still require their native bindings. Main profession panel integration remains incomplete."])
    (ROOT / "assets/reference/profession-ui/auxiliary-source-manifest.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Ported seven original top-bar/data types and explicit currency API")


if __name__ == "__main__":
    build()
