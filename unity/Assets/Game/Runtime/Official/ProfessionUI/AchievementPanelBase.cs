using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Official.ProfessionUI
{
    // Original regular AchievementPanelBase filtering, search, sort and claim
    // callback order. Profession and gacha share this serialized controller.
    public sealed class AchievementPanelBase : MonoBehaviour
    {
        public WorldStateSwitcher<bool> EmptyStateSwitcher;
        [SerializeField] private bool mIsShowReceivedItem;
        [SerializeField] private bool mIsShowProcessingItem = true;
        [SerializeField] private PanelTitle mPanelTitle;
        [SerializeField] private List<AchievementPanelItem> mPanelItemList;
        private readonly List<(OfficialAchievementState state, AchievementPanelEntry info, Func<AchievementPanelEntry> read)> mInfoList
            = new List<(OfficialAchievementState, AchievementPanelEntry, Func<AchievementPanelEntry>)>();
        private readonly List<int> mTempIndexList = new List<int>();
        private IAchievementPanelSource data;
        private IAchievementPanelRefreshSource refreshSource;
        private AchievementPanelKey mDataKey;
        private bool mReceiveSameType, mIsAchievementGroup;
        private Action mItemClick;
        public void InitDataKey(IProfessionAchievementData context)
            => InitDataKey(new AchievementPanelKey(56, 10), new ProfessionAchievementPanelSource(context));
        public void InitDataKey(AchievementPanelKey key, IAchievementPanelSource source,
            bool receiveSameType = false, Action itemClick = null, bool isAchievementGroup = false)
        {
            ReleaseRefresh();
            data = source ?? throw new ArgumentNullException(nameof(source));
            mDataKey = key ?? throw new ArgumentNullException(nameof(key));
            mReceiveSameType = receiveSameType; mItemClick = itemClick; mIsAchievementGroup = isAchievementGroup;
            if (isActiveAndEnabled) Activate();
        }
        private void OnEnable() { if (data != null) Activate(); }
        private void Activate()
        {
            if (refreshSource == null && data is IAchievementPanelRefreshSource refreshing)
            { refreshSource = refreshing; refreshSource.Changed += InitData; }
            InitData();
        }
        private void OnDisable() => ReleaseRefresh();
        private void ReleaseRefresh()
        {
            if (refreshSource != null) refreshSource.Changed -= InitData;
            refreshSource = null;
        }
        private void InitData()
        {
            var groups = data.GetGroups(mDataKey, mReceiveSameType);
            if (groups.Count == 0) return;
            bool titleSet = false;
            mInfoList.Clear();
            foreach (var group in groups)
            {
                if (group.Count == 0) continue;
                if (!titleSet)
                {
                    var first = group[0];
                    if (!first.IsInvalid && mPanelTitle != null) mPanelTitle.SetKey(first.Title.key, first.Title.args);
                    titleSet = true;
                }
                var range = GetAchievementStateSearchRange(group, data.AchievementPanelShowCount);
                for (int index = range.startIndex; index <= range.endIndex; index++)
                {
                    int selected = index;
                    var info = group[index];
                    Func<AchievementPanelEntry> read = () => group[selected];
                    if (info.AlreadyGotReward)
                    {
                        if (mIsShowReceivedItem) mInfoList.Add((OfficialAchievementState.Received, info, read));
                    }
                    else if (info.CanGetReward) mInfoList.Add((OfficialAchievementState.Completed, info, read));
                    else if (mIsShowProcessingItem) mInfoList.Add((OfficialAchievementState.Processing, info, read));
                }
            }
            SortInfoList();
            RefreshPanel();
        }
        public static (int startIndex, int endIndex) GetAchievementStateSearchRange(AchievementPanelGroup group, int showCount)
        {
            if (group.Count <= 0) return (0, 0);
            int got = group.GotRewardMaxIndex > 0 ? group.GotRewardMaxIndex : 0;
            int can = group.CanRewardsMaxIndex > 0 ? group.CanRewardsMaxIndex : 0;
            return (got > 0 ? Math.Max(0, got - showCount) : 0, Math.Min(group.Count - 1, can + showCount));
        }
        private void RefreshPanel()
        {
            EmptyStateSwitcher.SetState(mInfoList.Count == 0);
            int count = Mathf.Clamp(mInfoList.Count, 0, data.AchievementPanelShowCount);
            for (int index = 0; index < mPanelItemList.Count; index++)
            {
                var item = mPanelItemList[index];
                item.gameObject.SetActive(index < count);
                if (index < count)
                {
                    var row = mInfoList[index];
                    item.Init(row.state, row.read, data, GetRewardCallBack, mReceiveSameType, mItemClick, mIsAchievementGroup);
                }
            }
        }
        private void GetRewardCallBack(int groupId, int count)
        {
            // Existing profession callers publish their full local transaction
            // before this callback; rebuild from that committed state as before.
            if (data is IAchievementPanelRefreshSource) { InitData(); return; }
            mTempIndexList.Clear();
            int remaining = count;
            for (int index = 0; index < mInfoList.Count; index++)
            {
                if (count > 0 && remaining-- < 0) break;
                var row = mInfoList[index];
                if (row.state != OfficialAchievementState.Completed) continue;
                if (mIsShowReceivedItem) mInfoList[index] = (OfficialAchievementState.Received, row.info, row.read);
                else mTempIndexList.Add(index);
            }
            for (int index = mTempIndexList.Count - 1; index >= 0; index--) mInfoList.RemoveAt(mTempIndexList[index]);
            if (mIsShowReceivedItem) SortInfoList();
            RefreshPanel();
        }
        private static int StateScore(OfficialAchievementState state) => state == OfficialAchievementState.Completed ? 3
            : state == OfficialAchievementState.Processing ? 2 : 1;
        private void SortInfoList() => mInfoList.Sort((left, right) => left.state != right.state
            ? StateScore(right.state) - StateScore(left.state) : left.info.Group != right.info.Group
                ? left.info.Group - right.info.Group : left.info.Index - right.info.Index);
    }
}
