using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleCustomTargetsHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        foreach (int id in new[] { 12060, 13300, 13670 })
        foreach (int direction in new[] { 0, 1, 2, 3 })
        foreach (bool flip in new[] { false, true })
        foreach (int mode in new[] { 0, 1, 2 })
        {
            var source=Unit(1,5,5); source.Direction=direction; source.FlipX=flip;
            var target=Unit(2,7,5);
            var skill=catalog.CreateSkill(id,0,0,5,20,1,null);
            if(skill.TargetType==2) target.Side=source.Side;
            target.BodyOffsets.Add(new FightGridPoint(0,1));
            var parent=skill.HitGraphEntities.Single(hit=>hit.HitComponentType=="FightHitCustomTargetComponent");
            skill.HitGraphRoots.Clear();
            skill.HitGraphRoots.Add(new FightBattleHitRoot {EntityId=parent.EntityId,Delay=.2f});
            var cast=FightBattleSkillCastPresentation.Capture(source,skill,new List<FightBattleUnitState>{target});
            var origin=new FightGridPoint(source.X,source.Y);
            var cells=FightBattleTargeting.TransformScope(parent.Scope,parent.ScopeTransform,source,cast)
                .Select(offset=>origin+offset).ToArray();
            var legal=cells.Where(cell=>!FightBattleTargeting.OccupiesPosition(source,cell)
                && !FightBattleTargeting.OccupiesPosition(target,cell)).ToArray();
            var expected=legal.OrderBy(cell=>Math.Min(Math.Abs(cell.X-7)+Math.Abs(cell.Y-5),
                Math.Abs(cell.X-7)+Math.Abs(cell.Y-6)))
                .ThenBy(cell=>Math.Abs(cell.X-5)+Math.Abs(cell.Y-5)).First();
            var ctx=new FightHitTargetSelectionContext {Source=source,TargetingSource=source,Skill=skill,
                Units=new List<FightBattleUnitState>{source,target},Targets=new List<FightBattleUnitState>{target},
                Primary=target,Grid=new FightBattleGrid {Width=16,Height=16},Random=new FightRandomStream(11),
                CanPlaceSummon=(child,cell,ignored)=>mode==0 ? ignored==0 && legal.Contains(cell)
                    : mode==1 && ignored==source.Id && cell.Equals(origin)};
            var traversal=new FightHitChainExecution(FightBattleHitCollector.CollectAll(skill,true),cast,true);
            int parents=0,children=0;
            while(traversal.TryTake(out var step))
            {
                var selection=traversal.Select(step,ctx,null);
                if(step.Hit.IsCustomTarget) {parents++;Check(selection.Targets.Count==0,"Parent settled damage");continue;}
                children++;
                Check(step.CustomTarget.Id==target.Id,"Custom resolver reselected its AI target");
                Check(selection.EffectCenters.Single().Equals(mode==0?expected:origin),"Wrong ranked/fallback landing cell");
                Check(step.SummonOnSourceFallback==(mode==1),"Fallback bypass leaked to ordinary placement");
                Check(step.Cast.FacingIndex==direction && step.Cast.FlipX==flip,"Child lost orientation");
                Check(Math.Abs(step.Hit.Delay-(.2f+parent.CustomTargetHitLinks[0].Delay))<.0001f,"Child lost edge delay");
            }
            Check(parents==1 && children==(mode==2?0:1),"Expected exactly one summon or no legal fallback");
        }
        foreach (int id in new[] { 12060, 13300, 13670 })
        foreach(int mode in new[]{0,1,2,3})
        {
            var cp = new FightBattleCheckpoint { StageId=1, RandomSeed=11,
                Grid=new FightBattleGrid { Width=16,Height=16,DefaultGroundType=0 } };
            var source=Unit(1,5,5); source.Level=150; source.SubRank=1; source.MoveMask=5;
            source.Skills.Add(catalog.CreateSkill(id,0,0,5,20,1,null));
            source.SkillState.Slots.Add(new FightSkillSlotState { Slot=0,LimitedTimes=-1 });
            cp.Units.Add(source);
            var wave=new FightBattleWaveState();
            for(int n=2;n<=5;n++) { var u=Unit(n,5+(n-2)%2,6+(n-2)/2); cp.Units.Add(u); wave.UnitIds.Add(n); }
            if(id==12060) cp.Units[1].Side=1;
            cp.Waves.Add(wave);
            if(mode==3) {cp.Units[2].X=source.X;cp.Units[2].Y=source.Y;}
            if(mode>0)
                for(int x=0;x<16;x++) for(int y=0;y<16;y++)
                    if(mode==2 || x!=source.X || y!=source.Y)
                        cp.Grid.BlockedPositions.Add(new FightGridPoint(x,y));
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var result=executor.Step(0,1);
            var cast=result.Event.SkillCasts.Single(value=>value.SkillEntityId==id);
            var parent=source.Skills[0].HitGraphEntities.Single(value=>value.HitComponentType=="FightHitCustomTargetComponent");
            var parentReceipt=cast.HitSelections.Single(value=>value.HitEntityId==parent.EntityId);
            Check(parentReceipt.Targets.Count==0,"Actual executor settled custom parent damage");
            var children=cast.HitSelections.Where(value=>parent.CustomTargetHits.Contains(value.HitEntityId)).ToArray();
            Check(children.Length==(mode>=2?0:1),"Actual custom executor did not create child receipts: "+id);
            foreach(var child in children) Check(child.EffectCenters.Count==1,"Actual child has ambiguous location");
            var spawns=cast.HitSelections.SelectMany(h=>h.SpawnedUnits).ToArray();
            Check(spawns.Length==(mode>=2?0:1),"Actual spawn count differs from legal placements");
            if(mode==1) Check(spawns.Single().X==source.X && spawns.Single().Y==source.Y,"Fallback did not overlap caster");
            if(mode==0) Check(spawns.All(p=>cp.Units.All(u=>u.Id==p.UnitId || u.Hp<=0 || u.Ended
                || !FightBattleTargeting.OccupiesPosition(u,new FightGridPoint(p.X,p.Y)))),"Ordinary summon overlaps a role");
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(!saved.Contains("PreviousCustomHit") && !saved.Contains("CustomLinkDelay"),"Custom traversal leaked into checkpoint");
            var restored=FightBattleCheckpointSerializer.Load(saved);
            var resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            executor.Step(cp.Sequence,cp.Counter+1); resume.Step(restored.Sequence,restored.Counter+1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),"Custom checkpoint continuation diverged");
            Console.WriteLine("Actual custom skill "+id+": "+children.Length+" child receipts; deterministic continuation.");
        }
        Console.WriteLine("Custom target scope checks passed: 72 ranked/fallback/invalid cases across three original parents and all orientations.");
    }
    static FightBattleUnitState Unit(int id,int x,int y)
    {
        var u = new FightBattleUnitState { Id=id, Side=id==1?1:2, CharacterType=id==1?1:2,
            X=x,Y=y,Hp=100000,MaxHp=100000,HasFlipX=true,Direction=3,
            Turn=new FightTurnUnit { Id=id,Hp=100000,Speed=100-id } };
        foreach(var pair in new[]{new[]{1,10001},new[]{2,10003},new[]{3,100000},new[]{4,100-id}})
            u.Properties.Add(new FightPropertyValue { Id=pair[0],Value=pair[1] });
        return u;
    }
}
