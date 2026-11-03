using System;
using System.IO;
using UnityEngine;
using Zjcs.Offline.Progression;

namespace Zjcs.Editor
{
    public static class WeeklyChallengeSessionAcceptance
    {
        public static void Run()
        {
            string root = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Validation workspace is required.");
            string path = Path.Combine(root, "weekly-challenge-session-test.json");
            var session = new OfflineWeeklyChallengeSession(path);
            DateTime beforeOpen = new DateTime(2026, 11, 8, 7, 59, 0, DateTimeKind.Local);
            DateTime open = beforeOpen.AddMinutes(1);
            long previousPeriod = session.CurrentPeriodStartLocalTicks(beforeOpen);
            Require(new DateTime(previousPeriod, DateTimeKind.Local) == new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Local),
                "Sunday before 08:00 belongs to the previous weekly period.");
            long currentPeriod = session.CurrentPeriodStartLocalTicks(open);
            Require(currentPeriod == open.Ticks, "Sunday 08:00 begins the weekly period.");

            const string checkpoint = "weekly-checkpoint";
            session.StartBattle(4800, currentPeriod, checkpoint);
            session = new OfflineWeeklyChallengeSession(path);
            Require(session.TryResumeBattle(4800, out string restored, out long restoredPeriod)
                && restored == checkpoint && restoredPeriod == currentPeriod,
                "The active weekly battle survives save reload.");

            long nextPeriod = session.CurrentPeriodStartLocalTicks(open.AddDays(7));
            Require(nextPeriod > currentPeriod && session.TryResumeBattle(4800, out restored, out restoredPeriod)
                && restored == checkpoint && restoredPeriod == currentPeriod,
                "A period rollover preserves an already-entered battle checkpoint.");
            session.CompleteBattle(4800, currentPeriod, true, 1234);
            session.CompleteBattle(4800, currentPeriod, true, 999);
            session = new OfflineWeeklyChallengeSession(path);
            Require(!session.TryResumeBattle(4800, out _, out _), "A settled weekly battle no longer resumes.");
            Require(session.DemonKingDamage(currentPeriod) == 1234,
                "Only the first settlement adds personal demon king damage to its period.");
            session.StartBattle(4800, nextPeriod, "next-period-checkpoint");
            session.CompleteBattle(4800, nextPeriod, false, 500);
            session = new OfflineWeeklyChallengeSession(path);
            Require(session.DemonKingDamage(currentPeriod) == 1234 && session.DemonKingDamage(nextPeriod) == 500,
                "Demon king damage is cumulative within, and isolated between, weekly periods.");
            session.StartBattle(4700, nextPeriod, "unrelated-stage-checkpoint");
            session.CompleteBattle(4700, nextPeriod, false, 777);
            Require(session.DemonKingDamage(nextPeriod) == 500,
                "Other weekly stages do not add damage to the demon king total.");

            string legacyPath = Path.Combine(root, "weekly-challenge-legacy-save.json");
            File.WriteAllText(legacyPath, "{\"schema\":1,\"highestObservedLocalTicks\":0,\"periods\":[{\"startLocalTicks\":"
                + currentPeriod + ",\"stages\":[]}]}");
            var legacySession = new OfflineWeeklyChallengeSession(legacyPath);
            Require(legacySession.DemonKingDamage(currentPeriod) == 0,
                "A legacy period without a demon king damage field loads with a zero total.");
            string persisted = File.ReadAllText(path);
            Require(persisted.Contains("\"attempts\": 1") && persisted.Contains("\"wins\": 1"),
                "A resume does not count a second attempt and settlement records the win.");
            Debug.Log("[Weekly challenge session acceptance] passed.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Weekly challenge session acceptance failed: " + message);
        }
    }
}
