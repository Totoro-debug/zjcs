using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.TeamUI;

namespace Zjcs.Official.ActivityRankUI
{
    // Data/resource operations consumed by the original rank presenters.
    public sealed class ActivityRankBindings
    {
        public ITeamIconServices Icons { get; }
        public Func<long, IProfessionInfoTarget> PlayerTarget { get; }
        public Action<string, bool> OpenRule { get; }
        public int ShowPlayerCountLimit { get; }
        public bool CanSwitchRange { get; }
        public ActivityRankBindings(ITeamIconServices icons, Func<long, IProfessionInfoTarget> playerTarget,
            Action<string, bool> openRule, int showPlayerCountLimit, bool canSwitchRange)
        {
            Icons = icons ?? throw new ArgumentNullException(nameof(icons));
            PlayerTarget = playerTarget ?? throw new ArgumentNullException(nameof(playerTarget));
            OpenRule = openRule ?? throw new ArgumentNullException(nameof(openRule));
            if (showPlayerCountLimit <= 0) throw new ArgumentOutOfRangeException(nameof(showPlayerCountLimit));
            ShowPlayerCountLimit = showPlayerCountLimit;
            CanSwitchRange = canSwitchRange;
        }
    }

    // Rank-key fields consumed by ActivityDomainBossRankPanel (transport removed).
    public sealed class ActivityDomainBossRankKey
    {
        public readonly int ActivityId;
        public TopRangeType RangeType;
        public ActivityDomainBossRankKey(int activityId, TopRangeType rangeType)
        { ActivityId = activityId; RangeType = rangeType; }
    }

    public struct ActivityRankLevel { public int Level, SubRank; }
    public abstract class ActivityRankNpcTarget : IProfessionInfoNpcTarget
    {
        public abstract int NpcClassId { get; }
        public abstract long TargetId { get; }
        public ActorTargetType TargetType => ActorTargetType.Npc;
        public long ServerId => -1;
        public bool IsNpc => true;
        public int NpcLevel, NpcSubRank;
        public abstract string GetFigurePath(Sex sex);
        public abstract bool CheckActorCanAssist(int playType);
    }
}
