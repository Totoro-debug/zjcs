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
        foreach (bool duplicate in new[] { false, true })
        {
            var source = Unit(1, 5, 5); source.Direction = direction; source.FlipX = flip;
            var target = Unit(2, 8, 8);
            var skill = catalog.CreateSkill(id, 0, 0, 5, 20, 1, null);
            var parent = skill.HitGraphEntities.Single(hit => hit.HitComponentType == "FightHitCustomTargetComponent");
            // Exercise the original custom parent directly, independent of any earlier summon in the skill.
            skill.HitGraphRoots.Clear(); skill.HitGraphRoots.Add(new FightBattleHitRoot { EntityId = parent.EntityId, Delay = .2f });
            if (duplicate) parent.CustomTargetHitLinks.Add(new FightBattleHitLink {
                EntityId=parent.CustomTargetHitLinks[0].EntityId,
                Delay=parent.CustomTargetHitLinks[0].Delay, Anchor=parent.CustomTargetHitLinks[0].Anchor });
            var cast = FightBattleSkillCastPresentation.Capture(source, skill, new List<FightBattleUnitState> { target });
            var center = new FightGridPoint(target.X, target.Y);
            var transformed = FightBattleTargeting.TransformScope(parent.Scope, parent.ScopeTransform, source, cast);
            var cells = transformed.Select(offset => center + offset).ToArray();
            var first = cells[0]; var second = cells[1];
            var large = Unit(3, first.X, first.Y);
            large.Side = skill.TargetType == 2 ? source.Side : target.Side;
            large.BodyOffsets.Clear(); large.BodyOffsets.Add(new FightGridPoint(0, 0));
            large.BodyOffsets.Add(new FightGridPoint(second.X-first.X, second.Y-first.Y));
            large.BodyOffsets.Add(new FightGridPoint(0, 0)); // duplicate declaration is one body per cell
            var outside = Unit(4, 15, 15);
            var dead = Unit(5, first.X, first.Y); dead.Hp = 0;
            var roster = new List<FightBattleUnitState> { source, target, large, outside, dead };
            var traversal = new FightHitChainExecution(FightBattleHitCollector.CollectAll(skill, true), cast, true);
            var ctx = new FightHitTargetSelectionContext { Source=source, TargetingSource=source, Skill=skill,
                Units=roster, Targets=new List<FightBattleUnitState>{target}, Primary=target,
                Grid=new FightBattleGrid { Width=16, Height=16 }, Random=new FightRandomStream(11) };
            int parents=0, children=0;
            while (traversal.TryTake(out var step))
            {
                var flight = FightBattleHitTiming.ResolveOverride(catalog, skill, source, step.Cast, step.Hit);
                var selection=traversal.Select(step,ctx,flight);
                if(step.Hit.IsCustomTarget) { parents++; Check(selection.Targets.Count==0,"Parent must not settle damage"); continue; }
                children++;
                Check(step.CustomTarget.Id==3,"Custom scan included a target outside scope or dead body");
                Check(selection.EffectCenters.Count==1 && cells.Take(2).Contains(selection.EffectCenters[0]),"Child lost actual body cell");
                Check(step.Cast.FacingIndex==direction && step.Cast.FlipX==flip,"Child lost original orientation");
                Check(Math.Abs(step.Hit.Delay-(.2f+parent.CustomTargetHitLinks[0].Delay))<.0001f,"Child lost original edge delay");
            }
            Check(parents==1 && children==(duplicate?4:2),"Custom parent failed multi-cell expansion: "+id+"/"+direction+"/"+flip+" = "+parents+"/"+children);
            ctx.Units=new List<FightBattleUnitState>();
            var empty=new FightHitChainExecution(FightBattleHitCollector.CollectAll(skill,true),cast,true);
            Check(empty.TryTake(out var only),"Missing empty-scope parent receipt");
            var parentOnly=empty.Select(only,ctx,null);
            Check(parentOnly.EffectCenters.Count==1 && !empty.TryTake(out var absent),
                "Custom parent fabricated children without a legal occupied cell");

        }
        foreach (int id in new[] { 12060, 13300, 13670 })
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
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var result=executor.Step(0,1);
            var cast=result.Event.SkillCasts.Single(value=>value.SkillEntityId==id);
            var parent=source.Skills[0].HitGraphEntities.Single(value=>value.HitComponentType=="FightHitCustomTargetComponent");
            var parentReceipt=cast.HitSelections.Single(value=>value.HitEntityId==parent.EntityId);
            Check(parentReceipt.Targets.Count==0,"Actual executor settled custom parent damage");
            var children=cast.HitSelections.Where(value=>parent.CustomTargetHits.Contains(value.HitEntityId)).ToArray();
            Check(id==12060 || children.Length>0,"Actual custom executor did not create child receipts: "+id);
            foreach(var child in children) Check(child.EffectCenters.Count==1,"Actual child has ambiguous location");
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(!saved.Contains("PreviousCustomHit") && !saved.Contains("CustomLinkDelay"),"Custom traversal leaked into checkpoint");
            var restored=FightBattleCheckpointSerializer.Load(saved);
            var resume=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            executor.Step(cp.Sequence,cp.Counter+1); resume.Step(restored.Sequence,restored.Counter+1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),"Custom checkpoint continuation diverged");
            Console.WriteLine("Actual custom skill "+id+": "+children.Length+" child receipts; deterministic continuation.");
        }
        Console.WriteLine("Custom target scope checks passed: 48 original parent/orientation/repeated-edge cases.");
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
