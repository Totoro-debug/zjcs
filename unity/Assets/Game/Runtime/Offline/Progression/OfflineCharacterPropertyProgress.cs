using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    [System.Serializable]
    public sealed class OfflineCharacterPropertyProgress
    {
        public int SeasonId;
        public int TreasurePlaneRank = 1;
        public int LegionLibraryLevel = 1;
        public long AstrologicalCost;
        public List<OfflineLegionBuildingLevel> LegionBuildingLevels = CreateDefaultLegionBuildingLevels();
        public OfflineLegionBuildingUpgrade LegionBuildingUpgrade;
        public List<OfflineCookingUse> CookingUses = new List<OfflineCookingUse>();
        public List<OfflineExtraProperty> ExtraProperties = new List<OfflineExtraProperty>();
        public List<OfflineNpcFriendship> NpcFriendships = new List<OfflineNpcFriendship>();
        public List<OfflineNpcTalentTree> NpcTalentTrees = new List<OfflineNpcTalentTree>();
        public List<OfflineCharacterTreasure> Treasures = new List<OfflineCharacterTreasure>();
        public List<OfflineTreasureElementLevel> TreasureElementLevels = new List<OfflineTreasureElementLevel>();
        public List<OfflinePeriodicCard> PeriodicCards = new List<OfflinePeriodicCard>();
        public List<OfflineLegionMapSkill> LegionMapSkills = new List<OfflineLegionMapSkill>();
        public List<OfflineEquipCabinetSeasonScore> EquipCabinetScores = new List<OfflineEquipCabinetSeasonScore>();

        public static void Normalize(OfflineCharacterPropertyProgress progress)
        {
            if (progress == null) return;
            if (progress.TreasurePlaneRank == 0) progress.TreasurePlaneRank = 1;
            if (progress.LegionLibraryLevel == 0) progress.LegionLibraryLevel = 1;
            if (progress.LegionBuildingLevels == null)
                progress.LegionBuildingLevels = new List<OfflineLegionBuildingLevel>();
            for (int buildingId = 1; buildingId <= 12; buildingId++)
                if (!progress.LegionBuildingLevels.Any(value => value != null && value.BuildingId == buildingId))
                    progress.LegionBuildingLevels.Add(new OfflineLegionBuildingLevel
                    {
                        BuildingId = buildingId,
                        Level = buildingId == 1 || buildingId == 4 ? 1 : 0
                    });
            OfflineLegionBuildingLevel hall = progress.LegionBuildingLevels.First(value =>
                value != null && value.BuildingId == 1);
            if (hall.Level < 1) hall.Level = 1;
            OfflineLegionBuildingLevel library = progress.LegionBuildingLevels.First(value =>
                value != null && value.BuildingId == 4);
            if (library.Level < 1) library.Level = 1;
            progress.LegionLibraryLevel = library.Level;
            if (progress.LegionBuildingUpgrade != null
                && progress.LegionBuildingUpgrade.BuildingId == 0
                && progress.LegionBuildingUpgrade.HangUpType == 0
                && progress.LegionBuildingUpgrade.TargetLevel == 0
                && progress.LegionBuildingUpgrade.Progress == 0
                && progress.LegionBuildingUpgrade.RequiredProgress == 0
                && progress.LegionBuildingUpgrade.LastProgressUnixSeconds == 0
                && progress.LegionBuildingUpgrade.ProgressRemainder == 0)
                progress.LegionBuildingUpgrade = null;
            if (progress.CookingUses == null) progress.CookingUses = new List<OfflineCookingUse>();
            if (progress.ExtraProperties == null) progress.ExtraProperties = new List<OfflineExtraProperty>();
            if (progress.NpcFriendships == null) progress.NpcFriendships = new List<OfflineNpcFriendship>();
            if (progress.NpcTalentTrees == null) progress.NpcTalentTrees = new List<OfflineNpcTalentTree>();
            if (progress.Treasures == null) progress.Treasures = new List<OfflineCharacterTreasure>();
            if (progress.TreasureElementLevels == null)
                progress.TreasureElementLevels = new List<OfflineTreasureElementLevel>();
            if (progress.PeriodicCards == null) progress.PeriodicCards = new List<OfflinePeriodicCard>();
            if (progress.LegionMapSkills == null) progress.LegionMapSkills = new List<OfflineLegionMapSkill>();
            if (progress.EquipCabinetScores == null)
                progress.EquipCabinetScores = new List<OfflineEquipCabinetSeasonScore>();
            foreach (OfflineNpcTalentTree tree in progress.NpcTalentTrees)
                if (tree != null && tree.ActiveNodeIds == null) tree.ActiveNodeIds = new List<int>();
        }

        public static bool IsValid(OfflineCharacterPropertyProgress progress)
        {
            if (progress == null || progress.SeasonId < 0 || progress.TreasurePlaneRank < 1
                || progress.LegionLibraryLevel < 1 || progress.LegionLibraryLevel > 100
                || progress.LegionBuildingLevels == null || progress.LegionBuildingLevels.Count != 12
                || progress.AstrologicalCost < 0 || progress.CookingUses == null || progress.CookingUses.Count > 5000
                || progress.ExtraProperties == null || progress.ExtraProperties.Count > 20000
                || progress.NpcFriendships == null || progress.NpcFriendships.Count > 1000
                || progress.NpcTalentTrees == null || progress.NpcTalentTrees.Count > 1000
                || progress.Treasures == null || progress.Treasures.Count > 5000
                || progress.TreasureElementLevels == null || progress.TreasureElementLevels.Count > 100
                || progress.PeriodicCards == null || progress.PeriodicCards.Count > 100
                || progress.LegionMapSkills == null || progress.LegionMapSkills.Count > 1000
                || progress.EquipCabinetScores == null || progress.EquipCabinetScores.Count > 100) return false;

            OfflineLegionBuildingLevel hallLevel = progress.LegionBuildingLevels
                .FirstOrDefault(value => value != null && value.BuildingId == 1);
            OfflineLegionBuildingLevel libraryLevel = progress.LegionBuildingLevels
                .FirstOrDefault(value => value != null && value.BuildingId == 4);
            if (progress.LegionBuildingLevels.Any(value => value == null || value.BuildingId <= 0
                    || value.BuildingId > 12 || value.Level < 0 || value.Level > 100)
                || progress.LegionBuildingLevels.Select(value => value.BuildingId).Distinct().Count()
                    != progress.LegionBuildingLevels.Count
                || hallLevel == null || hallLevel.Level < 1 || libraryLevel == null
                || libraryLevel.Level != progress.LegionLibraryLevel) return false;
            if (progress.LegionBuildingUpgrade != null
                && (progress.LegionBuildingUpgrade.BuildingId <= 0
                    || progress.LegionBuildingUpgrade.HangUpType < 1
                    || progress.LegionBuildingUpgrade.HangUpType > 2
                    || progress.LegionBuildingUpgrade.TargetLevel <= 0
                    || progress.LegionBuildingUpgrade.Progress < 0
                    || progress.LegionBuildingUpgrade.RequiredProgress <= 0
                    || progress.LegionBuildingUpgrade.Progress >= progress.LegionBuildingUpgrade.RequiredProgress
                    || progress.LegionBuildingUpgrade.LastProgressUnixSeconds < 0
                    || progress.LegionBuildingUpgrade.ProgressRemainder < 0
                    || progress.LegionBuildingUpgrade.ProgressRemainder >= 3600)) return false;

            if (progress.CookingUses.Any(value => value == null || value.CookingId <= 0 || value.UseTimes < 0)
                || progress.CookingUses.Select(value => value.CookingId).Distinct().Count() != progress.CookingUses.Count)
                return false;
            if (progress.ExtraProperties.Any(value => value == null || value.PropId <= 0 || value.Level < 0
                    || value.PropType <= 0 || value.Count < 0 || value.SourceId == null || value.SourceId.Length > 128))
                return false;
            if (progress.NpcFriendships.Any(value => value == null || value.NpcClassId <= 0
                    || value.Level < 0 || value.FriendshipExp < 0
                    || value.ActivateTimeTicks < 0 || value.ActivateTimeTicks > System.DateTime.MaxValue.Ticks
                    || value.CollectedTimeTicks < 0 || value.CollectedTimeTicks > System.DateTime.MaxValue.Ticks)
                || progress.NpcFriendships.Select(value => value.NpcClassId).Distinct().Count()
                    != progress.NpcFriendships.Count) return false;
            if (progress.NpcTalentTrees.Any(value => value == null || value.NpcClassId <= 0
                    || value.TalentExp < 0 || value.ActiveNodeIds == null || value.ActiveNodeIds.Count > 1000
                    || value.ActiveNodeIds.Any(node => node <= 0)
                    || value.ActiveNodeIds.Distinct().Count() != value.ActiveNodeIds.Count)
                || progress.NpcTalentTrees.Select(value => value.NpcClassId).Distinct().Count()
                    != progress.NpcTalentTrees.Count) return false;
            if (progress.Treasures.Any(value => value == null || value.ClassId <= 0 || value.Star < 0
                    || value.HitCount < 0 || value.SlotIndex < -1 || value.SlotIndex > 31
                    || value.Level < 1 || value.BaseLevel < 1 || value.Level < value.BaseLevel)
                || progress.Treasures.Select(value => value.ClassId).Distinct().Count() != progress.Treasures.Count)
                return false;
            if (progress.TreasureElementLevels.Any(value => value == null || value.ElementType < 0
                    || value.Level < 1 || value.BaseLevel < 1 || value.Level < value.BaseLevel)
                || progress.TreasureElementLevels.Select(value => value.ElementType).Distinct().Count()
                    != progress.TreasureElementLevels.Count) return false;
            if (progress.PeriodicCards.Any(value => value == null || value.CardId <= 0
                    || value.ExpiresAtUnixSeconds < 0)
                || progress.PeriodicCards.Select(value => value.CardId).Distinct().Count()
                    != progress.PeriodicCards.Count) return false;
            if (progress.LegionMapSkills.Any(value => value == null || value.SkillClassId <= 0 || value.Level < 0)
                || progress.LegionMapSkills.Select(value => value.SkillClassId).Distinct().Count()
                    != progress.LegionMapSkills.Count) return false;
            return !progress.EquipCabinetScores.Any(value => value == null || value.SeasonId <= 0 || value.Score < 0)
                && progress.EquipCabinetScores.Select(value => value.SeasonId).Distinct().Count()
                    == progress.EquipCabinetScores.Count;
        }

        private static List<OfflineLegionBuildingLevel> CreateDefaultLegionBuildingLevels()
        {
            var levels = new List<OfflineLegionBuildingLevel>(12);
            for (int buildingId = 1; buildingId <= 12; buildingId++)
                levels.Add(new OfflineLegionBuildingLevel
                {
                    BuildingId = buildingId,
                    Level = buildingId == 1 || buildingId == 4 ? 1 : 0
                });
            return levels;
        }
    }

    [System.Serializable] public sealed class OfflineCookingUse { public int CookingId; public long UseTimes; }
    [System.Serializable] public sealed class OfflineExtraProperty
    {
        public string SourceId = string.Empty;
        public int PropId;
        public int Level;
        public int PropType;
        public long Count;
    }
    [System.Serializable] public sealed class OfflineNpcFriendship
    {
        public int NpcClassId;
        public int Level;
        public long FriendshipExp;
        // Zero preserves the unknown date in legacy saves; never infer a meeting date.
        public long ActivateTimeTicks;
        public bool IsCollected;
        public long CollectedTimeTicks;
    }
    [System.Serializable] public sealed class OfflineNpcTalentTree
    {
        public int NpcClassId;
        public long TalentExp;
        public List<int> ActiveNodeIds = new List<int>();
    }
    [System.Serializable] public sealed class OfflineCharacterTreasure
    {
        public int ClassId;
        public bool Active;
        public int Star;
        public int HitCount;
        public int SlotIndex = -1;
        public int Level = 1;
        public int BaseLevel = 1;
    }
    [System.Serializable] public sealed class OfflineTreasureElementLevel
    {
        public int ElementType;
        public int Level = 1;
        public int BaseLevel = 1;
    }
    [System.Serializable] public sealed class OfflinePeriodicCard
    {
        public int CardId;
        public long ExpiresAtUnixSeconds;
    }
    [System.Serializable] public sealed class OfflineLegionMapSkill { public int SkillClassId; public int Level; }
    [System.Serializable] public sealed class OfflineLegionBuildingLevel { public int BuildingId; public int Level; }
    [System.Serializable] public sealed class OfflineLegionBuildingUpgrade
    {
        public int BuildingId;
        public int HangUpType;
        public int TargetLevel;
        public long Progress;
        public long RequiredProgress;
        public long LastProgressUnixSeconds;
        public int ProgressRemainder;
    }
    [System.Serializable] public sealed class OfflineEquipCabinetSeasonScore { public int SeasonId; public int Score; }
}
