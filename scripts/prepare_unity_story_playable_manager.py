"""Migrate the original playable manager and its FIFO prefab pools.

Scope activation, low-memory notifications and scene instantiation are explicit
offline inputs. Playback, reset, return, removal and preload order stay original.
"""
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_story_attachment_runtime import CODE, record

OUTPUT = ROOT / "unity/Assets/Game/Runtime/Official/StoryPlayableManager"
RECEIPT = ROOT / "assets/reference/official-map/map14-story-playable-manager-source-manifest.json"
NAMESPACE = "Zjcs.Official.StoryPlayableManager"

BOUNDARY = '''using System;
using UnityEngine;
namespace Zjcs.Official.StoryPlayableManager
{
    public interface IStoryPlayableInstanceFactory
    {
        GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays);
    }
    // Original scene-container lookup and injection must complete before the
    // borrowed object is exposed. Scope owners provide this explicit boundary.
    public static class StoryPlayableInstantiation
    {
        public static Func<Transform,IStoryPlayableInstanceFactory> Resolve { get; set; }
        public static GameObject Instantiate(Transform scope, GameObject prefab, Transform parent, bool worldPositionStays)
        {
            if(Resolve==null)throw new InvalidOperationException("Original playable scene injection is unbound.");
            var factory=Resolve(scope);
            if(factory==null)throw new InvalidOperationException("Original playable scope has no instance factory.");
            return factory.Instantiate(prefab,parent,worldPositionStays);
        }
    }
}
'''


