using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Keep the original UI/renderer addresses distinct. The supplied native
    // catalog explicitly binds each original address to its Windows prefab.
    public sealed class OfflineProfessionAppearancePartBindings : IProfessionAppearancePartBindings
    {
        private readonly SpineSkinInfoParser skins;
        private readonly OfflineProfessionImageResourceLoader resources;
        public AppearanceConfig Config { get; }

        public OfflineProfessionAppearancePartBindings(AppearanceConfig config,
            SpineSkinInfoParser skins, OfflineProfessionImageCatalog prefabs)
        {
            Config = config ? config : throw new ArgumentNullException(nameof(config));
            this.skins = skins ?? throw new ArgumentNullException(nameof(skins));
            resources = new OfflineProfessionImageResourceLoader(prefabs);
        }

        public bool HasEffect(string skinName) => skins.HasEffect(skinName);
        public IProfessionAppearancePartAsset LoadPrefab(string path)
            => new PartAsset(resources.LoadAsset<GameObject>(path));
        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
            => UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays);

        private sealed class PartAsset : IProfessionAppearancePartAsset
        {
            private readonly IProfessionAssetOperation<GameObject> operation;
            internal PartAsset(IProfessionAssetOperation<GameObject> operation) => this.operation = operation;
            public bool IsValid => operation.IsValid;
            public int Status => operation.Status;
            public GameObject Result => operation.Result;
            public event Action<IProfessionAssetOperation<GameObject>> Completed
            {
                // Packaged local catalog requests are synchronous. Preserve the
                // completed handle's immediate subscription and disposed checks.
                add { _ = operation.Status; value(this); }
                remove { _ = operation.Status; }
            }
            public void Release(bool tryUnloadAllAssets) => Dispose();
            public void Dispose() => operation.Dispose();
        }
    }
}
