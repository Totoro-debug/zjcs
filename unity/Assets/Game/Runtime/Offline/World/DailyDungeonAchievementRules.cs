using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.World
{
    [Serializable]
    public sealed class DailyDungeonAchievementReward
    {
        public int ItemId;
        public long Count;
    }

    [Serializable]
    public sealed class DailyDungeonAchievementDefinition
    {
        public int Id;
        public int RequiredCount;
        public int CycleIncrement;
        public int CycleCount;
        public DailyDungeonAchievementReward[] Rewards;
    }

    [Serializable]
    internal sealed class DailyDungeonAchievementDocument
    {
        public string Manifest;
        public int Group;
        public int AchievementType;
        public DailyDungeonAchievementDefinition[] Rows;
    }

    public enum DailyDungeonAchievementState
    {
        Processing,
        Completed,
        Received
    }

    public sealed class DailyDungeonAchievementMilestone
    {
        public int Index { get; internal set; }
        public int SourceId { get; internal set; }
        public int RequiredCount { get; internal set; }
        public IReadOnlyList<DailyDungeonAchievementReward> Rewards { get; internal set; }
        public DailyDungeonAchievementState State { get; internal set; }
    }

    public sealed class DailyDungeonAchievementRules
    {
        private const string ExpectedManifest = "854_156383";
        private readonly DailyDungeonAchievementMilestone[] milestones;

        private DailyDungeonAchievementRules(DailyDungeonAchievementMilestone[] values)
        {
            milestones = values;
        }

        public static DailyDungeonAchievementRules LoadDefault()
        {
            TextAsset asset = Resources.Load<TextAsset>("Dungeons/daily-dungeon-achievements");
            if (asset == null)
                throw new FileNotFoundException("Official daily dungeon achievement rules are missing.");
            DailyDungeonAchievementDocument document = JsonUtility.FromJson<DailyDungeonAchievementDocument>(asset.text);
            if (document == null || document.Manifest != ExpectedManifest || document.Group != 39
                || document.AchievementType != 22 || document.Rows == null)
                throw new InvalidDataException("Official daily dungeon achievement rules failed validation.");

            DailyDungeonAchievementDefinition[] rows = document.Rows;
            DailyDungeonAchievementDefinition[] seeds = rows.Where(value => value != null && value.CycleCount > 0).ToArray();
            DailyDungeonAchievementDefinition[] cycles = rows.Where(value => value != null && value.CycleIncrement > 0)
                .OrderBy(value => value.Id).ToArray();
            if (seeds.Length != 1 || cycles.Length == 0)
                throw new InvalidDataException("Official daily dungeon achievement cycle rows are missing or ambiguous.");

            var result = new List<DailyDungeonAchievementMilestone>();
            foreach (DailyDungeonAchievementDefinition row in rows.Where(value => value != null
                    && value.CycleCount == 0 && value.CycleIncrement == 0)
                .OrderBy(value => value.Id))
                result.Add(Create(result.Count, row, row.RequiredCount));

            DailyDungeonAchievementDefinition seed = seeds[0];
            int requiredCount = seed.RequiredCount;
            result.Add(Create(result.Count, seed, requiredCount));
            int cycleLength = checked(seed.CycleCount * cycles.Length);
            if (cycleLength > 10000)
                throw new InvalidDataException("Official daily dungeon achievement cycle is unexpectedly large.");
            for (int index = 0; index < cycleLength; index++)
            {
                DailyDungeonAchievementDefinition row = cycles[index % cycles.Length];
                requiredCount = checked(requiredCount + row.CycleIncrement);
                result.Add(Create(result.Count, row, requiredCount));
            }
            if (result.Count < 5 || result.Any(value => value.RequiredCount <= 0
                || value.Rewards.Count == 0 || value.Rewards.Any(reward => reward == null
                    || reward.ItemId <= 0 || reward.Count <= 0)))
                throw new InvalidDataException("Official daily dungeon achievement milestones are invalid.");
            return new DailyDungeonAchievementRules(result.ToArray());
        }

        public IReadOnlyList<DailyDungeonAchievementMilestone> Visible(long progress,
            IEnumerable<int> claimedIndexes, int showCount = 5, bool showReceived = false)
        {
            progress = Math.Max(0L, progress);
            showCount = Math.Max(1, showCount);
            var claimed = new HashSet<int>(claimedIndexes ?? Array.Empty<int>());
            int gotMax = claimed.Where(index => index >= 0 && index < milestones.Length)
                .DefaultIfEmpty(0).Max();
            int canMax = milestones.Where(value => value.RequiredCount <= progress)
                .Select(value => value.Index).DefaultIfEmpty(0).Max();
            int start = gotMax > 0 ? Math.Max(0, gotMax - showCount) : 0;
            int end = Math.Min(milestones.Length - 1, canMax + showCount);
            var visible = new List<DailyDungeonAchievementMilestone>();
            for (int index = start; index <= end; index++)
            {
                DailyDungeonAchievementMilestone row = milestones[index];
                bool received = claimed.Contains(index);
                if (received && !showReceived) continue;
                visible.Add(new DailyDungeonAchievementMilestone
                {
                    Index = row.Index,
                    SourceId = row.SourceId,
                    RequiredCount = row.RequiredCount,
                    Rewards = row.Rewards,
                    State = received ? DailyDungeonAchievementState.Received
                        : row.RequiredCount <= progress ? DailyDungeonAchievementState.Completed
                        : DailyDungeonAchievementState.Processing
                });
            }
            return visible.OrderByDescending(value => value.State == DailyDungeonAchievementState.Completed ? 3
                    : value.State == DailyDungeonAchievementState.Processing ? 2 : 1)
                .ThenBy(value => value.Index).Take(showCount).ToArray();
        }

        public DailyDungeonAchievementMilestone Find(int index)
            => index < 0 || index >= milestones.Length ? null : milestones[index];

        private static DailyDungeonAchievementMilestone Create(int index,
            DailyDungeonAchievementDefinition row, int requiredCount)
        {
            if (row == null || row.Id <= 0 || requiredCount <= 0 || row.Rewards == null)
                throw new InvalidDataException("Official daily dungeon achievement row is invalid.");
            return new DailyDungeonAchievementMilestone
            {
                Index = index,
                SourceId = row.Id,
                RequiredCount = requiredCount,
                Rewards = row.Rewards.ToArray()
            };
        }
    }
}
