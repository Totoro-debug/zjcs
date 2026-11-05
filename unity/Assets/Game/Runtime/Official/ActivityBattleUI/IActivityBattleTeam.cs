using System;

namespace Zjcs.Official.ActivityBattleUI
{
    /// <summary>Local team state and the existing original invitation window.</summary>
    public interface IActivityBattleTeam
    {
        event Action Changed;
        // Counts include the local player, matching the original team display.
        int MemberCount { get; }
        int MaximumMemberCount { get; }
        void OpenInvitation();
    }
}
