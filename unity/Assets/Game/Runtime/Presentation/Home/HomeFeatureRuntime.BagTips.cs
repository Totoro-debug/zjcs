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
        internal void RefreshLocalCharacterBindings()
        {
            if (profile == null || scene == null || hud == null) return;
            // The Bag and character windows have separate profile sessions.
            // Resume Home on the last committed state of the same character.
            profile.Reload();
            RefreshPartnerDerivedState();
            bagTipServices?.Synchronize();
        }
    }
}
