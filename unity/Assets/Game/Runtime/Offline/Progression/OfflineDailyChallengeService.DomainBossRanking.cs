using System;

namespace Zjcs.Offline.Progression
{
    public sealed partial class OfflineDailyChallengeService
    {
        // Approved offline rank: only the current character's real completed results this period.
        // MaxDamageRecord is the original per-player field; zero damage still means participated.
        public void RecordDomainBossResult(int activityId, DateTime localNow, int durationHours,
            long actualDamage)
        {
            ValidateDomainBossRanking(activityId, durationHours);
            if (actualDamage < 0) throw new ArgumentOutOfRangeException(nameof(actualDamage));
            EnsureEntered(localNow);
            var activity = GetActivity(activityId);
            ResetPeriod(activity, localNow, durationHours);
            activity.hasDomainBossResult = true;
            activity.domainBossMaxDamage = Math.Max(activity.domainBossMaxDamage, actualDamage);
            Persist();
        }

        public long? DomainBossMaxDamage(int activityId, DateTime localNow, int durationHours)
        {
            ValidateDomainBossRanking(activityId, durationHours);
            if (save.firstEnteredLocalTicks == 0) return null;
            var activity = GetActivity(activityId);
            ResetPeriod(activity, localNow, durationHours);
            Persist();
            return activity.hasDomainBossResult ? (long?)activity.domainBossMaxDamage : null;
        }

        private static void ValidateDomainBossRanking(int activityId, int durationHours)
        {
            if (activityId != 209 && activityId != 212 && activityId != 226)
                throw new ArgumentOutOfRangeException(nameof(activityId));
            if (durationHours <= 1) throw new ArgumentOutOfRangeException(nameof(durationHours));
        }
    }
}
