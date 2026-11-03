using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class WeeklyChallengeSave
    {
        public int schema = 1;
        public long highestObservedLocalTicks;
        public List<WeeklyChallengePeriodSave> periods = new List<WeeklyChallengePeriodSave>();
    }

    [Serializable]
    public sealed class WeeklyChallengePeriodSave
    {
        public long startLocalTicks;
        public long demonKingDamage;
        public List<WeeklyChallengeStageSave> stages = new List<WeeklyChallengeStageSave>();
    }

    [Serializable]
    public sealed class WeeklyChallengeStageSave
    {
        public int stageId;
        public int attempts;
        public int wins;
        public string activeBattleCheckpoint = string.Empty;
    }

    public sealed class OfflineWeeklyChallengeSession
    {
        private readonly string path;
        private WeeklyChallengeSave save;

        public OfflineWeeklyChallengeSession(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath)) throw new ArgumentException("A save path is required.", nameof(savePath));
            path = Path.GetFullPath(savePath);
            Load();
        }

        public long CurrentPeriodStartLocalTicks(DateTime localNow)
        {
            if (localNow.Kind != DateTimeKind.Local) localNow = DateTime.SpecifyKind(localNow, DateTimeKind.Local);
            if (localNow.Ticks > save.highestObservedLocalTicks)
            {
                save.highestObservedLocalTicks = localNow.Ticks;
                Persist();
            }
            DateTime observed = new DateTime(Math.Max(localNow.Ticks, save.highestObservedLocalTicks), DateTimeKind.Local);
            int daysSinceSunday = (int)observed.DayOfWeek;
            DateTime start = observed.Date.AddDays(-daysSinceSunday).AddHours(8);
            if (observed < start) start = start.AddDays(-7);
            return start.Ticks;
        }

        public bool TryResumeBattle(int stageId, out string checkpoint, out long periodStartLocalTicks)
        {
            foreach (WeeklyChallengePeriodSave period in save.periods.OrderByDescending(value => value.startLocalTicks))
            {
                WeeklyChallengeStageSave stage = period.stages.FirstOrDefault(value => value.stageId == stageId);
                if (stage == null || string.IsNullOrEmpty(stage.activeBattleCheckpoint)) continue;
                checkpoint = stage.activeBattleCheckpoint;
                periodStartLocalTicks = period.startLocalTicks;
                return true;
            }
            checkpoint = string.Empty;
            periodStartLocalTicks = 0;
            return false;
        }

        public long DemonKingDamage(long periodStartLocalTicks)
        {
            WeeklyChallengePeriodSave period = save.periods.FirstOrDefault(value => value.startLocalTicks == periodStartLocalTicks);
            return period == null ? 0 : period.demonKingDamage;
        }

        public void StartBattle(int stageId, long periodStartLocalTicks, string initialCheckpoint)
        {
            if (stageId <= 0) throw new ArgumentOutOfRangeException(nameof(stageId));
            if (string.IsNullOrWhiteSpace(initialCheckpoint)) throw new ArgumentException("Initial checkpoint is required.", nameof(initialCheckpoint));
            WeeklyChallengeStageSave stage = GetStage(periodStartLocalTicks, stageId);
            if (string.IsNullOrEmpty(stage.activeBattleCheckpoint)) stage.attempts = checked(stage.attempts + 1);
            stage.activeBattleCheckpoint = initialCheckpoint;
            Persist();
        }

        public void SaveBattleCheckpoint(int stageId, long periodStartLocalTicks, string checkpoint)
        {
            WeeklyChallengeStageSave stage = GetStage(periodStartLocalTicks, stageId);
            stage.activeBattleCheckpoint = checkpoint ?? string.Empty;
            Persist();
        }

        public void CompleteBattle(int stageId, long periodStartLocalTicks, bool won, long playerDemonKingDamage = 0)
        {
            if (playerDemonKingDamage < 0) throw new ArgumentOutOfRangeException(nameof(playerDemonKingDamage));
            WeeklyChallengeStageSave stage = GetStage(periodStartLocalTicks, stageId);
            if (string.IsNullOrEmpty(stage.activeBattleCheckpoint)) return;
            if (won) stage.wins = checked(stage.wins + 1);
            stage.activeBattleCheckpoint = string.Empty;
            if (stageId == 4800)
            {
                WeeklyChallengePeriodSave period = save.periods.First(value => value.startLocalTicks == periodStartLocalTicks);
                period.demonKingDamage = checked(period.demonKingDamage + playerDemonKingDamage);
            }
            Persist();
        }

        private WeeklyChallengeStageSave GetStage(long periodStartLocalTicks, int stageId)
        {
            WeeklyChallengePeriodSave period = save.periods.FirstOrDefault(value => value.startLocalTicks == periodStartLocalTicks);
            if (period == null)
            {
                period = new WeeklyChallengePeriodSave { startLocalTicks = periodStartLocalTicks };
                save.periods.Add(period);
            }
            WeeklyChallengeStageSave stage = period.stages.FirstOrDefault(value => value.stageId == stageId);
            if (stage == null)
            {
                stage = new WeeklyChallengeStageSave { stageId = stageId };
                period.stages.Add(stage);
            }
            return stage;
        }

        private void Load()
        {
            if (!File.Exists(path))
            {
                save = new WeeklyChallengeSave();
                return;
            }
            save = JsonUtility.FromJson<WeeklyChallengeSave>(File.ReadAllText(path));
            if (save == null || save.schema != 1 || save.periods == null
                || save.periods.Any(value => value == null || value.startLocalTicks <= 0 || value.stages == null
                    || value.stages.Any(stage => stage == null || stage.stageId <= 0 || stage.attempts < 0 || stage.wins < 0)))
                throw new InvalidDataException("The weekly challenge save is corrupt.");
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
