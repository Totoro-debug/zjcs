"""Port the selected original scene application lifecycle across explicit backend ports."""
from __future__ import annotations

import json
import re
import uuid
from pathlib import Path

from prepare_battle_scene_renderer_runtime import BASE, ROOT, SOURCE, identity, replace
from prepare_unity_profession_character_resources import guid, meta as create_meta

DEST = ROOT / "unity/Assets/Game/Runtime/Official/CombatAppearance"


def meta(path, folder=False):
    target = path.with_name(path.name + ".meta")
    expected = uuid.uuid5(uuid.NAMESPACE_URL,
        "zjcs/profession/character-resource/" + path.relative_to(ROOT).as_posix()).hex
    if target.exists():
        assert guid(path) == expected, "Review changed scene script/assembly identity: " + str(path)
    else:
        create_meta(path, folder=folder)


def write_changed(path, text):
    raw = text.encode("utf-8")
    if not path.exists() or path.read_bytes().replace(b"\r\n", b"\n") != raw:
        path.write_bytes(raw)


def common(path):
    text = path.read_text(encoding="utf-8-sig")
    text = re.sub(r"^using (?:Framework[^;]*|R3|Spine[^;]*);\n", "", text, flags=re.M)
    text = replace(text, "namespace AppearanceSystem.Runtime;", "namespace Zjcs.Official.CombatAppearance\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.ProfessionUI;")
    text = re.sub(r"^\s*//IL_[^\n]*\n", "\n", text, flags=re.M)
    text = re.sub(r"(\w+)\.Deconstruct\(ref (\w+), ref (\w+)\);", r"\2 = \1.Key; \3 = \1.Value;", text)
    return text.rstrip() + "\n}\n"


def spine(path):
    text = common(path)
    text = replace(text, "AppearancePartHandler mPartHandler", "Component mPartHandler")
    text = replace(text, "ISkeletonComponent", "IProfessionAppearanceSkeletonComponent")
    text = re.sub(r"\bSkeletonDataAsset\b", "ScriptableObject", text)
    text = replace(text, "SpineAnimationSplitBinarySO", "ScriptableObject")
    text = replace(text, "public ScriptableObject ScriptableObject =>", "public ScriptableObject SkeletonDataAsset =>")
    text = replace(text, "private DisposableBag mAssetHandleBag;", "private List<IDisposable> mAssetHandleBag;")
    start = text.index("\t[Inject]")
    end = text.index("\n\tpublic SkinTextureAsset SkinTextureAsset", start)
    text = text[:start] + '''    private IProfessionAppearanceSceneBindings mBindings;
    protected IProfessionAppearanceSceneBindings Bindings => mBindings ?? ProfessionAppearanceSceneBindings.Required;
    public IProfessionAppearanceSceneBindings ResourceLoader => Bindings;
    public void Configure(IProfessionAppearanceSceneBindings bindings, AssetGuidProxy<SkinTextureAsset> skin,
        AssetGuidProxy<YooAssetTextureLoader> loader, Component partHandler)
    {
        mBindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
        SkinTextureAssetGuid = skin ?? throw new ArgumentNullException(nameof(skin));
        TextureLoaderGuid = loader ?? throw new ArgumentNullException(nameof(loader));
        mPartHandler = partHandler;
    }
''' + text[end:]
    text = replace(text, "((Component)this).GetComponent<IProfessionAppearanceSkeletonComponent>()", "Bindings.GetSkeletonComponent(this)")
    text = replace(text, "private Skin mSkin;", "private IProfessionAppearanceSkin mSkin;")
    text = replace(text, 'new Skin("combineSkin")', 'Bindings.CreateSkin("combineSkin")')
    text = replace(text, "SkeletonData skeletonData = mSkeletonDataAsset.GetSkeletonData(true);", "IProfessionAppearanceSkeletonData skeletonData = Bindings.GetSkeletonData(mSkeletonDataAsset, true);")
    text = replace(text, "Skin val = skeletonData.FindSkin(skin);", "IProfessionAppearanceSkin val = skeletonData.FindSkin(skin);")
    text = replace(text, "Skin val = mSkin;", "IProfessionAppearanceSkin val = mSkin;")
    text = replace(text, "SetSkeletonSkins(Skin newSkin)", "SetSkeletonSkins(IProfessionAppearanceSkin newSkin)")
    text = replace(text, "Skeleton skeleton =", "IProfessionAppearanceSkeleton skeleton =")
    text = replace(text, "!Object.op_Implicit((Object)(object)skeletonDataAsset)", "!skeletonDataAsset")
    text = replace(text, "Object.op_Implicit((Object)(object)mSkeletonDataAsset)", "mSkeletonDataAsset")
    # Replace only logger scaffolding, retaining each original guard and return.
    start = text.index("\t\t\tGLoggerErrorInterpolatedStringHandler")
    end = text.index("\n\t\t}", start)
    text = text[:start] + '            Debug.LogError("OnSkinUpdate Error\\n" + exception);' + text[end:]
    start = text.index("\t\t\tGLoggerErrorInterpolatedStringHandler")
    end = text.index("\n\t\t}\n\t\telse if", start)
    text = text[:start] + '            Debug.LogError("skeleton asset is null");' + text[end:]
    start = text.index("\t\t\t\tGLoggerWarnInterpolatedStringHandler")
    end = text.index("\n\t\t\t}\n\t\t\telse", start)
    text = text[:start] + '                Debug.LogWarning("skin is not exist:" + skin);' + text[end:]
    start = text.index("\t\t\tGLoggerWarnInterpolatedStringHandler")
    end = text.index("\n\t\t\treturn;", start)
    text = text[:start] + '            Debug.LogWarning("不存在指定的皮肤材质:" + skinName);' + text[end:]
    start = text.index("\t\t\tGLoggerErrorInterpolatedStringHandler")
    end = text.index("\n\t\t\treturn null;", start)
    text = text[:start] + '            Debug.LogError("The skinName is not exist:" + skinName);' + text[end:]
    for field, kind, guid in (("mTextureLoader", "YooAssetTextureLoader", "TextureLoaderGuid"),
                             ("mSkinTextureAsset", "SkinTextureAsset", "SkinTextureAssetGuid")):
        before = f"{field} = Disposable.AddTo<AssetOperationHandle<{kind}>>(ResourceLoader.LoadAssetByGuid<{kind}>({guid}.AssetGuid), ref mAssetHandleBag).Result;"
        after = f'''var handle = ResourceLoader.LoadAssetByGuid({guid}.AssetGuid);
            if (mAssetHandleBag == null) mAssetHandleBag = new List<IDisposable>();
            mAssetHandleBag.Add(handle);
            {field} = ({kind})handle.Result;'''
        text = replace(text, before, after)
    text = replace(text, "mAssetHandleBag.Dispose();", "if (mAssetHandleBag != null) foreach (var handle in mAssetHandleBag) handle.Dispose();")
    start = text.index("\t\t\tSlot[] items =")
    end = text.index("\n\t\t}\n\t\tskeleton.UpdateCache();", start)
    text = text[:start] + '''            for (int i = 0, count = skeleton.SlotCount; i < count; i++)
            {
                string attachmentName = skeleton.SetupAttachmentName(i);
                if (attachmentName != null)
                {
                    object attachment = newSkin.GetAttachment(i, attachmentName);
                    if (attachment != null) skeleton.SetSlotAttachment(i, attachment);
                }
            }''' + text[end:]
    return text


