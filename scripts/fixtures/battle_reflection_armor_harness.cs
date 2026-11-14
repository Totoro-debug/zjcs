using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleReflectionArmorHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Reflection armor: " + message); }
    static FightBattleUnitState Unit(int id, int x)
    {
        var u = new FightBattleUnitState { Id=id, Side=id, CharacterType=1, Profession=1,
            Level=30, SubRank=8, X=x, Y=5, Hp=1000000, MaxHp=1000000, LastMaxHp=1000000 };
        u.Turn.Id=id; u.Turn.Hp=u.Hp; u.Turn.Speed=id==1?1000:900;
        foreach (var pair in new Dictionary<int,long>{{1,1000},{2,10},{3,u.MaxHp},{4,u.Turn.Speed}})
            u.Properties.Add(new FightPropertyValue { Id=pair.Key, Value=pair.Value });
        return u;
    }
    static FightBattleSkillDefinition Skill(FightOfficialCombatCatalog c)
        => c.CreateSkill(10600,0,10008,2,30,8,c.CalculateSkillProps(10600,2,30,8)
            .Select(p=>new FightPropertyValue { Id=p.Key,Value=p.Value }).ToList());
    static FightBattleStatusState Status(FightOfficialCombatCatalog c,FightBattleUnitState owner)
        => c.CreateStatus(25035,owner.Id,owner.Id,775035,25035,10567,7,30,8,owner.Properties);

    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach (string mode in new[]{"partial","full","none","zero","dodge","blinding","immunity",
            "dead","die-action","child-denied","whitelist","forbidden","cure","overkill",
            "unknown-graph","unknown-port","wrong-target","no-result","blessing","replacement"})
        {
            var owner=Unit(2,6);var attacker=Unit(1,5);var status=Status(catalog,owner);owner.Statuses.Add(status);
            if(mode=="blessing")status.BlessPropertyScales=new List<FightStatusPropertyScale>{
                new FightStatusPropertyScale { EntityId=25035,PropertyId=233,Factor=2 }};
            if(mode=="replacement")status.ReplacementProperties=new List<FightStatusReplacementValue>{
                new FightStatusReplacementValue { PropertyId=233,RawValue=0 },
                new FightStatusReplacementValue { PropertyId=238,RawValue=17 }};
            FightStatusPropertyRuntime.RebuildAll(new[]{owner,attacker},catalog,formula);
            var skill=Skill(catalog);var hit=FightBattleHitCollector.CollectAll(skill).Single();
            var damage=new FightBattleDamageEvent { HasDamageValue=true,DamageValue=-80,Damage=-80,ShieldDamage=20,Delay=.28f };
            if(mode=="full")damage.Damage=damage.DamageValue=0;
            if(mode=="none")damage.ShieldDamage=0;
            if(mode=="zero")damage.Damage=damage.DamageValue=damage.ShieldDamage=0;
            if(mode=="dodge")damage.Dodge=true;
            if(mode=="blinding")damage.Blinding=true;
            if(mode=="immunity")damage.Immunity=true;
            if(mode=="dead")owner.Hp=0;
            if(mode=="die-action")owner.Statuses.Add(new FightBattleStatusState { ActionType=3 });
            if(mode=="child-denied"||mode=="whitelist")skill.CanTriggerChild=false;
            if(mode=="whitelist")skill.CanTriggerStatusEntityIds.Add(25035);
            if(mode=="forbidden")status.ForbiddenType=1;
            if(mode=="cure")damage.IsCure=true;
            if(mode=="overkill")damage.Damage=-1; // Never use clamped HP loss as the formula base.
            if(mode=="unknown-graph")status.LogicDefinitionJson=status.LogicDefinitionJson.Replace("\"SkillId\":11480","\"SkillId\":11481");
            if(mode=="unknown-port")status.LogicDefinitionJson=status.LogicDefinitionJson.Replace("\"Id\":1030","\"Id\":1031");
            if(mode=="no-result")damage.HasDamageValue=false;
            long beforeOwner=owner.Hp,beforeAttacker=attacker.Hp;
            var random=new FightRandomStream(34567);
            var invocations=FightStatusLogicRuntime.Apply(owner,status,FightStatusLogicEvent.AfterBeDamage,attacker,
                mode=="wrong-target"?attacker:owner,skill,hit,new[]{owner,attacker},catalog,random,damage,formula);
            bool eligible=new[]{"partial","full","whitelist","overkill","blessing","replacement"}.Contains(mode);
            Check(invocations.Count==(eligible?1:0)&&status.LogicTriggeredCount==(eligible?1:0),mode+" trigger gate");
            Check(random.Seed==34567&&status.LogicBlackboard.Count==0,"RNG/runtime blackboard changed: "+mode);
            Check(owner.Hp==beforeOwner&&attacker.Hp==beforeAttacker,"collection applied HP: "+mode);
            Check(status.LogicRulesSupported!=mode.StartsWith("unknown-"),"graph support flag: "+mode);
            if(eligible)
            {
                var v=invocations.Single();
                var props=status.StatusProperties.ToDictionary(p=>p.Id,p=>p.Value);
                long expected=Math.Abs(formula.CalculateStatusDamage(props,233,238,damage.ShieldAddFinalDamageValue,0));
                Check(expected>0&&v.HasCustomDamage&&v.CustomDamage==expected,"original result/formula base");
                if(mode=="blessing")Check(v.Trigger.BlessPropertyScales.Single().Factor==2
                    &&!ReferenceEquals(v.Trigger.BlessPropertyScales,status.BlessPropertyScales),"blessing source snapshot");
                if(mode=="replacement")Check(expected==17,"replacement/fixed damage did not precede formula");
                Check(v.SourceUnitId==owner.Id&&v.TargetUnitId==attacker.Id&&v.TargetPositionUnitId==attacker.Id
                    &&v.Trigger.EntityId==11480&&v.Trigger.SourceSkillItemId==10567&&v.Trigger.SourceSkillRank==7
                    &&v.Trigger.SourceSkillLevel==30&&v.Trigger.SourceSubRank==8&&v.SourceRootSkillId==25035
                    &&v.Delay==.28f,"source/cultivation/timing");
            }
            cases++;
        }
        int flows=0;
        foreach(int seed in new[]{1,17,50000})
        foreach(string mode in new[]{"partial","full","none","dodge","immunity","lethal","multi"})
        {
            var cp=new FightBattleCheckpoint { StageId=1001,MaxRound=10,RandomSeed=seed,
                Grid=new FightBattleGrid { Width=20,Height=20,DefaultGroundType=0 } };
            var actor=Unit(1,5);var target=Unit(2,6);cp.Units.Add(actor);cp.Units.Add(target);
            var slash=Skill(catalog);slash.LimitedTimes=1;
            if(mode=="multi")
            {
                var first=slash.HitGraphRoots.Single();
                slash.HitGraphRoots.Add(new FightBattleHitRoot { EntityId=first.EntityId,Delay=first.Delay+.4f,Anchor=first.Anchor });
            }
            actor.Skills.Add(slash);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(slash,0));
            target.Statuses.Add(Status(catalog,target));actor.Statuses.Add(Status(catalog,actor));
            if(mode!="none")
            {
                var shield=catalog.CreateStatus(24435,target.Id,target.Id,774435);
                shield.ShieldUsesCustomValue=true;shield.CustomShieldValue=mode=="full"?1000000:1;
                target.Statuses.Add(shield);
            }
            if(mode=="dodge")target.Properties.Add(new FightPropertyValue { Id=formula.PropertyId("DodgePercent"),Value=10000 });
            if(mode=="immunity")target.Statuses.Add(catalog.CreateStatus(24007,target.Id,target.Id,7724007));
            if(mode=="lethal"){target.Hp=target.Turn.Hp=1;}
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(cp.Sequence,cp.Counter+1).Event;resume.Step(restored.Sequence,restored.Counter+1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),"cold restoration: "+mode);
            var casts=step.SkillCasts.Where(s=>s.SkillEntityId==11480).ToArray();
            bool eligible=mode=="partial"||mode=="multi"||mode=="full";
            Check(casts.Length==(eligible?1:0),"actual reflection count: "+mode+"/"+seed);
            if(eligible)
            {
                var reflected=step.DamageEvents.Single(d=>d.SkillEntityId==11480);
                var incoming=step.DamageEvents.First(d=>d.SkillEntityId==10600);
                Check((mode=="full"?incoming.ShieldDamage>0&&incoming.Damage==0:incoming.ShieldDamage==1&&incoming.Damage<0)
                    &&reflected.Damage<0,"shield/custom damage: "+mode);
                Check(reflected.SourceId==target.Id&&reflected.TargetId==actor.Id&&reflected.ElementType==103
                    &&reflected.HitEntityId==11481&&reflected.DamageEntityId==11482
                    &&reflected.Delay>=incoming.Delay&&reflected.Delay<incoming.Delay+.2f,"original reaction chain/timing");
                Check(casts[0].Presentation!=null&&casts[0].SkillItemId==10567,"native presentation/source receipt");
                var controlSource=Unit(2,6);var controlTarget=Unit(1,5);
                var controlSkill=catalog.CreateSkill(11480,-1,10567,7,30,8,catalog.CalculateSkillProps(11480,7,30,8)
                    .Select(p=>new FightPropertyValue { Id=p.Key,Value=p.Value }).ToList());
                var status=target.Statuses.Single(s=>s.ClassId==25035);
                controlSkill.HasCustomDamage=true;
                controlSkill.CustomDamage=Math.Abs(formula.CalculateStatusDamage(
                    status.StatusProperties.ToDictionary(p=>p.Id,p=>p.Value),233,238,incoming.ShieldAddFinalDamageValue,0));
                var expected=new FightBattleHitExecutor(formula,new FightDamageSettlement()).Apply(new FightRandomStream(seed),
                    1,controlSource,controlTarget,controlSkill,FightBattleHitCollector.CollectAll(controlSkill).Single(),0,false);
                Check(reflected.DamageValue==expected.Settlement.DamageValue,"collected custom base was lost before actual hit");
            }
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(cp.Sequence,cp.Counter).Replayed&&FightBattleCheckpointSerializer.Save(cp)==saved,"replay duplicated reflection");
            flows++;
        }
        return "Reflection armor passed: "+cases+" filter/source/formula cases; "+flows+" executor/restoration/replay flows.";
    }
}
