using System;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMainlineHudModule hud;
        private OfficialMainlineRegionsModule regions;
        private OfficialMainlineNavigationModule navigation;
        private OfficialMainlineStatuesModule statues;
        private OfficialMainlineStaminaModule stamina;
        private OfficialMainlineExplorationModule exploration;
        private OfficialMainlineMiniMapModule miniMap;

        // Every map instance owns its modules. Nothing here is shared between
        // maps or profiles; the official services retain their existing saves.
        private void Awake() => InitializeModules();

        private void InitializeModules()
        {
            if (hud != null) return;
            hud = new OfficialMainlineHudModule(this);
            regions = new OfficialMainlineRegionsModule(this);
            navigation = new OfficialMainlineNavigationModule(this);
            statues = new OfficialMainlineStatuesModule(this);
            stamina = new OfficialMainlineStaminaModule(this);
            exploration = new OfficialMainlineExplorationModule(this);
            miniMap = new OfficialMainlineMiniMapModule(this);
        }

        public Func<Vector2?> PointerPressProvider
        {
            set { InitializeModules(); navigation.PointerPressProvider = value; }
        }

        // Nesting keeps the map host's state private while the modules own their
        // controls, pending operations and cleanup. Only cross-module members
        // are exposed inside these private implementation types.
        private abstract class OfficialMainlineModule
        {
            protected readonly OfficialMainlineMapRuntime host;
            protected OfficialMainlineModule(OfficialMainlineMapRuntime host) => this.host = host;
        }

        private sealed partial class OfficialMainlineHudModule : OfficialMainlineModule
        {
            public OfficialMainlineHudModule(OfficialMainlineMapRuntime host) : base(host) { }

            public void DisposePortrait()
            {
                if (playerHeadTexture != null) Destroy(playerHeadTexture);
                playerHeadTexture = null;
            }

            public void DisposeQuickBuy()
            {
                // Closing the map must not resume a pending story continuation.
                quickBuyDialog?.Abandon();
                if (quickBuyCanvas != null) Destroy(quickBuyCanvas);
                quickBuyCanvas = null;
                quickBuyDialog = null;
            }

            public void DisposeCommerce()
            {
                if (commerceScreen != null) Destroy(commerceScreen.gameObject);
                commerceScreen = null;
                if (firstPayEntry != null) Destroy(firstPayEntry);
                firstPayEntry = null;
            }
        }

        private sealed partial class OfficialMainlineRegionsModule : OfficialMainlineModule
        {
            public OfficialMainlineRegionsModule(OfficialMainlineMapRuntime host) : base(host) { }
        }

        private sealed partial class OfficialMainlineNavigationModule : OfficialMainlineModule
        {
            public OfficialMainlineNavigationModule(OfficialMainlineMapRuntime host) : base(host) { }
        }

        private sealed partial class OfficialMainlineStatuesModule : OfficialMainlineModule
        {
            public OfficialMainlineStatuesModule(OfficialMainlineMapRuntime host) : base(host) { }
        }

        private sealed partial class OfficialMainlineStaminaModule : OfficialMainlineModule
        {
            public OfficialMainlineStaminaModule(OfficialMainlineMapRuntime host) : base(host) { }
        }

        private sealed partial class OfficialMainlineExplorationModule : OfficialMainlineModule
        {
            public OfficialMainlineExplorationModule(OfficialMainlineMapRuntime host) : base(host) { }

            public void Dispose()
            {
                foreach (var window in exploreWindows) { ReleaseExploreMask(window); window.Destroy(); }
                exploreWindows.Clear();
                foreach (var texture in exploreSnapshots.Values) RenderTexture.ReleaseTemporary(texture);
                exploreSnapshots.Clear();
                exploreMasks.Clear();
                if (exploreCanvas != null) Destroy(exploreCanvas);
                exploreCanvas = null;
            }
        }

        private sealed partial class OfficialMainlineMiniMapModule : OfficialMainlineModule
        {
            public OfficialMainlineMiniMapModule(OfficialMainlineMapRuntime host) : base(host) { }

            public void DisposeRendering()
            {
                if (minimapCamera != null) minimapCamera.targetTexture = null;
                if (minimapTexture != null)
                {
                    minimapTexture.Release();
                    Destroy(minimapTexture);
                }
                minimapTexture = null;
                if (minimapImage != null) Destroy(minimapImage.gameObject);
                minimapImage = null;
            }
        }
    }
}
