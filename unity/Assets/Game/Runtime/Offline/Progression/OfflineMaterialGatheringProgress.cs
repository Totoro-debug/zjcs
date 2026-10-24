using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineMaterialGatheringProgress
    {
        public int Version = 1;
        public long RandomState;
        public long TotalAttackCount;
        public long TotalBreakCount;
        public bool QuickEnabled;
        public List<OfflineMaterialGatheringDungeonState> Dungeons = new List<OfflineMaterialGatheringDungeonState>();

        public static void Normalize(OfflineMaterialGatheringProgress progress)
        {
            if (progress == null) return;
            if (progress.Dungeons == null) progress.Dungeons = new List<OfflineMaterialGatheringDungeonState>();
        }

        public static bool IsValid(OfflineMaterialGatheringProgress progress)
        {
            if (progress == null || progress.Version != 1 || progress.RandomState < 0
                || progress.TotalAttackCount < 0 || progress.TotalBreakCount < 0
                || progress.Dungeons == null || progress.Dungeons.Count > 16) return false;
            var ids = new HashSet<int>();
            foreach (OfflineMaterialGatheringDungeonState dungeon in progress.Dungeons)
            {
                if (dungeon == null || dungeon.DungeonType < 1 || dungeon.DungeonType > 4
                    || dungeon.TargetMaterialId < 0
                    || dungeon.RemainingDurability < 0 || dungeon.BreakCount < 0 || dungeon.AttackCount < 0
                    || dungeon.CollectedAmount < 0
                    || dungeon.NewbieSequenceIndex < 0 || dungeon.NewbieSequenceIndex > 5
                    || dungeon.TargetMaterialId == 0 && dungeon.RemainingDurability != 0
                    || !ids.Add(dungeon.DungeonType)) return false;
            }
            return true;
        }
    }

    [Serializable]
    public sealed class OfflineMaterialGatheringDungeonState
    {
        public int DungeonType;
        public int TargetMaterialId;
        public int RemainingDurability;
        public int NewbieSequenceIndex;
        public long BreakCount;
        public long AttackCount;
        public long CollectedAmount;
    }
}
