using System;
using System.IO;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using UnityEngine;

namespace Zjcs.Editor
{
    public static class NpcAssistanceDailyResetAcceptance
    {
        private const int PlayType = 10;
        private const int NpcClassId = 8402;
        private const int StageId = 1;
        private const string EntryId = "npc-assistance-reset-acceptance";

        public static void Run()
        {
            string validationRoot = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(validationRoot))
                throw new InvalidOperationException("Set ZJCS_VALIDATION_DIR to an owned validation workspace.");
            string profileRoot = Path.Combine(Path.GetFullPath(validationRoot),
                "npc-assistance-reset-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(profileRoot);
            var slots = new ProfileSlotCatalog(profileRoot);
            string slotId = slots.CreateSlot();
            OfflineNpcAssistanceProgress progress = OfflineNpcAssistanceProgress.Open(slots, slotId);
            Require(progress.SetSelection(PlayType, EntryId, new[] { NpcClassId }, 3),
                "Could not prepare the daily assistance selection.");

            long beforeReset = Local(2026, 11, 5, 23, 59, 59);
            long reset = Local(2026, 11, 6, 6, 0, 0);
            long lastSecondBeforeNextReset = Local(2026, 11, 6, 23, 59, 59);
            long nextReset = Local(2026, 11, 7, 6, 0, 0);
            CommitAndFinish(progress, beforeReset);
            Require(progress.AssistCount(PlayType, NpcClassId, NpcAssistRefreshCycle.Daily, beforeReset) == 1,
                "The assistance count was not available before the local 06:00 reset.");
            Require(progress.AssistCount(PlayType, NpcClassId, NpcAssistRefreshCycle.Daily, reset) == 0,
                "The assistance count did not reset at local 06:00.");

            CommitAndFinish(progress, reset);
            Require(progress.AssistCount(PlayType, NpcClassId, NpcAssistRefreshCycle.Daily, lastSecondBeforeNextReset) == 1,
                "The count created at the reset boundary was not retained for that daily window.");
            Require(progress.AssistCount(PlayType, NpcClassId, NpcAssistRefreshCycle.Daily, nextReset) == 0,
                "The following local 06:00 reset was not applied.");

            VerifyVersionOneMigration(slots);
            Debug.Log("NPC assistance daily reset acceptance passed: local 06:00 boundaries and schema migration.");
        }

        private static void VerifyVersionOneMigration(ProfileSlotCatalog slots)
        {
            string legacySlot = slots.CreateSlot();
            var legacy = new OfflineNpcAssistanceSave { schema = 1 };
            legacy.counts.Add(new OfflineNpcAssistCount
            {
                playType = PlayType,
                npcClassId = NpcClassId,
                count = 1,
                refreshTimeUnixSeconds = LocalMidnight(2026, 11, 5)
            });
            File.WriteAllText(Path.Combine(slots.GetSlotDirectory(legacySlot), "npc-assistance-save.json"),
                JsonUtility.ToJson(legacy, true));

            OfflineNpcAssistanceProgress migrated = OfflineNpcAssistanceProgress.Open(slots, legacySlot);
            Require(migrated.AssistCount(PlayType, NpcClassId, NpcAssistRefreshCycle.Daily,
                Local(2026, 11, 5, 12, 0, 0)) == 1,
                "The version 1 stored local date was not retained through its current daily window.");
            Require(migrated.AssistCount(PlayType, NpcClassId, NpcAssistRefreshCycle.Daily,
                Local(2026, 11, 6, 6, 0, 0)) == 0,
                "The migrated version 1 count did not expire at the next official reset.");
            string migratedJson = File.ReadAllText(Path.Combine(slots.GetSlotDirectory(legacySlot),
                "npc-assistance-save.json"));
            Require(migratedJson.Contains("\"schema\": 2"),
                "The version 1 assistance save was not rewritten as schema 2.");
        }

        private static void CommitAndFinish(OfflineNpcAssistanceProgress progress, long nowUnixSeconds)
        {
            progress.CommitBattle(PlayType, EntryId, StageId, 3, 1, -1,
                NpcAssistRefreshCycle.Daily, nowUnixSeconds);
            progress.FinishBattle(PlayType, EntryId, StageId);
        }

        private static long Local(int year, int month, int day, int hour, int minute, int second)
        {
            return new DateTimeOffset(new DateTime(year, month, day, hour, minute, second,
                DateTimeKind.Local)).ToUnixTimeSeconds();
        }

        private static long LocalMidnight(int year, int month, int day)
        {
            return new DateTimeOffset(new DateTime(year, month, day, 0, 0, 0,
                DateTimeKind.Local)).ToUnixTimeSeconds();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
