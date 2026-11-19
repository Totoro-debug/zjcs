using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleThunderMysteryHarness
{
    static void Check(bool condition,string text) { if(!condition) throw new InvalidOperationException("Thunder mystery: "+text); }
    static FightBattleUnitState Unit(int id,FightDamageFormula formula)
    {
        var unit=new FightBattleUnitState {Id=id,Side=id,CharacterType=1,Level=50,SubRank=5,
            X=5,Y=id==1?5:6,Hp=1000000,MaxHp=1000000,LastMaxHp=1000000};
        unit.Turn.Id=id;unit.Turn.Hp=unit.Hp;unit.Turn.Speed=id==1?1000:10;
        foreach(var entry in new Dictionary<int,long>{{1,1000},{2,10},{3,unit.MaxHp},{4,unit.Turn.Speed}})
            unit.Properties.Add(new FightPropertyValue {Id=entry.Key,Value=entry.Value});
        unit.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("CritAvoidPercent"),Value=10000});
        return unit;
    }
    public static string Run(FightOfficialCombatCatalog catalog,FightDamageFormula formula,FightOfficialSkillItemCatalog items)
    {
        foreach(string mode in new[]{"damage","dodge","blinding","immunity","cure","applicator-death"})
        {
            var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=5,RandomSeed=91,
                Grid=new FightBattleGrid {Width=12,Height=12}};
            var actor=Unit(1,formula);var target=Unit(2,formula);
            var passiveProps=catalog.CalculateSkillProps(25000,2,20,5)
                .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList();
            var passive=catalog.CreateStatus(25000,1,1,1,25000,10502,2,20,5,passiveProps);
            Check(Math.Abs(passive.Components.HitSkill.Rate-.3f)<.0001f,"Original probability changed");
            passive.Components.HitSkill.Rate=1f; // Exercise both outcomes without a probabilistic test.
            actor.Statuses.Add(passive);
            if(mode=="applicator-death") {
                Check(!passive.ForbiddenOnApplicatorDie,"Original status unexpectedly suppresses on applicator death");
                Check(!FightStatusLifecycle.SetForbidden(actor,passive,FightStatusLifecycle.ForbiddenApplicatorDie,true)
                    && passive.ForbiddenType==0,"Applicator death changed the original passive status");
            }
            var props=catalog.CalculateSkillProps(11010,2,20,5)
                .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList();
            var skill=catalog.CreateSkill(11010,0,10042,2,20,5,props);
            if(mode=="cure") {target.Hp=target.Turn.Hp=500000;skill.HitGraphEntities.Single(x=>x.EntityId==11011).ActionType=5;}
            if(mode=="dodge") target.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("DodgePercent"),Value=10000});
            if(mode=="blinding") actor.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("BlindingPercent"),Value=10000});
            if(mode=="immunity") target.Statuses.Add(catalog.CreateStatus(24007,2,2,724007));
            skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            cp.Units.AddRange(new[]{actor,target});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(0,1).Event;resumed.Step(0,1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),mode+" checkpoint differs");
            var hit=step.DamageEvents.Single(x=>x.SkillEntityId==11010);
            Check(mode!="dodge"||hit.Dodge,"Dodge fixture did not dodge");
            Check(mode!="blinding"||hit.Blinding,"Blinding fixture did not blind");
            Check(mode!="immunity"||hit.Immunity,"Immunity fixture did not block");
            Check(mode!="cure"||hit.IsCure,"Cure fixture did not heal");
            var children=step.SkillCasts.Where(x=>x.SkillEntityId==10530).ToArray();
            Check(children.Length==(mode=="damage"||mode=="applicator-death"?1:0),mode+" produced "+children.Length+" lightning casts");
            if(mode=="damage"||mode=="applicator-death") {
                Check(children[0].ActorId==1&&children[0].TargetIds.Contains(2)
                    &&children[0].SkillItemId==10502,"Lightning lost its passive source or hit target");
                Check(step.DamageEvents.Any(x=>x.SkillEntityId==10530&&x.TargetId==2&&x.Damage<0),
                    "Original lightning did not damage the hit target");
            }
            string saved=FightBattleCheckpointSerializer.Save(cp);
            Check(executor.Step(0,1).Replayed&&saved==FightBattleCheckpointSerializer.Save(cp),mode+" replay repeated trigger");
        }
        foreach (var passive in new[] { (10512,25005,10850,.25f), (10560,25026,11300,.5f), (10562,25025,11290,.4f) })
        foreach (string mode in new[] { "damage", "shield", "dodge", "blinding", "immunity" })
        {
            var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=5,RandomSeed=91,
                Grid=new FightBattleGrid {Width=12,Height=12}};
            var actor=Unit(1,formula);var target=Unit(2,formula);
            var status=catalog.CreateStatus(passive.Item2,1,1,1,passive.Item2,passive.Item1,2,20,5);
            Check(Math.Abs(status.Components.HitSkill.Rate-passive.Item4)<.0001f,
                "Original hit passive probability differs: "+passive.Item1);
            status.Components.HitSkill.Rate=1f;actor.Statuses.Add(status);
            int entity=passive.Item1==10562?10600:10500, item=passive.Item1==10562?10008:10000;
            var props=catalog.CalculateSkillProps(entity,2,20,5)
                .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList();
            var skill=catalog.CreateSkill(entity,0,item,2,20,5,props);
            if(mode=="dodge") target.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("DodgePercent"),Value=10000});
            if(mode=="blinding") actor.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("BlindingPercent"),Value=10000});
            if(mode=="immunity") target.Statuses.Add(catalog.CreateStatus(24007,2,2,724007));
            if(mode=="shield") target.Statuses.Add(new FightBattleStatusState {ClassId=90003,InstanceId=1,
                OwnerId=2,CreatorId=2,ShieldKind=FightBattleShieldKind.Persistent,ShieldInitialized=true,
                ShieldValue=100000000,ShieldRemainValue=100000000});
            skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
            cp.Units.AddRange(new[]{actor,target});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
            var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
            var restored=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
            var resumed=FightBattleExecutor.Restore(restored,formula,new FightDamageSettlement(),catalog,items);
            var step=executor.Step(0,1).Event;resumed.Step(0,1);
            var hit=step.DamageEvents.Single(x=>x.SkillEntityId==entity);
            Check(mode!="dodge"||hit.Dodge,"Dodge fixture did not dodge: "+passive.Item1);
            Check(mode!="blinding"||hit.Blinding,"Blinding fixture did not blind: "+passive.Item1);
            Check(mode!="immunity"||hit.Immunity,"Immunity fixture did not block: "+passive.Item1);
            Check(mode!="shield"||hit.Damage==0&&hit.ShieldDamage>0&&target.Hp==target.MaxHp,
                "Shield did not absorb the original hit: "+passive.Item1);
            var children=step.SkillCasts.Where(x=>x.SkillEntityId==passive.Item3).ToArray();
            Check(children.Length==((mode=="damage"||mode=="shield")?1:0),
                passive.Item1+"/"+mode+" produced "+children.Length+" child casts");
            Check(status.Components.HitSkill.InvokeCount==children.Length,
                passive.Item1+"/"+mode+" changed trigger counters without a valid hit");
            if(children.Length>0) Check(children[0].SkillItemId==passive.Item1 && children[0].ActorId==1
                && children[0].TargetIds.Contains(2),"Child lost passive source or target: "+passive.Item1);
            if(children.Length>0) Check(step.DamageEvents.Any(hitEvent=>hitEvent.SkillEntityId==passive.Item3
                && hitEvent.SkillItemId==passive.Item1 && hitEvent.SourceId==1 && hitEvent.TargetId==2),
                "Child damage lost its passive source or target: "+passive.Item1);
            Check(FightBattleCheckpointSerializer.Save(cp)==FightBattleCheckpointSerializer.Save(restored),
                passive.Item1+"/"+mode+" checkpoint differs");
        }
        foreach (var passive in new[] { (10512,25005,10850), (10560,25026,11300) })
        {
            int applied=0, skipped=0;
            for (int i=0;i<64;i++)
            {
                var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=5,RandomSeed=73+i*1234567,
                    Grid=new FightBattleGrid {Width=12,Height=12}};
                var actor=Unit(1,formula);var target=Unit(2,formula);
                foreach (var unit in new[] { actor,target })
                    foreach (string property in new[] { "BaseEffectRate", "BaseEffectDodge" })
                        unit.Properties.Add(new FightPropertyValue {Id=formula.PropertyId(property),Value=10000});
                var status=catalog.CreateStatus(passive.Item2,1,1,1,passive.Item2,passive.Item1,2,20,5);
                status.Components.HitSkill.Rate=1f;actor.Statuses.Add(status);
                var props=catalog.CalculateSkillProps(10500,2,20,5)
                    .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList();
                var skill=catalog.CreateSkill(10500,0,10000,2,20,5,props);
                skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
                cp.Units.AddRange(new[]{actor,target});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);cp.Waves.Add(wave);
                var step=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items).Step(0,1).Event;
                Check(step.SkillCasts.Count(value=>value.SkillEntityId==passive.Item3)==1,
                    "Secondary effect probe did not cast its original child: "+passive.Item1);
                bool occurred=passive.Item1==10512
                    ? step.Movements.Any(value=>value.UnitId==target.Id)
                    : target.Statuses.Any(value=>value.ClassId==24215);
                if(occurred && passive.Item1==10512) {
                    var move=step.Movements.Single(value=>value.UnitId==target.Id);
                    var start=move.Path.First();var end=move.Path.Last();
                    Check(move.MoveBehaviourId==10853 && Math.Abs(end.X-start.X)+Math.Abs(end.Y-start.Y)==1,
                        "Original unstable aura knockback identity or distance differs");
                }
                if(occurred && passive.Item1==10560) {
                    var frozen=target.Statuses.Single(value=>value.ClassId==24215);
                    Check(frozen.DurationRound==1 && frozen.CreatorId==actor.Id && frozen.ApplyingSkillItemId==10560,
                        "Original freezing duration or passive source differs");
                }
                if(occurred) applied++; else skipped++;
            }
            Check(applied>0&&skipped>0,"Original probabilistic secondary effect did not produce both outcomes: "
                +passive.Item1+" applied="+applied+" skipped="+skipped);
        }
        foreach (string obstacle in new[] { "terrain", "enemy", "ally" })
        {
            bool collided=false; string lastAuraProbe="";
            for (int i=0;i<64 && !collided;i++)
            {
                var cp=new FightBattleCheckpoint {StageId=1001,MaxRound=5,RandomSeed=73+i*1234567,
                    Grid=new FightBattleGrid {Width=12,Height=12,DefaultGroundType=2}};
                var actor=Unit(1,formula);var target=Unit(2,formula);
                actor.MoveMask=target.MoveMask=5;
                foreach(var unit in new[]{actor,target})
                    foreach(string property in new[]{"BaseEffectRate","BaseEffectDodge"})
                        unit.Properties.Add(new FightPropertyValue {Id=formula.PropertyId(property),Value=10000});
                actor.Properties.Add(new FightPropertyValue {Id=formula.PropertyId("EffectRateValue"),Value=20000});
                var status=catalog.CreateStatus(25005,1,1,1,25005,10512,2,20,5);
                status.Components.HitSkill.Rate=1f;actor.Statuses.Add(status);
                var skill=catalog.CreateSkill(10500,0,10000,2,20,5,catalog.CalculateSkillProps(10500,2,20,5)
                    .Select(value=>new FightPropertyValue {Id=value.Key,Value=value.Value}).ToList());
                skill.LimitedTimes=1;actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
                cp.Units.AddRange(new[]{actor,target});var wave=new FightBattleWaveState();wave.UnitIds.Add(2);
                if(obstacle=="terrain") cp.Grid.BlockedPositions.Add(new FightGridPoint(5,7));
                else {
                    var blocker=Unit(3,formula);blocker.Side=obstacle=="enemy"?2:1;blocker.Y=7;blocker.MoveMask=5;
                    cp.Units.Add(blocker);if(blocker.Side==2)wave.UnitIds.Add(3);
                }
                cp.Waves.Add(wave);var executor=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
                var result=executor.Step(0,1);var hits=result.Event.DamageEvents.Where(value=>value.SkillEntityId==10100).ToArray();
                lastAuraProbe="position="+target.X+","+target.Y+" moves="+string.Join(",",result.Event.Movements.Select(value=>value.UnitId+":"+value.MoveBehaviourId))
                    +" damage="+string.Join(",",result.Event.DamageEvents.Select(value=>value.SkillEntityId+":"+value.TargetId));
                if(hits.Length==0)continue;
                int[] expected=obstacle=="enemy"?new[]{2,3}:new[]{2};
                Check(hits.Select(value=>value.TargetId).OrderBy(value=>value).SequenceEqual(expected)
                    && hits.All(value=>value.TrueDamage && value.Damage==-600)
                    && result.Event.Movements.Any(value=>value.UnitId==2 && value.MoveBehaviourId==10853)
                    && target.X==5 && target.Y==6,"Aura collision differs for "+obstacle);
                string saved=FightBattleCheckpointSerializer.Save(cp);
                Check(executor.Step(0,1).Replayed && saved==FightBattleCheckpointSerializer.Save(cp),
                    "Aura collision repeated on replay for "+obstacle);
                collided=true;
            }
            Check(collided,"Aura did not resolve a collision for "+obstacle+" "+lastAuraProbe);
        }
        return "Hit passives passed: damage, shield absorption, dodge, blinding, immunity, aura collision and checkpoint restore.";
    }
}
