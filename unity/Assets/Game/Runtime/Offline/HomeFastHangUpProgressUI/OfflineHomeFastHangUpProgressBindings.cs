using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.HomeFastHangUpProgressUI;
using Zjcs.Official.ProfessionUI;
using Current = Zjcs.Official.CombatAppearance;

namespace Zjcs.Offline.HomeFastHangUpProgressUI
{
    // The original presentation uses plain PlayerAppearance, whereas avatar
    // widgets use PlayerAnimationAppearance. Keep that original component type.
    public static class OfflineHomeFastHangUpProgressBindings
    {
        [Serializable] private sealed class References
        {
            public AssetGuidProxy<SkinTextureAsset> SkinTextureAssetGuid;
            public AssetGuidProxy<YooAssetTextureLoader> TextureLoaderGuid;
        }

        public static void Bind(GameObject root, IHomeFastHangUpProgressServices services,
            ProfessionNativeSceneAppearanceResources resources, IAppearanceAssetManager manager,
            IProfessionAppearancePartBindings parts, IProfessionPlayerAppearanceBindings player)
        {
            if (!root || root.activeInHierarchy) throw new InvalidOperationException("Bind the inactive original acceleration presentation.");
            if (services == null || !resources || manager == null || parts == null || player == null)
                throw new ArgumentNullException("Original presentation dependencies are required.");
            var controller = root.GetComponent<FastHangUpProgressBarDialog>();
            if (!controller) throw new InvalidOperationException("Original acceleration controller is unbound.");
            controller.Services = services;
            foreach (var value in root.GetComponentsInChildren<MyAppearance>(true)) value.Services = services;
            foreach (var value in root.GetComponentsInChildren<PlayerAppearanceOverride>(true)) value.Services = services;
            var appearance = controller.PlayerAppearance;
            var ui = controller.AppearanceUI;
            var character = appearance ? appearance.GetComponent<Current.AppearanceCharacter>() : null;
            var graphic = appearance ? appearance.GetComponent<ProfessionNativeUiGraphic>() : null;
            var handler = appearance ? appearance.GetComponent<AppearanceUIPartHandler>() : null;
            if (!appearance || !ui || !character || !graphic || !handler || graphic.skeletonDataAsset || graphic.AnimationState != null)
                throw new InvalidOperationException("Bind the original UI actor before initializing its skeleton.");
            var references = JsonUtility.FromJson<References>(JsonUtility.ToJson(ui));
            if (references?.SkinTextureAssetGuid?.AssetGuid != resources.SkinGuid
                || references.TextureLoaderGuid?.AssetGuid != resources.LoaderGuid)
                throw new InvalidOperationException("Restore the exact original presentation skin and texture loader.");
            var bindings = new OfflineProfessionUiAppearanceBindings(resources, graphic, manager, parts.Config,
                asset => new ProfessionNativeUiCharacterPose(asset,
                    (page, blend) => blend == 0 ? graphic.material : blend == 1 ? graphic.additiveMaterial
                        : blend == 2 ? graphic.multiplyMaterial : blend == 3 ? graphic.screenMaterial
                        : throw new NotSupportedException("Original UI blend mode is unbound: " + blend),
                    page => resources.MaterialForPage(asset, page).mainTexture));
            ui.Configure(bindings, references.SkinTextureAssetGuid, references.TextureLoaderGuid, handler);
            ui.ConfigureUI(bindings);
            character.Configure(bindings, handler);
            handler.ConfigureParts(parts);
            appearance.ConfigurePlayerAppearance(player);
        }
    }
}
