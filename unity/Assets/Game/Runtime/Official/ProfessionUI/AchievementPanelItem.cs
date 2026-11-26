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
    // Original regular AchievementPanelItem, including UIBase initialization,
    // matching blackboard refresh and the original group/-1 claim request.
    public sealed class AchievementPanelItem : UIBase
    {
        public WorldMultiLanguageText TitleText, ProgressText;
        public Transform RewardSlot;
        public Button GetRewardBtn;
        public WorldStateSwitcher<OfficialAchievementState> StateSwitcher;
        [SerializeField] private Slider mProgressSlider;
        [SerializeField] private WorldStateSwitcher<bool> mTipDotStateSwitcher;
        private OfficialAchievementState state;
        private Func<AchievementPanelEntry> read;
        private IAchievementPanelSource data;
        private Action<int, int> mGetRewardCallBack;
        private Action mItemClick;
        private bool mReceiveSameType, mIsAchievementGroupItem;
        private readonly List<IProfessionRewardIcon> mItemIcons = new List<IProfessionRewardIcon>();
        private void Awake() => GetRewardBtn.onClick.AddListener(OnClickGetRewardBtn);
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            if (data is IAchievementPanelBlackboardSource events) events.BlackboardChanged += OnBlackboardChanged;
            Refresh();
        }
        protected override void OnDisableAndInited()
        {
            if (data is IAchievementPanelBlackboardSource events) events.BlackboardChanged -= OnBlackboardChanged;
            RecycleItemIcons();
            base.OnDisableAndInited();
        }
        public void Init(OfficialProfessionAchievementView value, IProfessionAchievementData context,
            Action<int, int> callback, bool receiveSameType = false, Action itemClick = null, bool isAchievementGroup = false)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var source = new ProfessionAchievementPanelSource(context);
            Init(value.State, () => source.Read(value.Info.Index), source, callback, receiveSameType, itemClick, isAchievementGroup);
        }
        internal void Init(OfficialAchievementState value, Func<AchievementPanelEntry> reader, IAchievementPanelSource source,
            Action<int, int> callback, bool receiveSameType, Action itemClick, bool isAchievementGroup)
            => InitWrapper(() => {
                state = value; read = reader; data = source; mGetRewardCallBack = callback;
                mReceiveSameType = receiveSameType; mItemClick = itemClick; mIsAchievementGroupItem = isAchievementGroup;
            });
        private void OnBlackboardChanged(AchievementPanelBlackboardKey key)
        {
            var info = read();
            if (info.ProgressKey.HasValue && info.ProgressKey.Value.Matches(key)) RefreshProgress(info);
        }
        public void Refresh()
        {
            mTipDotStateSwitcher.SetState(mIsAchievementGroupItem ? !data.AchievementGroupLimited : data.AchievementGroupLimited);
            var info = read();
            if (!info.IsInvalid && TitleText != null) TitleText.SetKey(info.Content.key, info.Content.args);
            RefreshProgress(info);
            RecycleItemIcons();
            foreach (KeyValuePair<int, long> reward in info.Reward)
            {
                IProfessionRewardIcon icon = data.GetIcon(reward.Key, RewardSlot);
                icon.SetCount(reward.Value);
                icon.SetClickPopupInfoDialog();
                mItemIcons.Add(icon);
            }
            StateSwitcher.SetState(state);
        }
        private void RefreshProgress(AchievementPanelEntry info)
        {
            if (!info.Progress.HasValue) return;
            var progress = info.Progress.Value;
            ProgressText.Format(data.Compact(progress.currentValue), data.Compact(progress.maxValue));
            if (mProgressSlider == null) return;
            mProgressSlider.minValue = 0; mProgressSlider.maxValue = progress.maxValue; mProgressSlider.value = progress.currentValue;
        }
        private void RecycleItemIcons()
        {
            foreach (IProfessionRewardIcon icon in mItemIcons) data.RecycleIcon(icon);
            mItemIcons.Clear();
        }
        private void OnClickGetRewardBtn()
        {
            var info = read();
            if (!info.CanGetReward) data.ShowSystemMessage("AchievementPanel.CanNotGetReward");
            else if (info.AlreadyGotReward) data.ShowSystemMessage("AchievementPanel.AlreadyGotReward");
            else if (mItemClick == null)
            {
                data.Claim(info.Group, -1, mReceiveSameType, () => mGetRewardCallBack?.Invoke(info.Group, -1));
            }
            else mItemClick();
        }
    }
}
