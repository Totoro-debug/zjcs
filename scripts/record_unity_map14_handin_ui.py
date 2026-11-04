"""Record the native hand-in bindings produced by the original UI importer."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
RUNTIME=ROOT/"unity/Assets/Game/Resources/Map14/UI/HandIn"
SOURCE_RECEIPT=ROOT/"assets/reference/official-map/map14-handin-ui-source-manifest.json"
RECEIPT=ROOT/"assets/reference/official-map/map14-handin-ui-bindings-source-manifest.json"

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def entry(path, normalize_text=False):
    raw=path.read_bytes()
    if normalize_text:
        return {"path":path.relative_to(ROOT).as_posix(),"sha256":hashlib.sha256(raw.replace(b"\r\n",b"\n")).hexdigest(),"hashLineEndings":"LF"}
    return {"path":path.relative_to(ROOT).as_posix(),"sha256":sha(path)}

def build(shared=False):
    runtime, source_receipt, receipt = RUNTIME, SOURCE_RECEIPT, RECEIPT
    if shared:
        runtime=ROOT/"unity/Assets/Game/Resources/World/Mainline/HandIn"
        source_receipt=ROOT/"assets/reference/official-map/mainline-handin-ui-source-manifest.json"
        receipt=ROOT/"assets/reference/official-map/mainline-handin-ui-bindings-source-manifest.json"
    source=json.loads(source_receipt.read_text(encoding="utf-8"))
    manifest="884_156692" if shared else "871_156598"
    valid = source.get("counts")==dict(dialogNodes=43,iconNodes=19,components=101,sprites=19) if shared else (
        source.get("mapClassId")==14 and source.get("nodeCounts")=={"DialogHandInItemDialog":39,"ItemIconDefault":19})
    if source["manifest"]!=manifest or not valid:
        raise ValueError("Native hand-in binding source is not pinned")
    required={"DialogHandInItemDialog.prefab","ItemIconDefault.prefab","UIDialog_Show.anim","UIDialog_Hide.anim","UIGray.mat",
        "Btn_Base_Click.controller","Normal.anim","Highlighted.anim","Pressed.anim","Selected.anim","Disabled.anim","catalog.json"}
    if not all((runtime/name).is_file() for name in required):raise ValueError("Native hand-in binding closure is incomplete")
    files=sorted(p for p in runtime.iterdir() if p.is_file() and p.suffix in (".meta",".json",".prefab",".anim",".controller",".mat"))
    inputs=[source_receipt,Path(__file__).resolve()]
    inputs.extend(ROOT/"unity/Assets/Game/Editor"/name for name in ("OfficialMap11UiTools.Map14HandIn.cs","OfficialMap11UiTools.Map14HandInAnimation.cs","OfficialMap14HandInAcceptance.cs"))
    inputs.extend(ROOT/"unity/Assets/Game/Runtime/Presentation/World/Mainline"/name for name in ("OfficialMap14HandInDialogView.cs","OfficialMainlineMapRuntime.HandIn.cs","Map14HandInUiAudio.cs"))
    inputs.extend(ROOT/"unity/Assets/Game/Runtime/Official/WorldUI"/(name+".cs") for name in ("AwardPanelLayoutElement","WorldItemIcon","WorldItemProgress","WorldStateSwitcher","WorldMultiLanguageText","WorldMaintainGlobalScale","WorldMaintainGlobalScaleUI","WorldPopupWindow","WorldButtonSound","WorldPopupWindowSound","WorldBgDialogElement","WorldUIBloomGroup","WorldCountRedDotImage","WorldPreviewTool"))
    payload={"format":1,"manifest":manifest,"mapClassId":0 if shared else 14,
        "operation":"Restore original " + ("43/19" if shared else "39/19") + "-node native UI prefabs, component references, managed-reference progress state, three Animator settings, original popup clips, verified original button controller/clips, original grayscale material and current-bank-equivalent Wwise audio bindings. Resolve dynamic item art from the local official inventory asset catalog.",
        "sourceManifest":entry(source_receipt),"inputs":[entry(p, p.suffix in (".cs",".py")) for p in inputs],"outputs":[entry(p) for p in files]}
    receipt.write_bytes((json.dumps(payload,ensure_ascii=False,indent=2)+"\n").encode("utf-8"))
    print(json.dumps({"nativeBindingOutputs":len(files),"inputs":len(inputs)},ensure_ascii=False))
    return payload

if __name__=="__main__":
    import argparse
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mainline",action="store_true")
    build(parser.parse_args().mainline)
