using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    // Profile-local object identities, not NPC configuration IDs. Mutate only
    // the player clone supplied by ApplyCharacterMutation[IfChanged].
    [Serializable]
    public sealed class OfflineLocalActorIdentities
    {
        public long PlayerId;
        public long LastAllocatedId;
        public List<OfflineLocalNpcIdentity> Npcs = new List<OfflineLocalNpcIdentity>();

        public long NpcId(int npcClassId)
        {
            return Npcs.SingleOrDefault(value => value.NpcClassId == npcClassId)?.NpcId ?? 0;
        }

        public static bool IsValid(OfflineCharacterPropertyProgress properties)
        {
            var value = properties.LocalActorIdentities;
            if (value == null || value.PlayerId < 0 || value.LastAllocatedId < value.PlayerId
                || value.Npcs == null || value.Npcs.Count > 1000
                || value.Npcs.Any(row => row == null || row.NpcClassId <= 0 || row.NpcId <= 0
                    || row.NpcId > value.LastAllocatedId || row.NpcId == value.PlayerId)
                || value.Npcs.Select(row => row.NpcClassId).Distinct().Count() != value.Npcs.Count
                || value.Npcs.Select(row => row.NpcId).Distinct().Count() != value.Npcs.Count) return false;
            // An empty identity table is the backward-compatible, not-yet-
            // migrated state. Reading it alone must not write the profile.
            if (value.PlayerId == 0) return value.LastAllocatedId == 0 && value.Npcs.Count == 0;
            var records = properties.NpcConversations.Records;
            return records.All(row => row.PlayerId == value.PlayerId && row.NpcId == value.NpcId(row.NpcClassId)
                && row.DialogueId <= value.LastAllocatedId && row.DialogueId != value.PlayerId
                && !value.Npcs.Any(npc => npc.NpcId == row.DialogueId));
        }

        public static bool Ensure(OfflinePlayerProgress player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!player.CharacterCreated) return false;
            var properties = player.CharacterProperties;
            if (!OfflineCharacterPropertyProgress.IsValid(properties))
                throw new InvalidOperationException("Invalid local actor identity source.");
            var value = properties.LocalActorIdentities;
            var records = properties.NpcConversations.Records;
            bool changed = false;
            if (value.PlayerId == 0)
            {
                long[] owners = records.Select(row => row.PlayerId).Distinct().ToArray();
                if (owners.Length > 1) throw new InvalidOperationException("Conversation owners disagree.");
                foreach (var group in records.GroupBy(row => row.NpcClassId))
                {
                    long[] ids = group.Select(row => row.NpcId).Distinct().ToArray();
                    if (ids.Length != 1) throw new InvalidOperationException("Conversation NPC identities disagree.");
                    value.Npcs.Add(new OfflineLocalNpcIdentity { NpcClassId = group.Key, NpcId = ids[0] });
                }
                value.LastAllocatedId = records.SelectMany(row => new[] { row.PlayerId, row.NpcId, row.DialogueId })
                    .DefaultIfEmpty(0).Max();
                // Even removed history may have durable reward receipts. Never
                // reuse those IDs for a new conversation in this profile.
                foreach (string receipt in player.AppliedCharacterEventIds)
                {
                    if (!receipt.StartsWith("npc-dialogue-node:", StringComparison.Ordinal)) continue;
                    string[] parts = receipt.Split(':');
                    if (parts.Length != 3 || !long.TryParse(parts[1], NumberStyles.None,
                        CultureInfo.InvariantCulture, out long id) || id <= 0
                        || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int node) || node <= 0)
                        throw new InvalidOperationException("Invalid saved conversation reward receipt.");
                    value.LastAllocatedId = Math.Max(value.LastAllocatedId, id);
                }
                value.PlayerId = owners.Length == 1 ? owners[0] : Allocate(player);
                changed = true;
                if (!IsValid(properties)) throw new InvalidOperationException("Saved actor IDs overlap or disagree.");
            }
            foreach (int npcClass in properties.NpcFriendships.Select(row => row.NpcClassId).OrderBy(id => id))
            {
                if (value.NpcId(npcClass) != 0) continue;
                value.Npcs.Add(new OfflineLocalNpcIdentity { NpcClassId = npcClass, NpcId = Allocate(player) });
                changed = true;
            }
            return changed;
        }

        public static long Allocate(OfflinePlayerProgress transactionPlayer)
        {
            var value = transactionPlayer.CharacterProperties.LocalActorIdentities;
            return value.LastAllocatedId = checked(value.LastAllocatedId + 1);
        }
    }

    [Serializable]
    public sealed class OfflineLocalNpcIdentity
    {
        public int NpcClassId;
        public long NpcId;
    }

    public sealed partial class OfflineMainlineProgressSession
    {
        public ProgressionResult EnsureLocalActorIdentities()
        {
            Reload();
            return ApplyCharacterMutationIfChanged(OfflineLocalActorIdentities.Ensure);
        }
    }
}
