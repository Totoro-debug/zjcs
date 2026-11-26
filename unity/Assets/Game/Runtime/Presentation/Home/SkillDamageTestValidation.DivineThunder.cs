using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private static void VerifyAcquiredDivineThunder(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] damage, FightBattleCheckpoint checkpoint, string output)
        {
            int[] ids = { 10922, 10925, 10923, 10924, 10922, 10922, 10924, 10922,
                10923, 10924, 10923, 10922, 10925, 10924, 10924, 10925 };
            float[] delays = { .85f, .99f, 1.12f, 1.23f, 1.32f, 1.4f, 1.47f, 1.52f,
                1.56f, 1.63f, 1.71f, 1.8f, 1.91f, 2.02f, 2.14f, 2.24f };
            var strikes = cast.HitSelections.Where(hit => hit.HitEntityId >= 10922
                && hit.HitEntityId <= 10925).ToArray();
            Require(strikes.Length == 16 && strikes.Sum(hit => hit.Targets.Count) >= 3,
                "The acquired divine thunder lost its sixteen strikes or shared minimum.");
            for (int i = 0; i < strikes.Length; i++)
                Require(strikes[i].HitEntityId == ids[i] && Math.Abs(strikes[i].Delay - delays[i]) < .0001f
                    && strikes[i].EffectCenters.Count == 1 && strikes[i].SelectedGridPositions.Count == 1
                    && checkpoint.Grid.Contains(strikes[i].SelectedGridPositions[0]),
                    "An acquired lightning strike lost its original order, time or single in-bounds centre.");
            Require(damage.Length >= 3 && damage.All(hit => hit.SourceId == 1 && hit.TargetId != 1
                && hit.SkillItemId == 10037 && hit.DamageEntityId == 10926 && hit.ElementType == 103
                && hit.DamageType == 1 && !hit.TrueDamage && !hit.IsCure),
                "The acquired lightning lost its original light damage identity.");
            foreach (var status in checkpoint.Units.SelectMany(unit => unit.Statuses)
                .Where(status => status.ClassId == 24100 && status.CreatorId == 1))
                Require(status.ApplyingSkillItemId == 10037 && status.RootSkillId == 10920
                    && status.SourceSkillLevel == 2 && status.FalloffPercent == 4000,
                    "The acquired per-target falloff lost its source or cultivated status.");
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var runner = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(runner.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state,
                "Restoring divine thunder repeated a strike or changed its random/falloff state.");
            File.WriteAllText(Path.Combine(output, "acquired-divine-thunder-checkpoint.json"), state);
            File.WriteAllText(Path.Combine(output, "acquired-divine-thunder-strikes.json"), JsonUtility.ToJson(cast, true));
            Debug.Log("[Acquired active combat] 10037 sixteen lightning centres, minimum, light damage and restore passed.");
        }
    }
}
