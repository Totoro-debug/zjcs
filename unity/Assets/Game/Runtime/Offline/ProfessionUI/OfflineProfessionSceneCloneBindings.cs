using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Current = Zjcs.Official.CombatAppearance;

namespace Zjcs.Offline.ProfessionUI
{
    // The skill prefab retains its original appearance components and part slot.
    // Only the obsolete renderer endpoint is exchanged, before any Awake runs.
    internal static class OfflineProfessionSceneCloneBindings
    {
        [Serializable] private sealed class AppearanceReferences
        {
            public AssetGuidProxy<SkinTextureAsset> SkinTextureAssetGuid;
            public AssetGuidProxy<YooAssetTextureLoader> TextureLoaderGuid;
            public Component mPartHandler;
            public Component mSkeletonRenderer;
        }
        [Serializable] private sealed class PartReferences
        {
            public Component mSkeletonRenderer;
            public Transform mSlot;
            public string mSortingLayer;
            public int mDefaultSortingOrder;
        }

        internal static void Bind(Current.AppearanceRenderer appearance, Component originalRenderer,
            ProfessionNativeSceneRenderer native, ProfessionNativeSceneAppearanceResources resources,
            IAppearanceAssetManager manager, IProfessionAppearancePartBindings parts, ICharacterPartEntryProvider pool)
        {
            if (!appearance || !originalRenderer || !native
                || appearance.gameObject != native.gameObject || originalRenderer.gameObject != native.gameObject
                || native.gameObject.activeInHierarchy || native.GetComponent<Animator>())
                throw new InvalidOperationException("Bind the original inactive independent clone component graph.");
            var character = native.GetComponent<Current.AppearanceCharacter>();
            var handler = native.GetComponent<AppearanceRendererPartHandler>();
            var references = JsonUtility.FromJson<AppearanceReferences>(JsonUtility.ToJson(appearance));
            var partReferences = handler ? JsonUtility.FromJson<PartReferences>(JsonUtility.ToJson(handler)) : null;
            if (!character || !handler || references.mPartHandler != handler
                || references.mSkeletonRenderer != originalRenderer || partReferences.mSkeletonRenderer != originalRenderer
                || references.SkinTextureAssetGuid?.AssetGuid != resources.SkinGuid
                || references.TextureLoaderGuid?.AssetGuid != resources.LoaderGuid
                || !partReferences.mSlot || partReferences.mSlot.parent != native.transform)
                throw new InvalidOperationException("Original clone appearance, texture or part references differ.");
            var settings = Resources.Load<TextAsset>("Combat/SkillClones/renderer-settings")
                ?? throw new InvalidOperationException("Original clone renderer settings are not packaged.");
            native.ConfigureAnimation(resources, settings, "Player");
            var bindings = new OfflineProfessionSceneAppearanceBindings(resources, native, manager, parts.Config);
            handler.ConfigureParts(parts);
            handler.ConfigureRenderer(native, partReferences.mSlot, pool,
                partReferences.mSortingLayer, partReferences.mDefaultSortingOrder);
            character.Configure(bindings, handler);
            appearance.Configure(bindings, references.SkinTextureAssetGuid, references.TextureLoaderGuid, handler);
        }
    }
}
