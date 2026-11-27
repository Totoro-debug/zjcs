using System;
using System.Linq;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleAsuraHandHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static FightBattleSkillDefinition Skill(FightOfficialCombatCatalog catalog, int entity, int item, int rank, int level)
    {
        return catalog.CreateSkill(entity, 0, item, rank, level, 5,
            catalog.CalculateSkillProps(entity, rank, level, 5)
                .Select(p => new FightPropertyValue { Id=p.Key, Value=p.Value }).ToList());
    }
    static FightBattleStepEvent Next(FightBattleExecutor runner, int entity)
    {
        for (int i=0; i<60 && !runner.State.Finished; i++)
        {
            var step=runner.Step(runner.State.Sequence, runner.State.Counter+1).Event;
            if (step.SkillCasts.Any(c => c.SkillEntityId==entity)) return step;
        }
        throw new Exception("Asura missing cast: "+entity);
    }
    static void Equip(FightBattleUnitState actor, FightBattleSkillDefinition skill, FightOfficialCombatCatalog catalog)
    {
        actor.Skills.Clear(); actor.SkillState.Slots.Clear();
        actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
    }
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        int cases=0;
        foreach(int rank in new[]{3,6,7})
        foreach(int level in new[]{1,2,20})
        foreach(int seed in new[]{13,104729})
        foreach(int[] direction in new[]{new[]{0,1},new[]{0,-1},new[]{1,0},new[]{-1,0}})
        {
            var actor=Unit(1,1,6,6,formula); var enemy=Unit(2,2,6+direction[0],6+direction[1],formula);
            var checkpoint=new FightBattleCheckpoint {StageId=1,RandomSeed=seed,MaxRound=30,
                Grid=new FightBattleGrid {Width=14,Height=14,DefaultGroundType=2}};
            checkpoint.Units.AddRange(new[]{actor,enemy});
            var buff=Skill(catalog,11400,10063,rank,level); buff.LimitedTimes=1;
            Equip(actor,buff,catalog);
            var runner=FightBattleExecutor.Begin(checkpoint,formula,new FightDamageSettlement(),catalog,items);
            var buffStep=Next(runner,11400);
            var status=actor.Statuses.Single(s=>s.ClassId==24227);
            Check(status.CanDispersed && status.StatusType==1 && status.DurationRound==-1,
                "Asura original buff duration/dispel type mismatch.");
            Check(status.Components.HitDmgAddPer.IsTrueDamage && status.Components.HitDmgAddPer.DurationSkillCount==1,
                "Asura conversion/count not loaded.");
            Check(actor.Hp==actor.MaxHp && enemy.Hp==enemy.MaxHp, "Asura self buff dealt damage.");
            var attack=Skill(catalog,11440,10065,rank,level);
            var original=catalog.CreateSkill(11440,0,10065,rank,level,5,attack.Properties);
            // Basic attacks, buffs and cures preserve the next damaging-skill charge.
            foreach(var shape in new[]{new[]{1,0},new[]{2,2},new[]{2,3}})
            {
                var excluded=Skill(catalog,11440,10065,rank,level);
                excluded.ActionType=shape[0]; excluded.SkillType=shape[1];
                var modifier=new FightConditionalHitProperties(actor,excluded,items);
                var hit=new FightBattleHitDefinition {ActionType=4,DamageType=1};
                Check(!modifier.ResolveHitForCast(hit).TrueDamage,"Asura converted an excluded skill.");
                modifier.EndCast();
                Check(actor.Statuses.Contains(status) && status.Components.HitDmgAddPer.DurationSkillCount==1,
                    "Asura was consumed by a basic attack, buff or cure.");
            }
            long extra=status.StatusProperties.Single(p=>p.Id==formula.PropertyId("FixedStatusDmgAdd")).Value;
            long percent=status.StatusProperties.Single(p=>p.Id==formula.PropertyId("StatusDmgAddPer")).Value;
            Equip(actor,attack,catalog);
            var copy=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            runner=FightBattleExecutor.Restore(checkpoint,formula,new FightDamageSettlement(),catalog,items);
            var resumed=FightBattleExecutor.Restore(copy,formula,new FightDamageSettlement(),catalog,items);
            var step=Next(runner,11440); Next(resumed,11440);
            var hits=step.DamageEvents.Where(h=>h.SkillEntityId==11440).ToArray();
            Check(hits.Length==6 && hits.All(h=>h.TrueDamage && !h.IsCure && h.DamageType==1),
                "Asura must convert all six ordinary-damage segments.");
            var props=catalog.CalculateSkillProps(11440,rank,level,5);
            for(int i=0;i<6;i++)
            {
                int damageProp=i==5?201:200, fixedProp=i==5?191:190;
                double coefficient=props[damageProp]/10000.0;
                double amount=(1000.0*coefficient+props[fixedProp])*1000.0/1010.0;
                amount+=extra*coefficient;
                long expected=-(long)(amount*(1.0+percent/10000.0));
                Check(hits[i].Damage==expected && !hits[i].Critical && !hits[i].Block,
                    "Asura cultivated damage mismatch: "+hits[i].Damage+" / "+expected);
            }
            Check(!actor.Statuses.Contains(status),"Asura charge not consumed at the end of the entire skill.");
            Check(!original.Hits.Any(h=>h.TrueDamage),"Asura changed catalog definitions.");
            string saved=FightBattleCheckpointSerializer.Save(checkpoint);
            Check(saved==FightBattleCheckpointSerializer.Save(copy),"Asura save/restore diverged.");
            Check(runner.Step(checkpoint.Sequence,checkpoint.Counter).Replayed && saved==FightBattleCheckpointSerializer.Save(checkpoint),
                "Asura repeated a consumed charge on step replay.");
            var next=Next(runner,11440).DamageEvents.Where(h=>h.SkillEntityId==11440).ToArray();
            Check(next.Length==6 && next.All(h=>!h.TrueDamage),"Asura conversion leaked into the following skill.");
            Check(-next.Sum(h=>h.Damage)<-hits.Sum(h=>h.Damage),"Asura damage bonus leaked after consumption.");
            cases++;
        }
        foreach(bool summoned in new[]{false,true})
        foreach(bool shielded in new[]{false,true})
        {
            var source=Unit(1,1,6,6,formula);var receiver=Unit(2,2,6,7,formula);
            receiver.CharacterType=summoned?3:2;
            var prepare=Skill(catalog,11400,10063,3,2);
            var strike=Skill(catalog,11440,10065,3,2);strike.Slot=1;
            source.Skills.AddRange(new[]{prepare,strike});
            source.SkillState.Slots.Add(catalog.CreateSkillSlot(prepare,0));
            source.SkillState.Slots.Add(catalog.CreateSkillSlot(strike,1));
            FightBattleStatusState shield=null;
            if(shielded)
            {
                shield=catalog.CreateStatus(24174,2,2,2,24174,0,3,2,5,null,1000000000);
                receiver.Statuses.Add(shield);
            }
            var cp=new FightBattleCheckpoint {StageId=1,RandomSeed=13,MaxRound=10,
                Grid=new FightBattleGrid {Width=14,Height=14,DefaultGroundType=2}};
            cp.Units.AddRange(new[]{source,receiver});
            var execution=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var actual=Next(execution,11440);
            Check(actual.SkillCasts.Any(c=>c.SkillEntityId==11400)
                && actual.DamageEvents.Count(h=>h.SkillEntityId==11440 && h.TrueDamage)==6,
                "The complete Asura/attack step did not convert every hit against a shield or summon.");
            var skillProps=catalog.CalculateSkillProps(11440,3,2,5);
            var bonus=catalog.CalculateSkillProps(24227,3,2,5);
            long expected=0;
            for(int segment=0;segment<6;segment++)
            {
                double coefficient=skillProps[segment==5?201:200]/10000.0;
                double common=(1000.0*coefficient+skillProps[segment==5?191:190])*1000.0/1010.0;
                common+=bonus[formula.PropertyId("FixedStatusDmgAdd")]*coefficient;
                long baseDamage=(long)(common*(1.0+bonus[formula.PropertyId("StatusDmgAddPer")]/10000.0));
                expected+=baseDamage*(summoned?2:1)*(shielded?2:1);
            }
            Check(shielded ? receiver.Hp==receiver.MaxHp && shield.ShieldRemainValue==1000000000-expected
                : receiver.Hp==receiver.MaxHp-expected,
                "Asura conversion did not reach the shared breaking shield/summon calculation.");
            Check(!source.Statuses.Any(status=>status.ClassId==24227),"Asura conversion did not consume its buff after shield/summon damage.");
        }
        var naturalActor=Unit(1,1,6,6,formula);
        var naturalEnemy=Unit(2,2,6,7,formula);
        var naturalBuff=Skill(catalog,11400,10063,3,2);
        var naturalAttack=Skill(catalog,11440,10065,3,2); naturalAttack.Slot=1;
        naturalActor.Skills.AddRange(new[]{naturalBuff,naturalAttack});
        naturalActor.SkillState.Slots.Add(catalog.CreateSkillSlot(naturalBuff,0));
        naturalActor.SkillState.Slots.Add(catalog.CreateSkillSlot(naturalAttack,1));
        var naturalCheckpoint=new FightBattleCheckpoint {StageId=1,MaxRound=10,RandomSeed=104729,
            Grid=new FightBattleGrid {Width=14,Height=14,DefaultGroundType=2}};
        naturalCheckpoint.Units.AddRange(new[]{naturalActor,naturalEnemy});
        var naturalRunner=FightBattleExecutor.Begin(naturalCheckpoint,formula,new FightDamageSettlement(),catalog,items);
        int naturalBuffs=0, naturalConverted=0, naturalPlain=0;
        for(int i=0;i<100 && !naturalCheckpoint.Finished;i++)
        {
            var naturalStep=naturalRunner.Step(naturalCheckpoint.Sequence,naturalCheckpoint.Counter+1).Event;
            naturalBuffs+=naturalStep.SkillCasts.Count(c=>c.SkillEntityId==11400);
            int convertedSegments=naturalStep.DamageEvents.Count(h=>h.SkillEntityId==11440 && h.TrueDamage);
            naturalConverted+=convertedSegments;
            if(convertedSegments>0) Check(!naturalActor.Statuses.Any(status=>status.ClassId==24227),
                "Natural slot rotation retained a consumed Asura buff.");
            naturalPlain+=naturalStep.DamageEvents.Count(h=>h.SkillEntityId==11440 && !h.TrueDamage);
        }
        Check(naturalBuffs>0 && naturalConverted>=6,
            "Natural Asura/Eclipse slot rotation did not consume its prepared damage: "
                +naturalBuffs+" / "+naturalConverted+" / "+naturalPlain);
        return "Asura Hand passed: "+cases+" cultivation/direction/seed cases, six-hit conversion and bonus, exclusions, consumption, replay and save restoration.";
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
