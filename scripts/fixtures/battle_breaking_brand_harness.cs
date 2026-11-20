using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleBreakingBrandHarness
{
    static void Check(bool value,string message)
    { if(!value)throw new InvalidOperationException("Breaking brand: "+message); }
    static FightBattleUnitState Unit(int id,int x,int y,FightDamageFormula formula)
    {
        var unit=new FightBattleUnitState{Id=id,Side=id,CharacterType=1,Profession=6,Level=50,SubRank=5,
            X=x,Y=y,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=id==1?1000:10;
        foreach(var value in new Dictionary<int,long>{{1,1000},{2,10},{3,unit.MaxHp},{4,unit.Turn.Speed}})
            unit.Properties.Add(new FightPropertyValue{Id=value.Key,Value=value.Value});
        foreach(var name in new[]{"BaseBlockPercentValue","BaseBlockAvoidPercentValue","BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue","BaseEffectRate","BaseEffectDodge"})
            unit.Properties.Add(new FightPropertyValue{Id=formula.PropertyId(name),Value=10000});
        unit.Properties.Add(new FightPropertyValue{Id=formula.PropertyId("CritAvoidPercent"),Value=10000});
        return unit;
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(var direction in new[]{new[]{0,1},new[]{0,-1},new[]{-1,0},new[]{1,0}})
        foreach(var growth in new[]{new[]{2,20,1},new[]{7,30,5}})
        foreach(string mode in new[]{"single","large","zero-delay"})
        {
            var cp=new FightBattleCheckpoint{StageId=1001,MaxRound=5,RandomSeed=91,
                Grid=new FightBattleGrid{Width=12,Height=12}};
            var actor=Unit(1,5,5,formula);var target=Unit(2,5+direction[0],5+direction[1],formula);
            if(mode=="large")target.BodyOffsets.AddRange(new[]{new FightGridPoint(0,0),
                new FightGridPoint(direction[0],direction[1]),new FightGridPoint(0,0)});
            var skill=catalog.CreateSkill(11010,0,10042,growth[0],growth[1],growth[2],
                catalog.CalculateSkillProps(11010,growth[0],growth[1],growth[2])
                    .Select(value=>new FightPropertyValue{Id=value.Key,Value=value.Value}).ToList());
            var fixedHit=skill.HitGraphEntities.Single(hit=>hit.EntityId==11011);
            Check(fixedHit.ScopeEntries.Count==1&&Math.Abs(fixedHit.ScopeEntries[0].Delay-.12f)<.00001f,
                "Original 11011 scope delay changed");
            if(mode=="zero-delay")fixedHit.ScopeEntries[0].Delay=0;
            float scopeDelay=fixedHit.ScopeEntries[0].Delay;
            float expected=skill.HitGraphRoots.Single().Delay+scopeDelay;
            skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            cp.Units.AddRange(new[]{actor,target});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(0,1).Event;resume.Step(0,1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),"Cold continuation diverged");
            var damage=step.DamageEvents.Single();
            Check(Math.Abs(damage.Delay-expected)<.00001f,
                "Original single-cell delay omitted: expected="+expected+" actual="+damage.Delay+" mode="+mode);
            Check(damage.HitEntityId==11011&&damage.DamageEntityId==11012&&damage.Damage<0
                &&damage.BeforeHp-damage.AfterHp==-damage.Damage&&damage.ElementType==102&&!damage.TrueDamage,
                "Original damage/settlement differs");
            var mark=target.Statuses.Single(status=>status.ClassId==24052);
            Check(mark.ApplyingSkillItemId==10042&&mark.CreatorId==actor.Id&&mark.SourceSkillRank==growth[0]
                &&mark.SourceSkillLevel==growth[1]&&mark.SourceSubRank==growth[2],"Mark source/cultivation differs");
            Check(step.StatusViewEvents.Any(view=>view.Kind==FightBattleStatusViewEventKind.Start
                &&view.Status.ClassId==24052&&Math.Abs(view.Delay-expected)<.00001f),"Mark start did not follow the original hit delay");
            Check(Math.Abs(step.SkillCasts.Single().HitSelections.Single().Targets.Single().ScopeDelay-scopeDelay)<.00001f,
                "Hit selection lost its original scope delay");
            var saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0,1).Replayed&&FightBattleCheckpointSerializer.Save(cp)==saved,"Receipt replay repeated the hit or mark");
            cases++;
        }
        CheckConsumption(catalog,formula,items);
        return "Breaking brand passed: "+cases+" original hit and checkpoint cases; next-hit bonus/consumption, reapplication, healing and miss gates, status events and restoration.";
    }

    static void CheckConsumption(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        long plain=NextHit(catalog,formula,items,false,false);
        long branded=NextHit(catalog,formula,items,true,false);
        Check(Math.Abs(branded-plain*1.6)<=2,"Mark did not increase the next skill damage by 60%: "+plain+" / "+branded);
        NextHit(catalog,formula,items,true,true);
        NextHit(catalog,formula,items,true,false,true);
        foreach(var miss in new[]{"dodge","blinding","immunity"})
            NextHit(catalog,formula,items,true,false,false,miss);
        long otherPlain=NextHit(catalog,formula,items,false,false,false,null,10);
        long otherMarked=NextHit(catalog,formula,items,true,false,false,null,10);
        Check(otherPlain==otherMarked,"Mark increased damage outside its configured skill actions: "+otherPlain+" / "+otherMarked);
        long movePlain=NextHit(catalog,formula,items,false,false,false,null,6);
        long moveMarked=NextHit(catalog,formula,items,true,false,false,null,6);
        Check(Math.Abs(moveMarked-movePlain*1.6)<=2,"Mark did not increase configured SkillMove damage: "+movePlain+" / "+moveMarked);
    }
    static long NextHit(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items,bool branded,bool reapply,bool cure=false,string miss=null,int action=2)
    {
        var cp=new FightBattleCheckpoint{StageId=1001,MaxRound=5,RandomSeed=91,
            Grid=new FightBattleGrid{Width=12,Height=12}};
        var actor=Unit(1,5,5,formula);var target=Unit(2,5,6,formula);
        var props=catalog.CalculateSkillProps(11010,2,20,1)
            .Select(value=>new FightPropertyValue{Id=value.Key,Value=value.Value}).ToList();
        if(branded) target.Statuses.Add(catalog.CreateStatus(24052,2,1,1,11010,10042,2,20,1,props));
        var skill=catalog.CreateSkill(11010,0,10042,2,20,1,props);
        skill.ActionType=action;
        if(!reapply) skill.HitGraphEntities.Single(hit=>hit.EntityId==11011).AppliedStatuses.Clear();
        if(cure) { target.Hp=target.Turn.Hp=500000; skill.HitGraphEntities.Single(hit=>hit.EntityId==11011).ActionType=5; }
        if(miss=="dodge") target.Properties.Add(new FightPropertyValue{Id=formula.PropertyId("DodgePercent"),Value=10000});
        if(miss=="blinding") actor.Properties.Add(new FightPropertyValue{Id=formula.PropertyId("BlindingPercent"),Value=10000});
        if(miss=="immunity") target.Statuses.Add(catalog.CreateStatus(24007,2,2,724007));
        skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        cp.Units.AddRange(new[]{actor,target});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
        var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
        var step=executor.Step(0,1).Event;
        resume.Step(0,1);
        Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),
            "Mark consumption diverged after checkpoint restoration");
        if(cure) {
            Check(step.DamageEvents.Single().IsCure&&target.Statuses.Any(status=>status.ClassId==24052),
                "Healing consumed the mark before damage");
            return 0;
        }
        if(miss!=null) {
            var result=step.DamageEvents.Single();
            Check((miss=="dodge"?result.Dodge:miss=="blinding"?result.Blinding:result.Immunity)
                &&target.Statuses.Any(status=>status.ClassId==24052),miss+" consumed the mark without damage");
            return 0;
        }
        Check(step.DamageEvents.Single().Damage<0,"Next skill did not deal damage");
        if(action==10) {
            Check(target.Statuses.Any(status=>status.ClassId==24052)==branded,
                "Non-skill damage consumed the mark before the next skill");
            Check(!step.StatusViewEvents.Any(view=>view.Kind==FightBattleStatusViewEventKind.End
                &&view.Status.ClassId==24052),"Non-skill damage ended the mark's view");
            return -step.DamageEvents.Single().Damage;
        }
        Check(target.Statuses.Count(status=>status.ClassId==24052)==(reapply?1:0),
            reapply?"A new mark was lost when consuming the old mark":"Mark survived the next damaging skill");
        if(branded) Check(step.StatusViewEvents.Any(view=>view.Kind==FightBattleStatusViewEventKind.End
            &&view.Status.ClassId==24052),"Consumed mark did not emit its status end");
        if(reapply) Check(step.StatusViewEvents.Any(view=>view.Kind==FightBattleStatusViewEventKind.Start
            &&view.Status.ClassId==24052),"Replacement mark did not emit its status start");
        return -step.DamageEvents.Single().Damage;
    }
}
