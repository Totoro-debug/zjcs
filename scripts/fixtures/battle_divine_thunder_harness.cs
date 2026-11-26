using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleDivineThunderHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Divine thunder: " + message); }

    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula,
        FightOfficialSkillItemCatalog items)
    {
        int cases = 0, empty = 0, forced = 0;
        foreach (int body in new[] { 1, 4 })
        foreach (int seed in Enumerable.Range(1, 64).Select(value => value * 104729))
        {
            var cp = Battle(formula, seed, 8); var actor = cp.Units[0]; var target = cp.Units[1];
            if (body == 4) target.BodyOffsets.AddRange(new[] { new FightGridPoint(1, 0),
                new FightGridPoint(0, 1), new FightGridPoint(1, 1) });
            var skill = catalog.CreateSkill(10920, 0, 10037, 6, 2, 8,
                catalog.CalculateSkillProps(10920, 6, 2, 8).Select(p => new FightPropertyValue { Id=p.Key, Value=p.Value }).ToList());
            var hits = FightBattleHitCollector.CollectAll(skill).Where(hit => !hit.IsViewOnly).ToArray();
            Check(hits.Length == 16 && hits.All(hit => hit.RandomTargetGroupSelectionCount == 16
                && hit.RandomTargetGroupMinimumHits == 3), "the parent did not retain sixteen strikes and its shared minimum");
            var cast = FightBattleSkillCastPresentation.Capture(actor, skill, new List<FightBattleUnitState> { target });
            var selectors = new FightHitTargetSelectors(); var rng = new FightRandomStream(seed);
            var ordinary = new FightHitTargetSelectors(); var ordinaryRng = new FightRandomStream(seed);
            int landed = 0; bool corrected = false;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i]; bool mustHit = 16 - i <= 3 - landed;
                var context = new FightHitTargetSelectionContext { Source=actor, Units=cp.Units, Skill=skill,
                    Hit=hit, Primary=target, Targets=new List<FightBattleUnitState> { target }, Cast=cast, Grid=cp.Grid, Random=rng };
                var selected = selectors.For(hit).Select(context);
                Check(selected != null && selected.SelectedGridPositions.Count == 1,
                    "an authored strike selected multiple centres or vanished");
                if (!mustHit && !corrected)
                {
                    hit.RandomTargetGroupMinimumHits = 0; context.Random=ordinaryRng;
                    var baseline = ordinary.For(hit).Select(context);
                    Check(baseline.SelectedGridPositions[0].Equals(selected.SelectedGridPositions[0]),
                        "the minimum changed an ordinary draw before its tail was needed");
                    hit.RandomTargetGroupMinimumHits = 3;
                }
                else ordinaryRng.Seed = rng.Seed;
                if (mustHit) { corrected = true; forced++; Check(selected.Occurrences.Count > 0, "a necessary tail strike missed its eligible body cell"); }
                if (selected.Occurrences.Count > 0) landed++; else empty++;
                Check(selected.SelectedGridPositions.All(cp.Grid.Contains), "a lightning centre escaped the grid");
            }
            Check(landed >= 3 && landed <= 16, "the full cast did not meet its minimum"); cases++;
        }
        Check(empty > 0 && forced > 0, "fixed seeds did not cover ordinary empty cells and tail correction");
        foreach (int mode in new[] { 0, 1, 2, 3 })
        {
            var cp=Battle(formula,1234567,8); var actor=cp.Units[0]; var target=cp.Units[1];
            actor.X=0;actor.Y=0;target.X=1;target.Y=0;
            if(mode==1) target.Hp=0;
            if(mode==2) target.Targetable=false;
            if(mode==3) target.Statuses.Add(new FightBattleStatusState { ClassId=24008, ActionType=50, StackCount=1 });
            var skill=catalog.CreateSkill(10920,0,10037,6,2,8,null);
            var cast=FightBattleSkillCastPresentation.Capture(actor,skill,new List<FightBattleUnitState>{target});
            var selector=new FightHitTargetSelectors();var rng=new FightRandomStream(1234567);int landed=0;
            foreach(var hit in FightBattleHitCollector.CollectAll(skill).Where(hit=>!hit.IsViewOnly))
            {
                var selected=selector.For(hit).Select(new FightHitTargetSelectionContext { Source=actor, Units=cp.Units,
                    Skill=skill, Hit=hit, Cast=cast, Primary=target, Targets=new List<FightBattleUnitState>{target}, Grid=cp.Grid, Random=rng });
                Check(selected!=null && selected.SelectedGridPositions.Count==1 && selected.SelectedGridPositions.All(cp.Grid.Contains),
                    "an edge/empty roster lost an original strike or escaped the grid");
                if(selected.Occurrences.Count>0)landed++;
            }
            Check(mode==0 ? landed>=3 : landed==0,"tail correction hit a dead, hidden or untargetable entity");cases++;
        }
        foreach (int bodyMode in new[] { 0, 1, 2 })
        {
            var cp=Battle(formula,104729,8);var actor=cp.Units[0];
            var large=Unit(3,2,bodyMode==0?14:bodyMode==1?17:20,formula,8);
            large.BodyOffsets.AddRange(bodyMode==1 ? new[] {new FightGridPoint(0,0),new FightGridPoint(-1,0),new FightGridPoint(-2,0),new FightGridPoint(-3,0)} : new[] {new FightGridPoint(0,0),new FightGridPoint(1,0),new FightGridPoint(0,1),new FightGridPoint(1,1)});cp.Units.Add(large);cp.Waves[0].UnitIds.Add(3);
            Equip(catalog,actor,10920,10037,6,2,8);
            var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            FightBattleSkillCastEvent cast=null;string steps="";
            for(int n=0;n<12 && !cp.Finished && cast==null;n++) {
                var step=runner.Step(cp.Sequence,cp.Counter+1).Event;
                steps+=step.ActorId+":"+step.Kind+",";
                cast=step.SkillCasts.SingleOrDefault(value=>value.SkillEntityId==10920);
            }
            if(bodyMode==2) Check(cast==null,
                "the confirmed exclusive large-body branch fell through to a small target");
            else Check(cast!=null && cast.Presentation.TargetId==3,
                "the original large-body coverage lost an in-range non-centre body cell");cases++;
        }
        foreach (int rank in new[] { 3, 6, 7 })
        foreach (int level in new[] { 1, 2, 20 })
        foreach (int seed in new[] { 104729, 1234567, 9123456, 19123456 })
        {
            var cp = Battle(formula, seed, 8); var actor=cp.Units[0]; var target=cp.Units[1];
            Equip(catalog, actor, 10920, 10037, rank, level, 8);
            var runner=FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var restored=FightBattleExecutor.Restore(saved, formula, new FightDamageSettlement(), catalog, items);
            for (int turn=0; turn<12 && !cp.Finished; turn++)
            {
                var receipt=runner.Step(cp.Sequence,cp.Counter+1); restored.Step(saved.Sequence,saved.Counter+1);
                Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(saved),
                    "restoring changed random positions, per-target falloff or subsequent casts");
                foreach (var cast in receipt.Event.SkillCasts.Where(value=>value.SkillEntityId==10920))
                {
                    var selections=cast.HitSelections.Where(hit=>hit.HitEntityId>=10922 && hit.HitEntityId<=10925).ToArray();
                    Check(selections.Length==16 && selections.All(hit=>hit.EffectCenters.Count==1),
                        "actual battle lost an authored strike or multiplied its centre");
                    var damage=receipt.Event.DamageEvents.Where(hit=>cast.DamageEventIds.Contains(hit.DamageId)).ToArray();
                    Check(damage.Length>=3 && damage.All(hit=>hit.SourceId==1 && hit.TargetId==2
                        && hit.SkillItemId==10037 && hit.ElementType==103 && hit.DamageType==1
                        && hit.DamageEntityId==10926 && !hit.IsCure && !hit.TrueDamage),
                        "actual lightning lost its minimum or light damage identity");
                    for (int n=1;n<damage.Length;n++)
                        Check(damage[n].Damage==(long)(damage[0].Damage*Math.Pow((double).6f,n)),
                            "repeated hits lost their forty-percent per-target falloff: "+n+"/"+damage[n].Damage+"/"+damage[0].Damage);
                    cases++;
                }
            }
        }
        return "Divine thunder random tail minimum, sixteen centres, original light falloff and restored casts passed: "+cases;
    }
    static FightBattleCheckpoint Battle(FightDamageFormula formula, int seed, int subRank)
    {
        var cp = new FightBattleCheckpoint { StageId = 1, MaxRound = 4, RandomSeed = seed,
            Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
        cp.Units.Add(Unit(1, 1, 10, formula, subRank)); cp.Units.Add(Unit(2, 2, 12, formula, subRank));
        var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
        return cp;
    }
    static void Equip(FightOfficialCombatCatalog catalog, FightBattleUnitState actor,
        int entity, int item, int rank, int level, int subRank)
    {
        var skill = catalog.CreateSkill(entity, 0, item, rank, level, subRank,
            catalog.CalculateSkillProps(entity, rank, level, subRank)
                .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
    }
    static FightBattleUnitState Unit(int id, int side, int x, FightDamageFormula formula, int subRank)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 12,
            Level = 70, SubRank = subRank, X = x, Y = 10, Hp = 1000000000, MaxHp = 1000000000, LastMaxHp = 1000000000 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 900;
        foreach (var pair in new Dictionary<string, int> { { "Attack", 1000 }, { "Defence", 10 }, { "MaxHp", 1000000000 },
            { "Speed", unit.Turn.Speed }, { "BaseCritRatePercentValue", 10000 }, { "BaseCritAvoidPercentValue", 10000 },
            { "CritAvoidPercent", 10000 }, { "BaseBlockPercentValue", 10000 }, { "BaseBlockAvoidPercentValue", 10000 },
            { "BaseEffectRate", 10000 }, { "BaseEffectDodge", 10000 },
            { "BaseElementMaster", 10000 }, { "BaseElementResistance", 10000 }, { "BaseElementAdd", 10000 }, { "BaseElementReduce", 10000 } })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(pair.Key), Value = pair.Value });
        if (id == 1) unit.Properties.Add(new FightPropertyValue { Id = 413, Value = 0 });
        unit.BaseProperties = unit.Properties.Select(value => new FightPropertyValue { Id = value.Id, Value = value.Value }).ToList();
        return unit;
    }
}
