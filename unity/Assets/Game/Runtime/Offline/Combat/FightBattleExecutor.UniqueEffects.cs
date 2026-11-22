using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        internal bool TryGetUniquePropertyEffect(int classId, out int propertyId)
        {
            propertyId = 0;
            // The original component marks a single converted property as unique.
            // Compare its resolved value, including the caster snapshot and cultivation.
            if (!entities.TryGetValue(classId, out var components)) return false;
            var properties = Component(components, "FightStatusPropComponent");
            if (properties == null || !Bool(properties, "MutexByBetter")) return false;
            var transitions = FieldArray(properties, "TransitionPropCfgs");
            if (transitions.Count != 1) return false;
            propertyId = Int(Object(transitions[0], "TransitionPropCfgs"), "Prop");
            return propertyId > 0;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private bool RejectUniquePropertyEffect(FightBattleUnitState owner, FightBattleStatusState incoming)
        {
            if (statusCatalog == null
                || !statusCatalog.TryGetUniquePropertyEffect(incoming.ClassId, out int propertyId)) return false;
            FightStatusPropertyRuntime.ResolveForApplication(incoming, owner, units, statusCatalog, formula);
            long incomingValue = UniquePropertyValue(incoming, propertyId);
            var previous = new List<FightBattleStatusState>();
            foreach (var status in owner.Statuses)
            {
                if (status == null || status.ClassId != incoming.ClassId) continue;
                FightStatusPropertyRuntime.ResolveForApplication(status, owner, units, statusCatalog, formula);
                // A weaker application neither refreshes the winner nor remains hidden
                // to return later. Equal strength follows the confirmed new-source refresh.
                if (UniquePropertyValue(status, propertyId) > incomingValue) return true;
                previous.Add(status);
            }
            foreach (var status in previous) FightStatusLifecycle.End(owner, status);
            return false;
        }

        private static long UniquePropertyValue(FightBattleStatusState status, int propertyId)
        {
            foreach (var value in status.ResolvedProperties ?? new List<FightPropertyValue>())
                if (value != null && value.Id == propertyId) return value.Value;
            return 0;
        }
    }
}
