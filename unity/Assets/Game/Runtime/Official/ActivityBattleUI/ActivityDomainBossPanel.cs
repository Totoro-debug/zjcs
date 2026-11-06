using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ActivityRankUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Selected original ActivityDomainBossPanel. Panel registration and data
    // subscriptions belong to the lifetime of its local host.
    public sealed class ActivityDomainBossPanel : WorldInitializedUI
    {
        private enum TabType { Game, Rank }
        [Serializable] private sealed class TabData
        { public TabType TabType; public WorldTabItem TabItem; }
        [SerializeField] private ActivityDomainBossPanelDetailContent mBossContent;
        [SerializeField] private ActivityDomainBossRankPanel mRankPanel;
        [SerializeField] private WorldTabGroup mTabGroup;
        [SerializeField] private List<TabData> mTabDatas;
        [SerializeField] private WorldStateSwitcher<TabType> mContentState;
        [SerializeField] private TipDotNodeLinker mTipDotNodeLinker;
        [SerializeField] private string mRuleKey;
        private IActivityDomainBossPageBindings bindings;
        private int mActivityId;

        public void Configure(IActivityDomainBossPageBindings value, ActivityRankBindings rank)
        {
            bindings = value ?? throw new ArgumentNullException(nameof(value));
            mBossContent.Configure(value);
            mRankPanel.Configure(rank);
        }
        public void Init(int activityId) => InitWrapper(() => mActivityId = activityId);
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            bindings.BindTipDot(mTipDotNodeLinker, mActivityId);
            mTabGroup.OnTabItemChanged.AddListener(OnTabItemChange);
            mTabGroup.ResetTabItem(true);
            mBossContent.Init(mActivityId);
            mRankPanel.Init(mActivityId, mRuleKey);
        }
        protected override void OnDisableAndInited()
        {
            mTabGroup.OnTabItemChanged.RemoveListener(OnTabItemChange);
            base.OnDisableAndInited();
        }
        private void OnTabItemChange(WorldTabItem item, bool isOn)
        {
            if (!isOn) return;
            var data = mTabDatas.Find(row => row.TabItem == item)
                ?? throw new InvalidOperationException("The original activity tab mapping is incomplete.");
            mContentState.SetState(data.TabType);
        }
    }
}
