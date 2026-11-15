using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleFighterHpStacksHarness
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static FightBattleUnitState Unit(int id, long hp = 1000, long max = 1000)
    {
        var unit = new FightBattleUnitState { Id=id, Side=id, CharacterType=1, Level=150, SubRank=1,
            X=4+id, Y=4, MoveMask=5, Hp=hp, MaxHp=max, LastMaxHp=max,
            Turn=new FightTurnUnit { Id=id, Hp=hp, Speed=id==1 ? 1000 : 900 } };
        foreach (var p in new[] {new FightPropertyValue {Id=1,Value=100},
            new FightPropertyValue {Id=2,Value=100}, new FightPropertyValue {Id=3,Value=max},
            new FightPropertyValue {Id=4,Value=id==1 ? 1000 : 900}}) unit.Properties.Add(p);
        return unit;
    }
    static int Layers(FightBattleUnitState unit, int entity) =>
        unit.Statuses.Find(s=>s.ClassId==entity)?.StackCount ?? 0;
    static FightBattleStatusState Attach(FightOfficialCombatCatalog catalog, FightBattleUnitState unit,
        int passive, int item) {
        var status=catalog.CreateStatus(passive,unit.Id,unit.Id,1,passive,item,6,17,1);
        unit.Statuses.Add(status);return status;
    }
    static FightBattleStepEvent NextCast(FightBattleExecutor runner, int entity)
    {
        for (int i=0;i<30;i++) {
            var step=runner.Step(0,runner.State.Counter+1).Event;
            if(step.SkillCasts.Exists(c=>c.SkillEntityId==entity)) return step;
        }
        throw new Exception("No fixture cast: "+entity);
    }
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        foreach(var pair in new[] {new[]{25063,24299,10582},new[]{25094,24379,10589}})
        {
            var unit=Unit(1);var source=Attach(catalog,unit,pair[0],pair[2]);
            source.Components.HpIncreaseUnit.AccumulatedHp=149;
            var units=new List<FightBattleUnitState>{unit};
            foreach(var hp in new long[]{1000,851,850,849,701,700,550,400,250,1,399,550,699,700,850,1000})
            {
                unit.Hp=hp;
                FightStatusTriggerRuntime.Apply(unit,catalog,false,units:units);
                int expected=(int)Math.Min(5m,decimal.Floor((1000m-hp)/150m));
                Check(Layers(unit,pair[1])==expected,"Current-HP tier mismatch: "+pair[2]+" HP="+hp);
                var before=unit.Statuses.Find(s=>s.ClassId==pair[1]);
                FightStatusTriggerRuntime.Apply(unit,catalog,false,units:units);
                Check(ReferenceEquals(before,unit.Statuses.Find(s=>s.ClassId==pair[1])),"Repeated notification replaced the buff.");
                FightStatusTriggerRuntime.ApplyHpIncrease(unit,500,catalog,units);
                Check(Layers(unit,pair[1])==expected && source.Components.HpIncreaseUnit.AccumulatedHp==0,
                    "The generic healing accumulator changed a current-HP tier.");
            }
            unit.Hp=700;
            FightStatusTriggerRuntime.Apply(unit,catalog,true,units:units);
            var buff=unit.Statuses.Find(s=>s.ClassId==pair[1]);
            Check(buff!=null && buff.SourceSkillRank==6 && buff.SourceSkillLevel==17
                && buff.ApplyingSkillItemId==pair[2] && buff.CreatorId==1,"Cultivation/source lost.");
            FightStatusPropertyRuntime.RebuildAll(units,catalog,formula);
            var two=new List<FightPropertyValue>(buff.ResolvedProperties);
            Check(two.Count>0,"Configured stack properties did not resolve.");
            unit.Hp=550;
            FightStatusTriggerRuntime.Apply(unit,catalog,false,units:units);
            FightStatusPropertyRuntime.RebuildAll(units,catalog,formula);
            foreach(var prop in two) Check(buff.ResolvedProperties.Exists(p=>p.Id==prop.Id
                && Math.Abs(p.Value*2-prop.Value*3)<=3),"Stack property did not grow from two to three layers.");

            unit=Unit(1,850);source=Attach(catalog,unit,pair[0],pair[2]);
            var enemy=Unit(2);units=new List<FightBattleUnitState>{unit,enemy};
            var heal=new FightBattleSkillDefinition {EntityId=990111,Slot=0,TargetType=0,HasCustomCure=true,CustomCure=50};
            heal.Range.Add(new FightGridPoint(0,0));
            heal.Hits.Add(new FightBattleHitDefinition {EntityId=990112,DamageType=8,ActionType=5});
            unit.Skills.Add(heal);unit.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
            foreach(var name in new[]{"BaseCureAdd","BaseBeCureAdd"})
                unit.Properties.Add(new FightPropertyValue {Id=catalog.PropertyType(name),Value=10000});
            var attack=new FightBattleSkillDefinition {EntityId=990121,Slot=0,TargetType=1};
            attack.Range.Add(new FightGridPoint(-1,0));
            attack.Hits.Add(new FightBattleHitDefinition {EntityId=990122,DamageType=8,ActionType=1,
                HasCustomDamage=true,CustomDamage=200});
            enemy.Skills.Add(attack);enemy.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
            var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,ResultRules=new FightBattleResultRules()};
            cp.Units.AddRange(units);
            var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            NextCast(runner,990111);
            Check(unit.Hp>=900 && Layers(unit,pair[1])==0,"Actual healing did not remove the entry tier: item="+pair[2]+" hp="+unit.Hp+" layers="+Layers(unit,pair[1]));
            NextCast(runner,990121);
            Check(unit.Hp>0 && Layers(unit,pair[1])==(int)Math.Min(5m,decimal.Floor((1000m-unit.Hp)/150m)),
                "Actual damage did not recalculate tiers.");
            cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
            unit=cp.Units.Find(u=>u.Id==1);
            NextCast(runner,990111);
            Check(Layers(unit,pair[1])==(int)Math.Min(5m,decimal.Floor((1000m-unit.Hp)/150m)),
                "Checkpoint restore lost current-HP tier semantics.");
            unit=Unit(1,850,1001);Attach(catalog,unit,pair[0],pair[2]);
            FightStatusTriggerRuntime.Apply(unit,catalog,true,units:new[]{unit});
            Check(Layers(unit,pair[1])==1,"Fractional max-HP boundary rounded away a tier.");
        }
    }
}
