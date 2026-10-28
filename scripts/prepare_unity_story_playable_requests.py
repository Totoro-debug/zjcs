"""Migrate original story playable request bodies and their exact waits.

Graph lifecycle, value ports, entity views, prefab selection and resource loading
are explicit inputs. The original graph executor/server protocol is not replaced
by an invented base class implementation.
"""
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_story_attachment_runtime import CODE, record

OUTPUT = ROOT / "unity/Assets/Game/Runtime/Official/StoryPlayableRequests"
RECEIPT = ROOT / "assets/reference/official-map/map14-story-playable-requests-source-manifest.json"
NAMESPACE = "Zjcs.Official.StoryPlayableRequests"

CONTRACTS = '''using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.StoryPlayableRequests
{
    public interface IStoryPrefabAssetProxy { GameObject Load(); void Unload(); }
    public interface IStoryHandleProxy
    {
        MyPlayable PlayPlayable(GameObject prefab,GameObject source,GameObject target,float speed=1f,GameObject parentGo=null,Dictionary<string,object> param=null,IMyPlayableNeedStop needStopObj=null);
        IStoryPrefabAssetProxy GetStoryRegisterAssetProxy(string key);
    }
    public interface IStoryPlayableEntityVisibility { void SetForceVisible(bool visible); }
    public interface IStoryWorldStagePlayableSupport { void Stop(); void Start(); }
    public sealed class StoryPlayableEntityBinding:MonoBehaviour
    {
        public IStoryPlayableEntityVisibility ClientVisibilitySupporter { get; set; }
    }
    // Consumed node-body boundary. The real graph owner must supply lifecycle,
    // dynamic input ports and original EC view lookups; absent bindings fail.
    public abstract class StoryPlayableRequestNode
    {
        public Action StartContext,EndContext,CleanContext;
        public Func<string,object> InputValue,InputValues;
        public Func<StoryPlayableEntityBinding,IStoryWorldStagePlayableSupport> GetWorldStageSupport;
        public virtual void OnStart()=>StartContext.Invoke();
        public virtual void OnEnd()=>EndContext.Invoke();
        public virtual void CleanWhileStoryAllEnd()=>CleanContext.Invoke();
        protected T GetInputValue<T>(string key)=>(T)InputValue(key);
        protected T[] GetInputValues<T>(string key)=>(T[])InputValues(key);
        // Exact original ClientRunNode continuation bodies (no graph transfer).
        protected virtual UniTask<StoryRunNodeResultCode> InternalRun(CancellationToken token)=>new UniTask<StoryRunNodeResultCode>(StoryRunNodeResultCode.OK);
        protected virtual UniTask<StoryRunNodeResultCode> InternalSkip(CancellationToken token)=>new UniTask<StoryRunNodeResultCode>(StoryRunNodeResultCode.OK);
        protected virtual UniTask<StoryRunNodeResultCode> AsyncTransRunToSkip(CancellationToken token)=>new UniTask<StoryRunNodeResultCode>(StoryRunNodeResultCode.OK);
        protected virtual StoryRunNodeResultCode GetCodeForTimeout()=>StoryRunNodeResultCode.SafeEnd;
        public StoryRunNodeResultCode TimeoutResult=>GetCodeForTimeout();
        public UniTask<StoryRunNodeResultCode> RunAsync(CancellationToken token)=>InternalRun(token);
        public UniTask<StoryRunNodeResultCode> SkipAsync(CancellationToken token)=>InternalSkip(token);
        public UniTask<StoryRunNodeResultCode> TransitionToSkipAsync(CancellationToken token)=>AsyncTransRunToSkip(token);
    }
}
'''


def method(text, signature):
    start = text.index(signature); opening = text.index("{", start); depth = 1; pos = opening + 1
    # Selected methods contain no braces in strings after diagnostic adaptation.
    while depth:
        depth += (text[pos] == "{") - (text[pos] == "}"); pos += 1
    return text[start:pos]


