using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private sealed class AcquiredRockSummon
        {
            private int summonId;
            internal int RegularHits { get; private set; }
            internal int OpeningHits { get; private set; }

            internal void VerifySummon(FightBattleSkillCastEvent cast, FightBattleStepEvent step,
                FightBattleCheckpoint checkpoint, FightBattleUnitState actor, FightBattleSkillDefinition skill,
                string output)
            {
                Require(summonId == 0 && step.SpawnedUnitIds.Count == 1 && skill.LimitedTimes == 1,
                    "The cultivated rock emperor did not summon once in its original wave.");
                summonId = step.SpawnedUnitIds[0];
                var hit = cast.HitSelections.Single(selection => selection.HitEntityId == 12141);
                var spawn = hit.SpawnedUnits.Single();
                var summon = checkpoint.Units.Single(unit => unit.Id == summonId);
                Require(spawn.UnitId == summonId && Math.Abs(spawn.Delay - .8f) < .0001f
                    && Math.Abs(spawn.X - actor.X) + Math.Abs(spawn.Y - actor.Y) == 2
                    && spawn.InitialHp == spawn.InitialMaxHp && spawn.InitialHp > 0
                    && summon.MasterId == actor.Id && summon.MonsterTemplateId == 3196
                    && summon.Side == actor.Side && summon.Level == skill.Level && summon.Statuses.Any(status =>
                        status.ClassId == 12142 && status.CreatorId == actor.Id && status.DurationRound < 0
                        && status.SourceSkillRank == skill.Rank && status.SourceSkillLevel == skill.Level),
                    "The cultivated rock emperor changed its original position, timing, HP or lifecycle.");
                string serialized = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(serialized);
                var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
                Require(executor.Step(restored.Sequence, restored.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(restored) == serialized,
                    "Restoring the cultivated rock emperor changed its created unit or receipt.");
                File.WriteAllText(Path.Combine(output, "acquired-rock-emperor-checkpoint.json"), serialized);
            }

            internal int VerifyAttacks(FightBattleStepEvent step, FightBattleCheckpoint checkpoint)
            {
                if (summonId == 0) return 0;
                int hits = 0;
                foreach (var damage in step.DamageEvents.Where(value => value.SourceId == summonId
                    && (value.SkillEntityId == 56800 || value.SkillEntityId == 56810)))
                {
                    bool opening = damage.SkillEntityId == 56810;
                    Require(damage.HitEntityId == (opening ? 56811 : 56801)
                        && damage.DamageEntityId == (opening ? 56812 : 56802)
                        && damage.DamageType == 1 && damage.ElementType == 0 && !damage.IsCure && !damage.TrueDamage,
                        "The original rock emperor changed an attack entity or its physical damage.");
                    if (damage.Damage >= 0) continue;
                    if (opening) OpeningHits++; else RegularHits++;
                    hits++;
                }
                foreach (var view in step.StatusViewEvents.Where(value =>
                    value.Kind == FightBattleStatusViewEventKind.Start && value.Status.ClassId == 24216
                    && value.Status.CreatorId == summonId && value.Status.InstanceId > 0))
                {
                    var status = checkpoint.Units.Single(unit => unit.Id == view.OwnerId).Statuses
                        .SingleOrDefault(value => value.InstanceId == view.Status.InstanceId);
                    if (status == null) continue;
                    Require(status.DurationRound == 1 && status.ActionType == 58 && status.CanDispersed,
                        "The rock emperor's original taunt changed action, duration or dispersal.");
                }
                return hits;
            }

            internal bool IsVisible() => summonId > 0
                && FindObjectsOfType<BattleNativeMonsterRenderer>().Any(model =>
                    model.name == "MonsterModel_3196" && model.EntityRoot.name == "BattleUnit_" + summonId
                    && model.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled
                    && model.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh
                    && filter.sharedMesh.vertexCount > 0);

            internal bool IsAttackVisible() => IsNativeSkillParticleVisible("playable56800")
                || IsNativeSkillParticleVisible("playable56810");
        }
    }
}
