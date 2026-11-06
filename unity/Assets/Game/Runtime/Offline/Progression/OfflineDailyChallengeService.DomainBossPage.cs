using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    // A detached projection of one saved period/day. UI callers cannot mutate
    // claimed tiers or accidentally combine counters from different refreshes.
    public sealed class OfflineDomainBossPageProgress
    {
        public DateTime PeriodStart { get; internal set; }
        public DateTime PeriodEnd { get; internal set; }
        public DateTime NextLogicDayReset { get; internal set; }
        public bool CanChallenge { get; internal set; }
        public string ChallengeUnavailableReason { get; internal set; }
        public int ChallengeCount { get; internal set; }
        public int DailyAwardCount { get; internal set; }
        public int Difficulty { get; internal set; }
        public IReadOnlyCollection<int> AwardedDifficulties { get; internal set; }
    }

    public sealed partial class OfflineDailyChallengeService
    {
        public OfflineDomainBossPageProgress ReadDomainBossPage(int activityId,
            OfficialDailyChallengeCatalog catalog, DateTime localNow)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (activityId == FunnyBossActivityId || catalog.Difficulties(activityId).Count == 0)
                throw new ArgumentOutOfRangeException(nameof(activityId));
            int duration = catalog.DurationHours(activityId);
            EnsureEntered(localNow);
            localNow = AdvanceLocalClock(localNow, duration);
            // CanParticipate deliberately checks settlement windows before it
            // resets counters. A page still needs current counters while closed.
            bool canChallenge = CanParticipate(activityId, localNow, duration,
                catalog.DailyLimit(activityId), out string reason);
            DailyChallengeActivitySave activity = GetActivity(activityId);
            ResetPeriod(activity, localNow, duration);
            ResetLogicDay(activity, localNow);
            DateTime start = GetPeriodStartLocal(localNow, duration);
            var result = new OfflineDomainBossPageProgress
            {
                PeriodStart = start,
                PeriodEnd = start.AddHours(duration),
                NextLogicDayReset = GetLogicDayStartLocal(localNow).AddDays(1),
                CanChallenge = canChallenge,
                ChallengeUnavailableReason = reason,
                ChallengeCount = activity.participationCount > 0 ? activity.participationCount
                    : activity.participated ? 1 : 0,
                DailyAwardCount = activity.dailyChallengeAwardCount,
                Difficulty = activity.highestDifficulty,
                AwardedDifficulties = Array.AsReadOnly(activity.claimedBossDifficulties.ToArray())
            };
            Persist();
            return result;
        }
    }
}
