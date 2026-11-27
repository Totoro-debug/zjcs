using System;
using UnityEngine;
using Zjcs.Official.BattlePlanRules;
using Zjcs.Official.BattlePlanUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlots;
using BattlePlanType=Zjcs.Official.BattlePlanRules.BattlePlanType;

namespace Zjcs.Official.TreasureSlotsUI
{
    public interface ITreasureSlotsPanelServices
    {
        ITreasureIconServices Icons { get; }
        IBattlePlanWidgetServices Plans { get; }
        IBattlePlanContentRequests Requests { get; }
        event Action OnSetupTreasureSlot;
        event Action<BattlePlanType> OnFunctionDisabledUpdate;
        PlayerTreasureElementSlotsDataWrap GetTreasureElementSlotData(ElementType type);
        float TreasureSlotPropAddRate { get; }
        GameObject InstantiateIcon(GameObject prefab,Transform parent);
    }
}
