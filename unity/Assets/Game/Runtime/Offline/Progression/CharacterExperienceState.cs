using System;

namespace Zjcs.Offline.Progression
{
    // The selected game's experience queries supply this state to the local
    // transaction. The transaction continues to own award receipts and rollback.
    public readonly struct CharacterExperienceState
    {
        public int Level { get; }
        public int BaseLevel { get; }
        public bool IsBlessing { get; }
        public bool IsStoredExperience { get; }
        public long StoredExperienceLimit { get; }

        public CharacterExperienceState(int level, int baseLevel, bool blessing, bool stored, long storedExperienceLimit)
        {
            if (level < 0 || baseLevel < 0 || level < baseLevel || storedExperienceLimit < 0)
                throw new ArgumentOutOfRangeException(nameof(level));
            Level = level; BaseLevel = baseLevel; IsBlessing = blessing;
            IsStoredExperience = stored; StoredExperienceLimit = storedExperienceLimit;
        }
    }
}
