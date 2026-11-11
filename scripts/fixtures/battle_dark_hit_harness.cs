using System;
using System.Linq;
using System.Reflection;
using Zjcs.Combat.Official;

public static class BattleDarkHitHarness
{
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    static FightBattleUnitState Unit(FightOfficialCombatCatalog c,int id,int side,int x,int y) {
        var u=new FightBattleUnitState {Id=id,Side=side,X=x,Y=y,Hp=1000000000,MaxHp=1000000000,LastMaxHp=1000000000,Level=150,SubRank=1};
        u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=id==1?1000:1;
        foreach(var p in new[]{new FightPropertyValue{Id=1,Value=10001},new FightPropertyValue{Id=2,Value=0},
            new FightPropertyValue{Id=3,Value=1000000000},new FightPropertyValue{Id=4,Value=id==1?1000:1}})u.Properties.Add(p);
        foreach(string name in new[]{"BaseEffectRate","BaseEffectDodge"})
            u.Properties.Add(new FightPropertyValue {Id=c.PropertyType(name),Value=10000});
        return u;
    }
    public static void Run(FightOfficialCombatCatalog c,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        var owner=Unit(c,1,1,5,5);var target=Unit(c,2,2,6,5);
        var status=c.CreateStatus(25031,1,1,1,0,10565,2,20,1);owner.Statuses.Add(status);
        var def=status.Components.DarkHit;
        Check(def.DefinitionResolved&&def.Supported&&def.Elements.SequenceEqual(new[]{104})
            &&def.IgnoredSkills.SequenceEqual(new[]{11360})&&def.Statuses.Single().EntityId==24222
            &&Math.Abs(def.Statuses[0].BasePercent-.4)<1e-7&&def.Statuses[0].AffectedByProp,"Original dark-hit definition differs");
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        cp.Units.Add(owner);cp.Units.Add(target);
        var exec=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items);
        var method=typeof(FightBattleExecutor).GetMethod("ApplyDarkHitStatuses",BindingFlags.Instance|BindingFlags.NonPublic);
        var rng=(FightRandomStream)typeof(FightBattleExecutor).GetField("random",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(exec);
        var skill=c.CreateSkill(10810,0,10029,2,20,1,null);
        var hit=new FightBattleHitResult();int successes=0;
        for(int i=0;i<128;i++) {
            target.Statuses.Clear();
            var expected=new FightRandomStream(rng.Seed);
            double chance=c.CalculateStatusApplicationRate(owner,target,24222,def.Statuses[0].BasePercent,true,formula);
            bool applies=expected.Next()<=chance;
            method.Invoke(exec,new object[]{owner,target,skill,hit});
            Check(target.Statuses.Any(s=>s.ClassId==24222)==applies,"Original probability/resistance diverged at "+i);
            if(applies) {
                successes++;var applied=target.Statuses.Single(s=>s.ClassId==24222);
                Check(applied.ApplyingSkillItemId==10565&&applied.SourceSkillRank==2&&applied.SourceSkillLevel==20
                    &&applied.CreatorId==owner.Id,"Dark status lost passive source properties");
            }
        }
        Check(successes>0&&successes<128&&def.TriggerCount==128,"Each successful occurrence must count once: successes="+successes+" count="+def.TriggerCount
            +" active="+status.ForbiddenType+"/"+status.StackCount+" chance="+c.CalculateStatusApplicationRate(owner,target,24222,def.Statuses[0].BasePercent,true,formula));
        int count=def.TriggerCount;
        Action invoke=()=>method.Invoke(exec,new object[]{owner,target,skill,hit});
        hit.Type.Dodge=true;invoke();hit.Type.Dodge=false;
        hit.Type.Blinding=true;invoke();hit.Type.Blinding=false;
        hit.Type.Immunity=true;invoke();hit.Type.Immunity=false;
        target.Side=owner.Side;invoke();target.Side=2;
        skill.ElementType=102;invoke();skill.ElementType=104;
        skill.EntityId=11360;invoke();skill.EntityId=10810;
        status.ForbiddenType=1;invoke();status.ForbiddenType=0;
        status.StackCount=0;invoke();status.StackCount=1;
        skill.CanTriggerChild=false;invoke();
        Check(def.TriggerCount==count,"Excluded occurrence changed trigger count");
        skill.CanTriggerStatusEntityIds.Add(25031);invoke();
        Check(def.TriggerCount==++count,"Explicit status whitelist was ignored");
        skill.CanTriggerChild=true;
        hit.Type.Block=true;invoke();hit.Type.Block=false;
        Check(def.TriggerCount==++count,"Successful blocked zero-damage hit must count");
        hit.ShieldDamage=123;hit.Formula=new FightDamageFormulaResult {Damage=-123};invoke();
        Check(def.TriggerCount==++count,"Full shield absorption must not require HP damage");
        hit.ShieldDamage=0;hit.Formula=null;
        def.MaxTriggerCount=count;invoke();Check(def.TriggerCount==count,"Maximum count ignored");def.MaxTriggerCount=-1;
        var copy=status.Components.Copy();copy.DarkHit.Statuses[0].BasePercent=0;copy.DarkHit.Elements.Clear();
        Check(def.Statuses[0].BasePercent>0&&def.Elements.Count==1,"Definition copy shares mutable data");
        cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),c,items);
        var restored=cp.Units[0].Statuses.Single(s=>s.ClassId==25031).Components.DarkHit;
        Check(restored.TriggerCount==count,"Checkpoint replayed or lost counter");
        restored.DefinitionResolved=false;
        FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),c,items);
        restored=cp.Units[0].Statuses.Single(s=>s.ClassId==25031).Components.DarkHit;
        Check(restored.Supported&&restored.DefinitionResolved&&restored.TriggerCount==count,"Legacy definition restoration lost counter");

        foreach(int entityId in new[]{10810,12120}) {
        owner=Unit(c,1,1,5,5);target=Unit(c,2,2,6,5);var other=Unit(c,3,2,7,5);
        status=c.CreateStatus(25031,1,1,1,0,10565,2,20,1);owner.Statuses.Add(status);
        status.Components.DarkHit.Statuses[0].BasePercent=1;status.Components.DarkHit.Statuses[0].AffectedByProp=false;
        skill=c.CreateSkill(entityId,0,entityId==10810?10029:0,2,20,1,c.CalculateSkillProps(entityId,2,20,1)
            .Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList());
        owner.Skills.Add(skill);owner.SkillState.Slots.Add(c.CreateSkillSlot(skill,0));
        cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
        cp.Units.Add(owner);cp.Units.Add(target);cp.Units.Add(other);
        var step=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),c,items).Step(0,1).Event;
        int hits=step.DamageEvents.Count(h=>h.SourceId==1&&h.SkillEntityId==entityId&&!h.Dodge&&!h.Immunity&&!h.Blinding);
        Check(hits>=(entityId==10810?2:3)&&status.Components.DarkHit.TriggerCount==hits
            &&(entityId==10810?new[]{target,other}:new[]{target}).All(u=>u.Statuses.Any(s=>s.ClassId==24222&&s.ApplyingSkillItemId==10565)),
            "Actual original multi-target/segment dark attack did not dispatch each successful hit: "+entityId+"/"+hits);
        }
    }
}
