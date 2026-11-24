// Selected original controller body; local services replace global client owners.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.HomeFurniture
{
using Object = UnityEngine.Object;
public abstract class BaseHangUpFurniture : FurnitureBase
{
	private sealed class TipServicesAdapter : IHomeHangUpTipServices
	{
		private readonly BaseHangUpFurniture mOwner;
		private readonly IHomeHangUpFurnitureServices mHangUpServices;
		private readonly IHomeHangUpTipServices mInner;

		public TipServicesAdapter(BaseHangUpFurniture owner, IHomeHangUpFurnitureServices hangUpServices)
		{
			mOwner = owner;
			mHangUpServices = hangUpServices;
			mInner = hangUpServices.Tips;
		}

		public IHomeHangUpTipPlayer MainPlayer => mInner?.MainPlayer;
		public IHomeHangUpTipPlayer PlayerData => mInner?.PlayerData;
		public bool InMyHome => mInner != null && mInner.InMyHome;
		public IReadOnlyDictionary<EFurniture, List<int>> RecordHangUpIdDict => mInner?.RecordHangUpIdDict;
		public HomeHangUpTipSettings Settings => mInner?.Settings;
		public long GetLevelUpExp(int level) => mInner != null ? mInner.GetLevelUpExp(level) : 0;

		public void SetTipState(EFurniture furniture, int level)
		{
			mOwner.SetRewardTipState(level);
			mInner?.SetTipState(furniture, level);
			mHangUpServices?.OnTipLevelUpdated(furniture, level);
		}

		public void Publish(HangUpTipLevelChangeData data) => mInner?.Publish(data);
		public IDisposable Wait(float seconds, Action completed) => mInner?.Wait(seconds, completed);
	}

	[SerializeField]
	protected SpriteRenderer AwardIcon;

	[SerializeField]
	protected WorldStateSwitcher<int> mRewardTipSwitcher;

	private readonly List<int> mHangUpIdList = new List<int>();
	private HomeHangUpTipController mTipController;
	private bool mIsCollecting;

	public IHomeHangUpFurnitureServices HangUpServices { get; set; }

	public int TipLevel => mTipController != null ? mTipController.TipLevel : -1;

	public void Bind(EFurniture furniture, WorldStateSwitcher<EFurnitureState> states,
		HomeNotice notice, GameObject repairEffect, float showTime, float fullTime,
		IHomeFurnitureServices services, SpriteRenderer awardIcon,
		WorldStateSwitcher<int> rewardTipSwitcher, IHomeHangUpFurnitureServices hangUpServices)
	{
		base.Bind(furniture, states, notice, repairEffect, showTime, fullTime, services);
		AwardIcon = awardIcon;
		mRewardTipSwitcher = rewardTipSwitcher;
		HangUpServices = hangUpServices ?? throw new ArgumentNullException(nameof(hangUpServices));
	}

	protected override void Start()
	{
		base.Start();
		SetAwardIcon();
		RefreshHangUpIdList();
		EnsureController();
		if (mTipController == null)
		{
			SetRewardTipState(-1);
		}
		else
		{
			mTipController.Start();
			if (((Behaviour)this).isActiveAndEnabled)
			{
				mTipController.Enable();
			}
		}
	}

	protected virtual void OnEnable()
	{
		RefreshHangUpIdList();
		EnsureController();
		if (mTipController != null)
		{
			mTipController.Start();
			mTipController.Enable();
		}
	}

	protected virtual void OnDisable()
	{
		mTipController?.Disable();
	}

	protected override void OnDestroy()
	{
		mTipController?.Dispose();
		mTipController = null;
		base.OnDestroy();
	}

	protected override void OnHomeChange()
	{
		base.OnHomeChange();
		TryBeginHangUp();
	}

	public void SetAwardIcon()
	{
		if ((Object)(object)AwardIcon == (Object)null || HangUpServices == null)
		{
			return;
		}
		Sprite sprite = HangUpServices.LoadAwardSprite(mFurniture);
		if ((Object)(object)sprite != (Object)null)
		{
			AwardIcon.sprite = sprite;
		}
	}

	public void TryBeginHangUp()
	{
		EnsureController();
		mTipController?.TryBeginHangUp();
	}

	public bool CanReceiveAward()
	{
		return HangUpServices?.Tips != null
			&& HangUpServices.Tips.InMyHome
			&& mTipController != null
			&& mTipController.CanReceiveAward();
	}

	public void TryReceiveAward()
	{
		if (mIsCollecting || HangUpServices == null)
		{
			return;
		}
		RefreshHangUpIdList();
		if (mHangUpIdList.Count == 0)
		{
			return;
		}
		mIsCollecting = true;
		HangUpServices.CollectHangUpRewards(mFurniture, mHangUpIdList, (response, code) =>
		{
			try
			{
				OnGetHangUpAward(response, code);
			}
			finally
			{
				mIsCollecting = false;
			}
		});
	}

	protected override void OnClick()
	{
		if (CanReceiveAward())
		{
			TryReceiveAward();
		}
		else
		{
			PopFurnitureWindow();
		}
	}

	protected abstract void PopFurnitureWindow();

	private void OnGetHangUpAward(IHomeHangUpAwardMessage response, int code)
	{
		if (code != 0 || response == null || HangUpServices == null)
		{
			return;
		}
		HangUpServices.AwardCollected(response.DataList, response.SourceData);
		HangUpServices.MergeAndPopupAward(mFurniture, response);
		HangUpServices.PublishHangUpCollect(mFurniture, mHangUpIdList);
	}

	private void SetRewardTipState(int level)
	{
		mRewardTipSwitcher?.SetState(level);
	}

	private void EnsureController()
	{
		if (mTipController == null && HangUpServices?.Tips != null)
		{
			mTipController = new HomeHangUpTipController(mFurniture, new TipServicesAdapter(this, HangUpServices));
		}
	}

	private void RefreshHangUpIdList()
	{
		mHangUpIdList.Clear();
		IHomeHangUpTipServices tips = HangUpServices?.Tips;
		if (tips == null || tips.MainPlayer == null || tips.RecordHangUpIdDict == null)
		{
			return;
		}
		if (!tips.RecordHangUpIdDict.TryGetValue(mFurniture, out List<int> ids) || ids == null)
		{
			return;
		}
		IReadOnlyDictionary<int, HomeHangUpTipRecord> dataDict = tips.MainPlayer.HangUpDataDict;
		for (int i = 0; i < ids.Count; i++)
		{
			int hangUpId = ids[i];
			if (dataDict != null && dataDict.TryGetValue(hangUpId, out HomeHangUpTipRecord record) && record != null && record.IsUnlock)
			{
				mHangUpIdList.Add(hangUpId);
			}
		}
	}
}
}
