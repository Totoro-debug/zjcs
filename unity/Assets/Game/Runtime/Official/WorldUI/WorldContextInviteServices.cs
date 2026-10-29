using System;
using System.Threading.Tasks;

namespace Zjcs.Official.WorldUI
{
    public readonly struct WorldContextInviteResult
    {
        public readonly int ErrorCode;
        public readonly object Data;
        public WorldContextInviteResult(int errorCode, object data)
        { ErrorCode = errorCode; Data = data; }
    }

    // Original service consumers; the host supplies the already confirmed
    // availability of each mode, without changing its invitation operation.
    public interface IWorldContextInviteServices
    {
        int? RoomPlayerCount { get; }
        IWorldTeamInviteServices Team { get; }
        Task<WorldContextInviteResult> OperPlayerRoomInvite(WorldTeamInviteActor actor, IWorldTeamInviteTarget target);
        Task<WorldContextInviteResult> OperPlayerTeamChallengeInviteJoin(long playerId);
        void AppendChallengeInvitation(object data);
        bool TryGetLiteInfoNameFromCache(WorldTeamInviteActor actor, out string name);
        Task<string> GetLiteInfoName(WorldTeamInviteActor actor);
        Task ShowCompanionInviteConfirmation(string playerName, Action confirm, Action cancel);
        Task<int> SendCompanionInvite(long targetId);
        void ShowError(int errorCode, object data, bool useDefaultTip);
    }
}
