using System;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    // Only the original TipDotInfo fields consumed by the graph runtime.
    public sealed class PartnerTipDotGraphInfo
    {
        public string GraphName;
        public TipDotGraphType GraphType;
        public TipDotGraphScope GraphScope;
    }

    public sealed class PartnerTipDotRuntimeBindings
    {
        public readonly Func<bool> HasPlayer;
        public readonly Func<PartnerTipDotGraphInfo[]> GetAllGraphs;
        public readonly Func<string, TipDotGraphType?> GetGraphType;
        public readonly Func<Action<object>, Action, IDisposable> SubscribeScope;
        public readonly Func<Action, IDisposable> SubscribePlayerUpdate;

        public PartnerTipDotRuntimeBindings(Func<bool> hasPlayer,
            Func<PartnerTipDotGraphInfo[]> getAllGraphs, Func<string, TipDotGraphType?> getGraphType,
            Func<Action<object>, Action, IDisposable> subscribeScope, Func<Action, IDisposable> subscribePlayerUpdate)
        {
            HasPlayer = hasPlayer ?? throw new ArgumentNullException(nameof(hasPlayer));
            GetAllGraphs = getAllGraphs ?? throw new ArgumentNullException(nameof(getAllGraphs));
            GetGraphType = getGraphType ?? throw new ArgumentNullException(nameof(getGraphType));
            SubscribeScope = subscribeScope ?? throw new ArgumentNullException(nameof(subscribeScope));
            SubscribePlayerUpdate = subscribePlayerUpdate ?? throw new ArgumentNullException(nameof(subscribePlayerUpdate));
        }
    }
}
