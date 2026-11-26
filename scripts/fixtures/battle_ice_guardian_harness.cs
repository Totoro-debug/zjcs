using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleIceGuardianHarness
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException("Ice guardian: "+message);}
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(bool active in new[]{false,true})
        foreach(int rank in new[]{3,6,7})
        foreach(int level in new[]{1,2,20})
        {
            var cp=Battle(formula,104729,8);var actor=cp.Units[0];var enemy=cp.Units[1];
            actor.Hp=actor.MaxHp/2;actor.Turn.Hp=actor.Hp;enemy.X=11;
            actor.Statuses.Add(catalog.CreateStatus(25114,1,1,1,applyingSkillItemId:10605,
                sourceSkillRank:rank,sourceSkillLevel:level,sourceSubRank:8));
            if(active)Equip(catalog,actor,12070,10098,3,2,8);
            Equip(catalog,enemy,10640,10012,3,2,8);
            var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var first=runner.Step(cp.Sequence,cp.Counter+1).Event;
            Check(first.ActorId==1,"the fixture did not reach its owner's first round");
            var protection=first.SkillCasts.Where(cast=>cast.SkillEntityId==12160).ToArray();
            Check(first.DamageEvents.All(hit=>hit.SkillEntityId!=12160 || hit.Damage==0 && !hit.IsCure),
                "the protection marker unexpectedly damaged or healed its owner");
            Check(protection.Length==(active?0:1),
                "protection did not check actual active casts at the owner's round end: "+active+"/"+protection.Length);
            if(!active)
            {
                var frozen=actor.Statuses.Single(status=>status.ClassId==24390);

                Check(frozen.ApplyingSkillItemId==10605 && frozen.SourceSkillRank==rank && frozen.SourceSkillLevel==level,
                    "protection lost its cultivated source");
                var saved=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
                var restored=FightBattleExecutor.Restore(saved,formula,new FightDamageSettlement(),catalog,items);
                var enemyProps=catalog.CalculateSkillProps(10640,3,2,8);
                long reduce=frozen.StatusProperties.Single(v=>v.Id==232).Value;
                long fixedReduce=frozen.StatusProperties.Single(v=>v.Id==236).Value;
                decimal baseHurt=(1000m*enemyProps[200]/10000m+enemyProps[190])*1000m/1010m;
                decimal fixedHurt=fixedReduce*enemyProps[200]/10000m;
                long expectedHurt=-(long)((baseHurt-fixedHurt)/(1m+reduce/10000m));
                int hurts=0;int heals=0;string trace="";
                var cureProps=catalog.CalculateSkillProps(12190,rank,level,8);
                long expectedCure=(long)((decimal)actor.MaxHp*cureProps[210]/10000m+cureProps[170]);
                for(int n=0;n<5 && !cp.Finished;n++)
                {
                    var receipt=runner.Step(cp.Sequence,cp.Counter+1);restored.Step(saved.Sequence,saved.Counter+1);
                    Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(saved),
                        "restoration changed protection, enemy damage, thaw or healing");
                    heals+=receipt.Event.DamageEvents.Count(hit=>hit.SourceId==1 && hit.SkillItemId==10605
                        && hit.SkillEntityId==12190 && hit.IsCure && hit.Damage>0);
                    trace+=receipt.Event.ActorId+"/"+receipt.Event.ActorRound+" casts="+string.Join(",",receipt.Event.SkillCasts.Select(c=>c.SkillEntityId))+" statuses="+string.Join(",",actor.Statuses.Select(st=>st.ClassId+":"+st.CurDurationRound+":"+st.StartRound+":"+st.RoundUpdateTiming))+" cures="+string.Join(",",receipt.Event.DamageEvents.Where(h=>h.IsCure).Select(h=>h.Damage))+"; ";
                    foreach(var hurt in receipt.Event.DamageEvents.Where(hit=>hit.SourceId==2 && hit.TargetId==1 && hit.Damage<0))
                    {
                        if(heals==0)Check(hurt.Damage==expectedHurt,
                            "protection lost its cultivated percentage/fixed damage reduction: "+hurt.Damage+"/"+expectedHurt);
                        hurts++;
                    }
                    foreach(var heal in receipt.Event.DamageEvents.Where(hit=>hit.SkillEntityId==12190 && hit.IsCure))
                        Check(heal.Damage==expectedCure && heal.SourceId==1 && heal.TargetId==1
                            && heal.SkillItemId==10605 && heal.HitEntityId==12191 && heal.DamageEntityId==12192,
                            "thaw lost its original cultivated maximum-HP/fixed healing or source: "+heal.Damage+"/"+expectedCure);
                    if(heals>0)break;
                }
                Check(hurts>0,"the protected interval never received an actual enemy hit");
                Check(heals==1,"the following owner round did not thaw and heal exactly once: "+trace);
            }
            cases++;
        }
        return "Ice guardian idle-round protection, active-round suppression, cultivated thaw healing and restore passed: "+cases;
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
