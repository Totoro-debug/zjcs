using System;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStatusOnlyHarness
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, FightBattleReportScoreSettings scores, string output)
    {
        Directory.CreateDirectory(output);
        foreach (string name in new[] { "target", "debuff", "abnormal", "self", "failed", "renewal", "disperse", "move", "mixed",
            "child", "hit-listener", "damage-listener", "cure-listener", "original-11160", "original-12890", "original-11460" })
        {
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
                Grid = new FightBattleGrid { Width = 16, Height = 16 } };
            var source = Unit(1, 5); var target = Unit(2, 6); var other = Unit(3, 12);
            FightBattleSkillDefinition skill;
            FightBattleStatusHitSkillComponentState hitListener = null;
            FightBattleStatusDamageSkillComponentState damageListener = null;
            if (name.StartsWith("original-"))
                skill = catalog.CreateSkill(int.Parse(name.Substring(9)), 0, 0, 5, 20, 1, null);
            else
            {
                skill = new FightBattleSkillDefinition { EntityId = 930, ItemId = 931, Slot = 0,
                    TargetType = 1, Duration = 1f, ActionDuration = 1f, CanTriggerChild = true };
                skill.Range.Add(new FightGridPoint(1, 0));
                var hit = new FightBattleHitDefinition { EntityId = 932, DamageId = 933,
                    ActionType = 0, DamageType = 8, HasCustomDamage = true, CustomDamage = 12345, TargetMode = 0 };
                hit.AppliedStatuses.Add(new FightBattleStatusApplication { StatusId = name == "debuff" ? 24436 : name == "abnormal" ? 24010 : 24031,
                    TargetType = name == "self" ? 1 : 0, Chance = name == "failed" ? 0 : 1,
                    AffectedByProp = false, LessThanHpPer = -1f });
                if (name == "mixed")
                {
                    hit.ActionType = 4;
                    hit.AppliedStatuses.Add(new FightBattleStatusApplication { StatusId = 24436,
                        Chance = 1, AffectedByProp = false, LessThanHpPer = -1f });
                    hit.AppliedStatuses.Add(new FightBattleStatusApplication { StatusId = 24543, TargetType = 1,
                        Chance = 1, AffectedByProp = false, LessThanHpPer = -1f, CustomShieldValue = 100 });
                }
                if (name == "renewal") hit.AppliedStatuses.Add(hit.AppliedStatuses[0]);
                if (name == "disperse")
                {
                    var old = catalog.CreateStatus(24436, 2, 1); old.CanDispersed = true;
                    target.Statuses.Add(old); hit.DisperseStatusTypes.Add(old.StatusType);
                    hit.DisperseChance = 1; hit.DisperseCount = 1;
                }
                if (name == "move") hit.HitMove = new FightBattleSkillMove {
                    Distance = 1, Direction = 1, BasePercent = 1, AffectedByProp = false };
                if (name == "child") { hit.ChildSkillId = 10100; hit.ChildSkillRate = 1; hit.ChildSkillLessThanHpPer = -1; }
                if (name.EndsWith("listener"))
                {
                    hit.AppliedStatuses.Clear();
                    var status = catalog.CreateStatus(24031, 1, 1);
                    hitListener = new FightBattleStatusHitSkillComponentState { Rate = 1,
                        Damage = name == "damage-listener", Cure = name == "cure-listener" };
                    damageListener = new FightBattleStatusDamageSkillComponentState { Rate = 1,
                        Damage = false, Cure = false, IsAttackerTarget = true };
                    status.Components = new FightBattleStatusComponentState {
                        HitSkill = hitListener, DamageSkill = damageListener };
                    source.Statuses.Add(status);
                }
                skill.Hits.Add(hit);
            }
            source.Skills.Add(skill); source.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            cp.Units.AddRange(new[] { source, target, other });
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); cp.Waves.Add(wave);
            var step = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items).Step(0, 1).Event;
            var receipts = step.DamageEvents.Where(d => !d.IsSkillCost && d.SkillEntityId == skill.EntityId).ToArray();
            Check(receipts.Length > 0, name + ": original None occurrence was dropped.");
            foreach (var receipt in receipts.Where(d => name != "mixed"))
                Check(!receipt.IsCure && !receipt.HasDamageValue && !receipt.HasAbsoluteDamage
                    && receipt.BeforeHp == receipt.AfterHp && receipt.Damage == 0 && receipt.ShieldDamage == 0
                    && receipt.HasDamagePosition && receipt.HasDamageTriggerStatus,
                    name + ": a None occurrence must retain actual effects/position, without damage/cure settlement.");
            var stats = cp.Statistics.Skill(1, skill.EntityId, skill.ItemId);
            Check(stats.DamageHits == (name == "mixed" ? 1 : 0) && stats.CureHits == 0
                && (name == "mixed" ? stats.Damage > 0 : stats.Damage == 0) && stats.Heal == 0,
                name + ": pure status must not count as damage/cure.");
            if (!name.StartsWith("original-") && !name.EndsWith("listener"))
            {
                int accepted = name == "failed" ? 0 : name == "renewal" || name == "mixed" ? 2 : 1;
                Check(stats.BuffApplications + stats.DebuffApplications == accepted
                    && cp.Statistics.Unit(1).BuffApplications + cp.Statistics.Unit(1).DebuffApplications == accepted,
                    name + ": successful applications must enter existing status statistics exactly once.");
                Check(receipts.All(d => d.IsDamageTriggerStatus == (accepted > 0)), name + ": actual status flag.");
            }
            if (name == "self") Check(source.Statuses.Any(s => s.ClassId == 24031)
                && !target.Statuses.Any(s => s.ClassId == 24031), "Self status destination.");
            if (name == "debuff" || name == "abnormal") Check(cp.Statistics.Unit(1).OrdinaryDebuffApplications == (name == "debuff" ? 1 : 0)
                && cp.Statistics.Unit(1).AbnormalDebuffApplications == (name == "abnormal" ? 1 : 0),
                "Both ordinary and abnormal debuffs enter applied-status statistics with their original type.");
            if (name == "renewal") Check(target.Statuses.Single(s => s.ClassId == 24031).StackCount == 2,
                "Renewal is one live state and two accepted applications.");
            if (name == "mixed") Check(stats.BuffApplications == 1 && stats.DebuffApplications == 1
                && cp.Statistics.Unit(1).BuffApplications == 1 && cp.Statistics.Unit(1).DebuffApplications == 1
                && stats.ShieldGranted > 0 && cp.Statistics.Unit(1).ShieldGranted == stats.ShieldGranted,
                "One skill must independently report damage, shield, buff and debuff; status count is buff plus debuff."
                    + " buff=" + stats.BuffApplications + " debuff=" + stats.DebuffApplications + " shield=" + stats.ShieldGranted);
            if (name == "original-12890") Check(stats.ShieldGranted > 0
                && cp.Statistics.Unit(1).ShieldGranted == stats.ShieldGranted,
                "Original pure shield must grant and count its real shield, without a buff/debuff count.");
            if (name == "disperse") Check(!target.Statuses.Any(s => s.ClassId == 24436)
                && step.DispersedUnitIds.Count == 1 && step.DispersedUnitIds[0] == target.Id
                && receipts.Single().HasDisperseNotification && receipts.Single().DisperseNotificationIndex == 0,
                "The successful pure-status disperse retains its exact causing hit and compatible operation slot.");
            else Check(receipts.All(d => !d.HasDisperseNotification), "An unrelated hit must not acquire a disperse notification.");
            if (name == "move") Check(target.X != 6 || target.Y != 5, "Configured displacement did not execute.");
            if (name == "child") Check(step.SkillCasts.Any(c => c.SkillEntityId == 10100), "Configured child skill did not execute.");
            if (hitListener != null) Check(hitListener.InvokeCount == (name == "hit-listener" ? 1 : 0)
                && damageListener.RoundTriggeredCount == 0, name + ": generic hit listener vs damage/cure listeners.");
            if (name.StartsWith("original-"))
            {
                Check(receipts.Any(d => d.DamageEntityId > 0 && d.IsDamageTriggerStatus),
                    name + ": original status list not applied.");
                Check(stats.BuffApplications == 0 && stats.DebuffApplications == 0,
                    "Original StatusType.None states are not reclassified as a buff or debuff.");
            }
            string json = FightBattleCheckpointSerializer.Save(cp);
            var restored = FightBattleCheckpointSerializer.Load(json);
            Check(FightBattleCheckpointSerializer.Save(restored) == json, name + ": checkpoint round trip changed effects or receipts.");
            Check(restored.Statistics.Skill(1, skill.EntityId, skill.ItemId).BuffApplications == stats.BuffApplications,
                "Restoring a checkpoint must not count a successful application twice.");
            var report = FightBattleReportBuilder.Build(restored).Units.Single(u => u.UnitId == 1);
            // Selected original settings award 5 per buff/ordinary debuff and
            // 10 per abnormal debuff. None of these fixtures breaks resilience.
            long expectedScore = name.EndsWith("listener") || name.StartsWith("original-") || name == "failed"
                ? 0 : name == "abnormal" || name == "renewal" || name == "mixed" ? 10 : 5;
            Check(scores.TryGetApplyStatusScore(report, out long score) && score == expectedScore,
                name + ": real None/mixed applications must reach the original weighted player status score.");
            File.WriteAllText(Path.Combine(output, name + ".json"), json);
        }
    }

    private static FightBattleUnitState Unit(int id, int x)
    {
        var unit = new FightBattleUnitState { Id = id, Side = id == 1 ? 1 : 2, CharacterType = id == 1 ? 1 : 2,
            X = x, Y = 5, Hp = 1000000, MaxHp = 1000000, Level = 150, SubRank = 1, MoveMask = 5,
            Direction = 3, HasFlipX = true, Turn = new FightTurnUnit { Id = id, Hp = 1000000, Speed = 100 - id } };
        foreach (var pair in new[] { new[] { 1, 10001 }, new[] { 2, 10003 }, new[] { 3, 1000000 }, new[] { 4, 100 - id } })
            unit.Properties.Add(new FightPropertyValue { Id = pair[0], Value = pair[1] });
        return unit;
    }
}
