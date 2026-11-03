using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Rules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;

public static class PartnerAchievementValidation
{
    static void Require(bool value, string message)
    { if (!value) throw new Exception("Original partner achievements: " + message); }
    public static void Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var rules = new Rules(catalog.Rows);
        var normalTask = new Rules.AchievementTaskNpcFriendshipLevel(rules);
        var starTask = new Rules.AchievementTaskStarGodNpcFriendshipLevel(rules);
        int npc = 0, level = 0; bool found = false;
        var player = new Rules.Player(rules, 123, id => (found && id == npc, id == npc ? level : 0));
        int checkedRows = 0;
        foreach (var group in catalog.Rows("achievement").Where(row => new[] { 27, 77 }.Contains(Convert.ToInt32(row["AchievementType"]))).GroupBy(row => Convert.ToInt32(row["Group"])))
        {
            var ordered = group.OrderBy(row => Convert.ToInt32(row["Id"])).ToArray();
            var infos = rules.GetAchievementInfoListByGroupId(group.Key);
            Require(infos.Count == ordered.Length && infos.Select(info => info.StartRawId).SequenceEqual(ordered.Select(row => Convert.ToInt32(row["Id"]))), "original raw template ordering");
            var key = rules.GetAchievementGroupToKey(group.Key);
            Require(rules.GetAchievementKeyToGroup(key) == group.Key, "real normal/star key mapping");
            for (int index = 0; index < infos.Count; index++)
            {
                var info = infos[index];
                Require(info.Id == ((index << 12) | group.Key) && info.Id != info.StartRawId &&
                    Rules.AchievementInfo.GetGroupAndIndex(info.Id) == (group.Key, index), "original encoded ID differs from raw row ID");
                Require(ReferenceEquals(infos[0].AchievementData.AchievementDataKey, info.AchievementData.AchievementDataKey), "original parser key cache identity");
                var rawRewards = (Dictionary<string, object>)ordered[index]["Reward"];
                Require(info.Reward.Count == rawRewards.Count && info.Reward.All(value => Convert.ToInt64(rawRewards[value.Key.ToString()]) == value.Value), "exact original reward payload");
                npc = info.AchievementData.NpcClassId; level = info.AchievementData.Level - 1; found = true;
                Func<Rules.AchievementState> state = () => info.AchievementType == Rules.AchievementType.NpcFriendshipLevel
                    ? normalTask.GetAchievementState(info.Id, player) : starTask.GetAchievementState(info.Id, player);
                Require(state() == Rules.AchievementState.Processing, "below configured friendship boundary");
                level++;
                Require(state() == Rules.AchievementState.Completed, "exact configured boundary");
                var progress = info.AchievementType == Rules.AchievementType.NpcFriendshipLevel
                    ? normalTask.GetProgressValue(info.Id, player) : starTask.GetProgressValue(info.Id, player);
                Require(progress == ((long)level, (long)level), "original max/current progress order");
                found = false; Require(state() == Rules.AchievementState.Processing, "unmet partner stays processing even with an equal number"); found = true;
                player.RecordAchievementRewardByMaxIndex(key, index);
                Require(player.TryRecordAlreadyGotAchievementReward(group.Key, -1, out var ids, out var rewards, false) == 0 &&
                    ids.Contains(info.Id) && rewards.Last().SequenceEqual(info.Reward), "sequential original ID/reward calculation");
                Require(state() == Rules.AchievementState.Received, "receipt follows completed condition");
                level--; Require(state() == Rules.AchievementState.Processing, "original condition check precedes receipt check");
                checkedRows++;
            }
        }
        Require(checkedRows == 1980 && rules.GroupIds.Count() == 99, "all selected ordinary/star achievement groups");
        var normalKey = new Rules.AchievementDataKeyNpcFriendshipLevel(8401);
        var sameKey = new Rules.AchievementDataKeyNpcFriendshipLevel(8401);
        var starKey = new Rules.AchievementDataKeyStarGodNpcFriendshipLevel(8401);
        Require(normalKey.Equals(sameKey) && normalKey.GetHashCode() == sameKey.GetHashCode() && !normalKey.Equals(starKey) &&
            normalKey.GetOpCode() == 1118 && starKey.GetOpCode() == 20206 && rules.GetAchievementKeyToGroup(starKey) == -1 &&
            rules.GetAchievementKeyToGroup(Rules.AchievementDataKeyNormal.None) == 0, "key identity, independent kinds, original hashes/opcodes and absent defaults");
        var events = new List<string>();
        var small = new Rules.Player(rules, 456, _ => (true, 10));
        Require(small.TryRecordAlreadyGotAchievementReward(400, -1, out _, out _) == 1221 &&
            small.TryRecordAlreadyGotAchievementReward(-1, -1, out _, out _) == 1221, "original absent-manager errors");
        small.OnUpdateAchievementData += key => events.Add("progress:" + rules.GetAchievementKeyToGroup(key));
        small.OnRecordAlreadyGotAchievementReward += index => events.Add("index:" + index);
        small.OnRecordAlreadyGotAchievementRewardByGroupId += group => events.Add("group:" + group);
        small.RecordAchievementRewardByMaxIndex(normalKey, 1);
        var record = small.AchievementManager[normalKey.AchievementType].GetGroupRecord(400);
        Require(record.GotRewardMaxIndex == -1 && !record.TryAddGotRewardByIndex(456, 1), "original empty default and nonsequential claim refused");
        small.RecordAchievementRewardByMaxIndex(normalKey, -1); small.RecordAchievementRewardByMaxIndex(normalKey, 1);
        Require(events.SequenceEqual(new[] { "progress:400" }) && record.CanRewardsMaxIndex == 1, "negative max ignored and unchanged max does not notify");
        Require(small.TryRecordAlreadyGotAchievementReward(400, 0, out var zeroIds, out _) == 0 && zeroIds.Count == 0 && events.Last() == "group:400", "original zero-count branch");
        events.Clear();
        Require(small.TryRecordAlreadyGotAchievementReward(400, 999, out var truncated, out _) == 0 && truncated.Count == 2 &&
            events.SequenceEqual(new[] { "index:0", "index:1", "group:400" }) &&
            small.TryRecordAlreadyGotAchievementReward(400, -1, out _, out _) == 1220, "original quantity truncation, event order and exhausted group error");
        Require(small.TryResetAchievementGroupRecord(normalKey) && !small.TryResetAchievementGroupRecord(normalKey) &&
            record.CanRewardsMaxIndex == -1 && record.GotRewardMaxIndex == -1, "original explicit reset branch");

        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "partner-achievement-transactions"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var service = new OfflinePartnerAchievementService(session, rules);
        int notified = 0, batches = 0;
        service.ProgressUpdated += key => {
            var saved = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(saved.Save.Player.CharacterProperties.PartnerAchievements.Any(value => value.GroupId == rules.GetAchievementKeyToGroup(key)), "progress saved before notification"); notified++;
        };
        service.RewardIndexReceived += _ => {
            var saved = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(JsonUtility.ToJson(saved.Save.Player) == JsonUtility.ToJson(session.Save.Player), "reward records saved before index notification"); notified++;
        };
        service.RewardGroupReceived += _ => notified++;
        service.RewardsReceivedBatch += () => batches++;
        Require(!service.RefreshFromSave().IsApplied && !service.Claim(400).Transaction.IsApplied, "uncreated character cannot receive rewards");
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "伙伴馈赠验证"; value.Inventory.Clear();
            foreach (var pair in new[] { (8401, 10), (8404, 5), (10608, 5), (11015, 100) })
                value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = pair.Item1, Level = pair.Item2 });
        }).IsApplied, "actual created local save");
        Require(service.RefreshFromSave().IsApplied && notified == 4 && session.Save.Player.CharacterProperties.PartnerAchievements.Count == 4,
            "configured thresholds, both types, closed collaboration excluded");
        Require(service.ReadCurrentPlayer().IsAnyCanGetAchievementReward(normalKey), "current saved progress has claimable rewards");
        byte[] bytes = File.ReadAllBytes(slots.GetProgressPath(slot));
        Require(service.RefreshFromSave().Status == ProgressionResultStatus.Unchanged && bytes.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot))), "unchanged refresh does not write");
        int before = notified;
        using (var held = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
            Require(!service.Claim(400, 1).Transaction.IsApplied && session.InventoryCount(100) == 0 && notified == before &&
                session.Save.Player.CharacterProperties.PartnerAchievements.Single(value => value.GroupId == 400).GotRewardMaxIndex == -1,
                "write failure rolls back original reward, receipt and notifications");
        var first = service.Claim(400, 1);
        Require(first.Transaction.IsApplied && first.ClaimedIds.SequenceEqual(new[] { 400 }) && session.InventoryCount(100) == 3 && notified == before + 2,
            "retry saves one exact configured reward with its encoded receipt");
        var remainder = service.Claim(400);
        Require(remainder.Transaction.IsApplied && remainder.ClaimedIds.SequenceEqual(new[] { 4496 }) && session.InventoryCount(101) == 3,
            "negative request receives only remaining eligible group entries");
        bytes = File.ReadAllBytes(slots.GetProgressPath(slot)); before = notified;
        Require(!service.Claim(400).Transaction.IsApplied && service.Claim(400).ErrorCode == 1220 && before == notified &&
            bytes.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot))), "duplicate requests neither save nor grant");
        var reopened = new OfflinePartnerAchievementService(OfflineMainlineProgressSession.OpenCharacter(slots, slot), rules);
        Require(!reopened.Claim(400).Transaction.IsApplied && reopened.ReadCurrentPlayer().IsAlreadyGotAchievementReward(rules.GetAchievementInfoByGroupAndIndex(400, 1)), "receipt and inventory survive actual disk reopen");
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterProperties.NpcFriendships.Single(row => row.NpcClassId == 8401).Level = 15;
            value.Inventory.Add(new OfflineItemStack { ItemId = 102, Count = long.MaxValue });
        }).IsApplied, "overflow boundary save");
        before = notified;
        Require(!service.Claim(400).Transaction.IsApplied && session.InventoryCount(102) == long.MaxValue && notified == before &&
            session.Save.Player.CharacterProperties.PartnerAchievements.Single(value => value.GroupId == 400).GotRewardMaxIndex == 1,
            "inventory overflow rolls back refreshed progress and receipt");
        Require(session.ApplyCharacterMutation(value => value.Inventory.RemoveAll(item => item.ItemId == 102)).IsApplied &&
            service.Claim(400).Transaction.IsApplied && session.InventoryCount(102) == 3, "overflow retry grants once");
        Require(service.Claim(400, -1, true).Transaction.IsApplied && batches == 1 && session.InventoryCount(100) == 9 &&
            session.Save.Player.CharacterProperties.PartnerAchievements.Single(value => value.GroupId == 459).GotRewardMaxIndex == -1,
            "same-type request includes met normal groups, including the star NPC normal key, and preserves its independent star group");
        Require(service.Claim(459).Transaction.IsApplied && session.InventoryCount(118) == 2, "star reward keeps its own configured group");
        Require(!service.Claim(-1).Transaction.IsApplied && !service.Claim(401).Transaction.IsApplied, "unknown and unmet groups refused");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.PartnerAchievements.Add(new OfflinePartnerAchievementRecord { GroupId = 400 })).Status == ProgressionResultStatus.InvalidState, "duplicate saved groups rejected");
        var legacy = JsonUtility.FromJson<OfflineCharacterPropertyProgress>("{}"); OfflineCharacterPropertyProgress.Normalize(legacy);
        Require(legacy.PartnerAchievements.Count == 0, "legacy missing records remain empty; never guess old receipts");
        var detached = service.ReadCurrentPlayer(); detached.AchievementManager[normalKey.AchievementType].GetGroupRecord(400).GotRewardMaxIndex = -1;
        Require(service.ReadCurrentPlayer().IsAlreadyGotAchievementReward(rules.GetAchievementInfoByGroupAndIndex(400, 2)), "read snapshots cannot change the saved receipt");
        Debug.Log("Original partner achievement rules/transactions passed: all 1980 thresholds/rewards and 99 key groups, encoded IDs, sequential count/event branches, real atomic failure/retry/overflow, same-type scope, disk reload and legacy defaults");
    }
}
