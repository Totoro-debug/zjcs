// Complete original equipped-treasure page controller; explicit local services.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlots;
namespace Zjcs.Official.TreasureSlotsUI {
using System;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ItemIconTreasureSelectableItem : MonoBehaviour
{
    private ITreasurePageServices services;
    private ITreasurePageServices Services => services ?? throw new InvalidOperationException("Bind original treasure page services before activation.");
    public void Configure(ITreasurePageServices value) => services=value??throw new ArgumentNullException(nameof(value));

	public Action<int> OnSelectedTreasure;

	public ItemIconTreasure ItemIcon;

	[SerializeField]
	private WorldStateSwitcher<bool> mSelectStates;

	[SerializeField]
	private WorldStateSwitcher<bool> mEmptyStates;

	[SerializeField]
	private Button mSelectBtn;

	private int mTreasureClassId;

	private void OnEnable()
	{


		((UnityEvent)mSelectBtn.onClick).AddListener((UnityAction)SelectedItem);
	}

	private void OnDisable()
	{


		((UnityEvent)mSelectBtn.onClick).RemoveListener((UnityAction)SelectedItem);
	}

	public void InitItem(int treasureClassId)
	{
		mTreasureClassId = treasureClassId;
		IProfessionItemData itemById = Services.Icons.GetItemById(treasureClassId);
		if (itemById == null)
		{
			SetEmpty(isEmpty: true);
			return;
		}
		ItemIcon.Init(itemById);
		ItemIcon.ShowStar();
		ItemIcon.SetLevel();
		ItemIcon.SetRedDot();
		ItemIcon.SetAttachment(ItemIconAttachmentType.Upgrade, show: true);
		SetEmpty(isEmpty: false);
	}

	private void SelectedItem()
	{
		if (mTreasureClassId > 0)
		{
			OnSelectedTreasure?.Invoke(mTreasureClassId);
		}
	}

	public void OnSelected(int treasureClassId)
	{
		bool state = mTreasureClassId == treasureClassId;
		mSelectStates.SetState(state);
	}

	public void SetEmpty(bool isEmpty)
	{
		mEmptyStates.SetState(isEmpty);
	}

	public void Clear()
	{
		ItemIcon.Clear();
	}
}

}
