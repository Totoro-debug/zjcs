using UnityEngine;
using Zjcs.Offline.BagTipDots;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        OfflineBagTipDotAssets bagTipAssets;
        OfflineBagTipDotServices bagTipServices;
        public OfflineBagTipDotServices BagTipServices => bagTipServices;
        void InitializeBagTipDots()
        {
            bagTipAssets = OfflineBagTipDotAssets.LoadDefault();
            bagTipServices = new OfflineBagTipDotServices(() => profile.Save, rules,
                type => session.OwnsFurniture(type), bagTipAssets);
        }
        public void BindBagTipNativeTree(GameObject root)
        {
            if (!root || bagTipAssets == null || partnerTipDots == null) return;
            foreach (var linker in root.GetComponentsInChildren<TipDotNodeLinker>(true))
                if (linker.ConfiguredNodeKey.GraphName == bagTipAssets.GraphName) partnerTipDots.Bind(linker);
        }
        internal void RefreshInventoryBindings()
        {
            if (profile == null || scene == null || hud == null) return;
            // The Bag window uses its own transaction session on the same file.
            // Refresh the existing Home session before querying the committed data.
            profile.Reload();
            RefreshPartnerDerivedState();
            bagTipServices?.Synchronize();
        }
    }
}
