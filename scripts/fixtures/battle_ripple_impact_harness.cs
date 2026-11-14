using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleRippleImpactHarness
{
    static void Check(bool value,string message) { if(!value)throw new InvalidOperationException("Ripple impact: "+message); }
    static FightBattleUnitState Unit(int id,int x,int y,FightDamageFormula formula)
    {
        var u=new FightBattleUnitState{Id=id,Side=id,CharacterType=1,Profession=2,Level=50,SubRank=5,
            X=x,Y=y,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=id==1?1000:900;
        foreach(var p in new Dictionary<int,long>{{1,1000},{2,10},{3,u.MaxHp},{4,u.Turn.Speed}})
            u.Properties.Add(new FightPropertyValue{Id=p.Key,Value=p.Value});
        foreach(var name in new[]{"BaseBlockPercentValue","BaseBlockAvoidPercentValue","BaseCritRatePercentValue","BaseCritAvoidPercentValue","BaseEffectRate","BaseEffectDodge"})
            u.Properties.Add(new FightPropertyValue{Id=formula.PropertyId(name),Value=10000});
        u.Properties.Add(new FightPropertyValue{Id=formula.PropertyId("CritAvoidPercent"),Value=10000});
        return u;
    }
    static FightBattleSkillDefinition Skill(FightOfficialCombatCatalog c)
        =>c.CreateSkill(10600,0,10008,7,20,5,c.CalculateSkillProps(10600,7,20,5)
            .Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList());
    static FightBattleStatusState Shield(FightOfficialCombatCatalog c,int id,long amount)
    {
        var s=c.CreateStatus(24435,2,2,id);s.ShieldUsesCustomValue=true;s.CustomShieldValue=amount;return s;
    }
    static FightBattleCheckpoint Input(FightOfficialCombatCatalog c,FightDamageFormula f,int seed,int[] growth,string mode,long exact)
    {
        var cp=new FightBattleCheckpoint{StageId=1001,MaxRound=8,RandomSeed=seed,Grid=new FightBattleGrid{Width=20,Height=20}};
        var a=Unit(1,5,5,f);var h=Unit(2,6,5,f);cp.Units.AddRange(new[]{a,h});
        var secret=c.CreateStatus(25030,2,2,725030,25030,10563,growth[0],growth[1],growth[2],h.Properties);
        h.Statuses.Add(secret);
        var skill=Skill(c);skill.LimitedTimes=1;a.Skills.Add(skill);a.SkillState.Slots.Add(c.CreateSkillSlot(skill,0));
        if(mode!="no-shield")h.Statuses.Add(Shield(c,724435,mode=="exact"?exact:mode=="intact"||mode=="expiry"?1000000:1));
        if(mode=="multi-break")h.Statuses.Add(Shield(c,724436,1));
        if(mode=="remaining")h.Statuses.Add(Shield(c,724436,1000000));
        if(mode=="remaining-forbidden") {var remaining=Shield(c,724436,0);remaining.ForbiddenType=1;h.Statuses.Add(remaining);}
        if(mode=="dodge")h.Properties.Add(new FightPropertyValue{Id=f.PropertyId("DodgePercent"),Value=10000});
        if(mode=="blinding")a.Properties.Add(new FightPropertyValue{Id=f.PropertyId("BlindingPercent"),Value=10000});
        if(mode=="immunity")h.Statuses.Add(c.CreateStatus(24007,2,2,724007));
        if(mode=="lethal"){h.Hp=1;h.Turn.Hp=1;}
        if(mode=="secret-forbidden")secret.ForbiddenType=1;
        if(mode=="child-denied"||mode=="child-whitelist")skill.CanTriggerChild=false;
        if(mode=="child-whitelist")skill.CanTriggerStatusEntityIds.Add(25030);
        if(mode=="manual-remove")FightStatusLifecycle.End(h,h.Statuses.Single(s=>s.ClassId==24435));
        if(mode=="area") {
            var nearby=Unit(3,7,5,f);nearby.Side=1;nearby.Turn.Speed=100;
            nearby.BodyOffsets.Clear();nearby.BodyOffsets.Add(new FightGridPoint(0,0));nearby.BodyOffsets.Add(new FightGridPoint(1,0));cp.Units.Add(nearby);
            var outside=Unit(4,9,5,f);outside.Side=1;outside.Turn.Speed=100;cp.Units.Add(outside);
        }
        return cp;
    }
    public static string Run(FightOfficialCombatCatalog c,FightDamageFormula f,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(int seed in new[]{1,17,50000})
        foreach(var growth in new[]{new[]{2,20,1},new[]{7,30,5}})
        {
            var control=Input(c,f,seed,growth,"no-shield",0);
            var controlExecutor=FightBattleExecutor.Begin(control,f,new FightDamageSettlement(),c,items);
            long exact=-controlExecutor.Step(control.Sequence,control.Counter+1).Event.DamageEvents.Single(d=>d.SkillEntityId==10600).Damage;
            foreach(string mode in new[]{"break","exact","multi-break","intact","remaining","remaining-forbidden",
                "dodge","blinding","immunity","lethal","secret-forbidden","child-denied","child-whitelist",
                "no-shield","manual-remove","expiry","area"})
            {
                try {
                var cp=Input(c,f,seed,growth,mode,exact);
                var executor=FightBattleExecutor.Begin(cp,f,new FightDamageSettlement(),c,items);
                var copy=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                var restored=FightBattleExecutor.Restore(copy,f,new FightDamageSettlement(),c,items);
                var step=executor.Step(cp.Sequence,cp.Counter+1).Event;restored.Step(copy.Sequence,copy.Counter+1);
                Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(copy),"restored break diverged: "+mode);
                bool triggers=new[]{"break","exact","multi-break","child-whitelist","area"}.Contains(mode);
                var casts=step.SkillCasts.Where(v=>v.SkillEntityId==11330).ToArray();
                Check(casts.Length==(triggers?1:0),"last-shield break gate differs: "+mode);
                var holder=cp.Units.Single(u=>u.Id==2);var secret=holder.Statuses.Single(s=>s.ClassId==25030);
                Check(secret.ActionEndTriggeredCount==(triggers?1:0),"event counter differs: "+mode);
                if(mode=="exact") {
                    var incoming=step.DamageEvents.Single(d=>d.SkillEntityId==10600);
                    Check(incoming.Damage==0&&incoming.ShieldDamage==exact,"exact shield did not break with zero life damage");
                }
                if(triggers) {
                    Check(casts.Single().ActorId==2&&casts.Single().SkillItemId==10563&&casts.Single().Slot==-1,"child consumed slot or lost secret source");
                    var hits=step.DamageEvents.Where(d=>d.SkillEntityId==11330).ToArray();
                    Check(hits.Length==(mode=="area"?3:1)&&hits.All(d=>d.SourceId==2&&d.TargetId!=2&&d.Damage<0&&d.ElementType==101),"original water scope/body-cell occurrences differ");
                    foreach(var damaged in hits) {
                        var target=cp.Units.Single(u=>u.Id==damaged.TargetId);
                        var cold=target.Statuses.SingleOrDefault(s=>s.ClassId==24266);
                        Check(cold!=null,"cold missing on "+target.Id+"; states="+string.Join(",",target.Statuses.Select(s=>s.ClassId+":"+s.StackCount)));
                        int expectedLayers=mode=="area"&&target.Id==3?6:3;
                        Check(cold.StackCount==expectedLayers&&cold.DurationRound==2&&cold.ApplyingSkillItemId==10563
                            &&cold.SourceSkillRank==growth[0]&&cold.SourceSkillLevel==growth[1]&&cold.SourceSubRank==growth[2],"three-layer cold/cultivation differs");
                    }
                    if(mode=="area")Check(!cp.Units.Single(u=>u.Id==4).Statuses.Any(s=>s.ClassId==24266),"out-of-range target received cold");
                }
                if(mode=="expiry") {
                    for(int i=0;i<4;i++) {
                        var next=executor.Step(cp.Sequence,cp.Counter+1).Event;restored.Step(copy.Sequence,copy.Counter+1);
                        Check(!next.SkillCasts.Any(v=>v.SkillEntityId==11330),"natural expiry triggered ripple");
                        Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(copy),"expiry restore diverged");
                    }
                    Check(!holder.Statuses.Any(s=>s.ActionType==60),"original two-round shield did not expire");
                }
                string saved=FightBattleCheckpointSerializer.Save(cp);
                Check(executor.Step(cp.Sequence,cp.Counter).Replayed&&FightBattleCheckpointSerializer.Save(cp)==saved,"receipt replay repeated ripple");
                cases++;
                } catch(Exception error) { throw new InvalidOperationException("Ripple input "+mode+"/"+seed+"/"+growth[0],error); }
            }
            var owner=Unit(2,6,5,f);owner.Statuses.Add(c.CreateStatus(25030,2,2,725030));
            var arbitrarySkill=Skill(c);arbitrarySkill.ActionType=60;
            var invocations=new List<FightBattleStatusSkillInvocation>();
            FightStatusTriggerRuntime.ApplySkillEndStatuses(owner,arbitrarySkill,new[]{owner},c,new FightRandomStream(seed),invocations);
            Check(invocations.Count==0&&owner.Statuses.Single().ActionEndTriggeredCount==0,"skill-end was mistaken for shield break");
        }
        return "Ripple impact passed: "+cases+" actual break/remaining-shield/expiry/manual-removal/miss/area/cultivation/restoration flows; 6 unrelated skill-end exclusions.";
    }
}
