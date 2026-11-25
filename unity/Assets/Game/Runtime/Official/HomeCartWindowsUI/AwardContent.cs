// Selected 903_156948 client controllers; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.Map13MiniMap;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using HangUpInfo = Zjcs.Official.HomeCartWindowsUI.HomeCartHangUpInfo;
using Player = Zjcs.Official.HomeCartWindowsUI.IHomeCartWindowPlayer;
using ItemAwardIcon = Zjcs.Official.HomeAwardUI.HomeAwardIcon;
using ItemInfo = Zjcs.Official.HomeAwardUI.ItemInfo;
using ItemType = Zjcs.Official.HomeAwardUI.ItemType;
using AwardData = Zjcs.Official.HomeAwardUI.AwardData;
namespace Zjcs.Official.HomeCartWindowsUI
{
public class AwardContent : MonoBehaviour, LoopScrollDataSource
{
    public IHomeCartWindowServices Services { protected get; set; }
	public enum ShowItemStateType
	{
		WaitingShow,
		Showing,
		ShowFinished
	}

	[SerializeField]
	private float mPlayIntervalSeconds;

	[SerializeField]
	private float mShowItemIconTime;

	[SerializeField]
	private LoopScrollRect mAwardLoopScroll;

	[SerializeField]
	private AwardPanelLayoutElement mAwardLayoutElement;

	private ShowItemStateType mShowItemState;

	private bool mIsSelf;

	private List<AwardData> mAwardList = new List<AwardData>();

	private Action<ItemAwardIcon, AwardData> mInitItemIcon;


	private Queue<AwardPanelItemBase> mItemsWaitingShowQueue;

	public ShowItemStateType ShowItemState => mShowItemState;

	public event Action<ShowItemStateType> OnShowItemStateUpdate;

	public void Init(List<AwardData> awardList, bool needMergeItem = true, Action<ItemAwardIcon, AwardData> initItemIcon = null, bool isSelf = true)
	{
		if (mItemsWaitingShowQueue == null)
		{
			mItemsWaitingShowQueue = new Queue<AwardPanelItemBase>();
		}
		LoopScrollRect loopScrollRect = mAwardLoopScroll;
		if (loopScrollRect.dataSource == null)
		{
			loopScrollRect.dataSource = this;
		}
		mIsSelf = isSelf;
		ProcesAwardList(awardList, needMergeItem);
		mInitItemIcon = initItemIcon;
		this.StartCoroutine(ShowIn());
	}

	public void Clear()
	{
		this.StopAllCoroutines();
		mInitItemIcon = null;
		mAwardLoopScroll.ClearCells();
		mItemsWaitingShowQueue.Clear();
	}

	private void ProcesAwardList(List<AwardData> awardList, bool needMergeItem)
	{
		mAwardList.Clear();
		mAwardList.AddRange(awardList);
		if (needMergeItem)
		{
			Services.MergeAwardData(mAwardList);
		}
		for (int num = mAwardList.Count - 1; num >= 0; num--)
		{
			AwardData awardData = mAwardList[num];
			if (Services.IsIgnoreShowAward((awardData.AgiotageId > 0) ? awardData.AgiotageId : awardData.AwardId))
			{
				mAwardList.RemoveAt(num);
			}
		}
	}

	public void ProvideData(Transform transform, int idx)
	{
		AwardPanelItemBase component = transform.GetComponent<AwardPanelItemBase>();
		component.Init(mAwardList[idx], mInitItemIcon, mIsSelf);
		if (mPlayIntervalSeconds > 0f)
		{
			component.Hide();
			mItemsWaitingShowQueue.Enqueue(component);
		}
	}

	private IEnumerator ShowIn()
	{
		if ((Object)mAwardLayoutElement != (Object)null)
		{
			mAwardLayoutElement.SetCurrentRowNumByCellNum(mAwardList.Count);
			yield return null;
		}
		yield return (object)new WaitForSeconds(mShowItemIconTime);
		WaitForSeconds intervalSecond = new WaitForSeconds(mPlayIntervalSeconds);
		if (Services.Player != null)
		{
			Refresh();
			yield return null;
			SetState(ShowItemStateType.Showing);
			AwardPanelItemBase awardItem = default;
			while (mItemsWaitingShowQueue.TryDequeue(out awardItem))
			{
				yield return intervalSecond;
				awardItem.Show();
			}
			SetState(ShowItemStateType.ShowFinished);
		}
	}

	private void Refresh()
	{
		SetState(ShowItemStateType.WaitingShow);
		mAwardLoopScroll.dataSource = this;
		mAwardLoopScroll.totalCount = mAwardList.Count;
		mAwardLoopScroll.RefillCells();
	}

	private void SetState(ShowItemStateType stateType)
	{
		if (mShowItemState != stateType)
		{
			mShowItemState = stateType;
			OnShowItemStateUpdate?.Invoke(mShowItemState);
		}
	}
}
}
