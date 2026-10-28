using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.PlayerPlay;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.TeamUI
{
    public interface ITeamInviteFriendshipData
    {
        long FriendshipToTarget { get; }
        long FriendshipToPlayer { get; }
    }

    // Parser/manager calls consumed by the original invitation rows and list.
    public interface ITeamInviteUiServices : ITeamInviteComparerServices
    {
        bool MainPlayerAvailable { get; }
        bool ManagerAvailable { get; }
        bool RelationshipAvailable { get; }
        long MainPlayerId { get; }
        long CompanionId { get; }
        bool TeamChallengeHasSignin { get; }
        TeamTargetData NoneTarget { get; }
        PlayerPlayStateModule PlayerPlayStateModule { get; }
        ITeamIconServices Icons { get; }
        ITeamPlayerHpServices Hp { get; }
        ICharacterPortraitManager Portraits { get; }
        IWorldTeamInviteServices InviteServices { get; }
        event Action<long> RelationRemoved;
        event Action<TeamPanelPlayer> TeamPlayerAdded;
        event Action<TeamPanelPlayer> TeamPlayerRemoved;
        event Action<long, int, int> FriendshipUpdated;
        event Action<int> NpcFriendshipUpdated;
        bool TeamHasPlayer(long id);
        bool RoomHasPlayer(long id);
        bool TeamHasNpc(int npcClassId);
        bool MapHasNpcFollower(int npcClassId);
        bool IsJointStrikeInvitedAvailable(TeamInvitePlayerInfoData data);
        ITeamInviteFriendshipData GetFriendshipData(IProfessionInfoTarget target);
        ITeamInviteFriendshipData GetPlayerFriendshipData(long playerId);
        void SetCompanionTag(Component tag, long playerId);
        int RelationshipLevelByExp(int kind, long exp);
        long RelationshipExp(int kind, int level);
        long RelationshipLevelUpExp(int kind, int level);
        int NpcFriendshipMaxLevel(int npcClassId);
        long NpcFriendshipExp(int npcClassId, int level);
        int RequiredNpcAssistFriendshipLevel(int npcClassId);
        string PropSmallIcon(PropType propType);
        object PropValue(PropType propType, long value, bool includeUnit, bool includePropDesc, long difference);
        int? FestivalActivityId { get; }
        string Localize(string key);
        void SystemMessage(string message);
        Task WaitForNextFrame(CancellationToken token);
        void ConfigureInviteRow(TeamInvitePlayerItem row);
    }

    // Original R3 disposable-bag order, used by PropInfo's resource handle.
    internal sealed class TeamInviteResourceOperationBag : IDisposable
    {
        private System.Collections.Generic.List<IDisposable> values;
        private int count;
        private bool disposed;
        public T Add<T>(T value) where T : IDisposable
        {
            if (disposed) value.Dispose();
            else
            {
                if (values == null) values = new System.Collections.Generic.List<IDisposable>();
                values.Add(value); count++;
            }
            return value;
        }
        public void Dispose()
        {
            if (values != null)
            {
                for (int i = 0; i < count; i++) values[i]?.Dispose();
                values = null; count = 0;
            }
            disposed = true;
        }
    }
}