def request(text):
    source = text
    start = text.index("\t[Tooltip(", text.index("private struct _003CInternalRun"))
    text = text[:text.index("\t[StructLayout(")] + text[start:]
    start = text.index("\t[AsyncStateMachine("); end = text.index("\tprotected override UniTask<StoryRunNodeResultCode> InternalSkip", start)
    for token in ("DoRun();", "mPlayables.Count", "mRunningPlayableCount <= 0", "TimeSpan.FromSeconds(20.0)",
                  "(PlayerLoopTiming)8, token, false", "(DelayType)0", "enumerator.Current.OnStop +=", 
                  ").InternalRun(token).GetAwaiter()"):
        assert token in source, token
    text = text[:start] + '''\tprotected override async UniTask<StoryRunNodeResultCode> InternalRun(CancellationToken token)
\t{
\t\tDoRun();
\t\tmRunningPlayableCount = mPlayables.Count;
\t\tif (IsWaitPlayableStop && mPlayables != null)
\t\t{
\t\t\tforeach (MyPlayable playable in mPlayables) playable.OnStop += new Action<MyPlayable>(OnPlayableStop);
\t\t\tif (mRunningPlayableCount > 0)
\t\t\t\tawait UniTask.WaitUntil(() => mRunningPlayableCount <= 0, (PlayerLoopTiming)8, token, false)
\t\t\t\t\t.Timeout(TimeSpan.FromSeconds(20.0), (DelayType)0, (PlayerLoopTiming)8, null);
\t\t}
\t\treturn await base.InternalRun(token);
\t}

''' + text[end:]
    text = text.replace(": ClientRunNode", ": StoryPlayableRequestNode")
    text = text.replace("MyEntity", "StoryPlayableEntityBinding")
    text = text.replace("IResourceLoader", "IProfessionImageResourceLoader")
    text = text.replace("private DisposableBag mDisposableBag;", "private StoryPlayableResourceBag mDisposableBag;")
    text = text.replace("mDisposableBag = default;", "mDisposableBag = new StoryPlayableResourceBag();")
    text = text.replace("Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadAssetByGuid<GameObject>(PlayablesProxy[i].AssetGuid), ref mDisposableBag)", "mDisposableBag.Add(ResourceLoader.LoadAssetByGuid<GameObject>(PlayablesProxy[i].AssetGuid))")
    text = text.replace("private IStoryHandleProxy StoryHandleProxy", "public IStoryHandleProxy StoryHandleProxy")
    text = text.replace("StoryUtils.GetEntityPlayableSupportView(sourceEntity)?.StopPlayableHandler<ECPlayableWorldStageHandler>()", "GetWorldStageSupport(sourceEntity)?.Stop()")
    text = text.replace("StoryUtils.GetEntityPlayableSupportView(targetEntity)?.StopPlayableHandler<ECPlayableWorldStageHandler>()", "GetWorldStageSupport(targetEntity)?.Stop()")
    text = text.replace("StoryUtils.GetEntityPlayableSupportView(sourceEntity)?.StartPlayableHandler<ECPlayableWorldStageHandler>()", "GetWorldStageSupport(sourceEntity)?.Start()")
    text = text.replace("StoryUtils.GetEntityPlayableSupportView(targetEntity)?.StartPlayableHandler<ECPlayableWorldStageHandler>()", "GetWorldStageSupport(targetEntity)?.Start()")
    pattern = re.compile(r"(?m)^(?P<indent>[ \t]*)ILogger logger = [^\n]+;\n(?:(?P=indent)[^\n]*\n|(?P=indent)\t[^\n]*\n)*?(?P=indent)GZLoggerExtensions.Error\([^\n]+;")
    text, count = pattern.subn(lambda m: m["indent"] + 'UnityEngine.Debug.LogError("[Story] PlayableDura config error: " + PlayableDura);', text)
    assert count == 1
    return text


