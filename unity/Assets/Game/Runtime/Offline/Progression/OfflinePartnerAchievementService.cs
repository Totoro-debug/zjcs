using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AchievementRules = Zjcs.Official.Partners.PartnerFriendshipAchievementRules;

namespace Zjcs.Offline.Progression
{
    public sealed class PartnerAchievementClaimResult
    {
        public ProgressionResult Transaction { get; internal set; }
        public int ErrorCode { get; internal set; }
        public IReadOnlyCollection<int> ClaimedIds { get; internal set; } = Array.Empty<int>();
    }

    // Confirmed local request boundary. Rule calculations operate on the same
    // clone as inventory and receipts; notifications follow the durable write.
    public sealed class OfflinePartnerAchievementService
    {
        private readonly OfflineMainlineProgressSession session;
        public AchievementRules Rules { get; }
        public event Action<AchievementRules.AchievementDataKey> ProgressUpdated;
        public event Action<int> RewardIndexReceived, RewardGroupReceived;
        public event Action RewardsReceivedBatch;

        public OfflinePartnerAchievementService(OfflineMainlineProgressSession session, AchievementRules rules)
        { this.session = session ?? throw new ArgumentNullException(nameof(session)); Rules = rules ?? throw new ArgumentNullException(nameof(rules)); }

        private static bool Closed(int npc) => npc >= 11015 && npc <= 11022;
        private static int NpcId(AchievementRules.AchievementDataKey key) => key is AchievementRules.AchievementDataKeyNpcFriendshipLevel normal
            ? normal.NpcClassId : ((AchievementRules.AchievementDataKeyStarGodNpcFriendshipLevel)key).NpcClassId;

        public AchievementRules.Player ReadCurrentPlayer()
        {
            return Read(session.Save.Player);
        }

        private AchievementRules.Player Read(OfflinePlayerProgress source)
        {
            var player = new AchievementRules.Player(Rules, source.CharacterProperties.LocalActorIdentities.PlayerId, id => {
                var npc = source.CharacterProperties.NpcFriendships.FirstOrDefault(value => value.NpcClassId == id);
                return (npc != null, npc?.Level ?? 0);
            });
            foreach (var record in source.CharacterProperties.PartnerAchievements)
            {
                if (!Rules.TryGetAchievementGroupToKey(record.GroupId, out var key))
                    throw new InvalidOperationException("Unknown saved partner achievement group.");
                int count = Rules.GetAchievementInfoListByGroupId(record.GroupId).Count;
                if (record.CanRewardsMaxIndex >= count || record.GotRewardMaxIndex >= count)
                    throw new InvalidOperationException("Saved partner achievement index exceeds the selected group.");
                if (!player.AchievementManager.TryGetValue(key.AchievementType, out var achievement))
                    player.AchievementManager.Add(key.AchievementType, achievement = new AchievementRules.PlayerAchievement {
                        PlayerId = player.Id, AchievementType = key.AchievementType });
                achievement.GroupRecords.Add(record.GroupId, new AchievementRules.PlayerAchievementRecordDataWrap {
                    CanRewardsMaxIndex = record.CanRewardsMaxIndex, GotRewardMaxIndex = record.GotRewardMaxIndex });
            }
            return player;
        }

        private void Refresh(AchievementRules.Player player, IEnumerable<int> groups)
        {
            var normal = new AchievementRules.AchievementTaskNpcFriendshipLevel(Rules);
            var star = new AchievementRules.AchievementTaskStarGodNpcFriendshipLevel(Rules);
            foreach (int group in groups)
            {
                var key = Rules.GetAchievementGroupToKey(group);
                if (Closed(NpcId(key))) continue;
                int maxIndex = -1;
                foreach (var info in Rules.GetAchievementInfoListByGroupId(group))
                {
                    var state = key.AchievementType == AchievementRules.AchievementType.NpcFriendshipLevel
                        ? normal.GetAchievementState(info.Id, player) : star.GetAchievementState(info.Id, player);
                    if (state != AchievementRules.AchievementState.Processing) maxIndex = info.Index;
                }
                player.RecordAchievementRewardByMaxIndex(key, maxIndex);
            }
        }

