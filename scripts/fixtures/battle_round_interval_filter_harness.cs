using System;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleRoundIntervalFilterHarness
{
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static FightBattleUnitState Unit(int id,int side,int type,int speed)
    {
        var u=new FightBattleUnitState {Id=id,Side=side,CharacterType=type,Level=150,SubRank=1,
            X=id,Y=1,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000,
            Turn=new FightTurnUnit {Id=id,Hp=1000000,Speed=speed}};
        foreach(var pair in new[]{new long[]{1,1000},new long[]{2,1000},new long[]{3,1000000},new long[]{4,speed}})
            u.Properties.Add(new FightPropertyValue {Id=(int)pair[0],Value=pair[1]});
        var skill=new FightBattleSkillDefinition {EntityId=90010,Slot=0,TargetType=1};
        skill.Range.Add(new FightGridPoint(1,0));
        skill.Hits.Add(new FightBattleHitDefinition {EntityId=90011,HasCustomDamage=true,CustomDamage=-1});
        u.Skills.Add(skill);u.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
        return u;
    }
    public static void Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula)
    {
        foreach(int type in new[]{0,1,2,3,4,5,6,7,8,9})
        {
            var owner=Unit(1,1,type,1000);var enemy=Unit(2,2,2,1);var creator=Unit(3,1,type==8?1:8,1);
            var status=catalog.CreateStatus(24387,1,3,9924387,0,0,5,20,1);
            owner.Statuses.Add(status);
            var cp=new FightBattleCheckpoint {StageId=1,RandomSeed=23,MaxRound=100};
            cp.Units.AddRange(new[]{owner,enemy,creator});
            var component=status.Components.RoundInterval;
            Check(component.DefinitionResolved && !component.HasUnsupportedFilter
                && component.ExcludedApplyCharacterTypes.SequenceEqual(new[]{8}),"Original 24387 filter was not decoded.");
            var clone=component.Copy();clone.ExcludedApplyCharacterTypes.Clear();
            Check(component.ExcludedApplyCharacterTypes.Count==1,"Round filter copy shares its type list.");
            var saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var legacy=saved.Units[0].Statuses[0].Components.RoundInterval;
            Check(legacy.ExcludedApplyCharacterTypes.SequenceEqual(new[]{8}),"Round filter was lost in JSON.");
            legacy.DefinitionResolved=false;legacy.HasUnsupportedFilter=true;
            legacy.ExcludedApplyCharacterTypes.Clear();legacy.TriggeredCount=4;legacy.LastTriggeredRound=7;
            FightBattleExecutor.Restore(saved,formula,new FightDamageSettlement(),catalog);
            legacy=saved.Units[0].Statuses[0].Components.RoundInterval;
            Check(legacy.DefinitionResolved && !legacy.HasUnsupportedFilter
                && legacy.ExcludedApplyCharacterTypes.SequenceEqual(new[]{8})
                && legacy.TriggeredCount==4 && legacy.LastTriggeredRound==7,
                "Legacy round filter restore reset receipts or omitted the definition.");
            var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog);
            runner.Step(0,1);
            Check(component.TriggeredCount==(type==8?0:1),"24387 did not filter the applicator character type "+type);
            Check(component.LastTriggeredRound==(type==8?-1:owner.SkillState.Round),
                "Excluded applicator consumed the round receipt, or allowed trigger ran twice.");
            long next=cp.Timeline.Entries.Single(value=>value.Id==owner.Id).Next-cp.Timeline.Current;
            Check(next==(type==8?3162278L:2529822L),
                "Original 20% fast-forward did not change the real turn queue exactly once: "+next);
        }
    }
}
