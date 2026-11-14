using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Home;
using Zjcs.Offline.HomeUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        OfflineHomeFastForwardRequests fastForwardRequests;
        int fastForwardPresentationHolds;
        public event Action BedFastHangUpPresented;
        internal void PublishPresentedBedFastHangUp() => BedFastHangUpPresented?.Invoke();

        sealed class FastForwardPresentationHold : IDisposable
        {
            HomeFeatureRuntime owner;
            internal FastForwardPresentationHold(HomeFeatureRuntime owner) { this.owner = owner; }
            public void Dispose()
            {
                var previous = owner;
                owner = null;
                if (previous != null) previous.fastForwardPresentationHolds--;
            }
        }

        internal IDisposable HoldFastForwardAwards()
        {
            fastForwardPresentationHolds++;
            return new FastForwardPresentationHold(this);
        }

        internal OfflineHomeFastForwardResult RequestPresentedFastForward(int fastId, bool free,
            IReadOnlyDictionary<int, long> costs)
        {
            profile.Reload();
            ReloadContext();
            RefreshIncomeCatalog();
            session.AdvanceTo(Now());
            var definition = session.Definitions.FirstOrDefault(value => value.FastHangUpId == fastId);
            string receipt = (free && definition != null
                ? tutorialHost?.FastForwardReceiptFor(definition.HangUpId, fastId) : null)
                ?? ("home:fast-window:" + slotId + ":" + Guid.NewGuid().ToString("N"));
            var operation = free ? FastForwardRequests.PrepareFree(fastId, receipt)
                : FastForwardRequests.PrepareItems(fastId, costs, receipt);
            if (operation == null) return null;
            var result = RecoverPendingFastForward();
            if (result != null && free) FastForwardAppliedForTutorial?.Invoke(receipt);
            hud.RefreshAll();
            return result;
        }

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
