using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleFlameMomentumHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Flame momentum: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        Check(catalog.ReportParentSkillId(12820) == 12800 && catalog.ReportParentSkillId(13148) == -1
            && catalog.ReportParentSkillId(11910) == 11910 && items.IsActiveSkillEntity(11910)
            && !items.IsActiveSkillEntity(11150) && !items.IsActiveSkillEntity(25010),
            "the original report parent, fallback or active-item classification changed");
        VerifyParentClassification(catalog, formula, items);
        int cases = 0;
        foreach (int rank in new[] { 1, 2, 4, 5, 6, 7 })
        foreach (int level in new[] { 1, 2 })
        foreach (string mode in new[] { "critical", "normal", "forbidden", "no-child", "shield", "killed-target", "body", "damage-position" })
        {
            string actual = Exercise(catalog, formula, items, rank, level, mode, false);
            string restored = Exercise(catalog, formula, items, rank, level, mode, true);
            Check(actual == restored, "restoration changed damage, source, positions or trigger counters");
            cases++;
        }
        return "Flame momentum passed: " + cases + " cultivated critical area, normal/forbidden/child exclusion, shielding, killed target, body, frozen damage position and restoration cases.";
    }

    private static void VerifyParentClassification(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        foreach (int entityId in new[] { 11910, 11150, 12820, 13148 })
        {
            var source = new FightBattleUnitState { Id = 1, Side = 1, Hp = 1000, MaxHp = 1000, X = 5, Y = 5 };
            var target = new FightBattleUnitState { Id = 2, Side = 2, Hp = 1000, MaxHp = 1000, X = 5, Y = 6 };
            var status = catalog.CreateStatus(25010, 1, 1, 725010, 25010, 10543, 1, 1, 8);
            var skill = catalog.CreateSkill(entityId, -1, 0, 1, 1, 8,
                catalog.CalculateSkillProps(entityId, 1, 1, 8).Select(pair =>
                    new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
            bool active = entityId == 11910 || entityId == 12820;
            // Root ancestry and the explicit child whitelist are independent of
            // the authored report-parent classification tested here.
            skill.RootEntityId = active ? 25010 : 11910;
            if (!skill.CanTriggerStatusEntityIds.Contains(25010)) skill.CanTriggerStatusEntityIds.Add(25010);
            var hit = FightBattleHitCollector.CollectAll(skill).First();
            var damage = new FightBattleDamageEvent { SourceId = 1, TargetId = 2, SkillEntityId = entityId,
                Critical = true, HasDamageValue = true, DamageValue = -1000, Damage = -1000,
                HasDamagePosition = true, DamageX = 5, DamageY = 6 };
            var invocations = FightStatusLogicRuntime.Apply(source, status, FightStatusLogicEvent.DoDamage,
                source, target, skill, hit, new[] { source, target }, catalog, new FightRandomStream(19),
                damage, formula, skillItems: items);
            Check(invocations.Count == (active ? 1 : 0),
                "the report parent was replaced by root ancestry or a child-policy shortcut: " + entityId);
        }
    }

    private static string Exercise(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items, int rank, int level, string mode, bool restore)
    {
        var state = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = 37,
            Grid = new FightBattleGrid { Width = 16, Height = 16, DefaultGroundType = 2 } };
        int[] xs = { 5, 5, 6, 7, 5 }, ys = { 5, 6, 6, 6, 7 };
        for (int id = 1; id <= 5; id++)
        {
            var unit = new FightBattleUnitState { Id = id, Side = id == 1 || id == 5 ? 1 : 2,
                CharacterType = 1, Profession = 8, Level = 70, SubRank = 8, X = xs[id - 1], Y = ys[id - 1],
                Hp = id == 2 && mode == "killed-target" ? 1 : 1000000, MaxHp = 1000000, LastMaxHp = 1000000 };
            unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 100;
            foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000 },
                { "Speed", id == 1 ? 1000 : 100 }, { "BaseCritRatePercentValue", 10000 },
                { "BaseCritAvoidPercentValue", 10000 }, { "CritRatePercent", mode == "normal" ? 0 : 10000 },
                { "CritPowerPercent", 5000 }, { "BaseElementMaster", 10000 }, { "BaseElementResistance", 10000 },
                { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
                unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
            if (id == 1)
            {
                var skill = catalog.CreateSkill(11910, 0, 10088, 4, 2, 8, catalog.CalculateSkillProps(11910, 4, 2, 8)
                    .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
                if (mode == "no-child") { skill.CanTriggerChild = false; skill.CanTriggerStatusEntityIds.Clear(); }
                unit.Skills.Add(skill); unit.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
                var passive = catalog.CreateStatus(25010, 1, 1, 725010, 25010, 10543, rank, level, 8);
                if (mode == "forbidden") passive.ForbiddenType = FightStatusLifecycle.ForbiddenMutex;
                unit.Statuses.Add(passive);
            }
            if (id == 2 && mode == "body") unit.BodyOffsets.Add(new FightGridPoint(1, 0));
            if (id == 2 && mode == "shield") unit.Statuses.Add(new FightBattleStatusState {
                ClassId = 999901, InstanceId = 999901, OwnerId = id, CreatorId = id, DurationRound = -1,
                ShieldKind = FightBattleShieldKind.Persistent, ShieldValue = 1000000 });
            state.Units.Add(unit);
        }
        var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3, 4 }); state.Waves.Add(wave);
        var executor = FightBattleExecutor.Begin(state, formula, new FightDamageSettlement(), catalog, items);
        if (restore)
        {
            state = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
            executor = FightBattleExecutor.Restore(state, formula, new FightDamageSettlement(), catalog, items);
        }
        var owner = state.Units[0]; var primary = state.Units[1];
        var step = executor.Step(state.Sequence, state.Counter + 1).Event;
        var parent = step.DamageEvents.Where(hit => hit.SkillEntityId == 11910).ToArray();
        var casts = step.SkillCasts.Where(cast => cast.SkillEntityId == 11150).ToArray();
        var fire = step.DamageEvents.Where(hit => hit.SkillEntityId == 11150).ToArray();
        bool enabled = mode != "normal" && mode != "forbidden" && mode != "no-child";
        Check(parent.Length > 0 && parent.All(hit => hit.Critical == (mode != "normal")), "the controlled parent did not execute");
        Check(casts.Length == (enabled ? parent.Count(hit => hit.HasDamageValue && hit.DamageValue < 0) : 0),
            "critical active enemy hits did not create exactly one original fire child: " + rank + "/" + level + "/" + mode
                + " children=" + casts.Length + " parent=" + parent.Length + " supported=" + owner.Statuses[0].LogicRulesSupported);
        Check(fire.All(hit => hit.SourceId == 1 && hit.SkillItemId == 10543 && hit.HitEntityId == 11151
            && hit.DamageEntityId == 10023 && hit.ElementType == 102 && (hit.Damage < 0 || hit.ShieldDamage > 0) && !hit.IsCure && !hit.TrueDamage
            && (hit.TargetId == 2 || hit.TargetId == 3)), "the original fire scope, damage or secret source changed");
        if (enabled)
        {
            var cultivated = catalog.CalculateSkillProps(11150, rank, level, 8);
            long expectedDamage = (long)((1000m * cultivated[200] / 10000m + cultivated[190]) * 1000m / 1010m * 1.5m);
            Check(fire.Where(hit => hit.TargetId == 3).All(hit => hit.Critical && hit.Damage == -expectedDamage),
                "the original fixed-plus-percentage fire value changed: " + rank + "/" + level + "/" + mode
                    + " expected=" + expectedDamage + " actual=" + string.Join(",", fire.Where(hit => hit.TargetId == 3).Select(hit => hit.Damage)));
            Check(fire.Count(hit => hit.TargetId == 3) >= 1 && state.Units[3].Hp == 1000000 && state.Units[4].Hp == 1000000,
                "the original one-cell blast omitted its neighbour or struck a distant/friendly target");
            Check(casts.All(cast => cast.Presentation.HasEntityPosition && cast.Presentation.EntityX == 5
                && cast.Presentation.EntityY == 6) || mode == "damage-position", "the blast lost its actual damage centre");
            if (mode == "damage-position")
            {
                primary.X = primary.Y = 12;
                var hit = FightBattleHitCollector.CollectAll(owner.Skills[0]).First(value => value.EntityId == parent[0].HitEntityId);
                var invocations = FightStatusLogicRuntime.Apply(owner, owner.Statuses.Single(value => value.ClassId == 25010),
                    FightStatusLogicEvent.DoDamage, owner, primary, owner.Skills[0], hit, state.Units,
                    catalog, new FightRandomStream(19), parent[0], formula, skillItems: items);
                Check(invocations.Count == 1 && invocations[0].HitOrigin.HasValue
                    && invocations[0].HitOrigin.Value.X == parent[0].DamageX
                    && invocations[0].HitOrigin.Value.Y == parent[0].DamageY,
                    "the blast followed the moved unit instead of the frozen damage position");
            }
            else Check(fire.Count(hit => hit.TargetId == 3) == casts.Length,
                "a neighbour was struck more than once by a single blast");
            if (mode == "body") Check(fire.Count(hit => hit.TargetId == 2) == casts.Length,
                "a large target was struck more than once by a single blast");
        }
        var saved = FightBattleCheckpointSerializer.Save(state);
        Check(executor.Step(state.Sequence, state.Counter).Replayed && FightBattleCheckpointSerializer.Save(state) == saved,
            "replay repeated a critical blast or changed the final state");
        return saved;
    }
}
