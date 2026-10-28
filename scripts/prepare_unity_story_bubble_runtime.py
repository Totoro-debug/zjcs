"""Migrate original bubble-window, typewriter and world-to-UI follow behavior."""
import json
from pathlib import Path
import re
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_story_sound_runtime import runtime_meta

CODE=ROOT/".tools/source-packages/current/871_156598/official-code"
OUTPUT=ROOT/"unity/Assets/Game/Runtime/Official/StoryBubbles"
RECEIPT=ROOT/"assets/reference/official-map/map14-story-bubble-runtime-source-manifest.json"
NS="Zjcs.Official.StoryBubbles"


def record(path):return dict(path=source_path(path),sha256=sha(path.read_bytes()))


def build():
    inputs=[];outputs=[]
    names=("TMP_TypeWriterEffect","AttachToObjUI","BubbleDialog","UIBloomGroupBase","UIBloomGroup","ButtonSound")
    for name in names:
        p=CODE/("framework-firstpass-source" if name=="ButtonSound" else "client-source")/(name+".cs")
        inputs.append(record(p));text=p.read_text(encoding="utf8")
        text=re.sub(r"(?m)^using (?:Common|Framework(?:\.[\w.]+)?);\n","",text)
        text=re.sub(r"(?m)^\s*//IL_[^\n]*\n","",text)
        text=text.replace("using AK.Wwise;","using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;")
        text=text.replace("Vector2.op_Implicit(info.OffsetToRoot)","(Vector3)info.OffsetToRoot")
        if name=="TMP_TypeWriterEffect":
            start=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');end=text.index('\n\t\t}',start)
            text=text[:start]+'\t\t\tDebug.LogError("播放打字机效果，节点未激活：" + gameObject.name, this);'+text[end:]
            text=text.replace("MathUtils.Lerp(","BubbleMath.Lerp(")
        if name=="AttachToObjUI":text=text.replace("UIUtils.WorldToUIPos(","BubbleProjection.WorldToUIPos(")
        if name=="BubbleDialog":
            text=text.replace('[UIWindow("Story/BubbleDialog")]\n',"")
            text=text.replace("Parsers.Instance.BubbleDialogInfoParser","Services")
            text=text.replace("LanguageManager.Instance.GetText(LanguageUtils.GetBubbleDialogTextKey(info.Id, info.Order))",'Services.Localize("bubble_dialog_" + info.Id + "_" + info.Order)')
            start=text.index('\t[Inject]');end=text.index('\n\tpublic void Init(',start)
            text=text[:start]+'\tprivate IBubbleServices Services => BubbleServices.Current;\n'+text[end:]
            text=text.replace("MapRootManagerProxy.MyContainer.MyEntityDict.Values","Services.ServerEntities")
            text=text.replace("MapRootManagerProxy.MyContainer.MyClientEntityHandler.ClientEntities.Values","Services.ClientEntities")
            text=text.replace("MapMyCharacterManager.MyMeUiCharacter.GameObject","Services.SelfObject")
            text=text.replace("WindowManager.Instance.Close(","Services.Close(")
            start=text.index('\tprotected override void OnEnableAndInited()');end=text.index('\n\tpublic void SetContentAndState',start)
            text=text[:start]+'''\tprotected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		if (mDialogId > 0)
		{
			if (Services.TryGetHomeUI(out var home)) home.SetActive(false);
			if (Services.TryGetWorldPanel(out var world)) world.HideUI();
			if (Services.HasBottomBar) Services.HideBottomBar(this);
			if (Services.HasPlayerInfo) Services.HidePlayerInfo(this);
			Play(mDialogId);
		}
	}

	protected override void OnDisableAndInited()
	{
		if (Services.TryGetHomeUI(out var home)) home.SetActive(true);
		if (Services.TryGetWorldPanel(out var world)) world.ShowUI();
		if (Services.HasBottomBar) Services.TryShowBottomBar(this);
		if (Services.HasPlayerInfo) Services.TryShowPlayerInfo(this);
		mAttachToObj.AttachObject = null;
		mCoroutine = null;
		mWaitForSeconds = null;
		SetContentAndState(null);
		base.OnDisableAndInited();
	}
''' + text[end:]
            text='using UIBase = Zjcs.Official.ProfessionUI.UIBase;\nusing MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;\nusing MyEntity = Zjcs.Official.StoryBubbles.BubbleEntityBinding;\n'+text
        position=min(x for x in (text.find('[RequireComponent'),text.find('[DisallowMultipleComponent'),text.find('public class')) if x>=0)
        text='using Object = UnityEngine.Object;\n'+text[:position]+'namespace '+NS+'\n{\n'+text[position:]+'\n}\n'
        target=OUTPUT/(name+".cs");write(target,('// Migrated original source; see map14-story-bubble-runtime source receipt.\n'+text).encode());runtime_meta(target)
        outputs.extend([target,Path(str(target)+".meta")])
    asm=OUTPUT/"Zjcs.Official.StoryBubbles.asmdef"
    write(asm,encode(dict(name="Zjcs.Official.StoryBubbles",rootNamespace=NS,references=["Zjcs.Official.ProfessionUI","Zjcs.Official.WorldUI","Unity.TextMeshPro","Unity.ugui"],autoReferenced=True)))
    runtime_meta(asm);runtime_meta(OUTPUT);outputs.extend([asm,Path(str(asm)+".meta"),Path(str(OUTPUT)+".meta")])
    for name in ("BubbleServices","BubbleEntityBinding","BubbleProjection","BubbleMath","BubbleWindowSettings"):
        p=OUTPUT/(name+".cs");runtime_meta(p);outputs.extend([p,Path(str(p)+".meta")])
    inputs.extend([record(CODE/"client-source/UIUtils.cs"),record(CODE/"common-source/Common/MathUtils.cs"),record(CODE/"common-source/Common/BubbleDialogInfo.cs")])
    assemblies=[CODE/"managed/decoded"/name for name in ("Assembly-CSharp.dll","Framework.FirstPass.Runtime.dll")]
    proof=json.loads((CODE.parent/"code-manifest.json").read_bytes())
    for assembly in assemblies:
        if sha(assembly.read_bytes())!=proof['assemblies'][assembly.name]['decoded_sha256']:raise ValueError("Selected client assembly changed: "+assembly.name)
    for folder in ("unity/Assets/Game/Runtime/Offline/StoryBubbles","unity/Assets/Game/Editor/Map14/Bubbles","unity/Assets/Game/Resources/Map14/BubbleUI"):
        path=ROOT/folder
        if not path.is_dir():raise ValueError("Native bubble bindings have not been restored: "+folder)
        outputs.extend(sorted(p for p in path.rglob("*") if p.is_file()))
        outputs.append(Path(str(path)+".meta"))
    dependencies=[ROOT/"assets/reference/official-map"/name for name in (
        "map14-bubble-ui-source-manifest.json","map13-native-text-font-source-manifest.json","map14-entity-timelines-source-manifest.json")]
    write(RECEIPT,encode(dict(manifest="871_156598",mapClassId=14,assemblies=[record(p) for p in assemblies],inputs=inputs,
        dependencies=[record(p) for p in dependencies],
        tools=[record(Path(__file__)),record(ROOT/"scripts/prepare_unity_story_sound_runtime.py")],outputs=[record(p) for p in outputs],
        operation="Copy official BubbleDialog queue/click/autoplay/typewriter/layout/lifecycle, AttachToObjUI position following, TMP_TypeWriterEffect fixed/interval duration/cancellation, empty bloom grouping and ButtonSound. Preserve original world projection and decimal-rounded integer interpolation. Replace DI/singleton dependencies by explicit IBubbleServices, and MyEntity by an explicit presentation binding carrying original entity class ID and GameObject; no gameplay rules supplied by this adapter.",
        fontAdaptation="Clone the original font, atlas and material per window. If the Android dynamic atlas is empty but retains serialized glyph records, rebuild those glyphs from the same original TTF and require unchanged glyph indices and metrics. Never modify the source font asset.",
        limitations=["Formal map host, Timeline bubble callbacks, map/save flow and Windows Player integration remain pending; isolated Play Mode verification is documented in map-14.md."])))
    print("Original bubble window, fixed/interval typewriter and projection migrated.")


if __name__=="__main__":build()
