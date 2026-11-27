using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static string affinityPreviousCheckpoint;
        private static long acquiredAffinityBonus;

        private static void PrepareAcquiredAffinity(FightBattleCheckpoint checkpoint, int itemId)
        {
            affinityPreviousCheckpoint = itemId == 10528 ? FightBattleCheckpointSerializer.Save(checkpoint) : null;
        }

        private static void VerifyAcquiredAffinityStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output, int itemId, int element)
        {
            var hits = step.DamageEvents.Where(hit => hit.SourceId == 1 && hit.ElementType == element
                && !hit.IsCure && !hit.TrueDamage && hit.Damage < 0).ToArray();
            if (itemId == 10528)
            {
                Require(affinityPreviousCheckpoint != null && acquiredAffinityBonus > 0,
                    "The affinity comparison requires the actual cultivated passive and previous checkpoint.");
                if (hits.Length > 0)
                {
                    var control = FightBattleCheckpointSerializer.Load(affinityPreviousCheckpoint);
                    var actor = control.Units.Single(unit => unit.Id == 1);
                    foreach (var properties in new[] { actor.Properties, actor.BaseProperties }
                        .Where(value => value != null).Distinct())
                        properties.Single(value => value.Id == 414).Value -= acquiredAffinityBonus;
                    var assets = OfficialCombatAssetCatalog.LoadDefault();
                    FightStatusPropertyRuntime.RebuildAll(control.Units, assets.Load(), assets.LoadDamageFormula());
                    var controlExecutor = assets.RestoreBattle(control);
                    double elapsed = (checkpoint.StatusSeconds?.ElapsedSeconds ?? 0d)
                        - (control.StatusSeconds?.ElapsedSeconds ?? 0d);
                    Require(elapsed >= 0d, "The affinity comparison cannot move the gameplay clock backwards.");
                    controlExecutor.AdvanceStatusSeconds(elapsed);
                    var comparison = controlExecutor.Step(control.Sequence, control.Counter + 1).Event;
                    var plainHits = comparison.DamageEvents.Where(hit => hit.SourceId == 1
                        && hit.ElementType == element && !hit.IsCure && !hit.TrueDamage && hit.Damage < 0).ToArray();
                    if (plainHits.Length != hits.Length)
                    {
                        File.WriteAllText(Path.Combine(output, "companion-affinity-before-failure.json"), affinityPreviousCheckpoint);
                        File.WriteAllText(Path.Combine(output, "companion-affinity-actual-failure.json"),
                            FightBattleCheckpointSerializer.Save(checkpoint));
                        File.WriteAllText(Path.Combine(output, "companion-affinity-control-failure.json"),
                            FightBattleCheckpointSerializer.Save(control));
                    }
                    Require(plainHits.Length == hits.Length,
                        "The affinity control changed the original elemental hit count.");
                    for (int index = 0; index < hits.Length; index++)
                    {
                        var hit = hits[index];
                        var plain = plainHits[index];
                        Require(plain.SourceId == hit.SourceId && plain.TargetId == hit.TargetId
                            && plain.SkillItemId == hit.SkillItemId && plain.SkillEntityId == hit.SkillEntityId
                            && plain.HitEntityId == hit.HitEntityId && plain.DamageEntityId == hit.DamageEntityId
                            && plain.Delay == hit.Delay && plain.HasHitGridPosition == hit.HasHitGridPosition
                            && plain.HitGridX == hit.HitGridX && plain.HitGridY == hit.HitGridY
                            && hit.DamageValue < plain.DamageValue,
                            "Removing the real cultivated affinity did not lower the same original elemental hit.");
                    }
                    File.WriteAllText(Path.Combine(output, "companion-affinity-control-checkpoint.json"),
                        FightBattleCheckpointSerializer.Save(control));
                }
                affinityPreviousCheckpoint = FightBattleCheckpointSerializer.Save(checkpoint);
            }
            if (hits.Length == 0) return;
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            Require(FightBattleCheckpointSerializer.Save(restored) == state,
                "Affinity battle checkpoint changed during serialization.");
            var receipt = restored.Receipts.Last(value => value.Event != null
                && value.Event.DamageEvents.Any(hit => hit.SourceId == 1 && hit.ElementType == element && hit.Damage < 0));
            var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(receipt.Sequence, receipt.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "Affinity damage was applied twice after restore.");
            File.WriteAllText(Path.Combine(output, "companion-affinity-checkpoint.json"), state);
            Debug.Log("[Acquired passive affinity] " + itemId + (itemId == 10528
                ? " actual dark damage, cultivated affinity comparison and restore passed."
                : " actual light damage and restore passed."));
        }
    }
}
