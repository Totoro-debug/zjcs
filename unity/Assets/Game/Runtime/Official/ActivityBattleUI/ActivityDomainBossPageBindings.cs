using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Display inputs consumed by the original page. The local activity module
    // owns the period, participation, battle checkpoint and reward transactions.
    public sealed class ActivityDomainBossPageState
    {
        public ActivityDisplayData Activity;
        public int ActivityId, Difficulty, MonsterId;
        public int DailyChallengeCount, MaxDailyChallengeCount, DailyAwardCount, MaxDailyAwardCount;
        public bool CanChallenge, DifficultyLimited;
        public DateTime NextLogicDayResetTime;
        public float HpRatio;
        public IReadOnlyList<int> Difficulties;
        public ISet<int> AwardedDifficulties;
        public IReadOnlyDictionary<int, long> DailyAwards;
        public IReadOnlyDictionary<int, long> KillAwards;
    }

    public interface IActivityDomainBossPageBindings
    {
        event Action<int> Changed;
        ActivityDomainBossPageState Read(int activityId, int selectedDifficulty);
        IActivityBattleTeam Team(int activityId);
        void BindTipDot(TipDotNodeLinker linker, int activityId);
        ItemIcon GetRewardIcon(int itemId, Transform slot, UnityEngine.Object owner);
        void RecycleRewardIcons(UnityEngine.Object owner);
        void Challenge(int activityId);
        void ReceiveKilledAward(int activityId, int difficulty);
        void Navigate(string key);
    }
}
