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

def build():
    source=json.loads(SOURCE_RECEIPT.read_text(encoding="utf-8"))
    if source["manifest"]!="871_156598" or source["mapClassId"]!=14 or source["nodeCounts"]!={"DialogHandInItemDialog":39,"ItemIconDefault":19}:
        raise ValueError("Native hand-in binding source is not pinned")
    required={"DialogHandInItemDialog.prefab","ItemIconDefault.prefab","UIDialog_Show.anim","UIDialog_Hide.anim","UIGray.mat",
        "Btn_Base_Click.controller","Normal.anim","Highlighted.anim","Pressed.anim","Selected.anim","Disabled.anim","catalog.json"}
    if not all((RUNTIME/name).is_file() for name in required):raise ValueError("Native hand-in binding closure is incomplete")
    files=sorted(p for p in RUNTIME.iterdir() if p.is_file() and p.suffix in (".meta",".json",".prefab",".anim",".controller",".mat"))
    inputs=[SOURCE_RECEIPT,Path(__file__).resolve()]
    inputs.extend(ROOT/"unity/Assets/Game/Editor"/name for name in ("OfficialMap11UiTools.Map14HandIn.cs","OfficialMap11UiTools.Map14HandInAnimation.cs","OfficialMap14HandInAcceptance.cs"))
    inputs.extend(ROOT/"unity/Assets/Game/Runtime/Presentation/World/Mainline"/name for name in ("OfficialMap14HandInDialogView.cs","OfficialMainlineMapRuntime.HandIn.cs","Map14HandInUiAudio.cs"))
    inputs.extend(ROOT/"unity/Assets/Game/Runtime/Official/WorldUI"/(name+".cs") for name in ("AwardPanelLayoutElement","WorldItemIcon","WorldItemProgress","WorldStateSwitcher","WorldMultiLanguageText","WorldMaintainGlobalScale","WorldMaintainGlobalScaleUI","WorldPopupWindow","WorldButtonSound","WorldPopupWindowSound","WorldBgDialogElement","WorldUIBloomGroup","WorldCountRedDotImage","WorldPreviewTool"))
    payload={"format":1,"manifest":"871_156598","mapClassId":14,
        "operation":"Restore original 39/19-node native UI prefabs, component references, managed-reference progress state, three Animator settings, original popup clips, verified original button controller/clips, original grayscale material and current-bank-equivalent Wwise audio bindings. Resolve dynamic item art from the local official inventory asset catalog.",
        "sourceManifest":entry(SOURCE_RECEIPT),"inputs":[entry(p, p.suffix in (".cs",".py")) for p in inputs],"outputs":[entry(p) for p in files]}
    RECEIPT.write_bytes((json.dumps(payload,ensure_ascii=False,indent=2)+"\n").encode("utf-8"))
    print(json.dumps({"nativeBindingOutputs":len(files),"inputs":len(inputs)},ensure_ascii=False))
    return payload

if __name__=="__main__":build()
