using System;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.HomeAirship
{
    // Offline access excludes player trading, so the local ledger has no entries.
    [Serializable]
    public sealed class TipDotTradeLedgerHasTipConditionNode : TipDotConditionNodeBase
    {
        public override bool CheckShowTip() => false;
        public override TipDotNodeBase Clone() => new TipDotTradeLedgerHasTipConditionNode();
    }
}