def build():
    manifest = json.loads((CODE.parent / "code-manifest.json").read_bytes())
    assemblies = [CODE / "managed/decoded" / name for name in
                  ("GameFramework.Runtime.dll", "Framework.dll")]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest["assemblies"][path.name]["decoded_sha256"]:
            raise ValueError("Selected original playable manager assembly changed")
    sources = [CODE / "framework-source" / (name + ".cs") for name in
               ("GameObjectPoolBase", "GameObjectPool", "GameObjectDestoryListener",
                "GamePrefabTypeSetPools", "IGamePrefabTypeSetPools")]
    sources += [CODE / "gameframework-csharp9-source/GameFramework" / (name + ".cs") for name in
                ("MyPlayableManager", "IMyPlayableManager", "PreLoadData")]
    sources += [CODE / "gameframework-csharp9-source" / (name + ".cs") for name in
                ("ActivePlayableManagerInfo", "InactivePlayableManagerInfo")]
    outputs, adaptations = [], []
    for path in sources:
        text = path.read_text(encoding="utf8")
        text = re.sub(r"(?m)^using (?:Framework(?:\.[\w.]+)?|MessagePipe|R3|GameFramework);\n", "", text)
        text, count = logging_adapter(text)
        text = re.sub(r"(?m)^namespace GameFramework\n", "namespace " + NAMESPACE + "\n", text)
        if path.stem in ("GameObjectPool", "MyPlayableManager"):
            # Each original bag contains precisely one subscription. Retain that
            # single subscription and dispose at the same original call site.
            if path.stem == "GameObjectPool":
                text = text.replace("private DisposableBag mDisposableBag;", "private IDisposable mDisposableBag;")
                text = text.replace("Disposable.AddTo<IDisposable>(mPoolTransformGoDestoryListener.OnDestoryed.Subscribe(Destory), ref mDisposableBag);", "mDisposableBag = mPoolTransformGoDestoryListener.OnDestoryed.Subscribe(Destory);")
                text = text.replace("((Component)(object)mPoolTransform).GetOrAddComponent<GameObjectDestoryListener>()", "mPoolTransform.gameObject.AddComponent<GameObjectDestoryListener>()")
                text, n1 = re.subn(r"public GameObjectPool\(Transform rootSlot, T component\)\n\t\{.*?\n\t\}",
                    "public GameObjectPool(Transform rootSlot, T component)\n\t\t: this(rootSlot, (object)component != null ? component.gameObject : null) { }", text, flags=re.S)
                text, n2 = re.subn(r"public GameObjectPool\(Transform rootSlot, Transform goSlot, T component\)\n\t\{.*?\n\t\}",
                    "public GameObjectPool(Transform rootSlot, Transform goSlot, T component)\n\t\t: this(rootSlot, goSlot, (object)component != null ? component.gameObject : null) { }", text, flags=re.S)
                assert (n1, n2) == (1, 1)
            else:
                text = text.replace("SceneScopeBackgroundServiceBase, IMapScopeBackgroundService, IFightScopeBackgroundService, IHomeScopeBackgroundService, IMyPlayableManager, IPoolObjectPrepareLoad", "IMyPlayableManager, IDisposable")
                mappings = {
                    "private IServiceProvider mServiceProvider": "public IStoryPlayableInstanceFactory InstanceFactory",
                    "private IScopedGameObjectProxy mScopeGameObjectProxy": "public GameObject ScopeRoot",
                    "private IPublisher<ActivePlayableManagerInfo> mActivePlayableManagerSubscriber": "public Action<ActivePlayableManagerInfo> ActivePublisher",
                    "private IPublisher<InactivePlayableManagerInfo> mInactivePlayableManagerSubscriber": "public Action<InactivePlayableManagerInfo> InactivePublisher",
                    "private IApplicationService mApplicationService": "public GAction LowMemory",
                    "private IMyPlayableCenter MyPlayableCenter": "public IMyPlayableCenter MyPlayableCenter",
                }
                for old, new in mappings.items():
                    assert text.count(old) == 1, old
                    text = text.replace(old, new)
                text = text.replace("\t\t[Inject]\n", "")
                text = text.replace("protected override void ", "public void ")
                text = text.replace("\t\t\tbase.Initialize();\n", "").replace("\t\t\tbase.Dispose();\n", "")
                text = text.replace("private DisposableBag mDisposableBag;", "private IDisposable mDisposableBag;")
                text = text.replace("Disposable.AddTo<IDisposable>(mApplicationService.OnLowMemory.Subscribe(MemoryManager_OnLowMemory), ref mDisposableBag);", "mDisposableBag = LowMemory.Subscribe(MemoryManager_OnLowMemory);")
                text = text.replace("mScopeGameObjectProxy.GameObject.transform", "ScopeRoot.transform")
                text = text.replace("mActivePlayableManagerSubscriber.Publish(", "ActivePublisher.Invoke(")
                text = text.replace("mInactivePlayableManagerSubscriber.Publish(", "InactivePublisher.Invoke(")
                text = text.replace("ServiceProviderExtensions.Instantiate(mServiceProvider, prefab, mSlot, false)", "InstanceFactory.Instantiate(prefab, mSlot, false)")
        text = text.replace("ServiceProviderExtensions.Instantiate(((Component)(object)rootNode).GetSceneContainer(), goPrefab, slot, false)", "StoryPlayableInstantiation.Instantiate(rootNode, goPrefab, slot, false)")
        if any(token in text for token in ("_002Ector", "ServiceProviderExtensions", "private DisposableBag", "Disposable.AddTo", "[Inject]", "GetSceneContainer")):
            raise ValueError("Unresolved original playable manager dependency: " + path.name)
        if "namespace " + NAMESPACE not in text:
            match = re.search(r"(?m)^public (?:sealed )?(?:class|interface|struct)", text)
            assert match, path.name
            pos = match.start(); text = text[:pos] + "namespace " + NAMESPACE + "\n{\n" + text[pos:] + "\n}\n"
        text = "// Migrated official framework source; see playable manager receipt.\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n" + text
        target = OUTPUT / path.name; write(target, text.encode()); runtime_meta(target)
        outputs.extend([target, Path(str(target) + ".meta")])
        adaptations.append(dict(source=path.name, diagnosticBlocks=count,
            lifecycleAndServiceBoundary=path.stem == "MyPlayableManager",
            singleSubscriptionBoundary=path.stem in ("MyPlayableManager", "GameObjectPool")))
    target = OUTPUT / "StoryPlayableInstantiation.cs"; write(target, BOUNDARY.encode()); runtime_meta(target)
    outputs.extend([target, Path(str(target) + ".meta")])
    asmdef = OUTPUT / (NAMESPACE + ".asmdef")
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE,
        references=["Zjcs.Official.StoryPlayables"], autoReferenced=True)))
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs.extend([asmdef, Path(str(asmdef) + ".meta"), Path(str(OUTPUT) + ".meta")])
    editor = ROOT / "unity/Assets/Game/Editor/Map14/PlayableManager"
    if editor.is_dir():
        outputs.extend(p for p in editor.rglob("*") if p.is_file())
        outputs.append(Path(str(editor) + ".meta"))
    write(RECEIPT, encode(dict(manifest="871_156598", mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources],
        dependencies=[record(ROOT / "assets/reference/official-map/map14-story-playable-runtime-source-manifest.json")],
        tools=[record(Path(__file__)), record(ROOT / "scripts/prepare_unity_story_playable_runtime.py"),
               record(ROOT / "scripts/prepare_unity_story_sound_runtime.py")],
        outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation="Copy all original manager, FIFO prefab pool, destroy-listener, preload and activation-message methods. Preserve borrow/reset/init/destroy-callback/play order, return reset/clear/deactivate/reparent/enqueue order, removal reentrancy guard, exception recovery, original empty-active early return, low-memory release and preload extension. Replace scoped base lifecycle and DI/R3/MessagePipe entry points with explicit caller-owned lifecycle, native scope factory, original GAction low-memory input, typed activation publishers, and each single IDisposable subscription at its original disposal site. Repair only two decompiled constructor forwarders and diagnostics.",
        limitations=["Scope lifecycle, factory injection, native catalog loading and story-node request/wait/skip integration require a consuming host. Complete map/actor/audio/save and Windows Player remain pending."])))
    print("Original playable manager/pools migrated:", len(sources), "framework files.")


if __name__ == "__main__":
    build()
