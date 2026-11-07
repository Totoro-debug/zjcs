using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleHitSummonHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static FightBattleUnitState Unit(int id, int side, int x, int y, int speed = 1)
    {
        var u = new FightBattleUnitState { Id=id, Side=side, CharacterType=side == 1 ? 1 : 2,
            Hp=1000000, MaxHp=1000000, LastMaxHp=1000000, Level=150, SubRank=1,
            MoveMask=5, X=x, Y=y, Turn=new FightTurnUnit {Id=id,Hp=1000000,Speed=speed} };
        u.Properties.Add(new FightPropertyValue {Id=1,Value=10001});
        u.Properties.Add(new FightPropertyValue {Id=2,Value=10003});
        u.Properties.Add(new FightPropertyValue {Id=3,Value=1000000});
        u.Properties.Add(new FightPropertyValue {Id=4,Value=speed});
        return u;
    }
    static FightBattleCheckpoint State(params FightBattleUnitState[] units)
    {
        var cp = new FightBattleCheckpoint {StageId=1001,RandomSeed=13,
            Grid=new FightBattleGrid {Width=20,Height=20}, ResultRules=new FightBattleResultRules()};
        cp.Units.AddRange(units); return cp;
    }
    static void Equip(FightBattleUnitState unit, FightBattleSkillDefinition skill)
    {
        unit.Skills.Add(skill);unit.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
    }
    static FightBattleStatusState Status(FightOfficialCombatCatalog catalog, int id, int owner, int serial)
    {
        return catalog.CreateStatus(id, owner, owner, serial, 0, 0, 5, 20, 1);
    }
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        // A real official summon exercises registration, owner lookup and status application.
        var player=Unit(1,1,5,5,1000);var enemy=Unit(2,2,6,5);
        var creation=Status(catalog,25012,1,1);player.Statuses.Add(creation);
        Equip(player,catalog.CreateSkill(12140,0,10095,5,20,1,null));
        var cp=State(player,enemy);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        runner.Step(0,1);
        var summon=cp.Units.Find(u=>u.MasterId==1);
        Check(summon!=null,"Official 12140 did not create a summon.");
        Check(creation.Components.HitSummon.TriggeredCount==1
            && summon.Statuses.Exists(s=>s.ClassId==24070),"Creation did not apply official 25012/24070.");

        player=Unit(1,1,5,5,1000);enemy=Unit(2,2,6,5);
        var filtered=Status(catalog,60250,1,1);player.Statuses.Add(filtered);
        var filteredRemoval=Status(catalog,25015,1,2);player.Statuses.Add(filteredRemoval);
        Equip(player,catalog.CreateSkill(12200,0,10097,5,20,1,null));
        cp=State(player,enemy);
        cp.Grid=null;
        runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        for(int counter=1;counter<=5 && !cp.Units.Exists(u=>u.VoodooDoll!=null);counter++)
            runner.Step(0,counter);
        Check(cp.Units.Exists(u=>u.VoodooDoll!=null),"Official substitute fixture did not spawn.");
        Check(filtered.Components.HitSummon.TriggeredCount==0,"FilterSubstitute consumed a creation trigger.");
        var doll=cp.Units.Find(u=>u.VoodooDoll!=null);doll.Hp=doll.Turn.Hp=0;
        Check(!runner.Step(0,cp.Counter+1).Event.SkillCasts.Exists(c=>c.SkillEntityId==11020)
            && filteredRemoval.Components.HitSummon.TriggeredCount==0,
            "FilterSubstitute consumed or released a removal trigger.");

        Removal(catalog,items,formula,false);
        Removal(catalog,items,formula,true);
        Revival(catalog,items,formula);
        SummonTags(catalog,items,formula,12140,10095,3196,false);
        SummonTags(catalog,items,formula,10710,10019,3181,true);
        TagBoundaries(catalog,items,formula);
    }
    static void SummonTags(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, int skillId, int itemId, int monsterId, bool matchesBeast)
    {
        var player=Unit(1,1,5,5,1000);var enemy=Unit(2,2,6,5);
        foreach(int id in new[]{25068,25112,25281}) player.Statuses.Add(Status(catalog,id,1,id));
        Equip(player,catalog.CreateSkill(skillId,0,itemId,5,20,1,null));
        var cp=State(player,enemy);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        for(int i=1;i<=5 && !cp.Units.Exists(u=>u.MasterId==1);i++) runner.Step(0,i);
        var summon=cp.Units.Find(u=>u.MasterId==1);
        Check(summon!=null && summon.MonsterId==monsterId,"Official tagged summon was not created.");
        int[] statuses={25068,25112,25281}, children={24071,24072,24206};
        bool[] expected={true,matchesBeast,!matchesBeast};
        for(int i=0;i<statuses.Length;i++)
        {
            var trigger=player.Statuses.Find(s=>s.ClassId==statuses[i]).Components.HitSummon;
            Check(trigger.DefinitionResolved && !trigger.HasUnsupportedFilter
                && trigger.HasSummonEntityTagFilter,"Original summon tag query was not resolved.");
            Check(trigger.TriggeredCount==(expected[i]?1:0)
                && summon.Statuses.Exists(s=>s.ClassId==children[i])==expected[i],
                "Original summon entity tag filtering failed for "+statuses[i]+" / "+monsterId);
        }
        var saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var restored=saved.Units.Find(u=>u.Id==1).Statuses.Find(s=>s.ClassId==25281).Components.HitSummon;
        Check(restored.HasSummonEntityTagFilter && restored.SummonEntityTagQuery.Count==1
            && restored.SummonEntityTagQuery[0].Tags[0]==4295065600L
            && restored.TriggeredCount==(!matchesBeast?1:0),"Summon query or counter was lost in JSON.");
        var copy=restored.Copy();copy.SummonEntityTagQuery[0].Tags[0]=0;
        Check(restored.SummonEntityTagQuery[0].Tags[0]==4295065600L,"Summon query clone aliases its mask.");
        restored.DefinitionResolved=false;restored.HasUnsupportedFilter=true;
        restored.HasSummonEntityTagFilter=false;restored.SummonEntityTagQuery.Clear();
        FightBattleExecutor.Restore(saved,formula,new FightDamageSettlement(),catalog,items);
        restored=saved.Units.Find(u=>u.Id==1).Statuses.Find(s=>s.ClassId==25281).Components.HitSummon;
        Check(restored.DefinitionResolved && !restored.HasUnsupportedFilter
            && restored.SummonEntityTagQuery[0].Tags[0]==4295065600L
            && restored.TriggeredCount==(!matchesBeast?1:0),"Legacy restore reset the counter or omitted the tag query.");
    }
    static void TagBoundaries(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        // Lifecycle fixtures isolate filtering before probability/count consumption; the two
        // preceding cases exercise actual official skills through the normal summon factory.
        var player=Unit(1,1,5,5,1000);var enemy=Unit(2,2,6,5);
        var trigger=Status(catalog,25281,1,1);player.Statuses.Add(trigger);
        var summon=Unit(3,1,5,7);summon.MasterId=summon.Turn.MasterId=1;summon.MonsterId=3181;
        var cp=State(player,enemy,summon);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var invoke=typeof(FightBattleExecutor).GetMethod("ExecuteMissingHitSummon",
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Action fire=()=>invoke.Invoke(runner,new object[]{summon,true});
        fire();Check(trigger.Components.HitSummon.TriggeredCount==0,"Excluded tag consumed MaxCount.");
        summon.MonsterId=3201;summon.MonsterTemplateId=3201;
        Check(!catalog.TryGetEntityGameplayTags(3201,out var absent),"Missing-tag fixture gained tags.");
        fire();Check(trigger.Components.HitSummon.TriggeredCount==0,"Without query accepted missing tags.");
        summon.MonsterId=3192;summon.MonsterTemplateId=0;
        fire();Check(trigger.Components.HitSummon.TriggeredCount==0,"Monster template lookup used the monster row id as tags.");
        summon.MonsterId=3196;summon.MonsterTemplateId=3196;
        trigger.Components.HitSummon.Rate=0;
        fire();Check(trigger.Components.HitSummon.TriggeredCount==0,"Zero probability consumed MaxCount.");
        trigger.Components.HitSummon.Rate=1;
        fire();Check(trigger.Components.HitSummon.TriggeredCount==1
            && summon.Statuses.Exists(s=>s.ClassId==24206),"First matching summon did not trigger.");
        fire();Check(trigger.Components.HitSummon.TriggeredCount==1,"MaxCount did not limit repeat creation.");
        trigger.Components.HitSummon.TriggeredCount=0;trigger.Components.HitSummon.HasUnsupportedFilter=true;
        fire();Check(trigger.Components.HitSummon.TriggeredCount==0,"Unknown filter was enabled.");
    }
    static void Removal(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, bool expiry)
    {
        var player=Unit(1,1,1,1,expiry ? 1000 : 1);
        var nearby=Unit(2,2,10,11,expiry ? 1 : 1000);
        var summon=Unit(3,1,10,10);summon.MasterId=summon.Turn.MasterId=1;summon.MonsterId=3190;
        var far=Unit(4,2,1,2);var friendly=Unit(5,1,11,10);
        var trigger=Status(catalog,25015,1,1);player.Statuses.Add(trigger);
        if(!expiry)
        {
            var attack=new FightBattleSkillDefinition {EntityId=90010,Slot=0,TargetType=1};
            attack.Range.Add(new FightGridPoint(0,-1));
            attack.Hits.Add(new FightBattleHitDefinition {EntityId=90011,DamageType=8,
                HasCustomDamage=true,CustomDamage=-2000000,Delay=0.4f});
            Equip(nearby,attack);
        }
        var cp=State(player,nearby,summon,far,friendly);
        if(expiry) summon.Statuses.Add(catalog.CreateStatus(12202,3,1,2,0,0,5,20,1));
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var step=runner.Step(0,1).Event;
        for(int counter=2;expiry && counter<=10 && !step.SkillCasts.Exists(c=>c.SkillEntityId==11020);counter++)
            step=runner.Step(0,counter).Event;
        var casts=step.SkillCasts.FindAll(c=>c.SkillEntityId==11020);
        Check(casts.Count==1,"Removal did not release exactly one official 11020.");
        Check(casts[0].ActorId==1 && casts[0].Presentation.TargetX==10
            && casts[0].Presentation.TargetY==10,"Removal lost summoner identity or removed summon position.");
        Check(Math.Abs(casts[0].Delay-(expiry ? 0f : 0.4f))<0.00001f,
            "Removal cast lost its parent death time.");
        Check(step.DamageEvents.Exists(d=>d.SkillEntityId==11020 && d.SourceId==1 && d.TargetId==2),
            "Removal did not hit the enemy near the summon.");
        Check(!step.DamageEvents.Exists(d=>d.SkillEntityId==11020 && (d.TargetId==4 || d.TargetId==5)),
            "Removal hit an enemy near the caster or a friendly unit.");
        Check(summon.HitSummonRemoveProcessed && trigger.Components.HitSummon.TriggeredCount==1,
            "Removal receipt/counter was not retained.");
        cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        Check(cp.Units.Find(u=>u.Id==3).HitSummonRemoveProcessed,"Removal receipt was lost in JSON.");
        runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        Check(!runner.Step(0,cp.Counter+1).Event.SkillCasts.Exists(c=>c.SkillEntityId==11020),
            "Restoring a removed summon released the removal skill again.");

        // Existing saves have no receipt field. Restore must not turn old corpses into events.
        cp.Units.Find(u=>u.Id==3).HitSummonRemoveProcessed=false;
        runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        Check(!runner.Step(0,cp.Counter+1).Event.SkillCasts.Exists(c=>c.SkillEntityId==11020),
            "A legacy checkpoint replayed an old removal.");
    }
    static void Revival(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var player=Unit(1,1,1,1);var enemy=Unit(2,2,10,9,1000);
        var summon=Unit(3,1,10,10);summon.MasterId=summon.Turn.MasterId=1;summon.MonsterId=3190;
        var trigger=Status(catalog,25015,1,1);player.Statuses.Add(trigger);
        // Model a role-death response with the existing official back-to-life status.
        var revive=new FightBattleStatusState {ClassId=90001,InstanceId=2,OwnerId=1,CreatorId=1,
            HasRoleDieTrigger=true,RoleDieRulesSupported=true,RoleDieTryAtRemove=true,
            RoleDieMaxCount=1,RoleDieMeetCount=1};
        const int revivalId=24233;
        Check(catalog.HasBackToLifeComponent(revivalId),"Fixture requires official back-to-life status 24233.");
        revive.RoleDieStatuses.Add(new FightBattleStatusStateTrigger {EntityId=revivalId,ApplyTarget=5,BasePercent=1f});
        player.Statuses.Add(revive);
        var attack=new FightBattleSkillDefinition {EntityId=90010,Slot=0,TargetType=1};
        attack.Range.Add(new FightGridPoint(0,1));
        attack.Hits.Add(new FightBattleHitDefinition {EntityId=90011,DamageType=8,
            HasCustomDamage=true,CustomDamage=-2000000,Delay=0.4f});
        Equip(enemy,attack);
        var cp=State(player,enemy,summon);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var step=runner.Step(0,1).Event;
        Check(summon.Hp>0 && !summon.Ended,"Role-death response failed to revive the summon.");
        Check(!step.SkillCasts.Exists(c=>c.SkillEntityId==11020) && !summon.HitSummonRemoveProcessed,
            "An immediately revived summon was treated as removed.");
    }
}
