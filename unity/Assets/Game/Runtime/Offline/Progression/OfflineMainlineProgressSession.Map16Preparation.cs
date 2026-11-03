using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineMapAlonePreparationState
    {
        public int RequiredRank;
        public bool RankReached;
        public bool CountdownStarted;
        public bool Unlocked;
        public long UnlockAtUnixSeconds;
        public long RemainingSeconds;
    }

    public sealed partial class OfflineMainlineProgressSession
    {
        private const int Map16PreparationRank = 4;

        public OfflineMapAlonePreparationState GetMap16PreparationState(long nowUnixSeconds = 0)
        {
            Reload();
            long now = nowUnixSeconds > 0 ? nowUnixSeconds : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            RefreshMap16Preparation(now);
            Reload();
            OfflineWorldAlonePreparationConfig config = LoadMap16PreparationConfig();
            OfflineWorldAloneStatus status = Save.World.AloneWorldStatuses?
                .FirstOrDefault(value => value.MapClassId == config.mapClassId);
            long unlockAt = status?.PreparationStartedAtUnixSeconds > 0
                ? Map16PreparationUnlockAt(Save.Player, config.preparationRule,
                    status.PreparationStartedAtUnixSeconds)
                : 0;
            return new OfflineMapAlonePreparationState
            {
                RequiredRank = config.preparationRule.rank,
                RankReached = Save.Player.Rank >= config.preparationRule.rank,
                CountdownStarted = status?.PreparationStartedAtUnixSeconds > 0,
                Unlocked = status?.Status == 1,
                UnlockAtUnixSeconds = unlockAt,
                RemainingSeconds = unlockAt <= 0 || now >= unlockAt ? 0 : unlockAt - now
            };
        }

        public ProgressionResult StartMap16Preparation(long nowUnixSeconds = 0)
        {
            Reload();
            long now = nowUnixSeconds > 0 ? nowUnixSeconds : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            OfflineWorldAlonePreparationConfig config = LoadMap16PreparationConfig();
            if (Save.Player.Rank < config.preparationRule.rank)
                return Result(ProgressionResultStatus.NotReady,
                    message: "Reach Silver rank before starting the Map16 unlock countdown.");
            OfflineWorldAloneStatus current = Save.World.AloneWorldStatuses?
                .FirstOrDefault(value => value.MapClassId == config.mapClassId);
            if (current?.Status == 1 || current?.PreparationStartedAtUnixSeconds > 0)
                return Result(ProgressionResultStatus.Unchanged);

            OfflineMainlineSave next = Clone(Save);
            if (next.World.AloneWorldStatuses == null)
                next.World.AloneWorldStatuses = new System.Collections.Generic.List<OfflineWorldAloneStatus>();
            OfflineWorldAloneStatus status = next.World.AloneWorldStatuses
                .FirstOrDefault(value => value.MapClassId == config.mapClassId);
            if (status == null)
            {
                status = new OfflineWorldAloneStatus { MapClassId = config.mapClassId };
                next.World.AloneWorldStatuses.Add(status);
            }
            status.Status = 0;
            if (status.AppearTimeUnixSeconds <= 0) status.AppearTimeUnixSeconds = now;
            status.PreparationStartedAtUnixSeconds = now;
            return Commit(next, Result(ProgressionResultStatus.Applied), now);
        }

        public ProgressionResult RefreshMap16Preparation(long nowUnixSeconds = 0)
        {
            Reload();
            long now = nowUnixSeconds > 0 ? nowUnixSeconds : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            OfflineMainlineSave next = Clone(Save);
            bool changed = RefreshMap16Preparation(next, now);
            return changed ? Commit(next, Result(ProgressionResultStatus.Applied), now)
                : Result(ProgressionResultStatus.Unchanged);
        }

        private static bool RefreshMap16Preparation(OfflineMainlineSave save, long nowUnixSeconds)
        {
            OfflineWorldAlonePreparationConfig config = LoadMap16PreparationConfig();
            if (save.Player.Rank < config.preparationRule.rank || save.World.AloneWorldStatuses == null)
                return false;
            OfflineWorldAloneStatus status = save.World.AloneWorldStatuses
                .FirstOrDefault(value => value.MapClassId == config.mapClassId);
            if (status == null || status.Status != 0 || status.PreparationStartedAtUnixSeconds <= 0)
                return false;
            long unlockAt = Map16PreparationUnlockAt(save.Player, config.preparationRule,
                status.PreparationStartedAtUnixSeconds);
            if (nowUnixSeconds < unlockAt) return false;
            status.Status = 1;
            return true;
        }

        private static long Map16PreparationUnlockAt(OfflinePlayerProgress player,
            OfflineWorldAlonePreparationRule rule, long preparationStartedAt)
        {
            long unlockAt = checked(preparationStartedAt + rule.minDurationS);
            if (player.Rank == rule.rank)
            {
                long reachedAt = player.RankUnlockTimes?.FirstOrDefault(value => value.Rank == rule.rank)?.UnixSeconds ?? 0;
                if (reachedAt > 0)
                    unlockAt = Math.Max(unlockAt, checked(reachedAt + (long)rule.dayOffset * 24 * 60 * 60));
            }
            return unlockAt;
        }

        private static OfflineWorldAlonePreparationConfig LoadMap16PreparationConfig()
        {
            TextAsset source = Resources.Load<TextAsset>("Map16/world-alone-configuration");
            OfflineWorldAlonePreparationConfig config = source == null ? null
                : JsonUtility.FromJson<OfflineWorldAlonePreparationConfig>(source.text);
            if (config == null || config.mapClassId != 16 || config.preparationRule == null
                || config.preparationRule.rank != Map16PreparationRank || config.preparationRule.dayOffset != 14
                || config.preparationRule.minDurationS != 300)
                throw new InvalidDataException("Official Map16 preparation configuration is missing or invalid.");
            return config;
        }
    }
}
