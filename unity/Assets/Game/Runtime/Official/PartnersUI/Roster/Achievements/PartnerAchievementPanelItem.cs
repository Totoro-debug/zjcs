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
using UnityEngine.Events;
using UnityEngine.UI;

public class PartnerAchievementPanelItem : UIBase
{
    public MultiLanguageText TitleText;

    public MultiLanguageText ProgressText;

    public Transform RewardSlot;

    public Button GetRewardBtn;

    public WorldStateSwitcher<AchievementState> StateSwitcher;

    [SerializeField]
    private Slider mProgressSlider;

    [SerializeField]
    private WorldStateSwitcher<bool> mTipDotStateSwitcher;

    private (AchievementState state, AchievementInfo info) mAchievementInfoAndState;


    private List<ItemIcon> mItemIcons = new List<ItemIcon>();

    private Action<int, int> mGetRewardCallBack;

    private Action mItemClick;

    private bool mReceiveSameType;

    private bool mIsAchievementGroupItem;

    private IPartnerAchievementUiServices services;
    public void Configure(IPartnerAchievementUiServices value) => services = value ?? throw new ArgumentNullException(nameof(value));

    private void Awake()
    {
        ((UnityEvent)GetRewardBtn.onClick).AddListener((UnityAction)OnClickGetRewardBtn);
    }

    protected override void OnEnableAndInited()
    {
        base.OnEnableAndInited();
        Refresh();
    }

    protected override void OnDisableAndInited()
    {

        RecycleItemIcons();
        base.OnDisableAndInited();
    }

    public void Init((AchievementState state, AchievementInfo info) achievementInfoAndState, Action<int, int> getRewardCallBack, bool receiveSameType = false, Action itemClick = null, bool isAchievementGroup = false)
    {
        InitWrapper(() =>
        {
            mAchievementInfoAndState = achievementInfoAndState;
            mGetRewardCallBack = getRewardCallBack;
            mReceiveSameType = receiveSameType;
            mItemClick = itemClick;
            mIsAchievementGroupItem = isAchievementGroup;
        });
    }

    public void Refresh()
    {
        bool flag = services.AchievementGroupLimited;
        if (mIsAchievementGroupItem)
        {
            mTipDotStateSwitcher.SetState(!flag);
        }
        else
        {
            mTipDotStateSwitcher.SetState(flag);
        }
        SetAchievementItemTitle(mAchievementInfoAndState.info);
        RefreshProgress();
        RecycleItemIcons();
        foreach (KeyValuePair<int, long> item in mAchievementInfoAndState.info.Reward)
        {
            ItemIcon icon = services.GetIcon(item.Key, RewardSlot);
            icon.SetCount(item.Value);
            icon.SetClickPopupInfoDialog();
            mItemIcons.Add(icon);
        }
        StateSwitcher.SetState(mAchievementInfoAndState.state);
    }

    private void RecycleItemIcons()
    {
        foreach (ItemIcon mItemIcon in mItemIcons)
        {
            services.RecycleIcon(mItemIcon);
        }
        mItemIcons.Clear();
    }

    private void OnClickGetRewardBtn()
    {
        AchievementInfo info = mAchievementInfoAndState.info;
        if (!services.Player.IsCanGetAchievementReward(info))
        {
            services.ShowSystemMessage("AchievementPanel.CanNotGetReward");
        }
        else if (services.Player.IsAlreadyGotAchievementReward(info))
        {
            services.ShowSystemMessage("AchievementPanel.AlreadyGotReward");
        }
        else if (mItemClick == null)
        {
            services.Claim(info.Group, -1, mReceiveSameType, () =>
            {
                mGetRewardCallBack?.Invoke(info.Group, -1);
            });
        }
        else
        {
            mItemClick?.Invoke();
        }
    }

    private void RefreshProgress()
    {
        var progress = services.GetProgress(mAchievementInfoAndState.info);
        SetProgressValue(progress.maxValue, progress.currentValue);
    }

    private void SetProgressValue(long maxValue, long currentValue)
    {
        object obj = services.Compact(maxValue);
        object obj2 = services.Compact(currentValue);
        ProgressText.Format(obj2, obj);
        if ((Object)(object)mProgressSlider != (Object)null)
        {
            mProgressSlider.minValue = 0f;
            mProgressSlider.maxValue = maxValue;
            mProgressSlider.value = currentValue;
        }
    }

    private void SetAchievementItemTitle(AchievementInfo info)
    {
        if (!info.IsInvalid && !((Object)(object)TitleText == (Object)null))
        {
            if (info.AchievementType == AchievementType.NpcFriendshipLevel)
                TitleText.SetKey("ui_achievement_content_NpcFriendshipLevel", services.GetNpcName(info.AchievementData.NpcClassId));
            else TitleText.SetKey("ui_achievement_content_" + info.AchievementType);
        }
    }

    public void OnGameObjectPoolReset()
    {
    }

    public void OnGameObjectPoolClear()
    {
    }
}

}
