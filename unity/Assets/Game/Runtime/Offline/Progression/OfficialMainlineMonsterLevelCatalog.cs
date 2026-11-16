using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable] public sealed class MainlineMonsterLevel
    {
        public int MapClassId, PlacementId, EntityClassId, Region, SubRank, RegionLevel, RegionSubRank,
            PrivateLevelType, PublicLevelType;
        public float Level;
        public bool Absolute;

        // WorldEntityLevelComponent selects the placed region level or the player.
        public (int level, int subRank) Resolve(bool publicRegion, int playerLevel, int playerSubRank)
        {
            int type = publicRegion ? PublicLevelType : PrivateLevelType;
            switch (type)
            {
                case 0: return (0, 0);
                case 1: return (checked((int)(Absolute ? Level : RegionLevel + Level)),
                    SubRank == 0 ? RegionSubRank : SubRank);
                case 2: return (playerLevel, playerSubRank);
                default: throw new InvalidDataException("Unconverted monster level type: " + type);
            }
        }
    }

    [Serializable] public sealed class MainlineMonsterLevelData
    {
        public string manifest;
        public MainlineMonsterLevel[] monsters;
    }

    public sealed class OfficialMainlineMonsterLevelCatalog
    {
        public MainlineMonsterLevelData Data { get; }
        public OfficialMainlineMonsterLevelCatalog(string json)
        {
            Data = JsonUtility.FromJson<MainlineMonsterLevelData>(json);
            if (Data?.manifest != "884_156692" || Data.monsters == null
                || Data.monsters.Select(v => (v.MapClassId, v.PlacementId)).Distinct().Count() != Data.monsters.Length)
                throw new InvalidDataException("Original placed monster level bindings are invalid.");
        }

        public MainlineMonsterLevel Find(int mapClassId, int placementId) =>
            Data.monsters.SingleOrDefault(v => v.MapClassId == mapClassId && v.PlacementId == placementId);

        public static OfficialMainlineMonsterLevelCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("World/Mainline/monster-levels");
            return new OfficialMainlineMonsterLevelCatalog(asset?.text
                ?? throw new InvalidDataException("Original placed monster levels are missing."));
        }
    }
}
