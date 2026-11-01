using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightStatusReplacementValue
    {
        public int PropertyId;
        public long RawValue;
        public bool IsFloat;
    }

    [Serializable]
    public sealed class FightGameplayTagClause
    {
        public int Type;
        public long[] Tags = new long[4];

        // GameplayTagQueryExtension.IsMatch and GameplayTagExtension's four-word tests.
        public bool Matches(long[] tags)
        {
            bool all = true, any = false;
            for (int i = 0; i < 4; i++)
            {
                long mask = Tags[i];
                long overlap = (tags == null ? 0 : tags[i]) & mask;
                all &= overlap == mask;
                any |= overlap != 0;
            }
            switch (Type)
            {
                case 0: return all;
                case 1: return any;
                case 2: return !any;
                default: throw new NotSupportedException("Original gameplay tag query type: " + Type);
            }
        }
    }

    [Serializable]
    public sealed class FightBattleStatusReplayState
    {
        public float Rate = 1f;
        public float AddRateWhenNotHit;
        public int MaxCountEachRound = -1;
        public bool IsCheckSkillLimitedTimes = true;
        public List<int> FilterTypes = new List<int>();
        public List<int> TriggerInGameStatusList = new List<int>();
        public List<FightGameplayTagClause> TagQuery = new List<FightGameplayTagClause>();
        public float ExtraRate;
        public int CountRound = -1;
        public int CountEachRound;

        public FightBattleStatusReplayState Copy()
        {
            var copy = (FightBattleStatusReplayState)MemberwiseClone();
            copy.FilterTypes = new List<int>(FilterTypes);
            copy.TriggerInGameStatusList = new List<int>(TriggerInGameStatusList);
            copy.TagQuery = new List<FightGameplayTagClause>();
            foreach (var clause in TagQuery)
                copy.TagQuery.Add(new FightGameplayTagClause { Type = clause.Type, Tags = (long[])clause.Tags.Clone() });
            return copy;
        }
    }
}
