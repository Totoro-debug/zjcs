"""Port official scene renderer application code across explicit SDK/resource ports."""
from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / ".tools/source-packages/current/884_156692"
SOURCE = BASE / "official-code/client-source"
DEST = ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI"


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def replace(text, before, after):
    if before not in text:
        raise ValueError("Official application source changed: " + before)
    return text.replace(before, after)


def common(path):
    text = path.read_text(encoding="utf-8-sig")
    text = re.sub(r"^using (?:AppearanceSystem[^;]*|Framework[^;]*|R3|Spine[^;]*);\n", "", text, flags=re.M)
    text = re.sub(r"namespace AppearanceSystem[^;]*;", "namespace Zjcs.Official.ProfessionUI\n{\nusing Object = UnityEngine.Object;", text)
    text = re.sub(r"^\s*//IL_[^\n]*\n", "\n", text, flags=re.M)
    text = text.replace("Object.op_Implicit((Object)(object)TextureLoader)", "TextureLoader")
    text = text.replace("Object.op_Implicit((Object)(object)mMaterialOverride)", "mMaterialOverride")
    return text.rstrip() + "\n}\n"


def main():
    code_receipt = BASE / "code-manifest.json"
    code = json.loads(code_receipt.read_bytes())
    assembly = BASE / "official-code/managed/decoded/Assembly-CSharp.dll"
    assert code["manifest"] == "884_156692"
    assert identity(assembly)["sha256"] == code["assemblies"]["Assembly-CSharp.dll"]["decoded_sha256"]
    inputs = [SOURCE / name for name in (
        "AppearanceSystem/AppearanceRendererLite.cs",
        "AppearanceSystem.Runtime/SkeletonRendererTextureLoadHandler.cs",
        "AppearanceSystem.SpineExt/AppearanceRendererLiteMaterialOverride.cs",
        "AppearanceSystem.Runtime/AppearanceAddressUtils.cs",
        "AppearanceSystem.Runtime/AppearanceAnimUtils.cs")]
    text = common(inputs[0])
    text = replace(text, "public class AppearanceRendererLite : MonoBehaviour", "public class AppearanceRendererLite : MonoBehaviour, IProfessionAppearanceUnloadable")
    text = replace(text, "public SkeletonRenderer SkeletonRenderer;", "public Component SkeletonRenderer;")
    text = replace(text, "List<CustomRenderExistingMesh>", "List<Component>")
    text = replace(text, "private DisposableBag mDisposableBag;", "private List<System.IDisposable> mDisposableBag;")
    start = text.index("\t[Inject]")
    end = text.index("\n\tpublic YooAssetTextureLoader TextureLoader", start)
    text = text[:start] + '''    private IProfessionAppearanceUILiteBindings mBindings;
    private IProfessionAppearanceUILiteBindings Bindings => mBindings ?? ProfessionAppearanceUILiteBindings.Required;
    private IAppearanceAssetManager AppearanceAssetManager => (IAppearanceAssetManager)Bindings.AssetManager;
    private IProfessionAppearanceRendererLite RendererGraph => (IProfessionAppearanceRendererLite)SkeletonRenderer;
    public void Configure(IProfessionAppearanceUILiteBindings bindings)
    { mBindings = bindings ?? throw new System.ArgumentNullException(nameof(bindings)); }
''' + text[end:]
    text = replace(text, "string textureLoaderPath = AppearanceAddressUtils.GetTextureLoaderPath((ISkeletonComponent)(object)SkeletonRenderer, LevelOfResolution, AppearanceConfig.Value);", '''string name = RendererGraph.SkeletonDataName;
                string skeletonPath = Bindings.Config.GetSkeletonPath(name);
                string textureLoaderPath = $"{skeletonPath}/{name}_TextureLoader_{LevelOfResolution}.asset";''')
    text = replace(text, "mTextureLoader = Disposable.AddTo<AssetOperationHandle<YooAssetTextureLoader>>(ResourceLoader.LoadAsset<YooAssetTextureLoader>(textureLoaderPath), ref mDisposableBag).Result;", '''var handle = Bindings.LoadAsset(textureLoaderPath);
                if (mDisposableBag == null) mDisposableBag = new List<System.IDisposable>();
                mDisposableBag.Add(handle);
                mTextureLoader = (YooAssetTextureLoader)handle.Result;''')
    text = replace(text, "GetComponent<SkeletonRenderer>()", "GetComponent<IProfessionAppearanceRendererLite>() as Component")
    text = replace(text, "mDisposableBag.Dispose();", "if (mDisposableBag != null) foreach (var handle in mDisposableBag) handle.Dispose();")
    text = replace(text, "SkeletonRenderer.Initialize(false, false);", "RendererGraph.Initialize(false, false);")
    text = replace(text, "AppearanceAnimUtils.UpdateAnimation(SkeletonRenderer);", "RendererGraph.UpdateAnimationAndMesh();")
    text = replace(text, "AppearanceAssetManager, SkeletonRenderer, TextureLoader, mMaterials", "AppearanceAssetManager, RendererGraph, TextureLoader, mMaterials")
    text = replace(text, "SkeletonRenderer.separatorSlots.Count", "RendererGraph.SeparatorSlotCount")
    text = replace(text, "SkeletonRenderer.CustomMaterialOverride", "RendererGraph.CustomMaterialOverride")
    text = replace(text, "SkeletonRenderer.Skeleton.Skin", "RendererGraph.Skeleton.Skin")
    text = replace(text, "CollectSkinMaterials(Skin skin)", "CollectSkinMaterials(IProfessionAppearanceSkin skin)")
    start = text.index("\t\t\tforeach (SkinEntry attachment2 in skin.Attachments)")
    end = text.index("\n\t\t\tmSkinMaterials = list.ToArray();", start)
    text = text[:start] + '''            foreach (Material item in RendererGraph.SkinMaterials(skin))
                if (!list.Contains(item)) list.Add(item);''' + text[end:]
    text = replace(text, "foreach (CustomRenderExistingMesh renderExistingMesh in RenderExistingMeshes)", "foreach (Component component in RenderExistingMeshes)")
    text = replace(text, "renderExistingMesh.AddRuntimeReplacementMaterial", "((IProfessionAppearanceMaterialReplacement)component).AddRuntimeReplacementMaterial")
    text = replace(text, "renderExistingMesh.ResetRuntimeReplacementMaterials", "((IProfessionAppearanceMaterialReplacement)component).ResetRuntimeReplacementMaterials")
    outputs = {"AppearanceRendererLite.cs": text}
    text = common(inputs[1])
    text = re.sub(r"\bSkeletonRenderer\b", "IProfessionAppearanceRendererLite", text)
    text = replace(text, "!((Object)(object)skeletonRenderer == (Object)null)", "skeletonRenderer != null && skeletonRenderer.Component")
    text = replace(text, "(Object)(object)skeletonRenderer", "skeletonRenderer.Component")
    text = replace(text, "(Object)(object)mSkeletonRenderer", "mSkeletonRenderer?.Component")
    text = replace(text, "!((Object)(object)textureLoader == (Object)null)", "textureLoader")
    text = replace(text, "((OnDemandTextureLoader)mTextureLoader).RequestLoadMaterialTextures", "mTextureLoader.RequestLoadMaterialTextures")
    outputs["SkeletonRendererTextureLoadHandler.cs"] = text
    text = replace(common(inputs[2]), "public SkeletonRenderer SkeletonRenderer;", "public Component SkeletonRenderer;")
    outputs["AppearanceRendererLiteMaterialOverride.cs"] = text
    for name, text in outputs.items():
        path = DEST / name
        path.write_text(text, encoding="utf-8", newline="\n")
        meta = path.with_name(path.name + ".meta")
        if not meta.exists():
            meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.sha256(path.relative_to(ROOT).as_posix().encode()).hexdigest()[:32] + "\n", encoding="utf-8", newline="\n")
    ports = DEST / "ProfessionAppearanceRendererLiteBindings.cs"
    meta = ports.with_name(ports.name + ".meta")
    if not meta.exists():
        meta.write_text("fileFormatVersion: 2\nguid: " + hashlib.sha256(ports.relative_to(ROOT).as_posix().encode()).hexdigest()[:32] + "\n", encoding="utf-8", newline="\n")
    proof = dict(manifest=code["manifest"], code_receipt=identity(code_receipt), assembly=identity(assembly),
        official_application_sources=[identity(path) for path in inputs],
        migrated_application_and_ports=[identity(DEST / name) for name in (*outputs, ports.name)],
        existing_dependencies=[identity(DEST / name) for name in (
            "AppearanceAssetManager.cs", "IAppearanceAssetManager.cs", "YooAssetTextureLoader.cs",
            "AppearanceSkeletonPart.cs", "SkeletonRendererFollower.cs", "BaseSkeletonFollower.cs")],
        validation=identity(ROOT / "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneWeapons.cs"),
        generator=identity(Path(__file__).resolve()),
        adaptations=["SDK component calls and skin attachment atlas-page lookup use explicit ports; no SDK implementation is copied.",
            "Framework resource handles and disposable bag use the existing packaged resource port and IDisposable lifetimes.",
            "Original initialization, material deduplication, shared/independent selection, texture demand/refcounts, replacement and destruction order are retained."],
        limits=["The linked Play Mode fixture reconstructs the selected original four-node hierarchies across explicit native diagnostic endpoints; it is not a production prefab/backend.",
            "Production renderer depth, complete physics/mixing and gameplay/Windows Player integration remain required."])
    target = ROOT / "assets/reference/battle-animation/scene-renderer-runtime-source-manifest.json"
    target.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Migrated 3 original application classes and their renderer/material ports.")


if __name__ == "__main__":
    main()
