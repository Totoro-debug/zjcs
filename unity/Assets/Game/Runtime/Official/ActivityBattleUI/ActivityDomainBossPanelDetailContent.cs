using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Selected original detail controller. The formal local activity snapshot
    // replaces the three online data managers; selector/red-dot order is kept.
    public sealed class ActivityDomainBossPanelDetailContent : WorldInitializedUI
    {
        [SerializeField] private ActivityDomainBossPanelBattleContent mBattleContent;
        [SerializeField] private ActivityDomainBossPreviewContent mPreviewContent;
        [SerializeField] private ActivityInfoUI mActivityInfoUI;
        [SerializeField] private ActivityFestivalBossNumScrollView mNumScrollView;
        [SerializeField] private Button mLeftBtn, mRightBtn;
        [SerializeField] private WorldStateSwitcher<bool> mLeftStateSwitcher, mRightStateSwitcher;
        [SerializeField] private WorldStateSwitcher<bool> mUnlockStateSwitcher, mPreviewStateSwitcher;
        [SerializeField] private GameObject mLeftRedDot, mRightRedDot;
        private IActivityDomainBossPageBindings bindings;
        private ActivityDomainBossPageState state;
        private int mActivityId, mShowIndex;
        private readonly List<int> mInfoList = new List<int>();
        private readonly List<int> mDifficultyIndexList = new List<int>();

        public void Configure(IActivityDomainBossPageBindings value)
        {
            bindings = value ?? throw new ArgumentNullException(nameof(value));
            mBattleContent.Configure(value);
        }
        private void Awake()
        {
            mNumScrollView.SetSelectCallback(OnSelectionChanged);
            mNumScrollView.SetIsUnlockedDelegate(IsDifficultyUnlocked);
            mNumScrollView.SetShowRedDotRewardDelegate(ShowDifficultyRedDot);
            mLeftBtn.onClick.AddListener(mNumScrollView.SelectPrevCell);
            mRightBtn.onClick.AddListener(mNumScrollView.SelectNextCell);
        }
        public void Init(int id) => InitWrapper(() => mActivityId = id);
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            bindings.Changed += OnActivityDataUpdate;
            state = bindings.Read(mActivityId, 0);
            // The approved local period opens on first entry. No server summon
            // preview is fabricated; its original controller is retained.
            mPreviewStateSwitcher.SetState(false);
            mPreviewContent.gameObject.SetActive(false);
            mActivityInfoUI.Init(state.Activity);
            mInfoList.Clear(); mInfoList.AddRange(state.Difficulties); mInfoList.Sort();
            mDifficultyIndexList.Clear();
            for (int i = 0; i < mInfoList.Count; i++) mDifficultyIndexList.Add(i);
            InitData();
            RefreshContent();
        }
        protected override void OnDisableAndInited()
        {
            bindings.Changed -= OnActivityDataUpdate;
            base.OnDisableAndInited();
        }
        private void OnActivityDataUpdate(int activityId)
        {
            if (activityId != mActivityId) return;
            state = bindings.Read(mActivityId, mInfoList[mShowIndex]);
            RefreshContent();
        }
        private void InitData()
        {
            if (state.CanChallenge)
                mShowIndex = state.DifficultyLimited ? state.Difficulty - 1 : state.Difficulty;
            else
                for (int i = 0; i <= state.Difficulty; i++)
                    if (!state.AwardedDifficulties.Contains(i)) { mShowIndex = i; break; }
            mShowIndex = Math.Max(mShowIndex, 0);
            mShowIndex = Math.Min(mShowIndex, mDifficultyIndexList.Count - 1);
            mNumScrollView.UpdateData(mDifficultyIndexList, mShowIndex);
        }
        private void RefreshContent()
        {
            if (state == null || mInfoList.Count == 0) return;
            if (mShowIndex - 1 >= 0) mLeftRedDot.SetActive(NeedShowLeftRedDot(mShowIndex - 1));
            if (mShowIndex + 1 < mInfoList.Count) mRightRedDot.SetActive(NeedShowRightRedDot(mShowIndex + 1));
            mLeftStateSwitcher.SetState(mShowIndex - 1 >= 0);
            mRightStateSwitcher.SetState(mShowIndex + 1 < mInfoList.Count);
            mUnlockStateSwitcher.SetState(mInfoList[mShowIndex] <= state.Difficulty);
            mBattleContent.Init(mActivityId, mInfoList[mShowIndex]);
        }
        private void OnSelectionChanged(int index)
        {
            if (index < 0 || index >= mInfoList.Count) return;
            mShowIndex = index;
            state = bindings.Read(mActivityId, mInfoList[index]);
            RefreshContent();
        }
        private bool IsDifficultyUnlocked(int index) => mInfoList[index] <= state.Difficulty;
        private bool ShowDifficultyRedDot(int index)
        {
            int difficulty = mInfoList[index];
            if (state.Difficulty > difficulty && !state.AwardedDifficulties.Contains(difficulty)) return true;
            if (state.Difficulty == difficulty) return !state.DifficultyLimited & state.CanChallenge;
            if (state.Difficulty - 1 == difficulty) return state.DifficultyLimited & state.CanChallenge;
            return false;
        }
        private bool NeedShowLeftRedDot(int index)
        { for (int i = 0; i <= index; i++) if (ShowDifficultyRedDot(i)) return true; return false; }
        private bool NeedShowRightRedDot(int index)
        { for (int i = index; i < mInfoList.Count; i++) if (ShowDifficultyRedDot(i)) return true; return false; }
    }
}
