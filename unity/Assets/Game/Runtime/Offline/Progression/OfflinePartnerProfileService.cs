using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.Progression
{
    // The confirmed offline replacement for OperPlayerReceiveNpcProfileReward.
    // Read checks and the saved receipt always refer to the current character.
    public sealed class OfflinePartnerProfileService : IPartnerProfilePlayer
    {
        private readonly OfflineMainlineProgressSession session;
        public PartnerProfileRules Rules { get; }
        public event Action<int, int> Received;

        public OfflinePartnerProfileService(OfflineMainlineProgressSession session, PartnerProfileRules rules)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public bool TryGetNpcFriendshipLevel(int npcId, out int level)
            => new SavedPlayer(session.Save.Player).TryGetNpcFriendshipLevel(npcId, out level);

        public IReadOnlyCollection<int> ReceivedProfileRewards(int npcId)
            => session.Save.Player.CharacterProperties.NpcFriendships.FirstOrDefault(npc => npc.NpcClassId == npcId)
                ?.ReceivedProfileRewards.ToArray() ?? Array.Empty<int>();

        public ProgressionResult Claim(int npcId, int chapter)
        {
            ProgressionResult result;
            try { result = CommitClaim(npcId, chapter); }
            catch (IOException exception) { return Invalid(exception.Message); }
            catch (UnauthorizedAccessException exception) { return Invalid(exception.Message); }
            if (result.IsApplied) Received?.Invoke(npcId, chapter);
            return result;
        }

        private ProgressionResult CommitClaim(int npcId, int chapter)
        {
            session.Reload();
            var info = Rules.Get(npcId, chapter);
            if (info == null || npcId >= 11015 && npcId <= 11022)
                return Invalid("The original profile is unavailable in the confirmed offline scope.");
            return session.ApplyCharacterMutationIfChanged(player => {
                if (!player.CharacterCreated) throw new InvalidOperationException("A created character is required.");
                var npc = player.CharacterProperties.NpcFriendships.FirstOrDefault(value => value.NpcClassId == npcId);
                if (npc == null) throw new InvalidOperationException("Only met partners can unlock their profiles.");
                if (npc.ReceivedProfileRewards.Contains(chapter)) return false;
                if (!info.UnlockCondition.IsMet(new SavedPlayer(player)))
                    throw new InvalidOperationException("The original profile friendship condition is not met.");
                foreach (var reward in info.Reward)
                    if (!OfflineInventoryOperations.Grant(player, reward.Key, reward.Value, 0).IsApplied)
                        throw new InvalidOperationException("The original profile reward could not be saved.");
                npc.ReceivedProfileRewards.Add(chapter);
                return true;
            });
        }

        private static ProgressionResult Invalid(string message) => new ProgressionResult {
            Status = ProgressionResultStatus.InvalidState, Message = message };

        private sealed class SavedPlayer : IPartnerProfilePlayer
        {
            private readonly OfflinePlayerProgress player;
            public SavedPlayer(OfflinePlayerProgress player) { this.player = player; }
            public bool TryGetNpcFriendshipLevel(int npcId, out int level)
            {
                var npc = player.CharacterProperties.NpcFriendships.FirstOrDefault(value => value.NpcClassId == npcId);
                level = npc?.Level ?? 0;
                return npc != null;
            }
        }
    }
}
