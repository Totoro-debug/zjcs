using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleSwordCounterattackHarness
{
    static void Check(bool value,string message)
    { if(!value)throw new InvalidOperationException("Sword counterattack: "+message); }
    static FightBattleUnitState Unit(int id,int x,int y)
    {
        var u=new FightBattleUnitState{Id=id,Side=id,CharacterType=1,Profession=1,Level=20,SubRank=1,
            X=x,Y=y,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=id==1?1000:900;
        foreach(var pair in new Dictionary<int,long>{{1,1000},{2,10},{3,u.MaxHp},{4,u.Turn.Speed}})
            u.Properties.Add(new FightPropertyValue{Id=pair.Key,Value=pair.Value});
        return u;
    }
    static FightBattleSkillDefinition Skill(FightOfficialCombatCatalog c,int entity,int item,int rank=2,int level=20,int subRank=1)
        => c.CreateSkill(entity,0,item,rank,level,subRank,c.CalculateSkillProps(entity,rank,level,subRank)
            .Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList());
    static FightBattleStatusState Status(FightOfficialCombatCatalog c,FightBattleUnitState owner,int rank=2,int level=20,int subRank=1)
        => c.CreateStatus(25017,owner.Id,owner.Id,775017,25017,10537,rank,level,subRank,owner.Properties);

    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(int seed in new[]{1,17,50000,100000,1000000,123456789})
        foreach(string mode in new[]{"normal","full-shield","dodge","blinding","immunity","dead","die-action",
            "outside","large-body","same-cell","child-denied","child-whitelist","forbidden","cure"})
        {
            var owner=Unit(1,5,5);var attacker=Unit(2,6,5);
            var status=Status(catalog,owner);owner.Statuses.Add(status);
            var skill=Skill(catalog,10600,10008);var hit=FightBattleHitCollector.CollectAll(skill).Single();
            var damage=new FightBattleDamageEvent{HasDamageValue=true,DamageValue=-100,Damage=-100,Delay=.2f};
            if(mode=="full-shield"){damage.Damage=damage.DamageValue=0;damage.ShieldDamage=100;}
            if(mode=="dodge")damage.Dodge=true;
            if(mode=="blinding")damage.Blinding=true;
            if(mode=="immunity")damage.Immunity=true;
            if(mode=="dead")owner.Hp=0;
            if(mode=="die-action")owner.Statuses.Add(new FightBattleStatusState{ActionType=3});
            if(mode=="outside"||mode=="large-body")attacker.X=8;
            if(mode=="large-body")attacker.BodyOffsets.Add(new FightGridPoint(-1,0));
            if(mode=="same-cell")attacker.X=5;
            if(mode=="child-denied"||mode=="child-whitelist")skill.CanTriggerChild=false;
            if(mode=="child-whitelist")skill.CanTriggerStatusEntityIds.Add(25017);
            if(mode=="forbidden")status.ForbiddenType=1;
            if(mode=="cure")damage.IsCure=true;
            bool eligible=new[]{"normal","full-shield","large-body","child-whitelist"}.Contains(mode);
            var expectedRandom=new FightRandomStream(seed);
            bool success=eligible&&expectedRandom.Next()<=.5;
            var random=new FightRandomStream(seed);
            var result=FightStatusLogicRuntime.Apply(owner,status,FightStatusLogicEvent.AfterBeDamage,
                attacker,owner,skill,hit,new[]{owner,attacker},catalog,random,damage);
            Check(status.LogicRulesSupported,"original 25017 graph was rejected in "+mode);
            Check(result.Count==(success?1:0)&&status.LogicTriggeredCount==(success?1:0),"filter/probability mismatch: "+mode+"/"+seed);
            Check(random.Seed==expectedRandom.Seed,"ineligible result consumed probability: "+mode+"/"+seed);
            Check(status.LogicBlackboard.Count==0,"event-local attacker port leaked into save");
            if(success)
            {
                var value=result.Single();
                Check(value.SourceUnitId==owner.Id&&value.TargetUnitId==attacker.Id&&value.TargetPositionUnitId==attacker.Id
                    &&value.Trigger.EntityId==11210&&value.Trigger.SourceSkillItemId==10537
                    &&value.Trigger.SourceSkillRank==2&&value.Trigger.SourceSkillLevel==20&&value.Trigger.SourceSubRank==1
                    &&value.SourceRootSkillId==25017&&Math.Abs(value.Delay-.2f)<.00001f,"counterattack direction/source/timing changed");
            }
            cases++;
        }
        int flows=RunExecutor(catalog,formula,items);
        return "Sword counterattack passed: "+cases+" original graph filter/probability cases; "+flows+" actual executor/restoration flows.";
    }

    static int RunExecutor(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int count=0,counterCount=0,shieldCounterCount=0;
        foreach(int seed in new[]{1,17,50000,100000})
        foreach(bool both in new[]{false,true})
        foreach(string mode in new[]{"normal","full-shield","dodge","blinding","immunity","lethal"})
        {
            var cp=new FightBattleCheckpoint{StageId=1001,MaxRound=10,RandomSeed=seed,
                Grid=new FightBattleGrid{Width=20,Height=20,DefaultGroundType=0}};
            var actor=Unit(1,5,5);var target=Unit(2,6,5);cp.Units.Add(actor);cp.Units.Add(target);
            var slash=Skill(catalog,10600,10008);slash.LimitedTimes=1;
            actor.Skills.Add(slash);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(slash,0));
            target.Statuses.Add(Status(catalog,target,7,30,8));
            if(both)actor.Statuses.Add(Status(catalog,actor,3,21,2));
            if(mode=="full-shield")
            {
                var shield=catalog.CreateStatus(24435,target.Id,target.Id,774435);
                shield.ShieldUsesCustomValue=true;shield.CustomShieldValue=1000000;
                target.Statuses.Add(shield);
            }
            if(mode=="dodge")target.Properties.Add(new FightPropertyValue{Id=formula.PropertyId("DodgePercent"),Value=10000});
            if(mode=="blinding")actor.Properties.Add(new FightPropertyValue{Id=formula.PropertyId("BlindingPercent"),Value=10000});
            if(mode=="immunity")target.Statuses.Add(catalog.CreateStatus(24007,target.Id,target.Id,7724007));
            if(mode=="lethal"){target.Hp=1;target.Turn.Hp=1;}
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            string initial=FightBattleCheckpointSerializer.Save(cp);
            var restored=FightBattleCheckpointSerializer.Load(initial);
            var resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(cp.Sequence,cp.Counter+1).Event;resume.Step(restored.Sequence,restored.Counter+1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),"restored hit/counterattack diverged");
            var counters=step.SkillCasts.Where(s=>s.SkillEntityId==11210).ToArray();
            var incoming=step.DamageEvents.Single(d=>d.SkillEntityId==10600);
            bool missed=mode=="dodge"||mode=="blinding"||mode=="immunity";
            if(missed)Check(counters.Length==0&&incoming.Damage==0,"miss triggered a counterattack: "+mode);
            if(mode=="lethal")Check(target.Hp==0&&counters.Length==0,"dead holder counterattacked after a lethal hit");
            if(mode=="full-shield")Check(incoming.ShieldDamage>0&&incoming.Damage==0&&target.Hp==1000000,
                "actual shield did not absorb the full incoming hit");
            Check(counters.Length<=1,"counterattack recursively triggered another counterattack");
            counterCount+=counters.Length;if(mode=="full-shield")shieldCounterCount+=counters.Length;
            foreach(var cast in counters)
            {
                Check(cast.ActorId==target.Id&&cast.TargetIds.SequenceEqual(new[]{actor.Id})&&cast.Slot==-1
                    &&cast.SkillItemId==10537,"actual equipped attack did not counter toward its attacker");
                var damage=step.DamageEvents.Single(d=>d.SkillEntityId==11210);
                Check(damage.SourceId==target.Id&&damage.TargetId==actor.Id&&damage.Damage<0
                    &&damage.Delay>=.28f-.00001f,"counterattack hit/source/delay differs");
            }
            Check(actor.SkillState.Slots.Single().ReleasedTimes==1&&target.SkillState.Slots.Count==0,
                "passive counterattack consumed an equipped slot");
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(cp.Sequence,cp.Counter).Replayed&&FightBattleCheckpointSerializer.Save(cp)==saved,
                "receipt replay repeated the counterattack");
            count++;
        }
        Check(counterCount>0&&shieldCounterCount>0,"executor coverage never produced an actual normal/full-shield counterattack");
        return count;
    }
}
