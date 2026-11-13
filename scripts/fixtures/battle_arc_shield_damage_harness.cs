using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleArcShieldDamageHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Arc incoming damage: " + message); }

    static FightBattleUnitState Unit(int id, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState {
            Id=id, Side=id, CharacterType=1, Profession=1, Level=20, SubRank=1,
            X=5+id, Y=5, Hp=1000000, MaxHp=1000000, LastMaxHp=1000000
        };
        unit.Turn.Id=id; unit.Turn.Hp=unit.Hp; unit.Turn.Speed=id==1?1000:900;
        foreach (var pair in new Dictionary<string,long> {
            {"Attack",1000},{"Defence",10},{"MaxHp",unit.MaxHp},{"Speed",unit.Turn.Speed},
            {"BlockPercent",170},{"FixedBlockValue",7},{"BaseBlockPercentValue",100},
            {"BaseBlockAvoidPercentValue",100},{"BaseCritRatePercentValue",100},
            {"BaseCritAvoidPercentValue",100}
        }) unit.Properties.Add(new FightPropertyValue {Id=formula.PropertyId(pair.Key),Value=pair.Value});
        return unit;
    }

    static void Set(FightBattleUnitState unit, FightDamageFormula formula, string name, long value)
    {
        int id=formula.PropertyId(name); var property=unit.Properties.SingleOrDefault(p=>p.Id==id);
        if(property==null) unit.Properties.Add(new FightPropertyValue {Id=id,Value=value});
        else property.Value=value;
    }

    public static Dictionary<string,object>[] Run(FightOfficialCombatCatalog catalog,
        FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        var cp=new FightBattleCheckpoint { StageId=1001,MaxRound=20,RandomSeed=73,
            Grid=new FightBattleGrid {Width=20,Height=20,DefaultGroundType=0} };
        var owner=Unit(1,formula);cp.Units.Add(owner);cp.Units.Add(Unit(2,formula));
        var arc=catalog.CreateSkill(10760,0,10024,2,20,1,catalog.CalculateSkillProps(10760,2,20,1)
            .Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList());
        arc.LimitedTimes=1;owner.Skills.Add(arc);owner.SkillState.Slots.Add(catalog.CreateSkillSlot(arc,0));
        var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        var snapshots=new Dictionary<string,string> { {"before",FightBattleCheckpointSerializer.Save(cp)} };
        executor.Step(cp.Sequence,cp.Counter+1);
        Check(owner.Statuses.Single(s=>s.ClassId==24030).ShieldKind==0,"24030 became an absorbing shield");
        snapshots.Add("active",FightBattleCheckpointSerializer.Save(cp));
        while(owner.SkillState.Round<4) executor.Step(cp.Sequence,cp.Counter+1);
        Check(owner.Statuses.All(s=>s.ClassId!=24030),"original owner-round expiry did not occur");
        snapshots.Add("expired",FightBattleCheckpointSerializer.Save(cp));
        var slash=catalog.CreateSkill(10600,0,10008,2,20,1,catalog.CalculateSkillProps(10600,2,20,1)
            .Select(p=>new FightPropertyValue {Id=p.Key,Value=p.Value}).ToList());
        var incomingHits=FightBattleHitCollector.CollectAll(slash);
        Check(incomingHits.Count==1 && incomingHits[0].EntityId==10601 && incomingHits[0].DamageType==1,
            "original 10008/10600/10601 incoming attack changed");
        var cases=new List<Dictionary<string,object>>();
        foreach(var phase in snapshots)
        foreach(int seed in new[] {5,15,1})
        foreach(int avoid in new[] {0,2000})
        foreach(string mode in new[] {"normal","dodge","blinding"})
        {
            var restored=FightBattleCheckpointSerializer.Load(phase.Value);
            // The actual status and its property projection must survive a fresh executor.
            FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            Check(FightBattleCheckpointSerializer.Save(restored)==phase.Value,"cold restoration changed the combat input");
            var target=restored.Units.Single(u=>u.Id==1);var actor=restored.Units.Single(u=>u.Id==2);
            Set(actor,formula,"BlockAvoidPercent",avoid);
            if(mode=="dodge") Set(target,formula,"DodgePercent",10000);
            if(mode=="blinding") Set(actor,formula,"BlindingPercent",10000);
            var source=actor.Properties.ToDictionary(p=>p.Id,p=>p.Value);
            foreach(var p in slash.Properties) source[p.Id]=(source.TryGetValue(p.Id,out long v)?v:0)+p.Value;
            var dest=target.Properties.ToDictionary(p=>p.Id,p=>p.Value);
            var hit=incomingHits[0];long hp=target.Hp;
            var applied=new FightBattleHitExecutor(formula,new FightDamageSettlement())
                .Apply(new FightRandomStream(seed),1,actor,target,slash,hit,1,false);
            bool block=avoid==0 && (seed==5 || seed==15 && phase.Key=="active");
            Check(applied.Type.Block==block,"unexpected seeded block in "+phase.Key+"/"+seed+"/"+avoid);
            Check(applied.Type.Blinding==(mode=="blinding") && applied.Type.Dodge==(mode=="dodge"),"miss flags changed");
            Check(applied.ShieldDamage==0 && applied.Settlement.RealDamageValue==target.Hp-hp
                && applied.Settlement.DamageValue==applied.Formula.Damage,"block bypassed ordinary HP settlement");
            Check(mode=="normal" ? target.Hp<hp : target.Hp==hp,"miss or ordinary hit changed the wrong HP");
            Check(target.Statuses.Count(s=>s.ClassId==24030)==(phase.Key=="active"?1:0),"incoming hit consumed the property buff");
            Check(dest[formula.PropertyId("BlockPercent")]==(phase.Key=="active"?1970:170)
                && dest[formula.PropertyId("BlockValue")]==(phase.Key=="active"?25:7),"active/expired block projection changed");
            var info=new Dictionary<string,object> {
                {"SourceProps",source.ToDictionary(p=>p.Key.ToString(),p=>p.Value)},
                {"TargetProps",dest.ToDictionary(p=>p.Key.ToString(),p=>p.Value)},
                {"SourceType",1},{"TargetType",1},{"SourceMasterType",0},{"TargetMasterType",0},
                {"SourceLevel",20},{"TargetLevel",20},{"SourceSubRank",1},{"TargetSubRank",1},
                {"SourceProfession",1},{"TargetProfession",1},{"SourceEleType",slash.ElementType},
                {"TargetMonsterClassId",0},{"TargetMonsterType",0},{"TargetHp",hp},
                {"SkillDmgBasicPropType",hit.BasicProperty},{"SkillDmgPropType",hit.DamageProperty},
                {"SkillDmgFixedPropType",hit.FixedDamageProperty},{"Distance",0},
                {"IsPvp",false},{"StageType",1},{"CombatRatingSuppression",0}
            };
            cases.Add(new Dictionary<string,object> {
                {"name",phase.Key+"-"+seed+"-"+avoid+"-"+mode},{"info",info},
                {"flags",new Dictionary<string,bool> {{"IsBlock",applied.Type.Block},{"IsCrit",applied.Type.Critical},
                    {"IsBlinding",applied.Type.Blinding},{"IsDodge",applied.Type.Dodge},{"IsImmunity",applied.Type.Immunity}}},
                {"custom",null},{"confrontation",0},{"actualDamage",applied.Formula.Damage},
                {"actualAbsolute",applied.Formula.AbsoluteDamage},{"hpDelta",target.Hp-hp}
            });
        }
        var baseline=cases.Single(c=>(string)c["name"]=="before-15-0-normal");
        var active=cases.Single(c=>(string)c["name"]=="active-15-0-normal");
        var expired=cases.Single(c=>(string)c["name"]=="expired-15-0-normal");
        Check((long)active["hpDelta"]>(long)baseline["hpDelta"]
            && (long)expired["hpDelta"]==(long)baseline["hpDelta"],"original buff did not reduce HP damage or expiry retained its reduction");
        return cases.ToArray();
    }
}
