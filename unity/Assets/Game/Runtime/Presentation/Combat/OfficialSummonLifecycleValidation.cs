using System;
using System.Linq;
using Zjcs.Combat.Official;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    public static class OfficialSummonLifecycleValidation
    {
        public static Probe Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var attack = player.Skills.First(value => value.Slot == 0);
            foreach (var unit in checkpoint.Units)
            {
                foreach (var property in unit.Properties.Concat(unit.BaseProperties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 5000000;
                    if (unit != player && property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = 5000000;
            }
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            var summon = catalog.CreateSkill(12140, 0, 10095, 5, 20, 1, null);
            summon.LimitedTimes = 1;
            player.Skills.Add(summon); player.SkillState.Slots.Add(catalog.CreateSkillSlot(summon, 0));
            player.Statuses.Add(catalog.CreateStatus(25012, 1, 1, 9925012, 0, 0, 5, 20, 1));
            foreach (int id in new[] { 25068, 25112, 25281 })
                player.Statuses.Add(catalog.CreateStatus(id, 1, 1, 9900000 + id, 0, 0, 5, 20, 1));
            var removal = catalog.CreateStatus(25015, 1, 1, 9925015, 0, 0, 5, 20, 1);
            player.Statuses.Add(removal);
            var interval = catalog.CreateStatus(24387, 1, 1, 9924387, 0, 0, 5, 20, 1);
            player.Statuses.Add(interval);
            var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var intervalSave = saved.Units.Single(value => value.Id == 1).Statuses
                .Single(value => value.InstanceId == interval.InstanceId).Components.RoundInterval;
            Require(intervalSave.DefinitionResolved && !intervalSave.HasUnsupportedFilter
                && intervalSave.ExcludedApplyCharacterTypes.SequenceEqual(new[] { 8 })
                && intervalSave.TriggeredCount == 0 && intervalSave.LastTriggeredRound == -1,
                "Runtime checkpoint lost the original round interval applicator filter.");
            return new Probe(catalog, attack, removal, interval);
        }

        public sealed class Probe
        {
            private readonly FightOfficialCombatCatalog catalog;
            private readonly FightBattleSkillDefinition attack;
            private readonly FightBattleStatusState removal;
            private readonly FightBattleStatusState interval;
            private int observedIntervalCount, observedIntervalRound = -1;
            private int summonId, calls;
            private FightBattleStatusState healing;
            private bool healed;
            private bool summonTagsVerified;
            private readonly bool checkConditional = Environment.GetEnvironmentVariable("ZJCS_CONDITIONAL_HIT_ACCEPTANCE") == "1";
            private FightBattleStatusState conditional;
            private bool conditionalVerified;
            public bool ReadyToCapture => healed && (!checkConditional || conditionalVerified);
            public Probe(FightOfficialCombatCatalog catalog,
                FightBattleSkillDefinition attack, FightBattleStatusState removal, FightBattleStatusState interval)
            { this.catalog = catalog; this.attack = attack; this.removal = removal; this.interval = interval; }

            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var player = cp.Units.Single(value => value.Id == 1);
                var intervalState = interval.Components.RoundInterval;
                if (intervalState.TriggeredCount != observedIntervalCount)
                {
                    Require(intervalState.TriggeredCount == observedIntervalCount + 1
                        && intervalState.LastTriggeredRound > observedIntervalRound,
                        "Original round interval trigger executed more than once in the same listener round.");
                    observedIntervalCount = intervalState.TriggeredCount;
                    observedIntervalRound = intervalState.LastTriggeredRound;
                }
                if (summonId == 0)
                {
                    var summon = cp.Units.FirstOrDefault(value => value.MasterId == 1);
                    if (summon == null) return;
                    summonId = summon.Id;
                    Require(summon.Statuses.Any(value => value.ClassId == 24070),
                        "Official 25012 did not apply its creation status to the real summon.");
                    Require(summon.MonsterTemplateId == 3196
                        && summon.Statuses.Any(value => value.ClassId == 24071)
                        && !summon.Statuses.Any(value => value.ClassId == 24072)
                        && summon.Statuses.Any(value => value.ClassId == 24206),
                        "Official summon entity tags did not select 24071/24206 and exclude 24072.");
                    var tagSave = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                    foreach (int id in new[] { 25068, 25112, 25281 })
                    {
                        var tag = tagSave.Units.Single(value => value.Id == 1).Statuses
                            .Single(value => value.ClassId == id).Components.HitSummon;
                        Require(tag.DefinitionResolved && tag.HasSummonEntityTagFilter && !tag.HasUnsupportedFilter
                            && tag.SummonEntityTagQuery.Count == 1 && tag.TriggeredCount == (id == 25112 ? 0 : 1),
                            "Runtime checkpoint lost the summon tag definition or trigger count.");
                    }
                    summonTagsVerified = true;
                    Debug.Log("[HitSummon tags] entity 3196: 25068/25281 matched, 25112 excluded; runtime checkpoint retained queries and counts.");
                    var next = cp.Timeline.Entries.Single(value => value.Id == player.Id);
                    Require(interval.Components.RoundInterval.TriggeredCount == 1
                        && Math.Abs((next.Next - cp.Timeline.Current) / (double)player.Turn.IntervalTicks - .8) < .00001,
                        "Original 24387 did not advance the public player's next action by 20% exactly once.");
                    Debug.Log("[Round interval filter] original 24387 ApplyEntity accepted player, advanced actual timeline by 20% once; runtime checkpoint retained exclusion of substitute type 8.");
                    // Isolated fixture: official 12202 supplies a finite creator-clock lifetime.
                    // No business callback or removal flag is invoked by this probe.
                    var life = catalog.CreateStatus(12202, summonId, 1, 9912202, 12140, 0, 5, 20, 1);
                    FightStatusLifecycle.Apply(summon, life, player.SkillState.Round, life.InstanceId);
                    summon.Skills.Clear(); summon.SkillState.Slots.Clear();
                }
                var step = cp.Receipts.Last().Event;
                foreach (var cast in step.SkillCasts.Where(value => value.SkillEntityId == 11020))
                {
                    calls++;
                    var summon = cp.Units.Single(value => value.Id == summonId);
                    Require(cast.ActorId == 1 && cast.Presentation.TargetX == summon.X
                        && cast.Presentation.TargetY == summon.Y && summon.Ended
                        && summon.HitSummonRemoveProcessed && calls == 1,
                        "Official removal cast lost its summoner, removed position or single receipt.");
                    Require(step.DamageEvents.Where(value => value.SkillEntityId == 11020)
                        .All(value => value.SourceId == 1 && cp.Units.Single(unit => unit.Id == value.TargetId).Side != player.Side),
                        "Removal skill affected an ally or lost its summoner damage source.");
                    var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                    Require(restored.Units.Single(value => value.Id == summonId).HitSummonRemoveProcessed
                        && restored.Units.Single(value => value.Id == 1).Statuses
                            .Single(value => value.InstanceId == removal.InstanceId).Components.HitSummon.TriggeredCount == 1,
                        "Runtime checkpoint lost the removal receipt or component counter.");
                    player.Skills.Clear(); player.SkillState.Slots.Clear();
                    // 12040 is the healing child of official passive item 10597 (status 25098).
                    var heal = catalog.CreateSkill(12040, 0, 10597, 5, 20, 1, null);
                    heal.HasCustomCure = true; heal.CustomCure = player.MaxHp / 5;
                    player.Skills.Add(heal); player.SkillState.Slots.Add(catalog.CreateSkillSlot(heal, 0));
                    player.Hp = player.Turn.Hp = player.MaxHp / 2;
                    healing = catalog.CreateStatus(25034, 1, 1, 9925034, 12040, 0, 5, 20, 1);
                    player.Statuses.Add(healing);
                    var layers = catalog.CreateStatus(24242, 1, 1, 9924242, 12040, 0, 5, 20, 1);
                    layers.StackCount = 5;
                    player.Statuses.Add(layers);
                    Debug.Log("[HitSummon hosts] official 12140/25012 creation and 12202 expiry -> 25015/11020, source=1, anchor="
                        + summon.X + "," + summon.Y + ", receipt persisted.");
                }
                if (healing != null && !healed && step.DamageEvents.Any(value => value.IsCure && value.SkillEntityId == 12040))
                {
                    var change = step.DamageEvents.Where(value => value.IsCure && value.SkillEntityId == 12040)
                        .Sum(value => value.AfterHp - value.BeforeHp);
                    Require(change > 0 && healing.Components.HpIncreaseUnit.TriggeredCount > 0,
                        "Original healing skill did not deliver its actual HP increase.");
                    Require((player.Statuses.SingleOrDefault(value => value.ClassId == 24242)?.StackCount ?? 0) < 5,
                        "Healing did not execute the confirmed 25131 layer reduction.");
                    var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                    Require(saved.Units.Single(value => value.Id == 1).Statuses.Single(value => value.InstanceId == healing.InstanceId)
                        .Components.HpIncreaseUnit.AccumulatedHp == healing.Components.HpIncreaseUnit.AccumulatedHp,
                        "Healing remainder did not survive a runtime checkpoint.");
                    healed = true;
                    player.Skills.Clear(); player.SkillState.Slots.Clear();
                    player.Skills.Add(attack); player.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
                    foreach (var enemy in cp.Units.Where(value => value.Side != player.Side))
                        enemy.Hp = enemy.Turn.Hp = Math.Min(enemy.Hp, 1);
                    if(checkConditional)
                    {
                        conditional=catalog.CreateStatus(60290,1,1,9960290,attack.EntityId,0,5,20,1);
                        player.Statuses.Add(conditional);
                    }
                    Debug.Log("[HP increase hosts] official 12040 -> 25034/25131/24242, actual=" + change
                        + ", count=" + healing.Components.HpIncreaseUnit.TriggeredCount
                        + ", remainder=" + healing.Components.HpIncreaseUnit.AccumulatedHp);
                }
                if(checkConditional && healed && !conditionalVerified)
                {
                    if(conditional.Components.HitDmgAddPer.InvokeCount>0)
                    {
                        Require(step.DamageEvents.Any(value=>value.SourceId==1 && !value.IsCure && value.Damage<0),
                            "Conditional status counted without an actual damage settlement.");
                        conditionalVerified=true;
                        Debug.Log("[Conditional hit hosts] official 60290 target-debuff gate, count="
                            + conditional.Components.HitDmgAddPer.InvokeCount);
                    }
                    else foreach(var enemy in cp.Units.Where(value=>value.Side!=player.Side && value.Hp>0))
                        if(!enemy.Statuses.Any(value=>value.ClassId==24009))
                        {
                            var debuff=catalog.CreateStatus(24009,enemy.Id,1,9924009+enemy.Id,
                                attack.EntityId,0,5,20,1);
                            FightStatusLifecycle.Apply(enemy,debuff,enemy.SkillState.Round,debuff.InstanceId);
                        }
                }
            }

            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(summonId > 0 && calls == 1 && removal.Components.HitSummon.TriggeredCount == 1 && healed
                    && summonTagsVerified,
                    host + " did not execute the original summon lifecycle exactly once.");
                Require(!checkConditional || conditionalVerified,host+" did not execute the conditional hit status.");
                Require(observedIntervalCount > 0 && interval.Components.RoundInterval.TriggeredCount == observedIntervalCount,
                    host + " did not execute and observe the original round interval filter once per eligible round.");
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

    }
}
