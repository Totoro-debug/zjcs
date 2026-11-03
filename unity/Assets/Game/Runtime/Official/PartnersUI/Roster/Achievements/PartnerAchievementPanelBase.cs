// Selected original friendship achievement UI; see partner-achievement-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using AchievementRules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;
using AchievementType = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementType;
using AchievementState = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementState;
using AchievementInfo = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementInfo;
using AchievementDataKey = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementDataKey;
using AchievementDataBase = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.AchievementData;
using Player = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.Player;
using PlayerAchievementRecordDataWrap = Zjcs.Official.Partners.PartnerFriendshipAchievementRules.PlayerAchievementRecordDataWrap;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PartnerAchievementPanelBase : MonoBehaviour
{
    public WorldStateSwitcher<bool> EmptyStateSwitcher;

    [Header("是否显示已领取的成就")]
    [SerializeField]
    private bool mIsShowReceivedItem;

    [Header("是否显示进行中的成就")]
    [SerializeField]
    private bool mIsShowProcessingItem = true;

    [SerializeField]
    private PanelTitle mPanelTitle;

    [SerializeField]
    private List<PartnerAchievementPanelItem> mPanelItemList;

    private bool mTopDataInitFinished;


    private AchievementType mCurrentType;

    private AchievementDataKey mAchievementDataKey;

    private List<IReadOnlyList<AchievementInfo>> mAchievementInfosList;

    private List<(AchievementState state, AchievementInfo info)> mInfoList = new List<(AchievementState, AchievementInfo)>();

    private Action<int, int> mGetRewardCallBack;

    private List<int> mTempIndexList = new List<int>();

    private bool mIsShowSameTypeItem;

    private Action mItemClick;

    private bool mIsAchievementGroup;

    private IPartnerAchievementUiServices services;
    private int AchievementPanelShowCount => services.AchievementPanelShowCount;
    public void Configure(IPartnerAchievementUiServices value)
    {
        services = value ?? throw new ArgumentNullException(nameof(value));
        foreach (var item in mPanelItemList) item.Configure(value);
    }

    private void Awake()
    {
        mGetRewardCallBack = GetRewardCallBack;
        mAchievementInfosList = new List<IReadOnlyList<AchievementInfo>>();
    }

    public void InitDataKey(AchievementDataKey dataKey, bool receiveSameType = false, Action itemClick = null, bool isAchievementGroup = false)
    {
        mAchievementDataKey = dataKey;
        mCurrentType = dataKey.AchievementType;
        mIsShowSameTypeItem = receiveSameType;
        mItemClick = itemClick;
        mIsAchievementGroup = isAchievementGroup;
        InitAchievementInfos();
    }

    private void InitAchievementInfos()
    {
        mAchievementInfosList.Clear();
        IPartnerAchievementUiServices achievementRawInfoParser = services;
        mTopDataInitFinished = false;
        if (services.ShowAllInfosInSameType(mAchievementDataKey.AchievementType) || mIsShowSameTypeItem)
        {
            foreach (AchievementDataKey item in achievementRawInfoParser.GetAchievementDataKeysBySameType(mAchievementDataKey))
            {
                IReadOnlyList<AchievementInfo> achievementInfoListByType = achievementRawInfoParser.GetAchievementInfoListByType(item);
                if (achievementInfoListByType != null && achievementInfoListByType.Count != 0)
                {
                    mAchievementInfosList.Add(achievementInfoListByType);
                }
            }
            if (mAchievementInfosList.Count <= 0)
            {
                return;
            }
            SetAchievementPanelTitle(mAchievementInfosList[0][0]);
        }
        else
        {
            IReadOnlyList<AchievementInfo> achievementInfoListByType2 = achievementRawInfoParser.GetAchievementInfoListByType(mAchievementDataKey);
            if (achievementInfoListByType2 == null || achievementInfoListByType2.Count == 0)
            {
                return;
            }
            mAchievementInfosList.Add(achievementInfoListByType2);
            SetAchievementPanelTitle(achievementInfoListByType2[0]);
        }
        InitData();
    }

    private void InitData()
    {
        mInfoList.Clear();
        Player mainPlayer = services.Player;
        foreach (IReadOnlyList<AchievementInfo> mAchievementInfos in mAchievementInfosList)
        {
            (int startIndex, int endIndex) achievementStateSearchRange = GetAchievementStateSearchRange(mainPlayer, mAchievementInfos, AchievementPanelShowCount);
            int item = achievementStateSearchRange.startIndex;
            int item2 = achievementStateSearchRange.endIndex;
            for (int i = item; i <= item2; i++)
            {
                AchievementInfo achievementInfo = mAchievementInfos[i];
                if (mainPlayer.IsAlreadyGotAchievementReward(achievementInfo))
                {
                    if (mIsShowReceivedItem)
                    {
                        mInfoList.Add((AchievementState.Received, achievementInfo));
                    }
                }
                else if (mainPlayer.IsCanGetAchievementReward(achievementInfo))
                {
                    mInfoList.Add((AchievementState.Completed, achievementInfo));
                }
                else if (mIsShowProcessingItem)
                {
                    mInfoList.Add((AchievementState.Processing, achievementInfo));
                }
            }
        }
        SortInfoList();
        RefreshPanel();
    }

    private void RefreshPanel()
    {
        EmptyStateSwitcher.SetState(mInfoList.Count == 0);
        if (mInfoList.Count == 0)
        {
            HidePanelItems();
            return;
        }
        InitPanelItems();
    }

    private void InitPanelItems()
    {
        int num = Mathf.Clamp(mInfoList.Count, 0, AchievementPanelShowCount);
        for (int i = 0; i < mPanelItemList.Count; i++)
        {
            PartnerAchievementPanelItem achievementPanelItem = mPanelItemList[i];
            if (num > i)
            {
                ((Component)achievementPanelItem).gameObject.SetActive(true);
                achievementPanelItem.Init(mInfoList[i], mGetRewardCallBack, mIsShowSameTypeItem, mItemClick, mIsAchievementGroup);
            }
            else
            {
                ((Component)achievementPanelItem).gameObject.SetActive(false);
            }
        }
    }

    private void HidePanelItems()
    {
        for (int i = 0; i < mPanelItemList.Count; i++)
        {
            ((Component)mPanelItemList[i]).gameObject.SetActive(false);
        }
    }

    private void GetRewardCallBack(int groupId, int count)
    {
        mTempIndexList.Clear();
        int num = count;
        for (int i = 0; i < mInfoList.Count; i++)
        {
            if (count > 0 && num-- < 0)
            {
                break;
            }
            var (achievementState, item) = mInfoList[i];
            if (achievementState == AchievementState.Completed)
            {
                if (mIsShowReceivedItem)
                {
                    mInfoList[i] = (AchievementState.Received, item);
                }
                else
                {
                    mTempIndexList.Add(i);
                }
            }
        }
        for (int num2 = mTempIndexList.Count - 1; num2 >= 0; num2--)
        {
            mInfoList.RemoveAt(mTempIndexList[num2]);
        }
        if (mIsShowReceivedItem)
        {
            SortInfoList();
        }
        RefreshPanel();
    }

    private void SortInfoList()
    {
        mInfoList.Sort(SortListFunc);
    }

    private int SortListFunc((AchievementState state, AchievementInfo info) x, (AchievementState state, AchievementInfo info) y)
    {
        if (x.state != y.state)
        {
            return GetStateScore(y.state) - GetStateScore(x.state);
        }
        if (x.info.Group != y.info.Group)
        {
            return x.info.Group - y.info.Group;
        }
        return x.info.Index - y.info.Index;
    }

    private static int GetStateScore(AchievementState state)
    {
        return state switch
        {
            AchievementState.Completed => 3,
            AchievementState.Processing => 2,
            _ => 1,
        };
    }

    private void SetAchievementPanelTitle(AchievementInfo info)
    {
        if (!info.IsInvalid)
        {
            if ((Object)(object)mPanelTitle != (Object)null)
                mPanelTitle.SetKey("ui_achievement_title_" + info.AchievementType);
        }
    }

    public static (int startIndex, int endIndex) GetAchievementStateSearchRange(Player player, IReadOnlyList<AchievementInfo> achievementInfos, int showCount)
    {
        if (achievementInfos.Count <= 0)
        {
            return (startIndex: 0, endIndex: 0);
        }
        AchievementInfo achievementInfo = achievementInfos[0];
        int num = 0;
        int num2 = 0;
        if (player.AchievementManager.TryGetValue(achievementInfo.AchievementType, out var obj))
        {
            PlayerAchievementRecordDataWrap groupRecord = obj.GetGroupRecord(achievementInfo.Group);
            num = ((groupRecord.GotRewardMaxIndex > 0) ? groupRecord.GotRewardMaxIndex : num);
            num2 = ((groupRecord.CanRewardsMaxIndex > 0) ? groupRecord.CanRewardsMaxIndex : num);
        }
        num = ((num > 0) ? Math.Max(0, num - showCount) : 0);
        num2 = Math.Min(achievementInfos.Count - 1, num2 + showCount);
        return (startIndex: num, endIndex: num2);
    }
}

}
