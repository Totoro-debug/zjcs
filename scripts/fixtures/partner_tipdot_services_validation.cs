using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

public static class PartnerTipDotServicesValidation
{
    sealed class Subscription : IDisposable { public void Dispose() { } }
    static void Require(bool value, string message)
    { if (!value) throw new Exception("Saved partner tip services: " + message); }
    public static IEnumerator Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var gifts = OfficialPartnerGiftAssetCatalog.LoadDefault();
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var character = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"), read("Character/item-time-limits.json"),
            read("CharacterCreation/wardrobe.json"), read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"), catalog);
        var slots = new ProfileSlotCatalog(Path.Combine(work, "partner-tip-services"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var progression = new OfflineCharacterProgressionService(session, character, refreshDerivedProgress: false);
        var itemTypes = catalog.Rows("item").ToDictionary(row => Convert.ToInt32(row["ClassId"]), row => (ItemType)Convert.ToInt32(row["ItemType"]));
        bool limited = false;
        var player = new OfflinePartnerTipDotPlayer(session, catalog, gifts, function => function == 65 && limited,
            id => itemTypes.TryGetValue(id, out var type) ? type : (ItemType)character.ItemType(id));
        // This fixture exercises the actual profile graph. Achievement keys are
        // a required separate owner port; never fabricate them to run a graph.
        var services = new OfflinePartnerTipDotServices(session, catalog, gifts, player, progression,
            (id, starGod) => { throw new NotSupportedException("Achievement endpoint is outside the profile graph fixture."); },
            (error, type) => { throw new Exception("Original condition callback failed: " + type, error); });
        var expectedNpcIds = catalog.Rows("npc").Where(row => Convert.ToInt32(row["NpcType"]) != 2)
            .Select(row => Convert.ToInt32(row["Id"])).Where(id => id < 11015 || id > 11022).ToArray();
        Require(services.MapExploreNpcs().Select(npc => npc.Id).SequenceEqual(expectedNpcIds) &&
            services.MapExploreNpcs().Any(npc => npc.IsStarGodNpc), "original ignore-type semantics, order, star gods and closed range");
        Require(services.GetNpc(8401).GiftPreferenceDict.OrderBy(value => value.Key)
            .SequenceEqual(gifts.GetNpcInfo(8401).GiftPreferenceDict.OrderBy(value => value.Key)), "actual selected gift preferences");
        Require(services.GetNpc(-1) == null && services.GetNpcProfiles(-1) == null && services.GetNpcProfile(8401, 999) == null &&
            services.GetTalentGraph(8401) == null && services.GetTalentItem(8401) == null, "original absent parser defaults");
        var chapters = services.GetNpcProfiles(8401);
        Require(ReferenceEquals(chapters, services.GetNpcProfiles(8401)) &&
            ReferenceEquals(chapters[0], services.GetNpcProfile(8401, chapters[0].ProfileId)), "stable profile identity across parser operations");
        Require(services.GetNpcProfiles(11015) == null && services.GetNpcMeetStatus(player, 11015) == NpcMeetStatus.NotMeet,
            "confirmed closed NPC has no profile/meet entry");
        Require(!chapters[0].UnlockCondition.IsMet(player), "unmet NPC does not satisfy its profile condition");
        var profileRules = new PartnerProfileRules(catalog.Rows);
        var talentRules = new PartnerTalentRules(catalog.Rows, gifts.GetMaxLevel);
        var talentPlayer = new OfflinePartnerTalentPlayer(() => session.Save.Player, catalog);
        var rawConditions = catalog.Rows("npc_talent_tree_npc_map").ToDictionary(
            row => (Convert.ToInt32(row["NpcClassId"]), Convert.ToInt32(row["NodeId"])), row => Convert.ToInt32(row["UnlockCondition"]));
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "伙伴提示原配置验证"; value.Inventory.Clear();
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = 19 });
            foreach (int id in talentRules.Graphs.Keys)
                value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = id, Level = id == 10608 ? 49 : 100 });
            value.CharacterProperties.NpcTalentTrees.Add(new OfflineNpcTalentTree { NpcClassId = 10608, ActiveNodeIds = new List<int> { 1 } });
            value.Inventory.Add(new OfflineItemStack { ItemId = 42300, Count = 1 });
            value.Inventory.Add(new OfflineItemStack { ItemId = 1100, Count = 3 });
        }).IsApplied, "actual initial save with different NPC levels and activated sets");
        player.Synchronize();
        int talentNodes = 0;
        foreach (var entry in talentRules.Graphs)
        {
            var graph = services.GetTalentGraph(entry.Key);
            Require(ReferenceEquals(graph, services.GetTalentGraph(entry.Key)) && graph.Nodes.SequenceEqual(entry.Value.Nodes),
                "stable graph and original node enumeration");
            foreach (int nodeId in graph.Nodes)
            {
                var node = graph.GetNodeInfo(nodeId);
                Require(node.Condition.IsMet(player) == talentRules.IsConditionMet(rawConditions[(entry.Key, nodeId)], talentPlayer, entry.Key, nodeId) &&
                    services.GetTalentPointState(node, player.GetNpcTalentTreeUnlockedNodeIds(entry.Key)) ==
                        (PartnerTipDotTalentPointState)(int)talentRules.GetPointState(talentPlayer, entry.Value.GetNodeInfo(nodeId), player.GetNpcTalentTreeUnlockedNodeIds(entry.Key)),
                    "node condition/cost/adjacency uses its own actual NPC and node context: " + entry.Key + "/" + nodeId);
                talentNodes++;
            }
            Require(services.IsTalentTreeFunctionLimit(player, entry.Key) == talentRules.IsTalentTreeFunctionLimited(talentPlayer, entry.Key) &&
                services.IsTransformFunctionLimit(player, entry.Key) == talentRules.IsTransformFunctionLimited(talentPlayer, entry.Key) &&
                services.IsGiftCanTransformTalentPoint(player, entry.Key) == talentRules.CanTransformGift(talentPlayer, entry.Key), "actual talent gate and gift conversion bindings");
        }
        Require(talentNodes == 252 && services.IsTalentTreeFunctionLimit(player, 10608) && !services.IsTalentTreeFunctionLimit(player, 10708),
            "all selected talent nodes and independent friendship gates");
        Require(services.GetNpcMeetStatus(player, 8401) == progression.NpcMeetStatusOf(8401), "original current-rule meet service");
        int conditionCalls = 0;
        Action conditionChanged = () => conditionCalls++;
        services.RegisterCondition(services.GetTalentItem(10608).TalentTreeUnlockCondition, conditionChanged);
        var graphCatalog = OfficialPartnerTipDotAssetCatalog.LoadDefault();
        var graphHandler = new TipDotGraphHandler(graphCatalog.LoadGraph);
        var bans = new TipDotBanManager(Array.Empty<ITipDotBanStrategy>());
        TipDotNodeManager manager = null;
        var factory = new TipDotNodeFactory(graphHandler.GetNodeGraph, node => {
            node.NodeManager = manager; node.TipDotBanManager = bans;
            if (node is IPartnerTipDotNode partner) partner.Services = services;
        });
        var bindings = new PartnerTipDotRuntimeBindings(() => true, graphCatalog.GetAllGraphs, graphCatalog.GetGraphType,
            (add, remove) => new Subscription(), callback => new Subscription());
        manager = new TipDotNodeManager(new TipDotNodeDataRepository(), factory, graphHandler, bindings);
        try
        {
            manager.Init();
            var native = graphCatalog.LoadGraph("NpcProfileItem");
            var definition = native.nodes.Cast<TipDotGraphNode>().Single(node => node.TipDotNode is TipDotNpcProfileCanUnlockConditionNode);
            var key = new TipDotNodeKey(native.name, definition.TipDotNodeId.ToString(), new NpcProfileNodeInstanceInfo(8401, 1));
            var runtime = (TipDotNpcProfileCanUnlockConditionNode)manager.FindNodeOrCreate(key);
            var controller = new TipDotSystemController(manager);
            Action<TipDotStateInfo> onStateChanged = info => { };
            Require(controller.LinkNodeStateChange(key, onStateChanged) && runtime.IsActive && runtime.UiLinkCount == 1,
                "original UI listener activates the actual graph condition");
            yield return null; yield return null; yield return null;
            Require(!runtime.IsShowTip, "actual original profile graph is hidden below the real threshold");
            Require(session.ApplyCharacterMutation(value => {
                value.CharacterProperties.NpcFriendships.Single(npc => npc.NpcClassId == 8401).Level = 20;
                value.CharacterProperties.NpcFriendships.Single(npc => npc.NpcClassId == 10608).Level = 50;
            }).IsApplied, "actual profile/tree threshold transaction");
            player.Synchronize();
            yield return null; yield return null; yield return null;
            Require(runtime.IsShowTip && conditionCalls == 2 && !services.IsTalentTreeFunctionLimit(player, 10608),
                "saved events refresh original graph and type-based tree subscriptions: tip=" + runtime.IsShowTip +
                ", active=" + runtime.IsActive + ", condition calls=" + conditionCalls);
            Require(services.GetTalentGraph(10608).GetNodeInfo(2).Condition.IsMet(player) &&
                services.GetTalentPointState(services.GetTalentGraph(10608).GetNodeInfo(2), player.GetNpcTalentTreeUnlockedNodeIds(10608)) == PartnerTipDotTalentPointState.CanActive,
                "same cached nodes read replacement save and original predecessor context");
            limited = true; runtime.SetDirty();
            yield return null; yield return null; yield return null;
            Require(!runtime.IsShowTip, "original function 65 gate is preserved");
            limited = false; runtime.SetDirty();
            yield return null; yield return null; yield return null;
            Require(runtime.IsShowTip, "reopened function gate exposes original claimable chapter");
            var claims = new OfflinePartnerProfileService(session, profileRules);
            using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
                Require(!claims.Claim(8401, 1).IsApplied, "real profile reward write failure");
            player.Synchronize();
            yield return null; yield return null; yield return null;
            Require(runtime.IsShowTip && player.NpcManager.GetByNpcClassId(8401).ReceivedProfileRewards.Count == 0,
                "failed transaction retains claimable graph state");
            Require(claims.Claim(8401, 1).IsApplied, "real claim retry"); player.Synchronize();
            yield return null; yield return null; yield return null;
            Require(!runtime.IsShowTip && session.InventoryCount(107) == 3, "configured saved reward/receipt clears the real original graph");
            services.DeregisterCondition(services.GetTalentItem(10608).TalentTreeUnlockCondition, conditionChanged);
            int previousCalls = conditionCalls;
            Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.Single(npc => npc.NpcClassId == 10608).Level = 51).IsApplied,
                "save after condition unsubscribe"); player.Synchronize();
            Require(conditionCalls == previousCalls, "condition callback released");
            player.Reload();
            Require(controller.UnlinkNodeStateChange(key, onStateChanged) && !runtime.IsActive,
                "original UI listener releases graph activation");
            manager.DestoryGraphNode(native.name, key.InstanceInfo);
            runtime = (TipDotNpcProfileCanUnlockConditionNode)manager.FindNodeOrCreate(key);
            Require(controller.LinkNodeStateChange(key, onStateChanged) && runtime.IsActive,
                "recreated graph follows original UI activation");
            yield return null; yield return null; yield return null;
            Require(!runtime.IsShowTip, "graph recreation reads receipt restored from disk");
            string before = JsonUtility.ToJson(session.Save);
            _ = services.MapExploreNpcs().ToArray(); _ = services.GetTalentGraph(10608).Nodes.Count;
            _ = services.GetAllExpByDict(new Dictionary<int, long> { { 1100, 1 } }, services.GetNpc(8401).GiftPreferenceDict);
            Require(before == JsonUtility.ToJson(session.Save), "configuration/predicate reads preserve saved payload");
            Require(controller.UnlinkNodeStateChange(key, onStateChanged) && runtime.UiLinkCount == 0 && !runtime.IsActive,
                "recreated graph releases its UI listener");
        }
        finally { manager.Dispose(); services.Dispose(); }
        yield return null;
        Debug.Log("Saved partner tip services passed: stable original tables, 252 node contexts, current NPC/gift/profile rules, real native profile graph, atomic failure/retry/reload and subscription lifetime");
    }
}
