using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        [Serializable] private sealed class AsuraHandReceipt
        {
            public int buffs, convertedHits, ordinaryHits;
            public bool restored, attackCaptured;
        }
        private static AsuraHandReceipt asuraHandValidation;

        private static void PrepareAcquiredAsuraHand(OfflinePlayerProgress player, string output)
        {
            string path=Environment.GetEnvironmentVariable("ZJCS_SKILLS_ACQUIRED_ACTIVE_SAVE");
            Require(!string.IsNullOrWhiteSpace(path), "Asura requires the actual cultivated Eclipse Slashes save.");
            var source=JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(path));
            var skills=source.Player.Skills;
            var plan=skills.Plans[skills.SelectedPlan];
            Require(source.Player.Wardrobe.Profession==player.Wardrobe.Profession
                && source.Player.Level==player.Level && source.Player.SubRank==player.SubRank
                && plan.ActiveSkills[0]==10065 && plan.ActiveSkills.Count(id=>id>0)==1
                && plan.PassiveSkills.All(id=>id==0)
                && OfflineSkillProgression.OwnedRank(skills,10065)>1 && skills.ActiveSlotLevels[0]==2,
                "The Asura companion must come from the real isolated Eclipse acquisition/cultivation flow.");
            File.WriteAllText(Path.Combine(output,"companion-active-cultivated-save.json"),JsonUtility.ToJson(source,true));
            // Preserve the acquired companion while leaving slot zero fresh for
            // this flow's original merge, equipment and cultivation inputs.
            plan.ActiveSkills[0]=0; plan.ActiveSkills[1]=10065;
            skills.ActiveSlotLevels[0]=1; skills.ActiveSlotLevels[1]=2;
            player.Skills=skills;
            asuraHandValidation=new AsuraHandReceipt();
        }

        private static void VerifyAcquiredAsuraBuff(FightBattleSkillCastEvent cast,
            FightBattleDamageEvent[] damage, FightBattleStepEvent step, FightBattleUnitState actor)
        {
            Require(damage.Length==1 && damage[0].TargetId==actor.Id
                && damage[0].HitEntityId==11401 && damage[0].DamageEntityId==11402
                && damage[0].Damage==0 && !damage[0].IsCure,
                "Asura changed its original non-damaging self application.");
            var started=step.StatusViewEvents.Single(view=>view.Status.ClassId==24227
                && view.OwnerId==actor.Id && view.Kind==FightBattleStatusViewEventKind.Start);
            var active=actor.Skills.Single(skill=>skill.Slot==0);
            var status=OfficialCombatAssetCatalog.LoadDefault().Load().CreateStatus(24227,
                actor.Id,actor.Id,started.Status.InstanceId,11400,10063,active.Rank,active.Level,active.SubRank,active.Properties);
            Require(started.Status.CreatorId==actor.Id && started.Status.ApplyingSkillItemId==10063
                && !started.Status.CanStack && !started.Status.Forbidden,
                "Asura's actual status application lost its owner or original buff type.");
            Require(status.CreatorId==actor.Id && status.ApplyingSkillItemId==10063
                && status.SourceSkillRank==actor.Skills.Single(skill=>skill.Slot==0).Rank && status.SourceSkillLevel==2
                && status.SourceSubRank==5 && status.CanDispersed && status.DurationRound==-1
                && status.Components.HitDmgAddPer.DurationSkillCount==1
                && Math.Abs(started.Delay-cast.Delay-1f)<.0001f,
                "Asura lost its source cultivation, native delay or next-cast duration.");

        }

        private static void VerifyAcquiredAsuraStep(FightBattleStepEvent step,
            FightBattleCheckpoint checkpoint, string output)
        {
            // One settled step may contain both the preparation and its following
            // damaging skill; use immutable transition receipts for that lifetime.
            asuraHandValidation.buffs+=step.SkillCasts.Count(cast=>cast.ActorId==1 && cast.SkillEntityId==11400);
            var damage=step.DamageEvents.Where(hit=>hit.SourceId==1 && hit.SkillEntityId==11440).ToArray();
            if(damage.Length==0)return;
            Require(damage.Length==6 && damage.All(hit=>hit.SkillItemId==10065 && hit.DamageType==1
                && hit.ElementType==0 && hit.Damage<0 && !hit.IsCure),
                "The acquired Asura companion did not retain its six original physical hits.");
            bool converted=damage[0].TrueDamage;
            Require(damage.All(hit=>hit.TrueDamage==converted), "Asura converted only part of a multi-hit skill.");
            var actor=checkpoint.Units.Single(unit=>unit.Id==1);
            if(converted)
            {
                var starts=step.StatusViewEvents.Where(view=>view.OwnerId==actor.Id && view.Status.ClassId==24227
                    && view.Kind==FightBattleStatusViewEventKind.Start).ToArray();
                Require(asuraHandValidation.buffs>0 && !actor.Statuses.Any(status=>status.ClassId==24227)
                    && starts.All(start=>step.StatusViewEvents.Any(end=>end.OwnerId==actor.Id
                        && end.Status.InstanceId==start.Status.InstanceId && end.Kind==FightBattleStatusViewEventKind.End
                        && end.Delay>=damage.Max(hit=>hit.Delay))),
                    "Asura was not consumed after its matching complete skill.");
                asuraHandValidation.convertedHits+=damage.Length;
            }
            else if(asuraHandValidation.convertedHits>0) asuraHandValidation.ordinaryHits+=damage.Length;
            string saved=FightBattleCheckpointSerializer.Save(checkpoint);
            var restored=FightBattleCheckpointSerializer.Load(saved);
            var runner=OfficialCombatAssetCatalog.LoadDefault().RestoreBattle(restored);
            Require(runner.Step(checkpoint.Sequence,checkpoint.Counter).Replayed
                && FightBattleCheckpointSerializer.Save(restored)==saved,
                "Asura consumption changed when restoring and replaying its receipt.");
            asuraHandValidation.restored=true;
            File.WriteAllText(Path.Combine(output,"acquired-asura-hand-checkpoint.json"),saved);
            File.WriteAllText(Path.Combine(output,"acquired-asura-hand-receipt.json"),JsonUtility.ToJson(asuraHandValidation,true));
        }

        private static void CompleteAcquiredAsuraHand(string output)
        {
            Require(asuraHandValidation.buffs>0 && asuraHandValidation.convertedHits>=6
                && asuraHandValidation.restored
                && asuraHandValidation.attackCaptured,
                "Asura must render its original buff and converted attack, then consume its bonus and restore correctly.");
            File.WriteAllText(Path.Combine(output,"acquired-asura-hand-receipt.json"),JsonUtility.ToJson(asuraHandValidation,true));
            Debug.Log("[Acquired active combat] 10063 cultivated self buff, six-hit breaking conversion, consumption and restoration passed.");
        }
    }
}
