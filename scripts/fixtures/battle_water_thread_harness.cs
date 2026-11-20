using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleWaterThreadHarness
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Water thread: " + message); }

    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id=id, Side=side, CharacterType=1, Profession=3,
            Level=50, SubRank=5, X=x, Y=y, Hp=1000000, MaxHp=1000000, LastMaxHp=1000000,
            MoveDist=0, MoveMask=5 };
        unit.Turn.Id=id; unit.Turn.Hp=unit.Hp; unit.Turn.Speed=id==1?1000:1;
        foreach (var value in new Dictionary<int,long> { {1,1000},{2,10},{3,unit.MaxHp},{4,unit.Turn.Speed} })
            unit.Properties.Add(new FightPropertyValue { Id=value.Key, Value=value.Value });
        foreach (string key in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
            unit.Properties.Add(new FightPropertyValue { Id=formula.PropertyId(key), Value=10000 });
        unit.Properties.Add(new FightPropertyValue { Id=formula.PropertyId("CritAvoidPercent"), Value=10000 });
        return unit;
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases=0, cold=0, resisted=0;
        foreach (var direction in new[] { new[]{0,1},new[]{0,-1},new[]{-1,0},new[]{1,0} })
        foreach (int distance in new[] { 2,3,4,5,6 })
        foreach (bool blocked in new[] { false,true })
        foreach (int seed in new[] { 17,43,73,1012348 })
        foreach (bool paired in new[] { false,true })
        {
            if (!paired && distance>5) continue;
            int dx=direction[0],dy=direction[1],tx=10+dx*distance,ty=10+dy*distance;
            var cp=new FightBattleCheckpoint { StageId=1, MaxRound=8, RandomSeed=seed,
                Grid=new FightBattleGrid { Width=24, Height=24, DefaultGroundType=2 } };
            var actor=Unit(1,1,10,10,formula);
            var near=Unit(2,2,10+dx,10+dy,formula);
            var target=Unit(3,2,tx,ty,formula);
            var ally=Unit(4,1,10-dy*3,10+dx*3,formula);
            if (blocked) foreach (var offset in new[] { new[]{-1,0},new[]{1,0},new[]{0,-1},new[]{0,1} })
                if (!paired || tx+offset[0]!=near.X || ty+offset[1]!=near.Y)
                    cp.Grid.BlockedPositions.Add(new FightGridPoint(tx+offset[0],ty+offset[1]));
            var skill=catalog.CreateSkill(10690,0,10017,7,30,5,
                catalog.CalculateSkillProps(10690,7,30,5)
                    .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList());
            skill.LimitedTimes=1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            cp.Units.AddRange(new[]{actor,target,ally}); if(paired) cp.Units.Add(near);
            var wave=new FightBattleWaveState(); wave.UnitIds.Add(3); if(paired) wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var saved=FightBattleCheckpointSerializer.Save(cp);
            var restored=FightBattleCheckpointSerializer.Load(saved);
            var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(0,1).Event; resumed.Step(0,1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),"Cold restore changed full settlement");
            var root=step.SkillCasts.Single(value=>value.SkillEntityId==10690 && value.ActorId==1);
            var children=step.SkillCasts.Where(value=>value.SkillEntityId==10970).ToArray();
            var hits=step.DamageEvents.Where(value=>value.SkillEntityId==10970).ToArray();
            Check(children.Length==(paired && distance<=5?2:1) && hits.Length==children.Length
                && hits.Select(value=>value.TargetId).Distinct().Count()==hits.Length
                && hits.All(value=>paired && value.TargetId==2 || distance<=5 && value.TargetId==3),"Five-cell area, friendly exclusion or child count differs");
            Check(actor.SkillState.Slots[0].ReleasedTimes==1 && children.All(value=>value.ActorId==1
                && value.Slot==-1),"Child lost caster ownership or consumed another equipped release");
            foreach (var hit in hits)
            {
                Check(hit.HitEntityId==10971 && hit.DamageEntityId==10972 && hit.ElementType==101
                    && hit.Damage<0 && !hit.TrueDamage && !hit.IsCure
                    && hit.BeforeHp-hit.AfterHp==-hit.Damage
                    && Math.Abs(hit.Delay-root.Delay-.4f)<.0001f,"Authored water damage or child hit delay differs");
                var unit=cp.Units.Single(value=>value.Id==hit.TargetId);
                var status=unit.Statuses.SingleOrDefault(value=>value.ClassId==24266);
                if(status!=null) {
                    cold++;
                    Check(status.StackCount==1 && status.DurationRound==2 && status.CreatorId==1
                        && status.Origin.IsSet && status.Origin.SkillItemId==10017
                        && status.Origin.SkillEntityId==10690 && status.RootSkillId==10970 && status.SourceSkillRank==7
                        && status.SourceSkillLevel==30 && status.SourceSubRank==5,"Cold lost its one stack, two turns or cultivation: stack="+status.StackCount+", turns="+status.DurationRound+", creator="+status.CreatorId+", item="+status.ApplyingSkillItemId+", rank="+status.SourceSkillRank+", level="+status.SourceSkillLevel+", sub="+status.SourceSubRank);
                } else resisted++;
            }
            var moves=step.Movements.Where(value=>value.UnitId==3 && value.MoveBehaviourId==10973).ToArray();
            if(distance>5 || blocked) Check(moves.Length==0 && target.X==tx && target.Y==ty,"Out-of-range or enclosed target moved");
            else {
                var hit=hits.Single(value=>value.TargetId==3);
                Check(moves.Length==1 && moves[0].Path.Count>1
                    && Math.Abs(moves[0].Delay-hit.Delay-.3f)<.0001f
                    && Math.Abs(moves[0].MoveDuration-.15f)<.0001f
                    && Math.Abs(target.X-10)+Math.Abs(target.Y-10)<distance,"Original delayed pull did not approach source");
            }
            string after=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0,1).Replayed && FightBattleCheckpointSerializer.Save(cp)==after,"Receipt replay repeated damage, cold or movement");
            cases++;
        }
        Check(cold>0 && resisted>0,"Original 50% cold outcomes were not both exercised");
        return "Water thread passed: "+cases+" four-direction area/child/cold/cultivation/blocked-pull/cold-restore/replay cases; cold="+cold+", resisted="+resisted;
    }
}
