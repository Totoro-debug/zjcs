using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionRewardIcon
    {
        void SetCount(long count);
        void SetClickPopupInfoDialog();
    }
    public interface IProfessionAchievementData
    {
        OfficialProfessionAchievementRules Rules { get; }
        int TotalLevel { get; }
        ISet<string> Recorded { get; }
        int AchievementPanelShowCount { get; }
        bool AchievementGroupLimited { get; }
        bool CanGetReward(OfficialProfessionAchievement info);
        bool AlreadyGotReward(OfficialProfessionAchievement info);
        bool ClaimGroup(int group, int count, bool receiveSameType);
        void ShowSystemMessage(string key);
        IProfessionRewardIcon GetIcon(int itemId, Transform slot);
        void RecycleIcon(IProfessionRewardIcon icon);
        event Action Changed;
    }
    // AchievementPanelItem's ProfessionTotalLevel path; the original icon manager,
    // function limit, player state and RPC are supplied by local services.
    public sealed class AchievementPanelItem : MonoBehaviour
    {
        public WorldMultiLanguageText TitleText, ProgressText;
        public Transform RewardSlot;
        public Button GetRewardBtn;
        public WorldStateSwitcher<OfficialAchievementState> StateSwitcher;
        [SerializeField] private Slider mProgressSlider;
        [SerializeField] private WorldStateSwitcher<bool> mTipDotStateSwitcher;
        private OfficialProfessionAchievementView row;
        private IProfessionAchievementData data;
        private Action<int, int> mGetRewardCallBack;
        private Action mItemClick;
        private bool mReceiveSameType, mIsAchievementGroupItem;
        private readonly List<IProfessionRewardIcon> mItemIcons = new List<IProfessionRewardIcon>();
        private void Awake() => GetRewardBtn.onClick.AddListener(OnClickGetRewardBtn);
        private void OnEnable() { if (row != null && data != null) Refresh(); }
        private void OnDisable() => RecycleItemIcons();
        public void Init(OfficialProfessionAchievementView value, IProfessionAchievementData context,
            Action<int, int> callback, bool receiveSameType = false, Action itemClick = null, bool isAchievementGroup = false)
        {
            RecycleItemIcons();
            row = value ?? throw new ArgumentNullException(nameof(value));
            data = context ?? throw new ArgumentNullException(nameof(context));
            mGetRewardCallBack = callback;
            mReceiveSameType = receiveSameType;
            mItemClick = itemClick;
            mIsAchievementGroupItem = isAchievementGroup;
            if (isActiveAndEnabled) Refresh();
        }
        public void Refresh()
        {
            mTipDotStateSwitcher.SetState(mIsAchievementGroupItem ? !data.AchievementGroupLimited : data.AchievementGroupLimited);
            if (TitleText != null) TitleText.SetKey("ui_achievement_content_TotalProfessionLevel");
            int max = row.Info.RequiredLevel, current = data.TotalLevel;
            ProgressText.Format(OfficialPropFormat.Compact(current), OfficialPropFormat.Compact(max));
            if (mProgressSlider != null)
            {
                mProgressSlider.minValue = 0;
                mProgressSlider.maxValue = max;
                mProgressSlider.value = current;
            }
            RecycleItemIcons();
            foreach (KeyValuePair<int, long> reward in row.Info.Reward)
            {
                IProfessionRewardIcon icon = data.GetIcon(reward.Key, RewardSlot);
                icon.SetCount(reward.Value);
                icon.SetClickPopupInfoDialog();
                mItemIcons.Add(icon);
            }
            StateSwitcher.SetState(row.State);
        }
        private void RecycleItemIcons()
        {
            foreach (IProfessionRewardIcon icon in mItemIcons) data.RecycleIcon(icon);
            mItemIcons.Clear();
        }
        private void OnClickGetRewardBtn()
        {
            if (!data.CanGetReward(row.Info)) data.ShowSystemMessage("AchievementPanel.CanNotGetReward");
            else if (data.AlreadyGotReward(row.Info)) data.ShowSystemMessage("AchievementPanel.AlreadyGotReward");
            else if (mItemClick == null)
            {
                if (data.ClaimGroup(10, -1, mReceiveSameType)) mGetRewardCallBack?.Invoke(10, -1);
            }
            else mItemClick();
        }
    }
}
