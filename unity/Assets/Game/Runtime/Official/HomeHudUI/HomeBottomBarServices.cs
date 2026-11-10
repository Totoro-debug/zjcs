using System;

namespace Zjcs.Official.HomeHudUI
{
    // Local implementations supply the original player queries and notifications.
    public interface IHomeBottomBarServices
    {
        event Action OnSubRankUpdate;
        event Action OnPlanesRankUnlock;
        event Action OnPlayerUpdate;
        bool HasPlayer { get; }
        Sex PlayerSex { get; }
        bool CheckSubRankUpgradeShow();
    }

    public enum Sex { Male, Female, None }
}
