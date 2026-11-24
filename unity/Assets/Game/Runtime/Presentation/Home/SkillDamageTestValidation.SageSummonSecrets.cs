using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private sealed class AcquiredSageSummonSecret
        {
            private readonly int item;
            private readonly string output;
            private readonly Dictionary<int, long> enemyAttack = new Dictionary<int, long>();
            private int rank, observations, cures;
            private bool restored, captured;

            internal AcquiredSageSummonSecret(int item, string output)
            { this.item = item; this.output = output; }

            internal void Observe(FightBattleStepEvent step, FightBattleCheckpoint checkpoint,
                AcquiredRockSummon rock)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                var listener = actor.Statuses.Single(status => status.ClassId == (item == 10591 ? 25104 : 25105)
                    && status.ApplyingSkillItemId == item);
                rank = listener.SourceSkillRank;
                var summons = checkpoint.Units.Where(unit => unit.CharacterType == 3
                    && unit.MasterId == actor.Id && unit.Hp > 0 && !unit.Ended).ToArray();
                if (item == 10591)
                {
                    var gains = actor.Statuses.Where(status => status.ClassId == 24133).ToArray();
                    Require(gains.Length == (summons.Length > 0 ? 1 : 0),
                        "Summon resonance did not follow the actual number of living owned summons.");
                    if (gains.Length == 0) return;
                    var gain = gains[0];
                    var assets = OfficialCombatAssetCatalog.LoadDefault();
                    var formula = assets.LoadDamageFormula();
                    var props = assets.Load().CalculateSkillProps(24133, rank, 2, actor.SubRank)
                        .ToDictionary(pair => pair.Key, pair => pair.Value);
                    int attackId = formula.PropertyId("Attack");
                    long baseline = actor.BaseProperties.Single(value => value.Id == attackId).Value;
                    long reference = baseline + (actor.BaseProperties.FirstOrDefault(value =>
                        value.Id == formula.PropertyId("BaseAttack"))?.Value ?? 0);
                    long expected = Math.Max(1L, baseline + (long)(reference
                        * formula.ReadProperty(props, formula.PropertyId("AttackScale"))))
                        + (long)formula.ReadProperty(props, formula.PropertyId("FixedAttack"));
                    Require(actor.Properties.Single(value => value.Id == attackId).Value == expected
                        && gain.CreatorId == actor.Id && gain.OwnerId == actor.Id && gain.StackCount == 1
                        && gain.ApplyingSkillItemId == item && gain.SourceSkillRank == rank
                        && gain.SourceSkillLevel == 2 && gain.SourceSubRank == actor.SubRank
                        && gain.DurationRound < 0 && !gain.CanDispersed,
                        "The real cultivated resonance changed its attack, source, stacking or lifetime.");
                    foreach (var prop in props)
                        Require(gain.StatusProperties.Single(value => value.Id == prop.Key).Value == prop.Value,
                            "The real resonance changed a cultivated status property.");
                    observations++;
                }
                else
                {
                    // Let the actual emperor perform both attacks, then make an
                    // already taunted training enemy lethal. Only test-process
                    // properties change; the normal hit/death/heal path runs.
                    if (summons.Length > 0 && rock.RegularHits > 0 && rock.OpeningHits > 0)
                    {
                        var summon = summons.Single(unit => unit.MonsterTemplateId == 3196);
                        foreach (var enemy in checkpoint.Units.Where(unit => unit.Side != actor.Side
                            && unit.Statuses.Any(status => status.ActionType == 58 && status.CreatorId == summon.Id)))
                        {
                            if (!enemyAttack.ContainsKey(enemy.Id))
                                enemyAttack.Add(enemy.Id, enemy.Properties.Single(value => value.Id == 1).Value);
                            SetAcquiredBattleProperty(enemy, "Attack", checked(summon.MaxHp * 8));
                        }
                    }
                    if (summons.Length == 0)
                        foreach (var pair in enemyAttack)
                            SetAcquiredBattleProperty(checkpoint.Units.Single(unit => unit.Id == pair.Key), "Attack", pair.Value);
                    foreach (var cure in step.DamageEvents.Where(hit => hit.SkillEntityId == 12050))
                    {
                        var source = actor.Properties.ToDictionary(value => value.Id, value => value.Value);
                        var target = new Dictionary<int, long>(source);
                        var assets = OfficialCombatAssetCatalog.LoadDefault();
                        var formula = assets.LoadDamageFormula();
                        foreach (var prop in assets.Load().CalculateSkillProps(12050, rank, 2, actor.SubRank))
                            source[prop.Key] = (source.TryGetValue(prop.Key, out long previous) ? previous : 0) + prop.Value;
                        long offered = formula.CalculateCure(source, target, formula.PropertyId("SkillCureByHp"),
                            formula.PropertyId("SkillFixedCure")).Cure;
                        Require(cure.SourceId == actor.Id && cure.TargetId == actor.Id && cure.SkillItemId == item
                            && cure.HitEntityId == 12051 && cure.DamageEntityId == 12052 && cure.IsCure
                            && cure.HasDamageValue && cure.DamageValue == offered && cure.Damage > 0
                            && cure.AfterHp == Math.Min(cure.MaxHp, cure.BeforeHp + offered)
                            && cure.AfterHp - cure.BeforeHp == cure.Damage
                            && listener.RoleDieMasterType == 2 && listener.RoleDieCharacterTypes.SequenceEqual(new[] { 3 })
                            && checkpoint.Units.Any(unit => unit.MasterId == actor.Id && unit.CharacterType == 3
                                && (unit.Ended || unit.Hp <= 0) && listener.RoleDieSeenUnitIds.Contains(unit.Id)),
                            "The cultivated summon death did not settle its original owner cure and HP cap.");
                        cures++;
                    }
                    if (cures == 0) return;
                    observations++;
                }
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                File.WriteAllText(Path.Combine(output, "companion-sage-summon-" + item + "-checkpoint.json"), saved);
                var copy = FightBattleCheckpointSerializer.Load(saved);
                var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(copy);
                Require(executor.Step(copy.Sequence, copy.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(copy) == saved,
                    "The cultivated summon secret changed its receipt after restore.");
                restored = true;
                WriteReceipt();
            }

            internal bool ReadyCapture(AcquiredRockSummon rock) => !captured && observations > 0
                && (item == 10591 ? rock.IsVisible() && FindObjectsOfType<Transform>().Any(value =>
                    value.name == "24133" || value.name.StartsWith("24133(", StringComparison.Ordinal))
                    : IsNativeSkillParticleVisible("playable12050") || IsNativeSkillParticleVisible("playable12051"));

            internal void Captured() { captured = true; WriteReceipt(); }

            internal void Complete()
            {
                Require(observations > 0 && restored && captured && (item != 10600 || cures > 0),
                    "The cultivated summon secret did not settle, render and restore in the actual battle.");
                WriteReceipt();
                Debug.Log("[Acquired passive combat] " + item
                    + " cultivated summon secret, actual properties, native view and restore replay passed.");
            }

            private void WriteReceipt() => File.WriteAllText(Path.Combine(output,
                "companion-sage-summon-" + item + "-receipt.json"), JsonUtility.ToJson(new SageSummonSecretReceipt {
                    item = item, rank = rank, level = 2, observations = observations, cures = cures,
                    restored = restored, captured = captured
                }, true));
        }

        [Serializable]
        private sealed class SageSummonSecretReceipt
        {
            public int item, rank, level, observations, cures;
            public bool restored, captured;
        }
    }
}
