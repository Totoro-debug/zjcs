"""Copy the original entity-Timeline bubble track, clip and callbacks."""
from pathlib import Path
import json
import re
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_story_sound_runtime import runtime_meta

CODE=ROOT/".tools/source-packages/current/871_156598/official-code"
OUTPUT=ROOT/"unity/Assets/Game/Runtime/Official/StoryBubbleTimeline"
RECEIPT=ROOT/"assets/reference/official-map/map14-story-bubble-timeline-source-manifest.json"


def record(path):return dict(path=source_path(path),sha256=sha(path.read_bytes()))


def build():
    assembly=CODE/"managed/decoded/Assembly-CSharp.dll"
    proof=json.loads((CODE.parent/"code-manifest.json").read_bytes())
    if sha(assembly.read_bytes())!=proof["assemblies"][assembly.name]["decoded_sha256"]:raise ValueError("Selected original assembly changed")
    inputs=[];outputs=[]
    for name in ("EntityBehaviour","EntityClip","EntityTalkBehaviour","EntityTalkAsset","EntityTrack"):
        p=CODE/"client-source"/(name+".cs");inputs.append(record(p));text=p.read_text(encoding="utf8")
        text=re.sub(r"(?m)^using (?:Framework|Common);\n","",text)
        text=re.sub(r"(?m)^\s*//IL_[^\n]*\n","",text)
        text=text.replace("Parsers.Instance.BubbleDialogInfoParser","BubbleServices.Current")
        text=text.replace("LanguageManager.Instance.GetText(LanguageUtils.GetBubbleDialogTextKey(bubbleDialogInfo.Id, bubbleDialogInfo.Order))",'BubbleServices.Current.Localize("bubble_dialog_" + bubbleDialogInfo.Id + "_" + bubbleDialogInfo.Order)')
        text=text.replace("WindowManager.Instance", "((IBubblePopupServices)BubbleServices.Current)")
        text=text.replace("ScriptPlayable<EntityTalkBehaviour>.op_Implicit(val)","val")
        if name=="EntityTrack":
            old="return ((TrackAsset)this).CreateTrackMixer(graph, go, inputCount);"
            if text.count(old)!=1:raise ValueError("Inspect original EntityTrack base-call IL again")
            # Verified Assembly-CSharp IL_004e is call (not callvirt) to
            # UnityEngine.Timeline.TrackAsset.CreateTrackMixer. ILSpy's cast
            # emits a recursive virtual call when copied literally to C#.
            text=text.replace(old,"return base.CreateTrackMixer(graph, go, inputCount);")
        position=min(v for v in (text.find('[Serializable]'),text.find('[DisplayName'),text.find('public ')) if v>=0)
        text='using Object = UnityEngine.Object;\nusing MyEntity = Zjcs.Official.StoryBubbles.BubbleEntityBinding;\nusing Zjcs.Official.StoryBubbles;\n'+text[:position]+'namespace Zjcs.Official.StoryBubbleTimeline\n{\n'+text[position:]+'\n}\n'
        target=OUTPUT/(name+".cs");write(target,('// Migrated original source; see map14-story-bubble-timeline receipt.\n'+text).encode());runtime_meta(target)
        outputs.extend([target,Path(str(target)+".meta")])
    asm=OUTPUT/"Zjcs.Official.StoryBubbleTimeline.asmdef"
    write(asm,encode(dict(name="Zjcs.Official.StoryBubbleTimeline",references=["Zjcs.Official.StoryBubbles","Zjcs.Official.ProfessionUI","Unity.Timeline"],autoReferenced=True)))
    runtime_meta(asm);runtime_meta(OUTPUT);outputs.extend([asm,Path(str(asm)+".meta"),Path(str(OUTPUT)+".meta")])
    p=OUTPUT/"IBubblePopupServices.cs";runtime_meta(p);outputs.extend([p,Path(str(p)+".meta")])
    editor=ROOT/"unity/Assets/Game/Editor/Map14/BubbleTimeline"
    outputs.extend(sorted(p for p in editor.rglob("*") if p.is_file()));outputs.append(Path(str(editor)+".meta"))
    write(RECEIPT,encode(dict(manifest="871_156598",mapClassId=14,assemblies=[record(assembly)],inputs=inputs,
        dependencies=[record(ROOT/"assets/reference/official-map"/name) for name in ("map14-story-bubble-runtime-source-manifest.json","map14-entity-timelines-source-manifest.json")],
        tools=[record(Path(__file__))],outputs=[record(p) for p in outputs],
        ilEvidence=dict(method="EntityTrack.CreateTrackMixer",offset="IL_004e",opcode="call",target="UnityEngine.Playables.Playable UnityEngine.Timeline.TrackAsset::CreateTrackMixer(UnityEngine.Playables.PlayableGraph,UnityEngine.GameObject,System.Int32)",reader=dict(path="D:/Tool/Unity/Editor/Data/Managed/Unity.Cecil.dll",sha256=sha(Path("D:/Tool/Unity/Editor/Data/Managed/Unity.Cecil.dll").read_bytes()))),
        operation="Copy original entity binding, Timeline clip duration lookup/template clone, optional localized UTF-16 text-length interval, popup creation/completion and pause close. Explicit popup service replaces WindowManager; presentation binding replaces MyEntity. Restore verified nonvirtual base call hidden by ILSpy's cast syntax.",
        limitations=["Isolated native Timeline and original bubble UI verification is recorded in map-14.md. Full original Timeline asset/entity/camera/audio binding, formal map/save flow and Windows Player remain pending."])))
    print("Original entity bubble Timeline: five original classes and explicit popup binding recorded.")


if __name__=="__main__":build()
