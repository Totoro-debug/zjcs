using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

public static class BattleStatusLogicHarness
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static FightBattleUnitState Unit(int id, int side)
    {
        return new FightBattleUnitState
        {
            Id = id, Side = side, CharacterType = side == 1 ? 1 : 2,
            Hp = 1000000, MaxHp = 1000000, LastMaxHp = 1000000,
            X = id == 1 ? 4 : 8, Y = 4,
            Turn = new FightTurnUnit { Id = id, Hp = 1000000, Speed = 100 }
        };
    }

    public static void Run(FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        // 24669 contains the original FightOnStatusSelfInitTriggerCfg and initializes its
        // persistent AbsorbedCount blackboard value.  No client callback exists for this
        // component; the normal executor startup path must still run it once.
        var player = Unit(1, 1);
        var enemy = Unit(2, 2);
        var logic = catalog.CreateStatus(24669, 1, 1, 24669001, 0, 0, 5, 20, 1);
        player.Statuses.Add(logic);
        var checkpoint = new FightBattleCheckpoint
        {
            StageId = 1001, RandomSeed = 31,
            Grid = new FightBattleGrid { Width = 20, Height = 20 },
            ResultRules = new FightBattleResultRules()
        };
        checkpoint.Units.Add(player); checkpoint.Units.Add(enemy);
        var runner = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(),
            catalog, items);
        runner.Step(0, 1);

        Check(logic.HasLogicComponent && logic.LogicDefinitionResolved
            && !string.IsNullOrEmpty(logic.LogicDefinitionJson),
            "FightStatusLogicComponent was not retained from the official definition.");
        Check(logic.LogicSelfInitialized && logic.LogicStartInitialized,
            "Status logic startup was not dispatched exactly through the executor lifecycle.");
        var blackboard = logic.LogicBlackboard.SingleOrDefault(value => value.KeyId == 619624960);
        Check(blackboard != null && blackboard.LongValue == -1,
            "Official FightOnStatusSelfInitTriggerCfg did not initialize AbsorbedCount.");

        var restored = FightBattleCheckpointSerializer.Load(
            FightBattleCheckpointSerializer.Save(checkpoint));
        var restoredLogic = restored.Units.Single(value => value.Id == 1).Statuses
            .Single(value => value.ClassId == 24669);
        Check(restoredLogic.LogicDefinitionResolved && restoredLogic.LogicSelfInitialized
            && restoredLogic.LogicStartInitialized && restoredLogic.LogicBlackboard.Count == 1
            && restoredLogic.LogicBlackboard[0].LongValue == -1,
            "Status logic definition or persistent blackboard was lost in the checkpoint.");
        var restoredRunner = FightBattleExecutor.Restore(restored, formula,
            new FightDamageSettlement(), catalog, items);
        Check(restoredRunner != null && restoredLogic.LogicTriggeredCount >= 0,
            "Status logic checkpoint could not be restored by the official executor.");
        FilterBoundaries(catalog);
        EntityPortBoundaries(catalog);
        ChangeStatusStacks(catalog);
        OriginalStackStartup(catalog);
        AbsorbStatusStacks(catalog);
        AbsorbExecutor(catalog, items, formula);
        SkillDefinitionCheckpoints(catalog, items, formula);
    }

    private static void ChangeStatusStacks(FightOfficialCombatCatalog catalog)
    {
        var owner = Unit(1, 1);
        var main = catalog.CreateStatus(24723, 1, 9, 71, 13820, 10594, 5, 20, 1);
        var other = catalog.CreateStatus(24723, 1, 8, 72, 13820, 10594, 5, 20, 1);
        Check(main.IsOpenStack, "Original 24723 must be stackable for the self-instance test.");
        main.StackCount = 3; other.StackCount = 5;
        main.LogicBlackboard.Add(new FightBattleLogicBlackboardValue { KeyId = 9002, LongValue = -1 });
        owner.Statuses.Add(main); owner.Statuses.Add(other);
        const string selfFilter = "{\"_type\":\"LogicFilter\",\"LogicType\":0,\"SubFilters\":["
            + "{\"_type\":\"IteratingAgentStatusClassIdFilter\",\"StatusClassId\":24723},"
            + "{\"_type\":\"EntityKeyEqualsEntityFilter\",\"LhsEntityKey\":{\"Id\":1072},"
            + "\"RhsEntityKey\":{\"Id\":2}}]}";
        RunStack(main, owner, catalog, selfFilter, 0, 1);
        Check(main.StackCount == 2 && other.StackCount == 5 && main.LogicRulesSupported,
            "24723's original self-instance filter changed another source or lost the main status binding.");

        var a = catalog.CreateStatus(24752, 1, 9, 73);
        var b = catalog.CreateStatus(24752, 1, 8, 74);
        Check(a.IsOpenStack && b.IsOpenStack, "Original 24752 must support layer arithmetic.");
        a.StackCount = 4; b.StackCount = 2;
        owner.Statuses.Add(a); owner.Statuses.Add(b);
        var observer = catalog.CreateStatus(24669, 1, 1, 75);
        observer.HasConsumeStackCount = true;
        observer.ConsumeStackCountRulesSupported = true;
        observer.ConsumeStackCountTargetStatusId = 24752;
        observer.ConsumeStackCountThreshold = 5;
        observer.ConsumeStackCountStatuses.Clear();
        owner.Statuses.Add(observer);
        main.LogicBlackboard.Single(v => v.KeyId == 9002).LongValue = 4;
        int ended = 0;
        owner.StatusEnded += (unit, status) => { if (status.ClassId == 24752) ended++; };
        RunStack(main, owner, catalog, StatusFilter(24752), 0, -1, consume: true);
        Check(!owner.Statuses.Contains(a) && !owner.Statuses.Contains(b) && ended == 2
            && observer.ConsumeStackCountProgress == 1 && other.StackCount == 5,
            "Multiplier, zero-layer end, all-instance snapshot or actual consumed-layer notification failed.");

        // No matching status: create the requested layers before the lifecycle start callback.
        int layersAtStart = 0;
        owner.StatusStarted += (unit, status) => {
            if (status.ClassId == 24752) layersAtStart = status.StackCount;
        };
        main.LogicBlackboard.Single(v => v.KeyId == 9002).LongValue = 2;
        RunStack(main, owner, catalog, StatusFilter(24752), 24752, 1);
        var created = owner.Statuses.Single(v => v.ClassId == 24752);
        Check(created.StackCount == 2 && layersAtStart == 2 && created.CreatorId == 9
            && created.RootSkillId == 13820 && created.ApplyingSkillItemId == 10594
            && created.SourceSkillRank == 5 && created.SourceSkillLevel == 20
            && created.InstanceId > observer.InstanceId,
            "Unmatched positive creation lost requested layers, identity, source or skill cultivation.");
        RunStack(main, owner, catalog, StatusFilter(24752), 24752, 1, consume: true);
        Check(owner.Statuses.Count(v => v.ClassId == 24752) == 1 && created.StackCount == 4
            && observer.ConsumeStackCountProgress == 1,
            "A matched status was recreated or a positive change emitted consumption.");
        created.StackCount = created.MaxStackedCount;
        RunStack(main, owner, catalog, StatusFilter(24752), 24752, 10);
        Check(created.StackCount == created.MaxStackedCount,
            "Original maximum stack limit was bypassed.");
        FightStatusLifecycle.End(owner, created);
        RunStack(main, owner, catalog, StatusFilter(24752), 24752, 1, noSource: true);
        created = owner.Statuses.Single(v => v.ClassId == 24752);
        Check(created.StackCount == 2 && created.CreatorId == 0 && created.RootSkillId == 0
            && created.ApplyingSkillItemId == 0 && created.SourceProperties == null,
            "UseDefaultSourceInfoOnCreate retained the main status's source information.");

        const string tail = ",{\"_type\":\"FightHandleCfgCounterAdd\","
            + "\"CounterBBKey\":{\"Id\":9003},\"DeltaValue\":1}";
        RunStack(main, owner, catalog, StatusFilter(24752), 24752, -1, port: 1079, tail: tail);
        Check(created.StackCount == 2 && !main.LogicRulesSupported
            && !main.LogicBlackboard.Any(v => v.KeyId == 9003),
            "Unbound PetEntity port modified the owner or executed dependent handles.");
        RunStack(main, owner, catalog, "{\"_type\":\"LogicFilter\",\"LogicType\":1,\"SubFilters\":["
            + StatusFilter(24752) + ",{\"_type\":\"UnknownServerFilter\"}]}",
            24752, -1, tail: tail);
        Check(created.StackCount == 2 && !main.LogicRulesSupported
            && !main.LogicBlackboard.Any(v => v.KeyId == 9003),
            "Partially supported OR filter caused a partial mutation or ran its dependent tail.");
        RunStack(main, owner, catalog, "{\"_type\":\"EntityClassIdFilter\","
            + "\"EntityKey\":{\"Id\":1072},\"ClassId\":24752}", 0, -1);
        Check(!owner.Statuses.Contains(created) && main.LogicRulesSupported,
            "Iterating status ClassId was interpreted as the main unit's monster identity.");
        RunStack(main, owner, catalog, StatusFilter(24752), 24752, -1);
        main.LogicBlackboard.Single(v => v.KeyId == 9002).LongValue = 0;
        RunStack(main, owner, catalog, StatusFilter(24752), 24752, 1);
        Check(!owner.Statuses.Any(v => v.ClassId == 24752),
            "Zero or negative unmatched changes created a status.");
        owner.Statuses.Clear(); owner.Statuses.Add(null);
        main.LogicBlackboard.Single(v => v.KeyId == 9002).LongValue = 2;
        RunStack(main, owner, catalog, "{\"_type\":\"UnknownServerFilter\"}", 24752, 1, tail: tail);
        Check(owner.Statuses.Count == 1 && !main.LogicRulesSupported,
            "An empty status collection bypassed an unknown filter and created a status.");
    }

    private static string StatusFilter(int classId)
        => "{\"_type\":\"IteratingAgentStatusClassIdFilter\",\"StatusClassId\":" + classId + "}";

    private static void AbsorbStatusStacks(FightOfficialCombatCatalog catalog)
    {
        var owner = Unit(1, 1); var target = Unit(2, 2);
        var main = catalog.CreateStatus(24774, 1, 1, 80, 64280);
        owner.Statuses.Add(main);
        var skill = catalog.CreateSkill(64280, 0, 0, 1, 1, 0, null);
        FightStatusLogicRuntime.Apply(owner, main, FightStatusLogicEvent.StatusSelfInit,
            owner, target, null, null, new[] { owner, target }, catalog, new FightRandomStream(31));
        var energy = owner.Statuses.Single(v => v.ClassId == 24752);
        energy.StackCount = 30;
        var own = catalog.CreateStatus(24750, 1, 1, 81); own.StackCount = 2; owner.Statuses.Add(own);
        var a = catalog.CreateStatus(24750, 2, 1, 82); a.StackCount = 3; target.Statuses.Add(a);
        var b = catalog.CreateStatus(24750, 2, 9, 83); b.StackCount = 4; target.Statuses.Add(b);
        var observer = catalog.CreateStatus(24669, 2, 2, 84);
        observer.HasConsumeStackCount = observer.ConsumeStackCountRulesSupported = true;
        observer.ConsumeStackCountTargetStatusId = 24750;
        observer.ConsumeStackCountThreshold = 10;
        observer.ConsumeStackCountStatuses.Clear(); target.Statuses.Add(observer);
        int ended = 0;
        target.StatusEnded += (unit, status) => {
            if (status.ClassId != 24750) return;
            ended++;
            // The snapshot must retain B's four layers even if A's end callback changes B.
            if (ReferenceEquals(status, a)) b.StackCount = 1;
        };
        FightStatusLogicRuntime.Apply(owner, main, FightStatusLogicEvent.DoDamage,
            owner, target, skill, FightBattleHitCollector.CollectAll(skill)[0], new[] { owner, target }, catalog, new FightRandomStream(31));
        Check(energy.StackCount == 100 && ended == 2 && !target.Statuses.Contains(a)
            && !target.Statuses.Contains(b) && owner.Statuses.Contains(own)
            && !main.LogicBlackboard.Any(v => v.KeyId == 619624960)
            && observer.ConsumeStackCountProgress == 0 && main.LogicRulesSupported,
            "Original 24774 failed target-only snapshot absorption, x10 capped gain or emitted consumption.");
        FightStatusLogicRuntime.Apply(owner, main, FightStatusLogicEvent.DoDamage,
            owner, target, skill, FightBattleHitCollector.CollectAll(skill)[0], new[] { owner, target }, catalog, new FightRandomStream(31));
        Check(energy.StackCount == 100
            && !main.LogicBlackboard.Any(v => v.KeyId == 619624960),
            "No-match absorption reused a stale output count.");

        target.Statuses.Add(catalog.CreateStatus(24750, 2, 1, 85));
        var mismatch = catalog.CreateSkill(12000, 0, 0, 1, 1, 0, null);
        mismatch.RootEntityId = 64280;
        FightStatusLogicRuntime.Apply(owner, main, FightStatusLogicEvent.DoDamage,
            owner, target, mismatch, FightBattleHitCollector.CollectAll(mismatch)[0], new[] { owner, target }, catalog, new FightRandomStream(31));
        Check(target.Statuses.Any(v => v.ClassId == 24750),
            "The skill condition used the root skill instead of this hit's actual skill entity.");

        string originalGraph = main.LogicDefinitionJson;
        const string absorb = "{\"_type\":\"FightHandleCfgAbsorbStatusStack\","
            + "\"TargetEntityKey\":{\"Id\":1022},\"FilterCondition\":FILTER,"
            + "\"RemoveStatus\":false,\"OutputCountKey\":{\"Id\":7001,\"IsPersistentBlackboard\":true}}";
        RunAbsorbFlow(main, owner, target, catalog, skill, absorb.Replace("FILTER", StatusFilter(24750)));
        Check(target.Statuses.Any(v => v.ClassId == 24750)
            && main.LogicBlackboard.Single(v => v.KeyId == 7001).LongValue == 1,
            "RemoveStatus=false removed a status or failed to count its layers.");
        string failedTail = ",{\"_type\":\"FightHandleCfgCounterAdd\","
            + "\"CounterBBKey\":{\"Id\":7002,\"IsPersistentBlackboard\":true},\"DeltaValue\":1}";
        RunAbsorbFlow(main, owner, target, catalog, skill, absorb.Replace("false", "true")
            .Replace("FILTER", "{\"_type\":\"LogicFilter\",\"LogicType\":1,\"SubFilters\":["
                + StatusFilter(24750) + ",{\"_type\":\"UnknownServerFilter\"}]}") + failedTail);
        Check(target.Statuses.Any(v => v.ClassId == 24750) && !main.LogicRulesSupported
            && main.LogicBlackboard.Single(v => v.KeyId == 7001).LongValue == 1
            && !main.LogicBlackboard.Any(v => v.KeyId == 7002),
            "Unsupported absorption filter changed statuses/output or ran the dependent tail.");
        RunAbsorbFlow(main, owner, target, catalog, skill, absorb.Replace("FILTER", StatusFilter(24750))
            .Replace("1022", "1079") + failedTail);
        Check(!main.LogicRulesSupported && target.Statuses.Any(v => v.ClassId == 24750)
            && !main.LogicBlackboard.Any(v => v.KeyId == 7002),
            "Unbound absorption target fell back to the owner or continued the flow.");
        RunAbsorbFlow(main, owner, target, catalog, skill, absorb.Replace("FILTER", StatusFilter(24750))
            .Replace("false", "true") + failedTail,
            "{\"_type\":\"LogicNotFilter\",\"TargetFilter\":{\"_type\":\"EntityHpPercentFilter\","
                + "\"EntityKey\":{\"Id\":1079},\"HpPercent\":1,\"Relation\":4}}");
        Check(!main.LogicRulesSupported && target.Statuses.Any(v => v.ClassId == 24750)
            && !main.LogicBlackboard.Any(v => v.KeyId == 7002),
            "An unbound HP port in the outer NOT condition allowed absorption.");
        main.LogicDefinitionJson = originalGraph; main.LogicRulesSupported = true;
        skill.RootEntityId = 12000;
        energy.StackCount = 20;
        FightStatusLogicRuntime.Apply(owner, main, FightStatusLogicEvent.DoDamage,
            owner, target, skill, FightBattleHitCollector.CollectAll(skill)[0], new[] { owner, target }, catalog, new FightRandomStream(31));
        Check(energy.StackCount == 30 && !target.Statuses.Any(v => v.ClassId == 24750),
            "Actual 64280 hit was rejected because its root skill differed.");
    }

    private static void RunAbsorbFlow(FightBattleStatusState main, FightBattleUnitState owner,
        FightBattleUnitState target, FightOfficialCombatCatalog catalog,
        FightBattleSkillDefinition skill, string handles, string filter = "null")
    {
        main.LogicRulesSupported = true;
        main.LogicDefinitionJson = "{\"Triggers\":[{\"_type\":\"FightOnDoDamageTriggerCfg\","
            + "\"Flow\":{\"FilterCondition\":" + filter + ",\"HandleCfgs\":[" + handles + "]}}]}";
        FightStatusLogicRuntime.Apply(owner, main, FightStatusLogicEvent.DoDamage,
            owner, target, skill, FightBattleHitCollector.CollectAll(skill)[0], new[] { owner, target }, catalog, new FightRandomStream(31));
    }

    private static void AbsorbExecutor(FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        var owner = Unit(1, 1); var target = Unit(2, 2);
        owner.X = 4; target.X = 6; owner.Y = target.Y = 4;
        owner.Turn.Speed = 100; target.Turn.Speed = 1;
        foreach (var unit in new[] { owner, target })
        {
            unit.Level = unit.SubRank = 1;
            unit.Properties.Add(new FightPropertyValue { Id = catalog.PropertyType("MaxHp"), Value = 1000000 });
            unit.Properties.Add(new FightPropertyValue { Id = catalog.PropertyType("Attack"), Value = 1000 });
            unit.BaseProperties = unit.Properties.Select(v => new FightPropertyValue { Id = v.Id, Value = v.Value }).ToList();
        }
        var main = catalog.CreateStatus(24774, 1, 1, 90, 64280);
        owner.Statuses.Add(main);
        var skill = catalog.CreateSkill(64280, 0, 0, 1, 1, 0, null);
        owner.Skills.Add(skill);
        owner.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
        var checkpoint = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 31,
            Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 },
            ResultRules = new FightBattleResultRules() };
        checkpoint.Units.Add(owner); checkpoint.Units.Add(target);
        var runner = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
        runner.Step(0, 1);
        owner.Statuses.Single(v => v.ClassId == 24752).StackCount = 20;
        var absorbed = catalog.CreateStatus(24750, 2, 1, 91); absorbed.StackCount = 3;
        target.Statuses.Add(absorbed);
        bool hit = false;
        for (int i = 0; i < 8 && !hit; i++)
            hit |= runner.Step(checkpoint.Sequence, checkpoint.Counter + 1).Event.DamageEvents
                .Any(v => v.SkillEntityId == 64280 && v.TargetId == 2);
        Check(hit && !target.Statuses.Contains(absorbed)
            && owner.Statuses.Single(v => v.ClassId == 24752).StackCount == 50,
            "Production 64280 absorption failed: hit=" + hit + "; absorbed=" + !target.Statuses.Contains(absorbed)
                + "; energy=" + owner.Statuses.Single(v => v.ClassId == 24752).StackCount
                + "; casts=" + string.Join(",", checkpoint.Receipts.SelectMany(v => v.Event.SkillCasts)
                    .Select(v => v.SkillEntityId)) + "; positions=" + owner.X + "," + owner.Y
                + "/" + target.X + "," + target.Y);
        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        Check(restored.Units[0].Statuses.Single(v => v.ClassId == 24752).StackCount == 50,
            "Production absorption result was lost during checkpoint restore.");
    }

    private static void OriginalStackStartup(FightOfficialCombatCatalog catalog)
    {
        var owner = Unit(1, 1);
        foreach (int id in new[] { 24745, 24774 })
        {
            var source = catalog.CreateStatus(id, 1, 9, id, 64280, 10594, 5, 20, 1);
            owner.Statuses.Add(source);
            FightStatusLogicRuntime.Apply(owner, source, FightStatusLogicEvent.StatusSelfInit,
                owner, owner, null, null, new[] { owner }, catalog, new FightRandomStream(31));
            int createdId = id == 24745 ? 24748 : 24752;
            var created = owner.Statuses.Single(v => v.ClassId == createdId);
            Check(created.StackCount == (id == 24745 ? 60 : 100)
                && created.CreatorId == 9 && created.RootSkillId == 64280 && source.LogicRulesSupported,
                "Untouched official startup graph failed its blackboard -> filtered creation: " + id);
        }
        var checkpoint = new FightBattleCheckpoint {
            StageId = 1001, RandomSeed = 31,
            Grid = new FightBattleGrid { Width = 20, Height = 20 },
            ResultRules = new FightBattleResultRules()
        };
        checkpoint.Units.Add(owner);
        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        Check(restored.Units[0].Statuses.Single(v => v.ClassId == 24748).StackCount == 60
            && restored.Units[0].Statuses.Single(v => v.ClassId == 24752).StackCount == 100,
            "Created status layers were lost in the checkpoint.");
        var main = owner.Statuses.Single(v => v.ClassId == 24745);
        RunFlow(main, owner, catalog, "null", "{\"_type\":\"FightHandleRemoveStatusCfg\","
            + "\"TargetEntityKey\":{\"Id\":1},\"FilterCondition\":" + StatusFilter(24748) + "}");
        Check(owner.Statuses.All(v => v.ClassId != 24748)
            && owner.Statuses.Any(v => v.ClassId == 24752) && owner.Statuses.Contains(main),
            "Binding status iteration broke the existing filtered removal handler.");
    }

    private static void RunStack(FightBattleStatusState main, FightBattleUnitState owner,
        FightOfficialCombatCatalog catalog, string filter, int create, int multiplier,
        bool consume = false, bool noSource = false, int port = 1, string tail = "")
    {
        RunFlow(main, owner, catalog, "null", "{\"_type\":\"FightHandleCfgChangeStatusStack\","
            + "\"TargetEntityKey\":{\"Id\":" + port + "},\"FilterCondition\":" + filter
            + ",\"ChangeValueKey\":{\"Id\":9002,\"IsPersistentBlackboard\":true},\"CreateStatusClassId\":" + create
            + ",\"Multiplier\":" + multiplier + ",\"InvokeConsumeEvent\":"
            + (consume ? "true" : "false") + ",\"UseDefaultSourceInfoOnCreate\":"
            + (noSource ? "true" : "false") + "}" + tail);
    }

    private static void SkillDefinitionCheckpoints(FightOfficialCombatCatalog catalog,
        FightOfficialSkillItemCatalog items, FightDamageFormula formula)
    {
        int[] ids = { 13640, 13730, 13740, 13820, 13830, 13840, 13870, 13960,
            13970, 50210, 50213, 50230, 54370, 54373, 64450 };
        var player = Unit(1, 1);
        foreach (int id in ids)
        {
            // 50213 is officially a logic-only skill with an empty HitList. Its execution
            // remains unavailable until OnSkillStart is implemented; retain its graph here.
            var skill = id == 50213 ? new FightBattleSkillDefinition { EntityId = id }
                : catalog.CreateSkill(id, 0, 0, 1, 1, 0, new List<FightPropertyValue>());
            catalog.RestoreSkillLogicDefinition(skill);
            Check(skill.HasLogicComponent && skill.LogicDefinitionResolved,
                "Original skill logic was discarded: " + id);
            var graph = (Dictionary<string, object>)OfficialJson.Parse(skill.LogicDefinitionJson);
            Check(graph.Count == 4 && graph.ContainsKey("OnDoHit")
                && graph.ContainsKey("OnDoHitTransient") && graph.ContainsKey("OnSkillStart")
                && graph.ContainsKey("OnHitFinish"), "Original skill event fields were changed: " + id);
            player.Skills.Add(skill);
        }
        var checkpoint = new FightBattleCheckpoint
        {
            StageId = 1001, RandomSeed = 31,
            Grid = new FightBattleGrid { Width = 20, Height = 20 },
            ResultRules = new FightBattleResultRules()
        };
        checkpoint.Units.Add(player); checkpoint.Units.Add(Unit(2, 2));
        var restored = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
        for (int i = 0; i < ids.Length; i++)
            Check(restored.Units[0].Skills[i].LogicDefinitionJson == player.Skills[i].LogicDefinitionJson,
                "Checkpoint changed polymorphic skill logic: " + ids[i]);

        // Simulate an old checkpoint with no logic fields, through the production restore path.
        string original = restored.Units[0].Skills[0].LogicDefinitionJson;
        restored.Units[0].Skills[0].HasLogicComponent = false;
        restored.Units[0].Skills[0].LogicDefinitionResolved = false;
        restored.Units[0].Skills[0].LogicDefinitionJson = null;
        restored.Units[0].Skills[0].FalloffCastId = 731;
        FightBattleExecutor.Restore(restored, formula, new FightDamageSettlement(), catalog, items);
        Check(restored.Units[0].Skills[0].LogicDefinitionJson == original
            && restored.Units[0].Skills[0].HasLogicComponent
            && restored.Units[0].Skills[0].FalloffCastId == 731,
            "Legacy skill logic restoration lost configuration or modified cast state.");
        var plain = catalog.CreateSkill(12000, 0, 0, 1, 1, 0, new List<FightPropertyValue>());
        Check(plain.LogicDefinitionResolved && !plain.HasLogicComponent && plain.LogicDefinitionJson == null,
            "A skill without an original logic component acquired a fabricated graph.");
    }

    private static void FilterBoundaries(FightOfficialCombatCatalog catalog)
    {
        var owner = Unit(1, 1);
        var status = catalog.CreateStatus(24669, 1, 1, 77, 0, 0, 1, 1, 1);
        const string counter = "{\"_type\":\"FightHandleCfgCounterAdd\","
            + "\"CounterBBKey\":{\"Id\":9001,\"IsPersistentBlackboard\":true},\"DeltaValue\":1}";
        string[] rejectedFilters = {
            "{\"_type\":\"LogicNotFilter\",\"TargetFilter\":{\"_type\":\"UnknownServerFilter\"}}",
            "{\"_type\":\"LogicFilter\",\"LogicType\":1,\"SubFilters\":["
                + "{\"_type\":\"CounterEqualFilter\",\"CounterBBKey\":{\"Id\":9001,\"IsPersistentBlackboard\":true},\"Value\":0},"
                + "{\"_type\":\"UnknownServerFilter\"}]}",
            "{\"_type\":\"LogicNotFilter\"}",
            "{\"_type\":\"LogicFilter\",\"LogicType\":7,\"SubFilters\":[]}",
            "{\"_type\":\"LogicFilter\",\"LogicType\":0,\"SubFilters\":[null]}"
        };
        foreach (string filter in rejectedFilters)
        {
            RunFlow(status, owner, catalog, filter, counter);
            Check(status.LogicBlackboard.Count == 0 && !status.LogicRulesSupported,
                "Unknown/malformed composed condition must not execute its dependent handle.");
        }

        // Use the actual official TargetFilter and LhsBBKey field names. An unrecognized
        // operand must not turn a NOT into true or a comparison into an implicit zero read.
        status.LogicBlackboard.Add(new FightBattleLogicBlackboardValue { KeyId = 9002, LongValue = 3 });
        RunFlow(status, owner, catalog,
            "{\"_type\":\"LogicNotFilter\",\"TargetFilter\":{\"_type\":\"BBIntEqualFilter\","
            + "\"LhsBBKey\":{\"Id\":9002,\"IsPersistentBlackboard\":true},\"Value\":3}}", counter);
        Check(!status.LogicBlackboard.Any(value => value.KeyId == 9001)
            && status.LogicRulesSupported, "Known NOT condition did not use its official operand.");
        RunFlow(status, owner, catalog,
            "{\"_type\":\"BBIntEqualFilter\",\"LhsBBKey\":{\"Id\":9002,\"IsPersistentBlackboard\":true},\"Value\":3}", counter);
        Check(status.LogicBlackboard.Single(value => value.KeyId == 9001).LongValue == 1,
            "Official BBInt compare did not read LhsBBKey.");
        RunFlow(status, owner, catalog, "null",
            "{\"_type\":\"UnknownServerHandle\"}," + counter);
        Check(status.LogicBlackboard.Single(value => value.KeyId == 9001).LongValue == 1
            && !status.LogicRulesSupported, "A missing handle allowed its dependent tail to run.");

        owner.MonsterTemplateId = 3201;
        RunFlow(status, owner, catalog,
            "{\"_type\":\"EntityClassIdFilter\",\"EntityKey\":{\"Id\":1},\"ClassId\":3202}", counter);
        Check(status.LogicBlackboard.Single(value => value.KeyId == 9001).LongValue == 1,
            "A different configured ClassId matched an unrelated zero-valued identity field.");
        RunFlow(status, owner, catalog,
            "{\"_type\":\"EntityClassIdFilter\",\"EntityKey\":{\"Id\":1},\"ClassId\":3201}", counter);
        Check(status.LogicBlackboard.Single(value => value.KeyId == 9001).LongValue == 2,
            "EntityClassIdFilter did not read the official ClassId field.");
        RunFlow(status, owner, catalog,
            "{\"_type\":\"LogicNotFilter\",\"TargetFilter\":{\"_type\":\"EntityClassIdFilter\","
            + "\"EntityKey\":{\"Id\":1072},\"ClassId\":24772}}", counter);
        Check(status.LogicBlackboard.Single(value => value.KeyId == 9001).LongValue == 2
            && !status.LogicRulesSupported,
            "Official IteratingAgentStatusEntity port must fail closed until status iteration is bound.");
    }

    private static void EntityPortBoundaries(FightOfficialCombatCatalog catalog)
    {
        const string tail = ",{\"_type\":\"FightHandleCfgCounterAdd\","
            + "\"CounterBBKey\":{\"Id\":9901,\"IsPersistentBlackboard\":true},\"DeltaValue\":1}";
        const string skill = "{\"_type\":\"FightHandleCfgTriggerSkills\","
            + "\"SourceEntityKey\":{\"Id\":SOURCE},\"TargetEntityKey\":{\"Id\":TARGET},"
            + "\"TriggerSkillCfgs\":[{\"SkillId\":12000,\"BasePercent\":1}]}";
        const string apply = "{\"_type\":\"FightHandleCfgTriggerStatuss\","
            + "\"SourceEntityKey\":{\"Id\":SOURCE},\"TargetEntityKey\":{\"Id\":TARGET},"
            + "\"TriggerStatusCfgs\":[{\"StatusId\":24823,\"BasePercent\":1}]}";
        const string remove = "{\"_type\":\"FightHandleRemoveStatusCfg\","
            + "\"TargetEntityKey\":{\"Id\":TARGET},\"FilterCondition\":FILTER}";
        var owner = Unit(1, 1);
        var main = catalog.CreateStatus(24669, 1, 1, 97);
        var victim = catalog.CreateStatus(24750, 1, 1, 98);
        owner.Statuses.Add(main); owner.Statuses.Add(victim);
        foreach (int port in new[] { 0, 2, 3, 1022, 1023, 1079, 175187006, 999999 })
            foreach (string handle in new[] {
                skill.Replace("SOURCE", port.ToString()).Replace("TARGET", "1"),
                skill.Replace("SOURCE", "1").Replace("TARGET", port.ToString()),
                apply.Replace("SOURCE", port.ToString()).Replace("TARGET", "1"),
                apply.Replace("SOURCE", "1").Replace("TARGET", port.ToString()),
                remove.Replace("TARGET", port.ToString()).Replace("FILTER", "null") })
            {
                main.LogicBlackboard.Clear();
                var random = new FightRandomStream(31);
                var casts = RunFlow(main, owner, catalog, "null", handle + tail, random);
                Check(casts.Count == 0 && owner.Statuses.Count == 2 && owner.Statuses.Contains(victim)
                    && !main.LogicRulesSupported && main.LogicBlackboard.Count == 0 && random.Seed == 31,
                    "Unbound/non-unit entity port consumed randomness, changed a unit or ran its dependent tail: " + port);
            }
        var positionRandom = new FightRandomStream(31);
        RunFlow(main, owner, catalog, "null", skill.Replace("SOURCE", "1").Replace("TARGET", "1")
            .Replace("\"TriggerSkillCfgs\"", "\"UseDamagePos\":true,\"DamagePosEntityKey\":{\"Id\":1023},\"TriggerSkillCfgs\"")
            + tail, positionRandom);
        Check(!main.LogicRulesSupported && main.LogicBlackboard.Count == 0 && positionRandom.Seed == 31,
            "Unbound DamageResult.Pos was replaced by a unit position or continued the flow.");
        foreach (bool empty in new[] { false, true })
        {
            if (empty) owner.Statuses.Clear();
            RunFlow(main, owner, catalog, "null", remove.Replace("TARGET", "1")
                .Replace("FILTER", "{\"_type\":\"UnknownServerFilter\"}") + tail);
            Check(!main.LogicRulesSupported && main.LogicBlackboard.Count == 0
                && owner.Statuses.Count == (empty ? 0 : 2),
                "Unsupported removal filter continued a dependent flow, including an empty collection.");
        }
        owner.Statuses.Add(main); owner.Statuses.Add(victim);
        int ends = 0;
        owner.StatusEnded += (unit, ended) => {
            if (ended == victim) { ends++; main.LogicBlackboard[0].LongValue = 0; }
        };
        var valid = RunFlow(main, owner, catalog, "null",
            skill.Replace("SOURCE", "1").Replace("TARGET", "1") + tail);
        Check(valid.Count == 1 && valid[0].SourceUnitId == owner.Id && valid[0].TargetUnitId == owner.Id,
            "Explicit MainEntity skill ports no longer resolve to their owner.");
        RunFlow(main, owner, catalog, "null", apply.Replace("SOURCE", "1").Replace("TARGET", "1") + tail);
        Check(owner.Statuses.Any(value => value.ClassId == 24823), "Explicit MainEntity status ports failed.");
        RunFlow(main, owner, catalog, "null", remove.Replace("TARGET", "1")
            .Replace("FILTER", StatusFilter(24823)) + tail);
        Check(main.LogicRulesSupported && owner.Statuses.Count == 2 && owner.Statuses.Contains(victim)
            && main.LogicBlackboard.Single(value => value.KeyId == 9901).LongValue == 3,
            "Supported filtered removal changed other states or interrupted the dependent flow.");
        // The first end callback changes a value read by the next candidate's filter.
        owner.Statuses.Remove(main);
        var later = catalog.CreateStatus(24750, 1, 1, 99); owner.Statuses.Add(later);
        main.LogicBlackboard[0].LongValue = 1;
        RunFlow(main, owner, catalog, "null", remove.Replace("TARGET", "1")
            .Replace("FILTER", "{\"_type\":\"BBIntEqualFilter\",\"LhsBBKey\":{\"Id\":9901,"
                + "\"IsPersistentBlackboard\":true},\"Value\":1}"));
        Check(ends == 1 && !owner.Statuses.Contains(victim) && owner.Statuses.Contains(later),
            "Removal preflight changed the established filtering/end-callback interleaving.");
    }

    private static List<FightBattleStatusSkillInvocation> RunFlow(FightBattleStatusState status, FightBattleUnitState owner,
        FightOfficialCombatCatalog catalog, string filter, string handles, FightRandomStream random = null)
    {
        status.LogicRulesSupported = true;
        status.LogicDefinitionJson = "{\"Triggers\":[{\"_type\":\"FightOnStatusSelfInitTriggerCfg\","
            + "\"Flow\":{\"FilterCondition\":" + filter + ",\"HandleCfgs\":[" + handles + "]}}]}";
        return FightStatusLogicRuntime.Apply(owner, status, FightStatusLogicEvent.StatusSelfInit,
            owner, owner, null, null, new[] { owner }, catalog, random ?? new FightRandomStream(31));
    }
}
