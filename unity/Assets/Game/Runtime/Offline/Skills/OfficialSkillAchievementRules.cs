using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Skills
{
    [Serializable]
    public sealed class OfflineSkillAchievementTemplate
    {
        public int RawId, Group, CycleCount;
        public long RequiredCount, Increment;
        public OfflineSkillItemCount[] Rewards = Array.Empty<OfflineSkillItemCount>();
    }
    public sealed class OfficialSkillAchievement
    {
        public int Group { get; }
        public int Index { get; }
        public int Id => (Index << 12) | Group;
        public int RawId { get; }
        public long RequiredCount { get; }
        public IReadOnlyList<OfflineSkillItemCount> Rewards { get; }
        public string EventId => "skill-achievement:" + Group.ToString(CultureInfo.InvariantCulture) + ":" + Index.ToString(CultureInfo.InvariantCulture);
        internal OfficialSkillAchievement(int group, int index, OfflineSkillAchievementTemplate row, long count)
        { Group = group; Index = index; RawId = row.RawId; RequiredCount = count; Rewards = row.Rewards; }
    }
    // Original AchievementRawInfoParser / CycleTemplateInfo expansion, AchievementUtils
    // search range, AchievementPanelBase filtering/sorting, and PlayerSkillDataExt score sum.
    public sealed class OfficialSkillAchievementRules
    {
        private readonly OfflineSkillRules rules;
        private readonly Dictionary<int, IReadOnlyList<OfficialSkillAchievement>> groups = new Dictionary<int, IReadOnlyList<OfficialSkillAchievement>>();
        public OfficialSkillAchievementRules(OfflineSkillRules value) { rules = value ?? throw new ArgumentNullException(nameof(value)); }
        public int ShowCount => rules.GiftShowCount;
        public IReadOnlyList<OfficialSkillAchievement> Group(int group)
        {
            if (group != 9 && group != 33) throw new ArgumentOutOfRangeException(nameof(group));
            if (groups.TryGetValue(group, out var existing)) return existing;
            var rows = rules.GiftTemplates.Where(value => value.Group == group).OrderBy(value => value.RawId).ToArray();
            if (rows.Length == 0) throw new FormatException("Original skill achievement group is unavailable: " + group);
            var result = new List<OfficialSkillAchievement>();
            Action<OfflineSkillAchievementTemplate, long> add = (row, count) =>
            {
                if (count <= 0 || row.Rewards.Length == 0 || row.Rewards.Any(value => value.ItemId <= 0 || value.Count <= 0))
                    throw new FormatException("Invalid original skill achievement milestone.");
                result.Add(new OfficialSkillAchievement(group, result.Count, row, count));
            };
            foreach (var row in rows.Where(value => value.CycleCount == 0 && value.Increment == 0)) add(row, row.RequiredCount);
            var seeds = rows.Where(value => value.CycleCount > 0).ToArray();
            if (seeds.Length > 1) throw new FormatException("Ambiguous skill achievement cycle seed.");
            if (seeds.Length == 1)
            {
                var seed = seeds[0];
                var increments = rows.Where(value => value.Increment > 0).ToArray();
                if (increments.Length == 0) throw new FormatException("Original achievement cycle increments are unavailable.");
                long count = seed.RequiredCount;
                add(seed, count);
                int total = checked(seed.CycleCount * increments.Length);
                for (int index = 1; index <= total; index++)
                {
                    var row = increments[(index - 1) % increments.Length];
                    count = checked(count + row.Increment);
                    add(row, count);
                }
            }
            return groups[group] = result;
        }
        public long Current(OfflineSkillProfile profile, int group)
        {
            if (group == 33) return profile.SeniorMaterialSpentOnLevelUp;
            if (group != 9) throw new ArgumentOutOfRangeException(nameof(group));
            long score = 0;
            foreach (var owned in profile.Skills)
            {
                if (owned == null) continue;
                var rank = rules.RankDisplays.FirstOrDefault(value => value.Rank == owned.Rank);
                if (rank == null) throw new FormatException("Original skill-rank score is unavailable: " + owned.Rank);
                score = checked(score + rank.Score);
            }
            return score;
        }
        public IReadOnlyList<OfficialSkillAchievement> CanClaim(OfflineSkillProfile profile, int group, ISet<string> recorded)
        {
            long count = Current(profile, group);
            return Group(group).Where(value => count >= value.RequiredCount && !recorded.Contains(value.EventId)).ToArray();
        }
        public IReadOnlyList<OfficialSkillAchievement> Visible(OfflineSkillProfile profile, int group, ISet<string> recorded,
            bool showReceived = false, bool showProcessing = true)
        {
            var milestones = Group(group);
            long count = Current(profile, group);
            int gotMax = milestones.Where(value => recorded.Contains(value.EventId)).Select(value => value.Index).DefaultIfEmpty(0).Max();
            int canMax = milestones.Where(value => count >= value.RequiredCount).Select(value => value.Index).DefaultIfEmpty(0).Max();
            int start = gotMax > 0 ? Math.Max(0, gotMax - ShowCount) : 0;
            int end = Math.Min(milestones.Count - 1, canMax + ShowCount);
            return milestones.Skip(start).Take(end - start + 1)
                .Where(value => recorded.Contains(value.EventId) ? showReceived : count >= value.RequiredCount || showProcessing)
                .OrderByDescending(value => recorded.Contains(value.EventId) ? 1 : count >= value.RequiredCount ? 3 : 2)
                .ThenBy(value => value.Index).Take(ShowCount).ToArray();
        }
    }
}
