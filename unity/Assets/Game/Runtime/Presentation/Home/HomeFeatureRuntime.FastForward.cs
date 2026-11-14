using System;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        OfflineHomeFastForwardRequests fastForwardRequests;

        OfflineHomeFastForwardRequests FastForwardRequests => fastForwardRequests
            ?? (fastForwardRequests = new OfflineHomeFastForwardRequests(rules, session, profile,
                () => context, new OfflineHomeOutputAddition(session, () => DateTime.UtcNow, _ => false)));

        public OfflineHomeFastForwardResult RecoverPendingFastForward()
        {
            if (session?.PendingFastForwardOperation == null) return null;
            OfflineHomeFastForwardResult result;
            try { result = FastForwardRequests.CompletePending(DateTime.UtcNow); }
            catch (Exception error)
            {
                UnityEngine.Debug.LogWarning("Home acceleration remains pending for recovery: " + error.Message);
                return null;
            }
            if (result != null) ReloadContext();
            return result;
        }
    }
}
