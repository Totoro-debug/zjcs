using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // Each original minimap owns its callback-bound rows. Resource DTOs and
    // another open map must never acquire this instance's clock or player state.
    internal sealed class MainlineMiniMapLabels : IMap13LabelServices
    {
        private readonly IMap13LabelServices source;
        private readonly IMapRegionTime time;
        private readonly Dictionary<int, MiniMapRegionInfo> regions = new Dictionary<int, MiniMapRegionInfo>();

        public MainlineMiniMapLabels(MiniMapManagedContext context, IMapRegionTime time)
        {
            source = context.LabelServices ?? throw new ArgumentException("Original label inputs are required.");
            this.time = time;
            if (source.MapClassId != time.MapClassId || source.Data.mapClassId != time.MapClassId)
                throw new InvalidDataException("Original label data and region clock belong to different maps.");
            foreach (var entry in context.GetMapRegionDict(time.MapClassId))
            {
                var original = entry.Value;
                if (original.ClassId != time.MapClassId || entry.Key != original.Region)
                    throw new InvalidDataException("Original minimap region identity differs.");
                var row = JsonUtility.FromJson<MapRegionInfo>(JsonUtility.ToJson(original.LabelInfo));
                row.BindUnlockTime(() => time.IsUnlockTimeReached(row.Region), () => time.GetUnlockTime(row.Region));
                regions.Add(entry.Key, new MiniMapRegionInfo { LabelInfo = row, RegionTag = original.RegionTag,
                    Areas = original.Areas == null ? null : new List<int>(original.Areas) });
            }
        }

        internal IReadOnlyDictionary<int, MiniMapRegionInfo> GetRegions(int map)
            => map == MapClassId ? regions : throw new InvalidDataException("Region belongs to another minimap.");
        public int MapClassId => time.MapClassId;
        public object Map => source.Map;
        public event Action Initialized { add => source.Initialized += value; remove => source.Initialized -= value; }
        public IMap13LabelPlayer Player => source.Player;
        public bool IsTeamMember => source.IsTeamMember;
        public int QueryRegion(Vector3 position) => source.QueryRegion(position);
        public MapRegionInfo GetRegion(int mapClassId, int region) => GetRegions(mapClassId)[region].LabelInfo;
        public Map13LabelData Data => source.Data;
        public CombatRatingConfig Colors => source.Colors;
        public DateTime Now => time.Now;
        public double CalcRemainSecond(DateTime endTime) => (endTime - Now).TotalSeconds;
    }
}
