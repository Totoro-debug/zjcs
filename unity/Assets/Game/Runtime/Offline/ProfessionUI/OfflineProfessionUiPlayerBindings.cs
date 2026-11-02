using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Current = Zjcs.Official.CombatAppearance;

namespace Zjcs.Offline.ProfessionUI
{
    // Per-window binding only. The window owns its original components; the
    // profile host owns the material manager, texture services and part loader.
    public sealed class OfflineProfessionUiPlayerBindings
    {
        [Serializable]
        private sealed class SerializedReferences
        {
            public AssetGuidProxy<SkinTextureAsset> SkinTextureAssetGuid;
            public AssetGuidProxy<YooAssetTextureLoader> TextureLoaderGuid;
        }

        private readonly ProfessionNativeSceneAppearanceResources resources;
        private readonly IAppearanceAssetManager manager;
        private readonly IProfessionAppearancePartBindings parts;
        private readonly IProfessionPlayerAppearanceBindings player;
        private readonly Func<ProfessionNativeUiGraphic, ProfessionNativeSceneCharacterAsset, IProfessionNativeUiPoseSource> createPose;
        private readonly HashSet<Current.PlayerAnimationAppearance> bound = new HashSet<Current.PlayerAnimationAppearance>();

        public OfflineProfessionUiPlayerBindings(ProfessionNativeSceneAppearanceResources resources,
            IAppearanceAssetManager manager, IProfessionAppearancePartBindings parts,
            IProfessionPlayerAppearanceBindings player,
            Func<ProfessionNativeUiGraphic, ProfessionNativeSceneCharacterAsset, IProfessionNativeUiPoseSource> createPose = null)
        {
            this.resources = resources ? resources : throw new ArgumentNullException(nameof(resources));
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.createPose = createPose ?? CreatePose;
        }

        // Call on the inactive original window and newly rented rows, before
        // activation/PlayerInfoUI.Refresh. Repeated tree visits preserve tracks.
        public int BindTree(GameObject root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            bound.RemoveWhere(value => !value);
            int added = 0;
            foreach (var appearance in root.GetComponentsInChildren<Current.PlayerAnimationAppearance>(true))
            {
                if (bound.Contains(appearance)) continue;
                if (appearance.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Bind the original UI player before activating its window.");
                var ui = appearance.GetComponent<Current.AppearanceUI>();
                var character = appearance.GetComponent<Current.AppearanceCharacter>();
                var graphic = appearance.GetComponent<ProfessionNativeUiGraphic>();
                var handler = appearance.GetComponent<AppearanceUIPartHandler>();
                if (!ui || !character || !graphic || !handler)
                    throw new InvalidOperationException("Original UI player component binding is incomplete.");
                if (graphic.skeletonDataAsset || graphic.AnimationState != null)
                    throw new InvalidOperationException("Bind the original UI player before initializing its skeleton.");
                var references = JsonUtility.FromJson<SerializedReferences>(JsonUtility.ToJson(ui));
                if (references?.SkinTextureAssetGuid?.AssetGuid != resources.SkinGuid
                    || references.TextureLoaderGuid?.AssetGuid != resources.LoaderGuid)
                    throw new InvalidOperationException("Bind the exact serialized original UI skin and texture loader.");

                var bindings = new OfflineProfessionUiAppearanceBindings(resources, graphic, manager, parts.Config,
                    asset => createPose(graphic, asset));
                ui.Configure(bindings, references.SkinTextureAssetGuid, references.TextureLoaderGuid, handler);
                ui.ConfigureUI(bindings);
                character.Configure(bindings, handler);
                handler.ConfigureParts(parts);
                appearance.ConfigurePlayerAppearance(player);
                bound.Add(appearance);
                added++;
            }
            return added;
        }

        private IProfessionNativeUiPoseSource CreatePose(ProfessionNativeUiGraphic graphic, ProfessionNativeSceneCharacterAsset asset)
            => new ProfessionNativeUiCharacterPose(asset,
                (page, blend) => blend == 0 ? graphic.material : blend == 1 ? graphic.additiveMaterial
                    : blend == 2 ? graphic.multiplyMaterial : blend == 3 ? graphic.screenMaterial
                    : throw new NotSupportedException("Original UI blend mode is unbound: " + blend),
                page => resources.MaterialForPage(asset, page).mainTexture);
    }
}
