using System;
using System.Collections.Generic;

namespace Zjcs.Offline.Skills
{
    [Serializable]
    public sealed class OfflineSkillProfile
    {
        public int Version = 1;
        public int SelectedPlan;
        public OfflineSkillPlan[] Plans = CreatePlans();
        public int[] ActiveSlotLevels = { 1, 1, 1, 1 };
        public int[] PassiveSlotLevels = { 1, 1, 1, 1 };
        public List<OfflineSkillBlessRecord> BlessRecords = new List<OfflineSkillBlessRecord>();
        public List<OfflineOwnedSkill> Skills = new List<OfflineOwnedSkill>();
        public List<OfflineSkillItemCount> Items = new List<OfflineSkillItemCount>();

        public static OfflineSkillPlan[] CreatePlans()
        {
            return new[]
            {
                new OfflineSkillPlan { Name = "方案一" },
                new OfflineSkillPlan { Name = "方案二" },
                new OfflineSkillPlan { Name = "方案三" }
            };
        }

        public OfflineSkillProfile Clone()
        {
            var copy = new OfflineSkillProfile
            {
                Version = Version,
                SelectedPlan = SelectedPlan,
                Plans = new OfflineSkillPlan[Plans == null ? 0 : Plans.Length],
                ActiveSlotLevels = Copy(ActiveSlotLevels),
                PassiveSlotLevels = Copy(PassiveSlotLevels),
                BlessRecords = new List<OfflineSkillBlessRecord>(),
                Skills = new List<OfflineOwnedSkill>(),
                Items = new List<OfflineSkillItemCount>()
            };
            for (int i = 0; i < copy.Plans.Length; i++)
                copy.Plans[i] = Plans[i] == null ? null : Plans[i].Clone();
            if (BlessRecords != null)
                foreach (OfflineSkillBlessRecord record in BlessRecords)
                    copy.BlessRecords.Add(record == null ? null : record.Clone());
            if (Skills != null)
                foreach (OfflineOwnedSkill skill in Skills)
                    copy.Skills.Add(skill == null ? null : skill.Clone());
            if (Items != null)
                foreach (OfflineSkillItemCount item in Items)
                    copy.Items.Add(item == null ? null : item.Clone());
            return copy;
        }

        private static int[] Copy(int[] values)
        {
            return values == null ? null : (int[])values.Clone();
        }
    }

    [Serializable]
    public sealed class OfflineSkillBlessRecord
    {
        public int SeasonId;
        public int[] ActiveSlotLevels = new int[4];
        public int[] PassiveSlotLevels = new int[4];

        public OfflineSkillBlessRecord Clone()
        {
            return new OfflineSkillBlessRecord
            {
                SeasonId = SeasonId,
                ActiveSlotLevels = ActiveSlotLevels == null ? null : (int[])ActiveSlotLevels.Clone(),
                PassiveSlotLevels = PassiveSlotLevels == null ? null : (int[])PassiveSlotLevels.Clone()
            };
        }
    }

    [Serializable]
    public sealed class OfflineSkillPlan
    {
        public string Name;
        public int[] ActiveSkills = new int[4];
        public int[] PassiveSkills = new int[4];

        public OfflineSkillPlan Clone()
        {
            return new OfflineSkillPlan
            {
                Name = Name,
                ActiveSkills = ActiveSkills == null ? null : (int[])ActiveSkills.Clone(),
                PassiveSkills = PassiveSkills == null ? null : (int[])PassiveSkills.Clone()
            };
        }
    }

    [Serializable]
    public sealed class OfflineOwnedSkill
    {
        public int ClassId;
        public int Rank;

        public OfflineOwnedSkill Clone() { return new OfflineOwnedSkill { ClassId = ClassId, Rank = Rank }; }
    }

    [Serializable]
    public sealed class OfflineSkillItemCount
    {
        public int ItemId;
        public long Count;

        public OfflineSkillItemCount Clone() { return new OfflineSkillItemCount { ItemId = ItemId, Count = Count }; }
    }

    [Serializable]
    public sealed class OfflineSkillDefinition
    {
        public int ClassId;
        public int ItemType;
        public int[] AvailableProfessions = Array.Empty<int>();
        public bool AvailableWithoutProfession;
        public int MaximumRank;
        public int PieceItemId;
        public int MoneyItemId;
        public OfflineSkillRankCost[] RankCosts = Array.Empty<OfflineSkillRankCost>();
    }

    [Serializable]
    public sealed class OfflineSkillRankCost
    {
        public int CurrentRank;
        public OfflineSkillItemCount[] Items = Array.Empty<OfflineSkillItemCount>();
    }

    [Serializable]
    public sealed class OfflineSkillRules
    {
        public OfflineSkillDefinition[] Skills = Array.Empty<OfflineSkillDefinition>();
        public OfflineSkillLevelCost[] SlotLevelCosts = Array.Empty<OfflineSkillLevelCost>();
        public OfflineSkillBlessCost[] SlotBlessCosts = Array.Empty<OfflineSkillBlessCost>();
        public OfflineSkillLevelLimit[] LevelLimits = Array.Empty<OfflineSkillLevelLimit>();
        public OfflineSkillSubRankLimit[] BaseLevelLimits = Array.Empty<OfflineSkillSubRankLimit>();
    }

    [Serializable]
    public sealed class OfflineSkillLevelCost
    {
        public int Level;
        public OfflineSkillItemCount[] Items = Array.Empty<OfflineSkillItemCount>();
    }

    [Serializable]
    public sealed class OfflineSkillBlessCost
    {
        public int Rank;
        public int BlessLevel;
        public OfflineSkillItemCount[] Items = Array.Empty<OfflineSkillItemCount>();
    }

    [Serializable]
    public sealed class OfflineSkillLevelLimit
    {
        public int SubRank;
        public int PlayerLevel;
        public int PlayerBlessLevel;
        public int SkillLevel;
        public int SkillBlessLevel;
    }

    [Serializable]
    public sealed class OfflineSkillSubRankLimit
    {
        public int SubRank;
        public int SkillLevel;
    }

    public sealed class OfflineSkillPlayerContext
    {
        public int Profession;
        public int SubRank;
        public int BaseLevel;
        public int PlayerRank;
        public int CurrentSeasonRank;
        public int CurrentSeasonId;
        public int PlayerBlessLevel;
        public HashSet<int> CurrentProfessionChain = new HashSet<int>();
    }

    public enum OfflineSkillResultCode
    {
        Applied,
        Unchanged,
        InvalidProfile,
        InvalidArgument,
        SkillNotOwned,
        SkillUnavailable,
        SlotUnavailable,
        LevelLimit,
        InsufficientItems,
        NoUpgradeAvailable
    }

    public sealed class OfflineSkillResult
    {
        public OfflineSkillResultCode Code { get; }
        public IReadOnlyDictionary<int, long> Cost { get; }
        public int ChangedCount { get; }
        public string Message { get; }
        public bool IsApplied => Code == OfflineSkillResultCode.Applied;

        internal OfflineSkillResult(OfflineSkillResultCode code,
            IDictionary<int, long> cost = null, int changedCount = 0, string message = null)
        {
            Code = code;
            Cost = cost == null ? new Dictionary<int, long>() : new Dictionary<int, long>(cost);
            ChangedCount = changedCount;
            Message = message ?? string.Empty;
        }
    }
}
