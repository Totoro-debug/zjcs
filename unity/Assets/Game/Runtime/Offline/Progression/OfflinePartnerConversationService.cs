using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.Progression
{
    // User-confirmed local boundary for the unavailable refresh/ack server:
    // refresh only when opening the partner panel; reward each newly accepted
    // node atomically with its original history/status transition.
    public sealed class OfflinePartnerConversationService
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialCharacterCatalog character;
        private readonly OfflinePartnerConversationPlayer player;
        private readonly long playerId;
        private readonly Func<long> nextIdentity;
        private readonly Func<bool> functionLimited;
        private readonly Action committed;
        private readonly int dayLimit, timeZone;
        private readonly DateTime resetTime;
        private sealed class FriendshipLevelInfo { public long FriendshipExp; public int SubRankLimit; }
        private readonly Dictionary<int, Dictionary<int, FriendshipLevelInfo>> friendshipLevels
            = new Dictionary<int, Dictionary<int, FriendshipLevelInfo>>();

        public OfflinePartnerConversationService(OfflineMainlineProgressSession session,
            OfficialCharacterCatalog character, OfflinePartnerConversationPlayer player, long playerId,
            Func<long> nextIdentity, Func<bool> functionLimited, int serverTimeZoneHours, Action committed)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.character = character ?? throw new ArgumentNullException(nameof(character));
            if (!character.HasCurrentPartnerRules) throw new ArgumentException("Bind the selected current partner catalog.", nameof(character));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            this.playerId = playerId;
            this.nextIdentity = nextIdentity ?? throw new ArgumentNullException(nameof(nextIdentity));
            this.functionLimited = functionLimited ?? throw new ArgumentNullException(nameof(functionLimited));
            this.committed = committed ?? throw new ArgumentNullException(nameof(committed));
            timeZone = serverTimeZoneHours;
            var settings = character.PartnerRows("game_settings").Single();
            dayLimit = Convert.ToInt32(settings["NpcDialogueDayLimit"], CultureInfo.InvariantCulture);
            resetTime = DateTime.ParseExact((string)settings["DailyResetTime"], "(HH:mm:ss)", CultureInfo.InvariantCulture);
            foreach (var row in character.PartnerRows("npc_friendship_level"))
            {
                int id = Convert.ToInt32(row["Id"], CultureInfo.InvariantCulture);
                if (!friendshipLevels.TryGetValue(id, out var levels))
                    friendshipLevels.Add(id, levels = new Dictionary<int, FriendshipLevelInfo>());
                levels.Add(Convert.ToInt32(row["Level"], CultureInfo.InvariantCulture), new FriendshipLevelInfo {
                    FriendshipExp = Convert.ToInt64(row["FriendshipExp"], CultureInfo.InvariantCulture),
                    SubRankLimit = Convert.ToInt32(row["SubRankLimit"], CultureInfo.InvariantCulture) });
            }
        }

        // NpcFriendshipLevelInfoParser.GetFriendshipLevelByExp, with only the
        // selected configuration lookup, row type and SubRank type adapted.
        private int GetFriendshipLevelByExp(int id, long friendshipExp, int subRank)
        {
            friendshipLevels.TryGetValue(id, out var dictionary);
            if (dictionary == null)
            {
                return 0;
            }
            int num = 0;
            foreach (KeyValuePair<int, FriendshipLevelInfo> item in dictionary)
            {
                if (item.Value.FriendshipExp <= friendshipExp && item.Key > num && item.Value.SubRankLimit <= subRank)
                {
                    num = item.Key;
                }
            }
            return num;
        }

        private DateTime UtcNow()
        {
            DateTime value = player.Now;
            if (value.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Bind the original conversation clock in UTC.");
            return value;
        }
        private long NewIdentity(HashSet<long> occupied)
        {
            long value = nextIdentity();
            if (value <= 0 || !occupied.Add(value)) throw new InvalidOperationException("The local identity generator returned an occupied or invalid ID.");
            return value;
        }
        private void ValidateOwner(OfflineNpcConversationProgress progress)
        {
            if (progress.Records.Any(value => value.PlayerId != playerId))
                throw new InvalidOperationException("Saved conversation owner differs from the local profile identity.");
            if (progress.Records.GroupBy(value => value.NpcClassId).Any(group => group.Select(value => value.NpcId).Distinct().Count() != 1)
                || progress.Records.GroupBy(value => value.NpcId).Any(group => group.Select(value => value.NpcClassId).Distinct().Count() != 1))
                throw new InvalidOperationException("Saved conversation NPC identity mapping is inconsistent.");
            if (progress.DayLimits.Any(value => !progress.Records.Any(row => row.NpcId == value.NpcId)))
                throw new InvalidOperationException("Saved conversation quota has no recoverable NPC identity mapping.");
        }

        public ProgressionResult RefreshOnPanelOpen()
        {
            session.Reload();
            if (!session.Save.Player.CharacterCreated || functionLimited()) return Unchanged();
            var result = session.ApplyCharacterMutationIfChanged(value => {
                var progress = value.CharacterProperties.NpcConversations;
                ValidateOwner(progress);
                var occupied = new HashSet<long>(progress.Records.SelectMany(row => new[] { row.NpcId, row.DialogueId, row.PlayerId }));
                occupied.Add(playerId);
                var transactionPlayer = player.ForCharacterMutation(value, _ => throw new InvalidOperationException("Creation must not emit a node-update notification."));
                var daily = new OfflinePartnerConversationDayLimits(progress, player.DialogueConfiguration, UtcNow, dayLimit, resetTime, timeZone);
                bool changed = false;
                foreach (var friendship in value.CharacterProperties.NpcFriendships)
                {
                    int npcClass = friendship.NpcClassId;
                    if (npcClass >= 11015 && npcClass <= 11022) continue; // Confirmed closed collaborations.
                    var npc = character.FindPartner("npc", npcClass);
                    if (npc == null) throw new InvalidOperationException("Original NPC configuration is absent: " + npcClass);
                    if (!(npc["Dialogues"] is List<object> order)) continue;
                    long npcId = progress.Records.FirstOrDefault(row => row.NpcClassId == npcClass)?.NpcId ?? 0;
                    foreach (object raw in order)
                    {
                        int classId = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                        if (progress.Records.Any(row => row.NpcClassId == npcClass && row.DialogueClassId == classId)) continue;
                        var first = player.DialogueConfiguration.Get(classId, 1)
                            ?? throw new InvalidOperationException("Original conversation start is absent: " + classId);
                        if (first.Type != PartnerConversationType.Auto
                            || !(first.UnlockCondition?.IsMet(transactionPlayer) ?? true)
                            || !(first.ChainSequenceCondition?.IsMet(transactionPlayer) ?? true)) continue;
                        // ValidateOwner rejects orphaned quotas before a new
                        // NPC identity can bypass an existing daily count.
                        if (npcId != 0 && daily.CheckNpcDialogueHasDayLimit(npcId, classId)) continue;
                        if (npcId == 0) npcId = NewIdentity(occupied);
                        long id = NewIdentity(occupied);
                        var record = PartnerConversationRecord.Create(playerId, npcId, classId, 1, id, UtcNow());
                        progress.Records.Add(OfflineNpcConversation.FromRecord(npcClass, record));
                        daily.RecordNpcDialogueDayLimit(npcId, classId);
                        changed = true;
                    }
                }
                return changed;
            });
            if (result.IsApplied) committed();
            return result;
        }

        public ProgressionResult Acknowledge(long dialogueId, int nodeId)
        {
            if (dialogueId <= 0 || nodeId <= 0) return Invalid("A saved dialogue and original node are required.");
            session.Reload();
            if (!session.Save.Player.CharacterCreated || functionLimited()) return Invalid("The original conversation function is locked.");
            string receipt = "npc-dialogue-node:" + dialogueId.ToString(CultureInfo.InvariantCulture) + ":" + nodeId.ToString(CultureInfo.InvariantCulture);
            var result = session.ApplyCharacterMutation(value => {
                var progress = value.CharacterProperties.NpcConversations;
                ValidateOwner(progress);
                int index = progress.Records.FindIndex(row => row.DialogueId == dialogueId);
                if (index < 0) throw new InvalidOperationException("Saved conversation is absent.");
                var saved = progress.Records[index];
                if (saved.NpcClassId >= 11015 && saved.NpcClassId <= 11022)
                    throw new InvalidOperationException("This collaboration NPC is closed by the confirmed offline policy.");
                var record = saved.ToRecord();
                var current = player.DialogueConfiguration.Get(record.DialogueClassId, record.CurrentNode);
                if (record.DialogueStatus == PartnerConversationStatus.End || current?.NextNodes == null || !current.NextNodes.Contains(nodeId))
                    throw new InvalidOperationException("The requested node is not an original successor of the current conversation.");
                var next = player.DialogueConfiguration.Get(record.DialogueClassId, nodeId)
                    ?? throw new InvalidOperationException("Original conversation node is absent.");
                int notifications = 0;
                var transactionPlayer = player.ForCharacterMutation(value, update => {
                    if (!ReferenceEquals(update, record)) throw new InvalidOperationException("Original update identity changed.");
                    notifications++;
                });
                if (!(next.UnlockCondition?.IsMet(transactionPlayer) ?? true))
                    throw new InvalidOperationException("The original successor condition is not met.");
                var friendship = value.CharacterProperties.NpcFriendships.SingleOrDefault(row => row.NpcClassId == saved.NpcClassId)
                    ?? throw new InvalidOperationException("Conversation NPC is no longer met.");
                foreach (var item in next.RewardItem ?? new Dictionary<int, long>())
                {
                    if (character.FindPartnerItem(item.Key) == null || !OfflineInventoryOperations.Grant(value, item.Key,
                        item.Value, new DateTimeOffset(UtcNow()).ToUnixTimeSeconds()).IsApplied)
                        throw new InvalidOperationException("Original conversation item reward could not be granted.");
                }
                if (next.RewardFriendship < 0) throw new InvalidOperationException("Original friendship reward is invalid.");
                if (next.RewardFriendship > 0)
                {
                    long experience = friendship.FriendshipExp;
                    if (experience == 0 && friendship.Level > 0 && friendshipLevels.TryGetValue(saved.NpcClassId, out var levels)
                        && levels.TryGetValue(friendship.Level, out var currentLevel)) experience = currentLevel.FriendshipExp;
                    friendship.FriendshipExp = checked(experience + next.RewardFriendship);
                    friendship.Level = GetFriendshipLevelByExp(saved.NpcClassId, friendship.FriendshipExp, value.SubRank);
                }
                record.SetCurrentNodeAndUpdateStatus(nodeId, transactionPlayer);
                if (record.DateTime.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Bind the original conversation clock in UTC.");
                if (notifications != 1) throw new InvalidOperationException("Original node update notification was not emitted exactly once.");
                progress.Records[index] = OfflineNpcConversation.FromRecord(saved.NpcClassId, record);
            }, receipt);
            if (result.IsApplied) committed();
            return result;
        }
        private static ProgressionResult Unchanged() => new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
        private static ProgressionResult Invalid(string message) => new ProgressionResult { Status = ProgressionResultStatus.InvalidState, Message = message };
    }
}
