// Complete original slot controller; explicit local dependencies only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.BattlePlanUI;
using BattlePlanType=Zjcs.Official.BattlePlanRules.BattlePlanType;
using Zjcs.Official.TreasureSlots;
namespace Zjcs.Official.TreasureSlotsUI {
using Object=UnityEngine.Object;
using System;
using System.Collections.Generic;



using UnityEngine;

public class TreasureSlotsPanelContent : MonoBehaviour, IWindowOperator
{
    private ITreasureSlotsPanelServices services;
    private ITreasureSlotsPanelServices Services => services ?? throw new System.InvalidOperationException("Bind original treasure slot services before activation.");
    public void Configure(ITreasureSlotsPanelServices value) => services=value??throw new System.ArgumentNullException(nameof(value));

	[SerializeField]
	private List<TreasureSlotItem> mSlotItems;

	[SerializeField]
	private WorldMultiLanguageText mShareLevelText;

	[SerializeField]
	private WorldMultiLanguageText mExtraPropUpDescText;

	[Header("方案切换")]
	[SerializeField]
	private PlanSwitchWidget mPlanSwitchWidget;

	private ElementType mType;

	private int mConfirmedTreasurePlanIndex;


	public GameObject Operator => ((Component)this).gameObject;

	public void Init(ElementType type)
	{
		mType = type;
		Refresh();
	}

	private void OnEnable()
	{
		InitPlanSwitchWidget();
		RefreshPlanSwitchVisible();
		Services.OnFunctionDisabledUpdate += OnFunctionDisabledUpdate;
		ITreasureReplaceEventLisener replaceEventListener = Services.Icons.ReplaceListener;
		replaceEventListener.OnTreasureStartReplace = (Action<TreasureReplaceInfo>)Delegate.Combine(replaceEventListener.OnTreasureStartReplace, new Action<TreasureReplaceInfo>(OnStartReplace));
		ITreasureReplaceEventLisener replaceEventListener2 = Services.Icons.ReplaceListener;
		replaceEventListener2.OnTreasureCancelReplace = (Action)Delegate.Combine(replaceEventListener2.OnTreasureCancelReplace, new Action(OnCancelReplace));
		Services.OnSetupTreasureSlot += Refresh;
		Services.Icons.OnTreasureRefresh += Player_OnTreasureRefresh;
		Services.Icons.OnAllTreasureBlessUpdate += Refresh;
	}

	private void OnDisable()
	{
		if ((Object)(object)mPlanSwitchWidget != (Object)null)
		{
			mPlanSwitchWidget.OnPlanChanged -= OnTreasurePlanChanged;
			mPlanSwitchWidget.OnClearClicked -= OnTreasurePlanClearClicked;
		}
		if (services != null)
		{
			Services.OnFunctionDisabledUpdate -= OnFunctionDisabledUpdate;
		}
		ITreasureReplaceEventLisener replaceEventListener = Services.Icons.ReplaceListener;
		replaceEventListener.OnTreasureStartReplace = (Action<TreasureReplaceInfo>)Delegate.Remove(replaceEventListener.OnTreasureStartReplace, new Action<TreasureReplaceInfo>(OnStartReplace));
		ITreasureReplaceEventLisener replaceEventListener2 = Services.Icons.ReplaceListener;
		replaceEventListener2.OnTreasureCancelReplace = (Action)Delegate.Remove(replaceEventListener2.OnTreasureCancelReplace, new Action(OnCancelReplace));
		if (services != null)
		{
			Services.OnSetupTreasureSlot -= Refresh;
			Services.Icons.OnTreasureRefresh -= Player_OnTreasureRefresh;
			Services.Icons.OnAllTreasureBlessUpdate -= Refresh;
		}
	}

	private void Player_OnTreasureRefresh(IProfessionItemData itemData, bool isNew)
	{
		Refresh();
	}

