using System;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeUI
{
    // Native hierarchy/layout is loaded unchanged. Bind local services before
    // original Awake/OnEnable, including objects created later by the pools.
    public sealed class OfflineHomeTopBar : IDisposable
    {
        private readonly OfficialHomeTopBarAssetCatalog assets;
        private OfflineHomeTopBarPools pools;
        public GameObject Root { get; private set; }
        public TopBarHandler Handler { get; private set; }
        public HomeTopBarManager Manager => pools?.Manager;

        public OfflineHomeTopBar(Transform parent, IPlayerItemUIHost host, TMP_FontAsset font,
            Func<string, string> localize, OfficialHomeTopBarAssetCatalog assets = null)
        {
            if (!parent || host == null || !font || localize == null)
                throw new ArgumentException("Complete local home top-bar bindings are required.");
            this.assets = assets ?? OfficialHomeTopBarAssetCatalog.LoadDefault();
            var staging = new GameObject("HomeTopBarStaging", typeof(RectTransform));
            staging.transform.SetParent(parent, false); staging.SetActive(false);
            try
            {
                pools = new OfflineHomeTopBarPools(parent, host, font, localize, this.assets);
                Root = Object.Instantiate(this.assets.parentPrefab, staging.transform, false);
                Root.SetActive(false);
                Handler = Root.GetComponent<TopBarHandler>(); Handler.Bind(Manager);
                Root.transform.SetParent(parent, false); Root.SetActive(true);
            }
            catch { Dispose(); throw; }
            finally { Object.Destroy(staging); }
        }

        public void Dispose()
        {
            if (Root) { Root.SetActive(false); Object.Destroy(Root); Root = null; Handler = null; }
            pools?.Dispose(); pools = null;
        }
    }
}
