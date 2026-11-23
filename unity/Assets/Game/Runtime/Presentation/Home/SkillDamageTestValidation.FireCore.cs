using System.IO;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void SetAcquiredBattleProperty(FightBattleUnitState actor, string name, long value)
        {
            int property = OfficialCombatAssetCatalog.LoadDefault().Load().PropertyType(name);
            foreach (var values in new[] { actor.Properties, actor.BaseProperties })
            {
                if (values == null) continue;
                var rating = values.FirstOrDefault(candidate => candidate.Id == property);
                if (rating == null) values.Add(new FightPropertyValue { Id = property, Value = value });
                else rating.Value = value;
            }
        }

        private static void VerifyAcquiredFireCore(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var criticalTargets = step.DamageEvents.Where(hit => hit.SourceId == 1 && hit.SkillEntityId == 10800
                && hit.ElementType == 102 && hit.Critical && !hit.Dodge && !hit.Immunity && !hit.Blinding)
                .Select(hit => hit.TargetId).OrderBy(id => id);
            var casts = step.SkillCasts.Where(cast => cast.ActorId == 1 && cast.SkillEntityId == 10540).ToArray();
            Require(casts.All(cast => cast.SkillItemId == 10503 && cast.TargetIds.Count == 1)
                && casts.SelectMany(cast => cast.TargetIds).OrderBy(id => id).SequenceEqual(criticalTargets),
                "The cultivated fire core lost a critical trigger or its target position.");
            if (casts.Length == 0) return;

            var damage = step.DamageEvents.Where(hit => hit.SkillEntityId == 10540).ToArray();
            Require(damage.Length > 0 && damage.All(hit => hit.SourceId == 1 && hit.SkillItemId == 10503
                && hit.HitEntityId == 10541 && hit.DamageEntityId == 10023 && hit.DamageType == 1
                && hit.ElementType == 102 && !hit.TrueDamage && !hit.IsCure) && damage.Any(hit => hit.Damage < 0),
                "The cultivated fire core did not deal its original fire damage.");
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var executor = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(executor.Step(restored.Sequence, restored.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "The fire core burst changed or repeated after restoring the battle.");
            File.WriteAllText(Path.Combine(output, "companion-fire-core-checkpoint.json"), state);
            UnityEngine.Debug.Log("[Acquired passive combat] 10503 cultivated fire core burst and restore replay passed.");
        }
    }
}
