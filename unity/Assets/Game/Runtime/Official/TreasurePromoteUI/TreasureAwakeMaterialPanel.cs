// Complete original promotion controller; explicit saved-player, parser and window ports.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TreasureUI;
using Zjcs.Official.TreasureSlotsUI;
using Zjcs.Official.TreasureDetailUI;
using Zjcs.Official.TreasureProperties;
using Zjcs.Official.TreasureCultivation;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using TreasureInfo=Zjcs.Official.TreasureProperties.TreasureInfo;
using PropType=System.Int32;
using ElementType=Zjcs.Official.TreasureSlots.ElementType;
namespace Zjcs.Official.TreasurePromoteUI {
using Object=UnityEngine.Object;
using System.Collections.Generic;
using System.Linq;


using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TreasureAwakeMaterialPanel : UIBase
{
    private ITreasurePromoteServices services;
    private ITreasurePromoteServices Services=>services??throw new InvalidOperationException("Bind original treasure promotion services before activation.");
    public void Configure(ITreasurePromoteServices value)=>services=value??throw new ArgumentNullException(nameof(value));

	[SerializeField]
	private ProfessionCommonMaterial mCommonMaterial;

	[SerializeField]
	private Button mAutoSelectBtn;

	[SerializeField]
	private Transform mItemSlot;

	[SerializeField]
	private GameObject mItemTemplate;

	[SerializeField]
	private GameObject mEmptyGo;

	public Dictionary<int, long> SelectItems = new Dictionary<int, long>();

	public IProfessionItemData ItemData;

	private long mNeedCommonPieceCount;

	private List<(IProfessionItemData It, long Count)> mPieces = new List<(IProfessionItemData, long)>();

	private Dictionary<int, ProfessionUseItemSliderGroup> mMaterialItems = new Dictionary<int, ProfessionUseItemSliderGroup>();

	protected void Awake()
	{


		((UnityEvent)mAutoSelectBtn.onClick).AddListener((UnityAction)OnAutoSelectClick);
	}

	protected override void OnEnable()
	{
		base.OnEnable();
		InitPieceItems();
	}

	protected override void OnDisable()
	{
		mPieces.Clear();
		ItemData = null;
		base.OnDisable();
	}

	private void OnAutoSelectClick()
	{
		long num = 0L;
		foreach (KeyValuePair<int, long> selectItem in SelectItems)
		{
			num += selectItem.Value;
		}
		foreach (var mPiece in mPieces)
		{
			if (num >= mNeedCommonPieceCount)
			{
				break;
			}
			if (!SelectItems.TryGetValue(mPiece.It.Info.ClassId, out var value) || value < mPiece.Count)
			{
				if (!SelectItems.ContainsKey(mPiece.It.Info.ClassId))
				{
					SelectItems.Add(mPiece.It.Info.ClassId, 0L);
				}
				long num2 = mPiece.Count;
				if (num + mPiece.Count > mNeedCommonPieceCount)
				{
					num2 = mNeedCommonPieceCount - num;
				}
				int classId = mPiece.It.Info.ClassId;
				SelectItems[classId] += num2;
				num += num2;
				mMaterialItems[classId].SetCountWithNotify(SelectItems[classId]);
			}
		}
		if (SelectItems.Count > 0)
		{
			Services.SystemMessage(LanguageManager.Instance.GetText("TreasureAwakeMaterialPanelSelectTip"));
		}
		else
		{
			Services.SystemMessage(LanguageManager.Instance.GetText("TreasureAwakeMaterialPanelSelectEmptyTip"));
		}
		RefreshCount();
	}

	private void InitPieceItems()
	{
		mItemSlot.DestroyActivedChildren();
		mNeedCommonPieceCount = Services.GetStarUpCommonPiece(ItemData.Info.ClassId, Services.Icons.GetState(ItemData).Star);
		mCommonMaterial.SetQuality(ItemData.GetQuality()).Show(ProfessionCommonMaterial.State.Show);
		mPieces = Services.GetCommonTreasurePiece(ItemData).ToList();
		mPieces.Sort(((IProfessionItemData It, long Count) a, (IProfessionItemData It, long Count) b) => (int)(a.Count - b.Count));
		mEmptyGo.SetActive((mPieces?.Count ?? 0) <= 0);
		GameObject gameObject = ((Component)mItemSlot).gameObject;
		List<(IProfessionItemData It, long Count)> list = mPieces;
		gameObject.SetActive(list != null && list.Count > 0);
		mMaterialItems.Clear();
		foreach (var mPiece in mPieces)
		{
			IProfessionItemData item = mPiece.It;
			long item2 = mPiece.Count;
			ProfessionUseItemSliderGroup useItemSliderGroup = Services.InstantiateMaterialRow(mItemTemplate,mItemSlot);
			SelectItems.TryGetValue(item.Info.ClassId, out var value);
			useItemSliderGroup.Init(item.Info.ClassId, value, item2);
			useItemSliderGroup.OnValueChangedEvent = OnGroupChanged;
			mMaterialItems.Add(item.Info.ClassId, useItemSliderGroup);
		}
		RefreshCount();
	}

	private void OnGroupChanged(int itemClassId, long count)
	{
		long num = SelectItems.Sum((KeyValuePair<int, long> kvp) => (kvp.Key == itemClassId) ? 0 : kvp.Value);
		if (num + count <= mNeedCommonPieceCount)
		{
			SelectItems[itemClassId] = count;
		}
		else
		{
			SelectItems[itemClassId] = mNeedCommonPieceCount - num;
		}
		mMaterialItems[itemClassId].SetCountWithNotify(SelectItems[itemClassId]);
		RefreshCount();
	}

	private void RefreshCount()
	{
		long num = 0L;
		foreach (KeyValuePair<int, ProfessionUseItemSliderGroup> mMaterialItem in mMaterialItems)
		{
			num += mMaterialItem.Value.UseCount;
		}
		mCommonMaterial.RefreshCount(num, mNeedCommonPieceCount);
	}
}

}
