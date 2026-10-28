using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    public interface ITeamNpcIcon { void Init(int npcClassId, int level, int subRank); }

    // Original TeamMemberIcon, including its generic Player branch for NPC data.
    public sealed class TeamMemberIcon : UIBase
    {
        public enum MemberType { Empty, Player, Npc }
        [SerializeField] private TeamPlayerIcon mPlayerIcon;
        [SerializeField] private Component mNpcIconUI;
        [SerializeField] private GameObject mLeaderFlagGo, mOfflineGo;
        [SerializeField] private WorldStateSwitcher<MemberType> mMemberStateSwitcher;
        private IProfessionInfoTarget mTargetData;
        private ITeamIconServices services;
        private long PlayerId => mTargetData?.TargetId ?? 0;

        public void Configure(ITeamIconServices value)
        { services = value; if (mPlayerIcon != null) mPlayerIcon.Configure(value); }
        protected override void OnEnableAndInited()
        {
            base.OnEnableAndInited();
            services.TeamPlayerOffline += OnTeamPlayerOffline;
            services.TeamPlayerOnlineChanged += OnTeamPlayerOffline;
        }
        protected override void OnDisableAndInited()
        {
            if (services.ManagerAvailable)
            { services.TeamPlayerOffline -= OnTeamPlayerOffline; services.TeamPlayerOnlineChanged -= OnTeamPlayerOffline; }
            base.OnDisableAndInited();
        }
        private void OnTeamPlayerOffline(long playerId) { if (PlayerId == playerId) SetTeamPlayerOffline(); }
        private void SetTeamPlayerOffline()
        {
            if (mOfflineGo == null) return;
            if (mTargetData != null && mTargetData.TargetType == ActorTargetType.Player)
                mOfflineGo.SetActive(!(services.TeamPlayerOnline(PlayerId) ?? true));
            else mOfflineGo.SetActive(false);
        }
        public void Init(IProfessionInfoTarget data, bool isLeader)
        {
            InitWrapper(() =>
            {
                mTargetData = data;
                if (mOfflineGo != null) mOfflineGo.SetActive(false);
                mLeaderFlagGo.SetActive(isLeader);
                bool present = data != null;
                if (present) { mPlayerIcon.Init(data); SetTeamPlayerOffline(); }
                mMemberStateSwitcher.SetState(present ? MemberType.Player : MemberType.Empty);
            });
        }
        public void InitNpcIcon(int npcClassId, int level, int subRank)
        {
            ((ITeamNpcIcon)mNpcIconUI).Init(npcClassId, level, subRank);
            mLeaderFlagGo.SetActive(false); mMemberStateSwitcher.SetState(MemberType.Npc);
        }
    }
}
