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
        private readonly IBattlePlanContentRequests requests;
        private int confirmed;
        public OfflineTreasurePlanActions(PlanSwitchWidget widget,IBattlePlanWidgetServices services,OfflineTreasureSlotService slots,Action<ProgressionResult> failed)
        {
            this.widget=widget??throw new ArgumentNullException(nameof(widget));this.services=services??throw new ArgumentNullException(nameof(services));
            requests=new OfflineTreasurePlanRequests(slots,failed);
            RefreshConfirmed();widget.OnPlanChanged+=Changed;widget.OnClearClicked+=ClearClicked;
        }
        public void RefreshConfirmed()=>confirmed=services.GetActivePlanIndex(BattlePlanType.Treasure);
        private void Changed(int index)
        {
            widget.SetInteractable(false);
            requests.Apply(BattlePlanType.Treasure,index,result=>{
                widget.SetInteractable(true);
                if(!result.Success) {widget.RollbackTo(confirmed);result.ReportFailure();}
                else {
                    confirmed=index;
                    string name=services.GetBattlePlanName(BattlePlanType.Treasure,index,services.GetPlanName(BattlePlanType.Treasure,index));
                    services.SystemMessage("ui_treasure_plan_switch_tip",name);
                }
            });
        }
        private void ClearClicked(int index)=>services.Confirm(widget,"ui_battle_plan_clear_confirm_tip",()=>{
            requests.Clear(BattlePlanType.Treasure,index,result=>{
                if(result.Success) {widget.RefreshView();services.SystemMessage("ui_battle_plan_clear_success_tip");}
                else result.ReportFailure();
            });
        });
        public void Dispose() {widget.OnPlanChanged-=Changed;widget.OnClearClicked-=ClearClicked;}
    }
}
