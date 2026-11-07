using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static class FightGridItemRuntime
    {
        // User-confirmed offline inheritance: independent final-property snapshot,
        // without the ordinary summoned-monster coefficient or a fabricated HP pool.
        public static FightBattleGridItemState Create(int id, int classId,
            FightBattleUnitState creator, FightBattleUnitState reference,
            FightGridPoint position, float creationDelay)
        {
            if (creator == null || reference == null) throw new ArgumentNullException();
            FightBattleGridItemDefinition definition = FightOfficialGridItemRules.Get(classId);
            return new FightBattleGridItemState
            {
                Id = id, GridItemClassId = classId, EntityClassId = definition.EntityClassId,
                OwnerId = creator.Id, ReferenceUnitId = reference.Id,
                X = position.X, Y = position.Y, CreationDelay = Math.Max(0f, creationDelay), HasRuntime = true,
                Runtime = new FightBattleUnitState
                {
                    Id = id, MasterId = creator.Id, RoleEntityId = definition.EntityClassId,
                    CharacterType = 6, Targetable = false, Side = reference.Side,
                    Level = reference.Level, SubRank = reference.SubRank,
                    X = position.X, Y = position.Y, Direction = reference.Direction,
                    MoveMask = definition.MoveMask,
                    BaseProperties = Copy(reference.Properties), Properties = Copy(reference.Properties)
                }
            };
        }

        public static void End(FightBattleGridItemState item)
        {
            if (item == null || item.Ended) return;
            item.Ended = true;
            if (!item.HasRuntime || item.Runtime == null) return;
            foreach (var status in new List<FightBattleStatusState>(item.Runtime.Statuses))
                FightStatusLifecycle.End(item.Runtime, status);
            item.Runtime.Ended = true;
            item.ViewRemoved?.Invoke(item);
        }

        private static List<FightPropertyValue> Copy(IList<FightPropertyValue> source)
        {
            var result = new List<FightPropertyValue>();
            if (source != null)
                foreach (var value in source)
                    if (value != null) result.Add(new FightPropertyValue {Id = value.Id, Value = value.Value});
            return result;
        }
    }
}
