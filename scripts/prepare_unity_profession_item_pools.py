"""Port original item attachment pools and their framework lifecycle dependencies."""
import hashlib
import json
from pathlib import Path
import re
ROOT = Path(__file__).resolve().parents[1]


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    runtime = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"
    ports = []
    names = {"GAction": "framework-firstpass-source/Framework/GAction.cs",
        "GameObjectDestoryListener": "framework-source/GameObjectDestoryListener.cs",
        "GameObjectPoolBase": "framework-source/GameObjectPoolBase.cs",
        "GameObjectPool": "framework-source/GameObjectPool.cs",
        "ItemIconAttachmentPool": "client-source/ItemIconAttachmentPool.cs",
        "ItemIconAttachmentManager": "client-source/ItemIconAttachmentManager.cs"}
    for name, relative in names.items():
        source = code / relative
        text = source.read_text(encoding="utf-8")
        if name == "GAction":
            text = text[:text.index("public sealed class GAction<T>")]
            text = text.replace("namespace Framework;\n", "")
            text = text.replace("GAction?", "GAction").replace("ObserverNode?", "ObserverNode")
            pattern = r"\t+GLoggerErrorInterpolatedStringHandler message = .*?\n\t+Log.Error\([^\n]+\n"
            text, count = re.subn(pattern, "\t\t\t\t\tUnityEngine.Debug.LogException(exception);\n", text, flags=re.S)
            if count != 1:
                raise ValueError("Inspect original GAction exception boundary")
        else:
            pattern = r"\t+GLogger(?:Warn|Error)InterpolatedStringHandler message = .*?\n\t+Log\.(?:Warn|Error)\([^\n]+\n"
            def logging(match):
                level = "LogError" if "Log.Error(" in match[0] else "LogWarning"
                return '\t\t\tDebug.' + level + '("Original ' + name + ' lifecycle warning/error.");\n'
            text = re.sub(pattern, logging, text, flags=re.S)
        text = re.sub(r"^using (Framework(?:\.[^;]+)?|R3);\n", "", text, flags=re.M)
        text = re.sub(r"^\s*//IL_[^\n]*\n", "\n", text, flags=re.M)
        text = text.replace("AssetOperationHandle<", "IProfessionAssetOperation<").replace("IResourceLoader", "IProfessionImageResourceLoader")
        text = text.replace("private DisposableBag mDisposableBag;", "private readonly ProfessionResourceOperationBag mDisposableBag = new ProfessionResourceOperationBag();")
        text = text.replace("private DisposableBag mDisposable;", "private readonly ProfessionResourceOperationBag mDisposable = new ProfessionResourceOperationBag();")
        text = text.replace("Disposable.AddTo<IDisposable>(mPoolTransformGoDestoryListener.OnDestoryed.Subscribe(Destory), ref mDisposableBag);", "mDisposableBag.Add(mPoolTransformGoDestoryListener.OnDestoryed.Subscribe(Destory));")
        text = text.replace("((Component)(object)mPoolTransform).GetOrAddComponent<GameObjectDestoryListener>()", "mPoolTransform.gameObject.AddComponent<GameObjectDestoryListener>()")
        text = text.replace("ServiceProviderExtensions.Instantiate(((Component)(object)rootNode).GetSceneContainer(), goPrefab, slot, false)", "ProfessionGameObjectPoolBridge.Instantiate(rootNode, goPrefab, slot, false)")
        text = text.replace("ServiceProviderExtensions.Instantiate(((Component)(object)slot).GetSceneContainer(), val, slot, false)", "ProfessionGameObjectPoolBridge.Instantiate(slot, val, slot, false)")
        if name == "GameObjectPool":
            text, count1 = re.subn(r"public GameObjectPool\(Transform rootSlot, T component\)\n\t\{.*?\n\t\}",
                "public GameObjectPool(Transform rootSlot, T component)\n\t\t: this(rootSlot, (object)component != null ? component.gameObject : null) { }", text, flags=re.S)
            text, count2 = re.subn(r"public GameObjectPool\(Transform rootSlot, Transform goSlot, T component\)\n\t\{.*?\n\t\}",
                "public GameObjectPool(Transform rootSlot, Transform goSlot, T component)\n\t\t: this(rootSlot, goSlot, (object)component != null ? component.gameObject : null) { }", text, flags=re.S)
            if count1 != 1 or count2 != 1:
                raise ValueError("Inspect decompiled pool constructor forwarding")
        if name == "ItemIconAttachmentManager":
            text = text.replace("IScriptableObjectConfig<ItemIconAttachmentConfig>", "Func<ItemIconAttachmentConfig>")
            text = text.replace("IApplicationService applicationService", "GAction lowMemory")
            text = text.replace("Disposable.AddTo<IDisposable>(applicationService.OnLowMemory.Subscribe(OnLowMemory), ref mDisposable);", "mDisposable.Add(lowMemory.Subscribe(OnLowMemory));")
            text = text.replace("mConfig.Value.Templates", "mConfig().Templates")
        if "_002Ector" in text or "ServiceProviderExtensions" in text or "GLogger" in text:
            raise ValueError("Original pool dependency remains unresolved: " + name)
        text = "namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;\n" + text + "\n}\n"
        target = runtime / (name + ".cs")
        target.write_text(text, encoding="utf-8", newline="\n")
        meta = target.with_name(target.name + ".meta")
        meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(("zjcs-profession-ui-" + name).encode()).hexdigest() + "\n", encoding="utf-8", newline="\n")
        ports.append(dict(source=record(source), runtime=record(target), meta=record(meta)))
    bridge = runtime / "ProfessionGameObjectPoolBridge.cs"
    meta = bridge.with_name(bridge.name + ".meta")
    meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.md5(b"zjcs-profession-pool-instantiation-bridge").hexdigest() + "\n", encoding="utf-8", newline="\n")
    result = dict(format=1, manifest="871_156598", ports=ports, bridge=record(bridge), bridge_meta=record(meta), generator=record(Path(__file__)),
        validation=record(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionItemAttachmentValidation.cs"),
        operation="Retain original FIFO pool, reset/clear callbacks, inactive pool roots, create/borrow/return order, reverse shrink traversal, low-memory release, config first-match lookup, retained GUID resource handles and disposal order. Port non-generic GAction linked observers/version/reentrancy, original destroy listener and constructor forwarding; replace logging, resource/config/application and scene-injection boundaries with explicit consumed adapters. Newly created pool root has no listener, so direct AddComponent is equivalent to source GetOrAddComponent.",
        limitations=["Application-wide lifecycle/GC policy is external; the manager consumes its original low-memory event only.", "Scene injection must explicitly bind the original item/attachment dependencies before exposure; full profession panel integration remains pending."])
    (ROOT / "assets/reference/profession-ui/item-pools-source-manifest.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Ported six original item pool and lifecycle types")


if __name__ == "__main__":
    build()
