using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredAffinityStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output, int itemId, int element)
        {
            var hits = step.DamageEvents.Where(hit => hit.SourceId == 1 && hit.ElementType == element
                && !hit.IsCure && !hit.TrueDamage && hit.Damage < 0).ToArray();
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
            Debug.Log("[Acquired passive affinity] " + itemId + " actual light damage and restore passed.");
        }
    }
}
