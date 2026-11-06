using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class SkeletonRendererHighTexturePreview : MonoBehaviour
{
    [SerializeField] private SkeletonRenderer mSkeletonRenderer;
    [SerializeField] private YooAssetTextureLoader mTextureLoader;

    private AppearanceCombineSkin mAppearanceCombineSkin;

    private void Awake()
    {
        mSkeletonRenderer = GetComponent<SkeletonRenderer>();
        mAppearanceCombineSkin = GetComponent<AppearanceCombineSkin>();
    }

    private void OnEnable()
    {
        TryUpdateMaterials();
        if (mAppearanceCombineSkin != null)
            mAppearanceCombineSkin.OnApplySkin += OnApplySkin;
        TryUpdateMaterials();
    }

    private void OnDisable()
    {
        if (mAppearanceCombineSkin != null)
            mAppearanceCombineSkin.OnApplySkin -= OnApplySkin;
    }

    private void OnDestroy()
    {
        if (mSkeletonRenderer != null)
            mSkeletonRenderer.CustomMaterialOverride.Clear();
    }

    private void OnApplySkin() => TryUpdateMaterials();

    public void TryUpdateMaterials()
    {
        if (mTextureLoader == null) return;

        mSkeletonRenderer.Initialize(false, false);
        Dictionary<Material, Material> overrides = mSkeletonRenderer.CustomMaterialOverride;
        overrides.Clear();
        mTextureLoader.TryInit();

        Skeleton skeleton = mSkeletonRenderer.skeleton;
        Skin skin = skeleton?.Skin ?? skeleton?.Data?.DefaultSkin;
        if (skin == null) return;

        foreach (Skin.SkinEntry entry in skin.Attachments)
        {
            Attachment attachment = entry.Attachment;
            if (!(attachment is IHasTextureRegion textured)) continue;
            if (!(textured.Region is AtlasRegion region)) continue;

            Material source = region.page.rendererObject as Material;
            if (source == null || overrides.ContainsKey(source)) continue;

            Material instance = new Material(source);
            instance.name += "_instance";
            overrides[source] = instance;
            if (!mTextureLoader.PlaceholderTextureIndexDict.TryGetValue(instance.mainTexture, out int index))
                continue;

            AppearanceOnDemandTextureLoader<YooAssetTextureReference, YooAssetRequest>.PlaceholderTextureMapping mapping =
                mTextureLoader.placeholderMap[index];
            instance.mainTexture = mapping.targetTextureReference.AssetReference.Load();
            if (instance.HasTexture(AppearanceMaterialUtils.sDyeTexId))
                instance.SetTexture(AppearanceMaterialUtils.sDyeTexId,
                    mapping.dyeTextureReference.AssetReference.Load());
        }
    }
}
