using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.TeamUI
{
    // Consumed manager boundaries from PlayerIcon/PlayerIconOther/TeamMemberIcon.
    // The local host supplies the same data; these interfaces contain no UI layout.
    public interface ITeamIconStatus
    {
        bool InitFinished { get; }
        bool IsVisible { get; }
        void Init(IProfessionInfoTarget target);
        void UpdateFriendshipStatus();
        void SetFriendInfo(bool show);
    }

    public interface ITeamIconServices
    {
        IProfessionPlayerInfoBindings PlayerInfo { get; }
        long MainPlayerId { get; }
        bool ManagerAvailable { get; }
        bool MainPlayerAvailable { get; }
        bool RelationshipAvailable { get; }
        bool IsFriend(long playerId);
        bool? TeamPlayerOnline(long playerId);
        event Action<long> TeamPlayerOffline;
        event Action<long> TeamPlayerOnlineChanged;
        event Action<long> RelationAdded;
        event Action<long> RelationRemoved;
        Task<object> GetLiteInfo(IProfessionInfoTarget target, CancellationToken cancellation, bool latest = false);
        long LiteInfoId(object info);
        Task WaitForSeconds(float seconds, CancellationToken cancellation);
        Task<object> GetPlayerBriefInfo(long playerId);
        void PopupNpc(Component owner, IProfessionInfoNpcTarget target);
        void PopupPlayer(Component owner, object briefInfo, int popupScene, int skillGroup);
    }
}
