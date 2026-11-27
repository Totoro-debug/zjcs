using System;
using System.Linq;
using Zjcs.Offline.Progression;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlots;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.BattlePlanRules;
using Zjcs.Official.BattlePlanUI;
using UnityEngine;
using BattlePlanType=Zjcs.Official.BattlePlanRules.BattlePlanType;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasureSlotsPanelServices : ITreasureSlotsPanelServices,IDisposable
    {
        private readonly OfflineTreasureSlotService slots;
        private readonly OfflineTreasureIconFactory factory;
        private string membership;
        private bool disabled,disposed;
        public ITreasureIconServices Icons{get;}
        public IBattlePlanWidgetServices Plans{get;}
        public IBattlePlanContentRequests Requests{get;}
        public float TreasureSlotPropAddRate{get;}
        public event Action OnSetupTreasureSlot;
        public event Action<BattlePlanType> OnFunctionDisabledUpdate;
        public OfflineTreasureSlotsPanelServices(ITreasureIconServices icons,IBattlePlanWidgetServices plans,
            IBattlePlanContentRequests requests,OfflineTreasureSlotService slots,OfflineTreasureIconFactory factory,float propRate)
        {
            Icons=icons??throw new ArgumentNullException(nameof(icons));Plans=plans??throw new ArgumentNullException(nameof(plans));
            Requests=requests??throw new ArgumentNullException(nameof(requests));this.slots=slots??throw new ArgumentNullException(nameof(slots));
            this.factory=factory??throw new ArgumentNullException(nameof(factory));TreasureSlotPropAddRate=propRate;
            membership=Membership();disabled=Plans.IsPlanFunctionDisabled(BattlePlanType.Treasure);slots.OnSetupTreasureSlot+=SetupChanged;
        }
        public PlayerTreasureElementSlotsDataWrap GetTreasureElementSlotData(ElementType type)=>slots.Snapshot().GetTreasureElementSlotData(type);
        public GameObject InstantiateIcon(GameObject prefab,Transform parent)=>factory.Instantiate(prefab,parent);
        private string Membership()
        {
            var player=slots.Snapshot();
            return player.ActivePlanIndex+":"+string.Join(";",player.TreasureSlotData.TreasureSlotDataDict.Select(element=>(int)element.Key+"="+
                string.Join(",",element.Value.TreasureSlots.Select(slot=>slot.Key+":"+slot.Value.TreasureClassId))));
        }
        private void SetupChanged(){membership=Membership();OnSetupTreasureSlot?.Invoke();}
        // Call after the enclosing window reloads and synchronizes its saved player.
        public void Synchronize()
        {
            if(disposed)return;
            string next=Membership();if(next!=membership){membership=next;OnSetupTreasureSlot?.Invoke();}
            bool value=Plans.IsPlanFunctionDisabled(BattlePlanType.Treasure);
            if(value!=disabled){disabled=value;OnFunctionDisabledUpdate?.Invoke(BattlePlanType.Treasure);}
        }
        public void Dispose(){if(disposed)return;disposed=true;slots.OnSetupTreasureSlot-=SetupChanged;}
    }
}
