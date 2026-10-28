"""Migrate original Animator parameter buffers, layered state and playable lifecycle."""
import json
from pathlib import Path
import re
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

CODE=ROOT/".tools/source-packages/current/871_156598/official-code"
OUTPUT=ROOT/"unity/Assets/Game/Runtime/Official/StoryAnimation"
RECEIPT=ROOT/"assets/reference/official-map/map14-story-animator-runtime-source-manifest.json"
NAMESPACE="Zjcs.Official.StoryAnimation"

def record(path):return dict(path=source_path(path),sha256=sha(path.read_bytes()))

def build():
    manifest=json.loads((CODE.parent/"code-manifest.json").read_bytes())
    assembly=CODE/"managed/decoded/Assembly-CSharp.dll"
    if sha(assembly.read_bytes())!=manifest["assemblies"][assembly.name]["decoded_sha256"]:raise ValueError("Selected original client assembly changed")
    inputs=[];outputs=[];adaptations=[]
    for name in ("AnimationController","StateMergeList","MyPlayableAnimator"):
        path=CODE/"client-source"/(name+".cs");inputs.append(record(path));text=path.read_text(encoding="utf8")
        text=text.replace("using GameFramework;","using Zjcs.Official.StoryPlayables;")
        text=re.sub(r"(?m)^using Framework\.Logging[^\n]*\n","",text)
        if name=="AnimationController":
            start=text.index("\tpublic PlayableGraph myGraph\n");end=text.index("\n\tpublic RuntimeAnimatorController RuntimeAnimatorController",start)
            text=text[:start]+"\tpublic PlayableGraph myGraph { get; set; }\n"+text[end:]
        text,count=logging_adapter(text)
        pos=text.index("[MyPlayableApply") if name=="MyPlayableAnimator" else text.index("public class ")
        text="using Object = UnityEngine.Object;\n"+text[:pos]+"namespace "+NAMESPACE+"\n{\n"+text[pos:]+"\n}\n"
        target=OUTPUT/(name+".cs");write(target,("// Migrated official source; see map14-story-animator-runtime source receipt.\n"+text).encode());runtime_meta(target)
        outputs.extend([target,Path(str(target)+".meta")]);adaptations.append(dict(source=name,diagnosticBlocks=count,autoPropertyCSharp9=name=="AnimationController"))
    asmdef=OUTPUT/"Zjcs.Official.StoryAnimation.asmdef";write(asmdef,encode(dict(name="Zjcs.Official.StoryAnimation",rootNamespace=NAMESPACE,references=["Zjcs.Official.StoryPlayables"],autoReferenced=True)))
    runtime_meta(asmdef);runtime_meta(OUTPUT);outputs.extend([asmdef,Path(str(asmdef)+".meta"),Path(str(OUTPUT)+".meta")])
    fixture=ROOT/"unity/Assets/Game/Editor/Map14/StoryAnimation/OfficialMap14StoryAnimatorAcceptance.cs"
    write(RECEIPT,encode(dict(manifest="871_156598",mapClassId=14,assemblies=[record(assembly)],inputs=inputs,
        tools=[record(Path(__file__)),record(ROOT/"scripts/prepare_unity_story_playable_runtime.py"),record(ROOT/"scripts/prepare_unity_story_sound_runtime.py")],
        adaptations=adaptations,outputs=[record(p) for p in outputs],validation=record(fixture),
        operation="Copy official AnimationController, StateMergeList and MyPlayableAnimator. Preserve initial parameter buffering, keyed layering/removal order, pause counters, Animator override callbacks, allowed-state checks, fade/stop parameter restoration and pool reuse. Adapt namespace, diagnostic logger and C# 13 field auto-property to equivalent Unity C# 9 only.",
        limitations=["Original visual model, Animator controller/clip and prefab binding, bone/attachment/skin behavior, free native skeleton animation and formal map flow remain pending."])))
    print("Original Animator runtime: AnimationController, StateMergeList and MyPlayableAnimator migrated.")

if __name__=="__main__":build()
