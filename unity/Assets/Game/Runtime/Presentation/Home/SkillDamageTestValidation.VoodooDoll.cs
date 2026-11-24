using System;
using System.Collections.Generic;
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
        private sealed class AcquiredVoodooDoll
        {
            private readonly Dictionary<int, int> expiryRounds = new Dictionary<int, int>();
            private readonly HashSet<int> expired = new HashSet<int>();
            private int transfers;
            private bool restored, captured;
            private bool modelSeen, linkSeen;

            internal void VerifySummon(FightBattleSkillCastEvent cast, FightBattleCheckpoint checkpoint,
                FightBattleUnitState actor, FightBattleSkillDefinition skill, string output)
            {
                var spawn = cast.HitSelections.Single(selection => selection.HitEntityId == 12201).SpawnedUnits.Single();
                var doll = checkpoint.Units.Single(unit => unit.Id == spawn.UnitId);
                var target = checkpoint.Units.Single(unit => unit.Id == doll.VoodooDoll.TargetId);
                var lifetime = doll.Statuses.Single(status => status.ClassId == 12202);
                Require(cast.SkillEntityId == 12200 && doll.IsVoodooDoll && doll.MonsterId == 4600
                    && doll.MasterId == actor.Id && doll.CharacterType == target.CharacterType
                    && doll.Side == target.Side && doll.Level == skill.Level
                    && Math.Abs(spawn.Delay - .35f) < .0001f
                    && cast.Presentation != null
                    && Math.Abs(spawn.X - cast.Presentation.SourceX)
                        + Math.Abs(spawn.Y - cast.Presentation.SourceY) == 2
                    && spawn.InitialHp == spawn.InitialMaxHp && spawn.InitialHp > 0
                    && lifetime.CreatorId == actor.Id && lifetime.DurationRound == 3
                    && lifetime.SourceSkillRank == skill.Rank && lifetime.SourceSkillLevel == skill.Level,
                    "The acquired substitute changed its inherited type, spawn, source or three-turn lifetime: unit="
                        + doll.Id + ", spawn=" + spawn.X + "/" + spawn.Y + ", source="
                        + cast.Presentation?.SourceX + "/" + cast.Presentation?.SourceY
                        + ", delay=" + spawn.Delay + ", duration=" + lifetime.DurationRound + ".");
                var inheritance = OfficialCombatAssetCatalog.LoadDefault().Load().CalculateSummonInheritance(
                    4600, skill.Level, skill.Rank, skill.SubRank,
                    target.Properties.ToDictionary(value => value.Id, value => value.Value));
                foreach (int id in inheritance.Fixed.Keys.Union(inheritance.Summoner.Keys))
                {
                    long expected = (inheritance.Fixed.TryGetValue(id, out long fixedValue) ? fixedValue : 0)
                        + (inheritance.Summoner.TryGetValue(id, out long inheritedValue) ? inheritedValue : 0);
                    if (id == 3) expected = Math.Max(1L, expected);
                    Require(doll.BaseProperties.Single(value => value.Id == id).Value == expected,
                        "The real substitute changed a current cultivated inheritance property.");
                }
                expiryRounds.Add(doll.Id, actor.SkillState.Round + 3);
                Restore(checkpoint, output);
            }

            internal int Observe(FightBattleStepEvent step, FightBattleCheckpoint checkpoint, string output)
            {
                int hits = 0;
                foreach (var transfer in step.DamageEvents.Where(hit => hit.SkillEntityId == 10140))
                {
                    var doll = checkpoint.Units.Single(unit => unit.Id == transfer.SourceId);
                    var suffered = step.DamageEvents.Single(hit => hit.TargetId == doll.Id
                        && hit.HasDamageValue && hit.DamageValue < 0 && hit.Delay == transfer.Delay);
                    Require(doll.IsVoodooDoll && transfer.TargetId == doll.VoodooDoll.TargetId
                        && transfer.HitEntityId == 10141 && transfer.DamageEntityId == 10004
                        && transfer.TrueDamage && transfer.IgnoreShield && !transfer.IsCure
                        && transfer.DamageValue == suffered.DamageValue
                        && transfer.AfterHp == Math.Max(0L, transfer.BeforeHp + suffered.DamageValue)
                        && transfer.Damage == transfer.AfterHp - transfer.BeforeHp
                        && step.SkillCasts.Any(cast => cast.ActorId == doll.Id && cast.SkillEntityId == 10140),
                        "The actual substitute did not transmit its settled damage through the original true-damage child.");
                    transfers++; hits++;
                }
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                foreach (var pair in expiryRounds)
                {
                    var doll = checkpoint.Units.Single(unit => unit.Id == pair.Key);
                    var target = checkpoint.Units.Single(unit => unit.Id == doll.VoodooDoll.TargetId);
                    if (doll.Ended && doll.Hp > 0 && target.Hp > 0 && !target.Ended)
                    {
                        Require(actor.SkillState.Round >= pair.Value,
                            "The substitute disappeared before its creator's original three-turn lifetime.");
                        expired.Add(doll.Id);
                    }
                }
                // The acquired loadout contains only the summon. After observing
                // one complete natural lifetime, add an original water projectile skill
                // inside this isolated battle to exercise real hostile hits and
                // substitute priority through the normal target/damage pipeline.
                if (expired.Count > 0 && actor.Skills.All(skill => skill.Slot != 1))
                {
                    var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
                    var props = catalog.CalculateSkillProps(10500, 1, 1, actor.SubRank)
                        .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList();
                    var attack = catalog.CreateSkill(10500, 1, 10000, 1, 1, actor.SubRank, props);
                    attack.CreatorCharacterType = actor.CharacterType;
                    actor.Skills.Add(attack);
                    actor.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 1));
                }
                if (hits > 0 || expired.Count > 0) Restore(checkpoint, output);
                return hits;
            }

            internal bool IsVisible()
            {
                // Entity 4600 uses the original summons_3193 model hierarchy.
                bool modelVisible = FindObjectsOfType<BattleNativeMonsterRenderer>().Any(model =>
                    model.name == "MonsterModel_3193" && model.SkeletonDataName == "summons_3193"
                    && expiryRounds.Keys.Any(id => model.EntityRoot.name == "BattleUnit_" + id)
                    && model.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled
                    && model.TryGetComponent<MeshFilter>(out var mesh)
                    && mesh.sharedMesh && mesh.sharedMesh.vertexCount > 0);
                bool linkVisible = IsNativeSkillParticleVisible("playable_Link");
                if (modelVisible && !modelSeen || linkVisible && !linkSeen)
                    Debug.Log("[Acquired substitute view] Original model=" + modelVisible + ", link=" + linkVisible + ".");
                modelSeen |= modelVisible; linkSeen |= linkVisible;
                return modelVisible && linkVisible;
            }

            internal void Captured() { captured = true; }

            internal void Complete(string output)
            {
                Require(expiryRounds.Count > 0 && transfers > 0 && expired.Count > 0 && restored && captured,
                    "The acquired substitute did not render, transmit and expire: summons=" + expiryRounds.Count
                        + ", transfers=" + transfers + ", expirations=" + expired.Count
                        + ", restored=" + restored + ", captured=" + captured + ".");
                File.WriteAllText(Path.Combine(output, "acquired-voodoo-doll-receipt.json"), JsonUtility.ToJson(
                    new VoodooDollReceipt { summons = expiryRounds.Count, transfers = transfers,
                        expirations = expired.Count, restored = restored, captured = captured }, true));
                Debug.Log("[Acquired active combat] 10097 cultivated substitute, inherited properties, original link, true damage, lifetime and restore replay passed.");
            }

            private void Restore(FightBattleCheckpoint checkpoint, string output)
            {
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                File.WriteAllText(Path.Combine(output, "acquired-voodoo-doll-checkpoint.json"), saved);
                var copy = FightBattleCheckpointSerializer.Load(saved);
                var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(copy);
                Require(executor.Step(copy.Sequence, copy.Counter).Replayed
                    && FightBattleCheckpointSerializer.Save(copy) == saved,
                    "The real substitute repeated a spawn, HP transfer or expiry after restore.");
                restored = true;
            }
        }

        [Serializable] private sealed class VoodooDollReceipt
        {
            public int summons, transfers, expirations;
            public bool restored, captured;
        }
    }
}
