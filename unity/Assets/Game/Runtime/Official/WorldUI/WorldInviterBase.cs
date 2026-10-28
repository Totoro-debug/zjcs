using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.PlayerPlay;

namespace Zjcs.Official.WorldUI
{
    // InviterBase's consumed actor/target projection. Networking and MessagePack are
    // supplied by the local host; identity remains the original signed 64-bit value.
    public sealed class WorldTeamInviteActor
    {
        public long TargetId, OwnerPlayerId;
        public int ServerId = -1, NpcClassId;
        public bool IsNpc;
    }

    public interface IWorldTeamInviteTarget
    {
        int GetTeamMaxPlayer();
        bool TryGetLimitErrorTip(bool isSelf, int errorCode, out string tip);
    }

    public readonly struct WorldTeamInviteResult
    {
        public readonly int ErrorCode;
        public readonly bool Value;
        public WorldTeamInviteResult(int errorCode, bool value)
        { ErrorCode = errorCode; Value = value; }
    }

    public interface IWorldTeamInviteServices
    {
        int? TeamPlayerCount { get; }
        bool IsLeader { get; }
        bool AllowMemberInvite { get; }
        Task<WorldTeamInviteResult> OperPlayerTeamInvite(WorldTeamInviteActor actor,
            IWorldTeamInviteTarget target);
        string NpcName(int npcClassId);
        string Format(string key, params object[] args);
        void SystemMessage(string text);
        void ShowError(int errorCode, bool useDefaultTip);
    }

    public abstract class WorldInviterBase : MonoBehaviour
    {
        public abstract Task<int> Invite(WorldTeamInviteActor actor, IWorldTeamInviteTarget target);
        public abstract Task<bool> CheckCanInvite(PlayerPlayStateModule playerPlayStateModule);

        protected static bool CheckTeamFullByTarget(IWorldTeamInviteServices services,
            IWorldTeamInviteTarget target)
        {
            if (!services.TeamPlayerCount.HasValue) return false;
            return services.TeamPlayerCount.Value >= target.GetTeamMaxPlayer();
        }
    }
}
