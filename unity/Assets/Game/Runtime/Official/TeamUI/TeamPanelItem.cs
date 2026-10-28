using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    // Original TeamPanelItem, including icon initialization before InitWrapper.
    public sealed class TeamPanelItem : UIBase, IGameObjectPoolItem
    {
        private enum Status { Empty, Leader, Member }
        [SerializeField] private TeamMemberIcon mTeamMemberIcon;
        [SerializeField] private Button mAddMemberBtn;
        [SerializeField] private Transform mSkillsSlot;
        [SerializeField] private TeamPanelItemButton mPanelItemButton;
        [SerializeField] private WorldStateSwitcher<Status> mStateSwitcher;
        private bool mIsLeader;
        private Action mAddMemberDelegate;
        private TeamPanelBriefInfo mBriefInfo;
        private ITeamPanelServices services;

        public void Configure(ITeamPanelServices value)
        {
            services = value;
            mTeamMemberIcon.Configure(value.Icons);
            mPanelItemButton.Configure(value);
            foreach (var hp in GetComponentsInChildren<TeamPlayerUIBase>(true)) hp.Configure(value.Hp);
            foreach (var head in GetComponentsInChildren<TeamHeadCharacterPortrait>(true)) head.Bind(value.Portraits);
        }
        private void Awake()
        { if (mAddMemberBtn != null) mAddMemberBtn.onClick.AddListener(OnAddMemberBtnClick); }
        public void Init(TeamPanelBriefInfo info, bool isLeader, Action addMemberDelegate)
        {
            mTeamMemberIcon.Init(info?.TargetData, isLeader);
            InitWrapper(() =>
            {
                mBriefInfo = info;
                mIsLeader = isLeader;
                mAddMemberDelegate = addMemberDelegate;
                if (mAddMemberBtn != null)
                    mAddMemberBtn.gameObject.SetActive(mBriefInfo == null && !services.IsOnlyOnPrivateMap);
                RefreshSkills();
                mStateSwitcher.SetState(GetCurrentStatus());
                mPanelItemButton.Init(mBriefInfo?.TargetData);
            });
        }
        private Status GetCurrentStatus()
        {
            if (mBriefInfo == null) return Status.Empty;
            return mIsLeader ? Status.Leader : Status.Member;
        }
        private void RefreshSkills()
        {
            if (mBriefInfo != null && mSkillsSlot != null)
                DoRefreshSkills(mBriefInfo.ActiveSkills, mBriefInfo);
        }
        private void DoRefreshSkills(IDictionary<int, object> skillDict, TeamPanelBriefInfo info)
        {
            services.RecycleSkillIcons(this);
            for (int index = mSkillsSlot.childCount - 1; index >= 0; index--)
                Destroy(mSkillsSlot.GetChild(index).gameObject);
            for (int i = 0; i < services.ActiveSkillSlotsCount; i++)
            {
                if (!skillDict.TryGetValue(i, out object item)) continue;
                ITeamPanelSkillIcon icon = services.GetSkillIcon(item, mSkillsSlot, this);
                int level = info.ActiveSkillSlotLevel(i);
                if (icon.IsSkill)
                {
                    icon.SetClickPopupInfoDialog(go =>
                        icon.SetSampleInfoPropDataProvider(go, info.Id, info.Level, info.SubRank));
                    icon.ShowSkillLevel(true, level, level > services.MaxSkillSlotBaseLevel(info.SubRank));
                }
                if (icon.IsEmpty)
                    icon.ShowEmptyLevel(true, level, level > services.MaxSkillSlotBaseLevel(info.SubRank));
            }
        }
        private void OnAddMemberBtnClick()
        {
            if (!services.CheckAllowInvite()) services.ShowError(6168, false);
            else mAddMemberDelegate?.Invoke();
        }
        public void OnGameObjectPoolReset() { }
        public void OnGameObjectPoolClear() { }
    }
}
