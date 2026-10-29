using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class DailyChallengeSave
    {
        public int schema = 1;
        public long firstEnteredLocalTicks;
        public long highestObservedLocalTicks;
        public int highestPeriodIndex;
        public long highestLogicDayTicks;
        public List<DailyChallengeActivitySave> activities = new List<DailyChallengeActivitySave>();
    }

    [Serializable]
    public sealed class DailyChallengeActivitySave
    {
        public int activityId;
        public int periodIndex;
        public string logicDay = string.Empty;
        public int participationCount;
        public bool participated;
        public string activeBattleCheckpoint = string.Empty;
        public int challengeDifficulty;
        public int dailyKillCount;
        public int periodKillCount;
        public int openedChestCount;
        public bool dailyChallengeAwardClaimed;
        public int dailyChallengeAwardCount;
        public int highestDifficulty;
        public long highestDamage;
        public int highestProgress;
        public List<int> claimedStepScores = new List<int>();
        public List<int> claimedBossDifficulties = new List<int>();
        public int pendingChestSeed;
        public int pendingChestPeriodIndex;
        public string pendingChestTransactionId = string.Empty;
        public List<OfficialChallengeReward> pendingChestRewards = new List<OfficialChallengeReward>();
        public List<string> completedBattleIds = new List<string>();
    }

    public sealed class OfflineDailyChallengeService
    {
        public const int FunnyBossActivityId = 217;
        private readonly string path;
        private DailyChallengeSave save;

        public OfflineDailyChallengeService(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("A save path is required.", nameof(savePath));
            path = Path.GetFullPath(savePath);
            Load();
        }

        public long FirstEnteredLocalTicks => save.firstEnteredLocalTicks;

        public void EnterFeature(DateTime localNow)
        {
            if (localNow.Kind != DateTimeKind.Local) localNow = DateTime.SpecifyKind(localNow, DateTimeKind.Local);
            if (save.firstEnteredLocalTicks == 0) save.firstEnteredLocalTicks = localNow.Ticks;
            if (localNow.Ticks > save.highestObservedLocalTicks)
                save.highestObservedLocalTicks = localNow.Ticks;
            Persist();
        }

        public DateTime GetPeriodStartLocal(DateTime localNow, int durationHours)
        {
            EnsureEntered(localNow);
            if (durationHours <= 1) throw new ArgumentOutOfRangeException(nameof(durationHours));
            localNow = AdvanceLocalClock(localNow, durationHours);
            DateTime anchor = new DateTime(save.firstEnteredLocalTicks, DateTimeKind.Local);
            long periodTicks = TimeSpan.FromHours(durationHours).Ticks;
            long index = CurrentPeriodIndex(localNow, durationHours);
            return new DateTime(anchor.Ticks + index * periodTicks, DateTimeKind.Local);
        }

        public DateTime GetLogicDayStartLocal(DateTime localNow)
        {
            localNow = AdvanceLocalClock(localNow, 0);
            DateTime today = localNow.Date.AddHours(8);
            DateTime start = localNow < today ? today.AddDays(-1) : today;
            long highest = Math.Max(save.highestLogicDayTicks,
                save.activities.Select(activity => long.TryParse(activity.logicDay, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out long ticks) ? ticks : 0).DefaultIfEmpty(0).Max());
            return highest > start.Ticks ? new DateTime(highest, DateTimeKind.Local) : start;
        }

        public bool CanParticipate(int activityId, DateTime localNow, int durationHours, int dailyLimit,
            out string reason)
        {
            reason = string.Empty;
            if (activityId <= 0 || durationHours <= 1 || dailyLimit <= 0)
            {
                reason = "活动配置无效";
                return false;
            }
            EnsureEntered(localNow);
            localNow = AdvanceLocalClock(localNow, durationHours);
            DateTime anchor = new DateTime(save.firstEnteredLocalTicks, DateTimeKind.Local);
            DateTime periodStart = GetPeriodStartLocal(localNow, durationHours);
            TimeSpan elapsed = localNow - periodStart;
            DateTime logicDayStart = GetLogicDayStartLocal(localNow);
            if (elapsed < TimeSpan.Zero || elapsed >= TimeSpan.FromHours(durationHours - 1)
                || localNow >= logicDayStart.AddHours(23))
            {
                reason = "活动结算中";
                return false;
            }
            if (localNow < anchor)
            {
                reason = "活动尚未开启";
                return false;
            }
            DailyChallengeActivitySave activity = GetActivity(activityId);
            ResetPeriod(activity, localNow, durationHours);
            ResetLogicDay(activity, localNow);
            int legacyCount = activity.participated && activity.participationCount == 0 ? 1 : 0;
            if (activity.participationCount + legacyCount >= dailyLimit)
            {
                reason = "今日已参与";
                Persist();
                return false;
            }
            Persist();
            return true;
        }

        public bool RecordParticipation(int activityId, DateTime localNow, int durationHours, int dailyLimit,
            int difficulty = 0, string checkpoint = null)
        {
            if (!CanParticipate(activityId, localNow, durationHours, dailyLimit, out _)) return false;
            DailyChallengeActivitySave activity = GetActivity(activityId);
            activity.participationCount++;
            activity.participated = true;
            activity.challengeDifficulty = Math.Max(0, difficulty);
            activity.activeBattleCheckpoint = checkpoint ?? string.Empty;
            Persist();
            return true;
        }

        public bool RecordDoubleShadowResult(DateTime localNow, int durationHours, long actualDamage, long bossMaxHp)
        {
            if (actualDamage < 0 || bossMaxHp <= 0 || durationHours <= 1) return false;
            EnsureEntered(localNow);
            DailyChallengeActivitySave activity = GetActivity(FunnyBossActivityId);
            ResetPeriod(activity, localNow, durationHours);
            ResetLogicDay(activity, localNow);
            activity.highestDamage = Math.Max(activity.highestDamage, actualDamage);
            int progress = actualDamage >= bossMaxHp ? 10000 :
                (int)Math.Min(10000m, (decimal)actualDamage * 10000m / bossMaxHp);
            activity.highestProgress = Math.Max(activity.highestProgress, progress);
            Persist();
            return true;
        }

        public bool RecordDoubleShadowKill(int durationHours, DateTime localNow, string battleId,
            int dailyKillLimit)
        {
            if (durationHours <= 1 || dailyKillLimit <= 0 || string.IsNullOrWhiteSpace(battleId)) return false;
            EnsureEntered(localNow);
            DailyChallengeActivitySave activity = GetActivity(FunnyBossActivityId);
            ResetPeriod(activity, localNow, durationHours);
            ResetLogicDay(activity, localNow);
            if (activity.completedBattleIds.Contains(battleId)) return true;
            activity.completedBattleIds.Add(battleId);
            if (activity.dailyKillCount < dailyKillLimit)
            {
                activity.dailyKillCount++;
                activity.periodKillCount++;
            }
            Persist();
            return true;
        }

        public int DoubleShadowChestCount(int durationHours, DateTime localNow)
        {
            if (durationHours <= 1 || save.firstEnteredLocalTicks == 0) return 0;
            DailyChallengeActivitySave activity = GetActivity(FunnyBossActivityId);
            ResetPeriod(activity, localNow, durationHours);
            return Math.Max(0, activity.periodKillCount - activity.openedChestCount);
        }

        public int GetDoubleShadowProgress(DateTime localNow, int durationHours, long bossMaxHp)
        {
            if (bossMaxHp <= 0 || durationHours <= 1) return 0;
            DailyChallengeActivitySave activity = GetActivity(FunnyBossActivityId);
            int periodIndex = CurrentPeriodIndex(localNow, durationHours);
            if (activity.periodIndex != periodIndex
                || !string.Equals(activity.logicDay,
                    GetLogicDayStartLocal(localNow).Ticks.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                return 0;
            int progress = activity.highestDamage >= bossMaxHp ? 10000 :
                (int)Math.Min(10000m, (decimal)activity.highestDamage * 10000m / bossMaxHp);
            return Math.Max(activity.highestProgress, progress);
        }

        public int GetDoubleShadowScore(DateTime localNow, int durationHours)
        {
            if (durationHours <= 1 || save.firstEnteredLocalTicks == 0) return 0;
            DailyChallengeActivitySave activity = GetActivity(FunnyBossActivityId);
            int periodIndex = CurrentPeriodIndex(localNow, durationHours);
            string logicDay = GetLogicDayStartLocal(localNow).Ticks.ToString(CultureInfo.InvariantCulture);
            return activity.periodIndex == periodIndex && activity.logicDay == logicDay
                ? activity.highestProgress : 0;
        }

        public long GetDoubleShadowDamage(DateTime localNow, int durationHours)
        {
            if (durationHours <= 1 || save.firstEnteredLocalTicks == 0) return 0;
            DailyChallengeActivitySave activity = GetActivity(FunnyBossActivityId);
            int periodIndex = CurrentPeriodIndex(localNow, durationHours);
            string logicDay = GetLogicDayStartLocal(localNow).Ticks.ToString(CultureInfo.InvariantCulture);
            return activity.periodIndex == periodIndex && activity.logicDay == logicDay
                ? activity.highestDamage : 0;
        }

        public int ParticipationCount(int activityId, DateTime localNow)
        {
            DailyChallengeActivitySave activity = GetActivity(activityId);
            ResetLogicDay(activity, localNow);
            if (!string.Equals(activity.logicDay,
                    GetLogicDayStartLocal(localNow).Ticks.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                return 0;
            Persist();
            return activity.participationCount > 0 ? activity.participationCount
                : activity.participated ? 1 : 0;
        }

        public bool DailyChallengeAwardClaimed(int activityId, DateTime localNow)
        {
            DailyChallengeActivitySave activity = GetActivity(activityId);
            ResetLogicDay(activity, localNow);
            return activity.dailyChallengeAwardClaimed;
        }

        public ProgressionResult ClaimDoubleShadowSteps(IEnumerable<int> configuredScores, string transactionId,
            OfflineMainlineProgressSession profile, Action<OfflineMainlineSave, IReadOnlyList<int>> grantRewards,
            DateTime localNow, int durationHours)
        {
            if (durationHours <= 1 || configuredScores == null || profile == null || grantRewards == null
                || string.IsNullOrWhiteSpace(transactionId))
                return new ProgressionResult { Status = ProgressionResultStatus.InvalidState,
                    Message = "阶段奖励领取参数无效。" };
            DailyChallengeActivitySave activity = GetActivity(FunnyBossActivityId);
            ResetPeriod(activity, localNow, durationHours);
            ResetLogicDay(activity, localNow);
            int[] claimable = configuredScores.Distinct().Where(score => score > 0
                && score <= activity.highestProgress && !activity.claimedStepScores.Contains(score))
                .OrderBy(score => score).ToArray();
            if (claimable.Length == 0)
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            ProgressionResult result = profile.ApplyInventoryTransaction(transactionId,
                next => grantRewards(next, claimable));
            if (result.Status != ProgressionResultStatus.Applied && result.Status != ProgressionResultStatus.Unchanged)
                return result;
            foreach (int score in claimable)
                if (!activity.claimedStepScores.Contains(score)) activity.claimedStepScores.Add(score);
            Persist();
            return result;
        }

        public ProgressionResult ClaimBossDifficultyAward(int activityId, int difficulty,
            OfficialDailyChallengeCatalog catalog, OfflineMainlineProgressSession profile,
            DateTime localNow, int durationHours)
        {
            if (catalog == null || profile == null || difficulty < 0 || durationHours <= 1)
                return InvalidRewardClaim();
            EnsureEntered(localNow);
            DailyChallengeActivitySave activity = GetActivity(activityId);
            ResetPeriod(activity, localNow, durationHours);
            if (activity.highestDifficulty <= difficulty || activity.claimedBossDifficulties.Contains(difficulty))
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            OfficialDomainBossDifficulty configured = catalog.Difficulty(activityId, difficulty);
            if (configured == null) return InvalidRewardClaim();
            string transactionId = "daily-challenge:boss:" + activityId + ":"
                + GetPeriodStartLocal(localNow, durationHours).Ticks + ":" + difficulty;
            IReadOnlyList<OfficialChallengeReward> rewards = catalog.ResolveBossDifficultyRewards(
                activityId, difficulty, StableSeed(transactionId));
            ProgressionResult result = GrantRewards(profile, transactionId, rewards, localNow);
            if (!IsCommitted(result)) return result;
            if (!activity.claimedBossDifficulties.Contains(difficulty)) activity.claimedBossDifficulties.Add(difficulty);
            Persist();
            return result;
        }

        public ProgressionResult ClaimDailyChallengeAward(int activityId, OfficialDailyChallengeCatalog catalog,
            OfflineMainlineProgressSession profile, DateTime localNow, int durationHours)
        {
            if (catalog == null || profile == null || durationHours <= 1) return InvalidRewardClaim();
            EnsureEntered(localNow);
            DailyChallengeActivitySave activity = GetActivity(activityId);
            ResetPeriod(activity, localNow, durationHours);
            ResetLogicDay(activity, localNow);
            int awardLimit = catalog.DailyAwardLimit(activityId);
            if (awardLimit <= 0 || activity.dailyChallengeAwardCount >= awardLimit)
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            IReadOnlyList<OfficialChallengeProperty> configured = catalog.DailyChallengeAwards(activityId);
            if (configured.Count == 0) return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            var rewards = configured.Where(value => value.Id > 0 && value.Value > 0)
                .Select(value => new OfficialChallengeReward { ItemId = value.Id, Count = value.Value }).ToArray();
            string transactionId = "daily-challenge:daily-award:" + activityId + ":"
                + GetLogicDayStartLocal(localNow).Ticks + ":" + activity.dailyChallengeAwardCount;
            ProgressionResult result = GrantRewards(profile, transactionId, rewards, localNow);
            if (!IsCommitted(result)) return result;
            activity.dailyChallengeAwardCount++;
            activity.dailyChallengeAwardClaimed = activity.dailyChallengeAwardCount >= awardLimit;
            Persist();
            return result;
        }

        public ProgressionResult ClaimDoubleShadowChest(OfficialDailyChallengeCatalog catalog,
            OfflineMainlineProgressSession profile, DateTime localNow, int durationHours)
        {
            if (catalog == null || profile == null || durationHours <= 1) return InvalidRewardClaim();
            EnsureEntered(localNow);
            DailyChallengeActivitySave activity = GetActivity(FunnyBossActivityId);
            ResetPeriod(activity, localNow, durationHours);
            if (string.IsNullOrEmpty(activity.pendingChestTransactionId))
            {
                if (activity.periodKillCount <= activity.openedChestCount)
                    return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
                activity.pendingChestSeed = BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0);
                activity.pendingChestPeriodIndex = activity.periodIndex;
                activity.pendingChestTransactionId = "daily-challenge:chest:" + activity.periodIndex + ":"
                    + activity.openedChestCount + ":" + activity.pendingChestSeed;
                activity.pendingChestRewards = catalog.RollDoubleShadowChest(activity.pendingChestSeed).ToList();
                Persist();
            }
            ProgressionResult result = GrantRewards(profile, activity.pendingChestTransactionId,
                activity.pendingChestRewards, localNow);
            if (!IsCommitted(result)) return result;
            if (activity.pendingChestPeriodIndex == activity.periodIndex) activity.openedChestCount++;
            activity.pendingChestSeed = 0;
            activity.pendingChestPeriodIndex = 0;
            activity.pendingChestTransactionId = string.Empty;
            activity.pendingChestRewards.Clear();
            Persist();
            return result;
        }

        public int GetHighestDifficulty(int activityId) => GetActivity(activityId).highestDifficulty;

        public string ActiveBattleCheckpoint(int activityId) => GetActivity(activityId).activeBattleCheckpoint ?? string.Empty;

        public int ActiveChallengeDifficulty(int activityId) => GetActivity(activityId).challengeDifficulty;

        public void SaveBattleCheckpoint(int activityId, string checkpoint)
        {
            if (activityId <= 0) throw new ArgumentOutOfRangeException(nameof(activityId));
            DailyChallengeActivitySave activity = GetActivity(activityId);
            activity.activeBattleCheckpoint = checkpoint ?? string.Empty;
            Persist();
        }

        public void RecordDifficulty(int activityId, int difficulty)
        {
            if (difficulty < 0) return;
            DailyChallengeActivitySave activity = GetActivity(activityId);
            activity.highestDifficulty = Math.Max(activity.highestDifficulty, difficulty);
            Persist();
        }

        private void EnsureEntered(DateTime localNow)
        {
            if (save.firstEnteredLocalTicks == 0) EnterFeature(localNow);
        }

        private void ResetPeriod(DailyChallengeActivitySave activity, DateTime localNow, int durationHours)
        {
            int previousHigh = Math.Max(save.highestPeriodIndex,
                save.activities.Select(value => value.periodIndex).DefaultIfEmpty(0).Max());
            int periodIndex = CurrentPeriodIndex(localNow, durationHours, previousHigh);
            save.highestPeriodIndex = Math.Max(previousHigh, periodIndex);
            if (activity.periodIndex == periodIndex) return;
            activity.periodIndex = periodIndex;
            activity.participationCount = 0;
            activity.participated = false;
            activity.highestDifficulty = 0;
            activity.highestDamage = 0;
            activity.highestProgress = 0;
            activity.claimedStepScores.Clear();
            activity.claimedBossDifficulties.Clear();
            activity.dailyKillCount = 0;
            activity.periodKillCount = 0;
            activity.openedChestCount = 0;
            activity.completedBattleIds.Clear();
        }

        private void ResetLogicDay(DailyChallengeActivitySave activity, DateTime localNow)
        {
            DateTime logicDayStart = GetLogicDayStartLocal(localNow);
            save.highestLogicDayTicks = Math.Max(save.highestLogicDayTicks, logicDayStart.Ticks);
            string logicDay = logicDayStart.Ticks.ToString(CultureInfo.InvariantCulture);
            if (string.Equals(activity.logicDay, logicDay, StringComparison.Ordinal)) return;
            activity.logicDay = logicDay;
            activity.participationCount = 0;
            activity.participated = false;
            activity.highestDamage = 0;
            activity.highestProgress = 0;
            activity.claimedStepScores.Clear();
            activity.dailyChallengeAwardClaimed = false;
            activity.dailyChallengeAwardCount = 0;
            activity.dailyKillCount = 0;
        }

        private int CurrentPeriodIndex(DateTime localNow, int durationHours, int previousHigh = -1)
        {
            if (durationHours <= 1 || save.firstEnteredLocalTicks == 0) return 0;
            localNow = AdvanceLocalClock(localNow, durationHours);
            int current = (int)Math.Max(0,
                (localNow.Ticks - save.firstEnteredLocalTicks) / TimeSpan.FromHours(durationHours).Ticks);
            int savedHigh = previousHigh >= 0 ? previousHigh : Math.Max(save.highestPeriodIndex,
                save.activities.Select(value => value.periodIndex).DefaultIfEmpty(0).Max());
            return Math.Max(current, savedHigh);
        }

        private DateTime AdvanceLocalClock(DateTime localNow, int durationHours)
        {
            if (localNow.Kind != DateTimeKind.Local) localNow = DateTime.SpecifyKind(localNow, DateTimeKind.Local);
            long highest = Math.Max(save.highestObservedLocalTicks, save.firstEnteredLocalTicks);
            foreach (DailyChallengeActivitySave activity in save.activities)
                if (long.TryParse(activity.logicDay, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out long logicDayTicks))
                    highest = Math.Max(highest, logicDayTicks);
            if (durationHours > 1 && save.firstEnteredLocalTicks > 0)
                highest = Math.Max(highest, save.firstEnteredLocalTicks
                    + (long)save.highestPeriodIndex * TimeSpan.FromHours(durationHours).Ticks);
            highest = Math.Max(highest, localNow.Ticks);
            if (highest > save.highestObservedLocalTicks)
            {
                save.highestObservedLocalTicks = highest;
                Persist();
            }
            return new DateTime(highest, DateTimeKind.Local);
        }

        private static ProgressionResult GrantRewards(OfflineMainlineProgressSession profile, string transactionId,
            IEnumerable<OfficialChallengeReward> rewards, DateTime localNow)
        {
            OfficialChallengeReward[] rows = (rewards ?? Array.Empty<OfficialChallengeReward>())
                .Where(value => value != null && value.ItemId > 0 && value.Count > 0).ToArray();
            return profile.ApplyInventoryTransaction(transactionId, save =>
            {
                foreach (OfficialChallengeReward reward in rows)
                {
                    InventoryOperationResult granted = OfflineInventoryOperations.Grant(save.Player,
                        reward.ItemId, reward.Count, new DateTimeOffset(localNow).ToUnixTimeSeconds());
                    if (granted.Status != InventoryOperationStatus.Applied)
                        throw new InvalidOperationException("Daily challenge reward could not be granted: " + reward.ItemId);
                }
            });
        }

        private static bool IsCommitted(ProgressionResult result) =>
            result.Status == ProgressionResultStatus.Applied || result.Status == ProgressionResultStatus.Unchanged;

        private static ProgressionResult InvalidRewardClaim() => new ProgressionResult
        { Status = ProgressionResultStatus.InvalidState, Message = "阶段奖励领取参数无效。" };

        private static int StableSeed(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value) hash = (hash ^ character) * 16777619;
                return (int)hash;
            }
        }

        private DailyChallengeActivitySave GetActivity(int activityId)
        {
            DailyChallengeActivitySave activity = save.activities.FirstOrDefault(item => item.activityId == activityId);
            if (activity != null) return activity;
            activity = new DailyChallengeActivitySave { activityId = activityId };
            save.activities.Add(activity);
            return activity;
        }

        private void Load()
        {
            if (!File.Exists(path))
            {
                save = new DailyChallengeSave();
                return;
            }
            save = JsonUtility.FromJson<DailyChallengeSave>(File.ReadAllText(path));
            if (save == null || save.schema != 1 || save.activities == null)
                throw new InvalidDataException("The daily challenge save is corrupt.");
        }

        private void Persist()
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
            if (File.Exists(path))
            {
                string backup = path + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Replace(temporary, path, backup);
            }
            else File.Move(temporary, path);
        }
    }
}
