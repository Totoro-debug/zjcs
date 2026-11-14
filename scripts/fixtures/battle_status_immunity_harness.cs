using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStatusImmunityHarness
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, FightBattleReportScoreSettings scores, string output)
    {
        Directory.CreateDirectory(output);
        foreach (string name in new[] { "single", "unlimited-negative", "unlimited-zero", "specific-id", "other-id",
            "buff", "failed", "renewal", "forbidden", "dispersible", "indispersible", "self", "friend", "enemy",
            "self-other", "friend-enemy", "two", "exhausted", "restore", "replacement", "legacy-definition", "copy", "initial" })
        {
            var cp = new FightBattleCheckpoint { StageId = 1, RandomSeed = 11,
                Grid = new FightBattleGrid { Width = 16, Height = 16 } };
            var source = Unit(1, 5); var target = Unit(2, 6); var other = Unit(3, 12);
            var protection = catalog.CreateStatus(name == "unlimited-negative" ? 25243
                : name == "unlimited-zero" || name == "replacement" ? 60640
                : name == "specific-id" || name == "other-id" ? 24671 : 24141, 2, 2,
                replacementProperties: name == "replacement" ? new[] {
                    new FightStatusReplacementValue { PropertyId = 5001, RawValue = 2 } } : null);
            var condition = protection.Components.ApplyCondition;
            Check(condition != null && condition.DefinitionResolved, name + ": original definition missing");
            if (name == "forbidden") protection.ForbiddenType = FightStatusLifecycle.ForbiddenStatusTrigger;
            if (name == "dispersible" || name == "indispersible") condition.CheckCanDisperse = true;
            if (name == "self" || name == "self-other") condition.CreatorType = 1;
            if (name == "friend" || name == "friend-enemy") condition.CreatorType = 3;
            if (name == "self" || name == "friend") target.Side = source.Side;
            if (name == "self") { source.Statuses.Add(protection); protection.OwnerId = 1; }
            else target.Statuses.Add(protection);
            if (name == "two" || name == "restore") { condition.MaxCount = 2; condition.DestroyOnMaximum = false; }
            if (name == "exhausted") { condition.MaxCount = 1; condition.RejectedCount = 1; condition.DestroyOnMaximum = false; }
            if (name == "restore") condition.RejectedCount = 1;
            if (name == "renewal") {
                var existing = catalog.CreateStatus(24436, 2, 1, rootSkillId: 930, applyingSkillItemId: 931);
                target.Statuses.Add(existing);
            }
            var skill = new FightBattleSkillDefinition { EntityId = 930, ItemId = 931, Slot = 0,
                TargetType = name == "self" || name == "friend" ? 2 : 1, Duration = 2, ActionDuration = 2 };
            skill.Range.Add(new FightGridPoint(1, 0));
            for (int i = 0; i < 3; i++) {
                var hit = new FightBattleHitDefinition { EntityId = 932 + i, DamageId = 942 + i, Delay = .25f + i * .25f,
                    ActionType = 0, DamageType = 8, TargetMode = 0 };
                hit.AppliedStatuses.Add(new FightBattleStatusApplication {
                    StatusId = name == "indispersible" ? 24052 : name == "specific-id" ? 24670 : name == "other-id" ? 24010 : name == "buff" ? 24031 : 24436,
                    TargetType = name == "self" ? 1 : 0, Chance = name == "failed" ? 0 : 1,
                    AffectedByProp = false, LessThanHpPer = -1 });
                skill.Hits.Add(hit);
            }
            source.Skills.Add(skill); source.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            cp.Units.AddRange(new[] { source, target, other });
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(new[] { 2, 3 }); cp.Waves.Add(wave);
            if (name == "initial") {
                condition.CreatorType = 1;
                cp.StageStatus = new FightBattleStageStatusConfig();
                target.CharacterType = 99;
                var group = new FightBattleStatusGroup { Key = 99 }; group.Status.Add(24436);
                cp.StageStatus.CharacterStatus.Add(group);
            }
            if (name == "copy") {
                var copied = protection.Components.Copy();
                copied.ApplyCondition.StatusTypes.Clear();
                Check(condition.StatusTypes.Count == 2, "Copied protection shares mutable filters");
            }
            if (name == "legacy-definition") protection.Components.ApplyCondition = null;
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            if (name == "legacy-definition") condition = protection.Components.ApplyCondition;
            if (name == "initial") {
                Check(!target.Statuses.Contains(protection) && target.StatusImmunityActionPendingCount == 1, "Initial real application escaped immunity");
                cp = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                executor = FightBattleExecutor.Restore(cp, formula, new FightDamageSettlement(), catalog, items);
            }
            if (name == "restore") {
                cp = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                executor = FightBattleExecutor.Restore(cp, formula, new FightDamageSettlement(), catalog, items);
                condition = cp.Units.Single(u => u.Id == 2).Statuses.Single(s => s.ClassId == 24141).Components.ApplyCondition;
            }
            var step = executor.Step(0, 1).Event;
            var observedActions = new List<FightBattleStatusCustomActionEvent>(step.StatusCustomActions);
            for (int i = 0; name == "initial" && !step.DamageEvents.Any(d => d.SkillEntityId == 930) && i < 8; i++) {
                step = executor.Step(cp.Sequence, cp.Counter + 1).Event;
                observedActions.AddRange(step.StatusCustomActions);
            }
            var receipts = step.DamageEvents.Where(d => d.SkillEntityId == 930).ToArray();
            Check(receipts.Length == 3, name + ": missing status hits");
            int refusals = name == "unlimited-negative" || name == "unlimited-zero" || name == "specific-id" ? 3
                : name == "two" || name == "replacement" ? 2
                : name == "single" || name == "renewal" || name == "dispersible" || name == "self"
                    || name == "friend" || name == "enemy" || name == "restore" || name == "legacy-definition" || name == "copy" ? 1 : 0;
            int accepted = name == "failed" ? 0 : 3 - refusals;
            Check(receipts.Count(d => !d.IsDamageTriggerStatus) == 3 - accepted, name + ": successful application flag");
            var stats = cp.Statistics.Skill(1, 930, 931);
            Check(stats.BuffApplications + stats.DebuffApplications == accepted, name + ": refused status entered statistics");
            var report = FightBattleReportBuilder.Build(cp).Units.Single(u => u.UnitId == 1);
            Check(scores.TryGetApplyStatusScore(report, out long applyScore)
                && applyScore == accepted * (name == "other-id" ? 10 : 5), name + ": refused application entered report score");
            var actions = observedActions.Where(a => a.Action == 1).ToArray();
            Check(actions.Length == (name == "initial" ? 1 : name == "specific-id" ? 0 : refusals), name + ": original immunity float suppression/count");
            if (name == "initial") Check(actions[0].SourceDamageId == 0 && !actions[0].HasDelay && cp.Units.Single(u => u.Id == 2).StatusImmunityActionPendingCount == 0, "Initial pending immunity replayed or lost");
            for (int i = 0; name != "initial" && i < actions.Length; i++) Check(actions[i].SourceDamageId == receipts[i].DamageId
                && actions[i].SourceDamageTargetId == receipts[i].TargetId
                && actions[i].HasDelay && Math.Abs(actions[i].Delay - receipts[i].Delay) < .00001f,
                name + ": causal hit association and actual refusal time");
            if (condition.MaxCount <= 0) Check(condition.RejectedCount == 0, name + ": unlimited protection consumed counter");
            else Check(condition.RejectedCount == (name == "exhausted" || name == "restore" || name == "initial" ? 1 : 0) + refusals,
                name + ": actual refusal count");
            bool remains = cp.Units.Any(u => u.Statuses.Contains(protection));
            if (name != "restore" && name != "initial") Check(remains == !(condition.MaxCount > 0 && condition.DestroyOnMaximum
                && condition.RejectedCount >= condition.MaxCount), name + ": exhausted protection lifetime");
            Check(cp.Units.All(u => u.StatusApplicationRejected == null), name + ": runtime callback retained");
            string json = FightBattleCheckpointSerializer.Save(cp);
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
