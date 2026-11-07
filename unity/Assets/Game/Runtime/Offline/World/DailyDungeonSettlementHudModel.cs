using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.World
{
    public sealed class DailyDungeonSettlementRewardCandidate
    {
        public int ItemId { get; internal set; }
        public int Index { get; internal set; }
        public long Count { get; internal set; }
        public float Probability { get; internal set; }
    }

    public sealed class DailyDungeonSettlementRewardRule
    {
        public int RuleId { get; internal set; }
        public float DoubleProbability { get; internal set; }
        public IReadOnlyList<DailyDungeonSettlementRewardCandidate> Candidates { get; internal set; }
    }

    public sealed class DailyDungeonSettlementHudModel
    {
        public string ClaimId { get; internal set; }
        public DailyDungeonLevelDefinition Level { get; internal set; }
        public DailyDungeonRunSave Run { get; internal set; }
        public int AwardTimes { get; internal set; }
        public int MaxAwardTimesPerClaim { get; internal set; }
        public int RemainingClaims { get; internal set; }
        public int PurchaseRemaining { get; internal set; }
        public bool IsPrivateRoute { get; internal set; }
        public bool CanClaim { get; internal set; }
        public bool CanPurchaseEntries { get; internal set; }
        public IReadOnlyList<DailyDungeonSettlementRewardRule> RewardRules { get; internal set; }
    }

    public static class DailyDungeonSettlementHudModelBuilder
    {
        public static DailyDungeonSettlementHudModel Build(DailyDungeonModule module,
            DailyDungeonPendingClaimSave pending, int requestedAwardTimes, int playerLevel, int subRank)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (pending?.run == null) return null;

            DailyDungeonRunSave run = pending.run;
            DailyDungeonLevelDefinition level = module.ResolveLevel(run.dungeonId, run.difficulty);
            DailyDungeonEntryAllowanceState allowance = module.EntryAllowance();
            int awardTimes = pending.awardTimes > 0 ? pending.awardTimes : requestedAwardTimes;
            int remaining = module.RemainEntries;
            bool privateRoute = level == null || level.IsPrivateRoute;
            int[] rewardRuleIds = privateRoute ? Array.Empty<int>()
                : module.ConfiguredRewardRuleIds(run, completed: run.grade > 0);
            var rewardRules = new List<DailyDungeonSettlementRewardRule>();
            foreach (int rewardRuleId in rewardRuleIds ?? Array.Empty<int>())
            {
                DailyDungeonRuntimeReward rule = module.Catalog.rewardRules?.FirstOrDefault(value =>
                    value != null && value.Id == rewardRuleId);
                if (rule == null || rule.IdList == null || rule.WeightList == null || rule.CountList == null
                    || rule.IdList.Length != rule.WeightList.Length || rule.IdList.Length != rule.CountList.Length)
                    continue;

                var candidates = new List<DailyDungeonSettlementRewardCandidate>();
                for (int index = 0; index < rule.IdList.Length; index++)
                {
                    if (rule.IdList[index] <= 0 || rule.CountList[index] <= 0f) continue;
                    float count = rule.CountList[index];
                    if (rule.LevelCountId > 0)
                        count *= module.LevelNumber(rule.LevelCountId, playerLevel);
                    if (rule.SubRankCountId != null && index < rule.SubRankCountId.Length
                        && rule.SubRankCountId[index] > 0)
                        count *= module.LevelNumber(rule.SubRankCountId[index], subRank);
                    long amount = Math.Max(0L, (long)count);
                    if (amount <= 0) continue;
                    candidates.Add(new DailyDungeonSettlementRewardCandidate
                    {
                        ItemId = rule.IdList[index],
                        Index = index,
                        Count = amount,
                        Probability = module.RewardProbability(rewardRuleId, index)
                    });
                }
                rewardRules.Add(new DailyDungeonSettlementRewardRule
                {
                    RuleId = rewardRuleId,
                    DoubleProbability = module.GradeRewardDoubleProbability(run, rewardRuleId),
                    Candidates = candidates.AsReadOnly()
                });
            }

            return new DailyDungeonSettlementHudModel
            {
                ClaimId = pending.id,
                Level = level,
                Run = run,
                AwardTimes = Math.Max(0, awardTimes),
                MaxAwardTimesPerClaim = module.MaxAwardTimesPerClaim,
                RemainingClaims = remaining,
                PurchaseRemaining = allowance.PurchaseRemaining,
                IsPrivateRoute = privateRoute,
                CanClaim = !privateRoute && level != null && run.grade > 0 && awardTimes > 0
                    && awardTimes <= module.MaxAwardTimesPerClaim && awardTimes <= remaining,
                CanPurchaseEntries = !privateRoute && allowance.PurchaseRemaining > 0
                    && module.ClaimCapacity > 0,
                RewardRules = rewardRules.AsReadOnly()
            };
        }
    }
}
