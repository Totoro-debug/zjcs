using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private sealed partial class AcquiredGuardianProbe
        {
            private bool sanctuaryGranted, sanctuaryTransferred, sanctuaryOpeningCaptured;

            private void ObserveSanctuary(FightBattleStepEvent step, FightBattleCheckpoint checkpoint)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                var ally = checkpoint.Units.Single(unit => unit.Id == AllyId);
                var protection = ally.Statuses.FirstOrDefault(status => status.ClassId == 24304
                    && status.CreatorId == actor.Id && status.ApplyingSkillItemId == item);
                if (!sanctuaryGranted && protection != null)
                {
                    Require(!protection.CanDispersed && protection.OwnerId == AllyId
                        && protection.SourceSkillLevel == 2 && protection.Components.DamageReduceBeforeFinal != null
                        && actor.Statuses.Any(status => status.ClassId == 24305)
                        && step.SkillCasts.Any(cast => cast.SkillEntityId == 11790 && cast.SkillItemId == item
                            && cast.ActorId == actor.Id), "The actual cultivated sanctuary lost its ally, link or original grant.");
                    SaveSanctuaryCheckpoint(checkpoint, "grant");
                    sanctuaryGranted = true;
                }
                for (int i = 0; i < step.DamageEvents.Count; i++)
                {
                    var transfer = step.DamageEvents[i];
                    if (transfer.SkillEntityId != 11780 || transfer.SkillItemId != item) continue;
                    var parent = step.DamageEvents.Take(i).LastOrDefault(hit => hit.TargetId == AllyId
                        && hit.SourceId == transfer.SourceId && hit.SkillEntityId != 11780 && !hit.IsCure);
                    Require(sanctuaryGranted && parent != null && transfer.TargetId == actor.Id
                        && checkpoint.Units.Single(unit => unit.Id == transfer.SourceId).Side != actor.Side
                        && transfer.HitEntityId == 11781 && transfer.DamageEntityId == 10005
                        && transfer.DamageType == 2 && !transfer.IsCure && transfer.Damage <= 0
                        && !transfer.Dodge && !transfer.Blinding && !transfer.Immunity
                        && (parent.Damage < 0 || parent.ShieldDamage > 0)
                        && transfer.Delay >= parent.Delay,
                        "The actual sanctuary transfer changed its settlement order, original attacker, guardian or damage type.");
                    SaveSanctuaryCheckpoint(checkpoint, "transfer");
                    File.WriteAllText(Path.Combine(output, "companion-sanctuary-receipt.json"),
                        JsonUtility.ToJson(new SanctuaryReceipt { ally = AllyId, attacker = transfer.SourceId,
                            parentIndex = step.DamageEvents.IndexOf(parent), transferIndex = i, protectedDamage = parent.Damage,
                            guardianDamage = transfer.Damage, guardianShield = transfer.ShieldDamage, restored = true }, true));
                    sanctuaryTransferred = true;
                    Debug.Log("[Acquired passive combat] 10580 cultivated allied sanctuary, ordered original transfer and restore replay passed.");
                }
            }

            private void SaveSanctuaryCheckpoint(FightBattleCheckpoint checkpoint, string phase)
            {
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                var replay = assets.RestoreBattle(restored).Step(restored.Sequence, restored.Counter);
                Require(replay.Replayed && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring sanctuary changed its state or settled its damage receipt again.");
                File.WriteAllText(Path.Combine(output, "companion-sanctuary-" + phase + ".json"), saved);
            }

            public bool OpeningReady()
            {
                return item == 10580 && sanctuaryGranted && IsNativeSkillParticleVisible("playable11790");
            }

            public void OpeningCaptured()
            {
                sanctuaryOpeningCaptured = true;
            }
        }

        [Serializable]
        private sealed class SanctuaryReceipt
        {
            public int ally, attacker, parentIndex, transferIndex;
            public long protectedDamage, guardianDamage, guardianShield;
            public bool restored;
        }
    }
}
