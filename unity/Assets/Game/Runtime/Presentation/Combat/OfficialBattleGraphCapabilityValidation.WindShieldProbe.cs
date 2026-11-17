using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
namespace Zjcs.Presentation.Combat
{
 public sealed partial class OfficialBattleGraphCapabilityValidation
 {
  private static bool ValidateWindShieldStart => Environment.GetEnvironmentVariable("ZJCS_WIND_SHIELD_START") == "1";
  private bool windShieldView, windShieldFrame;
  private float windShieldClock;
  private static OfficialBattleGraphCapabilityValidation ConfigureWindShieldStart(FightBattleCheckpoint checkpoint)
  {
   var basis=ConfigureSwordCounterattack(checkpoint);
   var probe=new OfficialBattleGraphCapabilityValidation(basis.catalog,basis.finisher,10990,"100/skill_14") { previousCaptureFramerate=basis.previousCaptureFramerate };
   var player=checkpoint.Units.Single(unit=>unit.Id==1);
   player.Statuses.Clear();
   player.Statuses.Add(basis.catalog.CreateStatus(25006,player.Id,player.Id,9925006,25006,10513,2,20,player.SubRank,player.Properties));
   checkpoint.RandomSeed=13;
   return probe;
  }
  private bool ObserveWindShieldStartStep(OfficialBattleRuntimeController controller)
  {
   if(!ValidateWindShieldStart)return false;
   if(settled)return true;
   var cp=controller.Checkpoint; var player=cp.Units.Single(unit=>unit.Id==1);
   var step=cp.Receipts.Last().Event;
   var shield=player.Statuses.SingleOrDefault(status=>status.ClassId==24175);
   if(shield==null)return true;
   Require(shield.ShieldInitialized && shield.ShieldRemainValue>0 && shield.ApplyingSkillItemId==10513 && player.SkillState.Round==0 && step.DamageEvents.Count==0 && step.StatusViewEvents.Any(e=>e.Kind==FightBattleStatusViewEventKind.Start && e.Status.ClassId==24175),"Wind shield pre-battle state or view receipt differs.");
   string save=FightBattleCheckpointSerializer.Save(cp);
   Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(save))==save,"Wind shield checkpoint differs.");
   settled=true; Debug.Log("[Wind shield] Pre-battle shield and checkpoint passed."); return true;
  }
  private bool ObserveWindShieldStartFrame(OfficialBattleRuntimeController controller)
  {
   if(!ValidateWindShieldStart)return false;
   if(!settled)return true;
   var view=UnityEngine.Object.FindObjectsOfType<MyFightStatus>().FirstOrDefault(v=>v.GetComponent<MyEntity>()?.ClassId==24175);
   if(!windShieldView && view) {var body=controller.TryGetUnitTransform(1);Require(body && view.Context.Owner==body.gameObject,"Wind shield view lost owner.");windShieldView=true;windShieldClock=Time.time;}
   if(!windShieldView)return true;
   if(!windShieldFrame && Time.time-windShieldClock>=.35f){windShieldFrame=true;CaptureName="wind-shield-start-native";return true;}
   if(!windShieldFrame)return true;
   if(!finisherScheduled){var player=controller.Checkpoint.Units.Single(v=>v.Id==1);player.Skills.Add(finisher);player.SkillState.Slots.Add(catalog.CreateSkillSlot(finisher,0));foreach(var prop in player.Properties.Concat(player.BaseProperties ?? player.Properties))if(prop.Id==catalog.PropertyType("Attack"))prop.Value=1000000000;finisherScheduled=true;}
   return true;
  }
  private void VerifyWindShieldStart(FightBattleCheckpoint checkpoint)
  {Time.captureFramerate=previousCaptureFramerate;Require(settled && windShieldView && windShieldFrame && checkpoint.Finished,"Wind shield native view or completed battle missing.");Debug.Log("[Wind shield] Native view and finished battle passed.");}
 }
}