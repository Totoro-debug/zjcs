using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Current AppearanceUI shares AppearanceSpine/AppearanceCharacter with the
    // scene path. The original character manager preheats both data assets.
    public sealed class OfflineProfessionUiAppearanceBindings : IProfessionAppearanceSceneBindings,
        IProfessionAppearanceSceneCharacterBindings, IProfessionAppearanceUIBindings
    {
        private readonly ProfessionNativeUiGraphic graphic;
        private readonly Func<ProfessionNativeSceneCharacterAsset, IProfessionNativeUiPoseSource> createPose;
        private readonly OfflineProfessionUiAnimationBindings animations;
        private readonly ScriptableObject[] preheated = new ScriptableObject[2];
        private readonly List<ProfessionNativeSkin> combinedSkins = new List<ProfessionNativeSkin>();
        private ScriptableObject boundAsset;
        public ProfessionNativeSceneAppearanceResources Resources { get; }
        public IAppearanceAssetManager AssetManager { get; }
        IProfessionAppearanceUIAssetManager IProfessionAppearanceUIBindings.AssetManager => AssetManager;
        public AppearanceConfig Config { get; }

        public OfflineProfessionUiAppearanceBindings(ProfessionNativeSceneAppearanceResources resources,
            ProfessionNativeUiGraphic graphic, IAppearanceAssetManager manager, AppearanceConfig config,
            Func<ProfessionNativeSceneCharacterAsset, IProfessionNativeUiPoseSource> createPose)
        {
            Resources = resources ? resources : throw new ArgumentNullException(nameof(resources));
            this.graphic = graphic ? graphic : throw new ArgumentNullException(nameof(graphic));
            AssetManager = manager ?? throw new ArgumentNullException(nameof(manager));
            Config = config ? config : throw new ArgumentNullException(nameof(config));
            this.createPose = createPose ?? throw new ArgumentNullException(nameof(createPose));
            animations = new OfflineProfessionUiAnimationBindings(manager);
            foreach (var model in resources.Models)
            {
                int index = model.Sex == "male" ? 0 : model.Sex == "female" ? 1 : throw new InvalidOperationException("Unbound original UI character sex.");
                if (preheated[index]) throw new InvalidOperationException("Duplicate original UI character sex.");
                _ = model.Asset.Data;
                preheated[index] = model.Asset;
            }
            if (!preheated[0] || !preheated[1]) throw new InvalidOperationException("Preheat both original UI characters before use.");
        }
        public bool TryGetPlayerSkeletonAssets(Sex sex, out ScriptableObject skeletonDataAsset, out ScriptableObject splitAnimationSo)
        {
            skeletonDataAsset = null; splitAnimationSo = null;
            if (sex < Sex.Male || (int)sex >= preheated.Length) return false;
            var selected = (ProfessionNativeSceneCharacterAsset)preheated[(int)sex];
            if (boundAsset != selected)
            {
                var source = createPose(selected);
                if (source == null || source.Asset != selected)
                    throw new InvalidOperationException("Bind the pose producer for this exact original UI asset.");
                graphic.Release();
                graphic.Configure(source);
                boundAsset = selected;
            }
            skeletonDataAsset = selected;
            // Every original split animation is in the verified native payload.
            return true;
        }
        public IProfessionAppearanceCharacterAsset LoadAssetByGuid(string guid) => new Handle(Resources.ConfigurationByGuid(guid));
        public IProfessionAppearanceSkeletonComponent GetSkeletonComponent(Component owner)
        {
            if (!owner || owner.gameObject != graphic.gameObject)
                throw new ArgumentException("Bind each original UI appearance to its own graphic.", nameof(owner));
            return graphic;
        }
        public IProfessionAppearanceSkeletonData GetSkeletonData(ScriptableObject asset, bool quiet)
        {
            Resources.FindModel(asset);
            var data = ((ProfessionNativeSceneCharacterAsset)asset).Data;
            if (graphic.skeletonDataAsset != asset || graphic.State?.Source != data)
                throw new InvalidOperationException("Initialize the bound original UI skeleton before assembling its skins.");
            foreach (var skin in combinedSkins)
                if (skin.SlotCount != data.SlotCount || skin.BoneCount != data.BoneCount || skin.ConstraintCount != data.Constraints.Count)
                    skin.RebindEmptyLayout(data.SlotCount, data.BoneCount, data.Constraints.Count);
            return data.SkinCatalog;
        }
        public IProfessionAppearanceSkin CreateSkin(string name)
        {
            var skin = (graphic.State ?? throw new InvalidOperationException("Initialize the original UI skeleton before creating its combined skin.")).CreateSkin(name);
            combinedSkins.Add(skin);
            return skin;
        }
        public bool TryLazyLoadSpineAnimation(IProfessionAppearanceUIGraphic value, string name, out object animation)
        {
            RequireGraphic(value);
            return animations.TryLazyLoadSpineAnimation(value, name, out animation);
        }
        public void PlayAnimation(IProfessionAppearanceUIGraphic value, string name, bool loop)
        {
            RequireGraphic(value);
            animations.PlayAnimation(value, name, loop);
        }
        private void RequireGraphic(IProfessionAppearanceUIGraphic value)
        {
            if (!ReferenceEquals(value, graphic)) throw new ArgumentException("UI animation belongs to another original graphic.", nameof(value));
        }
        public IProfessionAppearanceSceneCache CreateMecanimCache(IProfessionAppearanceSceneMecanim mecanim)
            => throw new NotSupportedException("The original UI appearance uses track animation rather than a Mecanim cache.");

        private sealed class Handle : IProfessionAppearanceCharacterAsset
        {
            private ScriptableObject asset;
            internal Handle(ScriptableObject asset) { this.asset = asset; }
            public ScriptableObject Result => asset ? asset : throw new ObjectDisposedException(nameof(Handle));
            public void Dispose() { asset = null; }
        }
    }
}
