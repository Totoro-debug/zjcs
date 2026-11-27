// Complete original slot controller; explicit local dependencies only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.BattlePlanUI;
using BattlePlanType=Zjcs.Official.BattlePlanRules.BattlePlanType;
using Zjcs.Official.TreasureSlots;
namespace Zjcs.Official.TreasureSlotsUI {
using Object=UnityEngine.Object;


using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TreasureSlotItem : MonoBehaviour
{
    private ITreasureSlotsPanelServices services;
    private ITreasureSlotsPanelServices Services => services ?? throw new System.InvalidOperationException("Bind original treasure slot services before activation.");
    public void Configure(ITreasureSlotsPanelServices value) => services=value??throw new System.ArgumentNullException(nameof(value));

	[SerializeField]
	private Transform mTreasureIconSlot;

	[SerializeField]
	private GameObject mTreasureItemPrefab;

	[SerializeField]
	private WorldStateSwitcher<ElementType> mElementBgStates;

	[SerializeField]
	private Button mReplaceBtn;

	private ItemIconTreasurePanel mItemIcon;

	private int mTreasureClassId;

	private int mSlotIndex;

	private TreasureReplaceInfo mReplaceInfo;


	private void OnEnable()
	{


		((UnityEvent)mReplaceBtn.onClick).AddListener((UnityAction)OnReplace);
	}

	private void OnDisable()
	{


		((UnityEvent)mReplaceBtn.onClick).RemoveListener((UnityAction)OnReplace);
		mElementBgStates.Clear();
		CancelReplace();
	}

	public void SetSlotIndex(int slotIndex)
	{
		mSlotIndex = slotIndex;
	}

	public void SetTreasure(ElementType type, int treasureClassId)
	{
		mElementBgStates.SetState(type);
		if (treasureClassId <= 0)
		{
			((Component)mTreasureIconSlot).gameObject.SetActive(false);
			return;
		}
		((Component)mTreasureIconSlot).gameObject.SetActive(true);
		if ((Object)(object)mItemIcon == (Object)null)
		{
			mItemIcon = Services.InstantiateIcon(mTreasureItemPrefab, mTreasureIconSlot).GetComponent<ItemIconTreasurePanel>();
		}
		IProfessionItemData itemById = Services.Icons.GetItemById(treasureClassId);
		mItemIcon.Init(itemById);
		mItemIcon.SetLevel();
		mItemIcon.ShowStar().SetRedDot().SetAttachment(ItemIconAttachmentType.NotGotTip, show: false)
			.SetAttachment(ItemIconAttachmentType.Upgrade, show: true)
			.SetClickListener((ItemIcon itemIcon) =>
			{
				itemIcon.PopupInfoDialog((GameObject go) =>
				{
					ITreasureDetailOptions component = go.GetComponent<ITreasureDetailOptions>();
					if (component != null)
					{
						component.SetCanOperation(canOperation: true);
						component.SetCanShowReplace(canShow: true);
						component.SetCanShowNpcTreasureAndState(canShow: true, canShowState: false);
					}
				});
			});
		mItemIcon.UnhasShow(isShow: false);
		mTreasureClassId = treasureClassId;
	}

	public void PrepareReplace(TreasureReplaceInfo info)
	{
		mReplaceInfo = info;
		((Component)mReplaceBtn).gameObject.SetActive(true);
	}

	public void CancelReplace()
	{
		((Component)mReplaceBtn).gameObject.SetActive(false);
	}

	private void OnReplace()
	{
		if (mReplaceInfo != null)
		{
			if (!Services.Icons.CanSetupTreasureSlot(mReplaceInfo.TreasureClassId, mSlotIndex))
			{
				Services.Icons.ReplaceListener.FireOnTreasureCancelReplace();
				return;
			}
			Services.Icons.SetupTreasureSlot(new PlayerSetupTreasureSlotRequest
			{
				TreasureClassId = mReplaceInfo.TreasureClassId,
				TargetIndex = mSlotIndex
			});
		}
	}
}

}