def renderer(path):
    text = common(path)
    text = replace(text, "private SkeletonRenderer mSkeletonRenderer;", "private Component mSkeletonRenderer;")
    text = replace(text, "\t[Inject]\n\tprivate IAppearanceAssetManager AppearanceAssetManager { get; set; }", '''    private IAppearanceAssetManager AppearanceAssetManager => Bindings.AssetManager;
    private IProfessionAppearanceSceneRenderer RendererGraph => (IProfessionAppearanceSceneRenderer)mSkeletonRenderer;''')
    text = replace(text, "InitSkeleton(SkeletonDataAsset skeletonDataAsset)", "InitSkeleton(ScriptableObject skeletonDataAsset)")
    text = replace(text, "GetComponent<SkeletonRenderer>()", "GetComponent<IProfessionAppearanceSceneRenderer>() as Component")
    text = replace(text, "\t\tGLoggerErrorInterpolatedStringHandler message;\n\t\tbool enabled;\n", "")
    start = text.index("\t\t\tmessage = new GLoggerErrorInterpolatedStringHandler")
    end = text.index("\n\t\t\tthrow;", start)
    text = text[:start] + '            Debug.LogError(name + " GetComponent,but gameObject is null\\n" + exception);' + text[end:]
    start = text.index("\t\t\tmessage = new GLoggerErrorInterpolatedStringHandler")
    end = text.index("\n\t\t}\n\t\telse", start)
    text = text[:start] + '            Debug.LogError("SkeletonRenderer is null! gameObject:" + gameObject.name);' + text[end:]
    text = replace(text, "mSkeletonRenderer.skeletonDataAsset", "RendererGraph.skeletonDataAsset")
    text = replace(text, "mSkeletonRenderer.Initialize", "RendererGraph.Initialize")
    text = replace(text, "mSkeletonRenderer.OnGetUpdatedSharedMaterials", "RendererGraph.OnGetUpdatedSharedMaterials")
    text = replace(text, "AppearanceAssetManager, mSkeletonRenderer, mTextureLoader", "AppearanceAssetManager, RendererGraph, mTextureLoader")
    text = replace(text, "mSkeletonRenderer.LateUpdateMesh()", "RendererGraph.LateUpdateMesh()")
    before = "partHandler.OnGetSharedMaterialDelegate = (SkeletonPartsRendererSharedMaterialDelegate)Delegate.Combine((Delegate)(object)partHandler.OnGetSharedMaterialDelegate, (Delegate)new SkeletonPartsRendererSharedMaterialDelegate(OnGetUpdatedSharedMaterials));"
    text = replace(text, before, "partHandler.OnGetSharedMaterialDelegate += OnGetUpdatedSharedMaterials;")
    return text


