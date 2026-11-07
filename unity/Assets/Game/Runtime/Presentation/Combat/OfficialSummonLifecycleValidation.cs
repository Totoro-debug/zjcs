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
            var removal = catalog.CreateStatus(25015, 1, 1, 9925015, 0, 0, 5, 20, 1);
            player.Statuses.Add(removal);
            return new Probe(catalog, attack, removal);
        }

        public sealed class Probe
        {
            private readonly FightOfficialCombatCatalog catalog;
            private readonly FightBattleSkillDefinition attack;
            private readonly FightBattleStatusState removal;
            private int summonId, calls;
            private FightBattleStatusState healing;
            private bool healed;
            public bool ReadyToCapture => healed;
            public Probe(FightOfficialCombatCatalog catalog,
                FightBattleSkillDefinition attack, FightBattleStatusState removal)
            { this.catalog = catalog; this.attack = attack; this.removal = removal; }

            public void ObserveStep(OfficialBattleRuntimeController controller)
            {
                var cp = controller.Checkpoint;
                var player = cp.Units.Single(value => value.Id == 1);
                if (summonId == 0)
                {
                    var summon = cp.Units.FirstOrDefault(value => value.MasterId == 1);
                    if (summon == null) return;
                    summonId = summon.Id;
                    Require(summon.Statuses.Any(value => value.ClassId == 24070),
                        "Official 25012 did not apply its creation status to the real summon.");
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
                    Debug.Log("[HP increase hosts] official 12040 -> 25034/25131/24242, actual=" + change
                        + ", count=" + healing.Components.HpIncreaseUnit.TriggeredCount
                        + ", remainder=" + healing.Components.HpIncreaseUnit.AccumulatedHp);
                }
            }

            public void Verify(string host, FightBattleCheckpoint checkpoint)
            {
                Require(summonId > 0 && calls == 1 && removal.Components.HitSummon.TriggeredCount == 1 && healed,
                    host + " did not execute the original summon lifecycle exactly once.");
            }
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }

    }
}
