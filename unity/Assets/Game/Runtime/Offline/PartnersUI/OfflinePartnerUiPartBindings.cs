using System;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // The original part handler selects addresses and owns every loaded part.
    // This adapter binds its packaged native prefab before any OnEnable runs.
    public sealed class OfflinePartnerUiPartBindings : IProfessionAppearancePartBindings
    {
        private readonly PartnerNativeDetailActorAssets assets;
        private readonly SpineSkinInfoParser skins;
        private readonly Action<GameObject> bindTextures;
        public AppearanceConfig Config => assets.Config;

        public OfflinePartnerUiPartBindings(PartnerNativeDetailActorAssets assets,
            SpineSkinInfoParser skins, Action<GameObject> bindTextures)
        {
            this.assets = assets ? assets : throw new ArgumentNullException(nameof(assets));
            this.skins = skins ?? throw new ArgumentNullException(nameof(skins));
            this.bindTextures = bindTextures ?? throw new ArgumentNullException(nameof(bindTextures));
        }

        public bool HasEffect(string skinName) => skins.HasEffect(skinName);
        public IProfessionAppearancePartAsset LoadPrefab(string path) => new Lease(assets.LoadPrefab(path));

        public GameObject Instantiate(GameObject prefab, Transform parent, bool worldPositionStays)
        {
            var staging = new GameObject("Original UI part bindings", typeof(RectTransform));
            staging.SetActive(false); staging.transform.SetParent(parent, false);
            GameObject result = null;
            try
            {
                result = UnityEngine.Object.Instantiate(prefab, staging.transform, worldPositionStays);
                bindTextures(result);
                foreach (var part in result.GetComponentsInChildren<AppearanceSkeletonPart>(true)) part.ConfigurePart(this);
                result.transform.SetParent(parent, worldPositionStays);
                return result;
            }
            catch { if (result) UnityEngine.Object.Destroy(result); throw; }
            finally { UnityEngine.Object.Destroy(staging); }
        }

        private sealed class Lease : IProfessionAppearancePartAsset
        {
            private GameObject asset;
            public Lease(GameObject value) { asset = value; }
            public bool IsValid => asset;
            public int Status => asset ? 2 : 0;
            public GameObject Result => asset;
            public event Action<IProfessionAssetOperation<GameObject>> Completed
            { add { value(this); } remove { } }
            public void Release(bool tryUnloadAllAssets) => Dispose();
            public void Dispose() { asset = null; }
        }
    }
}
