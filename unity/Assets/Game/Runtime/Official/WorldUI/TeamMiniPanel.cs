using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    /// <summary>TeamMiniPanel.RefreshMiniPanel / RefreshUI with local NPC team context.</summary>
    public sealed class TeamMiniPanel : MonoBehaviour
    {
        public GoddessTouchDialog.ObjectState[] mHasTeamStateSwitcher;
        public void Refresh(bool allowPrivateTeam, bool allowPublicTeam, bool hasTeam) =>
            GoddessTouchDialog.SetState(mHasTeamStateSwitcher,
                !allowPrivateTeam && !allowPublicTeam ? 0 : hasTeam ? 2 : 1);
    }
}
