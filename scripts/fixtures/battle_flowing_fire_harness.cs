using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;
public static class BattleFlowingFireHarness {
 static void Check(bool value,string message) { if(!value) throw new InvalidOperationException("Flowing fire: "+message); }
 public static string Run(FightOfficialCombatCatalog c,FightDamageFormula f,FightOfficialSkillItemCatalog items) {
  var counts=new HashSet<int>();int cases=0;
  foreach(int entity in new[]{12090,13590})
  foreach(int rank in new[]{3,4,5})
  foreach(int level in new[]{1,2,10})
  foreach(int seed in Enumerable.Range(1,64)) { counts.Add(Case(c,f,items,entity,rank,level,seed,"normal"));cases++; }
  foreach(int entity in new[]{12090,13590})
  foreach(string mode in new[]{"first-failure","second-failure","both-success","target-death","legacy-checkpoint","second-death"}) {
   int actual=Case(c,f,items,entity,4,2,73,mode);
   Check(mode=="legacy-checkpoint" || actual==(mode=="first-failure"||mode=="target-death"?0:mode=="second-failure"||mode=="second-death"?1:2),"sequential stop case changed: "+mode);cases++;
  }
  Check(counts.SetEquals(new[]{0,1,2}),"the original rates did not exercise all chain lengths");
  return "Flowing fire passed: "+cases+" cultivated base/skin chain cases, action times, original targets, failed-rate/death stop, attribution, restore and replay.";
 }
 static int Case(FightOfficialCombatCatalog c,FightDamageFormula f,FightOfficialSkillItemCatalog items,int entity,int rank,int level,int seed,string mode) {
  var s=new FightBattleCheckpoint {StageId=1,MaxRound=8,RandomSeed=seed*1000003+12345,Grid=new FightBattleGrid { Width=16,Height=16,DefaultGroundType=2 }};
  var actor=Unit(1,1,7,7,f);var target=Unit(2,2,7,8,f);var other=Unit(3,2,8,7,f);
  if(mode=="target-death")target.Hp=target.Turn.Hp=1;
  var root=c.CreateSkill(entity,0,10100,rank,level,8,Props(c,entity,rank,level));
  if(entity==13590){root.UseItemId=10185;root.SkillSkinId=39523;}
  Check(root.ExtraSkills.Count==2,"the actual extra skill component was omitted");
  Check(root.ExtraSkills[0].SkillId==(entity==12090?12093:13593) && root.ExtraSkills[1].SkillId==(entity==12090?12094:13594),"the skin reused foreign extra entities");
  if(mode!="normal" && mode!="legacy-checkpoint") {root.ExtraSkills[0].Rate=mode=="first-failure"?0f:1f;root.ExtraSkills[1].Rate=mode=="second-failure"?0f:1f;}
  if(mode=="second-death") {
   // Damage against this fixed defence/cultivation fixture is deterministic. A first
   // complete cast establishes the exact boundary without mirroring its formula.
   long firstDamage=SingleDamage(c,f,items,entity,rank,level);
   target.Hp=target.Turn.Hp=checked(firstDamage+1);
  }
  actor.Skills.Add(root);actor.SkillState.Slots.Add(c.CreateSkillSlot(root,0));
  s.Units.AddRange(new[]{actor,target,other});var wave=new FightBattleWaveState();wave.UnitIds.AddRange(new[]{2,3});s.Waves.Add(wave);
  var ex=FightBattleExecutor.Begin(s,f,new FightDamageSettlement(),c,items);
  var copy=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(s));
  if(mode=="legacy-checkpoint") copy.Units[0].Skills[0].ExtraSkills.Clear();
  var restored=FightBattleExecutor.Restore(copy,f,new FightDamageSettlement(),c,items);
  Check(FightBattleCheckpointSerializer.Save(s)==FightBattleCheckpointSerializer.Save(copy),"legacy restoration changed mutable state or failed to rebuild extras");
  var step=ex.Step(s.Sequence,s.Counter+1).Event;restored.Step(copy.Sequence,copy.Counter+1);
  Check(FightBattleCheckpointSerializer.Save(s)==FightBattleCheckpointSerializer.Save(copy),"restoring changed the cast order, cultivated damage or random stream");
  Check(actor.SkillState.Slots[0].ReleasedTimes==1 && step.SkillCasts.Count>=1 && step.SkillCasts.Count<=3,"the chain spent multiple ordinary releases");
  int[] expected=entity==12090?new[]{12090,12093,12094}:new[]{13590,13593,13594};
  float start=0f;long damage=0;
  for(int n=0;n<step.SkillCasts.Count;n++) {
   var cast=step.SkillCasts[n];var definition=c.CreateSkill(expected[n],-1,10100,rank,level,8,Props(c,expected[n],rank,level));
   Check(cast.SkillEntityId==expected[n] && cast.SkillItemId==10100 && cast.ActorId==actor.Id
    && cast.TargetIds.SequenceEqual(new[]{target.Id}) && cast.Slot==(n==0?0:-1)
    && Math.Abs(cast.Delay-start)<.0001f,"the original target, parent attribution or action-end schedule changed");
   var hit=step.DamageEvents.Single(x=>x.SkillEntityId==cast.SkillEntityId);
   var authored=FightBattleHitCollector.CollectAll(definition).Single();
   Check(hit.HitEntityId==(entity==12090?12091:13591) && hit.DamageEntityId==(entity==12090?12092:13592)
    && hit.SourceId==actor.Id && hit.TargetId==target.Id && hit.SkillItemId==10100
    && hit.DamageType==1 && hit.ElementType==102 && hit.Damage<0 && !hit.TrueDamage && !hit.IsCure
    && Math.Abs(hit.Delay-start-authored.Delay)<.0001f,"the original cultivated fire impact changed");
   if(n==0)damage=hit.Damage;else Check(mode=="second-death" || hit.Damage==damage,"an extra lost its original cultivation coefficients");
   Check(Math.Abs(cast.Presentation.Duration-definition.Duration)<.0001f && Math.Abs(cast.Presentation.ActionDuration-definition.ActionDuration)<.0001f,"the sequence replaced an authored native lifetime");
   start+=definition.ActionDuration;
  }
  Check(step.DamageEvents.Count==step.SkillCasts.Count && other.Hp==other.MaxHp,"the extra retargeted or created another impact");
  string settled=FightBattleCheckpointSerializer.Save(s);Check(ex.Step(s.Sequence,s.Counter).Replayed&&FightBattleCheckpointSerializer.Save(s)==settled,"receipt replay cast the extras twice");
  return step.SkillCasts.Count-1;
 }
 static long SingleDamage(FightOfficialCombatCatalog c,FightDamageFormula f,FightOfficialSkillItemCatalog items,int entity,int rank,int level) {
  var s=new FightBattleCheckpoint {StageId=1,MaxRound=8,RandomSeed=7,Grid=new FightBattleGrid { Width=16,Height=16,DefaultGroundType=2 }};
  var a=Unit(1,1,7,7,f);var b=Unit(2,2,7,8,f);
  var skill=c.CreateSkill(entity,0,10100,rank,level,8,Props(c,entity,rank,level));
  skill.ExtraSkills[0].Rate=0f;a.Skills.Add(skill);a.SkillState.Slots.Add(c.CreateSkillSlot(skill,0));
  s.Units.AddRange(new[]{a,b});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);s.Waves.Add(wave);
  return -FightBattleExecutor.Begin(s,f,new FightDamageSettlement(),c,items).Step(s.Sequence,s.Counter+1).Event.DamageEvents.Single().Damage;
 }
 static List<FightPropertyValue> Props(FightOfficialCombatCatalog c,int entity,int rank,int level) => c.CalculateSkillProps(entity,rank,level,8).Select(x=>new FightPropertyValue{Id=x.Key,Value=x.Value}).ToList();
 static FightBattleUnitState Unit(int id,int side,int x,int y,FightDamageFormula f) {
  var u=new FightBattleUnitState{Id=id,Side=side,CharacterType=side,Profession=8,Level=70,SubRank=8,X=x,Y=y,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000,MoveDist=0,MoveMask=5};
  u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=id==1?1000:10;
  foreach(var p in new Dictionary<string,long>{{"Attack",1000},{"Defence",10},{"MaxHp",u.MaxHp},{"Speed",u.Turn.Speed},{"CritAvoidPercent",10000},{"BaseBlockPercentValue",10000},{"BaseBlockAvoidPercentValue",10000},{"BaseCritRatePercentValue",10000},{"BaseCritAvoidPercentValue",10000},{"BaseEffectRate",10000},{"BaseEffectDodge",10000}}) u.Properties.Add(new FightPropertyValue{Id=f.PropertyId(p.Key),Value=p.Value});
  return u;
 }
}
