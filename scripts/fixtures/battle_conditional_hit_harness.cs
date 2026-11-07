using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

public static class BattleConditionalHitHarness
{
    static void Check(bool value,string message) {if(!value)throw new Exception(message);}
    static FightBattleUnitState Unit(int id) => new FightBattleUnitState {Id=id,Hp=500,MaxHp=1000,
        Turn=new FightTurnUnit {Id=id,Hp=500,Speed=100}};
    static Dictionary<int,long> Props() => new Dictionary<int,long>();
    static long Value(Dictionary<int,long> props,int id) => props.TryGetValue(id,out long value)?value:0;
    public static void Run(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        var actor=Unit(1);var target=Unit(2);
        var skill=new FightBattleSkillDefinition {EntityId=90010,ActionType=2,SkillType=0,CanTriggerChild=false};
        var hit=new FightBattleHitDefinition {EntityId=90011};
        int prop=catalog.PropertyType("StatusDmgAddPer");
        var status=catalog.CreateStatus(60280,1,1,1,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);
        var cast=new FightConditionalHitProperties(actor,skill,items);var props=Props();
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);
        Check(Value(props,prop)==0 && status.Components.HitDmgAddPer.InvokeCount==0,
            "Official 60280 ignored its target action condition.");
        target.Statuses.Add(new FightBattleStatusState {ClassId=90020,ActionType=52});
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);
        Check(Value(props,prop)==1000 && status.Components.HitDmgAddPer.InvokeCount==1,
            "Official 60280 failed against an actual target Action 52 status.");
        Check(actor.Properties.Count==0,"Conditional properties leaked into permanent actor properties.");
        props=Props();target.Statuses.Clear();cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);
        Check(props.Count==0,"A previous target's conditional properties leaked into another hit.");
        target.Statuses.Add(new FightBattleStatusState {ClassId=90020,ActionType=57});
        var counter=status.Components.HitDmgAddPer;
        counter.ConditionCount=2;counter.ConditionProgress=0;counter.InvokeCount=0;
        counter.MaxInvokeCount=1;counter.RoundResetCount=1;
        cast.Merge(target,hit,new FightDamageTypeData {Dodge=true},false,null,Props());
        Check(counter.ConditionProgress==0,"Dodged damage advanced the condition counter.");
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,Props());
        Check(counter.ConditionProgress==1 && counter.InvokeCount==0,"Two-hit condition fired on its first hit.");
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,Props());
        Check(counter.InvokeCount==1,"Two-hit condition did not fire on its second hit.");
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,Props());
        Check(counter.ConditionProgress==0,"Invocation maximum continued consuming condition hits.");
        actor.SkillState.Round++;
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,Props());
        Check(counter.InvokeCount==0 && counter.ConditionProgress==1,"Personal round reset did not reopen its exhausted maximum.");
        target.Statuses.Clear();
        actor.Statuses.Clear();status=catalog.CreateStatus(60290,1,1,2,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=2000}};
        actor.Statuses.Add(status);cast=new FightConditionalHitProperties(actor,skill,items);
        props=Props();cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);
        Check(props.Count==0,"Official 60290 triggered without a target debuff.");
        target.Statuses.Add(new FightBattleStatusState {ClassId=90021,StatusType=2});
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);
        Check(Value(props,prop)==2000,"Official 60290 did not detect the target debuff.");

        actor.Statuses.Clear();status=catalog.CreateStatus(25036,1,1,20,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);cast=new FightConditionalHitProperties(actor,skill,items);
        target.Hp=700;props=Props();cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);
        Check(props.Count==0,"Original strict 70% HP comparison triggered on its float boundary.");
        target.Hp=701;cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);
        Check(Value(props,prop)==1000,"Original greater-than-70% HP comparison missed 70.1%.");
        target.Hp=500;

        actor.Statuses.Clear();status=catalog.CreateStatus(24191,1,1,3,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);cast=new FightConditionalHitProperties(actor,skill,items);
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,Props());
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,Props());
        Check(actor.Statuses.Contains(status) && status.Components.HitDmgAddPer.DurationSkillCount==1,
            "Multi-hit cast consumed its duration before the last hit.");
        cast.EndCast();Check(!actor.Statuses.Contains(status),"One-cast conditional status did not expire at cast end.");

        actor.Statuses.Clear();skill.ElementType=103;status=catalog.CreateStatus(24085,1,1,4,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);cast=new FightConditionalHitProperties(actor,skill,items);
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,Props());cast.EndCast();
        Check(actor.Statuses.Contains(status),"Trigger-only duration expired without its required charge.");
        var stacks=catalog.CreateStatus(24058,1,1,5,90010);stacks.StackCount=3;actor.Statuses.Add(stacks);
        cast=new FightConditionalHitProperties(actor,skill,items);props=Props();
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);cast.EndCast();
        Check(Value(props,prop)==3000 && !actor.Statuses.Contains(stacks) && !actor.Statuses.Contains(status),
            "Official 24085 lost consumed-charge scaling or trigger-only cast duration.");
        actor.Statuses.Clear();target.Statuses.Clear();skill.ItemId=10002;
        status=catalog.CreateStatus(25271,1,1,40,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);cast=new FightConditionalHitProperties(actor,skill,items);
        foreach(int id in new[] {90001,90001,90002})
            target.Statuses.Add(new FightBattleStatusState {ClassId=id,StatusType=2});
        props=Props();cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);
        Check(Value(props,prop)==1200,"Official distinct-debuff scaling counted duplicate classes or lost the single-hit item classification.");
        skill.ItemId=10095;props=Props();cast=new FightConditionalHitProperties(actor,skill,items);
        cast.Merge(target,hit,new FightDamageTypeData(),false,null,props);
        Check(props.Count==0,"SkillSortType.SingleHit was confused with the skill's runtime SkillType.");
        ActualDamage(catalog,items,formula);
        BodyScope(catalog,items,formula);
        ApplyConditions(catalog,items);
        TagConditions(catalog,items);
        BurningGridCount(catalog,items,formula);
        ActualHits(catalog,items,formula);
    }
    static void ApplyConditions(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items)
    {
        var actor=Unit(1);var target=Unit(2);target.CharacterType=2;
        var skill=new FightBattleSkillDefinition {EntityId=90010,ActionType=2};
        int prop=catalog.PropertyType("StatusDmgAddPer");
        var status=catalog.CreateStatus(60230,1,1,30,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000},
            new FightPropertyValue {Id=catalog.PropertyType("Attack"),Value=9000}};
        actor.Statuses.Add(status);var properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(properties.Count==0,"Official 60230 applied without a target shield.");
        var shield=new FightBattleStatusState {ShieldKind=FightBattleShieldKind.Persistent,
            ShieldInitialized=true,ShieldRemainValue=100};target.Statuses.Add(shield);
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(Value(properties,prop)==1000 && properties.Count==1,
            "Shield condition failed or unrelated status properties leaked into the hit.");
        shield.ShieldRemainValue=0;properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(properties.Count==0,"Exhausted persistent shield still satisfied the condition.");
        actor.Statuses.Clear();status=catalog.CreateStatus(60240,1,1,31,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=2000}};
        actor.Statuses.Add(status);properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(properties.Count==0,"Summon condition accepted a monster.");
        target.CharacterType=3;
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(Value(properties,prop)==2000,"Summon condition did not accept an actual summon.");
        var doll=Unit(3);doll.CharacterType=4;doll.VoodooDoll=new FightVoodooDollLink {TargetId=target.Id};
        properties=Props();
        FightConditionalApplyProperties.Merge(actor,doll,skill,new FightDamageTypeData(),false,properties,
            new[] {actor,target,doll});
        Check(Value(properties,prop)==2000,"Substitute did not use its linked target's character type.");
        actor.Statuses.Clear();status=catalog.CreateStatus(60300,1,1,32,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(properties.Count==0,"Specific-skill condition accepted another skill.");
        skill.EntityId=10100;
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(Value(properties,prop)==1000,"Original specific-skill condition did not accept 10100.");
        actor.Statuses.Clear();status=catalog.CreateStatus(24208,1,1,33,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(Value(properties,prop)==-1000,"Configured reduction did not subtract its original property.");
        actor.Statuses.Clear();status=catalog.CreateStatus(24079,1,1,34,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);var stack=catalog.CreateStatus(24031,1,1,35,90010);
        stack.StackCount=8;actor.Statuses.Add(stack);properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(stack.StackCount==8 && properties.Count==0,"Another skill consumed the original five-charge requirement.");
        skill.EntityId=12860;
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData {Dodge=true},false,properties);
        Check(stack.StackCount==8,"Dodged hit consumed charges.");
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(stack.StackCount==3 && Value(properties,prop)==1000,"Original 24079 did not consume exactly five charges.");
        properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(stack.StackCount==3 && properties.Count==0,"Insufficient charge was consumed or applied damage.");
        actor.Statuses.Clear();
        foreach(int id in new[] {25098,60270})
        {
            status=catalog.CreateStatus(id,1,1,id,90010);
            status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
            actor.Statuses.Add(status);
        }
        properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(properties.Count==0,"Unconfirmed entity count or missing source tags were treated as a match.");
        actor.Statuses.Clear();status=catalog.CreateStatus(25095,1,1,36,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);
        int[] hp={1000,851,850,550,250,1};int[] scales={0,0,1,3,5,5};
        for(int i=0;i<hp.Length;i++)
        {
            target.Hp=hp[i];properties=Props();
            FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
            Check(Value(properties,prop)==1000*scales[i],"Confirmed HP-loss scale failed at HP="+hp[i]);
        }
        target.MaxHp=1001;target.Hp=851;properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(properties.Count==0,"Fractional 150.15 HP threshold triggered at only 150 lost HP.");
        target.Hp=850;
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(Value(properties,prop)==1000,"Fractional 150.15 HP threshold missed 151 lost HP.");
        Check(actor.Properties.Count==0,"Conditional apply properties changed permanent actor stats.");
    }
    static void TagConditions(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items)
    {
        var actor=Unit(1);var target=Unit(2);int prop=catalog.PropertyType("StatusDmgAddPer");
        var status=catalog.CreateStatus(60270,1,1,37,90010);
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);
        var skill=catalog.CreateSkill(12000,0,0,1,1,1,null);
        var properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(Value(properties,prop)==1000 && status.Components.HitApplyDamage.SourceSkillTagQuery.Count==1,
            "Original 60270 query did not read the actual skill's pinned tag 8192.");
        skill=catalog.CreateSkill(12140,0,0,1,1,1,null);skill.RootEntityId=12000;properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(properties.Count==0,"A child used its root skill's tags instead of its own.");
        skill=catalog.CreateSkill(12000,0,0,1,1,1,null);skill.GameplayTags=null;properties=Props();
        FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
        Check(properties.Count==0,"Missing tags bypassed the original query.");
        var cast=new FightConditionalHitProperties(actor,skill,items,catalog:catalog);
        cast.Merge(target,new FightBattleHitDefinition(),new FightDamageTypeData(),false,null,properties);
        Check(Value(properties,prop)==1000 && skill.GameplayTags[0]==catalog.SkillGameplayTags(12000)[0],
            "Legacy skill tag restoration did not preserve the pinned original tag words.");
        var copy=status.Components.Copy();copy.HitApplyDamage.SourceSkillTagQuery[0].Tags[0]=0;
        Check(status.Components.HitApplyDamage.SourceSkillTagQuery[0].Tags[0]==8192,
            "Status copying shared a mutable source tag query with the original.");
        int[] statusIds={60270,25135,25141,25282};
        int[] skillIds={12000,12000,12030,12540};
        for(int i=0;i<statusIds.Length;i++)
        {
            actor.Statuses.Clear();
            status=catalog.CreateStatus(statusIds[i],1,1,38+i,90010);
            status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
            actor.Statuses.Add(status);
            skill=catalog.CreateSkill(skillIds[i],0,0,1,1,1,null);properties=Props();
            FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
            Check(Value(properties,prop)==1000,"Original source tag query rejected its matching skill, including child-disabled skills: "+statusIds[i]);
            skill=catalog.CreateSkill(12140,0,0,1,1,1,null);properties=Props();
            FightConditionalApplyProperties.Merge(actor,target,skill,new FightDamageTypeData(),false,properties);
            Check(properties.Count==0,"Original source tag query accepted an unrelated skill: "+statusIds[i]);
        }
    }
    static void BurningGridCount(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        var actor=Unit(1);actor.Side=actor.CharacterType=1;
        var status=catalog.CreateStatus(25098,1,1,50,12040);
        int prop=catalog.PropertyType("FinalCureScale");
        status.StatusProperties=new List<FightPropertyValue> {new FightPropertyValue {Id=prop,Value=1000}};
        actor.Statuses.Add(status);
        var units=new List<FightBattleUnitState> {actor};
        var grid=new List<FightBattleGridItemState>();
        var skill=catalog.CreateSkill(12040,0,10597,1,1,1,null);
        Check(status.RoundEndRulesSupported && status.RoundEndEntityCount.Range.Count==13,
            "Official 25098 round-end entity count or its original range was not restored.");
        Check(FightStatusTriggerRuntime.CollectRoundEndSkills(actor,units,new FightRandomStream(1),grid).Count==0,
            "A round-end heal was created without a burning grid entity.");
        for(int n=0;n<=8;n++)
        {
            var properties=Props();
            FightConditionalApplyProperties.Merge(actor,actor,skill,new FightDamageTypeData(),true,properties,units,grid);
            Check(Value(properties,prop)==1000*Math.Max(0,Math.Min(n,6)-1),
                "Official burning grid cure scale failed for entity count="+n);
            if(n==8) break;
            var pos=status.RoundEndEntityCount.Range[n];
            grid.Add(new FightBattleGridItemState {Id=100+n,EntityClassId=3051,GridItemClassId=3320,
                OwnerId=99,X=pos.X,Y=pos.Y});
        }
        grid.Add(new FightBattleGridItemState {EntityClassId=3051,X=20,Y=20});
        grid.Add(new FightBattleGridItemState {EntityClassId=3051,Ended=true});
        grid.Add(new FightBattleGridItemState {EntityClassId=3054});
        var excluded=Props();
        FightConditionalApplyProperties.Merge(actor,actor,skill,new FightDamageTypeData(),true,excluded,units,
            new List<FightBattleGridItemState> {grid[0],grid[8],grid[9],grid[10]});
        Check(excluded.Count==0,"Ended, outside-range or different-template grids increased a one-grid cure.");
        var calls=FightStatusTriggerRuntime.CollectRoundEndSkills(actor,units,new FightRandomStream(1),grid);
        Check(calls.Count==1 && calls[0].Trigger.EntityId==12040 && calls[0].SourceUnitId==1
            && calls[0].TargetUnitId==1 && status.RoundEndTriggeredCount==1,
            "Official burning grid round-end healing lost its holder or counted the creating faction.");
        foreach(string name in new[] {"BaseCureAdd","BaseBeCureAdd"})
            actor.Properties.Add(new FightPropertyValue {Id=catalog.PropertyType(name),Value=10000});
        skill.HasCustomCure=true;skill.CustomCure=100;
        var executor=new FightBattleHitExecutor(formula,new FightDamageSettlement());
        var hit=new FightBattleHitDefinition {EntityId=12041,ActionType=5};
        var result=executor.Apply(new FightRandomStream(3),1,actor,actor,skill,hit,0,false,
            conditionalProperties:new FightConditionalHitProperties(actor,skill,items,units,catalog,grid));
        Check(result.Settlement.DamageValue==150,"Real cure did not use the original capped five increments.");
        actor.X=20;actor.Y=20;
        // Only one live entity remains in the new range, so it grants no extra increment.
        var moved=Props();
        FightConditionalApplyProperties.Merge(actor,actor,skill,new FightDamageTypeData(),true,moved,units,grid);
        Check(moved.Count==0,"Entity scaling retained a count from the holder's previous position.");
        var checkpoint=new FightBattleCheckpoint {StageId=1001,RandomSeed=1,GridItems=grid};
        checkpoint.Units.Add(actor);
        checkpoint=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        Check(checkpoint.GridItems.Count==11 && checkpoint.GridItems[0].OwnerId==99
            && checkpoint.Units[0].Statuses[0].RoundEndEntityCount.Range.Count==13,
            "Checkpoint lost burning grid entities, ownership or the original query range.");
        var copied=FightStatusLifecycle.Apply(Unit(2),status,0,51);
        copied.RoundEndEntityCount.Range.Clear();
        Check(status.RoundEndEntityCount.Range.Count==13,"A copied status shared its mutable entity range.");
    }
    static void ActualDamage(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        var actor=Unit(1);actor.Side=actor.CharacterType=1;
        var target=Unit(2);target.Side=target.CharacterType=2;
        var skill=new FightBattleSkillDefinition {EntityId=90010,ActionType=2,SkillType=0,CanTriggerChild=false};
        var hit=new FightBattleHitDefinition {EntityId=90011,ActionType=1,DamageType=1,
            HasCustomDamage=true,CustomDamage=100};
        var status=catalog.CreateStatus(60290,1,1,22,90010);
        status.StatusProperties=new List<FightPropertyValue> {
            new FightPropertyValue {Id=catalog.PropertyType("StatusDmgAddPer"),Value=2000}};
        actor.Statuses.Add(status);
        var executor=new FightBattleHitExecutor(formula,new FightDamageSettlement());
        var cast=new FightConditionalHitProperties(actor,skill,items);
        long baseline=executor.Apply(new FightRandomStream(13),1,actor,target,skill,hit,0,false,
            conditionalProperties:cast).Settlement.DamageValue;
        target.Hp=target.Turn.Hp=500;
        target.Statuses.Add(new FightBattleStatusState {ClassId=90021,StatusType=2});
        long increased=executor.Apply(new FightRandomStream(13),2,actor,target,skill,hit,0,false,
            conditionalProperties:cast).Settlement.DamageValue;
        Check(baseline<0 && increased<baseline && Math.Abs(increased-baseline*1.2)<=1,
            "Real damage settlement did not apply the gated 20% increment: "+baseline+" -> "+increased);
        target.Hp=target.Turn.Hp=500;target.Statuses.Clear();
        long next=executor.Apply(new FightRandomStream(13),3,actor,target,skill,hit,0,false,
            conditionalProperties:cast).Settlement.DamageValue;
        Check(next==baseline && status.Components.HitDmgAddPer.InvokeCount==1,
            "Real damage retained temporary properties after its target debuff disappeared.");
        actor.Statuses.Clear();status=catalog.CreateStatus(60230,1,1,23,90010);
        status.StatusProperties=new List<FightPropertyValue> {
            new FightPropertyValue {Id=catalog.PropertyType("StatusDmgAddPer"),Value=2000}};
        actor.Statuses.Add(status);
        var shield=new FightBattleStatusState {ShieldKind=FightBattleShieldKind.Persistent,
            ShieldInitialized=true,ShieldRemainValue=1000};
        target.Statuses.Add(shield);target.Hp=target.Turn.Hp=500;
        var shielded=executor.Apply(new FightRandomStream(13),4,actor,target,skill,hit,0,false,
            conditionalProperties:new FightConditionalHitProperties(actor,skill,items));
        Check(Math.Abs(shielded.ShieldDamage+baseline*1.2)<=1 && target.Hp==500,
            "Official 60230 did not apply its conditional increment before real shield absorption.");
        shield.ShieldRemainValue=0;
        next=executor.Apply(new FightRandomStream(13),5,actor,target,skill,hit,0,false,
            conditionalProperties:new FightConditionalHitProperties(actor,skill,items)).Settlement.DamageValue;
        Check(next==baseline,"Real hit kept the shield-only increment after shield exhaustion.");
    }
    static void BodyScope(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        var actor=Unit(1);actor.Side=actor.CharacterType=1;actor.Turn.Speed=1000;
        var enemy=Unit(2);enemy.Side=enemy.CharacterType=2;enemy.X=3;
        for(int x=-2;x<=2;x++) for(int y=-2;y<=2;y++)
            enemy.BodyOffsets.Add(new FightGridPoint(x,y));
        var skill=catalog.CreateSkill(10660,0,10014,1,1,0,null);
        var units=new List<FightBattleUnitState> {actor,enemy};
        Check(FightBattleTargeting.SelectInSkillRange(actor,units,skill).Count==0
            && FightBattleTargeting.HasCasterAreaTargets(actor,units,skill),
            "Original 10660 did not intersect the nearby body of a boss whose centre lies outside its scope.");
        foreach(var hit in FightBattleHitCollector.CollectAll(skill))
            Check(FightBattleTargeting.SelectScopeTargets(actor,units,skill,null,hit).Count==1,
                "A multi-cell body was missed or counted more than once by a single scope hit.");
        enemy.X=4;
        Check(!FightBattleTargeting.HasCasterAreaTargets(actor,units,skill),
            "Body intersection reached beyond the original eight-cell scope.");
        enemy.X=3;
        actor.Skills.Add(skill);actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill,0));
        var status=catalog.CreateStatus(60290,1,1,26,10660);actor.Statuses.Add(status);
        enemy.Statuses.Add(catalog.CreateStatus(24009,2,1,27,10660));
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13};cp.Units.AddRange(units);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        for(int i=0;i<20;i++)
        {
            var step=runner.Step(0,runner.State.Counter+1).Event;
            if(!step.SkillCasts.Exists(c=>c.SkillEntityId==10660)) continue;
            Check(step.DamageEvents.FindAll(d=>d.SourceId==1 && d.TargetId==2).Count==4
                && status.Components.HitDmgAddPer.InvokeCount==4,
                "Real 10660 did not settle its four original hits and four conditional triggers exactly once per body.");
            return;
        }
        throw new Exception("Original 10660 never cast at the nearby boss body.");
    }
    static void ActualHits(FightOfficialCombatCatalog catalog,FightOfficialSkillItemCatalog items,FightDamageFormula formula)
    {
        var actor=Unit(1);actor.Side=actor.CharacterType=1;actor.Hp=actor.Turn.Hp=100;
        actor.Turn.Speed=1000;
        foreach(var pair in new[] {new KeyValuePair<string,long>("MaxHp",1000),
            new KeyValuePair<string,long>("Speed",1000),new KeyValuePair<string,long>("BaseCureAdd",10000),
            new KeyValuePair<string,long>("BaseBeCureAdd",10000)})
            actor.Properties.Add(new FightPropertyValue {Id=catalog.PropertyType(pair.Key),Value=pair.Value});
        var enemy=Unit(2);enemy.Side=enemy.CharacterType=2;enemy.X=1;
        var skill=new FightBattleSkillDefinition {EntityId=90010,ActionType=2,SkillType=3,TargetType=0,
            HasCustomCure=true,CustomCure=100};
        skill.Range.Add(new FightGridPoint(0,0));
        skill.Hits.Add(new FightBattleHitDefinition {EntityId=90011,ActionType=5,Delay=.2f});
        skill.Hits.Add(new FightBattleHitDefinition {EntityId=90012,ActionType=5,Delay=.4f});
        actor.Skills.Add(skill);actor.SkillState.Slots.Add(new FightSkillSlotState {Slot=0,LimitedTimes=-1});
        var status=new FightBattleStatusState {ClassId=90020,InstanceId=1,OwnerId=1,CreatorId=1,RootSkillId=90010,
            DurationRound=-1,StatusProperties=new List<FightPropertyValue> {
                new FightPropertyValue {Id=catalog.PropertyType("CureAddPercent"),Value=1000}},
            StatusConfigProperties=new List<FightPropertyValue>(),
            Components=new FightBattleStatusComponentState {HitDmgAddPer=new FightBattleStatusHitDmgAddPerComponentState {
                DefinitionResolved=true,HitSourceType=2,Damage=false,Cure=true,DurationSkillCount=2,OnlySkillType=3}}};
        actor.Statuses.Add(status);
        var cp=new FightBattleCheckpoint {StageId=1001,RandomSeed=13};cp.Units.Add(actor);cp.Units.Add(enemy);
        var runner=FightBattleExecutor.Begin(cp,formula,new FightDamageSettlement(),catalog,items);
        NextCast(runner);
        Check(actor.Hp==320 && status.Components.HitDmgAddPer.InvokeCount==2
            && status.Components.HitDmgAddPer.DurationSkillCount==1,"Real two-hit cure did not use per-hit temporary properties and one duration charge: "+actor.Hp);
        cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        actor=cp.Units.Find(u=>u.Id==1);status=actor.Statuses.Find(s=>s.ClassId==90020);
        NextCast(runner);
        Check(actor.Hp==540 && status.Components.HitDmgAddPer.InvokeCount==4 && !actor.Statuses.Contains(status),
            "Restored cure lost its temporary properties, invocation count or remaining cast duration.");
        var legacy=catalog.CreateStatus(60280,1,1,12,90010);
        legacy.Components.HitDmgAddPer=new FightBattleStatusHitDmgAddPerComponentState {
            InvokeCount=4,ConditionProgress=1,DurationSkillCount=2,LastResetRound=1};
        actor.Statuses.Add(legacy);
        var legacyApply=catalog.CreateStatus(60230,1,1,13,90010);
        legacyApply.Components.HitApplyDamage=new FightBattleStatusHitApplyDamageComponentState();
        actor.Statuses.Add(legacyApply);
        var legacyBurn=catalog.CreateStatus(25098,1,1,14,12040);
        legacyBurn.RoundEndEntityCount=null;legacyBurn.RoundEndRulesSupported=false;
        legacyBurn.RoundEndTriggeredCount=4;
        legacyBurn.Components.HitApplyDamage.EntityCountScale=null;
        actor.Statuses.Add(legacyBurn);
        cp=FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(cp));
        runner=FightBattleExecutor.Restore(cp,formula,new FightDamageSettlement(),catalog,items);
        var restored=cp.Units.Find(u=>u.Id==1).Statuses.Find(s=>s.ClassId==60280).Components.HitDmgAddPer;
        Check(restored.DefinitionResolved && restored.TargetActionTypes.Contains(52) && restored.TargetActionTypes.Contains(57)
            && restored.InvokeCount==4 && restored.ConditionProgress==1 && restored.DurationSkillCount==2
            && restored.LastResetRound==1,"Legacy definition recovery lost official filters or live counters.");
        var restoredApply=cp.Units.Find(u=>u.Id==1).Statuses.Find(s=>s.ClassId==60230).Components.HitApplyDamage;
        Check(restoredApply.DefinitionResolved && restoredApply.IsCheckTargetShield
            && restoredApply.AddPercentPropertyId==catalog.PropertyType("StatusDmgAddPer"),
            "Legacy conditional apply recovery lost its shield gate or original property selector.");
        var restoredBurn=cp.Units.Find(u=>u.Id==1).Statuses.Find(s=>s.ClassId==25098);
        Check(restoredBurn.RoundEndRulesSupported && restoredBurn.RoundEndEntityCount.Range.Count==13
            && restoredBurn.RoundEndTriggeredCount==4
            && restoredBurn.Components.HitApplyDamage.EntityCountScale.ResultOffset==-1,
            "Legacy burning grid definition recovery lost its range, offset or live invocation count.");
    }
    static void NextCast(FightBattleExecutor runner)
    {
        for(int i=0;i<20;i++) if(runner.Step(0,runner.State.Counter+1).Event.SkillCasts.Exists(c=>c.SkillEntityId==90010)) return;
        throw new Exception("Conditional hit fixture did not cast its healing skill.");
    }
}