        private static bool Write(OfflinePlayerProgress target, AchievementRules.Player player)
        {
            bool changed = false;
            var records = target.CharacterProperties.PartnerAchievements;
            foreach (var achievement in player.AchievementManager.Values)
                foreach (var group in achievement.GroupRecords)
                {
                    var record = records.FirstOrDefault(value => value.GroupId == group.Key);
                    if (record == null)
                    { records.Add(record = new OfflinePartnerAchievementRecord { GroupId = group.Key }); changed = true; }
                    if (record.CanRewardsMaxIndex == group.Value.CanRewardsMaxIndex && record.GotRewardMaxIndex == group.Value.GotRewardMaxIndex) continue;
                    record.CanRewardsMaxIndex = group.Value.CanRewardsMaxIndex;
                    record.GotRewardMaxIndex = group.Value.GotRewardMaxIndex; changed = true;
                }
            return changed;
        }

        private void Capture(AchievementRules.Player player, List<Action> pending)
        {
            player.OnUpdateAchievementData += key => pending.Add(() => ProgressUpdated?.Invoke(key));
            player.OnRecordAlreadyGotAchievementReward += index => pending.Add(() => RewardIndexReceived?.Invoke(index));
            player.OnRecordAlreadyGotAchievementRewardByGroupId += group => pending.Add(() => RewardGroupReceived?.Invoke(group));
            player.OnRecordAlreadyGotAchievementRewardBatch += () => pending.Add(() => RewardsReceivedBatch?.Invoke());
        }
        private static ProgressionResult Invalid(string message) => new ProgressionResult { Status = ProgressionResultStatus.InvalidState, Message = message };

        public ProgressionResult RefreshFromSave()
        {
            var pending = new List<Action>();
            ProgressionResult result;
            try
            {
                session.Reload();
                result = session.ApplyCharacterMutationIfChanged(source => {
                    if (!source.CharacterCreated) return false;
                    var player = Read(source); Capture(player, pending);
                    Refresh(player, Rules.GroupIds); return Write(source, player);
                });
            }
            catch (IOException error) { return Invalid(error.Message); }
            catch (UnauthorizedAccessException error) { return Invalid(error.Message); }
            if (result.IsApplied) foreach (var notify in pending) notify();
            return result;
        }

        public PartnerAchievementClaimResult Claim(int groupId, int count = -1, bool receiveSameType = false)
        {
            var pending = new List<Action>();
            var claimed = new HashSet<int>();
            int errorCode = 0;
            ProgressionResult result;
            try
            {
                session.Reload();
                if (!Rules.TryGetAchievementGroupToKey(groupId, out var key) || Closed(NpcId(key)))
                    return new PartnerAchievementClaimResult { ErrorCode = 1221, Transaction = Invalid("Unavailable partner achievement group.") };
                result = session.ApplyCharacterMutationIfChanged(source => {
                    if (!source.CharacterCreated) throw new InvalidOperationException("A created character is required.");
                    var player = Read(source); Capture(player, pending);
                    if (!player.TryGetNpcFriendshipLevel(NpcId(key), out _))
                        throw new InvalidOperationException("Only met partners can receive friendship achievements.");
                    int[] groups = receiveSameType ? Rules.GroupIds.Where(id => {
                        var value = Rules.GetAchievementGroupToKey(id);
                        return value.AchievementType == key.AchievementType && !Closed(NpcId(value)) &&
                            player.TryGetNpcFriendshipLevel(NpcId(value), out _);
                    }).ToArray() : new[] { groupId };
                    Refresh(player, groups);
                    foreach (int group in groups)
                    {
                        if (receiveSameType && !player.IsAnyCanGetAchievementReward(Rules.GetAchievementGroupToKey(group))) continue;
                        int error = player.TryRecordAlreadyGotAchievementReward(group, count, out var ids, out var rewards);
                        if (error != 0) { errorCode = error; throw new InvalidOperationException("Original achievement error: " + error); }
                        foreach (var reward in rewards)
                            foreach (var value in reward)
                                if (!OfflineInventoryOperations.Grant(source, value.Key, value.Value, 0).IsApplied)
                                    throw new InvalidOperationException("Original partner achievement reward could not be saved.");
                        claimed.UnionWith(ids);
                    }
                    if (receiveSameType && claimed.Count > 0) player.FireOnRecordAlreadyGotAchievementRewardBatch();
                    return Write(source, player);
                });
            }
            catch (IOException error) { result = Invalid(error.Message); }
            catch (UnauthorizedAccessException error) { result = Invalid(error.Message); }
            if (result.IsApplied) foreach (var notify in pending) notify();
            return new PartnerAchievementClaimResult { Transaction = result, ErrorCode = errorCode,
                ClaimedIds = result.IsApplied ? claimed.ToArray() : Array.Empty<int>() };
        }
    }
}
