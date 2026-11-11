using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Zjcs.Combat.Official;

public static class BattleDamageStatusResultHarness
{
    private static readonly FieldInfo Known = typeof(FightBattleDamageEvent).GetField("HasDamageTriggerStatus");
    private static readonly FieldInfo Triggered = typeof(FightBattleDamageEvent).GetField("IsDamageTriggerStatus");
    private static readonly Regex NewFields = new Regex("\\\"(?:HasDamageTriggerStatus|IsDamageTriggerStatus)\\\":(?:true|false),");
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, string output, bool baseline)
    {
        Check((Known == null && Triggered == null) == baseline, "Compile actual pre-change sources for the baseline.");
        Directory.CreateDirectory(output);
        foreach (string name in new[] { "empty", "target", "self", "probability-failed", "hp-condition",
            "rejected", "success-then-failure", "failure-then-success", "both-owners", "random-one",
            "random-one-failed", "renewal", "preexisting-only" })
        {
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
                Grid = new FightBattleGrid { Width = 16, Height = 16 } };
            var source = Unit(1, 5); var target = Unit(2, 6); var other = Unit(3, 12);
            var skill = new FightBattleSkillDefinition { EntityId = 930, ItemId = 931, Slot = 0,
                TargetType = 1, Duration = 1f, ActionDuration = 1f };
            skill.Range.Add(new FightGridPoint(1, 0));
            var hit = new FightBattleHitDefinition { EntityId = 932, DamageId = 933,
                DamageType = 8, HasCustomDamage = true, CustomDamage = 5, TargetMode = 0 };
            skill.Hits.Add(hit);
            source.Skills.Add(skill); source.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            bool expected = false;
            Action<int, float> add = (owner, chance) => hit.AppliedStatuses.Add(new FightBattleStatusApplication {
                StatusId = 24031, TargetType = owner, Chance = chance, AffectedByProp = false, LessThanHpPer = -1f });
            switch (name)
            {
                case "target": add(0, 1); expected = true; break;
                case "self": add(1, 1); expected = true; break;
                case "probability-failed": add(0, 0); break;
                case "hp-condition": add(0, 1); hit.AppliedStatuses[0].LessThanHpPer = .1f; break;
                case "rejected": add(0, 1); target.VoodooDoll = new FightVoodooDollLink {
                    TargetId = 3, SyncSkillEntityId = 10640 }; break;
                case "success-then-failure": add(0, 1); add(1, 0); expected = true; break;
                case "failure-then-success": add(1, 0); add(0, 1); expected = true; break;
                case "both-owners": add(1, 1); add(0, 1); expected = true; break;
                case "random-one": add(1, 1); add(0, 1); hit.StatusListRandomOne = true; expected = true; break;
                case "random-one-failed": add(1, 0); add(0, 0); hit.StatusListRandomOne = true; break;
                case "renewal": add(0, 1); add(0, 1); expected = true; break;
                case "preexisting-only": target.Statuses.Add(catalog.CreateStatus(24031, 2, 1)); break;
            }
            cp.Units.AddRange(new[] { source, target, other });
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); cp.Waves.Add(wave);
            var step = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            var damage = step.Event.DamageEvents.Single(d => !d.IsSkillCost && d.DamageEntityId == 933);
            if (!baseline)
            {
                Check((bool)Known.GetValue(damage) && (bool)Triggered.GetValue(damage) == expected, name + ": wrong direct application result.");
                if (name == "both-owners") Check(source.Statuses.Any(s => s.ClassId == 24031)
                    && target.Statuses.Any(s => s.ClassId == 24031), "Success must not short-circuit later list entries.");
                if (name == "rejected") Check(!target.Statuses.Any(s => s.ClassId == 24031), "Rejected application must return false.");
                if (name == "random-one") Check(source.Statuses.Count(s => s.ClassId == 24031)
                    + target.Statuses.Count(s => s.ClassId == 24031) == 1, "Random-one must not apply the other entry.");
                if (name == "renewal") Check(target.Statuses.Count(s => s.ClassId == 24031) == 1
                    && target.Statuses.Single(s => s.ClassId == 24031).StackCount == 2, "Accepted renewal counts without making a second entity.");
            }
            string json = FightBattleCheckpointSerializer.Save(cp);
            var restored = FightBattleCheckpointSerializer.Load(json);
            Check(FightBattleCheckpointSerializer.Save(restored) == json, name + ": checkpoint round trip changed state.");
            string normalized = NewFields.Replace(json, "");
            if (!baseline)
            {
                var legacy = FightBattleCheckpointSerializer.Load(normalized);
                Check(legacy.Receipts.SelectMany(r => r.Event.DamageEvents).All(d => !(bool)Known.GetValue(d)
                    && !(bool)Triggered.GetValue(d)), "Legacy receipts must not infer status success from live statuses.");
            }
            File.WriteAllText(Path.Combine(output, name + ".json"), normalized);
        }
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
}
