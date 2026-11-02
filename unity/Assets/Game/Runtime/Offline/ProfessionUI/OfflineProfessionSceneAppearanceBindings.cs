using System;
using UnityEngine;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    public sealed class OfflineProfessionSceneAppearanceBindings : IProfessionAppearanceSceneBindings,
        IProfessionAppearanceSceneCharacterBindings
    {
        private sealed class Handle : IProfessionAppearanceCharacterAsset
        {
            private ScriptableObject asset;
            internal Handle(ScriptableObject asset) { this.asset = asset; }
            public ScriptableObject Result => asset ? asset : throw new ObjectDisposedException(nameof(Handle));
            // Catalog assets remain owned by Resources, not an individual actor.
            public void Dispose() { asset = null; }
        }
        private readonly ProfessionNativeSceneRenderer renderer;
        private readonly ScriptableObject[] preheated = new ScriptableObject[2];
        public ProfessionNativeSceneAppearanceResources Resources { get; }
        public IAppearanceAssetManager AssetManager { get; }
        public AppearanceConfig Config { get; }
        public OfflineProfessionSceneAppearanceBindings(ProfessionNativeSceneAppearanceResources resources,
            ProfessionNativeSceneRenderer renderer, IAppearanceAssetManager manager, AppearanceConfig config)
        {
            Resources = resources ? resources : throw new ArgumentNullException(nameof(resources));
            this.renderer = renderer ? renderer : throw new ArgumentNullException(nameof(renderer));
            AssetManager = manager ?? throw new ArgumentNullException(nameof(manager));
            Config = config ? config : throw new ArgumentNullException(nameof(config));
            foreach (var model in resources.Models)
            {
                int index = model.Sex == "male" ? 0 : model.Sex == "female" ? 1 : throw new InvalidOperationException("Unbound original character sex.");
                if (preheated[index]) throw new InvalidOperationException("Duplicate original character sex.");
                _ = model.Asset.Data;
                preheated[index] = model.Asset;
            }
            if (!preheated[0] || !preheated[1]) throw new InvalidOperationException("Preheat both original scene characters before use.");
        }
        public bool TryGetPlayerSkeletonAssets(Sex sex, out ScriptableObject skeletonDataAsset, out ScriptableObject splitAnimationSo)
        {
            skeletonDataAsset = null; splitAnimationSo = null;
            if (sex < Sex.Male || (int)sex >= preheated.Length) return false;
            skeletonDataAsset = preheated[(int)sex];
            // The native current asset contains every exported split animation.
            // It never claims an SDK split-loader object or lazy SDK loading.
            return skeletonDataAsset;
        }
        public IProfessionAppearanceCharacterAsset LoadAssetByGuid(string guid) => new Handle(Resources.ConfigurationByGuid(guid));
        public IProfessionAppearanceSkeletonComponent GetSkeletonComponent(Component owner)
        {
            if (!owner || owner.gameObject != renderer.gameObject) throw new ArgumentException("Bind each original appearance to its own scene renderer.");
            return renderer;
        }
        public IProfessionAppearanceSkeletonData GetSkeletonData(ScriptableObject asset, bool quiet)
        { Resources.FindModel(asset); return ((ProfessionNativeSceneCharacterAsset)asset).Data.SkinCatalog; }
        public IProfessionAppearanceSkin CreateSkin(string name)
            => (renderer.State ?? throw new InvalidOperationException("Initialize the native scene skeleton before creating its combined skin.")).CreateSkin(name);
        public IProfessionAppearanceSceneCache CreateMecanimCache(IProfessionAppearanceSceneMecanim mecanim)
            => throw new NotSupportedException("This binding consumes completed native poses; SDK Mecanim translation and bone caching are not bound.");
    }
}
