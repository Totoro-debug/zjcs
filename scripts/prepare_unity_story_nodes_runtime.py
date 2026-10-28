"""Recover official story-node lookup/registration and Timeline camera binding."""
import json
from pathlib import Path
import re
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, source_path, write
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

CODE=ROOT/".tools/source-packages/current/871_156598/official-code"
OUTPUT=ROOT/"unity/Assets/Game/Runtime/Official/StoryNodes"
RECEIPT=ROOT/"assets/reference/official-map/map14-story-nodes-runtime-source-manifest.json"
NAMESPACE="Zjcs.Official.StoryNodes"

def record(path):return dict(path=source_path(path),sha256=sha(path.read_bytes()))

def manager(text):
    # ILSpy cannot resolve the original UniTask builder without its stripped
    # first-pass references. Retain the ordinary methods verbatim, and restore
    # the two async methods directly from their emitted MoveNext call paths.
    start=text.index("\tprivate Dictionary<string, GameObject> mStoryRegistedNodes")
    end=text.index("\n\t[AsyncStateMachine(",start)
    body=text[start:end]
    body=re.sub(r"\t\[Inject\]\n\tprivate ILogger<StoryNodeRegisterMgr> Logger \{ get; set; \}\n", "",body)
    pattern=re.compile(r"(?m)^(?P<indent>\s*)ILogger logger = [^\n]+;\n(?:(?P=indent)[^\n]*\n|(?P=indent)\t[^\n]*\n)*?(?P=indent)GZLoggerExtensions.Error\([^\n]+;")
    messages=iter(('注册剧情外部节点失败，节点为空，key:', '获取剧情节点失败，key：'))
    def diagnostic(match):return match['indent']+'UnityEngine.Debug.LogError("'+next(messages)+'" + name);'
    body,count=pattern.subn(diagnostic,body)
    if count!=2 or 'ZLogger' in body or 'Logger' in body:raise ValueError("Unresolved original registry diagnostic")
    state_sources=text[:start]
    for expression in ('UniTask.WaitUntil','(PlayerLoopTiming)8, token, false','GetNode<T>','GetNode(name)'):
        if expression not in state_sources:raise ValueError("Inspect original async registry state machine: "+expression)
    body+='''
	public async UniTask<T> GetNodeAsync<T>(string name, bool isWaitRegister, CancellationToken token) where T : MonoBehaviour
	{
		if (isWaitRegister)
			await UniTask.WaitUntil(() => (Object)(object)GetNode<T>(name) != (Object)null, (PlayerLoopTiming)8, token, false);
		return GetNode<T>(name);
	}

	public async UniTask<GameObject> GetNodeAsync(string name, bool isWaitRegister, CancellationToken token)
	{
		if (isWaitRegister)
			await UniTask.WaitUntil(() => (Object)(object)GetNode(name) != (Object)null, (PlayerLoopTiming)8, token, false);
		return GetNode(name);
	}
'''
    return 'using System;\nusing System.Collections.Generic;\nusing System.Threading;\nusing Cysharp.Threading.Tasks;\nusing UnityEngine;\nusing Zjcs.Official.ProfessionUI;\n\npublic class StoryNodeRegisterMgr : IStoryNodeRegisterMgr, IProfessionStoryNodeRegister\n{\n'+body+'}\n'

def build():
    proof=json.loads((CODE.parent/"code-manifest.json").read_bytes());assembly=CODE/"managed/decoded/Assembly-CSharp.dll"
    if sha(assembly.read_bytes())!=proof["assemblies"][assembly.name]["decoded_sha256"]:raise ValueError("Selected original client assembly changed")
    inputs=[];outputs=[]
    for name in ("IStoryNodeRegisterMgr","StoryNodeRegisterMgr","StoryNodeRegister","CinemachineTrackStoryBinder"):
        path=CODE/"client-source"/(name+".cs");inputs.append(record(path));text=path.read_text(encoding="utf8")
        if name=="StoryNodeRegisterMgr":text=manager(text)
        else:
            text=re.sub(r"(?m)^using Framework[^\n]*\n","",text)
            text=text.replace('[Inject]\n\tprivate IStoryNodeRegisterMgr NodeRegisterManager','public IStoryNodeRegisterMgr NodeRegisterManager')
            text,_=logging_adapter(text)
        pos=text.index('[RequireComponent') if name=="CinemachineTrackStoryBinder" else text.index('public ')
        text='using Object = UnityEngine.Object;\n'+text[:pos]+'namespace '+NAMESPACE+'\n{\n'+text[pos:]+'\n}\n'
        target=OUTPUT/(name+".cs");write(target,('// Migrated official source; see map14-story-nodes-runtime source receipt.\n'+text).encode());runtime_meta(target)
        outputs.extend([target,Path(str(target)+".meta")])
    asmdef=OUTPUT/"Zjcs.Official.StoryNodes.asmdef";write(asmdef,encode(dict(name="Zjcs.Official.StoryNodes",rootNamespace=NAMESPACE,references=["Zjcs.Official.ProfessionUI","UniTask","Cinemachine","Unity.Timeline"],autoReferenced=True)))
    runtime_meta(asmdef);runtime_meta(OUTPUT);outputs.extend([asmdef,Path(str(asmdef)+".meta"),Path(str(OUTPUT)+".meta")])
    fixture=ROOT/"unity/Assets/Game/Editor/Map14/StoryNodes/OfficialMap14StoryNodesAcceptance.cs"
    write(RECEIPT,encode(dict(manifest="871_156598",mapClassId=14,assemblies=[record(assembly)],inputs=inputs,
        tools=[record(Path(__file__)),record(ROOT/"scripts/prepare_unity_story_playable_runtime.py"),record(ROOT/"scripts/prepare_unity_story_sound_runtime.py")],
        outputs=[record(p) for p in outputs],validation=record(fixture),
        operation="Copy original external story-node dictionary/event/lifecycle/lookup rules and Cinemachine Timeline binding. Restore two async methods from exact emitted MoveNext: optional UniTask.WaitUntil at PlayerLoopTiming 8 with original token and cancelImmediately=false, followed by original lookup. Explicit property injection replaces DI. SDK/type and diagnostic adaptations only; reuse profession registration interface for shared host compatibility.",
        limitations=["Node registration and camera binding only; full original Timeline assets, virtual-camera graph/scene, character/bubble/native appearance and formal map input/save flow remain pending."])))
    print("Original story registry: four classes including async lookup and Cinemachine track binder migrated.")

if __name__=="__main__":build()
