using System;
using TMPro;
using UnityEngine;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.PartnersUI;
using Object = UnityEngine.Object;

namespace Zjcs.Offline.HomeUI
{
    // Native hierarchy/layout is loaded unchanged. Bind local services before
    // original Awake/OnEnable, including objects created later by the pools.
    public sealed class OfflineHomeTopBar : IDisposable
    {
        private readonly OfficialHomeTopBarAssetCatalog assets;
        private readonly TMP_FontAsset font;
        private readonly IPlayerItemUIHost host;
        private readonly Func<string, string> localize;
        private GameObject managerRoot;
        private GameObjectPool<TopBarItem> topBarPool;
        private GameObjectPool<MePlayerItemUI> currencyPool;
        private OfflinePartnerUiSoundScope sound;
        public GameObject Root { get; private set; }
        public TopBarHandler Handler { get; private set; }
        public HomeTopBarManager Manager { get; private set; }

        public OfflineHomeTopBar(Transform parent, IPlayerItemUIHost host, TMP_FontAsset font,
            Func<string, string> localize, OfficialHomeTopBarAssetCatalog assets = null)
        {
            if (!parent || host == null || !font || localize == null)
                throw new ArgumentException("Complete local home top-bar bindings are required.");
            this.host = host; this.font = font; this.localize = localize;
            this.assets = assets ?? OfficialHomeTopBarAssetCatalog.LoadDefault();
            var staging = new GameObject("HomeTopBarStaging", typeof(RectTransform));
            staging.transform.SetParent(parent, false); staging.SetActive(false);
            try
            {
                sound = new OfflinePartnerUiSoundScope(this.assets.Sound);
                managerRoot = Object.Instantiate(this.assets.managerPrefab, staging.transform, false);
                topBarPool = new GameObjectPool<TopBarItem>(managerRoot.transform,
                    (slot, _, active) => Instantiate(this.assets.topBarPrefab, slot, active), this.assets.topBarPrefab.name);
                currencyPool = new GameObjectPool<MePlayerItemUI>(managerRoot.transform,
                    (slot, _, active) => Instantiate(this.assets.currencyPrefab, slot, active), this.assets.currencyPrefab.name);
                Manager = new HomeTopBarManager(topBarPool, currencyPool, this.assets.ItemId);
                Root = Object.Instantiate(this.assets.parentPrefab, staging.transform, false);
                Root.SetActive(false);
                Handler = Root.GetComponent<TopBarHandler>(); Handler.Bind(Manager);
                managerRoot.transform.SetParent(parent, false);
                Root.transform.SetParent(parent, false); Root.SetActive(true);
            }
            catch { Dispose(); throw; }
            finally { Object.Destroy(staging); }
        }

        private GameObject Instantiate(GameObject prefab, Transform slot, bool active)
        {
            var inactive = new GameObject("TopBarPoolBinding"); inactive.SetActive(false);
            inactive.transform.SetParent(managerRoot.transform, false);
            try
            {
                var value = Object.Instantiate(prefab, inactive.transform, false); value.SetActive(false);
                foreach (var text in value.GetComponentsInChildren<TMP_Text>(true)) text.font = font;
                foreach (var text in value.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = localize;
                foreach (var currency in value.GetComponentsInChildren<MePlayerItemUI>(true)) currency.Bind(host);
                value.transform.SetParent(slot, false); value.SetActive(active);
                return value;
            }
            finally { Object.Destroy(inactive); }
        }

        public void Dispose()
        {
            if (Root) { Root.SetActive(false); Object.Destroy(Root); Root = null; Handler = null; }
            topBarPool?.Destory(); topBarPool = null;
            currencyPool?.Destory(); currencyPool = null;
            if (managerRoot) { Object.Destroy(managerRoot); managerRoot = null; }
            sound?.Dispose(); sound = null; Manager = null;
        }
    }
}
