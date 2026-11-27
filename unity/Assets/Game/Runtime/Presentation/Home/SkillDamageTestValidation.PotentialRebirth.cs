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
        private static PotentialRebirthReceipt potentialRebirth;

        private static void PrepareAcquiredPotentialRebirth(FightBattleUnitState actor)
        {
            potentialRebirth = new PotentialRebirthReceipt();
            // Only this isolated diagnostic supplies the damaged initial state.
            actor.Hp = actor.Turn.Hp = 1;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            actor.Statuses.Add(catalog.CreateStatus(24012, actor.Id, actor.Id, 9058101));
            actor.Statuses.Add(catalog.CreateStatus(60421, actor.Id, actor.Id, 9058102));
        }

        private static void VerifyAcquiredPotentialRebirthStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            var actor = checkpoint.Units.Single(unit => unit.Id == 1);
            var listener = actor.Statuses.Single(status => status.ClassId == 25064
                && status.ApplyingSkillItemId == 10581);
            Require(listener.HpLimitMaxCount == 1 && listener.HpLimitTriggeredCount <= 1,
                "Potential Rebirth lost its single-use fatal listener.");
            foreach (var cure in step.DamageEvents.Where(hit => hit.SkillEntityId == 11770))
            {
                var assets = OfficialCombatAssetCatalog.LoadDefault();
                var props = assets.Load().CalculateSkillProps(11770, listener.SourceSkillRank,
                    listener.SourceSkillLevel, actor.SubRank);
                long expected = (long)((decimal)cure.MaxHp * props[210] / 10000m + props[170]);
                Require(cure.SourceId == actor.Id && cure.TargetId == actor.Id && cure.SkillItemId == 10581
                    && cure.HitEntityId == 11771 && cure.DamageEntityId == 11772 && cure.IsCure
                    && cure.BeforeHp == 1 && cure.HasDamageValue && cure.DamageValue == expected
                    && cure.Damage == Math.Min(expected, cure.MaxHp - 1) && cure.AfterHp == 1 + cure.Damage
                    && listener.HpLimitTriggeredCount == 1
                    && step.DamageEvents.Any(hit => hit.TargetId == actor.Id && !hit.IsCure
                        && hit.SourceId != actor.Id && !hit.Dodge && !hit.Immunity && !hit.Blinding
                        && hit.AfterHp == 1),
                    "Potential Rebirth lost the fatal clamp, original cultivated self-cure or HP cap.");
                Require(cure.HasDisperseNotification && step.DispersedUnitIds.Contains(actor.Id)
                    && !actor.Statuses.Any(status => status.ClassId == 24012 || status.ClassId == 60421),
                    "Potential Rebirth did not complete its actual self-dispersal.");
                potentialRebirth.cures++;
                potentialRebirth.healed += cure.Damage;
            }
            if (potentialRebirth.cures == 0) return;
            string state = FightBattleCheckpointSerializer.Save(checkpoint);
            var restored = FightBattleCheckpointSerializer.Load(state);
            var runner = OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(runner.Step(checkpoint.Sequence, checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored) == state
                && restored.Units.Single(unit => unit.Id == actor.Id).Statuses
                    .Single(status => status.ClassId == 25064).HpLimitTriggeredCount == 1,
                "Potential Rebirth restoration repeated healing or restored the consumed fatal listener.");
            potentialRebirth.restored = true;
            File.WriteAllText(Path.Combine(output, "companion-potential-rebirth-checkpoint.json"), state);
            File.WriteAllText(Path.Combine(output, "companion-potential-rebirth-receipt.json"),
                JsonUtility.ToJson(potentialRebirth, true));
        }

        private static void CompleteAcquiredPotentialRebirth(bool captured)
        {
            Require(captured && potentialRebirth.cures == 1 && potentialRebirth.healed > 0
                && potentialRebirth.restored,
                "Potential Rebirth did not render its single cultivated recovery and restore its consumed state.");
            Debug.Log("[Acquired passive combat] 10581 fatal survival, cultivated healing, dispersal and restore passed.");
        }

        [Serializable]
        private sealed class PotentialRebirthReceipt
        {
            public int cures;
            public long healed;
            public bool restored;
        }
    }
}
