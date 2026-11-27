using System;
using Zjcs.Offline.Progression;
using Zjcs.Official.BattlePlanRules;
using Zjcs.Official.BattlePlanUI;

namespace Zjcs.Offline.TreasureUI
{
    // TreasureSlotsPanelContent's original plan-change/clear callbacks, with durable local requests.
    public sealed class OfflineTreasurePlanActions : IDisposable
    {
        private readonly PlanSwitchWidget widget;
        private readonly IBattlePlanWidgetServices services;
        private readonly OfflineTreasureSlotService slots;
        private readonly Action<ProgressionResult> failed;
        private int confirmed;
        public OfflineTreasurePlanActions(PlanSwitchWidget widget,IBattlePlanWidgetServices services,OfflineTreasureSlotService slots,Action<ProgressionResult> failed)
        {
            this.widget=widget??throw new ArgumentNullException(nameof(widget));this.services=services??throw new ArgumentNullException(nameof(services));
            this.slots=slots??throw new ArgumentNullException(nameof(slots));this.failed=failed??throw new ArgumentNullException(nameof(failed));
            RefreshConfirmed();widget.OnPlanChanged+=Changed;widget.OnClearClicked+=ClearClicked;
        }
        public void RefreshConfirmed()=>confirmed=services.GetActivePlanIndex(BattlePlanType.Treasure);
        private void Changed(int index)
        {
            widget.SetInteractable(false);
            var result=slots.ApplyTreasurePlan(index,"treasure-plan-apply-"+Guid.NewGuid().ToString("N"));
            widget.SetInteractable(true);
            if(!result.IsApplied && result.Status!=ProgressionResultStatus.Unchanged) {widget.RollbackTo(confirmed);failed(result);}
            else {
                confirmed=index;
                string name=services.GetBattlePlanName(BattlePlanType.Treasure,index,services.GetPlanName(BattlePlanType.Treasure,index));
                services.SystemMessage("ui_treasure_plan_switch_tip",name);
            }
        }
        private void ClearClicked(int index)=>services.Confirm(widget,"ui_battle_plan_clear_confirm_tip",()=>{
            var result=slots.ClearTreasurePlan(index,"treasure-plan-clear-"+Guid.NewGuid().ToString("N"));
            if(result.IsApplied || result.Status==ProgressionResultStatus.Unchanged) {widget.RefreshView();services.SystemMessage("ui_battle_plan_clear_success_tip");}
            else failed(result);
        });
        public void Dispose() {widget.OnPlanChanged-=Changed;widget.OnClearClicked-=ClearClicked;}
    }
}
