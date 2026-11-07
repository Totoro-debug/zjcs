using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.World
{
    public sealed class DailyDungeonLeaderboardGift
    {
        public int Id { get; internal set; }
        public int SourceId { get; internal set; }
        public string Title { get; internal set; }
        public int Grade { get; internal set; }
        public int RequiredCount { get; internal set; }
        public long Progress { get; internal set; }
        public IReadOnlyList<DailyDungeonAchievementReward> Rewards { get; internal set; }
        public DailyDungeonAchievementState State { get; internal set; }
    }

    public sealed partial class DailyDungeonModule
    {
        [Serializable] private sealed class TopGiftDocument
        { public string Manifest; public int AchievementType; public TopGiftDefinition[] Rows; }
        [Serializable] private sealed class TopGiftDefinition
        {
            public int Id, SourceId, Group, DungeonId, Difficulty, Grade, RequiredCount;
            public string Title;
            public DailyDungeonAchievementReward[] Rewards;
        }

        private TopGiftDefinition[] leaderboardGiftDefinitions;

        private TopGiftDefinition[] LeaderboardGiftDefinitions
        {
            get
            {
                if (leaderboardGiftDefinitions != null) return leaderboardGiftDefinitions;
                TextAsset asset = Resources.Load<TextAsset>("Dungeons/daily-dungeon-top-achievements")
                    ?? throw new FileNotFoundException("Official daily dungeon leaderboard gifts are missing.");
                TopGiftDocument document = JsonUtility.FromJson<TopGiftDocument>(asset.text);
                if (document?.Manifest != "884_156692" || document.AchievementType != 57
                    || document.Rows == null || document.Rows.Length == 0
                    || document.Rows.Any(row => row == null || row.Id <= 0 || row.SourceId <= 0
                        || row.DungeonId <= 0 || row.Difficulty < 0 || row.Grade <= 0
                        || row.RequiredCount <= 0 || row.Rewards == null || row.Rewards.Length == 0
                        || row.Rewards.Any(reward => reward == null || reward.ItemId <= 0 || reward.Count <= 0))
                    || document.Rows.Select(row => row.Id).Distinct().Count() != document.Rows.Length)
                    throw new InvalidDataException("Official daily dungeon leaderboard gifts failed validation.");
                return leaderboardGiftDefinitions = document.Rows;
            }
        }

        public IReadOnlyList<DailyDungeonLeaderboardGift> BuildLeaderboardGiftModel(int dungeonId, int difficulty)
        {
            if (ResolveLevel(dungeonId, difficulty)?.SupportsLeaderboard != true)
                return Array.Empty<DailyDungeonLeaderboardGift>();
            return LeaderboardGiftDefinitions.Where(row => row.DungeonId == dungeonId && row.Difficulty == difficulty)
                .Select(row =>
                {
                    long progress = Save.completionCounts.FirstOrDefault(value => value.dungeonId == dungeonId
                        && value.difficulty == difficulty && value.grade == row.Grade)?.count ?? 0;
                    return new DailyDungeonLeaderboardGift
                    {
                        Id = row.Id, SourceId = row.SourceId, Title = row.Title, Grade = row.Grade,
                        RequiredCount = row.RequiredCount, Progress = progress, Rewards = row.Rewards,
                        State = Save.claimedLeaderboardGiftIds.Contains(row.Id) ? DailyDungeonAchievementState.Received
                            : progress >= row.RequiredCount ? DailyDungeonAchievementState.Completed
                            : DailyDungeonAchievementState.Processing
                    };
                }).OrderByDescending(row => row.State == DailyDungeonAchievementState.Completed ? 2
                    : row.State == DailyDungeonAchievementState.Processing ? 1 : 0)
                .ThenBy(row => row.Id).ToArray();
        }

        public bool ClaimLeaderboardGift(int id,
            Func<string, IReadOnlyList<DailyDungeonAchievementReward>, bool> grantRewards)
        {
            TopGiftDefinition definition = LeaderboardGiftDefinitions.FirstOrDefault(row => row.Id == id);
            if (definition == null || grantRewards == null) return false;
            DailyDungeonLeaderboardGift gift = BuildLeaderboardGiftModel(definition.DungeonId, definition.Difficulty)
                .FirstOrDefault(row => row.Id == id);
            if (gift?.State != DailyDungeonAchievementState.Completed) return false;
            // The inventory adapter deduplicates this receipt if saving the
            // claimed flag fails after the inventory has committed.
            if (!grantRewards("daily-dungeon:leaderboard-gift:" + id, gift.Rewards)) return false;
            DailyDungeonSave next = Clone(Save);
            if (next.claimedLeaderboardGiftIds.Contains(id)) return false;
            next.claimedLeaderboardGiftIds.Add(id);
            next.claimedLeaderboardGiftIds.Sort();
            Persist(next);
            return true;
        }
    }
}
