// Selected official detail-property controller; see partner-detail-properties-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerDetailNpcInfo;
using PlayerNpcDataWrap = Zjcs.Official.PartnersUI.IPartnerDetailNpcData;
using NpcTargetData = Zjcs.Official.PartnersUI.PartnerDetailNpcTarget;
using LiteInfoCallback = Zjcs.Official.PartnersUI.PartnerDetailLiteInfoCallback;
using PlayerNpcCollectRep = Zjcs.Official.PartnersUI.PartnerDetailCollectedReply;
using NavigateResult = Zjcs.Official.PartnersUI.PartnerDetailNavigateResult;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NpcDetailPanelPropContent : UIBase
{
	[SerializeField]
	private Button mPropBtn;

	[SerializeField]
	private Button mJumpBtn;

	[SerializeField]
	private Button mTipBtn;

	[SerializeField]
	private Button mCollectionBtn;

	[SerializeField]
	private MultiLanguageText mLevelText;

	[SerializeField]
	private Transform mSlot;

	[SerializeField]
	private GameObject mPropItemGroupPrefab;

	[SerializeField]
	private WorldStateSwitcher<bool> mPropStateSwitcher;

	[SerializeField]
	private Image mUnCollectedImg;

	[SerializeField]
	private Image mCollectedImg;

	private NpcInfo mNpcInfo;

	private PlayerNpcDataWrap mNpcData;

	private NpcTargetData mNpcTargetData;

	private LiteInfoCallback mLiteInfoCallback;

    private IPartnerDetailPropertyServices services;
    public void Configure(IPartnerDetailPropertyServices value)
    { services = value ?? throw new ArgumentNullException(nameof(value)); }

	protected override void OnDestroy()
	{
		mLiteInfoCallback?.Release();
		mLiteInfoCallback = null;
		base.OnDestroy();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		((UnityEvent)mPropBtn.onClick).AddListener((UnityAction)OnClickPropBtn);
		((UnityEvent)mJumpBtn.onClick).AddListener((UnityAction)OnClickJumpBtn);
		((UnityEvent)mTipBtn.onClick).AddListener((UnityAction)OnClickTipBtn);
		((UnityEvent)mCollectionBtn.onClick).AddListener((UnityAction)OnClickCollectionBtn);
		ShowContent();
	}

	protected override void OnDisableAndInited()
	{
		((UnityEvent)mPropBtn.onClick).RemoveListener((UnityAction)OnClickPropBtn);
		((UnityEvent)mJumpBtn.onClick).RemoveListener((UnityAction)OnClickJumpBtn);
		((UnityEvent)mTipBtn.onClick).RemoveListener((UnityAction)OnClickTipBtn);
		((UnityEvent)mCollectionBtn.onClick).RemoveListener((UnityAction)OnClickCollectionBtn);
		base.OnDisableAndInited();
	}

	public void Init(PlayerNpcDataWrap npcDataWrap, NpcInfo npcInfo)
	{
		InitWrapper(() =>
		{
			mNpcData = npcDataWrap;
			mNpcInfo = npcInfo;
			mNpcTargetData = services.CreateNpcTarget(mNpcData.NpcClassId);
		});
	}

	private void ShowContent()
	{
		if (mNpcData != null)
		{
			if (!services.IsPinFunctionLimited)
			{
				((Component)mCollectionBtn).gameObject.SetActive(true);
				bool isCollected = mNpcData.IsCollected;
				((Component)mUnCollectedImg).gameObject.SetActive(!isCollected);
				((Component)mCollectedImg).gameObject.SetActive(isCollected);
			}
			else
			{
				((Component)mCollectionBtn).gameObject.SetActive(false);
			}
		}
		int num = 0;
		IReadOnlyDictionary<PropType, long> propDict = services.CalcPlayerNpcProps(mNpcInfo.Id);
		SortedDictionary<PropType, long> sortPropTypeDict = GetSortPropTypeDict(propDict);
		mSlot.DestroyChildren();
		Dictionary<int, List<(PropType, long)>> dictionary = new Dictionary<int, List<(PropType, long)>>();
		foreach (KeyValuePair<PropType, long> item in sortPropTypeDict)
		{
			if (item.Value != 0L && services.IsUnlockTravelNpcProp(item.Key))
			{
				int key = num / 2;
				if (!dictionary.TryGetValue(key, out var value))
				{
					value = new List<(PropType, long)>();
					dictionary.Add(key, value);
				}
				value.Add((item.Key, item.Value));
				num++;
			}
		}
		foreach (KeyValuePair<int, List<(PropType, long)>> item2 in dictionary)
		{
			bool showBg = item2.Key % 2 == 0;
			NpcFriendshipPropItemGroup group = PartnerDetailUiUtils.Instantiate<NpcFriendshipPropItemGroup>(mPropItemGroupPrefab, mSlot);
            group.Configure(services);
            group.Init(addPropsList: GetNpcTreasurePropsList(mNpcInfo.TreasureClassId, item2.Value), propList: item2.Value, showBg: showBg);
		}
		mPropStateSwitcher.SetState(dictionary.Count > 0);
		bool active = services.CanNavigateNpc(mNpcInfo.Id);
		((Component)mJumpBtn).gameObject.SetActive(active);
		mLevelText.Format(mNpcData.FriendShipLevel);
	}

	private SortedDictionary<PropType, long> GetSortPropTypeDict(IReadOnlyDictionary<PropType, long> propDict)
	{
		SortedDictionary<PropType, long> sortedDictionary = new SortedDictionary<PropType, long>();
		if (propDict == null || propDict.Count == 0)
		{
			return sortedDictionary;
		}
		foreach (KeyValuePair<PropType, long> item in propDict)
		{
			sortedDictionary[item.Key] = item.Value;
		}
		return sortedDictionary;
	}

	private List<(PropType propType, long value)> GetNpcTreasurePropsList(int treasureClassId, List<(PropType propType, long value)> propsList)
	{
		if (!services.IsTreasureActive(treasureClassId))
		{
			return null;
		}
		double treasureToNpcFriendshipPercent = services.GetTreasureToNpcFriendshipPercent(treasureClassId);
		if (treasureToNpcFriendshipPercent <= 0.0)
		{
			return null;
		}
		List<(PropType, long)> list = new List<(PropType, long)>();
		foreach (var props in propsList)
		{
			long item = (long)((double)props.value * treasureToNpcFriendshipPercent);
			list.Add((props.propType, item));
		}
		return list;
	}

	private void OnClickTipBtn()
	{
        services.PopupFriendship(this, mNpcInfo.IsStarGodNpc, dialog =>
        {
            if (dialog != null) dialog.Init(mNpcInfo.Friendship);
        });
	}

	private void OnClickCollectionBtn()
	{
        services.SetCollected(mNpcInfo.Id, !mNpcData.IsCollected, OnClickCollectionBtnCompleted);
	}

	private void OnClickCollectionBtnCompleted(PlayerNpcCollectRep response, int errCode)
	{
        if (errCode != 0)
        {
            Debug.LogError("OperPlayerCollectNpc failed, errCode:" + errCode);
        }
        else if (response == null)
        {
            Debug.LogError("mCollectionBtn OperPlayerCollectNpc response is null");
        }
		else
		{
			bool isCollected = response.isCollected;
			((Component)mUnCollectedImg).gameObject.SetActive(!isCollected);
			((Component)mCollectedImg).gameObject.SetActive(isCollected);
			string key;
			if (isCollected)
			{
				key = (mNpcInfo.IsStarGodNpc ? "ui_had_collect_stargod_npc" : "ui_had_collect_npc");
			}
			else
			{
				key = (mNpcInfo.IsStarGodNpc ? "ui_had_not_collect_stargod_npc" : "ui_had_not_collect_npc");
			}
			services.SystemMessage(services.GetText(key));
		}
	}

	private void NavigateCallBack(NavigateResult ret)
	{
		if (!ret.IsSuccess && services.IsNavigateTipValid(ret.TipMsg))
		{
			services.BroadcastNavigateTip(ret.TipMsg);
		}
	}

	private void OnClickPropBtn()
	{
        services.GetLiteInfo(mNpcTargetData, mLiteInfoCallback ?? (mLiteInfoCallback = new PartnerDetailLiteInfoCallback((PartnerDetailLiteInfo liteInfo) =>
        {
            mNpcTargetData.NpcLevel = liteInfo.Level;
            mNpcTargetData.NpcSubRank = liteInfo.SubRank;
            mNpcTargetData.CreatePlanesRank = liteInfo.TargetData.CreatePlanesRank;
            services.PopupNpcBrief(this, mNpcTargetData);
        })));
	}

	private void OnClickJumpBtn()
	{
        services.NavigateNpcPosition(mNpcInfo.Id, NavigateCallBack);
	}
}

}
