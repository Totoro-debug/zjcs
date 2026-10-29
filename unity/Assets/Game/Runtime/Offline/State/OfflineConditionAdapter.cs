using System;
using System.Collections.Generic;

namespace Zjcs.Offline.State
{
    public static class OfflineConditionAdapter
    {
        public static bool Evaluate(IReadOnlyDictionary<string, object> condition, Func<bool> evaluateOfficialCondition)
        {
            bool? openingPolicy = OfflineOpeningPolicy.Evaluate(condition);
            if (openingPolicy.HasValue)
                return openingPolicy.Value;

            if (evaluateOfficialCondition == null)
                throw new ArgumentNullException(nameof(evaluateOfficialCondition));
            return evaluateOfficialCondition();
        }
    }
}
