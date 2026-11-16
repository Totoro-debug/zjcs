using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleElementHarmonyHarness
{
    static void Check(bool value,string message) { if (!value) throw new Exception("Element harmony: "+message); }
    static FightBattleUnitState Unit(int id)
    {
        var u=new FightBattleUnitState {Id=id,Side=id,CharacterType=1,Level=50,SubRank=5,
            X=5,Y=id==1?5:6,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=id==1?1000:10;
        foreach (var p in new[]{new long[]{1,1000},new long[]{2,10},new long[]{3,1000000},new long[]{4,u.Turn.Speed}})
            u.Properties.Add(new FightPropertyValue {Id=(int)p[0],Value=p[1]});
        return u;
    }
    static FightBattleSkillDefinition Skill(FightOfficialCombatCatalog c,int slot,int element,int kind)
    {
        var props=c.CalculateSkillProps(11010,2,20,5).Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList();
        var s=c.CreateSkill(11010,slot,10042,2,20,5,props);
        s.ElementType=element;
        if (kind==1) s.SkillType=1; // A damage-bearing debuff counts under the user's rule.
        if (kind==2) { s.HitGraphEntities.Clear();s.Hits.Clear();s.HasCustomDamage=false;s.SkillType=3; }
        if (kind==3) { s.EntityId=990001;s.HitGraphEntities.Clear();s.Hits.Clear();
            s.HasCustomDamage=false;s.ExtraSkills.Add(new FightBattleSkillExtra {SkillId=11010,Rate=1f}); }
        if (kind==5) foreach (var hit in s.HitGraphEntities) hit.DamageType=0;
        return s;
    }
    static long Scenario(FightOfficialCombatCatalog catalog,FightDamageFormula formula,
        FightOfficialSkillItemCatalog items,int[] elements,int[] kinds,int expected)
    {
        var player=Unit(1);var enemy=Unit(2);
        for (int i=0;i<elements.Length;i++) {
            var skill=Skill(catalog,i,elements[i],kinds[i]);
            player.Skills.Add(skill);
            if (kinds[i]!=4) player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        }
        var source=catalog.CreateStatus(25111,1,1,9925111,25111,10602,2,20,5);
        player.Statuses.Add(source);
        var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=5,RandomSeed=91,
            Grid=new FightBattleGrid {Width=12,Height=12}};
        cp.Units.AddRange(new[]{player,enemy});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var first=runner.Step(0,1).Event;
        var applied=player.Statuses.SingleOrDefault(s=>s.ClassId==24388);
        Check(expected==0 ? applied==null : applied!=null && applied.StackCount==expected,
            "wrong stack count for "+string.Join(",",elements)+": "+(applied?.StackCount.ToString() ?? "absent"));
        Check(first.StatusViewEvents.Any(e=>e.Kind==FightBattleStatusViewEventKind.Start
            && e.Status.ClassId==24388)==(expected>0),"original mastery status view start was lost");
        var saved=FightBattleCheckpointSerializer.Save(cp);
        var restored=FightBattleCheckpointSerializer.Load(saved);
        FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
        Check(FightBattleCheckpointSerializer.Save(restored)==saved,"restore changed the passive result");
        if (applied==null) return 0;
        Check(applied.ApplyingSkillItemId==10602 && applied.SourceSkillRank==2 && applied.SourceSkillLevel==20,
            "status lost passive source or cultivation");
        int prop=catalog.PropertyType("ElementMaster");
        var value=applied.ResolvedProperties.SingleOrDefault(p=>p.Id==prop);
        Check(value!=null && value.Value>0,"original mastery property did not resolve");
        var legacyPlayer=restored.Units.Single(u=>u.Id==1);
        var legacySource=legacyPlayer.Statuses.Single(s=>s.ClassId==25111);
        var legacyBuff=legacyPlayer.Statuses.Single(s=>s.ClassId==24388);
        legacySource.ElementHarmonyStartChecked=false;
        legacySource.Components.Start=null;
        legacyBuff.StackCount=1;
        var legacyRunner=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
        legacyRunner.Step(0,restored.Counter+1);
        Check(legacyPlayer.Statuses.Count(s=>s.ClassId==24388)==1 && legacyBuff.StackCount==expected,
            "legacy checkpoint duplicated or failed to correct the existing buff");
        return value.Value;
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        Scenario(catalog,formula,items,new int[0],new int[0],0);
        Scenario(catalog,formula,items,new[]{0,101},new[]{0,2},0);
        Scenario(catalog,formula,items,new[]{100},new[]{5},0);
        long one=Scenario(catalog,formula,items,new[]{102,102},new[]{0,1},1);
        Scenario(catalog,formula,items,new[]{102,101},new[]{0,4},1);
        long two=Scenario(catalog,formula,items,new[]{102,101,104},new[]{0,1,2},2);
        long child=Scenario(catalog,formula,items,new[]{103},new[]{3},1);
        long four=Scenario(catalog,formula,items,new[]{100,101,102,103,104},new[]{0,0,0,1,0},4);
        Check(two==one*2 && four==one*4 && child==one,"mastery did not scale with distinct damage elements");
        var configured=catalog.CalculateSkillProps(24388,2,20,5);
        Check(configured.TryGetValue(catalog.PropertyType("ElementMaster"),out long baseValue)
            && one==baseValue,"mastery differed from the original rank and level configuration");
        OfficialNestedEffects(catalog,formula,items);
        DynamicStart(catalog,formula,items);
        return "Element harmony passed: damage graphs, debuff, duplicate/neutral elements, four-stack cap, mastery and checkpoint.";
    }
    static void OfficialNestedEffects(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        var player=Unit(1);var enemy=Unit(2);
        foreach (var entry in new[]{new[]{10910,10036},new[]{11200,10061}}) {
            int slot=player.Skills.Count;
            var props=catalog.CalculateSkillProps(entry[0],2,20,5)
                .Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList();
            var skill=catalog.CreateSkill(entry[0],slot,entry[1],2,20,5,props);
            player.Skills.Add(skill);player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        }
        player.Statuses.Add(catalog.CreateStatus(25111,1,1,9925111,25111,10602,2,20,5));
        var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=5,RandomSeed=91,
            Grid=new FightBattleGrid {Width=12,Height=12}};
        cp.Units.AddRange(new[]{player,enemy});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
        FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items).Step(0,1);
        Check(player.Statuses.Single(s=>s.ClassId==24388).StackCount==2,
            "original random-hit wind damage or flame status damage was not counted");
    }
    static void DynamicStart(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        var player=Unit(1);var enemy=Unit(2);
        var skill=Skill(catalog,0,102,0);
        skill.HitGraphEntities.Single(h=>h.EntityId==11011).AppliedStatuses.Add(
            new FightBattleStatusApplication {StatusId=25111,TargetType=1,Chance=1f,AffectedByProp=false});
        player.Skills.Add(skill);player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=5,RandomSeed=91,
            Grid=new FightBattleGrid {Width=12,Height=12}};
        cp.Units.AddRange(new[]{player,enemy});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        runner.Step(0,1);
        var source=player.Statuses.Single(s=>s.ClassId==25111);
        Check(source.ElementHarmonyStartChecked && player.Statuses.Single(s=>s.ClassId==24388).StackCount==1,
            "mid-battle source start did not apply exactly one stack");
        var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
        resumed.Step(0,restored.Counter+1);
        Check(restored.Units.Single(u=>u.Id==1).Statuses.Single(s=>s.ClassId==24388).StackCount==1,
            "mid-battle start was replayed after checkpoint restore");
    }
}
