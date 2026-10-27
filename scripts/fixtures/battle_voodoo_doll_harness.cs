using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleVoodooDollHarness
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static FightBattleUnitState Unit(int id, int side, int type, long hp, int speed) {
        var u = new FightBattleUnitState { Id=id, Side=side, CharacterType=type, Hp=hp,
            MaxHp=hp, LastMaxHp=hp, Level=150, SubRank=1, X=5, Y=5 };
        u.Properties.Add(new FightPropertyValue {Id=1,Value=10001});
        u.Properties.Add(new FightPropertyValue {Id=2,Value=10003});
        u.Properties.Add(new FightPropertyValue {Id=3,Value=hp});
        u.Properties.Add(new FightPropertyValue {Id=4,Value=speed});
        u.Turn = new FightTurnUnit {Id=id,Hp=hp,Speed=speed}; return u;
    }
    static FightBattleStatusState Debuff(int id, int owner) => new FightBattleStatusState {
        InstanceId=id, ClassId=90001, StatusType=2, OwnerId=owner, CreatorId=1,
        RoundTarget=2, DurationRound=4, CurDurationRound=4, StartRound=1,
        StackCount=4, HpAccTriggeredCount=7
    };
    public static void Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        FightDamageFormula formula)
    {
        var player=Unit(1,1,1,100007,101); var enemy=Unit(2,2,2,2000000,1); enemy.X=6;
        var summonSkill=catalog.CreateSkill(12200,0,10097,5,20,1,null);
        player.Skills.Add(summonSkill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(summonSkill,0));
        var state=new FightBattleCheckpoint {StageId=1001,RandomSeed=13,
            ResultRules=new FightBattleResultRules()};
        state.Units.Add(player);state.Units.Add(enemy);
        var executor=FightBattleExecutor.Begin(state,formula,new FightDamageSettlement(),catalog,items);
        executor.Step(0,1);
        var doll=state.Units.Find(u=>u.VoodooDoll!=null);
        Check(doll!=null,"Official 12200 did not generate its 4600 substitute entity.");
        Check(doll.VoodooDoll.TargetId==2 && doll.CharacterType==2 && doll.Side==2
            && doll.Level==20 && doll.X==5 && doll.Y==7,"Substitute lost target identity/side, skill level or caster-front spawn.");
        Check(doll.Properties.Find(p=>p.Id==2).Value==6001,"Substitute did not inherit target defence at 60%.");
        var buff=new FightBattleStatusState {ClassId=90002,StatusType=1};
        Check(FightStatusLifecycle.Apply(doll,buff,0,100)==null,"Substitute accepted a buff.");

        // Destination clock and source clock differ. Keep remaining two rounds and stack/counters.
        doll.SkillState.Round=3; enemy.SkillState.Round=9;
        var negative=Debuff(101,doll.Id);doll.Statuses.Add(negative);
        var shield=new FightBattleStatusState {ClassId=90003,InstanceId=102,
            ShieldKind=FightBattleShieldKind.Persistent,ShieldInitialized=true,
            ShieldValue=3000000,ShieldRemainValue=3000000};enemy.Statuses.Add(shield);
        // Force a 500k-HP doll to receive one million post-formula damage in the actual executor.
        doll.Hp=doll.MaxHp=doll.LastMaxHp=500000;
        doll.Properties.Find(p=>p.Id==3).Value=500000;
        doll.BaseProperties.Find(p=>p.Id==3).Value=500000;
        player.Skills.Clear();player.SkillState.Slots.Clear();
        var attack=new FightBattleSkillDefinition {EntityId=90010,Slot=0,TargetType=1};
        attack.Range.Add(new FightGridPoint(0,2));
        attack.Hits.Add(new FightBattleHitDefinition {EntityId=90011,DamageType=8,
            HasCustomDamage=true,CustomDamage=-1000000});
        player.Skills.Add(attack);player.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
        var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(state));
        executor=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
        var step=executor.Step(0,2).Event;
        var linked=restored.Units.Find(u=>u.Id==2);var deadDoll=restored.Units.Find(u=>u.VoodooDoll!=null);
        Check(linked.Hp==1000000,"Overflow transmission did not deduct the full million HP: "+linked.Hp);
        Check(linked.Statuses.Find(s=>s.ClassId==90003).ShieldRemainValue==3000000,"Transmission consumed target shield.");
        var transmitted=step.DamageEvents.Find(d=>d.TargetId==2 && d.TrueDamage);
        Check(transmitted!=null && transmitted.IgnoreShield && transmitted.DamageValue==-1000000
            && transmitted.Damage==-1000000,"Direct true-damage event lost full input or display flags.");
        var transferred=linked.Statuses.Find(s=>s.ClassId==90001);
        Check(transferred!=null && transferred.StackCount==4 && transferred.CurDurationRound==2
            && transferred.StartRound==9 && transferred.HpAccTriggeredCount==7,
            "Death transfer lost remaining rounds, stacks or internal counters.");
        Check(deadDoll.Ended && deadDoll.VoodooDoll.DebuffsTransferred,"Doll death was not finalized.");
        Check(!linked.Statuses.Exists(s=>s.ClassId==12204),"Dead substitute left its linked-target marker behind.");
        int serial=200;
        Check(FightVoodooDollRules.TransferDebuffs(deadDoll,linked,restored.Units,()=>++serial)==0,
            "Repeated death transferred debuffs twice.");

        var source=Unit(3,2,2,100,1);source.VoodooDoll=new FightVoodooDollLink {TargetId=2};
        source.SkillState.Round=3;var existing=Debuff(201,2);existing.IsOpenStack=true;
        existing.MaxStackedCount=6;existing.StackCount=3;existing.IsResetDurationWhenStack=true;
        existing.StartRound=8;linked.Statuses.Clear();linked.Statuses.Add(existing);
        source.Statuses.Add(Debuff(202,3));restored.Units.Add(source);
        FightVoodooDollRules.TransferDebuffs(source,linked,restored.Units,()=>++serial);
        Check(linked.Statuses.Count==1 && existing.StackCount==6 && existing.StartRound==9
            && existing.CurDurationRound==4,"Transfer bypassed official stack cap/refresh behavior.");

        // A creator-clock lifetime belongs to a different unit and must still expire there.
        var life=catalog.CreateStatus(12202,3,1,203,12200,10097,5,20,1,player.Properties);
        source.Statuses.Clear();source.Ended=false;life.StartRound=1;life.CurDurationRound=3;
        source.Statuses.Add(life);
        FightStatusLifecycle.TickTurn(restored.Units[0],restored.Units,4,FightStatusLifecycle.TimingEnd);
        Check(source.Ended && source.Statuses.Count==0,"Creator-clock lifetime did not destroy the applicator.");
        linked.Hp=0;Check(restored.ResultRules.Evaluate(restored,1).Won,
            "A target-type substitute incorrectly blocked enemy clear.");
    }
}