	private void OnStartReplace(TreasureReplaceInfo info)
	{
		foreach (TreasureSlotItem mSlotItem in mSlotItems)
		{
			mSlotItem.PrepareReplace(info);
		}
	}

	private void OnCancelReplace()
	{
		foreach (TreasureSlotItem mSlotItem in mSlotItems)
		{
			mSlotItem.CancelReplace();
		}
	}

	private void OnTreasureSlotChanged(ElementType type)
	{
		if (mType == type)
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		PlayerTreasureElementSlotsDataWrap treasureElementSlotData = Services.GetTreasureElementSlotData(mType);
		mShareLevelText.Format(treasureElementSlotData?.GetShareLevel() ?? 1);
		mExtraPropUpDescText.Format(Services.TreasureSlotPropAddRate * 100f);
		for (int i = 0; i < mSlotItems.Count; i++)
		{
			TreasureSlotItem treasureSlotItem = mSlotItems[i];
			int treasureClassId = 0;
			if (treasureElementSlotData != null && treasureElementSlotData.TreasureSlots.TryGetValue(i, out var value) && Services.Icons.GetItemById(value.TreasureClassId) != null)
			{
				treasureClassId = value.TreasureClassId;
			}
			treasureSlotItem.SetSlotIndex(i);
			treasureSlotItem.SetTreasure(mType, treasureClassId);
		}
	}

	private void InitPlanSwitchWidget()
	{
		if (!((Object)(object)mPlanSwitchWidget == (Object)null))
		{
			mPlanSwitchWidget.Init(BattlePlanType.Treasure, PlanSwitchWidget.PlanIndexSource.ActivePlanIndex, 0, skipEmptyPlan: false);
			mPlanSwitchWidget.OnPlanChanged += OnTreasurePlanChanged;
			mPlanSwitchWidget.OnClearClicked += OnTreasurePlanClearClicked;
			mConfirmedTreasurePlanIndex = Services.Plans.GetActivePlanIndex(BattlePlanType.Treasure);
		}
	}

	private void RefreshPlanSwitchVisible()
	{
		if (!((Object)(object)mPlanSwitchWidget == (Object)null))
		{
			((Component)mPlanSwitchWidget).gameObject.SetActive(!Services.Plans.IsPlanFunctionDisabled(BattlePlanType.Treasure));
		}
	}

	private void OnFunctionDisabledUpdate(BattlePlanType type)
	{
		if (type == BattlePlanType.Treasure)
		{
			RefreshPlanSwitchVisible();
		}
	}

	private void OnTreasurePlanChanged(int planIndex)
	{
		mPlanSwitchWidget.SetInteractable(interactable: false);
		Services.Requests.Apply(BattlePlanType.Treasure, planIndex, (BattlePlanRequestOutcome result) =>
		{
			mPlanSwitchWidget.SetInteractable(interactable: true);
			if (!result.Success)
			{
				mPlanSwitchWidget.RollbackTo(mConfirmedTreasurePlanIndex);
				result.ReportFailure();
			}
			else
			{
				mConfirmedTreasurePlanIndex = planIndex;
				string battlePlanName = Services.Plans.GetBattlePlanName(BattlePlanType.Treasure, planIndex, Services.Plans.GetPlanName(BattlePlanType.Treasure, planIndex));
				Services.Plans.SystemMessage("ui_treasure_plan_switch_tip", battlePlanName);
			}
		});
	}

	private void OnTreasurePlanClearClicked(int planIndex)
    {
        Services.Plans.Confirm(this,"ui_battle_plan_clear_confirm_tip",()=>ConfirmClearTreasurePlan(planIndex));
    }

	private void ConfirmClearTreasurePlan(int planIndex)
	{
		Services.Requests.Clear(BattlePlanType.Treasure, planIndex, (BattlePlanRequestOutcome result) =>
		{
			if (result.Success)
			{
				mPlanSwitchWidget.RefreshView();
				Services.Plans.SystemMessage("ui_battle_plan_clear_success_tip");
			}
		});
	}
}

}
