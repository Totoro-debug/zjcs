// Selected original NPC profile controller; see partner-profile-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using TabGroup = Zjcs.Official.WorldUI.WorldTabGroup;
using TabItem = Zjcs.Official.WorldUI.WorldTabItem;
using Object = UnityEngine.Object;
using PlayerNpcDataWrap = Zjcs.Official.PartnersUI.IPartnerProfileNpcData;
using NpcProfileInfo = Zjcs.Official.Partners.PartnerProfileRules.ProfileInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NpcDetailPanelProfileContent : UIBase
{
    public enum ProfileStatus
    {
        Lock,
        Unlock,
        CanUnlock
    }

    public enum ContentImageStatus
    {
        Long,
        Short,
        Hide
    }


    [SerializeField]
    private Button mUnlockBtn;

    [SerializeField]
    private Transform mSlot;

    [SerializeField]
    private MultiLanguageText mContentText;

    [SerializeField]
    private MultiLanguageText mLockTipText;

    [SerializeField]
    private TabGroup mTabGroup;

    [SerializeField]
    private TabGroup mShortTabGroup;

    [SerializeField]
    private ScrollRect mTabScrollRect;

    [SerializeField]
    private List<NpcDetailPanelProfileContentTabItem> mTabItems;

    [SerializeField]
    private List<NpcDetailPanelProfileContentTabItem> mShortTabItems;

    [SerializeField]
    private WorldStateSwitcher<ProfileStatus> mStateSwitcher;

    [SerializeField]
    private WorldStateSwitcher<ContentImageStatus> mContentImageStateSwitcher;

    [SerializeField]
    private WorldStateSwitcher<bool> mIsLongProfileStates;

    private NpcProfileInfo mProfileInfo;

    private PlayerNpcDataWrap mNpcData;

    private List<NpcProfileInfo> mNpcProfileInfos;

    private ItemIcon mItemIcon;

    private IPartnerProfileUiServices services;
    public void Configure(IPartnerProfileUiServices value)
    {
        services = value ?? throw new ArgumentNullException(nameof(value));
        foreach (var item in mTabItems) item.Configure(value);
        foreach (var item in mShortTabItems) item.Configure(value);
    }

    private void Awake()
    {
        ((UnityEvent)mUnlockBtn.onClick).AddListener((UnityAction)OnUnlockBtn);
        ((UnityEvent<TabItem, bool>)mTabGroup.OnTabItemChanged).AddListener((UnityAction<TabItem, bool>)OnTabItemChanged);
        ((UnityEvent<TabItem, bool>)mShortTabGroup.OnTabItemChanged).AddListener((UnityAction<TabItem, bool>)OnTabItemChanged);
    }

    protected override void OnEnableAndInited()
    {
        base.OnEnableAndInited();
        InitContent();
    }

    protected override void OnDisableAndInited()
    {
        RecycleItemIcon();
        base.OnDisableAndInited();
    }

    public void Init(PlayerNpcDataWrap npcDataWrap)
    {
        InitWrapper(() =>
        {
            mNpcData = npcDataWrap;
            mNpcProfileInfos = services.GetNpcProfiles(npcDataWrap.NpcClassId);
        });
    }

    private void OnTabItemChanged(TabItem tabItem, bool value)
    {
        if (value)
        {
            NpcDetailPanelProfileContentTabItem component = ((Component)tabItem).GetComponent<NpcDetailPanelProfileContentTabItem>();
            mProfileInfo = component.Info;
            Refresh();
        }
    }

    private void InitContent()
    {
        bool flag = mNpcProfileInfos.Count <= 3;
        mIsLongProfileStates.SetState(!flag);
        List<NpcDetailPanelProfileContentTabItem> list = (flag ? mShortTabItems : mTabItems);
        NpcDetailPanelProfileContentTabItem npcDetailPanelProfileContentTabItem = null;
        for (int i = 0; i < list.Count; i++)
        {
            NpcDetailPanelProfileContentTabItem npcDetailPanelProfileContentTabItem2 = list[i];
            if (i < mNpcProfileInfos.Count)
            {
                NpcProfileInfo info = mNpcProfileInfos[i];
                bool flag2 = GetProfileStatus(info) == ProfileStatus.CanUnlock;
                npcDetailPanelProfileContentTabItem2.Init(info, flag2);
                ((Component)npcDetailPanelProfileContentTabItem2).gameObject.SetActive(true);
                if ((Object)(object)npcDetailPanelProfileContentTabItem == (Object)null || (!npcDetailPanelProfileContentTabItem.CanUnlock & flag2))
                {
                    npcDetailPanelProfileContentTabItem = npcDetailPanelProfileContentTabItem2;
                }
            }
            else
            {
                ((Component)npcDetailPanelProfileContentTabItem2).gameObject.SetActive(false);
            }
        }
        mProfileInfo = npcDetailPanelProfileContentTabItem.Info;
        npcDetailPanelProfileContentTabItem.TabItem.SetIsOn(value: true, sendCallback: false, instant: true);
        if (!flag)
        {
            RectTransform component = ((Component)npcDetailPanelProfileContentTabItem).GetComponent<RectTransform>();
            UniTaskExtensions.Forget(SafeScrollToItem(component));
        }
        Refresh();
    }

    private async UniTask SafeScrollToItem(RectTransform rectTransform)
    {
        await UniTask.DelayFrame(1, (PlayerLoopTiming)8, default(CancellationToken), false);
        ScrollRectUtil.ScrollToItem(mTabScrollRect, rectTransform);
    }

    private void Refresh()
    {
        ProfileStatus profileStatus = GetProfileStatus(mProfileInfo);
        mStateSwitcher.SetState(profileStatus);
        RecycleItemIcon();
        KeyValuePair<int, long> keyValuePair = mProfileInfo.Reward.First();
        mItemIcon = services.GetIcon(keyValuePair.Key, mSlot);
        mItemIcon.SetClickPopupInfoDialog();
        mItemIcon.SetCount(keyValuePair.Value);
        mItemIcon.SetAttachment(ItemIconAttachmentType.GotTip, profileStatus == ProfileStatus.Unlock);
        if (profileStatus == ProfileStatus.Unlock)
        {
            mContentText.SetKey($"npc_profile_Content_{mProfileInfo.Id}_{mProfileInfo.ProfileId}");
            mContentImageStateSwitcher.SetState(ContentImageStatus.Hide);
        }
        else
        {
            ContentImageStatus state = ((!mProfileInfo.ShowLongContentImage) ? ContentImageStatus.Short : ContentImageStatus.Long);
            mContentImageStateSwitcher.SetState(state);
        }
        if (profileStatus == ProfileStatus.Lock || profileStatus == ProfileStatus.CanUnlock)
        {
            mLockTipText.Format(services.GetUnlockDescription(mProfileInfo));
        }
    }

    private void RecycleItemIcon()
    {
        if (!((Object)(object)mItemIcon == (Object)null))
        {
            if (services != null)
            {
                services.RecycleIcon(mItemIcon);
            }
            mItemIcon = null;
        }
    }

    private ProfileStatus GetProfileStatus(NpcProfileInfo info)
    {
        if (mNpcData.ReceivedProfileRewards.Contains(info.ProfileId))
        {
            return ProfileStatus.Unlock;
        }
        if (services.IsConditionMet(info))
        {
            return ProfileStatus.CanUnlock;
        }
        return ProfileStatus.Lock;
    }

    private void OnUnlockBtn()
    {
        services.ReceiveReward(mProfileInfo.Id, mProfileInfo.ProfileId, () =>
        {
            Refresh();
        });
    }
}

}
