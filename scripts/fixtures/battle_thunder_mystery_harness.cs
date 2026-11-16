using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleThunderMysteryHarness
{
    static void Check(bool condition,string text) { if(!condition) throw new InvalidOperationException("Thunder mystery: "+text); }
    static FightBattleUnitState Unit(int id,FightDamageFormula formula)
    {
        var unit=new FightBattleUnitState {Id=id,Side=id,CharacterType=1,Level=50,SubRank=5,
            X=5,Y=id==1?5:6,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=id==1?1000:10;
        foreach(var entry in new Dictionary<int,long>{{1,1000},{2,10},{3,unit.MaxHp},{4,unit.Turn.Speed}})
            unit.Properties.Add(new FightPropertyValue {Id=entry.Key,Value=entry.Value});
        unit.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("CritAvoidPercent"),Value=10000});
        return unit;
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        foreach(string mode in new[]{"damage","dodge","blinding","immunity","cure","applicator-death"})
        {
            var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=5,RandomSeed=91,
                Grid=new FightBattleGrid {Width=12,Height=12}};
            var actor=Unit(1,formula);var target=Unit(2,formula);
            var passiveProps=catalog.CalculateSkillProps(25000,2,20,5)
                .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList();
            var passive=catalog.CreateStatus(25000,1,1,1,25000,10502,2,20,5,passiveProps);
            Check(Math.Abs(passive.Components.HitSkill.Rate-.3f)<.0001f,"Original probability changed");
            passive.Components.HitSkill.Rate=1f; // Exercise both outcomes without a probabilistic test.
            actor.Statuses.Add(passive);
            if(mode=="applicator-death") {
                Check(!passive.ForbiddenOnApplicatorDie,"Original status unexpectedly suppresses on applicator death");
                Check(!FightStatusLifecycle.SetForbidden(actor,passive,FightStatusLifecycle.ForbiddenApplicatorDie,true)
                    && passive.ForbiddenType==0,"Applicator death changed the original passive status");
            }
            var props=catalog.CalculateSkillProps(11010,2,20,5)
                .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList();
            var skill=catalog.CreateSkill(11010,0,10042,2,20,5,props);
            if(mode=="cure") {target.Hp=target.Turn.Hp=500000;skill.HitGraphEntities.Single(x=>x.EntityId==11011).ActionType=5;}
            if(mode=="dodge") target.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("DodgePercent"),Value=10000});
            if(mode=="blinding") actor.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("BlindingPercent"),Value=10000});
            if(mode=="immunity") target.Statuses.Add(catalog.CreateStatus(24007,2,2,724007));
            skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            cp.Units.AddRange(new[]{actor,target});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(0,1).Event;resumed.Step(0,1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),mode+" checkpoint differs");
            var hit=step.DamageEvents.Single(x=>x.SkillEntityId==11010);
            Check(mode!="dodge"||hit.Dodge,"Dodge fixture did not dodge");
            Check(mode!="blinding"||hit.Blinding,"Blinding fixture did not blind");
            Check(mode!="immunity"||hit.Immunity,"Immunity fixture did not block");
            Check(mode!="cure"||hit.IsCure,"Cure fixture did not heal");
            var children=step.SkillCasts.Where(x=>x.SkillEntityId==10530).ToArray();
            Check(children.Length==(mode=="damage"||mode=="applicator-death"?1:0),mode+" produced "+children.Length+" lightning casts");
            if(mode=="damage"||mode=="applicator-death") {
                Check(children[0].ActorId==1&&children[0].TargetIds.Contains(2)
                    &&children[0].SkillItemId==10502,"Lightning lost its passive source or hit target");
                Check(step.DamageEvents.Any(x=>x.SkillEntityId==10530&&x.TargetId==2&&x.Damage<0),
                    "Original lightning did not damage the hit target");
            }
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0,1).Replayed&&saved==FightBattleCheckpointSerializer.Save(cp),mode+" replay repeated trigger");
        }
        return "Thunder mystery passed: damage, dodge, blinding, immunity, cure, applicator-death handling and checkpoint/replay cases.";
    }
}
