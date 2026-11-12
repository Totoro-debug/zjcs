using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleBasicHemophagiaHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static FightBattleUnitState Unit(FightOfficialCombatCatalog catalog, int id, int side, int x)
    {
        var unit = new FightBattleUnitState { Id=id, Side=side, X=x, Y=5, Hp=500000000,
            MaxHp=1000000000, LastMaxHp=1000000000, Level=150, SubRank=1, CharacterType=1 };
        unit.Turn.Id=id; unit.Turn.Hp=unit.Hp; unit.Turn.Speed=id==1?1000:1;
        foreach (var property in new[] { new FightPropertyValue {Id=1,Value=10001},
            new FightPropertyValue {Id=2,Value=0}, new FightPropertyValue {Id=3,Value=unit.MaxHp},
            new FightPropertyValue {Id=4,Value=unit.Turn.Speed} }) unit.Properties.Add(property);
        foreach (string name in new[] {"BaseEffectRate","BaseEffectDodge","BaseCureAdd","BaseBeCureAdd"})
            unit.Properties.Add(new FightPropertyValue {Id=catalog.PropertyType(name),Value=10000});
        return unit;
    }
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        Check(catalog.HemophagiaSkillEntityId==10110,"Original Battle.HemophagiaEntityId was not loaded");
        int cases=0;
        foreach (string mode in new[] {"normal","fraction","above-one","zero","negative","truncate-zero",
            "overkill","shield","full","forbidden","to-shield","full-to-shield","cure-modifier","multiple-targets","hp-range-whitelist"})
        {
            var owner=Unit(catalog,1,1,5); var target=Unit(catalog,2,2,6);
            long scale=mode=="fraction"?3333:mode=="above-one"?25000:mode=="zero"?0:
                mode=="negative"?-10000:mode=="truncate-zero"?1:10000;
            owner.Properties.Add(new FightPropertyValue {Id=catalog.PropertyType("OnceHitHemophagiaPer"),Value=scale});
            if(mode=="full"||mode=="full-to-shield") owner.Hp=owner.MaxHp;
            if(mode=="hp-range-whitelist")
            {
                owner.Hp=299999999;
                owner.Statuses.Add(catalog.CreateStatus(25067,1,1,1,25067,14521,5,20,1));
                owner.Statuses.Add(catalog.CreateStatus(24315,1,1,2,25067,14521,5,20,1));
            }
            if(mode=="overkill"||mode=="truncate-zero") target.Hp=1;
            if(mode=="forbidden") owner.Statuses.Add(catalog.CreateStatus(24223,1,1,1,0,0,2,20,1));
            if(mode=="to-shield"||mode=="full-to-shield")
                owner.Statuses.Add(catalog.CreateStatus(25136,1,1,1,0,0,2,20,1));
            if(mode=="shield") target.Statuses.Add(catalog.CreateStatus(24435,2,2,1,0,0,2,20,1,
                customShieldValue:1000000000));
            if(mode=="cure-modifier") owner.Properties.Add(new FightPropertyValue {
                Id=catalog.PropertyType("CureAddPercent"),Value=5000});
            int entityId=mode=="multiple-targets"?10810:10630;
            var skill=catalog.CreateSkill(entityId,0,mode=="multiple-targets"?10029:10011,2,20,1,catalog.CalculateSkillProps(entityId,2,20,1)
                .Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList());
            foreach(var hit in skill.Hits)
                hit.HemophagiaProperty=catalog.PropertyType("OnceHitHemophagiaPer");
            foreach(var hit in skill.HitGraphEntities)
                hit.HemophagiaProperty=catalog.PropertyType("OnceHitHemophagiaPer");
            owner.Skills.Add(skill); owner.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            var checkpoint=new FightBattleCheckpoint {StageId=1001,RandomSeed=73,MaxRound=10,
                Grid=new FightBattleGrid {Width=20,Height=20,DefaultGroundType=0}};
            checkpoint.Units.Add(owner); checkpoint.Units.Add(target);
            if(mode=="multiple-targets") checkpoint.Units.Add(Unit(catalog,3,2,7));
            long beforeHp=owner.Hp;
            var executor=FightBattleExecutor.Begin(checkpoint,formula,new FightDamageSettlement(),catalog,items);
            if(mode=="hp-range-whitelist") Check(owner.Statuses.Any(s=>s.ClassId==24315),
                "Original low-HP status did not activate before lifesteal");
            var step=executor.Step(0,1).Event;
            var hits=step.DamageEvents.Where(d=>d.SourceId==owner.Id&&d.SkillEntityId==entityId&&!d.IsCure).ToArray();
            var sources=hits.Where(d=>decimal.Truncate(Math.Max(0L,d.BeforeHp-d.AfterHp)*scale/10000m)>0).ToArray();
            var cures=step.DamageEvents.Where(d=>d.IsCure).ToArray();
            var casts=step.SkillCasts.Where(c=>c.SkillEntityId==10110).ToArray();
            Check(hits.Length>0,"No original attack: "+mode);
            Check(cures.Length==sources.Length&&casts.Length==sources.Length,"One original cure per positive occurrence: "+mode);
            Check(step.DamageEvents.Select(d=>d.DamageId).Distinct().Count()==step.DamageEvents.Count,
                "Child cure reused its causing damage serial: "+mode);
            for(int i=0;i<cures.Length;i++)
            {
                var cure=cures[i]; var source=sources[i];
                long expected=(long)decimal.Truncate((source.BeforeHp-source.AfterHp)*scale/10000m);
                if(mode=="cure-modifier") expected=(long)(expected*1.5);
                Check(cure.SkillEntityId==10110&&cure.HitEntityId==10111&&cure.DamageEntityId==10028
                    &&cure.SourceId==1&&cure.TargetId==1&&cure.SkillItemId==skill.ItemId,
                    "Original cure identity/ownership differs: "+mode);
                Check(casts[i].DamageEventIds.Contains(cure.DamageId)
                    &&Math.Abs(cure.Delay-source.Delay)<1e-6f&&Math.Abs(casts[i].Delay-source.Delay)<1e-6f,
                    "Cure escaped its original cast or hit time: "+mode);
                Check(cure.HasDamagePosition&&cure.DamageX==owner.X&&cure.DamageY==owner.Y,
                    "Cure did not resolve at the caster's actual cell: "+mode);
                if(mode!="forbidden"&&mode!="to-shield"&&mode!="full-to-shield")
                    Check(cure.HasDamageValue&&cure.DamageValue==expected,
                        "Actual-HP basis, truncation, uncapped percentage or cure modifiers differ: "+mode+" "+cure.DamageValue+"/"+expected);
                if(mode=="full"||mode=="forbidden"||mode=="to-shield"||mode=="full-to-shield")
                    Check(cure.BeforeHp==cure.AfterHp&&cure.Damage==0,"Cure interception/overheal failed: "+mode);
            }
            Check(owner.Hp-beforeHp==cures.Sum(c=>c.AfterHp-c.BeforeHp),"Healing was applied twice: "+mode);
            Check(checkpoint.Statistics.Unit(1).HealDealt==cures.Sum(c=>c.AfterHp-c.BeforeHp),
                "Healing statistics included overheal or conversion: "+mode);
            if(mode=="to-shield"||mode=="full-to-shield")
                Check(owner.Statuses.Any(s=>s.ClassId==24435&&s.ShieldRemainValue>0),"Original cure-to-shield chain was bypassed: "+mode);
            if(mode=="overkill") Check(sources.Length==1&&sources[0].BeforeHp-sources[0].AfterHp==1
                &&Math.Abs(sources[0].DamageValue)>1,"Overkill probe did not clip actual HP");
            if(mode=="shield") Check(hits.All(h=>h.ShieldDamage>0&&h.BeforeHp==h.AfterHp)&&cures.Length==0,
                "Shield absorption entered the lifesteal basis");
            if(mode=="normal"||mode=="fraction"||mode=="above-one"||mode=="full")
                Check(cures.Length==3,"Original three-hit skill did not steal once per hit: "+mode);
            if(mode=="multiple-targets") Check(sources.Select(h=>h.TargetId).Distinct().Count()==2,
                "The original area skill did not exercise two actual targets");
            if(mode=="hp-range-whitelist") Check(owner.Hp>owner.MaxHp*.3
                &&owner.Statuses.Any(s=>s.ClassId==24315),
                "Original lifesteal must retain the existing HP-status trigger whitelist");
            Check(owner.SkillState.Slots.Single().ReleasedTimes==1,"Child cure consumed another ordinary release: "+mode);
            string saved=FightBattleCheckpointSerializer.Save(checkpoint);
            var restored=FightBattleCheckpointSerializer.Load(saved);
            var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            Check(FightBattleCheckpointSerializer.Save(restored)==saved,"Restore replayed healing: "+mode);
            executor.Step(0,1); resumed.Step(0,1);
            Check(FightBattleCheckpointSerializer.Save(restored)==FightBattleCheckpointSerializer.Save(checkpoint),
                "Resumed battle diverged or repeated lifesteal: "+mode);
            cases++;
        }
        Console.WriteLine("Basic lifesteal original-skill execution cases passed: "+cases);
    }
}
