using System;
using System.Linq;
using Zjcs.Combat.Official;
public static class BattleJudgmentBladeHarness
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Prop(FightBattleUnitState unit,FightDamageFormula formula,string name,long value)
    {
        int id=formula.PropertyId(name);var old=unit.Properties.FirstOrDefault(p=>p.Id==id);
        if(old!=null)old.Value=value;else unit.Properties.Add(new FightPropertyValue{Id=id,Value=value});
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(int rank in new[]{3,6,7})
        foreach(int level in new[]{1,2,20})
        foreach(int seed in new[]{13,104729})
        foreach(string mode in new[]{"ordinary","critical","dodge","forbidden","child-denied","partial"})
        {
            var actor=Unit(1,1,6,6,formula);var enemy=Unit(2,2,6,7,formula);
            if(mode=="critical")Prop(actor,formula,"CritRatePercent",20000);
            if(mode=="dodge")Prop(enemy,formula,"DodgePercent",10000);
            Prop(enemy,formula,"EffectDodgeValue",20000);
            var props=catalog.CalculateSkillProps(11440,3,1,5).Select(p=>new FightPropertyValue{Id=p.Key,Value=p.Value}).ToList();
            var strike=catalog.CreateSkill(11440,0,10065,3,1,5,props);
            strike.LimitedTimes=mode=="partial"?1:5;
            if(mode=="child-denied")strike.CanTriggerChild=false;
            actor.Skills.Add(strike);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(strike,0));
            var listener=catalog.CreateStatus(25080,1,1,1,25080,10574,rank,level,5,actor.Properties);
            if(mode=="forbidden")listener.ForbiddenType=1;
            actor.Statuses.Add(listener);
            Check(listener.DurationRound==-1 && !listener.CanDispersed && !listener.Components.HitSkill.Crit
                && listener.Components.HitSkill.MaxInvokeCount==-1,"Judgment Blade original hit listener changed.");
            var markTemplate=catalog.CreateStatus(24316,2,1,2,25080,10574,rank,level,5,actor.Properties);
            Check(markTemplate.DurationRound==-1 && markTemplate.CanDispersed && markTemplate.IsOpenStack
                && markTemplate.MaxStackedCount==20 && markTemplate.StackCountThreshold==10
                && markTemplate.StackCountMaxTriggers==1 && markTemplate.StackCountRemoveOnMax
                && markTemplate.StackCountRulesSupported,"Judgment Blade original ten-layer threshold changed.");
            var cp=new FightBattleCheckpoint{StageId=1,RandomSeed=seed,MaxRound=30,
                Grid=new FightBattleGrid{Width=14,Height=14,DefaultGroundType=2}};
            cp.Units.AddRange(new[]{actor,enemy});
            var copy=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var run=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleExecutor.Begin(copy,formula,new FightDamageSettlement(),catalog,items);
            int casts=0,eligible=0,bursts=0;
            for(int i=0;i<160 && !cp.Finished;i++)
            {
                var step=run.Step(cp.Sequence,cp.Counter+1).Event;restored.Step(copy.Sequence,copy.Counter+1);
                casts+=step.SkillCasts.Count(c=>c.ActorId==1 && c.SkillEntityId==11440);
                if(mode=="ordinary" || mode=="critical" || mode=="partial")
                    eligible+=step.DamageEvents.Count(h=>h.SourceId==1 && h.SkillEntityId==11440 && h.Damage<0 && !h.Dodge);
                var hits=step.DamageEvents.Where(h=>h.SkillEntityId==11810).ToArray();bursts+=hits.Length;
                Check(hits.All(h=>h.SourceId==1 && h.TargetId==2 && h.SkillItemId==10574 && h.HitEntityId==11811
                    && h.DamageEntityId==10004 && h.DamageType==1 && h.ElementType==0 && h.Damage<0
                    && !h.IsCure && !h.TrueDamage),"Judgment Blade physical child strike lost its actual source or graph.");
                if(mode!="critical")
                {
                    var values=catalog.CalculateSkillProps(11810,rank,level,5);
                    long expected=-(long)((1000d*values[200]/10000d+values[190])*1000d/1010d);
                    Check(hits.All(h=>h.Damage==expected),"Judgment Blade did not inherit passive cultivation: "+expected+" / "+string.Join(";",hits.Select(h=>h.Damage)));
                }
                Check(bursts==eligible/10,"Judgment Blade did not trigger once at each ten eligible hits: "+mode+" / "+eligible+" / "+bursts);
                var marks=enemy.Statuses.Where(s=>s.ClassId==24316).ToArray();
                Check(marks.Length==(eligible%10==0?0:1) && (marks.Length==0 || marks[0].StackCount==eligible%10),
                    "Judgment Blade did not consume the ten-layer mark and retain the remainder.");
                if(marks.Length>0)Check(marks[0].CreatorId==1 && marks[0].ApplyingSkillItemId==10574
                    && marks[0].SourceSkillRank==rank && marks[0].SourceSkillLevel==level,
                    "Judgment Blade mark lost its cultivated creator.");
                string saved=FightBattleCheckpointSerializer.Save(cp);
                Check(saved==FightBattleCheckpointSerializer.Save(copy),"Judgment Blade checkpoint continuation diverged.");
                Check(run.Step(cp.Sequence,cp.Counter).Replayed && saved==FightBattleCheckpointSerializer.Save(cp),
                    "Judgment Blade repeated a mark or threshold strike after replay.");
                if(casts==strike.LimitedTimes)break;
            }
            Check(casts==strike.LimitedTimes && listener.Components.HitSkill.InvokeCount==eligible,
                "Judgment Blade attack count or listener invocation count changed.");
            Check(eligible==(mode=="partial"?6:mode=="ordinary" || mode=="critical"?30:0),
                "Judgment Blade eligible hit fixture did not execute its required branch.");
            cases++;
        }
        return "Judgment Blade passed: "+cases+" cultivated ten-layer/consumption, critical and exclusion, child damage and restore cases.";
    }
    private static FightBattleUnitState Unit(int id,int side,int x,int y,FightDamageFormula formula)
    {
        var u=new FightBattleUnitState{Id=id,Side=side,CharacterType=1,Profession=7,Level=50,SubRank=5,
            X=x,Y=y,Hp=1000000000,MaxHp=1000000000,LastMaxHp=1000000000,MoveMask=5};
        u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=id==1?1000:10;
        foreach(var p in new System.Collections.Generic.Dictionary<string,long>{{"Attack",1000},{"Defence",10},
            {"MaxHp",u.MaxHp},{"Speed",u.Turn.Speed},{"BaseCritRatePercentValue",10000},
            {"BaseCritAvoidPercentValue",10000},{"CritAvoidPercent",10000},{"BaseBlockPercentValue",10000},
            {"BaseBlockAvoidPercentValue",10000}})Prop(u,formula,p.Key,p.Value);
        return u;
    }
}
