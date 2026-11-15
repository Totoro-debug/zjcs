using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGraphCapabilityValidation
    {
        private bool ValidateFighterHpSecrets => Environment.GetEnvironmentVariable("ZJCS_FIGHTER_HP_SECRETS") == "1";

        private static OfficialBattleGraphCapabilityValidation ConfigureFighterHpSecrets(FightBattleCheckpoint checkpoint)
        {
            // One shared battle covers both current-HP secrets and fatal prevention.
            var basis = ConfigureSwordCounterattack(checkpoint);
            var probe = new OfficialBattleGraphCapabilityValidation(basis.catalog, basis.finisher,10990,"100/skill_14")
                {previousCaptureFramerate=basis.previousCaptureFramerate};
            var player=checkpoint.Units.Single(u=>u.Id==1);
            player.Statuses.Clear();
            player.Hp=player.Turn.Hp=player.MaxHp*7/10;
            foreach(var pair in new[]{new[]{25063,10582},new[]{25094,10589},new[]{25013,10518}})
                player.Statuses.Add(basis.catalog.CreateStatus(pair[0],1,1,9900000+pair[0],
                    pair[0],pair[1],6,17,1));
            foreach(var name in new[]{"BaseCureAdd","BaseBeCureAdd"})
            {
                int id=basis.catalog.PropertyType(name);
                foreach(var list in new[]{player.Properties,player.BaseProperties}.Where(p=>p!=null).Distinct())
                {
                    var prop=list.SingleOrDefault(p=>p.Id==id);
                    if(prop==null) list.Add(new FightPropertyValue {Id=id,Value=10000});
                    else prop.Value=10000;
                }
            }
            foreach(var enemy in checkpoint.Units.Where(u=>u.Side!=player.Side))
                foreach(var skill in enemy.Skills) {skill.HasCustomDamage=true;skill.CustomDamage=player.MaxHp*10;}
            checkpoint.RandomSeed=13;
            return probe;
        }

        private bool ObserveFighterHpSecretsStep(OfficialBattleRuntimeController controller)
        {
            if(!ValidateFighterHpSecrets) return false;
            if(settled) return true;
            var cp=controller.Checkpoint;
            var player=cp.Units.Single(u=>u.Id==1);
            var step=cp.Receipts.Last().Event;
            foreach(var pair in new[]{new[]{25063,24299},new[]{25094,24379}})
            {
                int expected=(int)Math.Min(5m,decimal.Floor(((decimal)player.MaxHp-player.Hp)/(player.MaxHp*.15m)));
                var buff=player.Statuses.SingleOrDefault(s=>s.ClassId==pair[1]);
                Require((buff?.StackCount??0)==expected,"Current-HP tier differs in the native battle: "+pair[1]);
                if(buff!=null) Require(buff.SourceSkillRank==6 && buff.SourceSkillLevel==17,
                    "Current-HP benefit lost original cultivation.");
                Require(player.Statuses.Single(s=>s.ClassId==pair[0]).Components.HpIncreaseUnit.AccumulatedHp==0,
                    "Native battle accumulated healing for a current-HP secret.");
            }
            if(!step.SkillCasts.Any(c=>c.SkillEntityId==10990)) return true;
            Require(player.Hp>1 && player.Statuses.Single(s=>s.ClassId==25013).HpLimitTriggeredCount==1,
                "Native fatal recovery did not preserve the living holder.");
            var recovery=step.DamageEvents.Single(d=>d.SkillEntityId==10990);
            Require(recovery.SourceId==1 && recovery.TargetId==1 && recovery.SkillItemId==10518 && recovery.Damage>0,
                "Original recovery lost self target or secret attribution.");
            var immune=player.Statuses.Single(s=>s.ClassId==24082);
            Require(immune.DurationRound==2 && immune.SourceSkillRank==6 && immune.SourceSkillLevel==17,
                "Original two-round indomitable lost cultivation.");
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved))==saved,
                "Combined HP-secret checkpoint changed on reload.");
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"fighter-hp-checkpoint.json"),saved);
            foreach(var unit in cp.Units) {unit.Skills.Clear();unit.SkillState.Slots.Clear();}
            settled=true;
            Debug.Log("[Fighter HP secrets] Current-HP tiers, fatal prevention, original 10990/24082, cultivation and checkpoint passed.");
            return true;
        }

        private bool ObserveFighterHpSecretsFrame(OfficialBattleRuntimeController controller)
        {
            if(!ValidateFighterHpSecrets) return false;
            foreach(var status in UnityEngine.Object.FindObjectsOfType<MyFightStatus>())
            {
                int id=status.GetComponent<MyEntity>()?.ClassId ?? 0;
                if(id==24082 || id==24299 || id==24379) swordCounterattackPlaying.Add((uint)id);
            }
            if(nativeCasts==0 || finisherScheduled) return false;
            var body=controller.TryGetUnitTransform(1);
            foreach(var native in UnityEngine.Object.FindObjectsOfType<MyFightSkill>())
                if(native.Component?.Entity?.ClassId==10990)
                {
                    Require(native.Source && native.Target && native.Source.transform==body && native.Target.transform==body,
                        "Original 10990 native view is not bound to its holder.");
                    swordCounterattackNative=true;
                }
            if(swordCounterattackFrames<FirstPromotionFrameTimes.Length
                && Time.time-swordCounterattackClock>=FirstPromotionFrameTimes[swordCounterattackFrames])
            {
                CaptureName="fighter-hp-phase-"+(++swordCounterattackFrames);
                return true;
            }
            return swordCounterattackFrames<FirstPromotionFrameTimes.Length || Time.time-swordCounterattackClock<.85f;
        }

        private void VerifyFighterHpSecrets()
        {
            if(!ValidateFighterHpSecrets) return;
            Time.captureFramerate=previousCaptureFramerate;
            Require(swordCounterattackNative && animated && swordCounterattackFrames==4
                && swordCounterattackPlaying.Contains(24082),
                "Original recovery entity, body animation or sampled native frames are incomplete.");
            Debug.Log("[Fighter HP secrets] Original recovery view, body animation and four rendered frames passed.");
        }
    }
}
