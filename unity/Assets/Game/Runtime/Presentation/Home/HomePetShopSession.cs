using System;
using System.IO;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Home
{
    // This owner survives navigation between the home and adventure scenes.
    public static class HomePetShopSession
    {
        private static string path;
        public static OfflinePetShopNewFunctionSession Current { get; private set; }

        public static void Begin(string root, string slotId)
        {
            var slots = new ProfileSlotCatalog(root);
            string nextPath = Path.GetFullPath(slots.GetProgressPath(slotId));
            if (Current != null && string.Equals(path, nextPath, StringComparison.OrdinalIgnoreCase)) return;
            Current?.Dispose();
            Current = null;
            path = null;
            var configuration = OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
            var partnerRules = OfficialPartnerAssetCatalog.LoadDefault().Conditions;
            OfflinePetShopNewFunctionSession created = null;
            created = new OfflinePetShopNewFunctionSession(slots, slotId, configuration, partnerRules, () =>
            {
                if (ReferenceEquals(Current, created)) { Current = null; path = null; }
            });
            path = nextPath;
            Current = created;
        }
    }
}