def build():
    manifest = json.loads((CODE.parent / "code-manifest.json").read_bytes())
    assembly = CODE / "managed/decoded/Assembly-CSharp.dll"
    assert sha(assembly.read_bytes()) == manifest["assemblies"][assembly.name]["decoded_sha256"]
    names = ("PlayPlayableNode", "PlayPlayableForGameObjectNode", "StoryPlayable", "StoryPlayableSelect",
             "EStoryPlayableSelectType", "StoryRunNodeResultCode")
    sources = [CODE / "client-source" / (name + ".cs") for name in names]
    outputs, adaptations = [], []
    for path in sources:
        text = path.read_text(encoding="utf8")
        if path.stem.startswith("PlayPlayable"):
            text = request(text)
        if path.stem == "StoryPlayable":
            text = text.replace("private IStoryNodeRegisterMgr NodeRegisterManager", "public IStoryNodeRegisterMgr NodeRegisterManager")
            text = text.replace("private IResourceLoader ResourceLoader", "public IProfessionImageResourceLoader ResourceLoader")
            text = text.replace("private DisposableBag mDisposableBag;", "private StoryPlayableResourceBag mDisposableBag;")
            text = text.replace("mDisposableBag = default;", "mDisposableBag = new StoryPlayableResourceBag();")
            text = text.replace("Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadAssetByGuid<GameObject>(Playable.AssetGuid), ref mDisposableBag)", "mDisposableBag.Add(ResourceLoader.LoadAssetByGuid<GameObject>(Playable.AssetGuid))")
            text = text.replace("Object.op_Implicit((Object)(object)mPlayable)", "(Object)(object)mPlayable != (Object)null")
        if path.stem == "StoryPlayableSelect":
            text = text.replace("UnsafeAssetPrefabProxy", "IStoryPrefabAssetProxy")
        text = re.sub(r"(?m)^using (?:Common|Framework(?:\.[\w.]+)?|GameFramework|NaughtyAttributes|R3|XNode|GFrameCore(?:\.[\w.]+)?|Microsoft\.Extensions\.Logging|ZLogger|Cysharp\.Threading\.Tasks\.CompilerServices);\n", "", text)
        text = re.sub(r"(?m)^\s*\[(?:CreateNodeMenu|Input|Inject|CheckStoryRegisterKey|Dropdown)[^\n]*\n", "", text)
        text, diagnostics = logging_adapter(text)
        for token in ("IAsyncStateMachine", "ClientRunNode", "AssetOperationHandle", "Disposable.AddTo", "ZLogger", "[Inject]", "StoryUtils."):
            if token in text: raise ValueError("Unresolved original request dependency: " + path.name + ": " + token)
        match = re.search(r"(?m)^(?:\[Serializable\]|public (?:class|enum))", text); assert match
        pos = match.start(); text = text[:pos] + "namespace " + NAMESPACE + "\n{\n" + text[pos:] + "\n}\n"
        text = "// Migrated original client request body; see playable requests receipt.\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryNodes;\nusing Zjcs.Official.ProfessionUI;\n" + text
        target = OUTPUT / path.name; write(target, text.encode()); runtime_meta(target)
        outputs.extend([target, Path(str(target) + ".meta")]); adaptations.append(dict(source=path.name, diagnosticBlocks=diagnostics,
            asyncRestoredFromOriginalMoveNext=path.stem.startswith("PlayPlayable"), explicitGraphAndEntityBoundary=path.stem.startswith("PlayPlayable")))
    # Reuse the already verified original R3 bag behavior through its existing
    # native resource port, retaining ascending and reentrant disposal semantics.
    bag_source = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI/ProfessionImageResourceBridge.cs"
    bag = bag_source.read_text(encoding="utf8"); start = bag.index("    internal sealed class ProfessionResourceOperationBag")
    bag = bag[start:bag.rfind("}")].replace("internal sealed class ProfessionResourceOperationBag", "internal sealed class StoryPlayableResourceBag")
    target = OUTPUT / "StoryPlayableResourceBag.cs"; write(target, ("using System;\nusing System.Collections.Generic;\nnamespace " + NAMESPACE + "\n{\n" + bag + "}\n").encode()); runtime_meta(target)
    outputs.extend([target, Path(str(target) + ".meta")])
    target = OUTPUT / "StoryPlayableRequestNode.cs"; write(target, CONTRACTS.encode()); runtime_meta(target)
    outputs.extend([target, Path(str(target) + ".meta")])
    proxy_source = CODE / "client-source/StoryHandleProxy.cs"
    proxy, _ = logging_adapter(proxy_source.read_text(encoding="utf8"))
    proxy_methods = [method(proxy, signature) for signature in
        ("public MyPlayable PlayPlayable(", "public UnsafeAssetPrefabProxy GetStoryRegisterAssetProxy(",
         "private void BindMyPlayableManager(", "private void UnbindMyPlayableManager(")]
    proxy_text = "\n\n".join(proxy_methods).replace("UnsafeAssetPrefabProxy", "IStoryPrefabAssetProxy")
    proxy_text = proxy_text.replace("private void ", "public void ")
    proxy_text = proxy_text.replace("mScriptableObjectConfig.Value.GetRegisterProxy(registerKey)", "RegisterProxyLookup(registerKey)")
    target = OUTPUT / "StoryHandleProxy.cs"
    write(target, ("// Selected original client proxy methods; remaining services are outside this port.\n"
        "using System;\nusing System.Collections.Generic;\nusing UnityEngine;\nusing Object=UnityEngine.Object;\n"
        "using Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryPlayableManager;\nnamespace " + NAMESPACE +
        "\n{\npublic class StoryHandleProxy:IStoryHandleProxy\n{\nprivate IMyPlayableManager mPlayableManager;\n"
        "public Func<string,IStoryPrefabAssetProxy> RegisterProxyLookup {get;set;}\n" + proxy_text + "\n}\n}\n").encode())
    runtime_meta(target); outputs.extend([target, Path(str(target) + ".meta")])
    client_base = CODE / "client-source/ClientRunNode.cs"; base = client_base.read_text(encoding="utf8")
    for signature in ("protected override UniTask<StoryRunNodeResultCode> InternalRun", "protected override UniTask<StoryRunNodeResultCode> InternalSkip", "protected override UniTask<StoryRunNodeResultCode> AsyncTransRunToSkip"):
        assert "return new UniTask<StoryRunNodeResultCode>(StoryRunNodeResultCode.OK);" in method(base, signature)
    run_base = CODE / "client-source/RunNode.cs"
    assert "return StoryRunNodeResultCode.SafeEnd;" in method(run_base.read_text(encoding="utf8"), "protected virtual StoryRunNodeResultCode GetCodeForTimeout()")
    asmdef = OUTPUT / (NAMESPACE + ".asmdef"); write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE,
        references=["Zjcs.Official.StoryPlayables", "Zjcs.Official.StoryPlayableManager", "Zjcs.Official.StoryNodes", "Zjcs.Official.ProfessionUI", "UniTask"], autoReferenced=True)))
    runtime_meta(asmdef); runtime_meta(OUTPUT); outputs.extend([asmdef, Path(str(asmdef) + ".meta"), Path(str(OUTPUT) + ".meta")])
    editor = ROOT / "unity/Assets/Game/Editor/Map14/PlayableRequests"
    if editor.is_dir():
        outputs.extend(p for p in editor.rglob("*") if p.is_file()); outputs.append(Path(str(editor) + ".meta"))
    write(RECEIPT, encode(dict(manifest="871_156598", mapClassId=14, assemblies=[record(assembly)],
        inputs=[record(p) for p in [*sources, client_base, run_base, proxy_source, bag_source]],
        dependencies=[record(ROOT / "assets/reference/official-map" / name) for name in
            ("map14-story-playable-manager-source-manifest.json", "map14-story-nodes-runtime-source-manifest.json")],
        tools=[record(Path(__file__)), record(ROOT / "scripts/prepare_unity_story_playable_runtime.py"), record(ROOT / "scripts/prepare_unity_story_sound_runtime.py")],
        outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation="Copy both original playable node bodies, StoryPlayable callback wrapper, selector and enums. Restore emitted async waits exactly: DoRun, count, subscribe after play, optional WaitUntil at PlayerLoopTiming 8 with original token/cancelImmediately=false, original 20-second scaled timeout, then original ClientRunNode OK continuation. Preserve skip/transition stop gates, duration overrides, per-index dynamic source/target ports, forced visibility and world-stage stop/start order, lease disposal, story-end cleanup and selector load/unload branches. Explicit boundaries supply the actual graph lifecycle, EC views, prefab proxy and native resource operations; no substitute graph/server executor is implemented here.",
        limitations=["Node-body boundary requires a consuming real graph owner and actual entity/asset bindings. Formal shared mainline executor still needs integration; map/actor/audio/save and Windows Player remain pending."])))
    print("Original story playable request bodies migrated:", len(sources), "client files.")


if __name__ == "__main__":
    build()
