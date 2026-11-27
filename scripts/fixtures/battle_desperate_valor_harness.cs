using System;
using System.Linq;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleDesperateValorHarness
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula)
    {
        int cases=0;
        foreach(int rank in new[]{3,6,7})
        foreach(int level in new[]{1,2,20})
        foreach(long maximum in new[]{1000L,1001L})
        {
            var actor=new FightBattleUnitState {Id=1,Side=1,CharacterType=1,Profession=7,
                Level=50,SubRank=5,Hp=maximum,MaxHp=maximum,LastMaxHp=maximum};
            actor.Properties.AddRange(new[]{new FightPropertyValue {Id=1,Value=1000},
                new FightPropertyValue {Id=2,Value=10},new FightPropertyValue {Id=3,Value=maximum}});
            var listener=catalog.CreateStatus(25034,1,1,1,25034,10547,rank,level,5);
            actor.Statuses.Add(listener);
            var target=new FightBattleUnitState {Id=2,Side=2,CharacterType=1,Level=50,SubRank=5,
                Hp=1000000000,MaxHp=1000000000,LastMaxHp=1000000000};
            target.Properties.AddRange(new[]{new FightPropertyValue {Id=1,Value=1000},
                new FightPropertyValue {Id=2,Value=10},new FightPropertyValue {Id=3,Value=target.MaxHp}});
            target.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("CritAvoidPercent"),Value=10000});
            var units=new[]{actor,target};
            foreach(int missingPercent in new[]{0,14,15,16,29,30,45,60,75,90,99,60,30,15,0})
            {
                actor.Hp=Math.Max(1L,maximum-(long)Math.Ceiling(maximum*missingPercent/100m));
                FightStatusTriggerRuntime.Apply(actor,catalog,false,units:units);
                FightStatusPropertyRuntime.RebuildAll(units,catalog,formula);
                int layers=(int)Math.Min(5m,decimal.Floor((maximum-actor.Hp)/(maximum*.15m)));
                var buff=actor.Statuses.SingleOrDefault(status=>status.ClassId==24242);
                Check((buff?.StackCount??0)==layers,"Desperate Valor current HP/cap mismatch.");
                if(buff!=null)
                {
                    Check(!buff.CanDispersed && buff.DurationRound==-1 && buff.CreatorId==1
                        && buff.ApplyingSkillItemId==10547 && buff.SourceSkillRank==rank
                        && buff.SourceSkillLevel==level && buff.SourceSubRank==5,
                        "Desperate Valor lost its permanent layer source or cultivation.");
                }
                long fixedAdd=buff?.StatusProperties.SingleOrDefault(p=>p.Id==formula.PropertyId("FixedStatusDmgAdd"))?.Value??0;
                long percent=buff?.StatusProperties.SingleOrDefault(p=>p.Id==formula.PropertyId("StatusDmgAddPer"))?.Value??0;
                var skill=new FightBattleSkillDefinition {EntityId=990547,ActionType=2,SkillType=0};
                skill.Properties.Add(new FightPropertyValue {Id=200,Value=10000});
                var hit=new FightBattleHitDefinition {ActionType=4,DamageType=1,BasicProperty=1,DamageProperty=200};
                var result=new FightBattleHitExecutor(formula,new FightDamageSettlement()).Apply(new FightRandomStream(13),1,
                    actor,target,skill,hit,1,false,onTransient:type=>{});
                Check(!result.Type.Critical && !result.Type.Block,"The deterministic damage control rolled a critical/block.");
                if(!result.Type.Critical && !result.Type.Block)
                {
                    long expected=-(long)((1000.0*1000.0/1010.0+fixedAdd*layers)*(1.0+percent*layers/10000.0));
                    Check(result.Settlement.RealDamageValue==expected,"Desperate Valor actual damage did not follow its cultivated layers: "
                        +result.Settlement.RealDamageValue+" / "+expected);
                }
                foreach(var unit in units) {unit.Turn.Id=unit.Id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=100;}
                var checkpoint=new FightBattleCheckpoint {StageId=1,RandomSeed=13,MaxRound=10};checkpoint.Units.AddRange(units);
                string saved=FightBattleCheckpointSerializer.Save(checkpoint);
                Check(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved))==saved,
                    "Desperate Valor stack source changed during save restoration.");
                cases++;
            }
        }
        return "Desperate Valor passed: "+cases+" cultivated current-HP tiers, healing, cap, actual damage and save cases.";
    }
}
