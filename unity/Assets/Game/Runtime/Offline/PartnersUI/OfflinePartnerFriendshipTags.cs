using System;
using System.Linq;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.PartnersUI
{
    // Confirmed offline policy: original character/length checks, then local save.
    public sealed class OfflinePartnerFriendshipTags
    {
        private readonly OfflineMainlineProgressSession session;
        private readonly OfflinePartnerGiftConfiguration configuration;
        public OfflinePartnerFriendshipTags(OfflineMainlineProgressSession session, OfflinePartnerGiftConfiguration configuration)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        public string Get(int id) => session.Save.Player.CharacterProperties.NpcFriendships
            .FirstOrDefault(value => value.NpcClassId == id)?.CustomFriendshipTag ?? string.Empty;

        public int CheckText(string tag)
        {
            if (tag == null) return configuration.CheckNpcFriendshipTagLegal(tag);
            if (!configuration.IsAllCharacterValid(tag)) return 1150;
            return configuration.CheckNpcFriendshipTagLegal(tag);
        }

        public ProgressionResult Set(int id, string tag, string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId)) return Invalid("A custom-title command identity is required.");
            session.Reload();
            if (session.Save.Player.AppliedCharacterEventIds.Contains(eventId))
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            int error = CheckText(tag);
            if (error != 0) return Invalid("Official custom-title validation error: " + error);
            var info = configuration.GetNpcInfo(id);
            var npc = session.Save.Player.CharacterProperties.NpcFriendships.FirstOrDefault(value => value.NpcClassId == id);
            if (info == null || !info.CanCustomFriendshipTag || npc == null || npc.Level < configuration.CanCustomTagFriendShipLevel
                || (id >= 11015 && id <= 11022)) return Invalid("Custom titles are unavailable for this partner.");
            return session.ApplyCharacterMutationIfChanged(player => {
                var target = player.CharacterProperties.NpcFriendships.Single(value => value.NpcClassId == id);
                if (target.Level < configuration.CanCustomTagFriendShipLevel) throw new InvalidOperationException("Partner friendship changed before saving.");
                if (target.CustomFriendshipTag == tag) return false;
                target.CustomFriendshipTag = tag;
                return true;
            }, eventId);
        }
        public void SetFromPlayer(int id, string tag)
        {
            var result = Set(id, tag, "partner-tag:" + Guid.NewGuid().ToString("N"));
            if (!result.IsApplied && result.Status != ProgressionResultStatus.Unchanged)
                throw new InvalidOperationException(result.Message);
        }
        public ProgressionResult Set(int id, string tag, string eventId, Action<string> success)
        {
            if (success == null) throw new ArgumentNullException(nameof(success));
            var result = Set(id, tag, eventId);
            if (result.IsApplied || result.Status == ProgressionResultStatus.Unchanged) success(Get(id));
            return result;
        }
        private static ProgressionResult Invalid(string message) => new ProgressionResult {
            Status = ProgressionResultStatus.InvalidState, Message = message };
    }
}