def mecanim(path):
    text = common(path)
    text = replace(text, "[RequireComponent(typeof(SkeletonMecanim))]\n", "")
    text = replace(text, "private SkeletonMecanim mSkeletonMecanim;", "private IProfessionAppearanceSceneMecanim mSkeletonMecanim;")
    text = replace(text, "SkeletonMecanimCacheHelper", "IProfessionAppearanceSceneCache")
    text = replace(text, "\t[Inject]\n\tprivate IBoneTransformCacheManager BoneTransformCacheManager { get; set; }\n", "")
    text = replace(text, "public SkeletonMecanim SkeletonMecanim", "public IProfessionAppearanceSceneMecanim SkeletonMecanim")
    text = replace(text, "(Object)(object)mSkeletonMecanim == (Object)null", "mSkeletonMecanim == null")
    text = replace(text, "GetComponent<SkeletonMecanim>()", "GetComponent<IProfessionAppearanceSceneMecanim>()")
    text = replace(text, "((Component)SkeletonMecanim).GetComponent<Animator>()", "SkeletonMecanim.Animator")
    text = replace(text, "new IProfessionAppearanceSceneCache(BoneTransformCacheManager, SkeletonMecanim)", "Bindings.CreateMecanimCache(SkeletonMecanim)")
    return text


def main():
    code_receipt = BASE / "code-manifest.json"
    code = json.loads(code_receipt.read_bytes())
    assembly = BASE / "official-code/managed/decoded/Assembly-CSharp.dll"
    assert code["manifest"] == "884_156692"
    assert identity(assembly)["sha256"] == code["assemblies"]["Assembly-CSharp.dll"]["decoded_sha256"]
    DEST.mkdir(parents=True, exist_ok=True)
    meta(DEST, folder=True)
    sources, outputs = [], []
    for name, adapt in (("AppearanceSpine", spine), ("AppearanceRenderer", renderer), ("AppearanceMecanim", mecanim)):
        source = SOURCE / "AppearanceSystem.Runtime" / (name + ".cs")
        text = adapt(source)
        for forbidden in ("[Inject]", "GLogger", "using Spine", "Object.op_", "Disposable.AddTo", "IBoneTransformCacheManager"):
            assert forbidden not in text, (name, forbidden)
        path = DEST / (name + ".cs")
        write_changed(path, text)
        meta(path)
        sources.append(identity(source))
        outputs.append(identity(path))
    ports = DEST / "ProfessionAppearanceSceneBindings.cs"
    meta(ports)
    assembly_reference = DEST / "Zjcs.Official.CombatAppearance.asmref"
    meta(assembly_reference)
    validation = ROOT / "unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneAppearance.cs"
    meta(validation)
    outputs.append(identity(ports))
    outputs.append(identity(assembly_reference))
    proof = dict(manifest=code["manifest"], code_receipt=identity(code_receipt), assembly=identity(assembly),
        official_application_sources=sources,
        shared_original_application_sources=[identity(SOURCE / "AppearanceSystem.Runtime" / name) for name in (
            "SkinTextureAsset.cs", "SkinTextureConfig.cs", "IAppearanceComponent.cs")],
        migrated_application_and_ports=outputs, generator=identity(Path(__file__).resolve()),
        generator_helpers=[identity(ROOT / "scripts/prepare_battle_scene_renderer_runtime.py"),
            identity(ROOT / "scripts/prepare_unity_profession_character_resources.py")],
        shared_dependencies=[identity(ROOT / "unity/Assets/Game/Runtime/Official/ProfessionUI" / name) for name in (
            "ProfessionAppearanceSpineBindings.cs", "ProfessionAppearanceRendererLiteBindings.cs",
            "ProfessionAppearanceRendererPartBindings.cs", "SkeletonRendererTextureLoadHandler.cs", "IAppearanceComponent.cs",
            "SkinTextureAsset.cs", "SkinTextureConfig.cs", "AssetGuidProxy.cs")],
        validation=identity(validation),
        operation="Copy the current original application classes in a separate namespace from the historical UI base. Preserve GUID loading, skin and material lifecycles, array callbacks, texture demand and Mecanim/cache call ordering; replace only framework/SDK operations with explicit resource, skeleton, renderer and cache ports.",
        limits=["This ports application behavior; the production renderer/Mecanim/cache backends, current texture/material resources, scene prefab, complete physics/mixing and formal battle/Windows Player binding remain required."])
    target = ROOT / "assets/reference/battle-animation/scene-appearance-runtime-source-manifest.json"
    target.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print("Migrated the 3 current original scene appearance application classes without changing the historical UI base.")


if __name__ == "__main__":
    main()
