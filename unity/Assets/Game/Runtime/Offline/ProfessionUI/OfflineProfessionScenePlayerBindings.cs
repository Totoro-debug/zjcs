using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Current = Zjcs.Official.CombatAppearance;

namespace Zjcs.Offline.ProfessionUI
{
    // Binds the original inactive Game model. Its owner supplies the saved
    // player, materials, effects and part pool; original components own refresh,
    // self-event registration and release. No hierarchy or scale is fabricated.
    public sealed class OfflineProfessionScenePlayerBindings
    {
        [Serializable] private sealed class AppearanceReferences
        {
            public AssetGuidProxy<SkinTextureAsset> SkinTextureAssetGuid;
            public AssetGuidProxy<YooAssetTextureLoader> TextureLoaderGuid;
        }
        [Serializable] private sealed class PartReferences
        {
            public Component mSkeletonRenderer;
            public Transform mSlot;
            public string mSortingLayer;
            public int mDefaultSortingOrder;
        }
        private readonly ProfessionNativeSceneAppearanceResources resources;
        private readonly IAppearanceAssetManager manager;
        private readonly IProfessionAppearancePartBindings parts;
        private readonly ICharacterPartEntryProvider pool;
        private readonly IProfessionPlayerAppearanceBindings player;
        private readonly HashSet<Current.PlayerAnimatorAppearance> bound = new HashSet<Current.PlayerAnimatorAppearance>();

        public OfflineProfessionScenePlayerBindings(ProfessionNativeSceneAppearanceResources resources,
            IAppearanceAssetManager manager, IProfessionAppearancePartBindings parts,
            ICharacterPartEntryProvider pool, IProfessionPlayerAppearanceBindings player)
        {
            this.resources = resources ? resources : throw new ArgumentNullException(nameof(resources));
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
        }

        public int BindTree(GameObject root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            bound.RemoveWhere(value => !value);
            int added = 0;
            foreach (var appearance in root.GetComponentsInChildren<Current.PlayerAnimatorAppearance>(true))
            {
                if (bound.Contains(appearance)) continue;
                if (appearance.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Bind the original Game player before activation.");
                var native = appearance.GetComponent<ProfessionNativeSceneRenderer>();
                var animation = appearance.GetComponent<ProfessionNativeSceneAppearance>();
                var character = appearance.GetComponent<Current.AppearanceCharacter>();
                var handler = appearance.GetComponent<AppearanceRendererPartHandler>();
                var animator = appearance.GetComponent<Animator>();
                if (!native || !animation || !character || !handler || !animator
                    || appearance.Animator != animator || appearance.AnimController
                    || appearance.AppearanceCharacter != character || appearance.PlayerPhase != Current.PlayerPhase.Game
                    || appearance.AppendSkinType != AppendSkinType.Game)
                    throw new InvalidOperationException("Bind the original Game model's serialized component graph.");
                if (native.State != null || native.skeletonDataAsset)
                    throw new InvalidOperationException("Bind the original player before initializing its skeleton.");
                var references = JsonUtility.FromJson<AppearanceReferences>(JsonUtility.ToJson(animation));
                var partReferences = JsonUtility.FromJson<PartReferences>(JsonUtility.ToJson(handler));
                if (references.SkinTextureAssetGuid?.AssetGuid != resources.SkinGuid
                    || references.TextureLoaderGuid?.AssetGuid != resources.LoaderGuid
                    || partReferences.mSkeletonRenderer != native || !partReferences.mSlot
                    || partReferences.mSlot.parent != native.transform)
                    throw new InvalidOperationException("Original Game appearance resource or part references are incomplete.");
                native.Configure(resources);
                var bindings = new OfflineProfessionSceneAppearanceBindings(resources, native, manager, parts.Config);
                handler.ConfigureParts(parts);
                handler.ConfigureRenderer(native, partReferences.mSlot, pool,
                    partReferences.mSortingLayer, partReferences.mDefaultSortingOrder);
                character.Configure(bindings, handler);
                bool animatorBound = false;
                animation.Configure(bindings, handler, _ =>
                {
                    if (!animatorBound) { native.BindPlayerAnimator(animator); animatorBound = true; }
                    native.ApplyPlayerSkinPose(animator);
                });
                appearance.ConfigurePlayerAppearance(player);
                appearance.ConfigureAnimatorResources(path => LoadAnimator(path), LoadAnimator);
                bound.Add(appearance); added++;
            }
            return added;
        }

        private static RuntimeAnimatorController LoadAnimator(string address)
        {
            const string prefix = "Assets/AppearanceAssets/Character/Model/Anim/";
            if (address == null || !address.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException("Unbound original player Animator address: " + address);
            string name = address.Substring(prefix.Length);
            int extension = name.LastIndexOf('.');
            if (extension < 0) throw new ArgumentException("Original controller address has no extension.");
            return UnityEngine.Resources.Load<RuntimeAnimatorController>("Combat/GameAnimators/Anim/" + name.Substring(0, extension));
        }
    }
}
