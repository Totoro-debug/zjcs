using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    /// <summary>LostGoddessFurniturePropComponent.GetPropDict with the confirmed Map11 offline rewards.</summary>
    public sealed class OfflineGoddessPropSource : IOfflineCharacterPropSource
    {
        private readonly OfficialCharacterCatalog catalog;
        private readonly Func<OfflineWorldProgress> worldProgress;

        public OfflineGoddessPropSource(OfficialCharacterCatalog catalog, Func<OfflineWorldProgress> worldProgress = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.worldProgress = worldProgress;
        }

        public int HandleType => CharacterPropHandleType.LostGoddess;

        /// <summary>PlayerHangUpDataExt.GetBlessingLevel: only Goddess (type 2) contributes.</summary>
        public int BlessingLevel(OfflineWorldProgress world)
        {
            Dictionary<string, object> settings = catalog.Rows("game_settings").First();
            int count = (world?.ActivatedGoddessRegions ?? new List<OfflineMapRegionKey>())
                .Where(value => value != null).GroupBy(value => value.MapClassId + ":" + value.Region)
                .Select(group => FindRegion(group.First())).Count(region => region != null
                    && (!region.TryGetValue("GoddessActivePropAwardDict", out object value)
                        || value == null || value is Dictionary<string, object> awards && awards.Count == 0));
            return checked(Convert.ToInt32(settings["BaseBlessingLevel"], CultureInfo.InvariantCulture)
                + count * Convert.ToInt32(settings["GoddessToBlessingLevel"], CultureInfo.InvariantCulture));
        }

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player) =>
            CalculateProps(player, worldProgress?.Invoke(), 0);

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player,
            OfflineWorldProgress world, long nowUnixSeconds)
        {
            var result = new Dictionary<int, long>();
            if (player == null || world == null) return result;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineMapRegionKey active in world.ActivatedGoddessRegions)
            {
                if (active == null || !visited.Add(active.MapClassId + ":" + active.Region)) continue;
                Dictionary<string, object> region = FindRegion(active);
                if (region == null || !region.TryGetValue("GoddessActivePropAwardDict", out object value)
                    || !(value is Dictionary<string, object> awards) || awards.Count == 0) continue;
                // Ordinary goddesses have no furniture award dictionary. Map11's 49 lost goddesses
                // replace the entire original dictionary, so its other resource gains cannot stack.
                if (active.MapClassId == 11)
                {
                    Add(result, 1001, 100);
                    Add(result, 1005, 100);
                    continue;
                }
                foreach (KeyValuePair<string, object> award in awards)
                    Add(result, int.Parse(award.Key, CultureInfo.InvariantCulture),
                        Convert.ToInt64(award.Value, CultureInfo.InvariantCulture));
            }
            return result;
        }

        public long CalculateCombatRating(OfflinePlayerProgress player,
            IReadOnlyDictionary<int, long> props, CharacterPropScoreContext context) => context?.Common(props) ?? 0;

        private Dictionary<string, object> FindRegion(OfflineMapRegionKey active) =>
            catalog.Rows("map_region").FirstOrDefault(row =>
                Convert.ToInt32(row["ClassId"], CultureInfo.InvariantCulture) == active.MapClassId
                && Convert.ToInt32(row["Region"], CultureInfo.InvariantCulture) == active.Region);

        private static void Add(Dictionary<int, long> target, int key, long amount)
        {
            target.TryGetValue(key, out long previous);
            target[key] = checked(previous + amount);
        }
    }
}
