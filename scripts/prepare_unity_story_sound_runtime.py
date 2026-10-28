"""Copy original MyPlayableSound and Wwise Timeline callbacks to Windows C#."""
import json
import hashlib
from pathlib import Path
import re
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write

CODE=ROOT/".tools/source-packages/current/871_156598/official-code"
OUTPUT=ROOT/"unity/Assets/Game/Runtime/Official/StorySounds"
RECEIPT=ROOT/"assets/reference/official-map/map14-story-sound-runtime-source-manifest.json"
NAMESPACE="Zjcs.Official.StorySounds"

def record(path):return dict(path=source_path(path),sha256=sha(path.read_bytes()))

def runtime_meta(path):
    identity=source_path(path).removeprefix("unity/")
    guid=hashlib.md5(("zjcs-official:"+identity).encode()).hexdigest()
    text="fileFormatVersion: 2\nguid: "+guid+"\n"
    if path.is_dir():text+="folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
    elif path.suffix==".cs":text+="MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
    elif path.suffix==".asmdef":text+="AssemblyDefinitionImporter:\n  externalObjects: {}\n"
    else:raise ValueError("Unsupported story runtime metadata")
    text+="  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    target=Path(str(path)+".meta")
    if target.exists() and "\n".join(line.rstrip() for line in target.read_text(encoding="utf8").splitlines())+"\n"!=text:raise ValueError("Unrelated story runtime metadata: "+str(target))
    write(target,text.encode())

def build():
    proof=json.loads((CODE.parent/"code-manifest.json").read_bytes())
    assemblies=[CODE/"managed/decoded"/n for n in ("Assembly-CSharp.dll","GameFramework.Runtime.dll")]
    for assembly in assemblies:
        if sha(assembly.read_bytes())!=proof["assemblies"][assembly.name]["decoded_sha256"]:raise ValueError("Selected original assembly changed: "+assembly.name)
    names=("MyPlayableSound","WwiseEventPlayableAsset","WwiseEventPlayableBehaviour","WwiseEventTrack","MyPlayableTimeLine")
    outputs=[];inputs=[]
    for name in names:
        path=CODE/("gameframework-csharp9-source/GameFramework" if name=="MyPlayableTimeLine" else "client-source")/(name+".cs");inputs.append(record(path));text=path.read_text(encoding="utf-8")
        text=text.replace("using AK.Wwise;","using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;")
        text=text.replace("using GameFramework;","using Zjcs.Official.StoryPlayables;")
        text=re.sub(r"(?m)^using Framework\.Logging[^\n]*\n","",text)
        if name=="WwiseEventPlayableBehaviour":
            start=text.index("\t\t\tGLoggerInfoInterpolatedStringHandler message")
            end=text.index("\n\t\t\tWwiseEvent.Post",start)
            text=text[:start]+"\t\t\tDebug.Log(\"eventSource\" + WwiseEventSource.name);"+text[end:]
        text=text.replace("ScriptPlayable<WwiseEventPlayableBehaviour>.op_Implicit(val)","(Playable)val")
        if name=="MyPlayableTimeLine":
            text="using Zjcs.Official.StoryPlayables;\n"+text.replace("namespace GameFramework","namespace "+NAMESPACE)
        else:
            pos=text.index("[TrackColor") if name=="WwiseEventTrack" else text.index("public class ")
            text=text[:pos]+"namespace "+NAMESPACE+"\n{\n"+text[pos:]+"\n}\n"
        target=OUTPUT/(name+".cs");write(target,("// Migrated official source; see map14-story-sound-runtime source receipt.\n"+text).encode());runtime_meta(target)
        outputs.extend([target,Path(str(target)+".meta")])
    asmdef=OUTPUT/"Zjcs.Official.StorySounds.asmdef"
    write(asmdef,encode(dict(name="Zjcs.Official.StorySounds",rootNamespace=NAMESPACE,references=["Zjcs.Official.StoryPlayables","Zjcs.Official.ProfessionUI","Unity.Timeline"],autoReferenced=True)))
    runtime_meta(asmdef);runtime_meta(OUTPUT);outputs.extend([asmdef,Path(str(asmdef)+".meta"),Path(str(OUTPUT)+".meta")])
    dependencies=[ROOT/"unity/Assets/Game/Runtime/Official/ProfessionUI"/(n+".cs") for n in ("OfficialWwiseEvent","WwiseEventReference")]
    dependencies.append(ROOT/"assets/reference/profession-ui/audio-source-manifest.json")
    fixture=ROOT/"unity/Assets/Game/Editor/Map14/StorySounds/OfficialMap14StorySoundAcceptance.cs"
    write(RECEIPT,encode(dict(manifest="871_156598",mapClassId=14,assemblies=[record(p) for p in assemblies],inputs=inputs,
        dependencies=[record(p) for p in dependencies],tools=[record(Path(__file__))],
        outputs=[record(p) for p in outputs],validation=record(fixture),
        operation="Copy original MyPlayableSound lifecycle/loop boundary, MyPlayableTimeLine source/target/manual-clock/stop lifecycle and Wwise Timeline Play callback/asset/track. Reuse the existing official SDK simple Post bridge; preserve target fallback, event ID and returned playing ID. Adapt namespace, diagnostic logging and ILSpy implicit-operator syntax only.",
        limitations=["This migrates event dispatch, not sound-bank DSP/RTPC or the Windows mixer host. Native original prefab and formal map bindings remain pending."])))
    print("Original story sound runtime: MyPlayableSound, MyPlayableTimeLine and three Wwise Timeline classes migrated.")

if __name__=="__main__":build()
