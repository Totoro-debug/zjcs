using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Combat.Official;

namespace Zjcs.Offline.World
{
    public enum OfflineNpcAssistCandidateMode
    {
        FightAssist,
        TeamInvite
    }

    public enum OfflineNpcAssistBlockReason
    {
        None,
        MissingBattleModel,
        NotEncountered,
        Condition,
        InviteLimit
    }

    /// <summary>
    /// One official NPC invitation row after the local profile and battle asset
    /// constraints have been evaluated. Presentation owns the text and prefab;
    /// this record keeps the qualification decision in one place.
    /// </summary>
    public sealed class OfflineNpcAssistCandidate
    {
        internal OfflineNpcAssistCandidate(int npcClassId, bool available, bool selected,
            bool encountered, bool conditionMet, bool modelAvailable, bool friendshipBlocked,
            int friendshipLevel, int assistCount, OfflineNpcAssistBlockReason blockReason)
        {
            NpcClassId = npcClassId;
            Available = available;
            Selected = selected;
            Encountered = encountered;
            ConditionMet = conditionMet;
            ModelAvailable = modelAvailable;
            FriendshipBlocked = friendshipBlocked;
            FriendshipLevel = friendshipLevel;
            AssistCount = assistCount;
            BlockReason = blockReason;
        }

        public int NpcClassId { get; }
        public bool Available { get; }
        public bool Selected { get; }
        public bool Encountered { get; }
        public bool ConditionMet { get; }
        public bool ModelAvailable { get; }
        public bool FriendshipBlocked { get; }
        public int FriendshipLevel { get; }
        public int AssistCount { get; }
        public OfflineNpcAssistBlockReason BlockReason { get; }
    }

    /// <summary>
    /// Shared qualification seam for every local TeamInviteNpc/FightAssistNpc
    /// entry. The mode controls the official candidate population; the caller
    /// supplies only profile facts, model availability and persisted counts.
    /// </summary>
    public static class OfflineNpcAssistanceCandidates
    {
        public static IReadOnlyList<OfflineNpcAssistCandidate> Build(
            FightOfficialNpcAssistanceCatalog catalog,
            OfflineNpcAssistCandidateMode mode,
            int playType,
            int playerLevel,
            IReadOnlyDictionary<int, int> friendshipLevels,
            Func<string, int> storyCount,
            Func<int, bool> hasBattleModel,
            Func<int, NpcAssistRefreshCycle, long, int> assistCount,
            IReadOnlyCollection<int> selectedNpcClassIds,
            int maximum,
            long nowUnixSeconds,
            bool enforceInviteLimits)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!Enum.IsDefined(typeof(OfflineNpcAssistCandidateMode), mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (playerLevel < 0) throw new ArgumentOutOfRangeException(nameof(playerLevel));
            if (maximum < 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (enforceInviteLimits && playType <= 0)
                throw new ArgumentOutOfRangeException(nameof(playType));
            if (enforceInviteLimits && nowUnixSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds));

            IReadOnlyList<int> ids = mode == OfflineNpcAssistCandidateMode.TeamInvite
                ? catalog.TeamInviteNpcClassIds() : catalog.FightAssistNpcClassIds();
            FightOfficialNpcInviteRule invite = enforceInviteLimits
                ? catalog.InviteRule(playType) : null;
            var selected = selectedNpcClassIds == null
                ? new HashSet<int>() : new HashSet<int>(selectedNpcClassIds);
            var result = new List<OfflineNpcAssistCandidate>(ids.Count);
            int selectedCount = 0;
            foreach (int npcClassId in ids)
            {
                bool encountered = friendshipLevels != null
                    && friendshipLevels.ContainsKey(npcClassId);
                bool conditionMet = catalog.AssistConditionMet(npcClassId, playerLevel,
                    friendshipLevels, storyCount);
                bool eligible = mode == OfflineNpcAssistCandidateMode.TeamInvite
                    ? catalog.CanInviteTeamNpc(npcClassId, encountered, conditionMet)
                    : catalog.CanInviteNpc(npcClassId, encountered, conditionMet);
                bool friendshipBlocked = !encountered && catalog.RequiresEncounter(npcClassId);
                bool modelAvailable = hasBattleModel == null || hasBattleModel(npcClassId);
                int count = 0;
                bool inviteLimited = false;
                if (enforceInviteLimits && invite.OneNpcMaxInviteCount > 0 && assistCount != null)
                {
                    count = assistCount(npcClassId, (NpcAssistRefreshCycle)invite.RefreshCycle,
                        nowUnixSeconds);
                    inviteLimited = count >= invite.OneNpcMaxInviteCount;
                }

                OfflineNpcAssistBlockReason reason = modelAvailable
                    ? friendshipBlocked
                        ? OfflineNpcAssistBlockReason.NotEncountered
                        : !conditionMet ? OfflineNpcAssistBlockReason.Condition
                        : inviteLimited ? OfflineNpcAssistBlockReason.InviteLimit
                        : !eligible ? OfflineNpcAssistBlockReason.Condition
                        : OfflineNpcAssistBlockReason.None
                    : OfflineNpcAssistBlockReason.MissingBattleModel;
                bool available = modelAvailable && eligible && !inviteLimited;
                bool isSelected = selected.Contains(npcClassId) && available
                    && selectedCount < maximum;
                if (isSelected) selectedCount++;
                int friendshipLevel = friendshipLevels != null
                    && friendshipLevels.TryGetValue(npcClassId, out int level) ? level : 1;
                result.Add(new OfflineNpcAssistCandidate(npcClassId, available, isSelected,
                    encountered, conditionMet, modelAvailable,
                    friendshipBlocked, friendshipLevel, count, reason));
            }
            return result;
        }

        public static IReadOnlyList<int> SelectedIds(
            IReadOnlyList<OfflineNpcAssistCandidate> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            return candidates.Where(value => value != null && value.Selected)
                .Select(value => value.NpcClassId).ToArray();
        }
    }
}
