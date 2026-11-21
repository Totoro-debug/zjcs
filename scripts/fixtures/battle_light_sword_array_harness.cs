using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleLightSwordArrayHarness
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Light sword array: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        foreach (int[] direction in new[] { new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 0 }, new[] { -1, 0 } })
        {
            int dx = direction[0], dy = direction[1];
            var actor = Unit(1, 1, 6, 6, 1, formula);
            actor.Hp = actor.Turn.Hp = actor.MaxHp / 2;
            var attacker = Unit(2, 2, 6 + dx, 6 + dy, 1000000, formula);
            var nearby = Unit(3, 2, 6 + 2 * dx, 6 + 2 * dy, 1, formula);
            var outside = Unit(4, 2, 6 + 3 * dx, 6 + 3 * dy, 1, formula);
            var ally = Unit(5, 1, 6 + dx + dy, 6 + dy - dx, 1, formula);
            var checkpoint = new FightBattleCheckpoint {
                StageId = 1, MaxRound = 10, RandomSeed = 91,
                Grid = new FightBattleGrid { Width = 14, Height = 14, DefaultGroundType = 2 }
            };
            checkpoint.Units.AddRange(new[] { actor, attacker, nearby, outside, ally });
            var wave = new FightBattleWaveState();
            wave.UnitIds.AddRange(new[] { attacker.Id, nearby.Id, outside.Id }); checkpoint.Waves.Add(wave);
            AddSkill(actor, 11990, 10083, catalog);
            AddSkill(attacker, 10600, 10008, catalog);
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var cold = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var restored = FightBattleExecutor.Restore(cold, formula, new FightDamageSettlement(), catalog, items);
            int incoming = 0, counters = 0, openings = 0;
            while (incoming < 6 && checkpoint.Counter < 100 && !checkpoint.Finished)
            {
                int before = actor.Statuses.SingleOrDefault(status => status.ClassId == 24378)?.StackCount ?? 0;
                var step = executor.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event;
                restored.Step(cold.Sequence, cold.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(cold),
                    "restoration changed the opening, charge count or retaliation");
                var opening = step.SkillCasts.Where(cast => cast.SkillEntityId == 11990).ToArray();
                openings += opening.Length;
                if (opening.Length > 0)
                {
                    var status = actor.Statuses.Single(status => status.ClassId == 24378);
                    Check(opening.Length == 1 && step.ActorRound == 0 && status.StackCount == 4
                        && status.MaxStackedCount == 4 && !status.CanDispersed,
                        "opening differs: casts=" + opening.Length + ", round=" + step.ActorRound
                        + ", stacks=" + status.StackCount + ", max=" + status.MaxStackedCount
                        + ", dispellable=" + status.CanDispersed + ", hits="
                        + string.Join(",", step.DamageEvents.Select(hit => hit.SourceId + ":" + hit.TargetId
                            + "/" + hit.Damage + "/" + hit.HasDamageValue + "/" + hit.SkillEntityId)));
                }
                var received = step.DamageEvents.Where(hit => hit.SourceId == attacker.Id
                    && hit.TargetId == actor.Id && !hit.IsCure && hit.Damage < 0).ToArray();
                if (received.Length == 0) continue;
                Check(received.Length == 1, "the isolated incoming slash must hit once");
                incoming++;
                var retaliation = step.SkillCasts.Where(cast => cast.SkillEntityId == 12000).ToArray();
                int after = actor.Statuses.SingleOrDefault(status => status.ClassId == 24378)?.StackCount ?? 0;
                Check(retaliation.Length == (before > 0 ? 1 : 0) && after == Math.Max(0, before - 1),
                    "charge transition differs: " + before + " -> " + after + ", counters=" + retaliation.Length
                    + ", casts=" + string.Join(",", step.SkillCasts.Select(value => value.ActorId + ":" + value.SkillEntityId)));
                if (retaliation.Length == 0) continue;
                counters++;
                var cast = retaliation.Single();
                var hits = step.DamageEvents.Where(hit => hit.SkillEntityId == 12000).ToArray();
                Check(cast.ActorId == actor.Id && cast.SkillItemId == 10083 && cast.Slot < 0
                    && hits.Select(hit => hit.TargetId).OrderBy(id => id).SequenceEqual(new[] { attacker.Id, nearby.Id })
                    && hits.All(hit => hit.SourceId == actor.Id && hit.HitEntityId == 12001
                        && hit.DamageEntityId == 10026 && hit.ElementType == 103 && hit.DamageType == 1
                        && hit.Damage < 0 && !hit.TrueDamage && !hit.IsCure),
                    "the light counter must retain its owner/cultivation and exclude friendly/outside units");
            }
            Check(openings == 1 && incoming == 6 && counters == 4 && !actor.Statuses.Any(status => status.ClassId == 24378),
                "four swords did not retire: openings=" + openings + ", incoming=" + incoming
                + ", counters=" + counters + ", stacks="
                + (actor.Statuses.SingleOrDefault(status => status.ClassId == 24378)?.StackCount ?? -1)
                + ", actor round=" + actor.SkillState.Round + ", attacker round=" + attacker.SkillState.Round);
            cases++;
        }
        VerifyGuards(catalog, formula);
        return "Light sword array passed: " + cases
            + " original opening, four-charge, counter-area and restore cases; nine damage/filter/unsupported-flow guards.";
    }

    private static void VerifyGuards(FightOfficialCombatCatalog catalog, FightDamageFormula formula)
    {
        foreach (string mode in new[] { "hit", "shield", "zero", "dodge", "blind", "immune", "cure", "forbidden", "unsupported" })
        {
            var owner = Unit(1, 1, 6, 6, 100, formula);
            var attacker = Unit(2, 2, 7, 6, 100, formula);
            var units = new[] { owner, attacker };
            AddSkill(attacker, 10600, 10008, catalog);
            var skill = attacker.Skills.Single();
            if (mode == "forbidden") skill.CanTriggerChild = false;
            var hit = FightBattleHitCollector.CollectAll(skill).Single();
            var status = catalog.CreateStatus(24378, 1, 1, 924378, 11990, 10083, 5, 20, 8, owner.Properties);
            owner.Statuses.Add(status);
            if (mode == "unsupported") status.LogicDefinitionJson = status.LogicDefinitionJson.Replace(
                "FightHandleCfgNewIntInBB", "UnmigratedCounterHandle");
            var damage = new FightBattleDamageEvent {
                SourceId = 2, TargetId = 1, HasDamageValue = mode != "cure", IsCure = mode == "cure",
                Damage = mode == "hit" ? -100 : 0, ShieldDamage = mode == "shield" ? 100 : 0,
                Dodge = mode == "dodge", Blinding = mode == "blind", Immunity = mode == "immune"
            };
            var random = new FightRandomStream(91);
            var untouched = new FightRandomStream(91);
            var result = FightStatusLogicRuntime.Apply(owner, status, FightStatusLogicEvent.AfterBeDamage,
                attacker, owner, skill, hit, units, catalog, random, damage, formula);
            bool allowed = mode == "hit" || mode == "shield" || mode == "zero";
            Check(result.Count == (allowed ? 1 : 0) && status.StackCount == (allowed ? 3 : 4)
                && status.LogicTriggeredCount == (allowed ? 1 : 0),
                "damage guard consumed or triggered an incorrect sword: " + mode);
            if (!allowed) Check(random.Next() == untouched.Next() && status.LogicBlackboard.Count == 0,
                "a rejected damage/unsupported flow consumed randomness or persisted an event port: " + mode);
            if (mode == "unsupported") Check(!status.LogicRulesSupported,
                "an unsupported handle executed a partial retaliation");
        }
    }

    private static void AddSkill(FightBattleUnitState unit, int entity, int item, FightOfficialCombatCatalog catalog)
    {
        var skill = catalog.CreateSkill(entity, 0, item, 5, 20, 8,
            catalog.CalculateSkillProps(entity, 5, 20, 8)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList());
        unit.Skills.Add(skill); unit.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, int speed, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 4, Level = 70, SubRank = 8,
            X = x, Y = y, Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000
        };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = speed;
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = speed });
        return unit;
    }
}
