using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

public static class BattleUnitSpawnHarness
{
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        foreach (int skillId in new[] {10710,10770,11140,12200,12140,57780,12060,13300,13670})
            foreach (bool modified in new[] {false,true}) Natural(catalog,items,formula,skillId,modified);
        Clock();
    }
    private static void Natural(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula, int skillId, bool modified)
    {
        var cp = new FightBattleCheckpoint { StageId=1,RandomSeed=11,
            Grid=new FightBattleGrid {Width=16,Height=16,DefaultGroundType=0} };
        for(int id=1;id<=2;id++)
        {
            var u=new FightBattleUnitState {Id=id,Side=id,CharacterType=id==1?1:2,X=4+id,Y=5,
                Hp=1000000,MaxHp=1000000,Level=150,SubRank=1,MoveMask=5,Direction=3,HasFlipX=true};
            u.Turn.Id=id;u.Turn.Hp=u.Hp;u.Turn.Speed=100-id;
            foreach(var pair in new[]{new[]{1,10001},new[]{2,10003},new[]{3,1000000},new[]{4,100-id}})
                u.Properties.Add(new FightPropertyValue {Id=pair[0],Value=pair[1]});
            cp.Units.Add(u);
        }
        var skill=catalog.CreateSkill(skillId,0,0,5,20,1,null);
        if(modified)
            foreach(var hit in skill.Hits.Where(hit=>hit.IsSummon))
            {
                hit.Delay+=.125f;
                for(int i=0;i<hit.ScopeEntries.Count;i++) hit.ScopeEntries[i].Delay=.25f+i*.125f;
            }
        cp.Units[0].Skills.Add(skill);
        cp.Units[0].SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
        var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
        if(skillId==12060)
        {
            cp.Units[1].Side=1;
            var enemy=new FightBattleUnitState {Id=3,Side=2,CharacterType=2,X=14,Y=14,Hp=1000000,MaxHp=1000000};
            enemy.Turn.Id=3;enemy.Turn.Hp=enemy.Hp;enemy.Turn.Speed=1;cp.Units.Add(enemy);wave.UnitIds.Add(3);
        }
        var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var step=executor.Step(0,1);
        Check(step.Event != null,"No step event for " + skillId);
        var ids=new HashSet<int>();
        foreach(var cast in step.Event.SkillCasts)
            foreach(var hit in cast.HitSelections)
                foreach(var spawn in hit.SpawnedUnits)
                {
                    var state=cp.Units.Single(unit=>unit.Id==spawn.UnitId);
                    Check(ids.Add(state.Id) && state.X==spawn.X && state.Y==spawn.Y,"Spawn receipt lost identity/initial position");
                    Check(spawn.HasInitialHealth && spawn.InitialHp==state.Hp && spawn.InitialMaxHp==state.MaxHp,
                        "Spawn did not retain its initial health before later hits");
                    var definition=FightBattleHitCollector.CollectAll(skill).First(value=>value.EntityId==hit.HitEntityId);
                    Check(Math.Abs(spawn.Delay-hit.EffectDelays[spawn.EffectCenterIndex]
                        -definition.ScopeEntries[spawn.ScopeIndex].Delay)<.00001f,"Spawn did not retain its original scope and flight time");
                }
        Check((ids.Count>0)
            && ids.SetEquals(step.Event.SpawnedUnitIds),"Failed placements or missing summons entered the appearance receipts: "
            +skillId+", recorded="+ids.Count+", spawned="+step.Event.SpawnedUnitIds.Count+", targets="
            +string.Join(",",step.Event.SkillCasts.SelectMany(c=>c.TargetIds))+", units="
            +string.Join(",",cp.Units.Select(u=>u.Id+":"+u.Hp+":"+u.X+":"+u.Y)));
        var saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        var restored=saved.Receipts.Last().Event.SkillCasts.SelectMany(c=>c.HitSelections).SelectMany(h=>h.SpawnedUnits).ToArray();
        Check(restored.Length==ids.Count && restored.All(value=>ids.Contains(value.UnitId) && value.HasInitialHealth
            && value.InitialHp>0 && value.InitialMaxHp>0),"Checkpoint lost summon appearance receipts");
        Console.WriteLine("Summon receipt "+skillId+" modified="+modified+" count="+ids.Count);
    }
    private static void Clock()
    {
        var first=new FightBattleHitSelectionSnapshot.SpawnedUnit {UnitId=3,X=5,Y=6,Delay=.5f,
            HasInitialHealth=true,InitialHp=90,InitialMaxHp=100};
        var cast=new FightBattleSkillCastEvent {Delay=.25f};
        cast.HitSelections.Add(new FightBattleHitSelectionSnapshot {SpawnedUnits=new List<FightBattleHitSelectionSnapshot.SpawnedUnit> {
            first,new FightBattleHitSelectionSnapshot.SpawnedUnit {UnitId=4,Delay=.25f},
            new FightBattleHitSelectionSnapshot.SpawnedUnit {UnitId=5,Delay=.5f}}});
        var step=new FightBattleStepEvent {SkillCasts=new List<FightBattleSkillCastEvent> {cast}};
        var order=new List<int>();var visible=new HashSet<int>();
        var clock=new OfficialBattleUnitSpawnPlayback(value=>{order.Add(value.UnitId);visible.Add(value.UnitId);
            Check(value.UnitId!=3 || value.X==5 && value.HasInitialHealth && value.InitialHp==90 && value.InitialMaxHp==100,
                "Queued position or initial health was mutated");},id=>visible.Contains(id));
        clock.Enqueue(step); first.X=999;first.InitialHp=999;cast.Delay=999;
        clock.AdvanceTimers(.5f);clock.DispatchDue();
        Check(order.SequenceEqual(new[]{4}) && clock.IsPending(3),"Out-of-order summon appeared too soon");
        clock.Enqueue(step);
        clock.AdvanceTimers(.25f);clock.DispatchDue();clock.Enqueue(step);clock.DispatchDue();
        Check(order.SequenceEqual(new[]{4,3,5}) && float.IsPositiveInfinity(clock.NextDelay),"Duplicate enqueue or equal-time ordering differs");
        var restored=new OfficialBattleUnitSpawnPlayback(_=>{throw new Exception("Restored unit replayed generation");},_=>true);
        restored.Enqueue(step);restored.AdvanceTimers(2000);restored.DispatchDue();
        clock.Clear();Check(!clock.IsPending(3),"Dispose retained pending summon");
    }
    private static void Check(bool value,string error) {if(!value) throw new InvalidOperationException(error);}
}
