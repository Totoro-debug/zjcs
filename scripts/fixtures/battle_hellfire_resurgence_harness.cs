using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleHellfireResurgenceHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Hellfire resurgence: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases = 0;
        var selections = new HashSet<string>();
        foreach (int rank in new[] { 1, 4, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (bool full in new[] { false, true })
        foreach (bool grow in new[] { false, true })
        foreach (int seed in new[] { 7, 91, 734 })
        {
            var actor = Unit(1, 1, 4, 4, formula);
            if (full) actor.Hp = actor.MaxHp;
            actor.Turn.Hp = actor.Hp;
            var enemy = Unit(2, 2, 12, 12, formula);
            var ally = Unit(3, 1, 4, 12, formula);
            var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 6, RandomSeed = seed,
                Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
            state.Units.AddRange(new[] { actor, enemy, ally });
            var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); state.Waves.Add(wave);
            var skill = catalog.CreateSkill(11520, 0, 10068, rank, level, 8,
                catalog.CalculateSkillProps(11520, rank, level, 8)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            skill.LimitedTimes = 1;
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
            FightBattleStepEvent opening = null;
            while (state.Counter < 8 && !state.Finished)
            {
                opening = executor.Step(state.Sequence, state.Counter + 1).Event;
                if (opening.SkillCasts.Any(cast => cast.ActorId == 1 && cast.SkillEntityId == 11520)) break;
            }
            Check(opening != null && opening.SkillCasts.Any(cast => cast.SkillEntityId == 11520)
                && opening.DamageEvents.All(hit => !hit.IsCure), "the active did not defer its cure to later turns");
            var regeneration = actor.Statuses.Single(status => status.ClassId == 24235);
            Check(regeneration.DurationRound == 2 && regeneration.RoundStartMaxCount == 2
                && !regeneration.RoundStartTryAtStart && regeneration.SourceSkillRank == rank
                && regeneration.SourceSkillLevel == level && regeneration.SourceSubRank == 8,
                "the original two-turn status or cultivation changed");
            if (grow)
            {
                int maxHp = formula.PropertyId("MaxHp");
                actor.BaseProperties.Single(property => property.Id == maxHp).Value *= 2;
                FightStatusPropertyRuntime.RebuildAll(state.Units, catalog, formula);
                actor.Turn.Hp = actor.Hp;
            }
            int[] negativeIds = { 24010, 24266, 24216, 24194 };
            foreach (int id in negativeIds)
            {
                var status = catalog.CreateStatus(id, 1, 2, 800000 + id);
                // Hold unrelated control effects beyond this fixture's window;
                // their removal must prove dispelling rather than natural expiry.
                status.DurationRound = status.CurDurationRound = 10; status.StartRound = actor.SkillState.Round;
                Check(status.CanDispersed && (status.StatusType == 2 || status.StatusType == 3), "invalid negative fixture");
                actor.Statuses.Add(status);
            }
            var positive = catalog.CreateStatus(24377, 1, 1, 824377, customShieldValue: 10000);
            positive.DurationRound = positive.CurDurationRound = 10; positive.StartRound = actor.SkillState.Round;
            actor.Statuses.Add(positive);
            var locked = catalog.CreateStatus(24266, 1, 2, 924266, 24266, 99999);
            locked.CanDispersed = false; locked.DurationRound = locked.CurDurationRound = 10; locked.StartRound = actor.SkillState.Round;
            actor.Statuses.Add(locked);
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            // Simulate the earlier one-child checkpoint; restoration must bind
            // the newly migrated graph without resetting its remaining lifetime.
            copy.Units.Single(unit => unit.Id == 1).Statuses.Single(status => status.ClassId == 24235)
                .RoundStartSkills.RemoveAll(trigger => trigger.EntityId == 11703);
            var resumed = FightBattleExecutor.Restore(copy, formula, new FightDamageSettlement(), catalog, items);
            int cures = 0;
            while (actor.SkillState.Round < 4 && state.Counter < 40 && !state.Finished)
            {
                var step = executor.Step(state.Sequence, state.Counter + 1).Event;
                resumed.Step(copy.Sequence, copy.Counter + 1);
                Check(FightBattleCheckpointSerializer.Save(state) == FightBattleCheckpointSerializer.Save(copy),
                    "restoration changed healing, random dispels, duration or counters");
                var childCasts = step.SkillCasts.Where(cast => cast.SkillEntityId == 11700).ToArray();
                if (childCasts.Length == 0) continue;
                cures++;
                var dispelCast = step.SkillCasts.Single(cast => cast.SkillEntityId == 11703);
                var dispelHit = step.DamageEvents.Single(hitEvent => hitEvent.SkillEntityId == 11703);
                Check(dispelCast.ActorId == 1 && dispelCast.SkillItemId == 10068 && dispelCast.Slot < 0
                    && dispelHit.TargetId == 1 && dispelHit.HitEntityId == 11704 && dispelHit.DamageEntityId == 11705
                    && !dispelHit.IsCure && dispelHit.Damage == 0,
                    "the current independent dispel child was lost or combined with healing");
                Check(childCasts.Length == 1 && childCasts[0].ActorId == 1 && childCasts[0].SkillItemId == 10068
                    && childCasts[0].Slot < 0 && step.ActorId == 1 && step.ActorRound == cures + 1,
                    "healing did not occur exactly at each subsequent caster turn start");
                var hit = step.DamageEvents.Single(value => value.SkillEntityId == 11700);
                var props = catalog.CalculateSkillProps(11700, rank, level, 8).ToDictionary(pair => pair.Key, pair => pair.Value);
                long raw = (long)(actor.MaxHp * formula.ReadProperty(props, 210) + formula.ReadProperty(props, 170));
                Check(hit.IsCure && hit.SourceId == 1 && hit.TargetId == 1 && hit.HitEntityId == 11701
                    && hit.DamageEntityId == 11702 && hit.Damage == Math.Min(raw, actor.MaxHp - hit.BeforeHp)
                    && hit.AfterHp - hit.BeforeHp == hit.Damage,
                    "cure lost its original current maximum HP, cultivation, fixed term or full-health cap: "
                    + rank + "/" + level + "/" + full + "/" + grow + ", actual=" + hit.Damage + ", raw=" + raw
                    + ", cure=" + hit.IsCure + ", source=" + hit.SourceId + ", target=" + hit.TargetId
                    + ", before=" + hit.BeforeHp + ", after=" + hit.AfterHp + ", max=" + actor.MaxHp
                    + ", dodge=" + hit.Dodge + ", blind=" + hit.Blinding + ", immune=" + hit.Immunity);
                int remaining = actor.Statuses.Count(status => negativeIds.Contains(status.ClassId) && status.InstanceId == 800000 + status.ClassId);
                Check(remaining == 4 - cures * 2, "each subsequent turn did not randomly dispel exactly two eligible negatives");
                Check(actor.Statuses.Contains(positive) && actor.Statuses.Contains(locked), "a cure dispelled a positive or protected negative");
                if (cures == 1) selections.Add(string.Join(",", actor.Statuses.Where(status => negativeIds.Contains(status.ClassId)
                    && status.InstanceId == 800000 + status.ClassId).Select(status => status.ClassId).OrderBy(id => id)));
            }
            Check(cures == 2 && regeneration.RoundStartTriggeredCount == 2
                && actor.Statuses.All(status => status.ClassId != 24235) && ally.Hp == 5000000,
                "the status healed beyond two later turns, retained itself, or healed an ally");
            string saved = FightBattleCheckpointSerializer.Save(state);
            Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved,
                "replay repeated a cure or consumed another random dispel");
            cases++;
        }
        Check(selections.Count > 1, "random dispels always removed the same two negatives");
        return "Hellfire resurgence passed: " + cases + " cultivation, delayed turns, cure, full health, dynamic maximum HP, random dispel and restore cases.";
    }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 8,
            Level = 70, SubRank = 8, X = x, Y = y, Hp = 5000000, MaxHp = 10000000, LastMaxHp = 10000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 10000 : 100;
        foreach (var pair in new Dictionary<string, long> { { "Attack", 1000 }, { "Defence", 10 },
            { "MaxHp", unit.MaxHp }, { "Speed", unit.Turn.Speed }, { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 },
            { "BaseCureAdd", 10000 }, { "BaseBeCureAdd", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
