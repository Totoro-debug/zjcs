"""Migrate the original Animator-parameter Timeline track and clip behaviour."""
import json
from pathlib import Path
import re
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_story_sound_runtime import runtime_meta

CODE=ROOT/".tools/source-packages/current/871_156598/official-code"
OUTPUT=ROOT/"unity/Assets/Game/Runtime/Official/StoryAnimatorTimeline"
RECEIPT=ROOT/"assets/reference/official-map/map14-story-animator-timeline-source-manifest.json"


def record(path):return dict(path=source_path(path),sha256=sha(path.read_bytes()))


def build():
    assembly=CODE/"managed/decoded/Assembly-CSharp.dll"
    proof=json.loads((CODE.parent/"code-manifest.json").read_bytes())
    if sha(assembly.read_bytes())!=proof["assemblies"][assembly.name]["decoded_sha256"]:raise ValueError("Selected original assembly changed")
    inputs=[];outputs=[]
    for name in ("TlAnimatorParamTrack","TlAnimatorParamClip","TlAnimatorParamBehaviour"):
        path=CODE/"client-source"/(name+".cs");inputs.append(record(path));text=path.read_text(encoding="utf8")
        text=re.sub(r"(?m)^using Framework[^\n]*\n","",text)
        text=re.sub(r"(?m)^\s*//IL_[^\n]*\n","",text)
        text=text.replace("ScriptPlayable<TlAnimatorParamBehaviour>.op_Implicit(ScriptPlayable<TlAnimatorParamBehaviour>.Create(graph, template, 0))","ScriptPlayable<TlAnimatorParamBehaviour>.Create(graph, template, 0)")
        if name=="TlAnimatorParamBehaviour":
            old="((PlayableBehaviour)this).OnPlayableCreate(playable);"
            if text.count(old)!=1:raise ValueError("Inspect original OnPlayableCreate IL again")
            text=text.replace(old,"base.OnPlayableCreate(playable);")
            start=text.index("\t\t\tGLoggerErrorInterpolatedStringHandler");end=text.index("\n\t\t\treturn;",start)
            text=text[:start]+'\t\t\tDebug.LogError("TlAnimatorParamBehaviour myAnimator == null");'+text[end:]
        index=min(v for v in (text.find("[Serializable]"),text.find("[TrackColor"),text.find("public class")) if v>=0)
        text='using Object = UnityEngine.Object;\nusing Zjcs.Official.StoryAnimation;\n'+text[:index]+"namespace Zjcs.Official.StoryAnimatorTimeline\n{\n"+text[index:]+"\n}\n"
        target=OUTPUT/(name+".cs");write(target,("// Migrated original source; see map14-story-animator-timeline receipt.\n"+text).encode());runtime_meta(target)
        outputs.extend([target,Path(str(target)+".meta")])
    asm=OUTPUT/"Zjcs.Official.StoryAnimatorTimeline.asmdef"
    write(asm,encode(dict(name="Zjcs.Official.StoryAnimatorTimeline",references=["Zjcs.Official.StoryAnimation","Unity.Timeline"],autoReferenced=True)))
    runtime_meta(asm);runtime_meta(OUTPUT);outputs.extend([asm,Path(str(asm)+".meta"),Path(str(OUTPUT)+".meta")])
    editor=ROOT/"unity/Assets/Game/Editor/Map14/AnimatorTimeline"
    outputs.extend(sorted(p for p in editor.rglob("*") if p.is_file()));outputs.append(Path(str(editor)+".meta"))
    write(RECEIPT,encode(dict(manifest="871_156598",mapClassId=14,assemblies=[record(assembly)],inputs=inputs,
        dependencies=[record(ROOT/"assets/reference/official-map/map14-story-animator-runtime-source-manifest.json")],
        tools=[record(Path(__file__))],outputs=[record(p) for p in outputs],
        ilEvidence=dict(method="TlAnimatorParamBehaviour.OnPlayableCreate",offset="IL_0002",opcode="call",target="System.Void UnityEngine.Playables.PlayableBehaviour::OnPlayableCreate(UnityEngine.Playables.Playable)",reader=dict(path="D:/Tool/Unity/Editor/Data/Managed/Unity.Cecil.dll",sha256=sha(Path("D:/Tool/Unity/Editor/Data/Managed/Unity.Cecil.dll").read_bytes()))),
        operation="Copy the original Timeline track, template cloning, hash initialization, first-frame parameter snapshot, AnimationController-before-Animator lookup, trigger/Update(0), pause restore and EndResume branch. Preserve the original base call verified from IL; only adapt namespace, diagnostic and implicit-conversion syntax.",
        limitations=["Isolated native Animator/Timeline dispatch verification is recorded in map-14.md. Original Animator/controller/model rendering, full entity Timeline, formal map/save flow and Windows Player remain pending."])))
    print("Original Animator Timeline track/clip/behaviour and verified base-call adaptation recorded.")


if __name__=="__main__":build()
