using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private sealed class ObservedUnitLife
        {
            public bool Dead;
            public long Hp, MaxHp;
        }
        private sealed class PendingUnitLife
        {
            public FightBattleUnitLifeEvent Event;
            public FightBattleSkillCastEvent Cast;
        }
        private Dictionary<int, ObservedUnitLife> observedUnitLives;
        private List<PendingUnitLife> unitLifeEvents;
        private float? unitLifeTimeOverride;

        private void BeginUnitLifeEvents()
        {
            observedUnitLives = new Dictionary<int, ObservedUnitLife>();
            unitLifeEvents = new List<PendingUnitLife>();
            foreach (var unit in units) RegisterUnitLife(unit);
        }

        private void RegisterUnitLife(FightBattleUnitState unit)
        {
            if (observedUnitLives == null || unit == null) return;
            observedUnitLives[unit.Id] = new ObservedUnitLife {
                Dead = unit.Hp <= 0, Hp = unit.Hp, MaxHp = unit.MaxHp };
        }

        private void RecordUnitLife(FightBattleUnitState unit, bool dead)
        {
            // Pet action casters borrow an owner's identity; only the actual
            // roster member may publish that entity's lifecycle projection.
            if (unitLifeEvents == null || unit == null
                || !unitById.TryGetValue(unit.Id, out var actual) || !ReferenceEquals(actual, unit)) return;
            if (!observedUnitLives.TryGetValue(unit.Id, out var before))
            {
                RegisterUnitLife(unit);
                return;
            }
            if (before.Dead != dead)
                unitLifeEvents.Add(new PendingUnitLife {
                    Event = new FightBattleUnitLifeEvent { UnitId = unit.Id, IsDead = dead,
                        BeforeHp = before.Hp, BeforeMaxHp = before.MaxHp, Hp = unit.Hp, MaxHp = unit.MaxHp,
                        Delay = Math.Max(0f, unitLifeTimeOverride ?? gridViewTimeOverride ?? movementStartContext?.StatusStartTime ?? 0f) },
                    Cast = gridViewCast });
            before.Dead = dead;
            before.Hp = unit.Hp;
            before.MaxHp = unit.MaxHp;
        }

        private void CompleteUnitLifeEvents(FightBattleStepEvent result)
        {
            if (result == null || unitLifeEvents == null) return;
            result.HasUnitLifeEvents = true;
            foreach (var pending in unitLifeEvents)
            {
                pending.Event.Delay += Math.Max(0f, pending.Cast?.Delay ?? 0f);
                result.UnitLifeEvents.Add(pending.Event);
            }
        }

        private void ClearUnitLifeEvents()
        {
            unitLifeEvents = null;
            observedUnitLives = null;
        }
    }
}
