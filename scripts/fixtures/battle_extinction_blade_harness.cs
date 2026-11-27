using System;
using System.Linq;
using System.Collections.Generic;
using Zjcs.Combat.Official;
public static class BattleExtinctionBladeHarness
{
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static IEnumerable<FightGridPoint> Body(FightBattleUnitState unit)
  =>new[]{new FightGridPoint()}.Concat(unit.BodyOffsets).Distinct().Select(p=>new FightGridPoint(unit.X+p.X,unit.Y+p.Y));
 public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
 {
  int cases=0,armourApplied=0,armourMissed=0;
  foreach(int rank in new[]{3,6,7})
  foreach(int level in new[]{1,2,20})
  foreach(int seed in new[]{13,104729})
  foreach(int[] direction in new[]{new[]{1,0},new[]{-1,0},new[]{0,1},new[]{0,-1}})
  foreach(bool large in new[]{false,true})
  foreach(bool wall in new[]{false,true})
  foreach(string effect in new[]{"baseline","boosted","suppressed"})
  {
   int dx=direction[0],dy=direction[1],px=-dy,py=dx;
   var actor=Unit(1,1,10,10,formula);
   var first=Unit(2,2,10+3*dx,10+3*dy,formula);
   var second=Unit(3,2,10+3*dx+px,10+3*dy+py,formula);
   var outside=Unit(4,2,22,22,formula);
   if(effect=="boosted")actor.Properties.Add(new FightPropertyValue{Id=formula.PropertyId("EffectRateValue"),Value=20000});
   if(effect=="suppressed")foreach(var target in new[]{first,second})target.Properties.Add(new FightPropertyValue{Id=formula.PropertyId("EffectDodgeValue"),Value=20000});
   foreach(var unit in new[]{actor,first,second})unit.BaseProperties=unit.Properties.Select(p=>new FightPropertyValue{Id=p.Id,Value=p.Value}).ToList();
   if(large){actor.BodyOffsets.AddRange(new[]{new FightGridPoint(),new FightGridPoint(px,py)});
    second.BodyOffsets.AddRange(new[]{new FightGridPoint(),new FightGridPoint(dx,dy),new FightGridPoint(px,py),new FightGridPoint(dx+px,dy+py)});}
   var cp=new FightBattleCheckpoint{StageId=1,RandomSeed=seed,MaxRound=10,Grid=new FightBattleGrid{Width=24,Height=24,DefaultGroundType=2}};
   cp.Units.AddRange(new[]{actor,first,second,outside});
   if(wall)for(int offset=-1;offset<=1;offset++)cp.Grid.BlockedPositions.Add(new FightGridPoint(10+dx+offset*px,10+dy+offset*py));
   var props=catalog.CalculateSkillProps(11530,rank,level,5);
   var skill=catalog.CreateSkill(11530,0,10069,rank,level,5,props.Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList());
   skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
   var application=skill.HitGraphEntities.Single(h=>h.EntityId==11531).AppliedStatuses.Single(a=>a.StatusId==24113);
   Check(application.AffectedByProp && Math.Abs(application.Chance-.5f)<.00001f,"Extinction Blade original base application rate changed.");
   double chance=catalog.CalculateStatusApplicationRate(actor,first,24113,.5d,true,formula);
   Check(effect=="baseline"?Math.Abs(chance-.5d)<.00001d:effect=="boosted"?chance>1d:chance<0d,
    "Extinction Blade effect-rate fixture is invalid: "+effect+" / "+chance);
   var run=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
   var step=run.Step(cp.Sequence,cp.Counter+1).Event;
   var cast=step.SkillCasts.Single(c=>c.SkillEntityId==11530 && c.ActorId==1);
   var movement=step.Movements.Single(m=>m.UnitId==1);
   var landing=new FightGridPoint(actor.X,actor.Y);
   Check(movement.Path.Count==2 && movement.Path[0].Equals(new FightGridPoint(10,10))
    && movement.Path[1].Equals(landing) && skill.Range.Contains(new FightGridPoint(actor.X-10,actor.Y-10))
    && !landing.Equals(new FightGridPoint(10,10)) && Math.Abs(movement.Delay-cast.Delay-.07f)<.0001f,
    "Extinction Blade lost its original direct jump range, delay or path.");
   var occupied=new HashSet<FightGridPoint>(Body(first).Concat(Body(second)).Concat(Body(outside)));
   Check(Body(actor).All(p=>cp.Grid.CanStand(p,actor.MoveMask,occupied)),"Extinction Blade landed an occupied or blocked body cell.");
   var expectedTargets=new[]{first,second,outside}.Where(u=>Body(u).Any(p=>Math.Abs(p.X-actor.X)+Math.Abs(p.Y-actor.Y)<=2)).ToArray();
   var hits=step.DamageEvents.Where(h=>h.SkillEntityId==11530).ToArray();
   int expectedOccurrences=expectedTargets.Sum(u=>Body(u).Count(p=>Math.Abs(p.X-actor.X)+Math.Abs(p.Y-actor.Y)<=2));
   Check(hits.Length==expectedOccurrences && hits.Length>=2 && !hits.Any(h=>h.TargetId==outside.Id)
    && hits.Select(h=>h.TargetId).Distinct().Count()==expectedTargets.Length
    && hits.GroupBy(h=>h.TargetId).All(g=>g.Select(h=>h.DamageX+","+h.DamageY).Distinct().Count()==g.Count()),
    "Extinction Blade radius: "+hits.Length+" / "+expectedTargets.Length+" rank="+rank+" level="+level+" seed="+seed+" direction="+dx+","+dy+" large="+large+" wall="+wall+" landing="+actor.X+","+actor.Y+" actual="+string.Join(";",hits.Select(h=>h.TargetId+":"+h.HitEntityId+":"+h.DamageEntityId+":"+h.Delay))+" targets="+string.Join(";",new[]{first,second,outside}.Select(u=>u.Id+":"+u.X+","+u.Y)));
   var armourBefore=new HashSet<int>();
   foreach(var hit in hits)
   {
    var target=expectedTargets.Single(u=>u.Id==hit.TargetId);
    int distance=Math.Abs(hit.DamageX-actor.X)+Math.Abs(hit.DamageY-actor.Y);
    long defence=armourBefore.Contains(target.Id)?7:10;
    long expectedDamage=-(long)((1000d*props[200]/10000d+props[190])*1000d/(1000d+defence));
    Check(hit.HasDamagePosition && Body(target).Contains(new FightGridPoint(hit.DamageX,hit.DamageY))
     && distance<=2 && hit.Damage==expectedDamage && hit.ElementType==104 && hit.DamageType==1 && !hit.TrueDamage
     && !hit.IsCure && !hit.Critical && !hit.Block && hit.HitEntityId==11531 && hit.DamageEntityId==11534
     && Math.Abs(hit.Delay-cast.Delay-(.42f+.08f*distance))<.0001f,
     "Extinction Blade dark damage, fixed/proportional terms or scope delay changed: "+hit.Damage+" / "+expectedDamage);
    if(hit.IsDamageTriggerStatus){armourBefore.Add(target.Id);armourApplied++;}
    else armourMissed++;
    var armour=target.Statuses.SingleOrDefault(s=>s.ClassId==24113);
    Check(effect=="baseline" || (armour!=null)==(effect=="boosted"),"Extinction Blade did not use the original effect-rate branch: "+effect);
    if(armour==null)continue;
    Check(armour.DurationRound==2 && armour.CanDispersed && armour.StatusType==2 && armour.CreatorId==1
     && armour.ApplyingSkillItemId==10069 && armour.RootSkillId==11530 && armour.SourceSkillRank==rank
     && armour.SourceSkillLevel==level && armour.SourceSubRank==5,
     "Extinction Blade armour reduction lost its source, cultivation or two-turn duration.");
    var armourProps=catalog.CalculateSkillProps(24113,rank,level,5);
    Check(armourProps.All(p=>armour.StatusProperties.Any(v=>v.Id==p.Key && v.Value==p.Value))
     && target.Properties.Single(p=>p.Id==formula.PropertyId("Defence")).Value==7,
     "Extinction Blade armour reduction did not apply its original cultivated values.");
   }
   Check(cp.StatusSeconds.PendingHits.Count==hits.Length,"Extinction Blade did not schedule each floating impact.");
   string saved=FightBattleCheckpointSerializer.Save(cp);var copy=FightBattleCheckpointSerializer.Load(saved);
   var restored=FightBattleExecutor.Restore(copy,formula,new FightDamageSettlement(),catalog,items);
   Check(restored.Step(copy.Sequence,copy.Counter).Replayed && FightBattleCheckpointSerializer.Save(copy)==saved,
    "Extinction Blade repeated a jump or damage receipt after restore.");
   double advance=hits.Max(h=>h.Delay)+.01d;
   run.AdvanceStatusSeconds(advance);restored.AdvanceStatusSeconds(advance);
   Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(copy),"Extinction Blade floating restoration diverged.");
   foreach(var target in expectedTargets)
   {
    var floating=target.Statuses.Single(s=>s.ClassId==24269);
    Check(floating.CreatorId==1 && floating.ApplyingSkillItemId==10069 && floating.RootSkillId==11530
     && floating.SourceSkillRank==rank && floating.SourceSkillLevel==level && floating.Components.Seconds.Started,
     "Extinction Blade floating lost its source or configured lifetime.");
   }
   for(int index=0;index<8 && cp.Units.Any(u=>u.Statuses.Any(s=>s.ClassId==24269 || s.ClassId==24270));index++)
   {
    double next=run.NextStatusSecondsDelay;Check(!double.IsInfinity(next),"Extinction Blade floating/recovery timer stopped.");
    run.AdvanceStatusSeconds(next);restored.AdvanceStatusSeconds(next);
    Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(copy),"Extinction Blade recovery restoration diverged.");
   }
   Check(cp.Units.All(u=>u.Statuses.All(s=>s.ClassId!=24269 && s.ClassId!=24270)),"Extinction Blade floating/recovery did not expire.");
   cases++;
  }
  Check(armourApplied>0 && armourMissed>0,"Extinction Blade armour outcomes: "+armourApplied+" / "+armourMissed);
  return "Extinction Blade passed: "+cases+" cultivated directions, walls, full bodies, dark area damage, armour and restored floating/recovery cases; armour="+armourApplied+"/"+armourMissed;
 }
    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 7,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000000, MaxHp = 1000000000,
            LastMaxHp = 1000000000, MoveMask = 5
        };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = id == 1 ? 1000 : 10;
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
        foreach (var pair in new System.Collections.Generic.Dictionary<string,int> {
            { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 },
            { "BaseBlockAvoidPercentValue", 10000 }, { "BaseEffectRate", 10000 },
            { "BaseEffectDodge", 10000 }, { "BaseElementMaster", 10000 },
            { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        unit.BaseProperties=unit.Properties.Select(p=>new FightPropertyValue{Id=p.Id,Value=p.Value}).ToList();
        return unit;
    }
}
