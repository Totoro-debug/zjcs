using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    // Supplement to the character catalog: the map's verified LevelProp 410
    // rows are used by every character property consumer, including outside maps.
    internal static class OfficialHeroStatueProperties
    {
        [Serializable] private sealed class Data { public string manifest; public Row[] heroLevelProps; }
        [Serializable] private sealed class Row { public int Id, Level; public int[] Types; public long[] Values; }
        private static Dictionary<int, Dictionary<int, long>> rows;
        public static IReadOnlyDictionary<int, long> Get(int id, int level)
        {
            if (id != 410 || level < 1) return new Dictionary<int, long>();
            if (rows == null)
            {
                var asset = Resources.Load<TextAsset>("World/Mainline/interactions");
                var data = asset == null ? null : JsonUtility.FromJson<Data>(asset.text);
                if (data?.manifest != "854_156383" || data.heroLevelProps?.Length != 500)
                    throw new InvalidDataException("Official hero statue LevelProp rows are unbound.");
                rows = data.heroLevelProps.ToDictionary(value => value.Level,
                    value => value.Types.Select((type, index) => new { type, count = value.Values[index] })
                        .ToDictionary(prop => prop.type, prop => prop.count));
            }
            return rows.TryGetValue(Math.Min(level, 500), out var props) ? props : new Dictionary<int, long>();
        }
    }
}
