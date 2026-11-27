using System;
using System.Linq;
using Zjcs.Combat.Official;
public static class BattleWildBladeSiphonHarness
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Prop(FightBattleUnitState unit,FightDamageFormula formula,string name,long value)
    {
        int id=formula.PropertyId(name);var existing=unit.Properties.FirstOrDefault(p=>p.Id==id);
        if(existing!=null)existing.Value=value;else unit.Properties.Add(new FightPropertyValue{Id=id,Value=value});
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(int rank in new[]{3,6,7})
        foreach(int level in new[]{1,2,20})
        foreach(int seed in new[]{13,104729})
        foreach(string mode in new[]{"critical","ordinary","dodge","block","forbidden","child-denied"})
        {
            var actor=Unit(1,1,6,6,formula);var enemy=Unit(2,2,6,7,formula);
            actor.Hp=actor.MaxHp/2;actor.Turn.Hp=actor.Hp;
            Prop(actor,formula,"CritRatePercent",mode=="ordinary"?0:20000);
            if(mode=="dodge")Prop(enemy,formula,"DodgePercent",10000);
            if(mode=="block")Prop(enemy,formula,"BlockPercent",10000);
            var props=catalog.CalculateSkillProps(11440,rank,level,5).Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList();
            var strike=catalog.CreateSkill(11440,0,10065,rank,level,5,props);strike.LimitedTimes=1;
            if(mode=="child-denied")strike.CanTriggerChild=false;
            actor.Skills.Add(strike);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(strike,0));
            var listener=catalog.CreateStatus(25009,1,1,1,25009,10519,rank,level,5,actor.Properties);
            if(mode=="forbidden")listener.ForbiddenType=1;
            actor.Statuses.Add(listener);
            Check(listener.DurationRound==-1 && !listener.CanDispersed && listener.Components.HitSkill.Crit
                && listener.Components.HitSkill.MaxInvokeCount==-1,"Wild Blade original listener configuration changed.");
            var cp=new FightBattleCheckpoint{StageId=1,RandomSeed=seed,MaxRound=10,
                Grid=new FightBattleGrid{Width=14,Height=14,DefaultGroundType=2}};
            cp.Units.AddRange(new[]{actor,enemy});
            var copy=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var run=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleExecutor.Begin(copy,formula,new FightDamageSettlement(),catalog,items);
            FightBattleStepEvent actual=null;
            for(int i=0;i<60 && !cp.Finished;i++)
            {
                actual=run.Step(cp.Sequence,cp.Counter+1).Event;
                restored.Step(copy.Sequence,copy.Counter+1);
                if(actual.SkillCasts.Any(c=>c.SkillEntityId==11440))break;
            }
            Check(actual!=null && actual.SkillCasts.Any(c=>c.SkillEntityId==11440),"Wild Blade attack was not executed.");
            var heals=actual.DamageEvents.Where(h=>h.SkillEntityId==10960).ToArray();
            int expectedCount=mode=="critical"?6:0;
            Check(heals.Length==expectedCount,"Wild Blade critical trigger count mismatch: "+mode+" / "+heals.Length);
            var healing=catalog.CalculateSkillProps(10960,rank,level,5);
            Check(formula.PropertyId("SkillCureByHp")==210,"The official cure property is not maximum-HP based.");
            long expectedCure=(long)(actor.MaxHp*(healing[210]/10000d)+healing[170]);
            Check(heals.All(h=>h.IsCure && h.SourceId==1 && h.TargetId==1 && h.Damage==expectedCure),
                "Wild Blade self cure or cultivated values changed: expected="+expectedCure+" actual="+string.Join(";",heals.Select(h=>h.SourceId+"/"+h.TargetId+"/"+h.Damage+"/"+h.IsCure))+" props="+string.Join(";",healing.Select(v=>v.Key+":"+v.Value)));
            Check(actor.Hp==Math.Min(actor.MaxHp,actor.MaxHp/2+expectedCount*expectedCure),"Wild Blade did not settle its actual self healing.");
            Check(listener.Components.HitSkill.InvokeCount==expectedCount,"Wild Blade listener count mismatch.");
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(saved==FightBattleCheckpointSerializer.Save(copy),"Wild Blade checkpoint continuation diverged.");
            Check(run.Step(cp.Sequence,cp.Counter).Replayed && saved==FightBattleCheckpointSerializer.Save(cp),
                "Wild Blade healing repeated on step replay.");
            cases++;
        }
        return "Wild Blade Siphon passed: "+cases+" cultivated critical/self-cure, exclusions, counter and checkpoint cases.";
    }
    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id = id, Side = side, CharacterType = 1, Profession = 7,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000000, MaxHp = 1000000000,
            LastMaxHp = 1000000000, MoveMask = 5
        };
        unit.Turn.Id = id;
        unit.Turn.Hp = unit.Hp;
        unit.Turn.Speed = id == 1 ? 1000 : 10;
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Attack"), Value = 1000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Defence"), Value = 10 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("MaxHp"), Value = unit.MaxHp });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("Speed"), Value = unit.Turn.Speed });
        foreach (var pair in new System.Collections.Generic.Dictionary<string,int> {
            { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 },
            { "BaseBlockAvoidPercentValue", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        return unit;
    }
}
