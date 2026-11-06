using System;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Selected original ActivityNodeInstanceInfo.
    public sealed class ActivityNodeInstanceInfo : BaseNodeInstanceInfo
    {
        public int ActivityId;
        public ActivityNodeInstanceInfo(int activityId) => ActivityId = activityId;
        public override string GetInstanceDesc() => $"ActivityId:{ActivityId}";
        public override bool Equals(object value)
            => value is ActivityNodeInstanceInfo activity && ActivityId == activity.ActivityId;
        public override int GetHashCode() => HashCode.Combine(ActivityId);
    }
}
