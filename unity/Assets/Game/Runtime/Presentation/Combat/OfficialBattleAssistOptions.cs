using System;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.Combat
{
    /// <summary>
    /// Mode-owned identity for the shared NPC assist transaction. The battle controller remains
    /// independent of this state; the host supplies the official play type and entry key at the seam.
    /// </summary>
    public sealed class OfficialBattleAssistOptions
    {
        public int PlayType = (int)NpcAssistPlayType.All;
        public string EntryId;
        public int MaxPlayerOverride = -1;

        public void Validate()
        {
            if (PlayType <= 0) throw new ArgumentOutOfRangeException(nameof(PlayType));
            if (!string.IsNullOrWhiteSpace(EntryId) && EntryId.Length > 256)
                throw new ArgumentException("Battle assist entry identity is too long.", nameof(EntryId));
            if (MaxPlayerOverride == 0 || MaxPlayerOverride < -1)
                throw new ArgumentOutOfRangeException(nameof(MaxPlayerOverride));
        }

        public OfficialBattleAssistOptions Clone()
        {
            return new OfficialBattleAssistOptions
            {
                PlayType = PlayType,
                EntryId = EntryId,
                MaxPlayerOverride = MaxPlayerOverride
            };
        }
    }
}
