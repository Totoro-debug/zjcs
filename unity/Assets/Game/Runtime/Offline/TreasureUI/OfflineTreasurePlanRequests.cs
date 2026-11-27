using System;
using Zjcs.Offline.Progression;
using Zjcs.Official.BattlePlanRules;
using Zjcs.Official.BattlePlanUI;

namespace Zjcs.Offline.TreasureUI
{
    // Both original plan UI owners use this same durable content boundary.
    public sealed class OfflineTreasurePlanRequests : IBattlePlanContentRequests
    {
        private readonly OfflineTreasureSlotService slots;
        private readonly Action<ProgressionResult> failed;
        public OfflineTreasurePlanRequests(OfflineTreasureSlotService slots,Action<ProgressionResult> failed)
        {this.slots=slots??throw new ArgumentNullException(nameof(slots));this.failed=failed??throw new ArgumentNullException(nameof(failed));}
        public void Apply(BattlePlanType type,int index,Action<BattlePlanRequestOutcome> complete)
        {Require(type,complete);Complete(slots.ApplyTreasurePlan(index,"treasure-plan-apply-"+Guid.NewGuid().ToString("N")),complete);}
        public void Clear(BattlePlanType type,int index,Action<BattlePlanRequestOutcome> complete)
        {Require(type,complete);Complete(slots.ClearTreasurePlan(index,"treasure-plan-clear-"+Guid.NewGuid().ToString("N")),complete);}
        private static void Require(BattlePlanType type,Action<BattlePlanRequestOutcome> complete)
        {
            if(type!=BattlePlanType.Treasure)throw new ArgumentException("Bind this plan type to its own local content owner.",nameof(type));
            if(complete==null)throw new ArgumentNullException(nameof(complete));
        }
        private void Complete(ProgressionResult result,Action<BattlePlanRequestOutcome> complete)
        {
            var outcome=new BattlePlanRequestOutcome(result.IsApplied||result.Status==ProgressionResultStatus.Unchanged,()=>failed(result));
            complete(outcome);
            outcome.ReportFailure();
        }
    }
}
