using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;

public static class PartnerTalentPlayerValidation
{
    static void Require(bool value, string message)
    { if (!value) throw new Exception("Saved partner talent view: " + message); }

    public static void Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var gifts = OfficialPartnerGiftAssetCatalog.LoadDefault();
        var rules = new PartnerTalentRules(name => catalog.Rows(name), gifts.GetMaxLevel);
        var tasks = OfficialMainlineTaskCatalog.Load(File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks.json")),
            File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks-source-manifest.json")));
        var slots = new ProfileSlotCatalog(Path.Combine(work, "talent-player-profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.Open(slots, slot, tasks);
        var source = new OfflinePartnerTalentPlayer(() => session.Save.Player, catalog);
        string original = JsonUtility.ToJson(session.Save);
        Require(!source.TryGetNpcFriendshipLevel(10608, out int absent) && absent == 0 &&
            source.GetNpcTalentExp(10608) == 0 && source.GetNpcTalentTreeUnlockedNodeIds(10608).Count == 0,
            "missing NPC/tree reads original empty defaults");
        Require(JsonUtility.ToJson(session.Save) == original, "predicates do not mutate or initialize a persisted tree");
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "天赋提示读档验证";
            player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 10608, Level = 100, FriendshipExp = gifts.GetMaxLevelInfo(10608).FriendshipExp });
            player.CharacterProperties.NpcTalentTrees.Add(new OfflineNpcTalentTree { NpcClassId = 10608, TalentExp = 2400,
                ActiveNodeIds = new System.Collections.Generic.List<int> { 1 } });
            player.Inventory.Clear();
            player.Inventory.Add(new OfflineItemStack { ItemId = 1100, Count = 3 });
            player.Inventory.Add(new OfflineItemStack { ItemId = 1357, Count = 100 });
            player.Inventory.Add(new OfflineItemStack { ItemId = 42300, Count = 1 });
        }).IsApplied, "actual isolated save transaction");
        Require(source.TryGetNpcFriendshipLevel(10608, out int level) && level == 100 && source.GetNpcTalentExp(10608) == 2400 &&
            source.GetNpcTalentTreeUnlockedNodeIds(10608).SequenceEqual(new[] { 1 }), "same source follows replacement player/tree records");
        ((int[])source.GetNpcTalentTreeUnlockedNodeIds(10608))[0] = 999;
        Require(source.GetNpcTalentTreeUnlockedNodeIds(10608).SequenceEqual(new[] { 1 }) &&
            source.GetNpcTalentExp(10708) == 0 && source.GetNpcTalentTreeUnlockedNodeIds(10708).Count == 0,
            "read-only node projection cannot mutate the save or leak another NPC's state");
        Require(source.GetNpcGifts().Select(item => item.Key).SequenceEqual(new[] { 1100 }), "fragments and talent points are excluded by the actual item type");
        Require(!rules.IsTalentTreeFunctionLimited(source, 10608) && !rules.IsTransformFunctionLimited(source, 10608) &&
            rules.CanTransformGift(source, 10608) && rules.GetRemainingPoints(source, 10608) == 128,
            "original predicates consume actual saved inventory, friendship, experience and unlocked cost");
        Require(rules.GetPointState(source, rules.GetTalentGraphByNpcClassId(10608).GetNodeInfo(2),
            source.GetNpcTalentTreeUnlockedNodeIds(10608)) == PartnerTalentRules.EPointState.CanActive, "actual saved adjacency and exact point inventory");
        session.Reload();
        Require(source.GetNpcTalentExp(10608) == 2400 && rules.CanTransformGift(source, 10608), "disk reload retains current source binding");
        var reopened = OfflineMainlineProgressSession.Open(slots, slot, tasks);
        var restored = new OfflinePartnerTalentPlayer(() => reopened.Save.Player, catalog);
        Require(restored.GetNpcTalentTreeUnlockedNodeIds(10608).SequenceEqual(new[] { 1 }) && restored.GetNpcTalentExp(10608) == 2400,
            "new session restores the existing save schema");
        Require(session.ApplyCharacterMutation(player => {
            player.CharacterProperties.NpcFriendships.Single(npc => npc.NpcClassId == 10608).Level = 49;
            player.CharacterProperties.NpcTalentTrees.Single(tree => tree.NpcClassId == 10608).TalentExp = 0;
            player.Inventory.RemoveAll(item => item.ItemId == 1100 || item.ItemId == 42300);
        }).IsApplied, "actual follow-up transaction replaces all dependent state");
        Require(rules.IsTalentTreeFunctionLimited(source, 10608) && rules.IsTransformFunctionLimited(source, 10608) &&
            !rules.CanTransformGift(source, 10608) && source.GetNpcTalentExp(10608) == 0,
            "live source does not retain stale level, inventory or experience");
        reopened.Reload();
        Require(rules.IsTalentTreeFunctionLimited(restored, 10608) && !rules.CanTransformGift(restored, 10608), "other reopened source follows an explicit reload");
        original = JsonUtility.ToJson(session.Save);
        _ = rules.GetRemainingPoints(source, 10608); _ = source.GetNpcGifts().ToArray();
        Require(JsonUtility.ToJson(session.Save) == original, "repeated read-only queries preserve the saved payload");
        Debug.Log("Saved original partner talent predicates passed: actual atomic state replacement, reload, reopened profile and exact gift-type projection");
    }
}
