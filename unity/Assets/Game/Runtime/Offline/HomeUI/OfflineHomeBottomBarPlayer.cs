using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.HomeHudUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeUI
{
    // Selected PlayerBlessLevelUtils.CheckSubRankUpgradeShow and the original
    // system-level parser caches, reading the current local player on every query.
    public sealed class OfflineHomeBottomBarPlayer : IHomeBottomBarServices
    {
        private readonly OfflineMainlineProgressSession profile;
        private readonly Dictionary<int, int> baseLevelLimits;
        private readonly HashSet<int> subRanks;

        public OfflineHomeBottomBarPlayer(OfflineMainlineProgressSession profile,
            OfficialCurrentHomeRuleAssetCatalog configuration = null)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            configuration = configuration ?? OfficialCurrentHomeRuleAssetCatalog.LoadDefault();
            baseLevelLimits = configuration.Rows("system_level_limit")
                .Where(row => Number(row, "PlayerBlessLevel") == 0)
                .GroupBy(row => Number(row, "SubRank"))
                .ToDictionary(group => group.Key, group => group.Max(row => Number(row, "PlayerLevel")));
            subRanks = new HashSet<int>(configuration.Rows("player_subrank")
                .Select(row => Number(row, "SubRank")));
        }

        public bool HasPlayer => profile.Save.Player != null && profile.Save.Player.CharacterCreated;
        public Sex PlayerSex => HasPlayer ? (Sex)profile.Save.Player.Wardrobe.Sex : Sex.None;
        public event Action OnSubRankUpdate;
        public event Action OnPlanesRankUnlock;
        public event Action OnPlayerUpdate;

        public bool CheckSubRankUpgradeShow()
        {
            if (!HasPlayer) return false;
            var player = profile.Save.Player;
            int playerBaseLevelLimit = GetPlayerBaseLevelLimit(player.SubRank);
            int playerBaseLevelLimit2 = GetPlayerBaseLevelLimit(player.SubRank + 1);
            bool flag = false;
            if (subRanks.Contains(player.SubRank + 1))
            {
                // User-confirmed offline policy removes PlanesInfo.PlanesRank;
                // the next configured rank and both personal level checks remain.
                flag = true;
            }
            if (flag && player.Level >= playerBaseLevelLimit && playerBaseLevelLimit2 > playerBaseLevelLimit)
            {
                return true;
            }
            return false;
        }

        private int GetPlayerBaseLevelLimit(int subRank)
            => baseLevelLimits.TryGetValue(subRank, out int value) ? value : 0;

        // The owning UI scope forwards the corresponding committed player event.
        // Reading the marker never mutates the profile or synthesizes a rank-up.
        public void NotifySubRankUpdate() => OnSubRankUpdate?.Invoke();
        public void NotifyPlayerUpdate() => OnPlayerUpdate?.Invoke();

        private static int Number(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key]);
    }
}
