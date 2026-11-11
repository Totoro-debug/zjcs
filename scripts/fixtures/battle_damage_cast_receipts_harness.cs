using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Zjcs.Combat.Official;

public static class BattleDamageCastReceiptsHarness
{
    private static readonly FieldInfo IdsField = typeof(FightBattleSkillCastEvent).GetField("DamageEventIds");
    private static readonly Regex NewField = new Regex("\\\"DamageEventIds\\\":(?:\\[[^\\]]*\\]|null),");
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static List<int> Ids(FightBattleSkillCastEvent cast) => (List<int>)IdsField.GetValue(cast);

    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, string output, bool baseline)
    {
        Check((IdsField == null) == baseline, "Baseline must compile the actual pre-change sources.");
        Directory.CreateDirectory(output);
        foreach (int id in new[] { 10910, 10920, 13520 })
            RunCase("official-" + id, catalog.CreateSkill(id, 0, 0, 5, 20, 1, null),
                catalog, items, formula, output, baseline, 0, false);
        RunCase("repeated-class", Controlled(930, 5, false), catalog, items, formula, output, baseline, 20, false);
        RunCase("nested-children", Controlled(940, 5, true), catalog, items, formula, output, baseline, 0, true);
        RunCase("zero-damage", Controlled(950, 0, false), catalog, items, formula, output, baseline, 0, false);
    }

    private static FightBattleSkillDefinition Controlled(int id, long amount, bool children)
    {
        var skill = new FightBattleSkillDefinition { EntityId = id, ItemId = id + 1, Slot = 0,
            TargetType = 1, ActionDuration = 1f, Duration = 1.2f };
        skill.Range.Add(new FightGridPoint(1, 0));
        for (int i = 0; i < (children ? 2 : 1); i++)
            skill.Hits.Add(new FightBattleHitDefinition { EntityId = id + 2, DamageId = id + 3,
                DamageType = 8, HasCustomDamage = true, CustomDamage = amount, TargetMode = 0,
                Delay = i * .2f, ChildSkillId = children ? 10640 : 0,
                ChildSkillRate = children ? 1f : 0f, ChildSkillLessThanHpPer = -1f });
        if (id == 930)
        {
            skill.HasReplay = true; skill.ReplayStatusId = 24031; skill.ReplayStackCount = 10;
            skill.ReplayRelation = 1; skill.ReplayConsumeStack = true; skill.ReplayConsumeCount = 10;
        }
        return skill;
    }

    private static FightBattleUnitState Unit(int id, int x)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id == 1 ? 1 : 2,
            CharacterType = id == 1 ? 1 : 2, X = x, Y = 5, Hp = 1000000, MaxHp = 1000000,
            Level = 150, SubRank = 1, MoveMask = 5, Direction = 3, HasFlipX = true,
            Turn = new FightTurnUnit { Id = id, Hp = 1000000, Speed = 100 - id } };
        foreach (var pair in new[] { new[] { 1, 10001 }, new[] { 2, 10003 },
            new[] { 3, 1000000 }, new[] { 4, 100 - id } })
            unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
        return unit;
    }

    private static void RunCase(string name, FightBattleSkillDefinition skill,
        FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items, FightDamageFormula formula,
        string output, bool baseline, int stacks, bool nested)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
            Grid = new FightBattleGrid { Width = 16, Height = 16 } };
        var source = Unit(1, 5); var target = Unit(2, nested || skill.EntityId < 1000 ? 6 : 8);
        var other = Unit(3, 11);
        source.Skills.Add(skill); source.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
        if (stacks > 0)
        {
            var status = catalog.CreateStatus(24031, 1, 1); status.StackCount = stacks;
            source.Statuses.Add(status);
        }
        if (skill.EntityId >= 1000)
        {
            target.BodyOffsets.Add(new FightGridPoint(1, 0)); target.BodyOffsets.Add(new FightGridPoint(0, 1));
            target.BodyOffsets.Add(new FightGridPoint(1, 1));
        }
        cp.Units.AddRange(new[] { source, target, other });
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); wave.UnitIds.Add(3); cp.Waves.Add(wave);
        var step = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
        Check(step.Event.DamageEvents.Count > 0, name + ": no settled damage.");
        if (!baseline)
        {
            var damages = step.Event.DamageEvents.ToDictionary(d => d.DamageId);
            var assigned = new HashSet<int>();
            foreach (var cast in step.Event.SkillCasts)
                foreach (int id in Ids(cast))
                {
                    Check(assigned.Add(id), name + ": one occurrence belongs to multiple casts.");
                    Check(damages.TryGetValue(id, out var damage) && !damage.IsSkillCost
                        && damage.SourceId == cast.ActorId && damage.SkillEntityId == cast.SkillEntityId,
                        name + ": damage does not belong to its actual cast.");
                }
            Check(assigned.Count == damages.Count, name + ": direct damage omitted from cast receipts.");
            if (stacks > 0)
            {
                var same = step.Event.SkillCasts.Where(c => c.SkillEntityId == 930).ToArray();
                Check(same.Length == 3 && same.All(c => Ids(c).Count == 1),
                    "Repeated same-class casts lost their independent occurrence lists.");
            }
            if (nested)
            {
                var parent = step.Event.SkillCasts.Single(c => c.SkillEntityId == 940);
                var children = step.Event.SkillCasts.Where(c => c.SkillEntityId == 10640).ToArray();
                Check(children.Length == 2 && children.All(c => Ids(c).Count > 0) && Ids(parent).Count == 2,
                    "Child damage was merged into the parent's receipt.");
                Check(Ids(parent)[1] > Ids(parent)[0] + 1, "Nested fixture did not interleave real damage serials.");
            }
            if (skill.EntityId == 950)
                Check(assigned.Count > 0 && step.Event.DamageEvents.All(d => d.Damage == 0),
                    "Zero-damage occurrences must retain their actual identity.");
        }
        string json = FightBattleCheckpointSerializer.Save(cp);
        var restored = FightBattleCheckpointSerializer.Load(json);
        Check(FightBattleCheckpointSerializer.Save(restored) == json, name + ": checkpoint round trip changed receipts.");
        if (!baseline)
        {
            var legacy = FightBattleCheckpointSerializer.Load(NewField.Replace(json, ""));
            Check(legacy.Receipts.SelectMany(r => r.Event.SkillCasts).All(c => Ids(c).Count == 0),
                name + ": legacy saves must not infer cast ownership from class/actor/serial intervals.");
        }
        File.WriteAllText(Path.Combine(output, name + ".json"), NewField.Replace(json, ""));
    }
}
