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
            session.CompleteBattle(4800, currentPeriod, true);
            session = new OfflineWeeklyChallengeSession(path);
            Require(!session.TryResumeBattle(4800, out _, out _), "A settled weekly battle no longer resumes.");
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
