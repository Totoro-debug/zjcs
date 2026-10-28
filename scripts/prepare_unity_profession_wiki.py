"""Port WikiEntry control flow and record the selected original wiki table/region."""
import hashlib
import json
from pathlib import Path
import re

from prepare_unity_profession_native_inputs import ROOT
from official_apk import Apk
from official_tables import Constructors
from prepare_unity_profession_auxiliary import CODE, RUNTIME, record, write


def build():
    for relative in ("unity/Assets/Game/Runtime/Official/ProfessionUI/IProfessionWikiContext.cs", "unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionWikiTable.cs"):
        path = ROOT / relative
        path.with_name(path.name + ".meta").write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(("zjcs:" + relative).encode()).hexdigest() + "\n", encoding="utf-8", newline="\n")
    source = CODE / "client-source/WikiEntry.cs"
    original = source.read_text(encoding="utf-8")
    text = original[original.index("\t[SerializeField]"):]
    text = re.sub(r"^\s*//IL_[^\n]*\n", "\n", text, flags=re.M)
    text = re.sub(r"\t\[Inject\]\n\tprivate [^\n]+ \{ get; set; \}\n", "", text)
    text = text.replace("\tprivate DisposableBag mDisposableBag;", "\tprivate IProfessionWikiContext Context;\n\n\tpublic void Bind(IProfessionWikiContext context) => Context = context ?? throw new ArgumentNullException(nameof(context));")
    # These spans contain only the original structured logging machinery.
    text, logs = re.subn(r"\t+(?:ILogger logger =|logger =).*?GZLoggerExtensions\.[^\n]+\n", "", text, flags=re.S)
    if logs != 8:
        raise ValueError("Inspect WikiEntry logging spans: " + str(logs))
    text = re.sub(r"^\t+(?:ZLogger\w+ val\d+;|bool flag = default;|ILogger logger;)\n", "", text, flags=re.M)
    text = text.replace("\t\tmDisposableBag = default;\n", "")
    subscribe = "Disposable.AddTo<IDisposable>(ObservableSubscribeExtensions.Subscribe<Unit>(UnityUIComponentExtensions.OnClickAsObservable(mButton), (Action<Unit>)OnClick), ref mDisposableBag);"
    assert text.count(subscribe) == 1
    text = text.replace(subscribe, "mButton.onClick.AddListener(OnClick);")
    text = text.replace("mDisposableBag.Dispose();", "mButton.onClick.RemoveListener(OnClick);")
    text = text.replace("private void OnClick(Unit _)", "private void OnClick()")
    text = text.replace("ServerInfoManager.Instance.CheckFunctionDisabled(null, ServerFunctionType.Wiki)", "Context.ServerWikiDisabled")
    text = text.replace("MeManager.MainPlayer.IsFunctionLimit(FunctionType.Wiki)", "Context.Player.IsWikiLimited")
    replacements = {
        "LanguageManager.Instance.GetText": "Context.Localize",
        "BroadcastManager.Instance.SystemMsg": "Context.SystemMsg",
        "Parsers.Instance.WikiInfoParser": "Context",
        "Parsers.Instance.ConditionInfoParser": "Context",
        "WikiClickPublisher.Publish(default(WikiNewTipDotClickEvent))": "Context.PublishWikiClick()",
        "UniTaskExtensions.Forget": "Forget",
        "MeManager.MainPlayer": "Context.Player",
        "EventReporter.CreateEventData": "Context.CreateEventData",
        "EventReporter.ReportEvent": "Context.ReportEvent",
        "ICondition condition": "IProfessionWikiCondition condition",
        "WikiInfo wikiInfo": "ProfessionWikiInfo wikiInfo",
    }
    for old, new in replacements.items():
        text = text.replace(old, new)
    start = text.index('\t[AsyncStateMachine(typeof(_003CGoto_003Ed__29))]')
    end = text.index('\tprivate void ReportEvent', start)
    text = text[:start] + '''\tprivate async Task Goto(string url, string title)
\t{
\t\t// The source passes these three SDK parameters; title is unused there.
\t\tstring result = await Context.OpenHtml(url, "2", "2", "1");
\t\tif (!string.IsNullOrEmpty(result)) Context.SystemMsg(result);
\t}

\tprivate static async void Forget(Task task)
\t{
\t\ttry { await task; }
\t\tcatch (Exception exception) { Debug.LogException(exception); }
\t}

''' + text[end:]
    if any(x in text for x in ("GZLogger", "ZLogger", "UniTask", "DisposableBag", "[Inject]", "MeManager", "Parsers.Instance")):
        raise ValueError("Unresolved WikiEntry dependency")
    port = write("WikiEntry", "namespace Zjcs.Official.ProfessionUI\n{\nusing System;\nusing System.Collections.Generic;\nusing System.Threading.Tasks;\nusing UnityEngine;\nusing UnityEngine.UI;\npublic class WikiEntry : MonoBehaviour\n{\n" + text + "\n}\n")
    cache = ROOT / ".tools/profession-native-ui/871_156598/typed"
    plan = json.loads((cache / "native-bindings-main-dynamic.json").read_text(encoding="utf-8"))
    native_script = cache / "export/ExportedProject/Assets/Scripts/Assembly-CSharp/WikiEntry.cs.meta"
    native_guid = re.search(r"(?m)^guid: ([0-9a-f]{32})$", native_script.read_text(encoding="utf-8"))[1]
    native = []
    for relative, entry in plan["assets"].items():
        if not relative.endswith(".prefab"):
            continue
        path = ROOT / entry["path"]
        for block in re.split(r"(?m)(?=^--- !u!)", path.read_text(encoding="utf-8")):
            if not re.search(r"m_Script: \{fileID: \d+, guid: " + native_guid, block):
                continue
            fields = {key: re.search(r"(?m)^  " + key + r":([^\n]*)$", block)[1].strip() for key in ("WikiType", "mButtonGo", "mButton", "mClickClearNewTip")}
            if fields["WikiType"] != "Profession":
                raise ValueError("Inspect original profession wiki scope")
            native.append(dict(prefab=record(path), relative_path=relative, component_header=block.splitlines()[0], serialized_fields=fields))
    if len(native) != 1:
        raise ValueError("Original main profession wiki component count changed")
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Wrong wiki source version")
    constructors = Constructors(CODE)
    table = apk.asset("config_luban", "/wiki.g.bytes")
    raw = table.read().m_Script.encode("utf-8", "surrogateescape")
    rows = json.loads(json.dumps(constructors.table(raw, "TbWikiInfo")))
    region_source = CODE / "launch-source/Common/RegionCodeEnum.cs"
    region_text = region_source.read_text(encoding="utf-8")
    if not re.search(r"enum RegionCodeEnum\s*\{\s*CN,", region_text):
        raise ValueError("Original CN region value changed")
    parser = CODE / "common-source/Common/WikiInfoParser.cs"
    if "RegionCodeEnum regionCodeEnum = RegionCodeEnum.CN;" not in parser.read_text(encoding="utf-8"):
        raise ValueError("Original client region selection changed")
    cn_rows = [row for row in rows if row["Region"] == 0]
    if cn_rows or len(rows) != 4:
        raise ValueError("Recheck the selected original wiki regions and offline integration")
    target = ROOT / "unity/Assets/Official/ProfessionUI/Config"
    target.mkdir(parents=True, exist_ok=True)
    folder_meta = target.with_name(target.name + ".meta")
    folder_meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(b"zjcs:profession:original-config").hexdigest() + "\nfolderAsset: yes\n", encoding="utf-8", newline="\n")
    payload = target / "ProfessionWiki.json"
    payload.write_text(json.dumps(dict(manifest=apk.version, clientRegion=0, rows=rows), ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    meta = payload.with_name(payload.name + ".meta")
    meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(b"zjcs:profession:original-wiki-table").hexdigest() + "\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8", newline="\n")
    result = dict(format=1, manifest=apk.version, source=record(source), port=port,
        native_components=native, native_script_metadata=record(native_script),
        parser=record(parser), region_enum=record(region_source), constructor=record(CODE / "common-source/Common/WikiInfo.cs"),
        table=dict(source=apk.provenance(table), object_sha256=hashlib.sha256(table.get_raw_data()).hexdigest(), payload_sha256=hashlib.sha256(raw).hexdigest(), consumed_bytes=len(raw), rows=rows, selected_cn_rows=cn_rows),
        config=record(payload), config_meta=record(meta), config_folder_meta=record(folder_meta),
        context=record(RUNTIME / "IProfessionWikiContext.cs"), context_meta=record(RUNTIME / "IProfessionWikiContext.cs.meta"),
        region_adapter=record(ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionWikiTable.cs"),
        region_adapter_meta=record(ROOT / "unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionWikiTable.cs.meta"),
        generator=record(Path(__file__)),
        validation=record(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionAuxiliaryValidation.cs"),
        operation="Retain original WikiEntry fields, enable subscription/disable disposal, server/player/config/condition guard order, URL/message branches, SDK arguments, report metadata and click event order. Port the generated Goto state machine to C# async Task with explicit consumed service boundaries. Decode the complete selected Luban wiki table and preserve the original CN-only, first-entry region filter. The four current rows are US-only, so Profession has no CN configuration and its original IsLimit hides the entry; no new offline disable rule is added.",
        limitations=["Full profession integration and its player/window/condition bindings remain pending. The source current CN branch never opens external HTML; other regions and a web SDK are not implemented or claimed."])
    (ROOT / "assets/reference/profession-ui/wiki-source-manifest.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Ported original WikiEntry; decoded four original rows and confirmed zero CN entries")


if __name__ == "__main__":
    build()
