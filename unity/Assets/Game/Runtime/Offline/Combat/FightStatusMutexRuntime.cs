using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed class FightStatusMutexGroup
    {
        // Common.EStatusMutexGroupMode: Weight = 0, Order = 1.
        public int Mode;
        public List<int> StatusIds = new List<int>();
    }

    // FightStatusMutexForbiddenGroup, partitioned by the confirmed source identity rule.
    public sealed class FightStatusMutexRuntime
    {
        private sealed class Group
        {
            public FightStatusMutexGroup Definition;
            public FightBattleStatusOrigin Origin;
            public readonly List<FightBattleStatusState> Statuses = new List<FightBattleStatusState>();
        }

        private readonly Dictionary<int, FightStatusMutexGroup> definitions =
            new Dictionary<int, FightStatusMutexGroup>();
        private readonly Dictionary<FightBattleUnitState, List<Group>> holders =
            new Dictionary<FightBattleUnitState, List<Group>>();

        public FightStatusMutexRuntime(IReadOnlyList<FightStatusMutexGroup> groups)
        {
            if (groups == null) return;
            foreach (var group in groups)
                foreach (int id in group.StatusIds) definitions[id] = group;
        }

        // The status list is the saved application order. Restore registrations without
        // replaying status starts, and reconcile changes made between executor steps.
        public bool Synchronize(FightBattleUnitState owner)
        {
            if (owner == null || owner.Statuses == null) return false;
            var previous = new Dictionary<FightBattleStatusState, int>();
            foreach (var status in owner.Statuses)
                if (status != null) previous[status] = status.ForbiddenType;
            if (holders.TryGetValue(owner, out var groups))
                foreach (var group in new List<Group>(groups))
                    foreach (var status in new List<FightBattleStatusState>(group.Statuses))
                        if (!owner.Statuses.Contains(status)) Remove(owner, status);
            foreach (var status in new List<FightBattleStatusState>(owner.Statuses)) Add(owner, status);
            foreach (var pair in previous)
                if (pair.Key.ForbiddenType != pair.Value) return true;
            return false;
        }

        public void Add(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (owner == null || status == null || !definitions.TryGetValue(status.ClassId, out var definition)) return;
            if (!holders.TryGetValue(owner, out var groups))
                holders[owner] = groups = new List<Group>();
            var origin = FightBattleStatusOrigin.Resolve(status);
            Group selected = null;
            foreach (var group in groups)
            {
                if (group.Statuses.Contains(status)) return;
                if (ReferenceEquals(group.Definition, definition)
                    && FightBattleStatusOrigin.Matches(group.Origin, origin)) selected = group;
            }
            if (selected == null)
            {
                selected = new Group { Definition = definition, Origin = origin.Copy() };
                groups.Add(selected);
            }
            selected.Statuses.Add(status);
            // The original does not clear a retained Mutex reason for a first registration.
            if (selected.Statuses.Count != 1) Update(owner, selected);
        }

        public void Remove(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (owner == null || !holders.TryGetValue(owner, out var groups)) return;
            foreach (var group in new List<Group>(groups))
                if (group.Statuses.Remove(status))
                {
                    if (group.Statuses.Count == 0) groups.Remove(group);
                    else Update(owner, group);
                    return;
                }
        }

        private static void Update(FightBattleUnitState owner, Group group)
        {
            var statuses = group.Statuses;
            if (statuses.Count == 1)
                FightStatusLifecycle.SetForbidden(owner, statuses[0], FightStatusLifecycle.ForbiddenMutex, false);
            else if (group.Definition.Mode == 1)
            {
                FightStatusLifecycle.SetForbidden(owner, statuses[0], FightStatusLifecycle.ForbiddenMutex, false);
                for (int i = 1, count = statuses.Count; i < count; i++)
                    FightStatusLifecycle.SetForbidden(owner, statuses[i], FightStatusLifecycle.ForbiddenMutex, true);
            }
            else
            {
                var winner = statuses[0];
                int weight = group.Definition.StatusIds.IndexOf(winner.ClassId);
                for (int i = 1, count = statuses.Count; i < count; i++)
                {
                    var candidate = statuses[i];
                    int nextWeight = group.Definition.StatusIds.IndexOf(candidate.ClassId);
                    if (nextWeight >= weight)
                    {
                        FightStatusLifecycle.SetForbidden(owner, winner, FightStatusLifecycle.ForbiddenMutex, true);
                        winner = candidate;
                        weight = nextWeight;
                        FightStatusLifecycle.SetForbidden(owner, winner, FightStatusLifecycle.ForbiddenMutex, false);
                    }
                    else FightStatusLifecycle.SetForbidden(owner, candidate, FightStatusLifecycle.ForbiddenMutex, true);
                }
            }
        }
    }
}
