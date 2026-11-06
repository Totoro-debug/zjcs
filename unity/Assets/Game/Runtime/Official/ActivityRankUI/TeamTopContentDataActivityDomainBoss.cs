using System;
using System.Collections.Generic;

namespace Zjcs.Official.ActivityRankUI
{
    // Original TeamTopContentDataActivityDomainBoss projection. The transport seam
    // consumes only a completed local period score, as approved for offline play.
    public sealed class TeamTopContentDataActivityDomainBoss : TeamTopContentDataBase
    {
        private Func<int, long?> readCompletedScore;
        private Func<long> playerId;
        private Func<long, string> formatScore;
        private long? score;
        private long rankedPlayerId;

        public void Configure(Func<int, long?> readCompletedScore, Func<long> playerId, Func<long, string> formatScore)
        {
            this.readCompletedScore = readCompletedScore ?? throw new ArgumentNullException(nameof(readCompletedScore));
            this.playerId = playerId ?? throw new ArgumentNullException(nameof(playerId));
            this.formatScore = formatScore ?? throw new ArgumentNullException(nameof(formatScore));
        }

        public override void InitData(ActivityDomainBossRankKey topKey, Action action)
        {
            score = readCompletedScore(topKey.ActivityId);
            rankedPlayerId = playerId();
            if (rankedPlayerId <= 0 || score < 0) throw new InvalidOperationException("Invalid local completed rank result.");
            action?.Invoke();
        }

        public override TeamTopContentData GetDataByIndex(int index)
        {
            if (index != 0 || !score.HasValue) throw new ArgumentOutOfRangeException(nameof(index));
            return Row(false);
        }

        public override TeamTopContentData GetMySelfData() => score.HasValue ? Row(true) : new TeamTopContentData { Rank = -1 };
        private TeamTopContentData Row(bool myself) => new TeamTopContentData {
            Rank = 0, Score = formatScore(score.Value), IsMySelf = myself,
            PlayerIds = new List<long> { rankedPlayerId }, NpcTargetDatas = new List<ActivityRankNpcTarget>()
        };
        public override int GetShowCount() => score.HasValue ? 1 : 0;
        public override string GetNotInRankTipKey() => "SecretAreeRankPanel_no_join";
        public override string GetFirstRankTipKey() => "SecretAreeRankPanel_first_rank";
        public override string GetEmptyResultTipKey() => "SecretAreeRankPanel_wait_desc";
    }
}
