using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleDamageTypesHarness
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static FightBattleUnitState Unit(int id,int type,FightDamageFormula formula)
    {
        var unit=new FightBattleUnitState {Id=id,Side=id,CharacterType=type,Level=50,SubRank=5,
            Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        unit.Properties.Add(new FightPropertyValue {Id=1,Value=100});
        unit.Properties.Add(new FightPropertyValue {Id=2,Value=0});
        unit.Properties.Add(new FightPropertyValue {Id=3,Value=unit.MaxHp});
        unit.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("CritAvoidPercent"),Value=10000});
        return unit;
    }
    public static string Run(FightDamageFormula formula,Func<long,long,long> officialShieldFormula)
    {
        int cases=0;
        foreach(int element in new[]{0,102})
        foreach(int targetType in new[]{2,3})
        foreach(var shieldKind in new[]{FightBattleShieldKind.Persistent,FightBattleShieldKind.Disposable,FightBattleShieldKind.Counted})
        foreach(long shieldValue in new[]{0L,1L,50L,99L,100L,101L,150L,200L,1000L})
        foreach(bool ignoreShield in new[]{false,true})
        foreach(int commonScale in new[]{1,2})
        foreach(int[] mode in new[]{new[]{1,0},new[]{1,1},new[]{8,0},new[]{8,1}})
        {
            var actor=Unit(1,1,formula);var target=Unit(2,targetType,formula);
            target.Statuses.Add(new FightBattleStatusState {ClassId=990547,InstanceId=1,OwnerId=2,CreatorId=2,
                DurationRound=-1,ShieldKind=shieldKind,ShieldInitialized=true,ShieldValue=shieldValue,
                ShieldRemainValue=shieldValue,ShieldRate=1f,ShieldRemainCount=1});
            var skill=new FightBattleSkillDefinition {EntityId=990630,SkillType=0,ActionType=2,ElementType=element};
            skill.Properties.Add(new FightPropertyValue {Id=200,Value=10000});
            var hit=new FightBattleHitDefinition {ActionType=4,DamageType=mode[0],TrueDamage=mode[1]==1,
                BasicProperty=1,DamageProperty=200,DamageParam=100,IgnoreShield=ignoreShield};
            int commonCalls=0;
            var result=new FightBattleHitExecutor(formula,new FightDamageSettlement()).Apply(new FightRandomStream(13),1,
                actor,target,skill,hit,1,false,onTransientDamage:context=>{
                    Check(!context.IsCure && context.Damage==-100,"The common damage stage received a type-specific boost too early.");
                    commonCalls++;context.Damage*=commonScale;context.AbsoluteDamage*=commonScale;
                });
            long expected=-100L*commonScale;
            if(mode[0]!=8 && mode[1]==1)
                expected=officialShieldFormula(expected*(targetType==3?2:1),ignoreShield?0:shieldValue);
            long blocked=ignoreShield?0:Math.Min(-expected,shieldValue);
            long hpDamage=expected+blocked;
            Check(commonCalls==1 && result.ShieldDamage==blocked && result.Formula.Damage==hpDamage
                && result.Settlement.RealDamageValue==hpDamage && target.Hp==1000000+hpDamage,
                "Damage channel/common trigger/shield/HP mismatch: element="+element+" target="+targetType
                    +" type="+mode[0]+" breaking="+mode[1]+" shield="+shieldKind+"/"+shieldValue
                    +" actual="+result.Formula.Damage+" expected="+hpDamage);
            cases++;
        }
        Check(formula.CalculateFixedTrueDamage(long.MinValue)==long.MinValue+1
            && formula.CalculateFixedTrueDamage(17)==-17 && formula.CalculateFixedTrueDamage(-17)==-17,
            "Fixed true damage normalization changed its signed boundary.");
        return "Damage type pipelines passed: "+cases+" physical/element/fixed/breaking cases, common response order, summon scaling, three shield kinds, shield bypass and HP settlement.";
    }
}
