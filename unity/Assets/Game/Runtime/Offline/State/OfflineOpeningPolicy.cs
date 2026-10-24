using System.Collections.Generic;

namespace Zjcs.Offline.State
{
    public static class OfflineOpeningPolicy
    {
        public static bool? Evaluate(IReadOnlyDictionary<string, object> condition)
        {
            if (condition == null || !condition.TryGetValue("_type", out object rawType))
                return null;

            string type = rawType as string;
            switch (type)
            {
                case "ServerRankUnlockDaysCondition":
                case "ServerSeasonUnlockDaysCondition":
                case "PlayerRankAtServerRankCondition":
                case "ServerDaysCondition":
                case "LogicDomainServerDaysCondition":
                case "ServerStartTimeCondition":
                case "RegisteredDaysCondition":
                    return true;
                case "NpcAdventureUnlockCondition":
                    return !condition.TryGetValue("NeedUnlock", out object needUnlock)
                        || !(needUnlock is bool required)
                        || required;
                default:
                    return null;
            }
        }
    }
}
